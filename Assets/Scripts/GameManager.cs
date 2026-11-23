using UnityEngine;
using UnityEngine.SceneManagement;

// 한 판의 흐름: 숨는 시간(1분) -> 수색 시간(폭탄 타이머, 5분) -> 종료.
// 규칙상 타이머가 끝나면 잡는 팀 승리. 캐릭터/무기/네트워크는 아직 없으므로
// 이 단계에서는 단계 전환, 화면 표시, 종료 후 R 키 재시작만 담당한다.
public class GameManager : MonoBehaviour
{
    public enum Phase { Hiding, Seeking, Ended }

    [Header("단계별 시간 (초)")]
    public float hideDuration = 60f;  // 숨는 팀이 숨는 시간
    public float seekDuration = 300f; // 수색 시간. 다 지나면 잡는 팀 승리

    [Header("연출")]
    public float bannerSeconds = 3f; // "수색 시작" 안내가 떠 있는 시간

    public Phase CurrentPhase { get; private set; }
    public float TimeLeft { get; private set; }

    float bannerUntil = -1f;

    void Start()
    {
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

        switch (CurrentPhase)
        {
            case Phase.Hiding:
                DrawCenter("숨는 시간 " + FormatTime(TimeLeft), "숨을 곳을 찾으세요");
                break;
            case Phase.Seeking:
                DrawTimer(FormatTime(TimeLeft));
                if (Time.unscaledTime < bannerUntil)
                    DrawCenter("수색 시작", "잡는 팀이 움직입니다");
                break;
            case Phase.Ended:
                DrawCenter("시간 종료 - 잡는 팀 승리", "R 키를 눌러 다시 시작");
                break;
        }
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // 수색 중 화면 위쪽 중앙의 남은 시간
    static void DrawTimer(string text)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        DrawShadowed(new Rect(0f, 12f, Screen.width, 40f), text, style);
    }

    // 화면 중앙의 큰 안내 (제목 + 부제)
    static void DrawCenter(string title, string subtitle)
    {
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold
        };
        var subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18
        };

        DrawShadowed(new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 50f), title, titleStyle);
        DrawShadowed(new Rect(0f, Screen.height * 0.5f - 10f, Screen.width, 30f), subtitle, subStyle);
    }

    // 검은 그림자를 한 번 깔고 흰 글씨를 올려 어두운 배경에서도 읽히게 한다.
    static void DrawShadowed(Rect rect, string text, GUIStyle style)
    {
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
        GUI.color = prev;
    }
}
