using UnityEngine;

/// A posed hand, visual only, on the physical fingertip: the mouse player's right hand, and in a headset a
/// controller's hand (mirrored for the left).
/// Model: any hand with the XR Hands sample's joints (R_Wrist .. R_LittleTip): the sample RightHand.fbx for the
/// mouse player, the robot hand in a headset. Fingers along the wrist's forward, palm down.
/// Joints curl toward the palm about their local X axis.
public class HandVisual : MonoBehaviour
{
    public enum Pose { Point, Pinch }

    static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
    static readonly string[] Bones = { "Proximal", "Intermediate", "Distal" };
    static readonly string[] ThumbBones = { "Metacarpal", "Proximal", "Distal" };

    // Curl added to the relaxed bind pose, deg, per finger (thumb..little) and bone (proximal..distal).
    // Point: index straight, the rest folded. Pinch: see below.
    // Pinch: from above, thumb and index tips on either side of the block end (36 mm apart: 25 mm wide plus pads),
    // the index hanging straight down so no joint bulges into the levels above (searched in the editor).
    // The rest curl into a fist, behind the knuckles.
    static readonly float[,] PointCurl = { { 15, 25, 25 }, { -15, -15, 0 }, { 55, 50, 30 }, { 60, 45, 25 }, { 65, 40, 20 } };
    static readonly float[,] PinchCurl = { { 0, -20, -14 }, { 70, -10, 0 }, { 105, 95, 60 }, { 105, 95, 60 }, { 105, 95, 60 } };
    /// Pinch swings the thumb metacarpal across to oppose the index (replaces its curl)
    static readonly Vector3 PinchThumbBase = new(20f, -20f, -20f);

    // flat [finger * 3 + bone]: Unity keeps 1D arrays across a script reload in play mode, not 2D ones
    Transform[] joints;
    Quaternion[] bind;
    Transform indexTip, indexDistal, thumbTip, wrist;

    void Awake()
    {
        joints = new Transform[15];
        bind = new Quaternion[15];
        for (int f = 0; f < 5; f++)
            for (int b = 0; b < 3; b++)
            {
                joints[f * 3 + b] = Find("R_" + Fingers[f] + (f == 0 ? ThumbBones : Bones)[b]);
                bind[f * 3 + b] = joints[f * 3 + b].localRotation;
            }
        indexTip = Find("R_IndexTip");
        indexDistal = Find("R_IndexDistal");
        thumbTip = Find("R_ThumbTip");
        wrist = Find("R_Wrist");
    }

    Transform Find(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;
        throw new MissingReferenceException("Hand joint not found: " + name);
    }

    public void SetPose(Pose pose)
    {
        if (joints == null || joints.Length == 0)
            Awake();  // a script reload in play mode can leave the lookups empty
        var curl = pose == Pose.Point ? PointCurl : PinchCurl;
        for (int f = 0; f < 5; f++)
            for (int b = 0; b < 3; b++)
                joints[f * 3 + b].localRotation = bind[f * 3 + b] * (f + b == 0 && pose == Pose.Pinch ? Quaternion.Euler(PinchThumbBase) : Quaternion.Euler(curl[f, b], 0f, 0f));
    }

    /// The point that touches the block: the index tip, or between thumb and index tips when pinching
    Vector3 ContactPoint(Pose pose) => pose == Pose.Point ? indexTip.position : (indexTip.position + thumbTip.position) / 2f;

    /// Moves the hand so its contact point sits at <paramref name="contact"/>, fingers along
    /// <paramref name="forward"/>, back of the hand toward <paramref name="up"/>
    public void Place(Pose pose, Vector3 contact, Vector3 forward, Vector3 up)
    {
        SetPose(pose);
        transform.rotation = Quaternion.identity;
        // finger direction in the hand's own frame: along the index tip for pointing, the whole hand (the wrist's
        // forward: +Z for the sample, -Z for the robot hand's FBX) for pinching
        Vector3 along = pose == Pose.Point ? (indexTip.position - indexDistal.position).normalized : wrist.forward;
        transform.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Inverse(Quaternion.LookRotation(along, Vector3.up));
        transform.position += contact - ContactPoint(pose);
        // pinching, the tip joints sit a little below the block's middle so the pads, above them, hold its sides
        // and the index never shows over the top
        if (pose == Pose.Pinch)
            transform.position -= up * 0.002f;
    }
}
