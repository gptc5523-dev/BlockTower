using UnityEngine;

/// Every colour the game draws, by what it's for. Colours above 1 are tints that brighten what they multiply
public static class Palette
{
    // the mouse player's screen text: warm dark translucent cards like the dining room's wood, amber for "your move"
    public static readonly Color Card = new(0.09f, 0.07f, 0.05f, 0.78f);
    public static readonly Color ResultCard = new(0.09f, 0.07f, 0.05f, 0.88f);
    public static readonly Color ResultRow = new(1f, 1f, 1f, 0.04f);
    public static readonly Color Ink = new(0.98f, 0.96f, 0.92f);
    public static readonly Color Muted = new(0.72f, 0.67f, 0.6f);
    public static readonly Color Amber = new(1f, 0.72f, 0.3f);
    public static readonly Color Good = new(0.45f, 0.85f, 0.5f);
    public static readonly Color Bad = new(0.95f, 0.4f, 0.35f);
    public static readonly Color TextShadow = new(0f, 0f, 0f, 0.6f);
    public static readonly Color CrosshairBack = new(0f, 0f, 0f, 0.5f);
    public static readonly Color MeterBack = new(0f, 0f, 0f, 0.55f);
    public static readonly Color MeterIdle = new(0.9f, 0.88f, 0.84f);
    public static readonly Color KeyCap = new(0.95f, 0.92f, 0.86f, 0.95f);
    public static readonly Color KeyCapInk = new(0.15f, 0.12f, 0.1f);
    public static readonly Color IconMaple = new(0.93f, 0.78f, 0.52f);
    public static readonly Color IconWalnut = new(0.82f, 0.66f, 0.42f);

    // the blocks and chairs: multiplied onto what they tint
    public static readonly Color NoTint = Color.white;
    public static readonly Color AllowedTint = new(0.55f, 0.95f, 1.9f);
    public static readonly Color ForbiddenTint = new(1.6f, 0.45f, 0.4f);
    /// The block touched first this turn, red for everyone till it's out (touch-move house rule)
    public static readonly Color CommittedTint = new(1.5f, 0.45f, 0.4f);

    // the seats: name tags
    public static readonly Color[] Seats =
        { new(0.9f, 0.3f, 0.25f), new(0.25f, 0.55f, 0.95f), new(0.3f, 0.8f, 0.35f), new(0.95f, 0.75f, 0.2f) };

    // the headset's menus
    public static readonly Color MenuText = Color.white;
    public static readonly Color MenuFaint = new(0.7f, 0.7f, 0.7f);
    public static readonly Color MenuButton = new(0.25f, 0.25f, 0.27f);
    public static readonly Color MenuQuit = new(0.75f, 0.22f, 0.17f);
    public static readonly Color MenuPage = new(0.12f, 0.12f, 0.13f);
    public static readonly Color SwingButton = new(0.2f, 0.5f, 1f);
    /// The start menu's label on a maple block (on walnut: Ink)
    public static readonly Color OnMaple = new(0.23f, 0.15f, 0.08f);

    // the people at the table
    /// The mouse player's hand (the sample model)
    public static readonly Color Skin = new(0.86f, 0.66f, 0.55f);
    public static readonly Color RobotHead = new(0.82f, 0.83f, 0.85f);
    public static readonly Color Visor = new(0.06f, 0.065f, 0.07f);
    /// T-shirts that sit with the wood and the dining room: navy, forest green, burgundy, mustard, teal, terracotta,
    /// charcoal, dusty blue
    public static readonly Color[] Shirts =
    {
        new(0.16f, 0.22f, 0.36f), new(0.18f, 0.33f, 0.24f), new(0.45f, 0.14f, 0.18f), new(0.80f, 0.62f, 0.20f),
        new(0.16f, 0.42f, 0.44f), new(0.72f, 0.36f, 0.24f), new(0.24f, 0.25f, 0.27f), new(0.42f, 0.53f, 0.66f),
    };
    /// A collar is the shirt this much as bright
    public const float CollarShade = 0.75f;

    // the dining room, as SceneBuilder makes it
    public static readonly Color RawWood = Color.white;
    /// Oiled: warmer than the raw scan
    public static readonly Color OiledWalnut = new(1.25f, 1f, 0.8f);
    public static readonly Color MapleBlock = new(1.7f, 1.6f, 1.4f);
    public static readonly Color Wallpaper = new(1.02f, 0.98f, 0.92f);
    public static readonly Color Trim = new(0.95f, 0.94f, 0.92f);
    public static readonly Color AmbientSky = new(0.36f, 0.34f, 0.32f);
    public static readonly Color AmbientEquator = new(0.68f, 0.64f, 0.58f);
    public static readonly Color AmbientGround = new(0.3f, 0.26f, 0.21f);
    public static readonly Color WarmLight = new(1f, 0.93f, 0.82f);
}
