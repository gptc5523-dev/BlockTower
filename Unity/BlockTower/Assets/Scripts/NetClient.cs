using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// One seat in a room, as the server lists them
[Serializable]
public class NetMember
{
    public int seat;
}

/// The messages' `t`, as Server/server.js has them
public static class NetType
{
    public const string Hello = "hello", Lobby = "lobby", Room = "room", Error = "error", Start = "start", Pose = "pose",
        Blocks = "blocks", Turn = "turn", Over = "over", Round = "round", Left = "left", End = "end", Closed = "_closed",
        Random = "random", Create = "create", Join = "join", Leave = "leave", Taken = "taken", Fell = "fell";
}

/// Poses as numbers in a message: position xyz, rotation xyzw
public static class NetPose
{
    /// Numbers a pose takes
    public const int Stride = 7;

    public static void Write(float[] a, int at, Vector3 p, Quaternion r)
    {
        a[at] = p.x; a[at + 1] = p.y; a[at + 2] = p.z;
        a[at + 3] = r.x; a[at + 4] = r.y; a[at + 5] = r.z; a[at + 6] = r.w;
    }

    public static Pose Read(float[] a, int at) =>
        new(new Vector3(a[at], a[at + 1], a[at + 2]), new Quaternion(a[at + 3], a[at + 4], a[at + 5], a[at + 6]));
}

/// Every message to and from the VR server (Server/server.js), one class for all: JsonUtility reads only fixed
/// fields. Absent numbers are -1 (or 0), absent arrays empty. Blocks `s`: 9 numbers a block (index, position xyz,
/// rotation xyzw, 1 if off the tower); head `h`: position xyz, rotation xyzw; hands `l`, `r`: the 26 joints the same
/// way (empty: not tracked)
[Serializable]
public class NetMsg
{
    public string t, name, code, msg;
    public int seat = -1, host = -1, turn = -1, from = -1, loser = -1, online, countdown = -1, seed, limit;
    public bool random, full, took, late;
    public NetMember[] members;
    public float[] h, l, r, s;
}

/// The Quest's line to the VR server: a WebSocket that keeps trying while the server is away. Received messages
/// queue up for the main thread (TryReceive); "_closed" says the line dropped
public class NetClient
{
    readonly string url, name;
    ClientWebSocket ws;
    readonly ConcurrentQueue<string> inbox = new();
    BlockingCollection<string> outbox;
    bool connecting;
    float retryAt;

    /// Seconds between tries while the server can't be reached
    const float Retry = 5f;

    public NetClient(string url, string name)
    {
        this.url = url;
        this.name = name;
    }

    public bool Connected => ws != null && ws.State == WebSocketState.Open;

    /// Call every frame: connects, and connects again after a drop
    public void Update()
    {
        if (!Connected && !connecting && Time.unscaledTime >= retryAt)
            Connect();
    }

    async void Connect()
    {
        connecting = true;
        retryAt = Time.unscaledTime + Retry;
        var socket = new ClientWebSocket();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await socket.ConnectAsync(new Uri(url), timeout.Token);
        }
        catch (Exception e)
        {
            socket.Dispose();
            connecting = false;
            if (ws == null)
                Debug.Log($"[BT] net: no server at {url} ({e.GetType().Name})");
            return;
        }
        ws = socket;
        outbox = new BlockingCollection<string>();
        connecting = false;
        Debug.Log($"[BT] net: connected to {url}");
        var box = outbox;
        _ = Task.Run(() => SendLoop(socket, box));
        _ = Task.Run(() => ReceiveLoop(socket, box));
        Send(new NetMsg { t = NetType.Hello, name = name });
    }

    public void Send(NetMsg m)
    {
        if (Connected && !outbox.IsAddingCompleted)
            outbox.Add(JsonUtility.ToJson(m));
    }

    /// The next message from the server, on the main thread
    public bool TryReceive(out NetMsg m)
    {
        m = null;
        if (!inbox.TryDequeue(out var text))
            return false;
        m = JsonUtility.FromJson<NetMsg>(text);
        return m != null;
    }

    public void Close()
    {
        outbox?.CompleteAdding();
        try { ws?.Abort(); } catch { }
        ws = null;
    }

    static async Task SendLoop(ClientWebSocket socket, BlockingCollection<string> box)
    {
        try
        {
            foreach (var text in box.GetConsumingEnumerable())
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception) { }  // the receive loop reports the drop
    }

    async Task ReceiveLoop(ClientWebSocket socket, BlockingCollection<string> box)
    {
        var buffer = new byte[64 * 1024];
        var message = new System.IO.MemoryStream();  // bytes till the message ends: a character can span two reads
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var got = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (got.MessageType == WebSocketMessageType.Close)
                    break;
                message.Write(buffer, 0, got.Count);
                if (!got.EndOfMessage)
                    continue;
                inbox.Enqueue(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                message.SetLength(0);
            }
        }
        catch (Exception) { }
        box.CompleteAdding();
        if (ws == socket)
            inbox.Enqueue(JsonUtility.ToJson(new NetMsg { t = NetType.Closed }));
        socket.Dispose();
    }
}
