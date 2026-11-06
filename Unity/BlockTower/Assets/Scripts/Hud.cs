using UnityEngine;

/// The look of the in-game UI (IMGUI): warm dark translucent cards like the dining room's wood, amber for "your
/// move", keys drawn as keycaps. Laid out for a 900 px high screen and scaled to the real one.
public static class Hud
{
    static GUIStyle style;
    /// Pixels per layout unit
    public static float S => Screen.height / 900f;
    /// Multiplies every colour drawn: fades
    public static float alpha = 1f;

    public static void Begin()
    {
        style ??= new GUIStyle { font = Scenery.Font, wordWrap = false, richText = false };
        alpha = 1f;
    }

    static Color Fade(Color c) => new(c.r, c.g, c.b, c.a * alpha);

    public static void Box(Rect r, Color c, float radius = 10f) =>
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Fade(c), 0f, radius * S);

    /// Text in a box, with a soft shadow so it holds on the pale table
    public static void Text(Rect r, string text, float size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool shadow = true)
    {
        style.fontSize = Mathf.RoundToInt(size * S);
        style.alignment = anchor;
        if (shadow)
        {
            style.normal.textColor = Fade(Palette.TextShadow);
            GUI.Label(new Rect(r.x + 1.5f * S, r.y + 1.5f * S, r.width, r.height), text, style);
        }
        style.normal.textColor = Fade(c);
        GUI.Label(r, text, style);
    }

    public static float Width(string text, float size)
    {
        style.fontSize = Mathf.RoundToInt(size * S);
        return style.CalcSize(new GUIContent(text)).x;
    }

    /// A dot; with a ring round it while something is under it to act on
    public static void Crosshair(bool onTarget)
    {
        var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
        float d = 5f * S;
        Box(new Rect(c.x - d / 2f - S, c.y - d / 2f - S, d + 2f * S, d + 2f * S), Palette.CrosshairBack, d);
        Box(new Rect(c.x - d / 2f, c.y - d / 2f, d, d), Palette.Ink, d);
        if (!onTarget)
            return;
        float ring = 22f * S, w = 2f * S;
        GUI.DrawTexture(new Rect(c.x - ring / 2f, c.y - ring / 2f, ring, ring), Texture2D.whiteTexture, ScaleMode.StretchToFill,
            true, 0f, Fade(Palette.Amber), w, ring / 2f);
    }

    /// The hint at the bottom: "[클릭] 앉기   [E] 일어서기" draws each [key] as a keycap, the rest as text
    public static float Prompt(string prompt, float y)
    {
        const float size = 19f, pad = 9f;
        var parts = Parse(prompt);
        float w = 0f;
        foreach (var (key, text) in parts)
            w += (key ? Width(text, size - 3f) + 2f * pad * S : Width(text, size)) + 6f * S;
        w += 30f * S;
        float h = 44f * S, x = (Screen.width - w) / 2f + 15f * S;
        Box(new Rect((Screen.width - w) / 2f, y, w, h), Palette.Card, 22f);
        foreach (var (key, text) in parts)
        {
            if (key)
            {
                float kw = Width(text, size - 3f) + 2f * pad * S;
                Box(new Rect(x, y + 9f * S, kw, h - 18f * S), Palette.KeyCap, 6f);
                Text(new Rect(x, y, kw, h), text, size - 3f, Palette.KeyCapInk, TextAnchor.MiddleCenter, false);
                x += kw + 6f * S;
            }
            else
            {
                Text(new Rect(x, y, Width(text, size) + 4f * S, h), text, size, Palette.Ink);
                x += Width(text, size) + 6f * S;
            }
        }
        return y + h;
    }

    static System.Collections.Generic.List<(bool key, string text)> Parse(string s)
    {
        var parts = new System.Collections.Generic.List<(bool, string)>();
        int i = 0;
        while (i < s.Length)
        {
            int open = s.IndexOf('[', i);
            if (open < 0)
            {
                parts.Add((false, s[i..]));
                break;
            }
            if (open > i)
                parts.Add((false, s[i..open]));
            int close = s.IndexOf(']', open);
            parts.Add((true, s[(open + 1)..close]));
            i = close + 1;
        }
        return parts;
    }

    /// A rounded meter: fill 0..1 in the given colour, an optional tick
    public static void Meter(Rect r, float fill, Color c, float tick = -1f)
    {
        Box(r, Palette.MeterBack, r.height / 2f);
        if (fill > 0f)
            Box(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(fill)), r.height), c, r.height / 2f);
        if (tick >= 0f)
            Box(new Rect(r.x + r.width * tick - S, r.y - 3f * S, 2f * S, r.height + 6f * S), Palette.Ink, 0f);
    }

    /// A little block, for counts
    public static void BlockIcon(Vector2 at)
    {
        Box(new Rect(at.x, at.y, 18f * S, 7f * S), Palette.IconMaple, 2f);
        Box(new Rect(at.x, at.y + 8f * S, 18f * S, 7f * S), Palette.IconWalnut, 2f);
    }

    /// A pill tag
    public static float Tag(Vector2 at, string text, Color back, Color ink)
    {
        float w = Width(text, 13f) + 14f * S, h = 20f * S;
        Box(new Rect(at.x, at.y, w, h), back, 10f);
        Text(new Rect(at.x, at.y, w, h), text, 13f, ink, TextAnchor.MiddleCenter, false);
        return w;
    }
}
