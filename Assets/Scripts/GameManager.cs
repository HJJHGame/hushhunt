using UnityEngine;

// 한 판의 흐름: 타이틀 대기 -> 숨는 시간(60초) -> 수색 시간(9분) -> 종료. 제한시간은 숨는 시간을 포함해 총 10분.
// 확정 규칙: 시간이 다 되면 숨는 팀 승리(단, 술래 전멸 후 탈출구가 열렸는데 전원 탈출 못 하면 숨는 팀 패배).
// 잡는 팀이 숨는 팀을 전멸시키면 잡는 팀 승리, 살아 있는 숨는 팀 전원이 탈출구로 나가면 숨는 팀 승리.
// 전멸/탈출은 아직 없다. 나중 단계(종이->총, 체력, 탈출구)가 EndRound(결과)를 부르면 된다.
// 캐릭터/무기/네트워크는 아직 없으므로 이 단계에서는 단계 전환, 화면 표시, 종료 후 R 키 재시작만 담당한다.
// Waiting 단계는 MainMenu 타이틀 화면과 짝이다. 타이머는 돌지 않는다.
public class GameManager : MonoBehaviour
{
    public enum Phase { Waiting, Hiding, Seeking, Ended }
    public enum Outcome { None, HidersWin, SeekersWin, HidersEscaped } // 판 결과 (Ended일 때 의미 있음)
    public enum Role { Hider, Seeker }

    // 확정 규칙 값. 씬에 저장된 값이 아니라 항상 이 상수를 쓴다 (Start에서 덮어씀).
    public const float HideSeconds = 60f;
    public const float MatchSeconds = 600f; // 한 판 총 제한시간 (숨는 시간 60초 포함). 수색 시간은 540초(9분)

    // 로컬 플레이어의 팀. 혼자 테스트에서는 GameSystems가 시작할 때 정하고 G로 바뀐다. (잡는 팀 시점은 숨는 시간 동안 검은 화면 + 타이머)
    public static Role LocalRole = Role.Hider;

    // 단계/결과가 바뀔 때 알린다. 종이/총/체력/탈출구 같은 다음 단계들이 여기에 붙는다.
    public event System.Action<Phase> PhaseChanged;
    public event System.Action<Outcome> RoundEnded;

    [Header("단계별 시간 (초)")]
    public float hideDuration = 60f;  // 숨는 팀이 숨는 시간
    public float seekDuration = 300f; // 수색 시간. 다 지나면 잡는 팀 승리

    [Header("연출")]
    public float bannerSeconds = 3f;  // "수색 시작" 안내가 떠 있는 시간
    public float urgentSeconds = 10f; // 남은 시간이 이 이하면 빨갛게 맥동

    public Phase CurrentPhase { get; private set; }
    public Outcome Result { get; private set; }
    public float TimeLeft { get; private set; }

    // MainMenu가 열려 있으면 타이머를 멈춘다.
    public bool paused;

    float bannerUntil = -1f;

    // OnGUI마다 GUIStyle을 새로 만들면 GC가 계속 쌓이므로 처음 한 번만 만든다.
    static GUIStyle timerStyle, titleStyle, subStyle;

    void Start()
    {
        // 타이틀 화면에서 '연습 시작'을 누르면 BeginRound로 실제 판이 시작된다.
        hideDuration = HideSeconds;
        seekDuration = MatchSeconds - HideSeconds;
        Result = Outcome.None;
        CurrentPhase = Phase.Waiting;
        TimeLeft = hideDuration;
    }

    void SetPhase(Phase p)
    {
        CurrentPhase = p;
        if (PhaseChanged != null) PhaseChanged(p);
    }

    // 판을 끝낸다. 시간 종료(숨는 팀 승리), 전멸(잡는 팀 승리), 탈출(숨는 팀 승리)이 부른다.
    public void EndRound(Outcome result)
    {
        if (CurrentPhase == Phase.Ended || CurrentPhase == Phase.Waiting) return;
        Result = result;
        TimeLeft = 0f;
        SetPhase(Phase.Ended);
        if (RoundEnded != null) RoundEnded(result);
    }

    // 타이틀 메뉴에서 호출: 숨는 시간부터 판을 시작한다.
    public void BeginRound()
    {
        if (CurrentPhase != Phase.Waiting) return;
        TimeLeft = hideDuration;
        SetPhase(Phase.Hiding);
    }

    void Update()
    {
        // 테스트용: R은 판 도중에도 바로 재시작한다 (타이틀 화면이 열려 있을 땐 무시).
        if (CurrentPhase != Phase.Waiting && !MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.R))
        {
            Restart();
            return;
        }
        if (CurrentPhase == Phase.Ended)
        {
            if (Input.GetKeyDown(KeyCode.R))
                Restart();
            return;
        }
        if (paused || CurrentPhase == Phase.Waiting) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft > 0f) return;

        if (CurrentPhase == Phase.Hiding)
        {
            TimeLeft = seekDuration;
            bannerUntil = Time.unscaledTime + bannerSeconds;
            SetPhase(Phase.Seeking);
        }
        else
        {
            // 제한시간 종료: 술래가 살아 있으면 숨는 팀 승리.
            // 술래가 전멸해 탈출구가 열렸는데 전원 탈출 못 했으면 숨는 팀 패배.
            var gate = FindFirstObjectByType<EscapeGate>();
            EndRound(gate != null && gate.IsOpen ? Outcome.SeekersWin : Outcome.HidersWin);
        }
    }

    void Restart()
    {
        SceneReloader.ReloadActive();
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MainMenu.IsMenuOpen || CurrentPhase == Phase.Waiting) return;
        EnsureStyles();

        bool urgent = CurrentPhase != Phase.Ended && TimeLeft <= urgentSeconds;
        Color timeColor = Color.white;
        if (urgent)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            timeColor = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), pulse);
        }

        switch (CurrentPhase)
        {
            case Phase.Hiding:
                if (LocalRole == Role.Seeker)
                {
                    // 잡는 팀은 숨는 시간 동안 맵 밖: 검은 화면과 타이머만 본다.
                    DrawEndOverlayAlpha(1f);
                    DrawCenter("숨는 시간 " + FormatTime(TimeLeft), "잠시 기다리세요", timeColor);
                }
                else
                    DrawCenter("숨는 시간 " + FormatTime(TimeLeft), "숨을 곳을 찾으세요", timeColor);
                break;
            case Phase.Seeking:
                DrawTimer(FormatTime(TimeLeft), timeColor);
                DrawProgress(TimeLeft / Mathf.Max(1f, seekDuration), urgent);
                if (Time.unscaledTime < bannerUntil)
                    DrawCenter("수색 시작", "남은 시간 " + Mathf.RoundToInt(seekDuration / 60f) + "분", Color.white);
                break;
            case Phase.Ended:
                DrawEndOverlay();
                DrawCenter(EndTitle(), "R 키를 눌러 다시 시작", Color.white);
                break;
        }

    }

    string EndTitle()
    {
        switch (Result)
        {
            case Outcome.SeekersWin: return "잡는 팀 승리";
            case Outcome.HidersEscaped: return "탈출 성공 - 숨는 팀 승리";
            default: return "시간 종료 - 숨는 팀 승리";
        }
    }

    void EnsureStyles()
    {
        if (timerStyle != null) return;
        timerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 40, fontStyle = FontStyle.Bold };
        subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 23, fontStyle = FontStyle.Bold };
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // 수색 중 화면 위쪽 중앙의 남은 시간
    static void DrawTimer(string text, Color color)
    {
        UiTheme.Apply(timerStyle);
        float tw = timerStyle.CalcSize(new GUIContent(text)).x + 56f;
        UiTheme.Frame(new Rect((Screen.width - tw) * 0.5f, 8f, tw, 48f), 0.62f, 1f, 7f); // 숫자 뒤 틀(모서리 장식)
        DrawShadowed(new Rect(0f, 12f, Screen.width, 40f), text, timerStyle, color);
    }

    // 타이머 아래 가느다란 남은 시간 막대
    static void DrawProgress(float fraction, bool urgent)
    {
        float w = 260f;
        float x = (Screen.width - w) * 0.5f;
        float y = 56f;
        UiTheme.SegBar(new Rect(x, y + 8f, w, 4f), fraction, urgent ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.85f, 0.85f, 0.8f), 12);
    }

    // 화면 위쪽 1/4 지점의 큰 안내 (제목 + 부제). 정중앙 크로스헤어와 겹치지 않게 위로 올렸다.
    static void DrawCenter(string title, string subtitle, Color titleColor)
    {
        UiTheme.Banner(new Rect(0f, Screen.height * 0.25f - 72f, Screen.width, 104f));
        DrawShadowed(new Rect(0f, Screen.height * 0.25f - 60f, Screen.width, 50f), title, titleStyle, titleColor);
        DrawShadowed(new Rect(0f, Screen.height * 0.25f - 10f, Screen.width, 30f), subtitle, subStyle, Color.white);
    }

    // 종료 후 화면을 어둡게
    static void DrawEndOverlay()
    {
        DrawEndOverlayAlpha(0.55f);
    }

    static void DrawEndOverlayAlpha(float alpha)
    {
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 진한 외곽선 + 불투명 글씨 (HudText)로 어두운 배경에서도 읽히게 한다.
    static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
    {
        HudText.Label(rect, text, style, color);
    }
}
