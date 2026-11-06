using UnityEngine;

/// Small helpers for what the scripts build and move at run time
public static class Scenery
{
    /// Korean-capable font (Resources), for the HUD and the headset's text
    public const string FontPath = "Fonts/Pretendard-Bold";
    static Font font;
    public static Font Font => font ? font : font = Resources.Load<Font>(FontPath);

    /// Text in the world, in the game's font, centred
    public static TextMesh Label(Transform parent, float size, Color color, string name = "VR Text")
    {
        var text = new GameObject(name).AddComponent<TextMesh>();
        text.transform.SetParent(parent, false);
        text.font = Font;
        text.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
        text.fontSize = 64;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        return text;
    }

    /// A shape to look at, nothing to bump into
    public static Transform Solid(PrimitiveType type, Transform parent = null)
    {
        var shape = GameObject.CreatePrimitive(type);
        Object.Destroy(shape.GetComponent<Collider>());
        shape.transform.SetParent(parent, false);
        return shape.transform;
    }

    /// A shape in one colour, nothing to bump into
    public static Transform Solid(PrimitiveType type, Color color, Transform parent = null)
    {
        var shape = Solid(type, parent);
        shape.GetComponent<Renderer>().material.color = color;
        return shape;
    }

    /// A body put straight there, not swept on the way
    public static void Teleport(Rigidbody body, Vector3 at, Quaternion turn)
    {
        body.transform.SetPositionAndRotation(at, turn);
        body.position = at;
        body.rotation = turn;
    }

    /// How far to close on a target this frame, at `rate` per second, whatever the frame rate
    public static float Closing(float rate) => 1f - Mathf.Exp(-rate * Time.deltaTime);
}
