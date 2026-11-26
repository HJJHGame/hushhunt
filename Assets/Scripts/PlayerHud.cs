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
    Health health;
    GunHolder gun;
    Armory armory;
    GUIStyle hpStyle;
    GUIStyle gunStyle;
    float flashUntil = -1f; // 피격 시 화면 붉은 번쩍임이 끝나는 시각
    const float FlashSeconds = 0.4f;

    void Start()
    {
        fpc = FindFirstObjectByType<FirstPersonController>();
        flash = FindFirstObjectByType<Flashlight>();
        if (fpc != null) { health = fpc.GetComponent<Health>(); gun = fpc.GetComponent<GunHolder>(); }
        armory = FindFirstObjectByType<Armory>();
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void OnDamaged(Health h, float amount, GameObject source)
    {
        flashUntil = Time.unscaledTime + FlashSeconds;
    }

    void Update()
    {
        // F 키처럼 일반 문자 키를 쓴다: F1은 원격 데스크톱/브라우저/유니티 도움말이 가로챌 수 있다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.K))
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
                hintStyle.fontStyle = FontStyle.Bold;
            }
            hintStyle.fontSize = Mathf.RoundToInt(19f * scale);
            string text = "[F] 손전등 " + (flash.IsOn ? "켜짐" : "꺼짐");
            HudText.Label(new Rect(Screen.width - 300f * scale - 16f, Screen.height - 44f * scale, 300f * scale, 32f * scale), text, hintStyle);
        }

        // F1: 현재 좌표와 가장자리까지 거리. 맵이 진짜 400x400m인지 직접 확인할 수 있다.
        if (showCoords && fpc != null)
        {
            if (coordStyle == null)
                coordStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            coordStyle.fontStyle = FontStyle.Bold;
            coordStyle.fontSize = Mathf.RoundToInt(19f * scale);
            var p = fpc.transform.position;
            float edge = WorldConfig.Half - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            string text = "위치 (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0")
                + ")  |  가장자리(±200m)까지 " + edge.ToString("0") + "m  |  맵 400x400m  [K] 끄기";
            HudText.Label(new Rect(16f, 12f, 700f * scale, 32f * scale), text, coordStyle);
        }

        DrawHealth(scale);
        DrawGun(scale);

        GUI.color = prev;
    }

    // 오른쪽 아래(손전등 안내 위): 잡는 팀의 총/탄 또는 총 없음 안내. 첫 종이 발견 후엔 자동 지급 카운트다운.
    void DrawGun(float scale)
    {
        if (gun == null || !gun.IsSeeker) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        if (gunStyle == null) gunStyle = new GUIStyle(hpStyle) { alignment = TextAnchor.LowerRight };
        gunStyle.fontSize = Mathf.RoundToInt(19f * scale);
        string text;
        if (gun.HasGun) text = "총 " + gun.Ammo + "/" + GunHolder.MagazineRounds;
        else
        {
            text = "총 없음: 종이를 찾으세요";
            float left = armory != null ? armory.AutoGrantLeft : -1f;
            if (left > 0f) text += " (자동 지급 " + Mathf.CeilToInt(left) + "초)";
        }
        var r = new Rect(Screen.width - 420f * scale - 16f, Screen.height - 80f * scale, 420f * scale, 32f * scale);
        HudText.Label(r, text, gunStyle);
    }

    // 왼쪽 아래 체력 막대 + 피격 시 붉은 번쩍임 + 사망 안내
    void DrawHealth(float scale)
    {
        if (health == null) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        hpStyle.fontSize = Mathf.RoundToInt(19f * scale);

        if (Time.unscaledTime < flashUntil)
        {
            float a = 0.35f * (flashUntil - Time.unscaledTime) / FlashSeconds;
            GUI.color = new Color(0.8f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        float w = 220f * scale, h = 12f * scale;
        float x = 16f * scale, y = Screen.height - 36f * scale;
        HudText.Label(new Rect(x, y - 34f * scale, w, 30f * scale), "체력 " + Mathf.CeilToInt(health.Hp), hpStyle);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Texture2D.whiteTexture);
        GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.75f, 0.85f, 0.7f), health.Hp01);
        GUI.DrawTexture(new Rect(x, y, w * health.Hp01, h), Texture2D.whiteTexture);

        if (health.IsDead)
        {
            var big = new GUIStyle(hpStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44f * scale) };
            HudText.Label(new Rect(0f, Screen.height * 0.5f - 30f * scale, Screen.width, 60f * scale), "사망", big, new Color(0.9f, 0.2f, 0.15f));
        }
    }
}
