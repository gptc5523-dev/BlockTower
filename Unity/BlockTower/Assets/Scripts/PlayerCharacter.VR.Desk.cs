using UnityEngine;

/// Where the headset player sits: the rig over the seat, and the virtual table top on the real desk once both palms
/// have rested on it
public partial class PlayerCharacter
{
    bool vrCentred;
    float vrHeadY;                 // the real seated head height, measured at each recentre
    float vrDeskY = float.NaN;     // the real desk top above the real floor, once both palms rested on it
    Vector3 vrPalms = new(float.NaN, 0f, 0f);  // tracking space: the palms' midpoint then
    float vrPalmsYaw;              // tracking space: square ahead off the desk then
    bool vrDeskArmed = true;       // the hands left the desk since it was last set

    /// The table is 900 deep about the tower (CAD/src/table.py), m
    const float TableHalfDepth = 0.45f;
    /// The palms go this far in from its near edge, m
    const float PalmsIn = 0.2f;

    /// The rig pose that puts the tracked point `local` (tracking space) over `spot` (floor point), the tracked
    /// direction `localYaw` along `yaw`, the floor raised by `lift`
    void RigFor(Vector3 spot, Vector3 local, float localYaw, float yaw, float lift, out Vector3 position, out float rigYaw)
    {
        rigYaw = yaw - localYaw;
        Vector3 offset = Quaternion.Euler(0f, rigYaw, 0f) * new Vector3(local.x, 0f, local.z);
        position = new Vector3(spot.x - offset.x, spot.y + lift, spot.z - offset.z);
    }

    /// Looking at the table from the seat. Once both palms rested on the real desk, they go 20 cm in from the
    /// table's near edge, square to it, so the real desk is where the table is (the head over the seat put a body
    /// sitting closer than the seat into the table). Else the head over the seat, looking where it looks.
    /// The real desk at the table top once it's known, else the head at seated eye height
    void SeatRig(out Vector3 position, out float yaw)
    {
        Vector3 eye = chair.SeatPosition + chair.Facing * DiningChair.HeadAhead;
        float floor = chair.transform.position.y;
        float lift = float.IsNaN(vrDeskY) ? seatedEyeHeight - vrHeadY : tower.transform.position.y - floor - vrDeskY;
        if (!float.IsNaN(vrPalms.x))
        {
            Vector3 hands = eye + chair.Facing * (Vector3.Dot(tower.transform.position - eye, chair.Facing) - TableHalfDepth + PalmsIn);
            RigFor(new Vector3(hands.x, floor, hands.z), vrPalms, vrPalmsYaw, FacingYaw, lift, out position, out yaw);
            return;
        }
        RigFor(new Vector3(eye.x, floor, eye.z), view.transform.localPosition, view.transform.localEulerAngles.y, FacingYaw, lift, out position, out yaw);
    }

    // the desk as last set, kept on the Quest for the next launch: the floor origin stays with the room, so the
    // palms' place in tracking space holds till the headset is recentred (then only the height is used)
    const string DeskKey = "vr.desk";

    void SaveDesk()
    {
        PlayerPrefs.SetString(DeskKey, float.IsNaN(vrDeskY) ? "" : System.FormattableString.Invariant(
            $"{vrDeskY} {vrPalms.x} {vrPalms.y} {vrPalms.z} {vrPalmsYaw}"));
        PlayerPrefs.Save();
    }

    /// The desk as set on the office Quest on 2026-11-06 (the player's call: the starting setting, set again with the
    /// palms any time): height m, palms in tracking space, yaw deg
    const string DefaultDesk = "0.840 -0.08 0.87 -0.10 272";

    void LoadDesk()
    {
        var v = PlayerPrefs.GetString(DeskKey, DefaultDesk).Split(' ');
        if (v.Length != 5)
            return;
        var f = System.Array.ConvertAll(v, x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        vrDeskY = f[0];
        vrPalms = new Vector3(f[1], f[2], f[3]);
        vrPalmsYaw = f[4];
        Debug.Log($"[BT] VR desk loaded: {vrDeskY:F3} m, palms {vrPalms}, yaw {vrPalmsYaw:F0}");
    }

    /// Back over the seat, the head height measured again
    void Recenter()
    {
        vrHeadY = view.transform.localPosition.y;
        Vector3 p;
        float yaw;
        if (vrAtDoor)  // standing at the door: the head there at standing eye height, looking into the room
            RigFor(vrDoor.position, view.transform.localPosition, view.transform.localEulerAngles.y, vrDoor.rotation.eulerAngles.y,
                standingEyeHeight - vrHeadY, out p, out yaw);
        else
            SeatRig(out p, out yaw);
        transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        if (vrFar && !vrAtDoor)
            transform.RotateAround(tower.transform.position, Vector3.up, 180f);
        vrCentred = true;
        Debug.Log($"[BT] VR recentred: head {view.transform.localPosition}, desk {vrDeskY:F3}, palms {vrPalms}");
    }

    /// Both palms lying flat and still on the desk for 1.5 s, any time: the view turns to the table and the table
    /// goes to the desk again. Both, level with each other and out in front: one palm, or both on the lap, read
    /// the lap as the desk (0.59 m)
    void FindDesk()
    {
        if (vrSince < 3f)
            return;  // not while the headset goes on: a hand held still in the air then read as the desk
        Vector3 head = view.transform.localPosition;
        bool resting = true;
        foreach (var h in vrHands)
        {
            // the whole hand flat on something: the palm level, all four fingertips level with each other and
            // just under the palm's middle (on a desk 16 mm). A palm held flat with the fingers hanging over the
            // desk's edge read 0.67 m for a 0.82 m desk (fingertips 10 cm under the palm)
            float under = h.palm.y - h.lowestTip;
            bool flat = h.fingers && !h.held && Vector3.Dot(h.up, Vector3.up) > 0.85f && head.y - h.palm.y > 0.3f
                && under > -0.005f && under < 0.035f && h.highestTip - h.lowestTip < 0.02f;
            if (!flat || Vector3.Distance(h.palm, h.restAt) > 0.01f)
            {
                h.restAt = h.palm;
                h.restSince = Time.time;
            }
            resting &= flat && Time.time - h.restSince >= 1.5f;
        }
        Vector3 left = vrHands[0].palm, right = vrHands[1].palm, mid = (left + right) / 2f;
        resting &= Mathf.Abs(left.y - right.y) < 0.03f
            && Mathf.Abs(vrHands[0].lowestTip - vrHands[1].lowestTip) < 0.015f  // both on the one desk
            && new Vector2(mid.x - head.x, mid.z - head.z).magnitude > 0.25f;
        if (!resting)
        {
            vrDeskArmed = true;  // the hands moved off: the next rest sets it again
            return;
        }
        if (!vrDeskArmed)
            return;
        vrDeskArmed = false;
        // the tip joint sits in the middle of the finger, 8 mm over the pad
        vrDeskY = Mathf.Min(vrHands[0].lowestTip, vrHands[1].lowestTip) - 0.008f;
        vrPalms = mid;
        // square to the desk: the hands' line across, facing away from the body
        Vector3 across = right - left;
        vrPalmsYaw = Quaternion.LookRotation(Vector3.Cross(new Vector3(across.x, 0f, across.z), Vector3.up)).eulerAngles.y;
        Debug.Log($"[BT] VR desk at {vrDeskY:F3} m, palms {mid}, yaw {vrPalmsYaw:F0}");
        SaveDesk();
        Recenter();
        TowerGame.Instance.Announce(GameText.DeskSet);
    }
}
