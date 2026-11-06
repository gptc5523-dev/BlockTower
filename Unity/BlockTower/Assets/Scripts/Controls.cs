using UnityEngine;
using UnityEngine.InputSystem;

/// The mouse player's mouse and keyboard through the Input System (the editor's; the game itself is the headset's)
public class Controls
{
    static Mouse M => Mouse.current;
    static Keyboard K => Keyboard.current;

    /// The old Input Manager's "Mouse X/Y" axes: 0.1 per pixel moved
    public Vector2 Look => M != null ? M.delta.ReadValue() * 0.1f : Vector2.zero;
    public bool ClickDown => M != null && M.leftButton.wasPressedThisFrame;
    public bool ClickUp => M != null && M.leftButton.wasReleasedThisFrame;
    public bool Pressed(Key key) => K != null && K[key].wasPressedThisFrame;

    /// WASD or arrows, x = strafe, y = forward
    public Vector2 Move
    {
        get
        {
            var k = K;
            if (k == null)
                return Vector2.zero;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }
}
