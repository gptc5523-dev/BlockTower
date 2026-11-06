using UnityEngine;

/// The mouse player's right hand (in the editor): aim at a block's short end and drag; a physical fingertip
/// pushes, the pads pinch and pull a block sticking out
public partial class PlayerCharacter
{
    /// Max distance from the camera to a block the hand can reach
    public float fingerReach = 1.3f;
    /// Hand travel per mouse axis unit while pushing or pulling, m (any drag direction counts)
    public float fingerDragScale = 0.002f;
    public float fingerLength = 0.07f;
    /// How far the fingers slide along a stuck block before they lose it, m
    public float pinchSlip = 0.005f;
    /// Most a fingertip pushes, N (CAD/checks/tower_mechanics.py section 6): 12 N moves any block, even a bottom one
    /// carrying the whole tower (11.0 N). A block holding harder stops the fingertip; the hand runs on without it
    public float pushStrength = 12f;
    /// How deep the pads grip the block end, m
    public float pinchDepth = 0.008f;
    /// How far a block must stick out toward you before the pads can close on it: 15 mm, about a sixth of its length.
    /// A flush end gives the fingers nothing to hold; push it out from the other side first
    public float pinchReach = 0.015f;
    /// Top hand speed, m/s: a careful hand. Smooths the mouse, so a flick cannot jolt a block out of the fingers
    public float handSpeed = 0.3f;

    // Fingertip: kinematic, so blocks react to it physically. Grip.None while hovering, then the first drag picks push or pull
    enum Grip { None, Push, Pull }
    Rigidbody finger;     // kinematic hand driver: goes where the mouse says, touches nothing
    Rigidbody fingertip;  // the physical fingertip, sprung to the driver with at most pushStrength
    Collider tipCapsule;
    HandVisual hand;
    Rigidbody fingerBlock;
    Vector3 pushDir, pushOrigin, fingerTip, targetTip;
    float advance;
    Grip grip;
    ConfigurableJoint pinch;
    Vector3 gripLocal;  // pinch point in the block's frame
    bool allowed;       // the aimed block may be taken under the rules
    Vector3 pushedOutDir;  // toward its far end
    Rigidbody pushedOut;  // the block pushed 20 % out the far side: grippable from the far side even if it sinks back (CanPinch)

    /// Where to look at the pushed-out block's far end (the end away from where it was pushed): its top face 7.5 mm in from the end while it sticks out
    /// that far (a line to the end face's centre grazes the block above), else the end face itself.
    Vector3 FarGripPoint()
    {
        Vector3 center = pushedOut.worldCenterOfMass;
        float halfLength = tower.BlockLength / 2f * pushedOut.transform.lossyScale.x;
        float sticksOut = Vector3.Dot(center - tower.transform.position, pushedOutDir);
        float half = tower.blockThickness / 2f;
        return sticksOut >= half
            ? center + pushedOutDir * (halfLength - half) + Vector3.up * (half * pushedOut.transform.localScale.y)
            : center + pushedOutDir * halfLength;
    }

    /// Holding the button: the first drag picks push or pull, then the hand follows the mouse
    void Work()
    {
        if (grip == Grip.None)
        {
            if (advance < -0.002f)
            {
                Pinch();
                committed = fingerBlock;
                Debug.Log($"[BT] pinch {fingerBlock.name}: {Protrusion() * 1000f:F0} mm out, far={farSide}");
            }
            else if (advance > 0.002f)
            {
                grip = Grip.Push;
                committed = fingerBlock;
                Debug.Log($"[BT] push {fingerBlock.name} far={farSide}");
            }
            prompt = GameText.DragToWork;
        }

        if (grip == Grip.Push)
        {
            advance = Mathf.Clamp(advance, 0f, 0.09f);
            if ((fingerBlock != pushedOut || pushedOutDir != pushDir) && -Protrusion() >= swingPush)
            {
                pushedOut = fingerBlock;
                pushedOutDir = pushDir;
                Debug.Log($"[BT] push far enough to swing: {fingerBlock.name} {-Protrusion() * 1000f:F0} mm out the far side");
            }
            prompt = fingerBlock == pushedOut ? GameText.LetGoGoRound : GameText.Push;
        }
        else if (grip == Grip.Pull)
        {
            Vector3 tip = finger.position + pushDir * (fingerLength / 2f);
            if (Vector3.Distance(fingerBlock.transform.TransformPoint(gripLocal), tip) > pinchSlip)
            {
                // the pads lost the block: held too tight while still in the tower, or past halfway out
                // it tipped and fell away from the fingers, which is just the block dropping
                bool stuck = Protrusion() < tower.BlockLength / 2f;
                Debug.Log($"[BT] {(stuck ? "slipped" : "dropped")}: {Protrusion() * 1000f:F0} mm out");
                Release();
                if (stuck)
                    prompt = GameText.Slipped;
                return;
            }
            advance = Mathf.Clamp(advance, -0.15f, 0f);
            prompt = Protrusion() >= tower.BlockLength ? GameText.LetGoTake : GameText.Pull;  // fully out
        }
        targetTip = pushOrigin + pushDir * advance;
    }

    /// How far the aimed block sticks out of the tower toward the player, m.
    /// A block is as long as the tower is wide, so its centre's offset from the tower axis is its overhang
    float Protrusion()
    {
        Vector3 offset = fingerBlock.worldCenterOfMass - tower.transform.position;
        return Vector3.Dot(offset, -pushDir);
    }

    /// Game call: a block that once reached 20 % out the far side stays grippable there. A tight block is pushed
    /// back in by the leaning stack above, even under the finger (seen: 16 mm out sank to 7 mm in 0.3 s, then to -2 mm).
    /// Freezing it in place instead propped the leaning stack and toppled the tower every time
    bool CanPinch() => Protrusion() >= pinchReach || (fingerBlock == pushedOut && farSide);

    /// Pinches the block end between thumb and index: a force-limited spring, like pad friction,
    /// so the hand can drag a loose block but slides off a stuck one
    void Pinch()
    {
        grip = Grip.Pull;
        pushOrigin += pushDir * (0.002f + pinchDepth);  // from 2 mm off the end face to inside the pads
        advance = 0f;
        fingerTip = targetTip = pushOrigin;
        // the pads hold the block through the driver; the fingertip capsule stays out of it while pinching
        PlaceFinger(teleport: true);
        Physics.SyncTransforms();
        gripLocal = fingerBlock.transform.InverseTransformPoint(fingerTip);
        pinch = fingerBlock.gameObject.AddComponent<ConfigurableJoint>();
        pinch.anchor = gripLocal;
        pinch.connectedBody = finger;
        pinch.rotationDriveMode = RotationDriveMode.Slerp;
        PinchDrive(pinch, false);
    }

    void Release()
    {
        if (holding && fingerBlock)
            Debug.Log($"[BT] release {grip} {fingerBlock.name}: {Protrusion() * 1000f:F0} mm toward me, far={farSide}");
        // now, not at the frame end: a joint left until then still drags the dropped block toward the hand
        if (pinch)
            DestroyImmediate(pinch);
        // pushed out the other side: swing round to that side and look at it, from either side
        // (if it drops, CollectTaken swings back to the own seat)
        if (grip == Grip.Push && fingerBlock && fingerBlock == pushedOut)
            Swing(!farSide);
        holding = false;
        grip = Grip.None;
    }

    /// Places the finger at the near end of the block under the crosshair; false if none is reachable
    bool AimFinger()
    {
        var ray = new Ray(view.transform.position, view.transform.forward);
        if (!Physics.Raycast(ray, out var hit, fingerReach, NotHands, QueryTriggerInteraction.Ignore)
            || !hit.rigidbody || !hit.rigidbody.GetComponentInParent<TowerSpawner>())
            return false;

        // Push along the block's length from the end facing the player; a side face cannot be pushed from here
        Transform block = hit.rigidbody.transform;
        Vector3 along = block.right;
        float facing = Vector3.Dot(along, ray.direction);
        if (Mathf.Abs(facing) < 0.35f)
            return false;

        fingerBlock = hit.rigidbody;
        allowed = tower.CanTake(fingerBlock);
        pushDir = along * Mathf.Sign(facing);
        float halfLength = tower.BlockLength / 2f * block.lossyScale.x;
        pushOrigin = hit.collider.bounds.center - pushDir * (halfLength + 0.002f);  // 2 mm off the end face
        fingerTip = targetTip = pushOrigin;
        PlaceFinger(teleport: true);
        return true;
    }

    /// Where the pinching pads are drawn: on the part of the block already out of the tower. The physical grip sits
    /// pinchDepth inside the end, so drawn there the thumb and index went through the levels above and below while
    /// the block was still in; a nearly flush block is held by its very end from just outside the tower face
    Vector3 PinchShown()
    {
        const float pad = 0.006f;  // fingertip pad half thickness: keeps the fingers clear of the tower face
        float halfLength = tower.BlockLength / 2f * fingerBlock.transform.lossyScale.x;
        Vector3 nearEnd = fingerBlock.worldCenterOfMass - pushDir * halfLength;
        return nearEnd + pushDir * Mathf.Clamp(Protrusion() - pad, -pad, pinchDepth);
    }

    void PlaceFinger(bool teleport)
    {
        Vector3 center = fingerTip - pushDir * (fingerLength / 2f);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, pushDir);
        ShowHand(true);
        if (teleport)
        {
            foreach (var body in new[] { finger, fingertip })
                Scenery.Teleport(body, center, rotation);
            fingertip.linearVelocity = Vector3.zero;
            tipCapsule.enabled = grip != Grip.Pull;
        }
        else
        {
            finger.MovePosition(center);
            finger.MoveRotation(rotation);
        }
    }

    void ShowHand(bool show)
    {
        finger.gameObject.SetActive(show);
        fingertip.gameObject.SetActive(show);
        hand.gameObject.SetActive(show);
    }
}
