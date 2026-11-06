/// Every line the player reads, in one place for the language setting to come. "[key]" is drawn as a keycap
public static class GameText
{
    // who
    public const string Me = "나";
    public static string Player(int seat) => $"플레이어 {seat + 1}";
    public static string Turn(string who) => $"{who} 차례";
    public static string Left(string who) => $"{who}님이 나갔습니다";
    public static string Took(string who) => $"{who}  블록 +1";
    public static string OutOfTime(string who) => $"{who}  시간 초과";
    public static string Restarted(string who) => $"{who}님이 재시작했습니다";
    public const string NewRound = "새 판 시작";

    // the rules
    public const string FinishFirst = "처음 건드린 블록을 끝까지 빼야 합니다";
    public const string NotTheTop = "맨 윗층은 뺄 수 없습니다";
    public const string Tight = "꽉 끼어 있어요";
    public const string Slipped = "미끄러졌습니다! 블록이 꽉 끼어 있어요";

    // the fall
    public const string Fell = "탑이 무너졌습니다!";
    public const string IToppled = "내가 무너뜨렸어요";
    public static string Toppled(string who) => $"{who}님이 무너뜨렸어요";
    public static string NewRoundIn(int seconds) => $"{seconds}초 뒤 새 판";
    public const string Lost = "패배";
    public const string MeMark = " (나)";

    // the mouse player's prompts
    public const string Sit = "[클릭] 앉기";
    public const string CloserToChair = "의자에 더 가까이 가세요";
    public const string AimShortEnd = "블록의 짧은 면을 조준하세요";
    public const string ToOwnSeat = "내 자리로";
    public const string ToFarSide = "반대편에서 보기";
    public const string StandUp = "[E] 일어서기";
    public const string ClickToWork = "[클릭 + 위로] 밀기   [클릭 + 아래로] 당기기";
    public const string DragToWork = "[위로] 밀기   [아래로] 당기기";
    public const string Push = "[위로] 밀기";
    public const string Pull = "[아래로] 당기기";
    public const string LetGoGoRound = "[놓기] 반대편으로 돌아가서 빼기";
    public const string LetGoTake = "[놓기] 내 더미로 가져가기";
    public static string PercentOut(float share) => $"{share * 100f:F0}% 빠짐";
    public static string Aim(bool farSide) => $"{AimShortEnd}    [Q] {(farSide ? ToOwnSeat : ToFarSide)}   {StandUp}";

    // over the tower in a headset
    public const string TowerStops = "탑이 멈추면 다음 사람 차례입니다";
    public const string HowToTake = "아무 손가락으로나 밀거나\n엄지와 다른 손가락으로 집어서 빼세요";
    public const string HowToSetUp = "시점 맞추기: 두 손바닥을 책상에 2초\n메뉴: 왼손바닥을 보며 엄지·검지 집기";
    public const string Practising = "연습 중";
    public const string DeskSet = "시점과 식탁 높이를 다시 맞췄습니다";
    public const string SwingThere = "반대편으로";
    public static string BlocksTaken(int n) => $"가져간 블록  {n}";
    public static string MyTurn(int seconds) => $"내 차례  {seconds}초";
    public static string TurnWithTime(string who, int seconds) => $"{Turn(who)}  {seconds}초";

    // the game menu
    public const string HowTo = "게임 방법";
    public const string Continue = "계속하기";
    public const string Restart = "재시작";
    public const string Practice = "연습(블록 1개) / 탑 바꾸기";
    public const string Info = "정보";
    public const string ToStart = "처음 메뉴로";
    public const string Quit = "종료";
    public const string Back = "돌아가기";
    public const string Licenses = "오픈소스 라이선스";
    public const string NoRestartInMulti = "멀티에서는 재시작할 수 없습니다";
    public const string NoPracticeInMulti = "멀티에서는 연습할 수 없습니다";
    public const string Rules = "블록 빼기\n"
        + "· 아무 손가락으로나 블록을 밀어서 빼세요\n"
        + "· 엄지와 다른 손가락으로 집어서 당겨 빼도 됩니다\n"
        + "· 처음 건드린 블록(빨간색)을 끝까지 빼야 합니다\n"
        + "· 맨 윗층은 뺄 수 없습니다\n"
        + "· 뺀 블록을 내려놓으면 내 자리 더미로 갑니다\n"
        + "· 탑을 무너뜨리면 집니다\n\n"
        + "시점·책상 다시 맞추기\n"
        + "· 두 손바닥을 책상에 펴서 2초 올려 두면\n"
        + "  시점과 식탁 높이를 맞춥니다 (다음에도 기억)\n\n"
        + "메뉴\n"
        + "· 왼손바닥을 보며 엄지·검지를 집으면 열립니다";
    public const string PrevPage = "이전";
    public const string NextPage = "다음";
    public static string Version(string version) => $"버전 {version}";

    // the start menu
    public const string Title = "BLOCK TOWER";
    public const string Single = "싱글";
    public const string Multi = "멀티";
    public const string Settings = "설정";
    public const string Friends = "친구랑";
    public const string Random = "랜덤";
    public const string BackShort = "뒤로";
    public const string CreateRoom = "방 만들기";
    public const string EnterCode = "코드 입력";
    public const string Erase = "지우기";
    public const string Start = "시작";
    public const string LeaveRoom = "나가기";
    public const string License = "라이선스";
    public const string NoServer = "서버에 연결할 수 없습니다\n사무실 네트워크에 연결됐는지 확인하세요";
    public const string Searching = "상대를 찾는 중";
    public const string NeedTwo = "2명 이상이어야 시작할 수 있습니다";
    public const string TypeCode = "방 코드 4자리를 누르세요";
    public static string Code(string typed) => $"방 코드: {typed}";
    public static string Joining(string code) => $"{Code(code)}\n들어가는 중";
    public static string RoomCode(string code) => $"방 코드  {code}";
    public const string Host = " (방장)";
    public const string You = " · 나";
    public const string HostStarts = "모두 모이면 '시작'을 누르세요";
    public const string TellCode = "친구에게 코드를 알려 주세요";
    public const string WaitForHost = "방장이 시작하기를 기다리는 중";
    public static string SearchingCount(int count, int seats) => $"{Searching}  {count}/{seats}";
    public static string StartsIn(int seconds) => $"{seconds}초 뒤 시작";
    public static string MultiOnline(int online) => $"{Multi} · {online}명";
    public const string MultiNoServer = Multi + " (서버 없음)";

    // leaving a game
    public const string ServerGone = "서버와 연결이 끊겼습니다";
    public const string HeadsetOff = "헤드셋을 벗어서 게임에서 나갔습니다";
}
