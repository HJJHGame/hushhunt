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
    GUIStyle gunStyle, gunNameStyle, gunCntStyle, gunMidStyle; // 매 프레임 새로 만들지 않게 캐시
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
                float w = 260f * scale, h = 12f * scale;
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

        DrawKeyGuide(scale);

        // X: 현재 좌표와 가장자리까지 거리. 맵 크기(WorldConfig.Half)를 직접 확인할 수 있다.
        if (showCoords && fpc != null && !SeekerHiding())
        {
            if (coordStyle == null)
                coordStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            coordStyle.fontStyle = FontStyle.Bold;
            coordStyle.fontSize = Mathf.RoundToInt(34f * scale);
            coordStyle.alignment = TextAnchor.MiddleCenter;
            UiTheme.Apply(coordStyle);
            var p = fpc.transform.position;
            string text = p.x.ToString("0.0") + ", " + p.z.ToString("0.0"); // 좌표 숫자만
            // 어두운 틀 + 큰 글씨 + 밝은 색: 복잡한 배경에서도 읽히게
            float cw = coordStyle.CalcSize(new GUIContent(text)).x + 44f * scale;
            var cr = new Rect(16f * scale, 12f * scale, Mathf.Max(cw, 180f * scale), 58f * scale);
            UiTheme.Frame(cr, 0.9f, 1f, 8f * scale);
            HudText.Label(cr, text, coordStyle, new Color(1f, 0.97f, 0.8f));
        }

        DrawHealth(scale);
        DrawGun(scale);

        GUI.color = prev;
    }

    GUIStyle keyStyle;
    readonly System.Collections.Generic.List<string> keyLines = new System.Collections.Generic.List<string>();

    // 키 안내: 오른쪽 가장자리 세로 가운데에 틀 + 한 줄씩. 코드에서 실제로 쓰는 키만 넣는다(테스트용 G/O/H는 제외).
    // 위치와 크기는 모두 화면 높이 배율. 줄 목록은 매 프레임 같은 리스트를 재사용한다.
    void DrawKeyGuide(float scale)
    {
        keyLines.Clear();
        keyLines.Add("[WASD] 이동");
        keyLines.Add("[Shift] 달리기");
        keyLines.Add("[Ctrl] 천천히 걷기");
        keyLines.Add("[C] 웅크리기 (멈춘 상태)");
        keyLines.Add("[Space] 점프");
        if (flash != null) keyLines.Add("[T] 손전등 " + (flash.IsOn ? "켜짐" : "꺼짐"));
        keyLines.Add("[F] 길게: 종이 줍기 / 탈출");
        keyLines.Add("[M] 지도");
        keyLines.Add("[X] 좌표 " + (showCoords ? "끄기" : "켜기"));
        if (gun != null && gun.HasGun) keyLines.Add("[클릭] 공격");
        keyLines.Add("[Esc] 메뉴");

        if (keyStyle == null)
        {
            keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        }
        keyStyle.fontSize = Mathf.RoundToInt(17f * scale);
        UiTheme.Apply(keyStyle);

        float lineH = 26f * scale, pad = 10f * scale;
        float w = 250f * scale, h = keyLines.Count * lineH + pad * 2f;
        var panel = new Rect(Screen.width - w - 16f * scale, Screen.height * 0.5f - h * 0.5f, w, h);
        UiTheme.Frame(panel, 0.5f, 0.9f, 7f * scale);
        for (int i = 0; i < keyLines.Count; i++)
            HudText.Label(new Rect(panel.x + pad + 4f * scale, panel.y + pad + i * lineH, w - pad * 2f, lineH), keyLines[i], keyStyle);
    }

    // 오른쪽 아래(손전등 안내 위): 잡는 팀의 총/탄 또는 총 없음 안내. 첫 종이 발견 후엔 자동 지급 카운트다운.
    void DrawGun(float scale)
    {
        if (gun == null || (!gun.IsSeeker && !gun.HasGun)) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        if (gunStyle == null) gunStyle = new GUIStyle(hpStyle) { alignment = TextAnchor.LowerRight };
        gunStyle.fontSize = Mathf.RoundToInt(19f * scale);
        if (gunNameStyle == null)
        {
            gunNameStyle = new GUIStyle(gunStyle) { alignment = TextAnchor.UpperLeft };
            gunCntStyle = new GUIStyle(gunStyle) { alignment = TextAnchor.UpperRight };
            gunMidStyle = new GUIStyle(gunStyle) { alignment = TextAnchor.MiddleCenter };
        }
        gunStyle.fontSize = Mathf.RoundToInt(27f * scale);
        gunNameStyle.fontSize = gunCntStyle.fontSize = gunMidStyle.fontSize = gunStyle.fontSize;
        UiTheme.Apply(gunStyle); UiTheme.Apply(gunNameStyle); UiTheme.Apply(gunCntStyle); UiTheme.Apply(gunMidStyle);
        float pw = 310f * scale, ph = 74f * scale;
        var panel = new Rect(Screen.width - pw - 16f * scale, Screen.height - 50f * scale - ph, pw, ph);
        if (!gun.HasGun)
        {
            string text = "총 없음: 종이를 찾으세요";
            float left = armory != null ? armory.AutoGrantLeft : -1f;
            if (left > 0f) text += " (자동 지급 " + Mathf.CeilToInt(left) + "초)";
            float tw = gunStyle.CalcSize(new GUIContent(text)).x + 36f * scale;
            var pr = new Rect(Screen.width - tw - 16f * scale, panel.y + 12f * scale, tw, 50f * scale);
            UiTheme.Frame(pr, 0.9f, 1f, 7f * scale);
            HudText.Label(pr, text, gunMidStyle);
            return;
        }
        UiTheme.Frame(panel, 0.9f, 1f, 8f * scale);
        bool knife = gun.Weapon == WeaponKind.Knife;
        int kind = gun.Weapon == WeaponKind.Pistol ? 1 : gun.Weapon == WeaponKind.Rifle ? 2 : 3;
        bool empty = !knife && gun.Ammo <= 0;
        Color tint = empty ? new Color(0.9f, 0.25f, 0.2f, 0.95f) : new Color(1f, 0.98f, 0.92f, 1f);
        UiTheme.IconWeapon(new Rect(panel.x + 12f * scale, panel.y + 12f * scale, 74f * scale, 44f * scale), kind, tint);
        float tx = panel.x + 100f * scale;
        HudText.Label(new Rect(tx, panel.y + 4f * scale, 110f * scale, 36f * scale), GunHolder.WeaponName(gun.Weapon), gunNameStyle, tint);
        if (knife) return;
        HudText.Label(new Rect(panel.xMax - 100f * scale, panel.y + 4f * scale, 88f * scale, 36f * scale), gun.Ammo + "/" + gun.MaxAmmo, gunCntStyle, tint);
        UiTheme.AmmoPips(new Rect(tx, panel.y + 44f * scale, panel.xMax - tx - 12f * scale, 22f * scale), gun.Ammo, gun.MaxAmmo,
            empty ? new Color(0.9f, 0.25f, 0.2f, 0.95f) : new Color(0.95f, 0.8f, 0.45f, 0.95f));
    }

    // 왼쪽 아래 체력 막대 + 피격 시 붉은 번쩍임 + 사망 안내
    void DrawHealth(float scale)
    {
        if (health == null) return;
        if (hpStyle == null)
            hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold };
        hpStyle.fontSize = Mathf.RoundToInt(30f * scale);
        UiTheme.Apply(hpStyle);

        // 피격: 가장자리가 붉게 번졌다 사라지는 비네트. 체력이 30% 아래면 심장 뛰듯 약하게 계속 깜빡인다.
        float vig = 0f;
        if (Time.unscaledTime < flashUntil) vig = 0.9f * (flashUntil - Time.unscaledTime) / FlashSeconds;
        if (!health.IsDead && health.Hp01 < 0.3f)
            vig = Mathf.Max(vig, (0.3f - health.Hp01) / 0.3f * (0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f)) + 0.15f);
        if (vig > 0.01f) UiTheme.DrawVignette(new Color(0.75f, 0.02f, 0.02f, Mathf.Clamp01(vig)));

        float w = 260f * scale, h = 18f * scale;
        float x = 16f * scale, y = Screen.height - 62f * scale; // 스태미나 막대가 바로 아래(Screen.height - 36)에 온다
        // 체력/스태미나 전체 뒤에 어두운 판: 배경이 복잡해도 막대와 글씨가 또렷하게 보인다. 위치는 그대로.
        UiTheme.Frame(new Rect(x - 12f * scale, y - 56f * scale, w + 24f * scale, 102f * scale), 0.9f, 1f, 8f * scale);
        HudText.Label(new Rect(x, y - 50f * scale, w, 40f * scale), "체력 " + Mathf.CeilToInt(health.Hp), hpStyle, new Color(1f, 0.97f, 0.9f));
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
