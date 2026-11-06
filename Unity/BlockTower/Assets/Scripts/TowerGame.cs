using System.Collections.Generic;
using UnityEngine;

/// The round at the table: whose turn it is, who took how many, the fall and the next tower. Alone it's always the
/// local player's turn; networked the server says whose. Whoever's turn it was when the tower fell loses.
public class TowerGame : MonoBehaviour
{
    public static TowerGame Instance { get; private set; }

    /// Long enough to read who lost and the scores, and watch the tower come down
    public float resetDelay = 6f;

    public readonly List<TablePlayer> players = new();
    public bool Over { get; private set; }
    public TablePlayer Loser { get; private set; }
    public float SecondsToReset => (Networked ? NetRoundDelay : resetDelay) - (Time.time - overTime);
    /// Networked, the server's wait from the fall to the next round, s (Server/server.js ROUND_DELAY)
    public const float NetRoundDelay = 10f;
    float overTime, overReal;

    /// The fall plays in slow motion for everyone (one shared world), easing back to normal speed
    public float slowMotion = 0.3f, slowSeconds = 2.5f;
    /// 1 at the fall, dying away: how hard the views shake
    public float Shake => Over ? Mathf.Clamp01(1f - (Time.unscaledTime - overReal) / 0.8f) : 0f;

    /// Alone at the table with a menu up: nothing at the table moves or counts down. Never in a networked game
    public bool Paused
    {
        get => paused;
        set
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
        }
    }
    bool paused;

    /// How long a note stays up (s)
    public const float NoteSeconds = 2.5f;
    public string Note { get; private set; }
    public float NoteTime { get; private set; } = -10f;

    public void Announce(string note)
    {
        Note = note;
        NoteTime = Time.time;
    }
    TowerSpawner tower;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create() => Instance = new GameObject("Tower Game").AddComponent<TowerGame>();

    void Awake() => tower = FindAnyObjectByType<TowerSpawner>();

    /// VR multiplayer: the server keeps the turns and starts the rounds (NetTurn, NetOver, NetRound); the player
    /// whose turn it is simulates the tower and alone checks it for a fall
    public bool Networked { get; private set; }
    int netTurn = -1;
    /// A networked game: the local player knocked the tower down (it tells the server)
    public System.Action<TablePlayer> Fell;

    /// The player who may touch the tower now (the local player joined first)
    public TablePlayer Current => Networked ? players.Find(p => p.seat == netTurn)
        : players.Count > 0 ? players[0] : null;

    public bool IsTurn(TablePlayer p) => !Over && Current == p;

    public void Join(TablePlayer p) => players.Add(p);

    public void Leave(TablePlayer p) => players.Remove(p);

    /// p's block left the tower and went to its pile
    public void Taken(TablePlayer p)
    {
        p.taken++;
        Announce(GameText.Took(p.DisplayName));
    }

    /// Into or out of a networked game: a new tower from `seed` (the same on every Quest), `first` to play
    public void NetRound(bool networked, int seed, int first)
    {
        Networked = networked;
        tower.seed = seed;
        if (networked)
            TowerSpawner.Practice = false;
        netTurn = first;
        NewRound();
    }

    /// The server gave the turn to `seat`
    public void NetTurn(int seat) => netTurn = seat;

    /// Another Quest's player knocked the tower down (the server says)
    public void NetOver(TablePlayer loser)
    {
        if (Over)
            return;
        GameOver(loser);
    }

    void GameOver(TablePlayer loser)
    {
        Over = true;
        overTime = Time.time;
        overReal = Time.unscaledTime;
        Loser = loser;
        foreach (var p in players)
            p.TowerFell();
        Debug.Log($"[BT] Game over: {(Loser ? Loser.DisplayName : "nobody")} toppled it");
    }

    void Update()
    {
        if (!tower || players.Count == 0 || Paused)
            return;
        // networked, only the Quest whose turn it is runs the tower
        if (!Over && (!Networked || (Current && Current.Local)) && tower.Fallen(Current ? Current.Moving : null))
        {
            GameOver(Current);
            if (Networked)
                Fell?.Invoke(Current);
        }
        if (Over)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - overReal) / slowSeconds);
            Time.timeScale = Mathf.Lerp(slowMotion, 1f, t * t);
        }
        if (Over && SecondsToReset <= 0f && !Networked)  // networked, the server starts the next round
            NewRound();
    }

    /// Someone chose 재시작 in the menu: a new round for the whole table now
    public void Restart(TablePlayer by)
    {
        if (Over)
            return;  // one is coming anyway
        NewRound();
        Announce(GameText.Restarted(by.DisplayName));
        Debug.Log($"[BT] restart by {by.DisplayName}");
    }

    /// Clears the piles and scores and stacks a new tower
    void NewRound()
    {
        foreach (var p in players)
        {
            p.NewRound();
            p.taken = 0;
        }
        foreach (var c in FindObjectsByType<DiningChair>())
            c.taken = 0;
        tower.Rebuild();
        Over = false;
        Loser = null;
        Time.timeScale = 1f;
        Announce(GameText.NewRound);
    }
}
