using UnityEngine;

/// The mouse player reaching round: seated, the view swings about the tower to its far side and back
public partial class PlayerCharacter
{
    // Reaching round: seated, the view can swing 180 deg about the tower to the far side, the body stays put.
    // A block pushed out the far side swings it there to pull; a block put on the pile swings it back
    [Header("Far side view")]
    public float swingTime = 0.7f;
    bool farSide;
    float sideBlend;                         // 0 own seat .. 1 far side
    float swingFromYaw, swingFromPitch, swingToYaw, swingToPitch;
    bool swingAim;                           // turn the head toward a block during the swing

    /// The seat's eye point carried round the tower axis by s * 180 deg, at the same height
    Vector3 SwungEye(float s)
    {
        Vector3 eye = transform.TransformPoint(0f, standingEyeHeight, 0f), axis = tower.transform.position;
        Vector3 offset = Quaternion.Euler(0f, 180f * s, 0f) * Vector3.ProjectOnPlane(eye - axis, Vector3.up);
        return new Vector3(axis.x + offset.x, eye.y, axis.z + offset.z);
    }

    /// Starts swinging the view to the far side or back; going there with a pushed-out block, the head turns to it
    void Swing(bool toFar)
    {
        farSide = toFar;
        swingAim = pushedOut;
        (swingFromYaw, swingFromPitch) = (yaw, pitch);
    }

    void UpdateSwing()
    {
        if (controls.Pressed(UnityEngine.InputSystem.Key.Q) && !holding)
            Swing(!farSide);
        float before = sideBlend;
        sideBlend = Mathf.MoveTowards(sideBlend, farSide ? 1f : 0f, Time.deltaTime / swingTime);
        if (swingAim && before != sideBlend && pushedOut)
        {
            // re-aimed every frame: a tight block keeps sinking back while the view goes round
            var look = Quaternion.LookRotation(FarGripPoint() - SwungEye(farSide ? 1f : 0f)).eulerAngles;
            (swingToYaw, swingToPitch) = (look.y - (farSide ? 180f : 0f), Mathf.DeltaAngle(0f, look.x));
            float t = Mathf.SmoothStep(0f, 1f, farSide ? sideBlend : 1f - sideBlend);
            yaw = Mathf.LerpAngle(swingFromYaw, swingToYaw, t);
            pitch = Mathf.Lerp(swingFromPitch, swingToPitch, t);
        }
    }
}
