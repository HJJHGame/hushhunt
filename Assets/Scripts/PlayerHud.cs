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

    GameManager gmRef;
    // 숨는 시간 중의 술래: 위치를 알려 주는 표시를 모두 가린다.
    bool SeekerHiding()
    {
        if (gmRef == null) gmRef = FindFirstObjectByType<GameManager>();
        return gmRef != null && gmRef.CurrentPhase == GameManager.Phase.Hiding && GameManager.LocalRole == GameManager.Role.Seeker;
    }

    void Update()
    {
        // X 키처럼 일반 문자 키를 쓴다: F1은 원격 데스크톱/브라우저/유니티 도움말이 가로챌 수 있다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.X))
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
                // 왼쪽 아래, 체력 막대 바로 아래에 같은 너비/같은 x로 세로로 쌓는다.
                float w = 220f * scale, h = 8f * scale;
                float x = 16f * scale;
                float y = Screen.height - 36f * scale;

                Color fill = fpc.IsExhausted
                    ? new Color(0.85f, 0.2f, 0.15f)
                    : Color.Lerp(new Color(0.9f, 0.6f, 0.2f), new Color(0.6f, 0.9f, 0.4f), fpc.Stamina01);
                // 체력과 같은 배치: 왼쪽 아이콘 + 오른쪽 눈금 막대
                float ic = 18f * scale;
                var icon = new Rect(x, y + h * 0.5f - ic * 0.5f, ic, ic);
                if (fpc.IsExhausted) fill = Color.Lerp(fill, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)));
                UiTheme.IconChevrons(icon, new Color(fill.r, fill.g, fill.b, 0.95f * visibility));
                UiTheme.SegBar(new Rect(x + ic + 10f * scale, y, w - ic - 10f * scale, h), fpc.Stamina01, fill, 10, visibility);
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
            string text = "[T] 손전등 " + (flash.IsOn ? "켜짐" : "꺼짐");
            HudText.Label(new Rect(Screen.width - 300f * scale - 16f, Screen.height - 44f * scale, 300f * scale, 32f * scale), text, hintStyle);
        }

        // X: 현재 좌표와 가장자리까지 거리. 맵 크기(WorldConfig.Half)를 직접 확인할 수 있다.
        if (showCoords && fpc != null && !SeekerHiding())
        {
            if (coordStyle == null)
                coordStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            coordStyle.fontStyle = FontStyle.Bold;
            coordStyle.fontSize = Mathf.RoundToInt(19f * scale);
            var p = fpc.transform.position;
            float edge = WorldConfig.Half - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            string text = "위치 (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0")
                + ")  |  가장자리(±" + WorldConfig.Half + "m)까지 " + edge.ToString("0") + "m  |  맵 " + (WorldConfig.Half * 2f) + "m 정사각  [X] 끄기";
            HudText.Label(new Rect(16f, 12f, 700f * scale, 32f * scale), text, coordStyle);
        }

        DrawHealth(scale);
        DrawGun(scale);

        GUI.color = prev;
    }

    // 오른쪽 아래(손전등 안내 위): 잡는 팀의 총/탄 또는 총 없음 안내. 첫 종이 발견 후엔 자동 지급 카운트다운.
    void DrawGun(float scale)
    {
        if (gun == null || (!gun.IsSeeker && !gun.HasGun)) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        if (gunStyle == null) gunStyle = new GUIStyle(hpStyle) { alignment = TextAnchor.LowerRight };
        gunStyle.fontSize = Mathf.RoundToInt(19f * scale);
        string text;
        if (gun.HasGun && gun.Weapon == WeaponKind.Knife) text = "칼";
        else if (gun.HasGun) text = GunHolder.WeaponName(gun.Weapon) + " " + gun.Ammo + "/" + gun.MaxAmmo;
        else
        {
            text = "총 없음: 종이를 찾으세요";
            float left = armory != null ? armory.AutoGrantLeft : -1f;
            if (left > 0f) text += " (자동 지급 " + Mathf.CeilToInt(left) + "초)";
        }
        var r = new Rect(Screen.width - 420f * scale - 16f, Screen.height - 80f * scale, 420f * scale, 32f * scale);
        // 글씨 뒤에 어두운 패널: 글씨 길이에 맞춰 오른쪽 정렬
        UiTheme.Apply(gunStyle);
        float tw = Mathf.Min(r.width, gunStyle.CalcSize(new GUIContent(text)).x + 20f * scale);
        UiTheme.Panel(new Rect(r.xMax - tw - 6f * scale, r.y - 1f, tw + 12f * scale, r.height + 2f), 0.5f, 0.4f);
        HudText.Label(r, text, gunStyle);
    }

    // 왼쪽 아래 체력 막대 + 피격 시 붉은 번쩍임 + 사망 안내
    void DrawHealth(float scale)
    {
        if (health == null) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        hpStyle.fontSize = Mathf.RoundToInt(19f * scale);

        // 피격: 가장자리가 붉게 번졌다 사라지는 비네트. 체력이 30% 아래면 심장 뛰듯 약하게 계속 깜빡인다.
        float vig = 0f;
        if (Time.unscaledTime < flashUntil) vig = 0.9f * (flashUntil - Time.unscaledTime) / FlashSeconds;
        if (!health.IsDead && health.Hp01 < 0.3f)
            vig = Mathf.Max(vig, (0.3f - health.Hp01) / 0.3f * (0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f)) + 0.15f);
        if (vig > 0.01f) UiTheme.DrawVignette(new Color(0.75f, 0.02f, 0.02f, Mathf.Clamp01(vig)));

        float w = 220f * scale, h = 12f * scale;
        float x = 16f * scale, y = Screen.height - 62f * scale; // 스태미나 막대가 바로 아래(Screen.height - 36)에 온다
        HudText.Label(new Rect(x, y - 34f * scale, w, 30f * scale), "체력 " + Mathf.CeilToInt(health.Hp), hpStyle);
        Color hc = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.75f, 0.85f, 0.7f), health.Hp01);
        float ic = 18f * scale;
        UiTheme.IconCross(new Rect(x, y + h * 0.5f - ic * 0.5f, ic, ic), hc);
        UiTheme.SegBar(new Rect(x + ic + 10f * scale, y, w - ic - 10f * scale, h), health.Hp01, hc, 10);

        if (health.IsDead)
        {
            var big = new GUIStyle(hpStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44f * scale) };
            HudText.Label(new Rect(0f, Screen.height * 0.5f - 30f * scale, Screen.width, 60f * scale), "사망", big, new Color(0.9f, 0.2f, 0.15f));
        }
    }
}
