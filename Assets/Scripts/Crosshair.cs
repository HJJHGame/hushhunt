using UnityEngine;

// 화면 정중앙의 흰색 동그란 크로스헤어. 코드로 그려서 이미지 파일이 필요 없다.
public class Crosshair : MonoBehaviour
{
    public int size = 28;          // 지름(px)
    public float thickness = 2f;   // 링 두께(px)

    Texture2D ring;
    GUIStyle noticeStyle;

    void Awake()
    {
        ring = BuildRing(size, thickness);
    }

    void OnDestroy()
    {
        if (ring != null) Destroy(ring);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MainMenu.IsMenuOpen) return;
        // 메뉴 바깥에서 커서가 풀렸을 때: 크로스헤어 대신 눈에 띄는 안내를 띄운다.
        // 멀티플레이어라 게임은 멈추지 않는다(Time.timeScale 변경 없음).
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            DrawReleasedNotice();
            return;
        }

        float x = Screen.width * 0.5f - size * 0.5f;
        float y = Screen.height * 0.5f - size * 0.5f;
        GUI.DrawTexture(new Rect(x, y, size, size), ring);
    }

    void DrawReleasedNotice()
    {
        // 화면 전체를 살짝 어둡게
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        if (noticeStyle == null)
        {
            noticeStyle = new GUIStyle(GUI.skin.label);
            noticeStyle.alignment = TextAnchor.MiddleCenter;
            noticeStyle.fontStyle = FontStyle.Bold;
            noticeStyle.normal.textColor = Color.white;
        }
        noticeStyle.fontSize = Mathf.Max(18, Screen.height / 22);
        GUI.Label(new Rect(0, Screen.height * 0.5f - 60f, Screen.width, 60f),
            "ESC - 커서 해제됨", noticeStyle);
        noticeStyle.fontSize = Mathf.Max(14, Screen.height / 36);
        GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40f),
            "화면을 클릭하면 다시 조작 (게임은 계속 진행 중)", noticeStyle);
    }

    static Texture2D BuildRing(int d, float t)
    {
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (d - 1) * 0.5f;
        float outer = d * 0.5f - 0.5f;
        float inner = outer - t;
        for (int py = 0; py < d; py++)
        {
            for (int px = 0; px < d; px++)
            {
                float r = Mathf.Sqrt((px - c) * (px - c) + (py - c) * (py - c));
                // 바깥/안쪽 가장자리를 1px 부드럽게
                float a = Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - inner + 0.5f);
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
