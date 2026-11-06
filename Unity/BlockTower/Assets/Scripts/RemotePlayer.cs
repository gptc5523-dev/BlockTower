using UnityEngine;
using UnityEngine.XR.Hands;

/// Another Quest at the table (VR multiplayer): its head, a torso under it (no neck, arms or legs: the headset knows
/// only the head and hands) and robot hands where that player has them, from the poses
/// it sends (world space: every Quest seats the room the same way). No body and no colliders: it never touches the
/// tower here, the one whose turn it is simulates it
public class RemotePlayer : TablePlayer
{
    /// The local player's eyes: name tags turn to them (Camera.main finds no camera in the headset rig)
    public static Transform Viewer;
    DiningChair chair;
    Transform head, torso;
    TextMesh nameTag;
    readonly Transform[][] joints = new Transform[2][];  // [0] left, [1] right; XRHandJointID index order
    readonly GameObject[] hands = new GameObject[2];
    readonly Pose[][] targets = new Pose[2][];
    Pose headTarget;
    bool posed;

    /// Over the head, the name tag, m
    const float TagOver = 0.16f;
    /// Resources: the robot torso, and its shirt and collar materials
    const string TorsoPath = "Avatar/RobotTorso", ShirtMaterial = "M_Tee_Cotton", CollarMaterial = "M_Tee_Rib";

    /// How fast the drawn head and hands close on the last received pose, 1/s (poses come 30 times a second)
    const float Follow = 25f;
    /// The torso's top (shoulders) under the head's centre, and behind it, m
    const float TorsoDrop = 0.2f, TorsoBack = 0.04f;

    public override string DisplayName => GameText.Player(seat);
    public override bool Local => false;
    public override Rigidbody Moving => null;
    public override void TowerFell() { }
    public override void NewRound() { }

    /// The shirt for `seat` in a room started with `seed`: no two seats alike. A room shuffles them by its seed, so
    /// every Quest dresses each seat the same
    public static Color Shirt(int seed, int seat)
    {
        var order = new int[Palette.Shirts.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        var rng = new System.Random(seed);
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return Palette.Shirts[order[seat % order.Length]];
    }

    public static RemotePlayer Make(int seat, DiningChair chair, GameObject leftModel, GameObject rightModel, Color shirt)
    {
        var p = new GameObject($"Remote {seat + 1}").AddComponent<RemotePlayer>();
        p.seat = seat;
        p.chair = chair;
        chair.Seat(p);
        p.MakeHead();
        p.MakeTorso(shirt);
        p.MakeTag();
        p.MakeHand(0, leftModel, "L_");
        p.MakeHand(1, rightModel, "R_");
        TowerGame.Instance.Join(p);
        return p;
    }

    /// A white robot head with a dark visor, as the hands
    void MakeHead()
    {
        head = Scenery.Solid(PrimitiveType.Sphere, Palette.RobotHead, transform);
        head.localScale = new Vector3(0.16f, 0.2f, 0.18f);
        var visor = Scenery.Solid(PrimitiveType.Cube, Palette.Visor, head);
        visor.localPosition = new Vector3(0f, 0.08f, 0.42f);
        visor.localScale = new Vector3(0.9f, 0.32f, 0.25f);
    }

    /// The robot torso (Assets/Resources/Avatar/RobotTorso.fbx, ~/Documents/3D/RobotTorso/build_torso.py); who it is
    /// shows on the name tag over the head
    void MakeTorso(Color shirt)
    {
        torso = Instantiate(Resources.Load<GameObject>(TorsoPath), transform, false).transform;
        foreach (var r in torso.GetComponentsInChildren<Renderer>())
            foreach (var m in r.materials)
                if (m.name.StartsWith(ShirtMaterial))
                    m.color = shirt;
                else if (m.name.StartsWith(CollarMaterial))
                    m.color = shirt * Palette.CollarShade;
    }

    void MakeTag()
    {
        nameTag = Scenery.Label(transform, 0.004f, Palette.Seats[seat % Palette.Seats.Length], "Name");
        nameTag.anchor = TextAnchor.LowerCenter;
        nameTag.text = DisplayName;
    }

    /// Hand `i` (0 left, 1 right), its joints named `prefix` + the joint
    void MakeHand(int i, GameObject model, string prefix)
    {
        var hand = hands[i] = Instantiate(model, transform, false);
        hand.SetActive(false);
        var all = hand.GetComponentsInChildren<Transform>(true);
        joints[i] = new Transform[XRHandJointID.EndMarker.ToIndex()];
        targets[i] = new Pose[joints[i].Length];
        for (int j = 0; j < joints[i].Length; j++)
        {
            string want = prefix + XRHandJointIDUtility.FromIndex(j);
            joints[i][j] = System.Array.Find(all, t => t.name == want);
        }
        foreach (var skin in hand.GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.updateWhenOffscreen = true;
    }

    /// A pose message from this player
    public void Pose(NetMsg m)
    {
        if (m.h != null && m.h.Length == NetPose.Stride)
            headTarget = NetPose.Read(m.h, 0);
        for (int i = 0; i < 2; i++)
        {
            var a = i == 0 ? m.l : m.r;
            bool on = a != null && a.Length == targets[i].Length * NetPose.Stride;
            if (on)
                for (int j = 0; j < targets[i].Length; j++)
                    targets[i][j] = NetPose.Read(a, j * NetPose.Stride);
            if (on && !hands[i].activeSelf)
                Snap(i);
            hands[i].SetActive(on);
        }
        if (!posed)
        {
            head.SetPositionAndRotation(headTarget.position, headTarget.rotation);
            posed = true;
        }
    }

    void Snap(int i)
    {
        for (int j = 0; j < joints[i].Length; j++)
            if (joints[i][j])
                joints[i][j].SetPositionAndRotation(targets[i][j].position, targets[i][j].rotation);
    }

    void Update()
    {
        float k = Scenery.Closing(Follow);
        head.SetPositionAndRotation(Vector3.Lerp(head.position, headTarget.position, k), Quaternion.Slerp(head.rotation, headTarget.rotation, k));
        // the torso hangs under the head, turning with its yaw only (a nod or a tilt leaves the body be)
        Vector3 ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        Quaternion yaw = ahead.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(ahead) : Quaternion.identity;
        torso.SetPositionAndRotation(head.position + Vector3.down * TorsoDrop - yaw * Vector3.forward * TorsoBack, yaw);
        if (posed && chair && Vector3.Distance(head.position, chair.SeatPosition) < 1f)
            chair.SitUnder(head.position);  // not while reaching round to the far side
        Vector3 tagAt = head.position + Vector3.up * TagOver;
        nameTag.transform.SetPositionAndRotation(tagAt, Viewer ? Quaternion.LookRotation(tagAt - Viewer.position) : Quaternion.identity);
        for (int i = 0; i < 2; i++)
        {
            if (!hands[i].activeSelf)
                continue;
            // parents first (index order), so each joint's world pose holds once its children move
            for (int j = 0; j < joints[i].Length; j++)
            {
                var t = joints[i][j];
                if (t)
                    t.SetPositionAndRotation(Vector3.Lerp(t.position, targets[i][j].position, k), Quaternion.Slerp(t.rotation, targets[i][j].rotation, k));
            }
        }
    }

    void OnDestroy()
    {
        TowerGame.Instance?.Leave(this);
        if (chair)
            chair.Free(this);
    }
}
