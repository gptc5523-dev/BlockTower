using UnityEngine;

/// Going round to the tower's far side to pull out a block pushed through it, and back
public partial class PlayerCharacter
{
    bool vrFar;                 // over on the tower's far side, to pull out a block pushed through
    Rigidbody vrSwingBlock;     // the committed block, once pushed swingPush out the far side
    Transform vrSwing;          // the round button over the tower
    TextMesh vrSwingText;
    bool vrSwingArmed;          // no fingertip on the button since it appeared or the view swung

    /// Push the block swingPush out the far side and a round button comes up over the tower; a fingertip on it
    /// swings the view to the far side (and, there, back). Taking the block out, or the go passing on, brings the
    /// player back to the own seat
    void SwingButton()
    {
        if (!vrSwing)
        {
            vrSwing = Scenery.Solid(PrimitiveType.Cylinder, Palette.SwingButton);
            vrSwing.localScale = new Vector3(0.035f, 0.002f, 0.035f);
            vrSwingText = Scenery.Label(null, 0.0018f, Palette.Ink);
        }
        bool myGo = TowerGame.Instance.IsTurn(this);
        if (vrFar && (!myGo || !committed))
            SwingVR(false);
        if (vrSwingBlock != committed)
            vrSwingBlock = null;
        if (committed && !vrSwingBlock && !vrFar
            && Vector3.Dot(committed.worldCenterOfMass - tower.transform.position, FarEnd(committed)) >= swingPush)
        {
            vrSwingBlock = committed;
            vrSwingArmed = false;
            Debug.Log($"[BT] VR {tower.Label(committed)} pushed out the far side: swing button up");
        }
        bool show = myGo && vrSwingBlock && !vrMenu.activeSelf && !holding && SessionFocus.Focused;
        vrSwing.gameObject.SetActive(show);
        vrSwingText.gameObject.SetActive(show);
        if (!show)
            return;

        // over the top of the tower, 5 cm clear of its highest block, its flat side to the eyes (the player asked: not
        // beside the block, where it was in the way of the hands at the tower)
        float top = tower.transform.position.y;
        foreach (var b in tower.blocks)
            if (b && b.transform.parent == tower.transform)
                top = Mathf.Max(top, b.GetComponent<Collider>().bounds.max.y);
        Vector3 at = new Vector3(tower.transform.position.x, top + 0.05f, tower.transform.position.z);
        vrSwing.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, view.transform.position - at));
        vrSwingText.text = vrFar ? GameText.ToOwnSeat : GameText.SwingThere;
        vrSwingText.transform.SetPositionAndRotation(at + Vector3.up * 0.03f, Quaternion.LookRotation(at - view.transform.position));

        bool touching = false;
        foreach (var h in vrHands)
            foreach (var t in h.tips)
                touching |= h.tracked && t.active && Vector3.Distance(t.target, at) < 0.02f;
        if (touching && vrSwingArmed)
            SwingVR(!vrFar);
        else if (!touching && !vrSwingArmed)
            vrSwingArmed = true;
    }

    /// The block's long axis, pointing to its end that's away from the player
    Vector3 FarEnd(Rigidbody block)
    {
        Vector3 axis = block.transform.right;  // a block's length runs along its local X
        return Vector3.Dot(axis, tower.transform.position - view.transform.position) >= 0f ? axis : -axis;
    }

    /// Round the tower to the far side, or back: the rig turns half a turn about the tower's axis
    void SwingVR(bool far)
    {
        if (vrFar == far)
            return;
        vrFar = far;
        ReleaseVR();
        transform.RotateAround(tower.transform.position, Vector3.up, 180f);
        vrSwingArmed = false;  // the finger that pressed is on the button's place on this side too
        Debug.Log($"[BT] VR swung to the {(far ? "far side" : "own seat")}");
    }
}
