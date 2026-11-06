using UnityEngine;

/// The mouse player's screen text (IMGUI): prompts, the out-meter, the result
public partial class PlayerCharacter
{
    /// How far the block under the hand sticks out of the tower, as a meter under the prompt: pale while pushing,
    /// green once it's out far enough to go round (pushing) or while pulling, with a tick at the go-round point
    void OutBar(float y)
    {
        float maxOut = tower.BlockLength;
        bool pulling = grip == Grip.Pull || (grip == Grip.None && CanPinch());
        float shown = (pulling ? Protrusion() : -Protrusion()) / maxOut;
        bool ready = pulling || fingerBlock == pushedOut;
        float w = 260f * Hud.S;
        var bar = new Rect((Screen.width - w) / 2f, y + 10f * Hud.S, w, 8f * Hud.S);
        Hud.Meter(bar, shown, ready ? Palette.Good : Palette.MeterIdle, pulling ? -1f : swingPush / maxOut);
        Hud.Text(new Rect(bar.x, bar.yMax + 2f * Hud.S, w, 18f * Hud.S), GameText.PercentOut(Mathf.Clamp01(shown)), 13f, Palette.Muted, TextAnchor.MiddleCenter);
    }

    void OnGUI()
    {
        // ponytail: IMGUI HUD, replace with a UI Toolkit HUD when the UI pass happens.
        if (Event.current.type != EventType.Repaint || vr)
            return;
        Hud.Begin();
        Hud.Crosshair(!GameOver && (hand.gameObject.activeSelf || (state == SeatState.Walking && aimedChair && chairInReach)));
        if (prompt != null && !GameOver)
        {
            float bottom = Hud.Prompt(prompt, Screen.height - 120f * Hud.S);
            if (fingerBlock && hand.gameObject.activeSelf)
                OutBar(bottom);
        }
        if (GameOver)
            Result(TowerGame.Instance);
    }

    /// The fall: who knocked it down, everyone ranked by blocks taken, the countdown to the next round
    void Result(TowerGame game)
    {
        float s = Hud.S, w = 520f * s, row = 40f * s;
        var ranked = new System.Collections.Generic.List<TablePlayer>(game.players);
        ranked.Sort((a, b) => b.taken.CompareTo(a.taken));
        float h = 150f * s + row * ranked.Count + 60f * s, x = (Screen.width - w) / 2f, y = (Screen.height - h) / 2f;
        Hud.Box(new Rect(x, y, w, h), Palette.ResultCard, 18f);
        Hud.Box(new Rect(x, y, w, 5f * s), Palette.Bad, 2f);
        Hud.Text(new Rect(x, y + 22f * s, w, 50f * s), GameText.Fell, 38f, Palette.Ink, TextAnchor.MiddleCenter);
        string who = game.Loser == this ? GameText.IToppled : game.Loser ? GameText.Toppled(game.Loser.DisplayName) : "";
        Hud.Text(new Rect(x, y + 74f * s, w, 30f * s), who, 18f, Palette.Bad, TextAnchor.MiddleCenter);
        float ry = y + 120f * s;
        for (int i = 0; i < ranked.Count; i++)
        {
            var p = ranked[i];
            if (i % 2 == 0)
                Hud.Box(new Rect(x + 24f * s, ry, w - 48f * s, row), Palette.ResultRow, 8f);
            Hud.Text(new Rect(x + 40f * s, ry, 30f * s, row), $"{i + 1}", 18f, Palette.Muted);
            if (p.seat >= 0)
                Hud.Box(new Rect(x + 72f * s, ry + row / 2f - 6f * s, 12f * s, 12f * s), Palette.Seats[p.seat], 6f);
            string name = p.DisplayName + (p == this ? GameText.MeMark : "");
            Hud.Text(new Rect(x + 94f * s, ry, w, row), name, 18f, Palette.Ink);
            if (p == game.Loser)
                Hud.Tag(new Vector2(x + 102f * s + Hud.Width(name, 18f), ry + row / 2f - 10f * s), GameText.Lost, Palette.Bad, Palette.Ink);
            Hud.BlockIcon(new Vector2(x + w - 100f * s, ry + row / 2f - 7.5f * s));
            Hud.Text(new Rect(x + w - 76f * s, ry, 40f * s, row), $"{p.taken}", 18f, Palette.Ink, TextAnchor.MiddleRight);
            ry += row;
        }
        float left = Mathf.Max(0f, game.SecondsToReset);
        var bar = new Rect(x + 40f * s, y + h - 44f * s, w - 80f * s, 6f * s);
        Hud.Meter(bar, 1f - left / game.resetDelay, Palette.Amber);
        Hud.Text(new Rect(x, bar.yMax + 4f * s, w, 24f * s), GameText.NewRoundIn(Mathf.CeilToInt(left)), 14f, Palette.Muted, TextAnchor.MiddleCenter);
    }
}
