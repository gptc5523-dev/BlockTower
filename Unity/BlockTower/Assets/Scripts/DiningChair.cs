using UnityEngine;

/// A chair the player can sit on. It slides straight back from the table and in again.
/// Mesh local axes: +Z = chair front (toward the table), origin = seat front edge on the floor.
/// Dimensions come from CAD/src/chair.py. Units: m.
public class DiningChair : MonoBehaviour
{
    /// How far the chair is pulled out before sitting
    public float pullOutDistance = 0.35f;
    /// How far the seated player scoots back in
    public float tuckInDistance = 0.15f;
    /// Time for a full slide, s
    public float slideTime = 0.4f;
    /// Seat centre on the seat surface, local (seat 410 deep, 440 high at the front, tilted 4 deg back)
    public Vector3 seatPoint = new(0f, 0.426f, -0.205f);

    [System.NonSerialized] public TablePlayer occupant;
    /// Blocks pulled out from this seat, stacked in one pile at this seat's table corner: the score you can see
    [System.NonSerialized] public int taken;
    float pileTop;

    Vector3 tuckedPosition;
    float slideFrom, slideTarget, slideAlpha = 1f, slideDuration;

    /// A seated head sits this far ahead of the hips, m
    public const float HeadAhead = 0.1f;
    /// Each pile on the table, from its middle: the corners stand at 0.8 and 0.45, the pile 80 mm in from both edges, m
    const float PileAcross = 0.72f, PileBack = 0.37f;

    public Vector3 SeatPosition => transform.TransformPoint(seatPoint);
    /// World direction the seated player faces
    public Vector3 Facing => transform.forward;

    void Awake() => tuckedPosition = transform.position;

    /// `who` sits here, the chair at the table
    public void Seat(TablePlayer who)
    {
        occupant = who;
        SlideTo(pullOutDistance - tuckInDistance);
    }

    /// `who` left: empty, back under the table
    public void Free(TablePlayer who)
    {
        if (occupant != who)
            return;
        occupant = null;
        SlideTo(0f);
    }

    /// Slide to this distance behind the tucked position; returns the slide duration
    public float SlideTo(float distance)
    {
        slideFrom = slideTarget;
        slideTarget = distance;
        slideDuration = slideTime * Mathf.Abs(distance - slideFrom) / pullOutDistance;
        slideAlpha = slideDuration > 0f ? 0f : 1f;
        if (slideAlpha >= 1f)
            SetSlide(slideTarget);
        return slideDuration;
    }

    void Update()
    {
        if (slideAlpha < 1f)
        {
            slideAlpha = Mathf.Min(1f, slideAlpha + Time.deltaTime / slideDuration);
            // ease in and out, like a chair dragged by hand
            SetSlide(Mathf.Lerp(slideFrom, slideTarget, Mathf.SmoothStep(0f, 1f, slideAlpha)));
        }
    }

    /// Stacks a pulled-out block on this seat's pile at the near table corner, each one flat on the last,
    /// lengthwise along the table edge. Table 1600 x 900 (CAD/src/table.py); chairs face across it, so the
    /// corner on this seat's side is at (sign(seat x) * 0.8, -Facing * 0.45). The pile stands 80 mm in from both edges
    public void LayDown(Rigidbody block, float tableTop)
    {
        if (taken == 0)
            pileTop = tableTop;
        Vector3 along = Vector3.Cross(Vector3.up, Facing);
        Vector3 spot = new Vector3(Mathf.Sign(tuckedPosition.x) * PileAcross, pileTop + 0.0005f, 0f) - Facing * PileBack;
        Quaternion lying = Quaternion.FromToRotation(Vector3.right, along);  // block length runs along its local X
        block.transform.SetParent(null, true);  // no longer part of the tower: it can't be aimed at as one
        Scenery.Teleport(block, spot, lying);
        block.linearVelocity = block.angularVelocity = Vector3.zero;
        pileTop += block.GetComponent<BoxCollider>().size.y * block.transform.localScale.y;  // nominal thickness, scaled per block
        taken++;
        Debug.Log($"[BT] laid on {name} pile: {taken} blocks, at {spot.ToString("F2")}");
        StartCoroutine(WatchPiled(block, spot, taken));
    }

    // ponytail: diagnostic for "the first piled block ends up on the floor" (2026-11-05); delete once the cause is fixed
    System.Collections.IEnumerator WatchPiled(Rigidbody block, Vector3 spot, int n)
    {
        float start = Time.time;
        foreach (float at in new[] { 0.1f, 0.5f, 2f })
        {
            yield return new WaitForSeconds(at - (Time.time - start));
            if (!block)
                yield break;
            Debug.Log($"[BT] pile #{n} after {at:F1}s: moved {(block.position - spot).ToString("F3")}, v {block.linearVelocity.ToString("F2")}, awake {!block.IsSleeping()}");
        }
    }

    void SetSlide(float distance) => transform.position = tuckedPosition - Facing * distance;

    /// Furthest a sitter can have the chair out, m
    const float MostOut = 0.7f;

    /// The chair under someone seated with their head at `head` (a headset's): the seat follows the body back and
    /// forth, the head a little ahead of the hips as when sitting up
    public void SitUnder(Vector3 head)
    {
        Vector3 hips = head - Facing * HeadAhead;
        Vector3 tuckedSeat = SeatPosition + (tuckedPosition - transform.position);
        float distance = Mathf.Clamp(Vector3.Dot(tuckedSeat - hips, Facing), 0f, MostOut);
        slideAlpha = 1f;  // no slide running: it follows the body
        slideTarget = distance;
        SetSlide(Mathf.Lerp(Vector3.Dot(tuckedPosition - transform.position, Facing), distance, Scenery.Closing(8f)));
    }
}
