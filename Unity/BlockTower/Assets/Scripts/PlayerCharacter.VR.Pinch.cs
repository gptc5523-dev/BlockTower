using System.Collections.Generic;
using UnityEngine;

/// The headset player's touch-move and holding: a block a hand moves is the one to take out; the thumb and a finger
/// either side of a block hold it on a joint to the hand's grip
public partial class PlayerCharacter
{
    Rigidbody vrTight;             // a block the real fingertip went 3 mm into without it moving away: tight
    float vrTightTime = -10f;
    readonly Dictionary<Rigidbody, Vector3> touchedAt = new();  // where each block was when a fingertip first met it

    /// A real hand moves faster than the mouse hand: the pads lose the block when the hand is this far from the grip
    public float vrPinchSlip = 0.03f;
    /// How far off a block each fingertip may stop and still hold it, m. Nothing stops a real finger on a block
    /// it can't feel: on the headset (2026-11-05) the fingers stopped 2-8 mm short where they looked on it
    public float vrPinchReach = 0.008f;

    /// Touch-move: a block a hand has moved 3 mm is the one to take out this turn
    void Touched(Rigidbody touched)
    {
        if (touched && !touchedAt.ContainsKey(touched))
            touchedAt[touched] = touched.position;
        if (touched && !committed && tower.CanTake(touched)
            && Vector3.Distance(touched.position, touchedAt[touched]) > 0.003f)
            Commit(touched, touchedAt[touched]);
    }

    /// Holding takes the thumb and any finger touching one block from either side; the hand holds on till it opens
    /// 5 mm wider than it took hold
    void GripVR(VrHand h)
    {
        // the tips where the hand is drawn (its body, stopped by what it met), not where the real hand went on to
        Vector3 thumb = Drawn(h, h.tips[0].target);
        h.pinching = false;
        h.pinchBlock = null;
        if (h.held)
        {
            Vector3 holder = Drawn(h, h.tips[h.pinchFinger].target);
            h.pinchPoint = (thumb + holder) / 2f;
            // closing on past the block (the real fingers meet through it) still holds; opening 5 mm lets go
            h.pinching = h.tips[0].active && h.tips[h.pinchFinger].active && Vector3.Distance(thumb, holder) < h.gripSpan + 0.005f;
            return;
        }
        h.pinchPoint = (thumb + Drawn(h, h.tips[Index].target)) / 2f;
        if (!h.tips[0].active)
            return;
        for (int f = 1; f < 5; f++)
        {
            if (!h.tips[f].active)
                continue;
            Vector3 finger = Drawn(h, h.tips[f].target);
            Vector3 mid = (thumb + finger) / 2f;
            var block = tower.BlockAt(mid, 0.001f);  // the two tips either side of one block
            // each fingertip on this block, not on the one beside, above or below it (a neighbour within reach of the
            // block is nearer the tip: that is the block it's on)
            if (!block || tower.BlockAt(thumb, 0.02f) != block || tower.BlockAt(finger, 0.02f) != block)
                continue;
            // both fingertips on the block, not just near it: within 2 cm it held like a magnet
            float gapThumb = TipGap(h, 0, thumb, block), gapFinger = TipGap(h, f, finger, block);
            if (gapThumb > vrPinchReach || gapFinger > vrPinchReach)
            {
                // ponytail: diagnostic for "the fingers look on the block but it isn't held" (2026-11-05)
                if (Time.time - nearMissLogged > 0.5f && gapThumb < 0.02f && gapFinger < 0.02f)
                {
                    nearMissLogged = Time.time;
                    Debug.Log($"[BT] VR {h.node} pinch miss {tower.Label(block)} f{f}: thumb {gapThumb * 1000f:F1} mm, finger {gapFinger * 1000f:F1} mm off the block");
                }
                continue;
            }
            h.pinching = true;
            h.pinchBlock = block;
            h.pinchFinger = f;
            h.pinchPoint = mid;
            h.gripSpan = Vector3.Distance(thumb, finger);
            return;
        }
    }

    float nearMissLogged = -10f;

    /// How far a fingertip is off a block: its pad ball, or the fingertip's skin as drawn (the bone's mesh piece:
    /// that's what stops on the block and what the player sees touch it), whichever is closer
    static float TipGap(VrHand h, int finger, Vector3 pad, Rigidbody block)
    {
        var box = block.GetComponent<Collider>();
        float gap = TowerSpawner.Surface(block, pad) - h.tips[finger].ball.radius;
        var bone = TipBone(h, finger);
        if (bone != null && bone.active && bone.shape.enabled)
        {
            // closest points of two convex shapes: alternate projections, settled in a few rounds
            Vector3 q = box.bounds.center, p = q;
            for (int i = 0; i < 4; i++)
            {
                p = bone.shape.ClosestPoint(q);
                q = box.ClosestPoint(p);
            }
            gap = Mathf.Min(gap, Vector3.Distance(p, q));
        }
        return Mathf.Max(gap, 0f);
    }

    void UpdatePinch(VrHand h, bool playing)
    {
        if (h.held && (!h.pinching || !playing))
            LetGo(h);
        if (h.held && Vector3.Distance(h.held.transform.TransformPoint(h.heldLocal), h.pinchPoint) > vrPinchSlip)
        {
            // the pads slid off: still in the tower, it's stuck; out of it, it just dropped
            bool stuck = tower.InFootprint(h.held);
            Debug.Log($"[BT] VR {(stuck ? "slipped" : "dropped")} {tower.Label(h.held)}");
            LetGo(h);
            if (stuck)
                vrSlipTime = Time.time;
        }
        if (h.pinching && !h.held && playing)
            TryPinch(h);
    }

    void TryPinch(VrHand h)
    {
        var block = h.pinchBlock;
        if (!block || (committed && block != committed) || !tower.CanTake(block) || OtherHandHolds(h, block))
            return;
        h.held = block;
        // between the pads
        Vector3 at = h.pinchPoint;
        Quaternion turn = h.rot;
        h.heldLocal = block.transform.InverseTransformPoint(at);
        // the grip turns with the hand, so the held block turns with the wrist
        h.grip.position = at;
        h.grip.rotation = turn;
        h.grip.transform.SetPositionAndRotation(at, turn);
        Physics.SyncTransforms();
        h.pinch = block.gameObject.AddComponent<ConfigurableJoint>();
        h.pinch.anchor = h.heldLocal;
        h.pinch.connectedBody = h.grip;
        h.pinch.rotationDriveMode = RotationDriveMode.Slerp;
        h.firm = false;
        PinchDrive(h.pinch, h.firm);
        Commit(block, block.position);
        holding = true;
        Debug.Log($"[BT] VR pinch {tower.Label(block)} with the {h.node}");
    }

    bool OtherHandHolds(VrHand h, Rigidbody block) => System.Array.Exists(vrHands, o => o != h && o.held == block);

    void LetGo(VrHand h)
    {
        if (h.held)
            IgnoreHeld(h, h.held, false);
        if (h.pinch)
            DestroyImmediate(h.pinch);  // now: a joint left to the frame end still drags the block
        h.held = null;
        holding = System.Array.Exists(vrHands, o => o.held);
    }

    /// The first block moved this turn is the one to take out (touch-move), red for everyone till it's out
    void Commit(Rigidbody block, Vector3 restedAt)
    {
        if (committed == block)
            return;
        committed = block;
        moving = block;
        blockStart = restedAt;
        Debug.Log($"[BT] VR committed to {tower.Label(block)}");
    }

    /// The block held a fingertip off just now: drawn as one that can't be taken (grey), with a note
    bool Tight(Rigidbody block) => block == vrTight && Time.time - vrTightTime < 0.3f;

    /// Lets go with both hands (the tower fell, a new one is being stacked, the menu opened)
    void ReleaseVR()
    {
        foreach (var h in vrHands)
            if (h != null)
                LetGo(h);
        touchedAt.Clear();
    }
}
