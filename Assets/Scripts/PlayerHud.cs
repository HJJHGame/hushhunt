using UnityEngine;

// 플레이어 HUD: 스태미나 바(지칠 때/회복 중에만 표시) + 손전등 상태 안내.
public class PlayerHud : MonoBehaviour
{
    FirstPersonController fpc;
    Flashlight flash;
    GUIStyle hintStyle;
    float visibility;
    MainMenu menu;

    // 메뉴는 GameSystems가 씬에 하나만 붙인다. 찾으면 캐시한다 (issue #28).
    bool MenuOpen()
    {
        if (menu == null) menu = FindFirstObjectByType<MainMenu>();
        return menu != null && menu.IsOpen;
    }


    void Start()
    {
        fpc = FindFirstObjectByType<FirstPersonController>();
        flash = FindFirstObjectByType<Flashlight>();
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MenuOpen()) return;
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

        GUI.color = prev;
    }
}
