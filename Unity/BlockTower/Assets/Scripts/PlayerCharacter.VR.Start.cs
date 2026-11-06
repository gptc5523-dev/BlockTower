using System.Collections.Generic;
using UnityEngine;

/// The headset's start menu: the player stands at the door (the scene's start) and
/// in front, at chest height, the buttons hang one above the other as wooden blocks stacked like the tower, each
/// pushed in with a fingertip (a few mm, then a click: pushing a block out of the tower). Into a game (싱글, or a
/// networked game starting) the player sits at the table. Faces: main (싱글, 멀티, 설정, 종료), 멀티 (친구랑, 랜덤),
/// 친구랑 (방 만들기, 코드 입력), the code pad, the room (시작 for its host, 나가기) and 설정. The multiplayer ones talk
/// to the server (PlayerCharacter.VR.Multi)
public partial class PlayerCharacter
{
    class StartKey
    {
        public Transform block;      // the wooden block, its scale its size
        public TextMesh label;
        public Vector3 rest;         // the block's place in the stack (x across, y up, m), not pressed (LayOut)
        public int row;              // its row in the face, top down
        public float sink;           // how far a fingertip has it pushed in, m
        public System.Action press;
        public bool ready = true;    // pressable now (시작: two in the room)
    }

    /// Showing the start menu: the tower is out of reach
    bool vrStart;
    GameObject vrStartRoot, vrStartFace;
    TextMesh vrStartTitle, vrStartNote;
    readonly Dictionary<GameObject, List<StartKey>> vrStartKeys = new();
    GameObject vrStartMain, vrStartMulti, vrStartFriend, vrStartCode, vrStartRoom, vrStartSettings;
    StartKey vrStartMultiKey, vrStartGo;
    string vrCode = "";
    bool vrStartArmed;
    float vrStartSince;

    /// A start button: a tower block at this many times its size, so a fingertip finds it and its label reads
    const float KeyScale = 1.5f;
    /// Gap between blocks across and between rows, m
    const float KeyGap = 0.02f;
    /// The stack: this far ahead of the door, its middle (title included) this far below standing eye height, m:
    /// straight ahead when looking level
    const float StackAhead = 0.55f, StackBelowEye = 0.03f;
    /// The title's height over the top block (no note between), m
    const float TitleOver = 0.18f;
    /// A note line's height, and the gap a note adds under it, m
    const float NoteLine = 0.026f, NoteGap = 0.02f;
    /// How far a block goes in under a fingertip, and how far it must go to press (m)
    const float KeySink = 0.006f, KeyPress = 0.004f;

    bool vrAtDoor;               // standing at the door, not at the table
    Pose vrDoor;                 // the scene's start: just inside the door, facing the table

    /// Digits in a room code
    const int CodeLength = 4;

    void MakeStart()
    {
        vrStartRoot = new GameObject("VR Start");
        vrStartTitle = Scenery.Label(vrStartRoot.transform, 0.012f, Palette.Amber);
        vrStartTitle.text = GameText.Title;
        vrStartNote = Scenery.Label(vrStartRoot.transform, 0.0035f, Palette.Ink);
        vrStartNote.anchor = TextAnchor.UpperCenter;  // under the title, growing down over the blocks' place

        vrStartMain = Face(new[] { new (string, System.Action)[]
        {
            (GameText.Single, () =>
            {
                HideStart();
                SitAtTable(null);
                TowerGame.Instance.Restart(this);
            }),
            (GameText.Multi, () =>
            {
                if (net.Connected)
                    ShowFace(vrStartMulti);
                else
                    StartNote(GameText.NoServer);
            }),
            (GameText.Settings, () => ShowFace(vrStartSettings)),
            (GameText.Quit, Application.Quit),
        }});
        vrStartMulti = Face(new[] { new (string, System.Action)[]
        {
            (GameText.Friends, () => ShowFace(vrStartFriend)),
            (GameText.Random, () =>
            {
                net.Send(new NetMsg { t = NetType.Random });
                StartNote(GameText.Searching);
            }),
            (GameText.BackShort, () => ShowFace(vrStartMain)),
        }});
        vrStartFriend = Face(new[] { new (string, System.Action)[]
        {
            (GameText.CreateRoom, () => net.Send(new NetMsg { t = NetType.Create })),
            (GameText.EnterCode, () => { vrCode = ""; ShowFace(vrStartCode); }),
            (GameText.BackShort, () => ShowFace(vrStartMulti)),
        }});
        var digits = new List<(string, System.Action)[]>();
        for (int row = 0; row < 2; row++)
        {
            var keys = new (string, System.Action)[5];
            for (int i = 0; i < 5; i++)
            {
                string d = (row * 5 + i + 1) % 10 + "";  // 1 2 3 4 5 / 6 7 8 9 0
                keys[i] = (d, () => Digit(d));
            }
            digits.Add(keys);
        }
        digits.Add(new (string, System.Action)[]
        {
            (GameText.Erase, () => { if (vrCode.Length > 0) vrCode = vrCode[..^1]; CodeNote(); }),
            (GameText.BackShort, () => ShowFace(vrStartFriend)),
        });
        vrStartCode = Face(digits.ToArray(), 0.7f);
        vrStartRoom = Face(new[] { new (string, System.Action)[]
        {
            (GameText.Start, () =>
            {
                if (vrStartGo.ready)
                    net.Send(new NetMsg { t = NetType.Start });
                else
                    StartNote(GameText.NeedTwo);
            }),
            (GameText.LeaveRoom, () => { LeaveNet(); ShowFace(vrStartMulti); }),
        }});
        vrStartGo = vrStartKeys[vrStartRoom][0];
        vrStartMultiKey = vrStartKeys[vrStartMain][1];
        vrStartSettings = Face(new[] { new (string, System.Action)[]
        {
            (GameText.License, () =>
            {
                OpenMenu();
                Show(vrInfo);
            }),
            (GameText.BackShort, () => ShowFace(vrStartMain)),
        }});
        vrStartRoot.SetActive(false);
    }

    /// A face of the start menu: rows of blocks (first row nearest the tower), maple and walnut in turn as in the tower
    GameObject Face((string label, System.Action press)[][] rows, float size = 1f)
    {
        var face = new GameObject("Face");
        face.transform.SetParent(vrStartRoot.transform, false);
        var keys = vrStartKeys[face] = new List<StartKey>();
        int n = 0;
        foreach (var row in rows)
            foreach (var (label, press) in row)
            {
                var block = Scenery.Solid(PrimitiveType.Cube, face.transform);  // pressed by position, not by meeting the hand
                block.name = $"Start {label}";
                block.localScale = new Vector3(tower.BlockLength, tower.blockThickness, tower.blockWidth) * KeyScale * size;
                var finish = n++ % 2 == 1 && tower.blockMaterialOdd ? tower.blockMaterialOdd : tower.blockMaterial;
                if (finish)
                    block.GetComponent<Renderer>().sharedMaterial = finish;
                var text = Scenery.Label(block, 0.0032f * size, n % 2 == 0 ? Palette.Ink : Palette.OnMaple);
                text.text = label;
                // on the end grain facing the player
                text.transform.localScale = new Vector3(1f / block.localScale.x, 1f / block.localScale.y, 1f / block.localScale.z);
                text.transform.localPosition = new Vector3(0f, 0f, -0.51f);
                keys.Add(new StartKey { block = block, label = text, press = press });
            }
        face.SetActive(false);
        // each block's row, top down; LayOut places the shown ones
        int k = 0;
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < rows[r].Length; i++)
                keys[k++].row = r;
        return face;
    }

    /// The start menu on: up from the table to the door, the main face in front
    void ShowStart()
    {
        vrStart = true;
        vrStartRoot.SetActive(true);
        ReleaseVR();
        GoToDoor();
        ShowFace(vrStartMain);
        Debug.Log("[BT] VR start menu");
    }

    void HideStart()
    {
        vrStart = false;
        vrStartRoot.SetActive(false);
        Debug.Log("[BT] VR start menu closed");
    }

    void ShowFace(GameObject face)
    {
        foreach (var f in vrStartKeys.Keys)
            f.SetActive(f == face);
        vrStartFace = face;
        vrStartSince = Time.time;
        vrStartArmed = false;
        StartNote(face == vrStartCode ? GameText.TypeCode : "");
        if (face == vrStartCode)
            CodeNote();
        if (face != vrStartRoom && vrRoom != null && !vrNetGame)
            LeaveNet();  // backed out of a room some other way: out of it on the server too
    }

    void StartNote(string note) => vrStartNote.text = note;

    void CodeNote() => StartNote(GameText.Code(vrCode.PadRight(CodeLength, '_').Replace("_", " _").Trim()));

    void Digit(string d)
    {
        if (vrCode.Length >= CodeLength)
            return;
        vrCode += d;
        CodeNote();
        if (vrCode.Length == CodeLength)
        {
            StartNote(GameText.Joining(vrCode));
            net.Send(new NetMsg { t = NetType.Join, code = vrCode });
        }
    }

    /// The stack in front of the door at chest height, the title over it and the note beside it, every frame (a
    /// recentre moves the player)
    void PlaceStart()
    {
        Vector3 ahead = Vector3.ProjectOnPlane(vrDoor.rotation * Vector3.forward, Vector3.up).normalized;
        Quaternion facing = Quaternion.LookRotation(ahead);  // forward: away from the player
        MultiLabel();
        if (vrStartFace == vrStartRoom)
            RoomNote();  // the countdown ticks
        // top down: the title, the note under it (its lines push the blocks down), the blocks; the whole at the eyes
        LayOut();
        float stack = 0f;
        if (vrStartFace)
            foreach (var key in vrStartKeys[vrStartFace])
                stack = Mathf.Max(stack, -key.rest.y);
        int lines = string.IsNullOrEmpty(vrStartNote.text) ? 0 : vrStartNote.text.Split('\n').Length;
        float note = lines * NoteLine + (lines > 0 ? NoteGap : 0f);
        float height = TitleOver + note + stack;
        Vector3 top = vrDoor.position + ahead * StackAhead
            + Vector3.up * (standingEyeHeight - StackBelowEye + height / 2f - TitleOver - note);
        vrStartTitle.transform.SetPositionAndRotation(top + Vector3.up * (TitleOver + note), facing);
        vrStartNote.transform.SetPositionAndRotation(top + Vector3.up * (note + TitleOver / 2f - 0.01f), facing);
        if (!vrStartFace)
            return;
        foreach (var key in vrStartKeys[vrStartFace])
            key.block.SetPositionAndRotation(top + facing * (key.rest + new Vector3(0f, 0f, key.sink)), facing);
    }

    /// The face's shown blocks in rows, each row centred, no gap for a hidden one (시작 when not the host)
    void LayOut()
    {
        if (!vrStartFace)
            return;
        var keys = vrStartKeys[vrStartFace].FindAll(k => k.block.gameObject.activeSelf);
        int shownRow = -1, lastRow = -1;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i].row != lastRow)
            {
                lastRow = keys[i].row;
                shownRow++;
            }
            var inRow = keys.FindAll(k => k.row == lastRow);
            int at = inRow.IndexOf(keys[i]);
            Vector3 box = keys[i].block.localScale;
            keys[i].rest = new Vector3((at - (inRow.Count - 1) / 2f) * (box.x + KeyGap), -shownRow * (box.y + KeyGap), 0f);
        }
    }

    /// Into the game at the table: on `to`, or any free chair
    void SitAtTable(DiningChair to)
    {
        to ??= System.Array.Find(chairs, c => !c.occupant || c.occupant == (TablePlayer)this);
        if (chair != to)
        {
            if (chair)
                chair.Free(this);
            chair = to;
            chair.Seat(this);
        }
        vrAtDoor = false;
        vrSeatedAt = Time.time;
        EnterState(SeatState.Seated, 0f);
        Recenter();
        Debug.Log($"[BT] VR seated on {chair.name}");
    }

    /// Up from the table, back to the door
    void GoToDoor()
    {
        if (chair)
        {
            chair.Free(this);
            chair = null;
        }
        vrAtDoor = true;
        EnterState(SeatState.Walking, 0f);
        Recenter();
    }

    /// A fingertip pushes a block in; in far enough, it presses (once per touch: every fingertip must leave every
    /// block before the next). Not in the first moment of a face, while the finger that changed it leaves
    void PressStart()
    {
        if (!vrStartFace)
            return;
        bool inAny = false;
        foreach (var key in vrStartKeys[vrStartFace])
        {
            if (!key.block.gameObject.activeSelf)
                continue;  // 시작, not the host's
            float sink = 0f;
            foreach (var h in vrHands)
            {
                if (!h.tracked)
                    continue;
                foreach (var t in h.tips)
                {
                    if (!t.active)
                        continue;
                    Vector3 p = key.block.InverseTransformPoint(t.target);  // in block sizes, about its centre
                    if (Mathf.Abs(p.x) > 0.5f || Mathf.Abs(p.y) > 0.5f)
                        continue;
                    // how far the fingertip's front is past the block's resting front face
                    Vector3 inward = key.block.forward;
                    Vector3 front = key.block.position - inward * (key.sink + key.block.localScale.z / 2f);
                    float depth = Vector3.Dot(t.target - front, inward) + t.ball.radius;
                    if (depth > 0f && depth < key.block.localScale.z)
                        sink = Mathf.Max(sink, Mathf.Min(depth, KeySink));
                }
            }
            key.sink = sink;
            if (sink > 0f)
                inAny = true;
            if (sink >= KeyPress && vrStartArmed && Time.time - vrStartSince > 0.4f)
            {
                vrStartArmed = false;
                AudioSource.PlayClipAtPoint(SynthSound.Click(), key.block.position, 0.8f);
                Debug.Log($"[BT] VR start: {key.label.text}");
                key.press();
                return;
            }
        }
        if (!inAny)
            vrStartArmed = true;
    }

}
