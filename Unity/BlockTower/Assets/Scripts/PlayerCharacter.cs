using UnityEngine;

/// First person player without an avatar: the camera rides the capsule.
/// WASD walks, mouse looks, left click sits on the chair in front, E stands up.
/// Seated, the right hand works the blocks: aim at the short end of a block, hold left click and drag the mouse
/// any way. A block sticking out toward you is pinched and pulled; any other is pushed with the index finger.
/// Push one out the far side, let go, and the view swings round so you can pull it there.
/// In a headset the hands are the player's own (PlayerCharacter.VR.cs).
[RequireComponent(typeof(CharacterController))]
public partial class PlayerCharacter : TablePlayer
{
    public Camera view;
    /// This player's mouse and keyboard (the editor's)
    public Controls controls = new();
    public override string DisplayName => seat >= 0 ? GameText.Player(seat) : GameText.Me;
    public override Rigidbody Moving => moving;

    [Header("Input")]
    /// Degrees per mouse axis unit
    public float mouseSensitivity = 2f;

    [Header("Movement")]
    /// Indoor walking pace with a short ease-in and ease-out
    public float walkSpeed = 1.5f;
    public float acceleration = 6f;
    public float deceleration = 5f;
    public float gravity = 9.81f;

    [Header("Camera")]
    /// Standing eye height above the floor
    public float standingEyeHeight = 1.65f;

    [Header("Chair")]
    /// Max floor distance from the player to a chair seat it can sit on
    public float reachDistance = 1.5f;
    /// Seated eye height above the floor
    public float seatedEyeHeight = 1.15f;
    /// Time to sit down or stand up, s
    public float sitTime = 1f;
    /// How far the seated player can turn the head from the chair facing, deg
    public float seatedYawLimit = 75f;

    [Header("Hand")]
    /// Right hand model (Assets/Hand/RightHand.fbx); visual only, it follows the physical fingertip
    public GameObject handModel;
    /// Physical fingertip, 12 mm thick, 70 mm long (an invisible capsule under the hand model)
    public float fingerDiameter = 0.012f;
    /// Most the pinching fingers can pull, N (pad friction). A loose block slides out;
    /// Game call: 12 N, above the 11.0 N a bottom block carrying the whole tower needs, so every block can come out
    /// (CAD/checks/tower_mechanics.py section 6); as strong as the push (pushStrength)
    public float pinchStrength = 12f;
    /// A block that left the tower is taken once it has lain still this long wherever it fell, then moves to the pile, s
    public float settleTime = 0.5f;
    /// Push a block out the far side at least this far, then let go, and the view swings round to pull it there.
    /// Loose on purpose: by hand nobody can tell 9 mm from 15 mm
    public float swingPush = 0.008f;

    float settled;  // how long the moved block has lain still clear of the tower

    enum SeatState { Walking, PullingChair, SittingDown, TuckingIn, Seated, PullingOut, StandingUp }

    CharacterController controller;
    DiningChair[] chairs;
    TowerSpawner tower;
    SeatState state = SeatState.Walking;
    float stateTime, stateDuration;
    DiningChair chair, aimedChair;
    Vector3 moveFrom, moveTo, velocity;
    float yaw, pitch, turnFromYaw, turnFromPitch, turnToYaw, turnToPitch, standingY;
    string prompt;
    static bool GameOver => TowerGame.Instance.Over;
    /// The hands' own colliders (fingertips, the headset's hands) are on Ignore Raycast: aim traces and block
    /// queries look past them
    public const int HandLayer = 2;
    public const int NotHands = ~(1 << HandLayer);
    Rigidbody moving;  // the block last held: the one being moved this turn, exempt from the fall check
    Vector3 blockStart;  // where it lay when it was first moved
    bool holding;
    Rigidbody committed;  // touch-move house rule: the first block pushed or pulled must be taken out before any other

    // Selection blink: the aimed block's tint pulses blue, red when the rules forbid it. The block touched first stays
    // red until it is out, for everyone at the table
    [Header("Selection")]
    public float blinkHz = 2f;
    Component lit;
    bool chairInReach;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        chairs = FindObjectsByType<DiningChair>();
        tower = FindAnyObjectByType<TowerSpawner>();
        view.transform.localPosition = new Vector3(0f, standingEyeHeight, 0f);
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        TowerGame.Instance.Join(this);

        finger = new GameObject("Hand Driver").AddComponent<Rigidbody>();
        finger.isKinematic = true;
        finger.gameObject.SetActive(false);

        var tip = new GameObject("Fingertip");
        tip.layer = HandLayer;  // so the aim trace sees the block behind it
        var capsule = tip.AddComponent<CapsuleCollider>();  // along local Y, like the push direction
        capsule.radius = fingerDiameter / 2f;
        capsule.height = fingerLength;
        tipCapsule = capsule;
        fingertip = tip.AddComponent<Rigidbody>();
        fingertip.mass = 0.02f;  // finger end segment
        fingertip.useGravity = false;
        fingertip.interpolation = RigidbodyInterpolation.Interpolate;
        // both bodies start at the origin, so the spring's rest pose is "fingertip on the driver"
        var press = tip.AddComponent<ConfigurableJoint>();
        press.connectedBody = finger;
        press.angularXMotion = press.angularYMotion = press.angularZMotion = ConfigurableJointMotion.Locked;
        var spring = new JointDrive { positionSpring = 2000f, positionDamper = 12.6f, maximumForce = pushStrength };  // critical for 20 g
        press.xDrive = press.yDrive = press.zDrive = spring;
        tip.SetActive(false);

        hand = Instantiate(handModel).AddComponent<HandVisual>();
        foreach (var r in hand.GetComponentsInChildren<Renderer>())
            r.material.color = Palette.Skin;
        hand.gameObject.SetActive(false);

        vr = UnityEngine.XR.XRSettings.isDeviceActive;
        if (vr)
            StartVR();
    }

    void Update()
    {
        if (vr)
        {
            UpdateVR();
            return;
        }
        if (controls.Pressed(UnityEngine.InputSystem.Key.F9))
            TowerGame.Instance.Restart(this);  // the headset menu's 재시작
        if (GameOver)
        {
            prompt = null;
            Look();  // free to look round at the fall
            return;
        }
        stateTime += Time.deltaTime;
        float alpha = stateDuration > 0f ? Mathf.Min(1f, stateTime / stateDuration) : 1f;
        float smooth = Mathf.SmoothStep(0f, 1f, alpha);
        prompt = null;
        Look();

        if (chair)
            CollectTaken();

        switch (state)
        {
            case SeatState.Walking:
                Walk();
                aimedChair = FindChair();
                if (aimedChair)
                {
                    prompt = chairInReach ? GameText.Sit : GameText.CloserToChair;
                    if (chairInReach && controls.ClickDown)
                        SitDown();
                }
                break;

            case SeatState.PullingChair:
                if (alpha >= 1f)
                {
                    // Lower the body so the camera ends at seated eye height over the seat
                    Vector3 seat = chair.SeatPosition;
                    moveFrom = transform.position;
                    moveTo = new Vector3(seat.x, chair.transform.position.y + seatedEyeHeight - standingEyeHeight, seat.z);
                    (turnFromYaw, turnFromPitch) = (yaw, pitch);
                    turnToYaw = yaw + Mathf.DeltaAngle(yaw, FacingYaw);
                    turnToPitch = 20f;  // looking down at the tower
                    EnterState(SeatState.SittingDown, sitTime);
                }
                break;

            case SeatState.SittingDown:
            case SeatState.StandingUp:
                transform.position = Vector3.Lerp(moveFrom, moveTo, smooth);
                if (state == SeatState.SittingDown)
                {
                    yaw = Mathf.Lerp(turnFromYaw, turnToYaw, smooth);
                    pitch = Mathf.Lerp(turnFromPitch, turnToPitch, smooth);
                }
                if (alpha >= 1f)
                {
                    if (state == SeatState.SittingDown)
                    {
                        transform.SetParent(chair.transform, true);  // ride along while scooting in
                        EnterState(SeatState.TuckingIn, chair.SlideTo(chair.pullOutDistance - chair.tuckInDistance));
                    }
                    else
                    {
                        controller.enabled = true;
                        chair.Free(this);
                        chair = null;
                        EnterState(SeatState.Walking, 0f);
                    }
                }
                break;

            case SeatState.TuckingIn:
                if (alpha >= 1f)
                    EnterState(SeatState.Seated, 0f);
                break;

            case SeatState.Seated:
                Seated();
                break;

            case SeatState.PullingOut:
                if (alpha >= 1f)
                {
                    transform.SetParent(null, true);
                    // step back behind the pulled-out chair
                    Vector3 behind = chair.SeatPosition - chair.Facing * 0.75f;
                    moveFrom = transform.position;
                    moveTo = new Vector3(behind.x, standingY, behind.z);
                    EnterState(SeatState.StandingUp, sitTime);
                }
                break;
        }
    }

    float FacingYaw => Quaternion.LookRotation(chair.Facing).eulerAngles.y;

    void Look()
    {
        if (holding)
        {
            // the view holds still while the hand works: mouse up pushes the block in, mouse down pulls it out
            advance += controls.Look.y * fingerDragScale;
        }
        else if (state is SeatState.Walking or SeatState.Seated)
        {
            yaw += controls.Look.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - controls.Look.y * mouseSensitivity, -89f, 89f);
        }
        if (state == SeatState.Seated)
        {
            // a seated head turns, the body does not spin
            float facing = FacingYaw;
            yaw = facing + Mathf.Clamp(Mathf.DeltaAngle(facing, yaw), -seatedYawLimit, seatedYawLimit);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // yaw stays relative to the own seat; the far side adds the 180 deg swing about the tower axis
        float s = Mathf.SmoothStep(0f, 1f, sideBlend);
        view.transform.localPosition = new Vector3(0f, standingEyeHeight, 0f);
        view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (s > 0f)
            view.transform.SetPositionAndRotation(SwungEye(s), Quaternion.Euler(pitch, yaw + 180f * s, 0f));
        // the fall jolts the view, dying away
        float shake = TowerGame.Instance.Shake;
        if (shake > 0f)
        {
            float t = Time.unscaledTime * 25f;
            view.transform.rotation *= Quaternion.Euler(shake * 1.5f * (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                shake * 1.5f * (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f, 0f);
        }
    }

    /// The pads' hold on a block: in the tower at most pinchStrength (critically damped for a 32 g block, the maple/walnut mean, its weight
    /// sags it 0.3 mm; the levels above clamp a block and it slips); `firm`, clear of the tower, stiff, so it doesn't
    /// trail behind the hand like on a rubber band
    void PinchDrive(ConfigurableJoint pinch, bool firm)
    {
        pinch.xDrive = pinch.yDrive = pinch.zDrive = firm
            ? new JointDrive { positionSpring = 5000f, positionDamper = 25.5f, maximumForce = 50f }
            : new JointDrive { positionSpring = 1000f, positionDamper = 11.4f, maximumForce = pinchStrength };
        pinch.slerpDrive = new JointDrive { positionSpring = 2f, positionDamper = 0.02f, maximumForce = firm ? 5f : pinchStrength * 0.04f };
    }

    void Walk()
    {
        var input = Vector3.ClampMagnitude(new Vector3(controls.Move.x, 0f, controls.Move.y), 1f);
        Vector3 target = transform.TransformDirection(input) * walkSpeed;
        Vector3 flat = new(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, target, (input.sqrMagnitude > 0f ? acceleration : deceleration) * Time.deltaTime);
        velocity.y = controller.isGrounded ? -0.5f : velocity.y - gravity * Time.deltaTime;
        velocity = new Vector3(flat.x, velocity.y, flat.z);
        controller.Move(velocity * Time.deltaTime);
    }

    /// The free chair under the crosshair (its backrest shows above the table even when tucked in).
    /// A slightly fat ray so the thin backrest rails are easy to hit
    DiningChair FindChair()
    {
        var ray = new Ray(view.transform.position, view.transform.forward);
        if (!Physics.SphereCast(ray, 0.03f, out var hit, 4f, NotHands, QueryTriggerInteraction.Ignore))
            return null;
        var c = hit.collider.GetComponentInParent<DiningChair>();
        if (!c || c.occupant)
            return null;
        chairInReach = Vector3.ProjectOnPlane(c.SeatPosition - transform.position, Vector3.up).magnitude < reachDistance;
        return c;
    }

    void SitDown()
    {
        chair = aimedChair;
        chair.occupant = this;
        aimedChair = null;
        standingY = transform.position.y;
        velocity = Vector3.zero;
        controller.enabled = false;
        EnterState(SeatState.PullingChair, chair.SlideTo(chair.pullOutDistance));
    }

    void Seated()
    {
        UpdateSwing();
        if (controls.Pressed(UnityEngine.InputSystem.Key.E))
        {
            Release();
            fingerBlock = null;
            ShowHand(false);
            farSide = false;
            sideBlend = 0f;
            pushedOut = null;
            DropCommitted();  // standing up: the touched block is no one's to finish
            EnterState(SeatState.PullingOut, chair.SlideTo(chair.pullOutDistance));
            return;
        }

        if (controls.ClickUp)
            Release();  // re-aim below; a pinched block drops
        if (controls.ClickDown && fingerBlock && allowed)
        {
            holding = true;
            moving = fingerBlock;
            advance = 0f;
            grip = Grip.None;
            blockStart = fingerBlock.position;
        }

        if (holding && !fingerBlock)
            Release();  // the held block was destroyed (tower rebuilt under the hand)
        if (holding)
            Work();
        else if (sideBlend != (farSide ? 1f : 0f))
        {
            // mid-swing the aim sweeps across the tower: a fingertip placed there would knock blocks back in
            fingerBlock = null;
            ShowHand(false);
        }
        else if (!AimFinger())
        {
            fingerBlock = null;
            ShowHand(false);
            prompt = GameText.Aim(farSide);
        }
        else if (committed && fingerBlock != committed)
        {
            fingerBlock = null;
            ShowHand(false);
            prompt = GameText.FinishFirst;
        }
        else
            prompt = allowed ? GameText.ClickToWork : GameText.NotTheTop;
    }

    /// The block moved this turn fell or was pulled clear of the tower: once it lies still, wherever it landed,
    /// it goes to this seat's pile, so the table shows who took how many
    void CollectTaken()
    {
        // not once the tower has fallen: the new tower clears the piles, and a block taken then vanished with them
        if (!moving || holding || GameOver || moving.transform.parent != tower.transform)
            return;
        // off the tower and dropped below where it was grabbed: it fell, it isn't just hanging out of the tower
        // clamped by the levels above (seen: 60 mm out and still held)
        bool clear = tower.OffTower(moving) && moving.position.y < blockStart.y - 0.01f;
        settled = clear && TowerSpawner.Still(moving) ? settled + Time.deltaTime : 0f;
        if (settled < settleTime)
            return;
        Debug.Log($"Block extracted: {moving.name} by {DisplayName}");
        if (fingerBlock == moving)
            fingerBlock = null;
        if (pushedOut == moving)
            pushedOut = null;
        if (committed == moving)
        {
            Tint(committed, Palette.NoTint);
            committed = null;
        }
        chair.LayDown(moving, tower.transform.position.y);
        moving = null;
        TowerGame.Instance.Taken(this);
        if (vrNetGame)
            vrTakenAt = Time.time;  // the turn passes once the tower is still (PassWhenStill)
        settled = 0f;
        if (farSide)
            Swing(false);
    }

    void FixedUpdate()
    {
        if (vr)
        {
            FixedUpdateVR();
            return;
        }
        if (holding)
        {
            fingerTip = Vector3.MoveTowards(fingerTip, targetTip, handSpeed * Time.fixedDeltaTime);
            PlaceFinger(teleport: false);
        }
    }

    void LateUpdate()
    {
        if (vr)
        {
            LateUpdateVR();
            return;
        }
        // after physics interpolation, so the hand sits exactly on the fingertip
        if (hand.gameObject.activeSelf)
            hand.Place(grip == Grip.Pull ? HandVisual.Pose.Pinch : HandVisual.Pose.Point,
                grip == Grip.Pull ? PinchShown() : fingertip.transform.position + fingertip.transform.up * (fingerLength / 2f),
                pushDir, Vector3.up);
        if (hand.gameObject.activeSelf)
            Blink(fingerBlock, allowed);
        else
            Blink(state == SeatState.Walking ? aimedChair : null, chairInReach);
    }

    /// Pulses the tint of the selected block or chair; restores the last one when the selection moves
    void Blink(Component target, bool ok)
    {
        if (lit && lit != target)
            Tint(lit, lit == committed ? Palette.CommittedTint : Palette.NoTint);
        lit = target;
        if (target)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * blinkHz * 2f * Mathf.PI);
            Tint(target, Color.Lerp(Palette.NoTint, ok ? Palette.AllowedTint : Palette.ForbiddenTint, pulse));
        }
    }

    void DropCommitted()
    {
        if (committed)
            Tint(committed, Palette.NoTint);
        committed = null;
    }

    void EnterState(SeatState next, float duration)
    {
        state = next;
        stateTime = 0f;
        stateDuration = duration;
    }

    /// The tower fell (TowerGame): let go, no swing round to a dead tower
    public override void TowerFell()
    {
        ReleaseVR();
        pushedOut = null;
        Release();
        ShowHand(false);
    }

    /// A new tower is being stacked (TowerGame): drop whatever the hand holds, back to the own seat
    public override void NewRound()
    {
        ReleaseVR();
        Release();
        fingerBlock = null;
        moving = null;
        pushedOut = null;
        committed = null;
        farSide = false;
        sideBlend = 0f;
    }

    /// Free the chair, take the hand away
    void OnDestroy()
    {
        TowerGame.Instance?.Leave(this);
        if (chair)
            chair.Free(this);
        foreach (var body in new Component[] { finger, fingertip, hand })
            if (body)
                Destroy(body.gameObject);
        DestroyVR();
    }
}
