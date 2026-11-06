using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// The same player in a headset (Quest 3, OpenXR), played sitting down: a headset player sits at a real desk, so the
/// game starts already seated on a random free chair, no walking. The head is the tracked headset; the hands are the
/// player's own, tracked, or the controllers.
/// The seat height comes from the real head height, until a palm has rested flat on the real desk for a second: then
/// the virtual table top is put at the real desk, so a block pushed along the table is pushed along the desk.
/// Recentring the headset (hold the Meta button) puts the head back over the seat and measures the head height again.
/// Tracked hands are drawn from their 26 joints (XR Hands skeleton driver), so every finger moves as the real one;
/// the hand is one body following the tracked wrist with at most pushStrength, its colliders the hand mesh cut per bone (convex hulls on the
/// tracked joints), so any part of the hand pushes a block it meets, as a real hand does. The thumb and any finger
/// touching a block from either side hold it with at most pinchStrength, as Pinch() does; the drawn fingers keep the
/// holding shape however far the real ones close. With controllers there are no finger joints: one fingertip ahead of
/// the controller, the trigger pinches, and the posed hand (HandVisual) shows where it is.
/// The menu opens with the left hand's menu gesture (palm toward the face, pinch) or the
/// left controller's menu button; its buttons are pressed with a fingertip.
/// Rules, piles, turns and the fall are the desktop player's. Text is a panel over the table (no IMGUI in a headset).
/// Only on when an XR device is running: the editor never starts one.
public partial class PlayerCharacter
{
    /// The headset player's hands, tracked or on a controller: the robot hand (Assets/Hand/RightRobotHand.fbx, made
    /// by HumanoidHand/export_unity.py), drawn and solid as it is. The posed left hand is this one mirrored
    public GameObject vrHandModel;
    /// Its left (Assets/Hand/LeftRobotHand.fbx), for the tracked left hand
    public GameObject leftHandModel;

    bool vr;
    readonly VrHand[] vrHands = new VrHand[2];
    XRHandSubsystem handSubsystem;
    TextMesh vrText;
    TextMesh vrHud;            // top middle of the view: blocks taken
    float vrSeatedAt = -100f;  // sat down at the table: the how-to lines show this long (IntroSeconds)
    /// How long after sitting down the how-to lines show over the tower, s (all of them are in the menu's 게임 방법)
    const float IntroSeconds = 5f;
    float vrSince, vrSlipTime = -10f;
    Rigidbody vrHover;  // the block under a fingertip or between the pads, for the blink

    // OpenXR gives the old XR input (InputDevices) only the controller poses: the trigger and buttons are read
    // through the Input System. [0] left, [1] right
    readonly InputAction[] trigger = new InputAction[2];
    InputAction menuButton;

    static InputAction Bind(string hand, string control)
    {
        var a = new InputAction(binding: $"<XRController>{{{hand}}}/{{{control}}}");
        a.Enable();
        return a;
    }

    /// My go, seated, menu shut: the fingers may touch the tower
    /// Networked, also: this Quest runs the tower and hasn't taken its block yet
    bool PlayingVR => TowerGame.Instance.IsTurn(this) && state == SeatState.Seated && !vrMenu.activeSelf && !vrStart && SessionFocus.Focused
        && (!vrNetGame || (vrAuthority && vrTakenAt < 0f));

    void StartVR()
    {
        // the floor is the rig's origin, so the head and hands sit at their real heights
        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        foreach (var s in inputs)
        {
            s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            // The palms are kept: the headset sends this on every wake too (taken off and put on), and dropping them
            // put the player back to the old seat each time. Off after a real recentre: palms on the desk again
            s.trackingOriginUpdated += _ => vrCentred = false;
        }
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;

        var pose = view.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        var position = new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
        var rotation = new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
        position.Enable();
        rotation.Enable();
        pose.positionInput = new InputActionProperty(position);
        pose.rotationInput = new InputActionProperty(rotation);
        trigger[0] = Bind("LeftHand", "Trigger");
        trigger[1] = Bind("RightHand", "Trigger");
        menuButton = Bind("LeftHand", "MenuButton");
        // full-screen effects cost too much at 72 Hz per eye and some break in stereo
        var effects = view.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
        if (effects)
            Destroy(effects);

        // standing at the door from the start (the scene's player start), the start menu in front; into a game, at
        // the table (SitAtTable). The rig is put there once the headset reports a head
        vrDoor = new Pose(transform.position, transform.rotation);
        LoadDesk();  // the desk as set last time
        vrAtDoor = true;
        controller.enabled = false;

        vrHands[0] = MakeVrHand(XRNode.LeftHand, leftHandModel, Handedness.Left);
        vrHands[1] = MakeVrHand(XRNode.RightHand, vrHandModel, Handedness.Right);

        vrText = Scenery.Label(null, 0.0045f, Palette.Ink);
        vrText.anchor = TextAnchor.LowerCenter;
        vrHud = Scenery.Label(null, 0.0022f, Palette.Ink);
        vrHud.anchor = TextAnchor.UpperCenter;
        MakeMenu();
        MakeStart();
        StartNet();
        ShowStart();
        // the full tower from the start; one block to practise on is in the menu (연습 / 탑 바꾸기)
        Debug.Log($"[BT] VR on: {XRSettings.loadedDeviceName}, hand tracking {(handSubsystem != null ? "on" : "off")}");
    }

    void UpdateVR()
    {
        vrSince += Time.deltaTime;
        UpdateNet();
        if (handSubsystem == null || !handSubsystem.running)
        {
            // the hand subsystem can start after the scene does
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            handSubsystem = subsystems.Find(x => x.running) ?? (subsystems.Count > 0 ? subsystems[0] : null);
        }
        if (!vrCentred && vrSince > 0.5f && view.transform.localPosition != Vector3.zero)
            Recenter();

        // the left hand's menu gesture (the right one is the headset's own) or the controller's menu button
        var aim = MetaAimHand.left;
        bool gesture = aim != null && ((ulong)aim.aimFlags.ReadValue() & (ulong)MetaAimFlags.MenuPressed) != 0;
        bool focused = SessionFocus.Focused;  // under the system menu the hands are the system's
        if (focused && ((gesture && !vrMenuGesture) || menuButton.WasPressedThisFrame()))
        {
            if (vrMenu.activeSelf)
                CloseMenu();
            else
                OpenMenu();
        }
        vrMenuGesture = gesture;

        foreach (var h in vrHands)
            ReadHand(h);
        if (focused && vrMenu.activeSelf)
            PressMenu();
        else if (focused)
        {
            if (vrStart)
                PressStart();
            FindDesk();
        }
        UpdatePause();
        bool playing = PlayingVR;
        foreach (var h in vrHands)
            UpdatePinch(h, playing);
        if (!TowerGame.Instance.IsTurn(this))
            DropCommitted();
        SwingButton();
        if (!TowerSpawner.Practice)
            CollectTaken();  // practising, the block stays where it's put
    }

    /// A menu up (the game's or the system's) stops a single game. A networked game plays on: the others are at the table
    void UpdatePause()
    {
        bool pause = !vrNetGame && !vrStart && (vrMenu.activeSelf || !SessionFocus.Focused);
        if (pause == TowerGame.Instance.Paused)
            return;
        TowerGame.Instance.Paused = pause;
        // no physics step to follow the hands while paused: drawn on the real hand, and back without sweeping the tower
        foreach (var h in vrHands)
        {
            (h.slipPos, h.slipRot) = (Vector3.zero, Quaternion.identity);
            h.lost = !pause;
        }
        Debug.Log($"[BT] VR {(pause ? "paused" : "playing on")}");
    }

    void FixedUpdateVR()
    {
        bool playing = PlayingVR;
        foreach (var h in vrHands)
        {
            if (h.held)
            {
                h.grip.MovePosition(h.pinchPoint);
                h.grip.MoveRotation(h.rot);
                if (h.firm != tower.MostlyOut(h.held))
                {
                    h.firm = !h.firm;
                    PinchDrive(h.pinch, h.firm);
                }
            }
            DriveHand(h, playing);
            if (h.ghost)
                continue;
            foreach (var t in h.tips)
            {
                if (!t.active)
                    continue;
                var touched = tower.BlockAt(t.ball.transform.position, t.ball.radius + 0.003f);
                Touched(touched);
                TouchBuzz(h, t, touched);
                // the real fingertip 3 mm into a block that didn't move away: it's tight
                var box = touched ? touched.GetComponent<Collider>() : null;
                if (box && Physics.ComputePenetration(t.ball, t.target, Quaternion.identity, box, box.transform.position,
                        box.transform.rotation, out Vector3 outward, out float depth)
                    && depth > 0.003f && Mathf.Abs(outward.y) < 0.5f)  // pushed along, not pressed down on (all hold that)
                    (vrTight, vrTightTime) = (touched, Time.time);
            }
            foreach (var p in h.parts)
                if (p.active)
                    Touched(PartIn(p, 0.003f));
        }
        if (!holding && touchedAt.Count > 32)
            touchedAt.Clear();
    }

    void LateUpdateVR()
    {
        vrHover = null;
        bool hoverOk = false;
        bool focused = SessionFocus.Focused;  // the system draws its own hands over its menu
        foreach (var h in vrHands)
        {
            // a tracked hand draws itself from its joints; a controller shows the posed hand at its fingertip
            h.real.SetActive(focused && h.fingers);
            h.posed.gameObject.SetActive(focused && h.tracked && !h.fingers);
            if (!focused || !h.tracked)
                continue;
            DrawAtBody(h);
            DrawHeld(h);
            bool pinch = h.held || h.pinching;
            if (!h.fingers)
            {
                Vector3 contact = h.held ? h.held.transform.TransformPoint(h.heldLocal) : pinch ? h.pinchPoint : Drawn(h, h.tips[Index].target);
                h.posed.Place(pinch ? HandVisual.Pose.Pinch : HandVisual.Pose.Point, contact, h.forward, h.up);
            }
            var near = h.held ? h.held : tower.BlockAt(pinch ? h.pinchPoint : Drawn(h, h.tips[Index].target), 0.02f);
            if (near && !vrHover)
            {
                vrHover = near;
                hoverOk = tower.CanTake(near) && (!committed || near == committed) && !Tight(near);
            }
        }
        if (vrStart)
            PlaceStart();
        else if (chair && state == SeatState.Seated && !vrFar)
            chair.SitUnder(view.transform.position);  // the chair under the body, as it really sits
        if (PlayingVR)
            Blink(vrHover, hoverOk);
        else
            Blink(null, false);
        VrPanel();
    }

    /// The text panel: floats above the tower's far side, facing the player
    void VrPanel()
    {
        // the start menu has its own title and note
        vrText.gameObject.SetActive(!vrMenu.activeSelf && !vrStart);
        var game = TowerGame.Instance;
        bool intro = Time.time - vrSeatedAt < IntroSeconds;
        // blocks taken, top middle of the view, following the head
        vrHud.gameObject.SetActive(!vrMenu.activeSelf && !vrStart);
        var eye = view.transform;
        vrHud.transform.SetPositionAndRotation(eye.position + eye.forward * 0.5f + eye.up * 0.17f, eye.rotation);
        vrHud.text = GameText.BlocksTaken(taken);
        Vector3 toHead = Vector3.ProjectOnPlane(view.transform.position - tower.transform.position, Vector3.up).normalized;
        Vector3 at = tower.transform.position - toHead * 0.35f + Vector3.up * 0.42f;
        vrText.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - view.transform.position));
        string text;
        if (GameOver)
            text = $"{GameText.Fell}\n{GameText.NewRoundIn(Mathf.CeilToInt(Mathf.Max(0f, game.SecondsToReset)))}";
        else if (!game.IsTurn(this))
            text = !game.Current ? "" : vrNetGame ? GameText.TurnWithTime(game.Current.DisplayName, SecondsLeft) : GameText.Turn(game.Current.DisplayName);
        else if (vrNetGame && vrTakenAt >= 0f)
            text = GameText.TowerStops;
        // only what the player must know now: a rule in the way. How to play is the first 5 s and the menu's 게임 방법
        else if (Time.time - vrSlipTime < 2f || (vrHover && Tight(vrHover)))
            text = GameText.Tight;
        else if (vrHover && !tower.CanTake(vrHover))
            text = GameText.NotTheTop;
        else if (committed && vrHover && vrHover != committed)
            text = GameText.FinishFirst;
        else
            text = intro ? GameText.HowToTake : "";
        if (vrNetGame && game.IsTurn(this) && !GameOver && vrTakenAt < 0f)
            text = GameText.MyTurn(SecondsLeft) + "\n" + text;
        if (Time.time - game.NoteTime < TowerGame.NoteSeconds && game.Note != null)
            text = game.Note + "\n" + text;
        if (TowerSpawner.Practice)
            text = GameText.Practising + "\n" + text;
        if (intro)
            text += "\n\n" + GameText.HowToSetUp;
        vrText.text = text;
    }

    void DestroyVR()
    {
        foreach (var h in vrHands)
        {
            if (h == null)
                continue;
            // the drawn hands are the player's children and go with it
            foreach (var c in new Component[] { h.grip, h.posed, h.body })
                if (c)
                    Destroy(c.gameObject);
        }
        foreach (var o in new Object[] { vrText ? vrText.gameObject : null, vrHud ? vrHud.gameObject : null, vrMenu, vrStartRoot, vrSwing ? vrSwing.gameObject : null,
                     vrSwingText ? vrSwingText.gameObject : null })
            if (o)
                Destroy(o);
        foreach (var a in trigger)
            a?.Dispose();
        net?.Close();
        menuButton?.Dispose();
    }
}
