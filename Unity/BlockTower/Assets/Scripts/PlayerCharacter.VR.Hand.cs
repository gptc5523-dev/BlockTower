using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using CommonUsages = UnityEngine.XR.CommonUsages;

/// The headset player's hands: read from hand tracking (or a controller), drawn from their joints, and solid as a
/// real hand is: one body following the tracked wrist with at most pushStrength, its colliders the hand mesh cut per bone (HandShape)
public partial class PlayerCharacter
{
    static readonly XRHandJointID[] TipJoints =
        { XRHandJointID.ThumbTip, XRHandJointID.IndexTip, XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip };
    const int Index = 1;
    /// The robot hand's fingertips, thumb .. little: half the distal end's thickness (HumanoidHand/build_hand2.py,
    /// T*0.66 for a finger, 13 mm for the thumb) at the export's 0.95 scale, m
    static readonly float[] RobotTipRadius = { 0.0062f, 0.0056f, 0.0057f, 0.0053f, 0.0047f };
    /// The physics hand's mass (kg): light, so a hand swept into a block knocks it like a fingertip, not a fist
    const float HandMass = 0.05f;
    /// Wrist to fingertip (m): the turning force at the wrist that lets a fingertip push with pushStrength
    const float HandReach = 0.18f;
    /// Fastest the hand and a block it overlaps are pushed apart, m/s. The fingers are put on the tracked joints every
    /// step, so tracking jitter puts them a few mm into a block they rest on; at the 1 m/s default the block was thrown
    /// clear (a loose one slides v^2/(2 mu g) = 170 mm). 0.05 m/s leaves it 0.4 mm (CAD/checks/tower_mechanics.py
    /// section 10). PhysX takes the lower of the two bodies' limits, so block on block keeps 1 m/s
    const float HandDepenetration = 0.05f;

    class Tip
    {
        public SphereCollider ball;  // on the hand's body, at the fingertip's pad
        public Vector3 target;       // the pad on the tracked hand (world)
        public Vector3 padLocal;     // the ball's centre about the tip joint, the tip joint's frame
        public Vector3 ballAt;       // the pad on the hand as drawn (world): holding, the shape it took hold with
        public bool active;
        public bool touching;        // on a block last step (TouchBuzz)
        public float offAt = -10f;   // when it last came off one
    }

    /// The palm or a finger bone: a piece of the hand's one body, put where the tracked hand has it every step
    class Part
    {
        public MeshCollider shape;      // the drawn hand's own skin about this bone (the palm: wrist, palm, metacarpals)
        public XRHandJointID from;      // the bone's joint (the palm: the wrist)
        public int tip = -1;            // the fingertip on this bone (0 thumb .. 4 little), -1 for none
        public Vector3 pos;             // tracked: the frame the shape is in, that joint's
        public Quaternion rot = Quaternion.identity;
        public bool active;
    }

    // the finger bones past the palm, thumb .. little
    static readonly XRHandJointID[,] FingerBones =
    {
        { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal },
        { XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal },
        { XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal },
        { XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal },
        { XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal },
    };
    // the controller's index finger, knuckle to tip: 35, 25 and 20 mm
    static readonly float[] BoneLength = { 0.035f, 0.025f, 0.02f };
    static PhysicsMaterial skinMaterial;

    class VrHand
    {
        public readonly List<Part> parts = new();
        // The hand as a human hand: one body driven onto the tracked wrist every step (Follow, at most pushStrength),
        // its colliders the hand's own skin on the tracked joints. Not sprung: sprung (2026-11-03) the hand lagged and
        // the fingers bent back on a block. Not kinematic: a kinematic hand (till 2026-11-05) pushed with no limit and
        // pressed blocks into each other
        public Rigidbody body;
        public XRHandSkeletonDriver skeleton;  // draws the hand model from the tracked joints (off while holding)
        public AudioSource buzz;     // the fingertip touch buzz (TouchBuzz)
        public Rigidbody pinchBlock;  // the block the thumb and a finger are both on (GripVR), the one TryPinch takes
        public Vector3 wristPos;   // the tracked wrist (world)
        public Quaternion wristRot = Quaternion.identity;
        public bool ghost = true;  // colliders off: not my go, or back from a tracking jump
        public XRNode node;
        public readonly Tip[] tips = new Tip[5];
        public Rigidbody grip;     // kinematic: the pinch point the held block is sprung to
        public GameObject real;    // the joint-driven hand, for tracked hands
        public HandVisual posed;   // the posed hand, for a controller
        public ConfigurableJoint pinch;
        public Rigidbody held;
        public Vector3 heldLocal;
        public bool tracked, fingers, pinching;
        public string was;         // last logged tracking state
        public Vector3 pinchPoint, forward, up;
        public Vector3 palm;       // tracking space (floor = 0)
        public float lowestTip;    // tracking space height of the lowest finger (not thumb) tip
        public float highestTip;   // and of the highest
        public Vector3 restAt;     // where the palm started resting
        public float restSince;
        public Quaternion rot = Quaternion.identity;  // the hand's world rotation (palm, or controller)
        public bool firm;          // the held block is clear of the tower and follows the hand stiffly
        public bool radiusLogged;
        public int pinchFinger;    // the finger (1 index .. 4 little) holding the block against the thumb
        public float gripSpan;     // thumb to that finger when it took hold
        public Pose[] heldShape;   // the bones about the wrist when it took hold
        public Transform[] bones;  // the 26 tracked joints (XRHandJointID index order), held shape included
        public Transform[] joints; // the drawn robot hand's 26 joints (XRHandJointID index order), sent to the others
        public Transform drawn;    // the drawn joints' parent: moved by the slip, so the hand is drawn where its body is
        public Vector3 aimPos;     // where Follow sent the body last step
        public Quaternion aimRot = Quaternion.identity;
        public Vector3 slipPos;    // how far short of that the body ended (what it met stopped it; nothing: zero)
        public Quaternion slipRot = Quaternion.identity;
        public bool lost = true;   // tracking dropped since the body was last driven
        public bool heldBack;      // logged: the body 20 mm short of the tracked wrist
        public Vector3 rigPos;     // the rig when the body was last driven (recentring, swinging move it at once)
        public Quaternion rigRot;
    }

    VrHand MakeVrHand(XRNode node, GameObject model, Handedness side)
    {
        var h = new VrHand { node = node };
        h.grip = new GameObject($"{node} Grip").AddComponent<Rigidbody>();
        h.grip.isKinematic = true;
        skinMaterial ??= new PhysicsMaterial("Skin")
        {
            dynamicFriction = 0.5f, staticFriction = 0.5f,
            bounciness = 0f, bounceCombine = PhysicsMaterialCombine.Minimum,  // dead on wood's 0.5: no jelly
        };
        h.body = new GameObject($"{node} Physics Hand") { layer = HandLayer }.AddComponent<Rigidbody>();
        // follows the tracked wrist with at most pushStrength (DriveHand), so a block it can't move stops it: a
        // kinematic hand pushed with no limit and pressed blocks into the ones under them
        h.body.useGravity = false;  // the arm holds it up
        h.body.mass = HandMass;
        h.body.maxDepenetrationVelocity = HandDepenetration;
        h.body.excludeLayers = ~(1 << TowerSpawner.BlockLayer);  // meets blocks only (not the table, chairs, the other hand)
        h.body.interpolation = RigidbodyInterpolation.Interpolate;
        h.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;  // a real hand can flick
        MakePart(h, $"{node} Palm", XRHandJointID.Wrist, -1);
        for (int f = 0; f < 5; f++)
            for (int b = 0; b < 3; b++)
                MakePart(h, $"{node} {FingerBones[f, b]}", FingerBones[f, b], b == 2 ? f : -1);
        // solid as the robot hand is drawn: its parts cut per bone (the player's own hand shape from the headset is
        // not used: what blocks meet is what the player sees)
        var modelSkin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var modelJoints = System.Array.ConvertAll(modelSkin.bones, b =>
            System.Enum.TryParse<XRHandJointID>(b.name.Substring(2), out var j) ? j : XRHandJointID.Invalid);  // "R_IndexTip"
        ShapeHand(h, modelSkin.sharedMesh, modelSkin.sharedMesh.bindposes, modelJoints);
        for (int i = 0; i < 5; i++)
        {
            var t = h.tips[i] = new Tip();
            var tip = new GameObject($"{node} Tip {i}") { layer = HandLayer };
            tip.transform.SetParent(h.body.transform, false);
            t.ball = tip.AddComponent<SphereCollider>();
            t.ball.sharedMaterial = skinMaterial;
            t.ball.enabled = false;  // till the hand is tracked
            t.ball.radius = RobotTipRadius[i];
        }

        // fixed about the wrist: the colliders reshape every step with the fingers. A ball of the hand's size
        h.body.automaticCenterOfMass = false;
        h.body.automaticInertiaTensor = false;
        h.body.centerOfMass = Vector3.zero;
        h.body.inertiaTensor = Vector3.one * (0.4f * HandMass * 0.05f * 0.05f);
        h.body.inertiaTensorRotation = Quaternion.identity;

        // the joint-driven hand: a child of the rig, as the joint poses are in tracking space. The model's root stays
        // on the rig origin and the driver moves the wrist (as the XR Hands sample does): the wrist joint takes its
        // tracking-space pose as its local pose, so a moved root would carry the hand that far again
        h.real = Instantiate(model, transform, false);
        h.real.SetActive(false);
        var events = h.real.AddComponent<XRHandTrackingEvents>();
        events.handedness = side;
        var skeleton = h.skeleton = h.real.AddComponent<XRHandSkeletonDriver>();
        // the wrist right under the root, as in the sample: the robot hand's FBX has it under an armature node turned
        // 270 deg (Blender's axes), which would turn the tracked pose the driver gives it
        string wristName = (side == Handedness.Left ? "L_" : "R_") + "Wrist";
        var wrist = System.Array.Find(h.real.GetComponentsInChildren<Transform>(true), t => t.name == wristName);
        wrist.SetParent(h.real.transform, true);
        skeleton.rootTransform = wrist;
        skeleton.handTrackingEvents = events;
        // a component added in a player build has its serialized list null (the editor makes an empty one)
        skeleton.jointTransformReferences = new List<JointToTransformReference>();
        var missing = new List<string>();
        skeleton.FindJointsFromRoot(missing);
        skeleton.InitializeFromSerializedReferences();
        h.joints = new Transform[XRHandJointID.EndMarker.ToIndex()];
        foreach (var r in skeleton.jointTransformReferences)
            h.joints[r.xrHandJointID.ToIndex()] = r.jointTransform;
        if (missing.Count > 0)
            Debug.LogWarning($"[BT] {side} hand joints missing: {string.Join(", ", missing)}");
        foreach (var skin in h.real.GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.updateWhenOffscreen = true;  // its bind-pose bounds sit at the rig origin, culled as the hand moves away

        var drawn = h.drawn = new GameObject($"{node} Drawn").transform;
        drawn.SetParent(transform, false);
        h.bones = new Transform[XRHandJointID.EndMarker.ToIndex()];
        for (int i = 0; i < h.bones.Length; i++)
        {
            h.bones[i] = new GameObject($"{node} {XRHandJointIDUtility.FromIndex(i)}").transform;
            h.bones[i].SetParent(drawn, false);
        }

        h.posed = Instantiate(vrHandModel).AddComponent<HandVisual>();
        if (node == XRNode.LeftHand)
            h.posed.transform.localScale = new Vector3(-1f, 1f, 1f);  // the right hand model, mirrored
        h.posed.gameObject.SetActive(false);
        return h;
    }

    /// The palm or a finger bone (`from` its joint, `tip` the fingertip on it or -1): a collider on the hand's body
    void MakePart(VrHand h, string name, XRHandJointID from, int tip)
    {
        var p = new Part { from = from, tip = tip };
        var go = new GameObject(name + " Shape") { layer = HandLayer };
        go.transform.SetParent(h.body.transform, false);
        p.shape = go.AddComponent<MeshCollider>();
        p.shape.convex = true;  // on a moving body
        p.shape.sharedMaterial = skinMaterial;
        p.shape.enabled = false;
        h.parts.Add(p);
    }

    /// The hand solid as drawn: each part's collider is its piece of `mesh` (HandShape.Cut)
    void ShapeHand(VrHand h, Mesh mesh, Matrix4x4[] bind, XRHandJointID[] joints)
    {
        var pieces = h.parts.ConvertAll(p => p.from).ToArray();
        var tips = h.parts.ConvertAll(p => p.tip >= 0 ? TipJoints[p.tip] : XRHandJointID.Invalid).ToArray();
        var hulls = HandShape.Cut(mesh, bind, joints, pieces, tips);
        for (int i = 0; i < hulls.Length; i++)
        {
            var shape = h.parts[i].shape;
            if (shape.sharedMesh)
                Destroy(shape.sharedMesh);
            shape.sharedMesh = hulls[i];
        }
    }

    /// The bone that carries fingertip `finger`'s ball
    static Part TipBone(VrHand h, int finger) => h.parts.Find(p => p.tip == finger);

    /// Every collider of the hand: palm, bones, tips
    static IEnumerable<Collider> HandColliders(VrHand h)
    {
        foreach (var p in h.parts)
            if (p.shape)
                yield return p.shape;
        foreach (var t in h.tips)
            yield return t.ball;
    }

    /// A joint of the drawn hand (tracked, or the shape it took hold with), world space
    Vector3 JointAt(VrHand h, XRHandJointID j) => transform.TransformPoint(h.bones[j.ToIndex()].localPosition);
    Quaternion JointTurn(VrHand h, XRHandJointID j) => transform.rotation * h.bones[j.ToIndex()].localRotation;

    /// Puts a hand part on this frame's joints (world space)
    void PlacePart(VrHand h, Part p)
    {
        p.active = true;
        // the shape is in the joint's frame, as the skin it was cut from
        p.pos = JointAt(h, p.from);
        p.rot = JointTurn(h, p.from);
        if (p.tip >= 0)
        {
            // the tip ball: the pad as far from the tip joint as on the tracked hand
            var t = h.tips[p.tip];
            var tipJoint = TipJoints[p.tip];
            t.ballAt = JointAt(h, tipJoint) + JointTurn(h, tipJoint) * t.padLocal;
        }
    }

    /// A tower block a hand part overlaps where the tracked hand has it, padded by `pad`
    Rigidbody PartIn(Part p, float pad)
    {
        var hull = p.shape.sharedMesh;
        if (!hull)
            return null;
        Vector3 centre = p.pos + p.rot * hull.bounds.center;
        foreach (var c in Physics.OverlapSphere(centre, hull.bounds.extents.magnitude + pad, NotHands, QueryTriggerInteraction.Ignore))
        {
            if (!tower.Standing(c.attachedRigidbody))
                continue;
            // within `pad`: the shape moved that far toward the block overlaps it
            Vector3 toward = (c.ClosestPoint(centre) - centre).normalized;
            if (Physics.ComputePenetration(p.shape, p.pos + toward * pad, p.rot, c, c.transform.position, c.transform.rotation, out _, out _))
                return c.attachedRigidbody;
        }
        return null;
    }

    /// Holding, the drawn hand keeps the shape it took hold with, only the wrist following the real one: the real
    /// fingers feel no block and close on through it
    void HoldShape(VrHand h)
    {
        var wrist = h.bones[XRHandJointID.Wrist.ToIndex()];
        if (!h.held)
        {
            h.heldShape = null;
            return;
        }
        if (h.heldShape == null)
        {
            h.heldShape = new Pose[h.bones.Length];
            for (int i = 0; i < h.bones.Length; i++)
                h.heldShape[i] = new Pose(Quaternion.Inverse(wrist.localRotation) * (h.bones[i].localPosition - wrist.localPosition),
                    Quaternion.Inverse(wrist.localRotation) * h.bones[i].localRotation);
        }
        for (int i = 0; i < h.bones.Length; i++)
            h.bones[i].SetLocalPositionAndRotation(wrist.localPosition + wrist.localRotation * h.heldShape[i].position,
                wrist.localRotation * h.heldShape[i].rotation);
    }

    /// The fingertips and pinch from the tracked hand, or else from its controller
    void ReadHand(VrHand h)
    {
        ReadHandPose(h);
        string now = !h.tracked ? "none" : h.fingers ? "hand" : "controller";
        if (now != h.was)
            Debug.Log($"[BT] VR {h.node}: {now}");
        h.was = now;
    }

    void ReadHandPose(VrHand h)
    {
        bool leftHand = h.node == XRNode.LeftHand;
        var tracked = handSubsystem != null ? (leftHand ? handSubsystem.leftHand : handSubsystem.rightHand) : default;
        if (handSubsystem != null && tracked.isTracked
            && tracked.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var knuckle)
            && tracked.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm)
            && tracked.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist))
        {
            h.tracked = h.fingers = true;
            h.wristPos = transform.TransformPoint(wrist.position);
            h.wristRot = transform.rotation * wrist.rotation;
            h.lowestTip = float.MaxValue;
            h.highestTip = float.MinValue;
            for (int i = 0; i < 5; i++)
            {
                var t = h.tips[i];
                t.active = tracked.GetJoint(TipJoints[i]).TryGetPose(out var tipPose);
                if (t.active)
                {
                    // the ball as thick as the robot fingertip (half its end's thickness), its bottom on the pad: the
                    // tip joint is the finger's very end on its middle line, so back one radius along the bone
                    // (forward points to the tip)
                    float radius = t.ball.radius = RobotTipRadius[i];
                    t.padLocal = -new Vector3(0f, 0f, radius);
                    Vector3 pad = tipPose.position + tipPose.rotation * t.padLocal;
                    t.target = transform.TransformPoint(pad);
                    if (i > 0)
                    {
                        h.lowestTip = Mathf.Min(h.lowestTip, tipPose.position.y);
                        h.highestTip = Mathf.Max(h.highestTip, tipPose.position.y);
                    }
                }
            }
            if (!h.radiusLogged)
            {
                h.radiusLogged = true;
                Debug.Log($"[BT] VR {h.node} fingertip radii mm: {string.Join(", ", System.Array.ConvertAll(h.tips, x => (x.ball.radius * 1000f).ToString("F1")))}");
            }
            for (int i = 0; i < h.bones.Length; i++)
                if (tracked.GetJoint(XRHandJointIDUtility.FromIndex(i)).TryGetPose(out var joint))
                    h.bones[i].SetLocalPositionAndRotation(joint.position, joint.rotation);
            GripVR(h);
            HoldShape(h);
            // on the hand as drawn: holding, the shape it took hold with, so the physics fingers don't close on
            // through the block they hold (they don't meet it) as the real ones do
            foreach (var part in h.parts)
                PlacePart(h, part);
            h.forward = (h.tips[Index].target - transform.TransformPoint(knuckle.position)).normalized;
            h.up = transform.rotation * (palm.rotation * Vector3.up);
            h.palm = palm.position;
            h.rot = transform.rotation * palm.rotation;
            return;
        }
        h.fingers = false;
        foreach (var part in h.parts)
            part.active = false;
        var device = InputDevices.GetDeviceAtXRNode(h.node);
        if (device.isValid && device.TryGetFeatureValue(CommonUsages.devicePosition, out var p)
            && device.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
        {
            h.tracked = true;
            Quaternion rot = transform.rotation * r;
            // the grip pose sits in the palm: the index tip rests ahead of it and a little down, on the trigger
            for (int i = 0; i < 5; i++)
                h.tips[i].active = i == Index;
            h.tips[Index].target = transform.TransformPoint(p) + rot * new Vector3(0f, -0.025f, 0.075f);
            h.pinchPoint = h.tips[Index].target;
            // the index finger's bones straight along the controller up to that tip; no palm or other fingers
            var ball = h.tips[Index];
            ball.ballAt = ball.target;
            float back = 0f;
            for (int b = 2; b >= 0; b--)
            {
                var bone = h.parts.Find(x => x.from == FingerBones[Index, b]);
                float l = BoneLength[b];
                bone.active = true;
                bone.pos = ball.target - rot * Vector3.forward * (back + l);
                bone.rot = rot;
                back += l;
            }
            h.pinching = trigger[leftHand ? 0 : 1].ReadValue<float>() > (h.held ? 0.4f : 0.6f);
            h.rot = rot;
            h.wristPos = transform.TransformPoint(p);
            h.wristRot = rot;
            h.forward = rot * Vector3.forward;
            h.up = rot * Vector3.up;
            return;
        }
        h.tracked = h.pinching = false;
        foreach (var t in h.tips)
            t.active = false;
    }

    /// Puts each collider where the tracked hand has its bone and the hand's body on the tracked wrist
    void DriveHand(VrHand h, bool playing)
    {
        // lost, the hand stays where it was
        if (!h.tracked)
        {
            h.lost = true;
            return;
        }
        Quaternion toBody = Quaternion.Inverse(h.wristRot);
        foreach (var p in h.parts)
            if (p.active)
                p.shape.transform.SetLocalPositionAndRotation(toBody * (p.pos - h.wristPos), toBody * p.rot);
        foreach (var t in h.tips)
            t.ball.transform.localPosition = toBody * (t.ballAt - h.wristPos);
        // off the tower only when it isn't my go; on again once out of every block (switched on inside one, it
        // would throw it out)
        bool was = h.ghost;
        h.slipPos = h.body.position - h.aimPos;
        h.slipRot = h.body.rotation * Quaternion.Inverse(h.aimRot);
        // Stopped by a block the body stays on it, however far the real hand goes on (the drawn hand with it). Put
        // there, not swept through the tower on the way, and off till out of every block, only when the hand came
        // back elsewhere: the rig recentred or swung round, tracking returned, or the real hand 300 mm off it (a
        // whole arm past the tower: wedged)
        bool rigMoved = transform.position != h.rigPos || transform.rotation != h.rigRot;
        (h.rigPos, h.rigRot) = (transform.position, transform.rotation);
        bool jump = rigMoved || h.lost || Vector3.Distance(h.body.position, h.wristPos) > 0.3f;
        h.lost = false;
        if (!playing || jump)
        {
            h.ghost = true;
            Put(h.body, h.wristPos, h.wristRot);
            (h.slipPos, h.slipRot) = (Vector3.zero, Quaternion.identity);
        }
        else
        {
            Follow(h.body, h.wristPos, h.wristRot);
            if (h.ghost && !HandInBlock(h))
                h.ghost = false;
        }
        if (h.ghost != was)
            Debug.Log($"[BT] VR {h.node} hand {(h.ghost ? "ghost" : "solid")}");
        (h.aimPos, h.aimRot) = (h.wristPos, h.wristRot);
        bool held = h.slipPos.magnitude > 0.02f;  // for the log: the tower holding the hand back
        if (held != h.heldBack)
            Debug.Log($"[BT] VR {h.node} {(held ? "held back by a block" : "free")}");
        h.heldBack = held;
        Solid(h, !h.ghost);
        // every step: a collider switched off and on forgets what it was told to ignore
        if (h.held)
            IgnoreHeld(h, h.held, true);
    }

    /// The tracked hand drawn where its body is: the real hand's pose moved by the body's slip, about the wrist
    void DrawAtBody(VrHand h)
    {
        Vector3 at = Drawn(h, transform.position);
        Quaternion turn = h.slipRot * transform.rotation;
        h.drawn.SetPositionAndRotation(at, turn);
        h.real.transform.SetPositionAndRotation(at, turn);
    }

    /// Holding, the hand model takes the drawn joints (the shape it took hold with, HoldShape) instead of the tracked
    /// ones: the real fingers close on through a block they can't feel, the drawn ones stay on it
    void DrawHeld(VrHand h)
    {
        bool hold = h.held && h.real.activeSelf;
        if (h.skeleton.enabled == hold)
            h.skeleton.enabled = !hold;
        if (!hold)
            return;
        // the driver puts each joint at its tracked pose, the wrist under the model root and each joint under its
        // parent: the same world poses as the drawn joints, both placed by DrawAtBody
        foreach (var r in h.skeleton.jointTransformReferences)
        {
            var bone = h.bones[r.xrHandJointID.ToIndex()];
            if (r.jointTransform)
                r.jointTransform.SetPositionAndRotation(bone.position, bone.rotation);
        }
    }

    /// A point on the tracked hand where the hand is drawn: moved with its body's slip about the wrist
    static Vector3 Drawn(VrHand h, Vector3 tracked) => h.wristPos + h.slipPos + h.slipRot * (tracked - h.wristPos);

    /// A body put straight there, not swept on the way, and still
    static void Put(Rigidbody b, Vector3 at, Quaternion turn)
    {
        Scenery.Teleport(b, at, turn);
        if (!b.isKinematic)
            b.linearVelocity = b.angularVelocity = Vector3.zero;
    }

    /// The physics hand to the tracked wrist by the next step, pushing with at most pushStrength (and the turning
    /// force for it at a fingertip): what it meets that holds harder stops it
    void Follow(Rigidbody b, Vector3 at, Quaternion turn)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 force = ((at - b.position) / dt - b.linearVelocity) * (b.mass / dt);
        b.AddForce(Vector3.ClampMagnitude(force, pushStrength));
        (turn * Quaternion.Inverse(b.rotation)).ToAngleAxis(out float deg, out Vector3 axis);
        if (deg > 180f)
            deg -= 360f;
        Vector3 spin = float.IsFinite(axis.x) ? axis * (deg * Mathf.Deg2Rad / dt) : Vector3.zero;
        Vector3 torque = (spin - b.angularVelocity) * (b.inertiaTensor.x / dt);  // a ball: the same about every axis
        b.AddTorque(Vector3.ClampMagnitude(torque, pushStrength * HandReach));
    }

    /// The hand's colliders on (those placed this frame) or off. One body: its own parts never meet each other
    void Solid(VrHand h, bool on)
    {
        foreach (var p in h.parts)
            p.shape.enabled = on && p.active;
        for (int i = 0; i < 5; i++)
            h.tips[i].ball.enabled = on && TipBone(h, i).active;
    }

    /// The hand's colliders pass through the block its pads hold (the joint holds it), and meet it again let go
    void IgnoreHeld(VrHand h, Rigidbody block, bool ignore)
    {
        var box = block.GetComponent<Collider>();
        foreach (var c in HandColliders(h))
            Physics.IgnoreCollision(c, box, ignore);
    }

    /// Any part of the tracked hand in a tower block
    bool HandInBlock(VrHand h)
    {
        foreach (var t in h.tips)
            if (t.active && tower.AnyBlockAt(t.target, t.ball.radius * 1.2f))
                return true;
        foreach (var p in h.parts)
            if (p.active && PartIn(p, 0.002f))
                return true;
        return false;
    }
}
