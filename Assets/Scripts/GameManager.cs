using UnityEngine;
using UnityEngine.SceneManagement;

// 한 판의 흐름: 타이틀 대기 -> 숨는 시간(1분) -> 수색 시간(폭탄 타이머, 5분) -> 종료.
// 규칙상 타이머가 끝나면 잡는 팀 승리. 캐릭터/무기/네트워크는 아직 없으므로
// 이 단계에서는 단계 전환, 화면 표시, 종료 후 R 키 재시작만 담당한다.
// Waiting 단계는 MainMenu 타이틀 화면과 짝이다. 타이머는 돌지 않는다.
public class GameManager : MonoBehaviour
{
    public enum Phase { Waiting, Hiding, Seeking, Ended }

    [Header("단계별 시간 (초)")]
    public float hideDuration = 60f;  // 숨는 팀이 숨는 시간
    public float seekDuration = 300f; // 수색 시간. 다 지나면 잡는 팀 승리

    [Header("연출")]
    public float bannerSeconds = 3f;  // "수색 시작" 안내가 떠 있는 시간
    public float urgentSeconds = 10f; // 남은 시간이 이 이하면 빨갛게 맥동

    public Phase CurrentPhase { get; private set; }
    public float TimeLeft { get; private set; }

    // MainMenu가 열려 있으면 타이머를 멈춘다.
    public bool paused;

    float bannerUntil = -1f;

    // OnGUI마다 GUIStyle을 새로 만들면 GC가 계속 쌓이므로 처음 한 번만 만든다.
    static GUIStyle timerStyle, titleStyle, subStyle;

    void Start()
    {
        // 타이틀 화면에서 '연습 시작'을 누르면 BeginRound로 실제 판이 시작된다.
        CurrentPhase = Phase.Waiting;
        TimeLeft = hideDuration;
    }

    // 타이틀 메뉴에서 호출: 숨는 시간부터 판을 시작한다.
    public void BeginRound()
    {
        if (CurrentPhase != Phase.Waiting) return;
        CurrentPhase = Phase.Hiding;
        TimeLeft = hideDuration;
    }

    void Update()
    {
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
            CurrentPhase = Phase.Seeking;
            TimeLeft = seekDuration;
            bannerUntil = Time.unscaledTime + bannerSeconds;
        }
        else
        {
            // 폭탄 타이머 종료 = 잡는 팀 승리
            CurrentPhase = Phase.Ended;
            TimeLeft = 0f;
        }
    }

    void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MainMenu.IsOpen || CurrentPhase == Phase.Waiting) return;
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
                DrawCenter("숨는 시간 " + FormatTime(TimeLeft), "숨을 곳을 찾으세요", timeColor);
                break;
            case Phase.Seeking:
                DrawTimer(FormatTime(TimeLeft), timeColor);
                DrawProgress(TimeLeft / Mathf.Max(1f, seekDuration), urgent);
                if (Time.unscaledTime < bannerUntil)
                    DrawCenter("수색 시작", "잡는 팀이 움직입니다", Color.white);
                break;
            case Phase.Ended:
                DrawEndOverlay();
                DrawCenter("시간 종료 - 잡는 팀 승리", "R 키를 눌러 다시 시작", Color.white);
                break;
        }

    }

    void EnsureStyles()
    {
        if (timerStyle != null) return;
        timerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
        subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // 수색 중 화면 위쪽 중앙의 남은 시간
    static void DrawTimer(string text, Color color)
    {
        DrawShadowed(new Rect(0f, 12f, Screen.width, 40f), text, timerStyle, color);
    }

    // 타이머 아래 가느다란 남은 시간 막대
    static void DrawProgress(float fraction, bool urgent)
    {
        float w = 260f;
        float x = (Screen.width - w) * 0.5f;
        float y = 56f;
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, 8f), Texture2D.whiteTexture);
        GUI.color = urgent ? new Color(0.9f, 0.2f, 0.15f, 0.95f) : new Color(0.85f, 0.85f, 0.8f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fraction), 4f), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 화면 위쪽 1/4 지점의 큰 안내 (제목 + 부제). 정중앙 크로스헤어와 겹치지 않게 위로 올렸다.
    static void DrawCenter(string title, string subtitle, Color titleColor)
    {
        DrawShadowed(new Rect(0f, Screen.height * 0.25f - 60f, Screen.width, 50f), title, titleStyle, titleColor);
        DrawShadowed(new Rect(0f, Screen.height * 0.25f - 10f, Screen.width, 30f), subtitle, subStyle, Color.white);
    }

    // 종료 후 화면을 어둡게
    static void DrawEndOverlay()
    {
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 검은 그림자를 한 번 깔고 글씨를 올려 어두운 배경에서도 읽히게 한다.
    static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f * color.a);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = prev;
    }
}
