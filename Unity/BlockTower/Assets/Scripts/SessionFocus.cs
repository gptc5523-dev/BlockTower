using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;

/// How the headset's OpenXR session stands, which Unity doesn't tell the game: the system menu up leaves the app
/// drawn but not focused, the headset off leaves it not even visible. Off a headset (the editor) it stays focused.
/// Switched on by VRBuild.Setup
#if UNITY_EDITOR
[UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName = "BlockTower Session Focus",
    BuildTargetGroups = new[] { UnityEditor.BuildTargetGroup.Android },
    Company = "Seoyeonsoft", Version = "1.0.0", FeatureId = "com.seoyeonsoft.blocktower.sessionfocus")]
#endif
public class SessionFocus : OpenXRFeature
{
    /// The app has the input: no system menu over it
    public static bool Focused { get; private set; } = true;
    /// The app is on the headset's screen, focused or under the system menu
    public static bool Visible { get; private set; } = true;

    protected override void OnSessionStateChange(int oldState, int newState)
    {
        Focused = newState == (int)XrSessionState.Focused;
        Visible = Focused || newState == (int)XrSessionState.Visible;
        Debug.Log($"[BT] session {(XrSessionState)oldState} -> {(XrSessionState)newState}");
    }
}
