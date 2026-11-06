using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Hands;

/// VR multiplayer (docs/superpowers/specs/2026-11-06-quest-multiplayer-design.md): the Quest talks to the VR server
/// (Server/server.js) for rooms, turns and relaying. The player whose turn it is runs the tower here as in single
/// play and sends the blocks 30 times a second; the others draw them (kinematic) and send their head and hands.
/// The turn passes with the tower as it stands, every block put to sleep so it stands as it stood
public partial class PlayerCharacter
{
    [Header("VR multiplayer")]
    /// The VR server (Server/server.js) on the office LAN
    public string serverUrl = "ws://192.168.0.167:3010";

    NetClient net;
    NetMsg vrRoom;               // the room joined, till its game starts
    float vrRoomAt;              // when that message came (its countdown counts from it)
    bool vrNetGame;              // in a networked game
    readonly Dictionary<int, RemotePlayer> remotes = new();
    bool vrAuthority = true;     // this Quest runs the tower
    float vrPoseSent, vrBlocksSent, vrFullSent;
    Pose[] vrSentBlocks;         // each block as last sent
    readonly Dictionary<int, Pose> vrBlockTargets = new();
    float vrTakenAt = -1f;       // took a block this turn: the turn passes once the tower is still
    bool vrPassSent;             // and the server was told, waiting for its turn message
    float vrStillFor;
    int vrTurnLimit;
    float vrTurnAt;
    int vrOnline;

    /// Chairs at the table, and the fewest a game takes (Server/server.js SEATS)
    const int SeatCount = 4, MinPlayers = 2;
    /// Poses and blocks sent a second
    const float NetHz = 30f;
    /// A block moved less than this since last sent isn't sent again, m and deg
    const float SendMove = 0.0005f, SendTurn = 0.5f;
    /// After taking a block: the tower still this long passes the turn, or at most this long after the take, s
    const float StillToPass = 1f, MostToPass = 3f;
    /// How fast drawn blocks close on the received ones, 1/s
    const float BlockFollow = 30f;
    /// Numbers a block takes in a message: its index, pose, 1 if off the tower
    const int BlockStride = NetPose.Stride + 2;

    void StartNet()
    {
        // a test server instead: its URL in server.txt in the app's files (adb push … files/server.txt)
        string url = serverUrl;
        string over = System.IO.Path.Combine(Application.persistentDataPath, "server.txt");
        if (System.IO.File.Exists(over))
            url = System.IO.File.ReadAllText(over).Trim();
        net = new NetClient(url, SystemInfo.deviceName);
        RemotePlayer.Viewer = view.transform;
        TowerGame.Instance.Fell = p =>
        {
            if (!vrNetGame || p != (TablePlayer)this)
                return;
            SendBlocks(true);
            net.Send(new NetMsg { t = NetType.Fell });
        };
    }

    void UpdateNet()
    {
        net.Update();
        while (net.TryReceive(out var m))
            OnNet(m);
        if (!SessionFocus.Visible)
            HeadsetOff();
        if (!vrNetGame)
            return;
        if (Time.time - vrPoseSent >= 1f / NetHz)
        {
            vrPoseSent = Time.time;
            SendPose();
        }
        if (vrAuthority)
        {
            if (Time.time - vrBlocksSent >= 1f / NetHz)
                SendBlocks(Time.time - vrFullSent > 1f);
            PassWhenStill();
        }
        else
            FollowBlocks();
    }

    void OnNet(NetMsg m)
    {
        switch (m.t)
        {
            case NetType.Lobby:
                vrOnline = m.online;
                break;
            case NetType.Room:
                vrRoom = m;
                vrRoomAt = Time.time;
                if (vrStart && vrStartFace != vrStartRoom)
                    ShowFace(vrStartRoom);
                break;
            case NetType.Error:
                if (vrStartFace == vrStartCode)
                    vrCode = "";
                StartNote(m.msg);
                break;
            case NetType.Start:
                BeginNet(m);
                break;
            case NetType.Pose:
                if (remotes.TryGetValue(m.seat, out var who))
                    who.Pose(m);
                break;
            case NetType.Blocks:
                if (vrNetGame && !vrAuthority)
                    ApplyBlocks(m.s, false);
                break;
            case NetType.Turn:
                OnTurn(m);
                break;
            case NetType.Over:
                if (vrNetGame)
                    TowerGame.Instance.NetOver(PlayerAt(m.loser));
                break;
            case NetType.Round:
                if (vrNetGame)
                    NetRound(m.seed, m.turn);
                break;
            case NetType.Left:
                if (remotes.Remove(m.seat, out var gone))
                {
                    TowerGame.Instance.Announce(GameText.Left(gone.DisplayName));
                    Destroy(gone.gameObject);
                }
                break;
            case NetType.End:
                EndNet(m.msg);
                break;
            case NetType.Closed:
                if (vrNetGame || vrRoom != null)
                    EndNet(GameText.ServerGone);
                break;
        }
    }

    TablePlayer PlayerAt(int at) => at == seat && vrNetGame ? this : remotes.TryGetValue(at, out var p) ? (TablePlayer)p : null;

    /// The chairs in a fixed order round the tower, the same on every Quest: seat n sits on the n-th
    DiningChair[] SeatChairs()
    {
        Vector3 t = tower.transform.position;
        return chairs.OrderBy(c => Mathf.Atan2(c.SeatPosition.z - t.z, c.SeatPosition.x - t.x)).ToArray();
    }

    /// The server started the room's game: everyone to their seat, a tower from the shared seed
    void BeginNet(NetMsg m)
    {
        int mine = vrRoom != null ? vrRoom.seat : -1;
        if (mine < 0)
            return;
        HideStart();
        vrRoom = null;
        vrNetGame = true;
        var seats = SeatChairs();
        seat = mine;
        SitAtTable(seats[mine % seats.Length]);
        foreach (var member in m.members)
            if (member.seat != mine)
                remotes[member.seat] = RemotePlayer.Make(member.seat, seats[member.seat % seats.Length], leftHandModel, vrHandModel,
                    RemotePlayer.Shirt(m.seed, member.seat));
        vrTurnLimit = m.limit;
        NetRound(m.seed, m.turn);
        Debug.Log($"[BT] net game: seat {seat}, {m.members.Length} players, seed {m.seed}");
    }

    void NetRound(int seed, int first)
    {
        TowerGame.Instance.NetRound(true, seed, first);
        vrSentBlocks = null;
        vrTakenAt = -1f;
        vrPassSent = false;
        vrTurnAt = Time.time;
        SetAuthority(first == seat, null);
    }

    /// The server passed the turn
    void OnTurn(NetMsg m)
    {
        if (!vrNetGame)
            return;
        var game = TowerGame.Instance;
        var from = PlayerAt(m.from);
        if (m.took && from && !from.Local)
        {
            from.taken++;
            game.Announce(GameText.Took(from.DisplayName));
        }
        if (m.late && from)
            game.Announce(GameText.OutOfTime(from.DisplayName));
        bool mine = m.seat == seat;
        if (vrAuthority && !mine)
        {
            // out of time with a block in hand: it stays where it is
            ReleaseVR();
            DropCommitted();
            moving = null;
        }
        vrTakenAt = -1f;
        vrPassSent = false;
        vrTurnAt = Time.time;
        vrTurnLimit = m.limit;
        SetAuthority(mine, m.s);
        game.NetTurn(m.seat);
    }

    /// This Quest runs the tower (`on`) or draws it as sent. Taking over, the blocks go where the last holder left
    /// them and sleep: untouched, they stand as they stood
    void SetAuthority(bool on, float[] tower_)
    {
        if (tower_ != null && tower_.Length > 0)
            ApplyBlocks(tower_, true);
        vrAuthority = on;
        vrBlockTargets.Clear();
        vrSentBlocks = null;
        foreach (var b in tower.blocks)
        {
            if (!b)
                continue;
            b.isKinematic = !on;
            if (on)
                b.Sleep();
        }
    }

    /// Blocks as sent: straight there (`snap`), or as targets the drawn ones close on
    void ApplyBlocks(float[] s, bool snap)
    {
        if (s == null)
            return;
        for (int at = 0; at + BlockStride <= s.Length; at += BlockStride)
        {
            int i = (int)s[at];
            if (i < 0 || i >= tower.blocks.Count || !tower.blocks[i])
                continue;
            var b = tower.blocks[i];
            var pose = NetPose.Read(s, at + 1);
            if (s[at + BlockStride - 1] > 0.5f && b.transform.parent == tower.transform)
                b.transform.SetParent(null, true);  // taken: no longer part of the tower here either
            if (snap)
                Scenery.Teleport(b, pose.position, pose.rotation);
            else
                vrBlockTargets[i] = pose;
        }
    }

    void FollowBlocks()
    {
        float k = Scenery.Closing(BlockFollow);
        foreach (var (i, pose) in vrBlockTargets)
        {
            var b = i < tower.blocks.Count ? tower.blocks[i] : null;
            if (b)
                b.transform.SetPositionAndRotation(Vector3.Lerp(b.transform.position, pose.position, k),
                    Quaternion.Slerp(b.transform.rotation, pose.rotation, k));
        }
    }

    /// The blocks that moved since last sent (all of them: `full`, once a second and when the turn passes)
    void SendBlocks(bool full)
    {
        var blocks = tower.blocks;
        if (vrSentBlocks == null || vrSentBlocks.Length != blocks.Count)
        {
            vrSentBlocks = new Pose[blocks.Count];
            full = true;
        }
        var s = new List<float>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (!b)
                continue;
            var last = vrSentBlocks[i];
            if (!full && (b.position - last.position).sqrMagnitude < SendMove * SendMove && Quaternion.Angle(b.rotation, last.rotation) < SendTurn)
                continue;
            vrSentBlocks[i] = new Pose(b.position, b.rotation);
            s.Add(i);
            s.Add(b.position.x); s.Add(b.position.y); s.Add(b.position.z);
            s.Add(b.rotation.x); s.Add(b.rotation.y); s.Add(b.rotation.z); s.Add(b.rotation.w);
            s.Add(b.transform.parent == tower.transform ? 0f : 1f);
        }
        vrBlocksSent = Time.time;
        if (full)
            vrFullSent = Time.time;
        if (s.Count > 0 || full)
            net.Send(new NetMsg { t = NetType.Blocks, full = full, s = s.ToArray() });
    }

    /// Took a block (CollectTaken): once the tower is still, pass the turn with it as it stands. Falling meanwhile,
    /// this player is the one who knocked it down
    void PassWhenStill()
    {
        if (vrTakenAt < 0f || vrPassSent || TowerGame.Instance.Over)
            return;
        bool still = tower.blocks.TrueForAll(b => !b || b.isKinematic || b.IsSleeping() || TowerSpawner.Still(b));
        vrStillFor = still ? vrStillFor + Time.deltaTime : 0f;
        if (vrStillFor < StillToPass && Time.time - vrTakenAt < MostToPass)
            return;
        SendBlocks(true);
        net.Send(new NetMsg { t = NetType.Taken });
        vrPassSent = true;
        vrStillFor = 0f;
    }

    /// The head and both hands as drawn here, world space
    void SendPose()
    {
        var m = new NetMsg { t = NetType.Pose, h = new float[NetPose.Stride] };
        NetPose.Write(m.h, 0, view.transform.position, view.transform.rotation);
        m.l = HandPose(vrHands[0]);
        m.r = HandPose(vrHands[1]);
        net.Send(m);
    }

    static float[] HandPose(VrHand h)
    {
        if (h == null || !h.real || !h.real.activeSelf || h.joints == null)
            return new float[0];
        var a = new float[h.joints.Length * NetPose.Stride];
        for (int j = 0; j < h.joints.Length; j++)
            if (h.joints[j])
                NetPose.Write(a, j * NetPose.Stride, h.joints[j].position, h.joints[j].rotation);
            else
                NetPose.Write(a, j * NetPose.Stride, Vector3.zero, Quaternion.identity);
        return a;
    }

    /// Out of the room or game (left, everyone else left, or the server went away): single play, the start menu
    void EndNet(string why)
    {
        foreach (var r in remotes.Values)
            if (r)
                Destroy(r.gameObject);
        remotes.Clear();
        CloseMenu();
        bool was = vrNetGame;
        vrNetGame = false;
        vrRoom = null;
        vrTakenAt = -1f;
        seat = -1;
        if (was)
        {
            TowerGame.Instance.NetRound(false, 0, -1);
            SetAuthority(true, null);
        }
        if (!vrStart)
            ShowStart();
        else
            ShowFace(vrStartMain);
        StartNote(why);
        Debug.Log($"[BT] net game over: {why}");
    }

    /// Leave the room or game for the start menu (the room's 나가기, the menu's 처음 메뉴로)
    void LeaveNet()
    {
        net.Send(new NetMsg { t = NetType.Leave });
        EndNet("");
    }

    /// The headset came off: out of the room or game at once, there's no holding up the others. If the message doesn't
    /// get out before the app stops, the server drops the silent Quest at its next ping
    void HeadsetOff()
    {
        if (net == null || (!vrNetGame && vrRoom == null))
            return;
        net.Send(new NetMsg { t = NetType.Leave });
        EndNet(GameText.HeadsetOff);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            HeadsetOff();
    }

    /// The start menu's room face: who's in, the code, the countdown
    void RoomNote()
    {
        var r = vrRoom;
        if (r == null)
            return;
        bool host = r.seat == r.host && !r.random;
        vrStartGo.block.gameObject.SetActive(host);
        vrStartGo.ready = host && r.members.Length >= MinPlayers;
        string list = string.Join("\n", r.members.Select(m =>
            $"{GameText.Player(m.seat)}{(m.seat == r.host && !r.random ? GameText.Host : "")}{(m.seat == r.seat ? GameText.You : "")}"));
        string head;
        if (r.random)
        {
            int left = r.countdown < 0 ? -1 : Mathf.Max(0, r.countdown - Mathf.FloorToInt(Time.time - vrRoomAt));
            head = GameText.SearchingCount(r.members.Length, SeatCount) + (left >= 0 ? "\n" + GameText.StartsIn(left) : "");
        }
        else
            head = GameText.RoomCode(r.code) + "\n" + (host ? r.members.Length >= MinPlayers ? GameText.HostStarts : GameText.TellCode
                : GameText.WaitForHost);
        StartNote(head + "\n\n" + list);
    }

    /// The main face's 멀티 block: how many Quests are on the server, or that it can't be reached
    void MultiLabel() => vrStartMultiKey.label.text = net.Connected ? GameText.MultiOnline(vrOnline) : GameText.MultiNoServer;

    /// The turn's time left, s (networked)
    int SecondsLeft => Mathf.CeilToInt(Mathf.Max(0f, vrTurnLimit - (Time.time - vrTurnAt)));
}
