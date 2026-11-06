using System.Collections.Generic;
using UnityEngine;

/// The headset's menu: a panel of buttons in front of the player, pressed with a fingertip
public partial class PlayerCharacter
{
    GameObject vrMenu;
    readonly List<(Transform box, System.Action press)> vrButtons = new();
    float vrMenuSince;
    bool vrMenuGesture;  // the left hand's menu gesture, last frame
    GameObject vrMain, vrInfo, vrLicenses, vrHowTo;  // the menu's faces: its buttons, 정보 (the game, its version), the licenses, 게임 방법
    TextMesh vrLicenseText, vrLicensePage;
    string[] vrLicensePages;
    int vrLicenseAt;
    /// Lines of the licenses shown at once
    const int LicenseLines = 22;

    /// Resources: the licenses shown
    const string LicensesPath = "Licenses/OpenSource", FontLicensePath = "Fonts/Pretendard-OFL";

    /// The menu as a panel of buttons in front of the player
    void MakeMenu()
    {
        vrMenu = new GameObject("VR Menu");
        vrMain = new GameObject("Buttons");
        vrMain.transform.SetParent(vrMenu.transform, false);
        void Button(GameObject panel, string label, Color color, float x, float y, float width, System.Action press)
        {
            var box = Box(panel, label, color, new Vector3(x, y, 0f), new Vector3(width, 0.045f, 0.01f));
            var text = Scenery.Label(panel.transform, 0.003f, Palette.MenuText);
            text.text = label;
            text.transform.localPosition = new Vector3(x, y, -0.006f);  // just in front of the box
            vrButtons.Add((box, press));
        }
        var grey = Palette.MenuButton;
        void MainButton(string label, Color color, float y, System.Action press) => Button(vrMain, label, color, 0f, y, 0.22f, press);
        MainButton(GameText.HowTo, grey, 0.21f, () => Show(vrHowTo));
        MainButton(GameText.Continue, grey, 0.15f, CloseMenu);
        MainButton(GameText.Restart, grey, 0.09f, () =>
        {
            CloseMenu();
            if (vrNetGame)
                TowerGame.Instance.Announce(GameText.NoRestartInMulti);
            else
                TowerGame.Instance.Restart(this);
        });
        MainButton(GameText.Practice, grey, 0.03f, () =>
        {
            CloseMenu();
            if (vrNetGame)
            {
                TowerGame.Instance.Announce(GameText.NoPracticeInMulti);
                return;
            }
            TowerSpawner.Practice = !TowerSpawner.Practice;
            TowerGame.Instance.Restart(this);
        });
        MainButton(GameText.Info, grey, -0.03f, () => Show(vrInfo));
        MainButton(GameText.ToStart, grey, -0.09f, () =>
        {
            CloseMenu();
            if (vrNetGame)
            {
                LeaveNet();  // out of the room; the others play on if two are left
                return;
            }
            TowerGame.Instance.Restart(this);
            ShowStart();
        });
        MainButton(GameText.Quit, Palette.MenuQuit, -0.15f, Application.Quit);

        // 게임 방법: every how-to line (over the tower they show only the first 10 s at the table)
        vrHowTo = new GameObject("How To");
        vrHowTo.transform.SetParent(vrMenu.transform, false);
        Box(vrHowTo, "Page", Palette.MenuPage, new Vector3(0f, 0.03f, 0.004f), new Vector3(0.36f, 0.3f, 0.002f));
        var howTo = Scenery.Label(vrHowTo.transform, 0.0016f, Palette.MenuText);
        howTo.anchor = TextAnchor.UpperLeft;
        howTo.alignment = TextAlignment.Left;
        howTo.transform.localPosition = new Vector3(-0.165f, 0.17f, 0f);
        howTo.text = GameText.Rules;
        Button(vrHowTo, GameText.Back, grey, 0f, -0.15f, 0.22f, () => Show(vrMain));

        // 정보: the game and its version, and the way to the licenses
        vrInfo = new GameObject("Info");
        vrInfo.transform.SetParent(vrMenu.transform, false);
        var title = Scenery.Label(vrInfo.transform, 0.005f, Palette.MenuText);
        title.text = Application.productName;
        title.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        var version = Scenery.Label(vrInfo.transform, 0.0025f, Palette.MenuFaint);
        version.text = GameText.Version(Application.version);
        version.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        Button(vrInfo, GameText.Licenses, grey, 0f, -0.03f, 0.22f, () =>
        {
            Show(vrLicenses);
            TurnLicense(-vrLicenseAt);  // from the first page
        });
        Button(vrInfo, GameText.Back, grey, 0f, -0.09f, 0.22f, () =>
        {
            if (vrStart)
                CloseMenu();  // opened from the start menu's 설정
            else
                Show(vrMain);
        });

        // the licenses a page at a time: the notices (Resources/Licenses) and the font's own license
        vrLicenses = new GameObject("Licenses");
        vrLicenses.transform.SetParent(vrMenu.transform, false);
        string all = Resources.Load<TextAsset>(LicensesPath).text + "\n\n" + Resources.Load<TextAsset>(FontLicensePath).text;
        var lines = all.Replace("\r", "").Split('\n');
        vrLicensePages = new string[(lines.Length + LicenseLines - 1) / LicenseLines];
        for (int i = 0; i < vrLicensePages.Length; i++)
            vrLicensePages[i] = string.Join("\n", lines, i * LicenseLines, Mathf.Min(LicenseLines, lines.Length - i * LicenseLines));
        Box(vrLicenses, "Page", Palette.MenuPage, new Vector3(0f, 0.03f, 0.004f), new Vector3(0.36f, 0.24f, 0.002f));
        vrLicenseText = Scenery.Label(vrLicenses.transform, 0.001f, Palette.MenuText);
        vrLicenseText.anchor = TextAnchor.UpperLeft;
        vrLicenseText.alignment = TextAlignment.Left;
        vrLicenseText.transform.localPosition = new Vector3(-0.165f, 0.14f, 0f);
        vrLicensePage = Scenery.Label(vrLicenses.transform, 0.0015f, Palette.MenuFaint);
        vrLicensePage.transform.localPosition = new Vector3(0f, -0.105f, 0f);
        Button(vrLicenses, GameText.PrevPage, grey, -0.12f, -0.15f, 0.1f, () => TurnLicense(-1));
        Button(vrLicenses, GameText.Back, grey, 0f, -0.15f, 0.1f, () => Show(vrInfo));
        Button(vrLicenses, GameText.NextPage, grey, 0.12f, -0.15f, 0.1f, () => TurnLicense(1));
        vrMenu.SetActive(false);
    }

    Transform Box(GameObject panel, string name, Color color, Vector3 at, Vector3 size)
    {
        var box = Scenery.Solid(PrimitiveType.Cube, color, panel.transform);
        box.name = name;
        box.localPosition = at;
        box.localScale = size;
        return box;
    }

    /// One face of the menu at a time
    void Show(GameObject face)
    {
        foreach (var f in new[] { vrMain, vrInfo, vrLicenses, vrHowTo })
            f.SetActive(f == face);
    }

    void TurnLicense(int by)
    {
        vrLicenseAt = Mathf.Clamp(vrLicenseAt + by, 0, vrLicensePages.Length - 1);
        vrLicenseText.text = vrLicensePages[vrLicenseAt];
        vrLicensePage.text = $"{vrLicenseAt + 1} / {vrLicensePages.Length}";
    }

    void OpenMenu()
    {
        // a forearm's length ahead and a little below the eyes, facing the player
        Vector3 head = view.transform.position;
        Vector3 ahead = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        Vector3 at = head + ahead * 0.4f + Vector3.down * 0.12f;
        vrMenu.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - head));
        vrMenu.SetActive(true);
        Show(vrMain);
        vrMenuSince = Time.unscaledTime;  // a single game stops with the menu up
        vrMenuArmed = false;
        ReleaseVR();
        Debug.Log("[BT] VR menu open");
    }

    void CloseMenu() => vrMenu.SetActive(false);

    bool vrMenuArmed;  // no fingertip in any button since the last press: one press per touch

    /// A fingertip in a button presses it (not in the first moment, while the hand that opened the menu leaves)
    void PressMenu()
    {
        if (Time.unscaledTime - vrMenuSince < 0.5f)
            return;
        bool inAny = false;
        foreach (var h in vrHands)
        {
            foreach (var t in h.tips)
            {
                if (!h.tracked || !t.active)
                    continue;
                foreach (var (box, press) in vrButtons)
                {
                    if (!box.gameObject.activeInHierarchy)
                        continue;  // the other face of the menu
                    Vector3 p = box.InverseTransformPoint(t.target);
                    if (Mathf.Abs(p.x) < 0.5f && Mathf.Abs(p.y) < 0.5f && Mathf.Abs(p.z) < 2.5f)  // 25 mm deep either side
                    {
                        inAny = true;
                        if (!vrMenuArmed)
                            continue;
                        vrMenuArmed = false;
                        press();
                        Debug.Log($"[BT] VR menu: {box.name}");
                        return;
                    }
                }
            }
        }
        if (!inAny)
            vrMenuArmed = true;
    }
}
