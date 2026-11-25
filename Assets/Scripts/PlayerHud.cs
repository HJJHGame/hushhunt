using UnityEngine;

// 플레이어 HUD: 스태미나 바(지칠 때/회복 중에만 표시) + 손전등 상태 안내.
public class PlayerHud : MonoBehaviour
{
    FirstPersonController fpc;
    Flashlight flash;
    GUIStyle hintStyle;
    GUIStyle coordStyle;
    float visibility;
    bool showCoords;

    void Start()
    {
        fpc = FindFirstObjectByType<FirstPersonController>();
        flash = FindFirstObjectByType<Flashlight>();
    }

    void Update()
    {
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.F1))
            showCoords = !showCoords;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MainMenu.IsMenuOpen) return;
        GUI.depth = 0;
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f);
        Color prev = GUI.color;

        if (fpc != null && fpc.useStamina)
        {
            float target = fpc.Stamina01 < 0.999f ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * 2f);
            if (visibility > 0.01f)
            {
                float w = 220f * scale, h = 8f * scale;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - 60f * scale;

                GUI.color = new Color(0f, 0f, 0f, 0.55f * visibility);
                GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Texture2D.whiteTexture);

                Color fill = fpc.IsExhausted
                    ? new Color(0.85f, 0.2f, 0.15f)
                    : Color.Lerp(new Color(0.9f, 0.6f, 0.2f), new Color(0.6f, 0.9f, 0.4f), fpc.Stamina01);
                fill.a = 0.9f * visibility;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(x, y, w * fpc.Stamina01, h), Texture2D.whiteTexture);
            }
        }

        if (flash != null)
        {
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight };
            }
            hintStyle.fontSize = Mathf.RoundToInt(15f * scale);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            string text = "[F] 손전등 " + (flash.IsOn ? "켜짐" : "꺼짐");
            GUI.Label(new Rect(Screen.width - 260f * scale - 16f, Screen.height - 40f * scale, 260f * scale, 28f * scale), text, hintStyle);
        }

        // F1: 현재 좌표와 가장자리까지 거리. 맵이 진짜 400x400m인지 직접 확인할 수 있다.
        if (showCoords && fpc != null)
        {
            if (coordStyle == null)
                coordStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            coordStyle.fontSize = Mathf.RoundToInt(15f * scale);
            var p = fpc.transform.position;
            float edge = WorldConfig.Half - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            string text = "위치 (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0")
                + ")  |  가장자리(±200m)까지 " + edge.ToString("0") + "m  |  맵 400x400m  [F1] 끄기";
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(16f, 12f, 640f * scale, 28f * scale), text, coordStyle);
        }

        GUI.color = prev;
    }
}
