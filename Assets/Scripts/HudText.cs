using UnityEngine;

// 화면 위 글씨 공용 그리기: 그림자 + 검정 외곽선(8방향 1px) + 완전 불투명 글씨. 글꼴은 UiTheme.
// 거의 검정인 화면 + 화면 압축(원격 데스크톱)에서도 선명하게 읽힌다.
public static class HudText
{
    public static void Label(Rect r, string text, GUIStyle style)
    {
        Label(r, text, style, Color.white);
    }

    public static void Label(Rect r, string text, GUIStyle style, Color color)
    {
        var prev = GUI.color;
        UiTheme.Apply(style);
        // 부드러운 그림자(오른쪽 아래) + 1px 검정 외곽선: 두꺼운 외곽선보다 깔끔하고 어두운 화면에서도 읽힌다.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        GUI.color = Color.black;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, style);
            }
        }
        color.a = 1f; // 옅은 글씨 금지: 항상 완전 불투명
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = prev;
    }
}

// 화면(OnGUI) 공용 모양: 글꼴, 어두운 반투명 패널 + 가는 흰 테두리, 테두리 있는 막대.
// 파일(글꼴/그림)을 따로 추가하지 않는다. 글꼴은 컴퓨터에 깔린 깔끔한 한글 글꼴을 이름으로 찾아 쓰고,
// 하나도 없으면 유니티 기본 글꼴을 그대로 쓴다.
public static class UiTheme
{
    // 위에서부터 먼저 찾은 글꼴을 쓴다 (Windows / Mac / 리눅스 순).
    static readonly string[] Preferred =
    {
        "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "나눔고딕",
    };

    static Font font;
    static bool fontTried;

    // 찾은 글꼴. 없으면 null (스타일의 기본 글꼴 유지).
    public static Font Font
    {
        get
        {
            if (fontTried) return font;
            fontTried = true;
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                foreach (var want in Preferred)
                {
                    foreach (var have in installed)
                    {
                        if (string.Equals(have, want, System.StringComparison.OrdinalIgnoreCase))
                        {
                            font = Font.CreateDynamicFontFromOSFont(have, 16);
                            Debug.Log("[UiTheme] 글꼴: " + have);
                            return font;
                        }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[UiTheme] 글꼴 찾기 실패, 기본 글꼴 사용: " + e.Message); }
            return font;
        }
    }

    // 스타일에 글꼴을 입힌다 (이미 있으면 그대로).
    public static GUIStyle Apply(GUIStyle style)
    {
        if (style != null && style.font == null && Font != null) style.font = Font;
        return style;
    }

    // 어두운 반투명 바탕 + 가는 흰 테두리.
    public static void Panel(Rect r, float fillAlpha = 0.55f, float outlineAlpha = 0.45f, float line = 1f)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.02f, 0.02f, 0.03f, fillAlpha);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        Outline(r, outlineAlpha, line);
        GUI.color = prev;
    }

    // 가는 흰 테두리만.
    public static void Outline(Rect r, float alpha = 0.45f, float line = 1f)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.92f, 0.92f, 0.9f, alpha);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, line), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - line, r.width, line), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, line, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - line, r.y, line, r.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 테두리 있는 막대. fraction 0~1만큼 fill 색으로 채운다. alpha는 전체 투명도(서서히 사라지게 할 때).
    public static void Bar(Rect r, float fraction, Color fill, float alpha = 1f)
    {
        var prev = GUI.color;
        Panel(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), 0.6f * alpha, 0.45f * alpha);
        fill.a = 0.92f * alpha;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // ---- 절차적 그림(파일 없음) ----
    static Texture2D gradTex, vignetteTex;

    // 위가 불투명하고 아래로 갈수록 투명한 세로 그라데이션 (흰색, GUI.color로 색을 입힌다).
    public static Texture2D Gradient
    {
        get
        {
            if (gradTex != null) return gradTex;
            gradTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 32; y++) gradTex.SetPixel(0, y, new Color(1f, 1f, 1f, y / 31f));
            gradTex.Apply();
            return gradTex;
        }
    }

    // 가장자리만 어두운/붉은 둥근 비네트 (흰색, GUI.color로 색을 입힌다).
    public static Texture2D Vignette
    {
        get
        {
            if (vignetteTex != null) return vignetteTex;
            const int n = 96;
            vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.85f);
                    vignetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            vignetteTex.Apply();
            return vignetteTex;
        }
    }

    // 화면 전체 가장자리를 color(알파=세기)로 물들인다.
    public static void DrawVignette(Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Vignette);
        GUI.color = prev;
    }

    // 모서리 장식(ㄱ자 네 개): 가는 테두리 위에 살짝 굵고 밝게 덧그린다.
    public static void Corners(Rect r, float len, float alpha = 0.9f, float t = 2f)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.95f, 0.93f, 0.88f, alpha);
        var w = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(r.x, r.y, len, t), w); GUI.DrawTexture(new Rect(r.x, r.y, t, len), w);
        GUI.DrawTexture(new Rect(r.xMax - len, r.y, len, t), w); GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, len), w);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, len, t), w); GUI.DrawTexture(new Rect(r.x, r.yMax - len, t, len), w);
        GUI.DrawTexture(new Rect(r.xMax - len, r.yMax - t, len, t), w); GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - len, t, len), w);
        GUI.color = prev;
    }

    // 고급 패널: 어두운 바탕 + 위쪽 은은한 그라데이션 + 가는 테두리 + 모서리 장식.
    public static void Frame(Rect r, float fillAlpha = 0.6f, float alpha = 1f, float corner = 6f)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.02f, 0.02f, 0.03f, fillAlpha * alpha);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.1f, 0.08f, 0.22f * alpha);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * 0.6f), Gradient);
        Outline(r, 0.3f * alpha);
        Corners(r, corner, 0.85f * alpha);
        GUI.color = prev;
    }

    // 칸 나눔 눈금이 있는 막대. segs칸, 채움 끝에 밝은 선. 위쪽 절반에 빛 번짐.
    public static void SegBar(Rect r, float fraction, Color fill, int segs, float alpha = 1f)
    {
        var prev = GUI.color;
        fraction = Mathf.Clamp01(fraction);
        Frame(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), 0.65f, alpha, 5f);
        float fw = r.width * fraction;
        fill.a = 0.92f * alpha;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x, r.y, fw, r.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.28f * alpha);
        GUI.DrawTexture(new Rect(r.x, r.y, fw, r.height * 0.55f), Gradient);
        if (fw > 2f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(r.x + fw - 1.5f, r.y, 1.5f, r.height), Texture2D.whiteTexture);
        }
        GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
        for (int i = 1; i < segs; i++)
            GUI.DrawTexture(new Rect(r.x + r.width * i / segs - 1f, r.y, 2f, r.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 십자(체력) 아이콘
    public static void IconCross(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        float t = r.height * 0.3f;
        GUI.DrawTexture(new Rect(r.center.x - t * 0.5f, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.center.y - t * 0.5f, r.width, t), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 겹쳐진 화살표(달리기/스태미나) 아이콘
    public static void IconChevrons(Rect r, Color c)
    {
        var prev = GUI.color;
        var m = GUI.matrix;
        GUI.color = c;
        float t = Mathf.Max(2f, r.height * 0.16f), len = r.width * 0.55f;
        for (int k = 0; k < 2; k++)
        {
            float cx = r.x + r.width * (0.3f + 0.35f * k), cy = r.center.y;
            GUIUtility.RotateAroundPivot(40f, new Vector2(cx, cy - len * 0.3f));
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - len * 0.65f, t, len * 0.7f), Texture2D.whiteTexture);
            GUI.matrix = m;
            GUIUtility.RotateAroundPivot(-40f, new Vector2(cx, cy + len * 0.3f));
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - len * 0.05f, t, len * 0.7f), Texture2D.whiteTexture);
            GUI.matrix = m;
        }
        GUI.color = prev;
    }

    static void Box(float x, float y, float w, float h)
    {
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
    }

    // 무기 실루엣 아이콘(사각형 조합). kind: 1 권총, 2 소총, 3 칼. r 안에 가로로 그린다.
    public static void IconWeapon(Rect r, int kind, Color c)
    {
        var prev = GUI.color;
        var m = GUI.matrix;
        GUI.color = c;
        float u = r.height / 10f, x = r.x, y = r.y;
        if (kind == 1) // 권총: 총신(슬라이드) + 손잡이
        {
            Box(x + r.width * 0.2f, y + 2f * u, r.width * 0.6f, 3f * u);
            Box(x + r.width * 0.2f, y + 5f * u, r.width * 0.2f, 1.2f * u);
            Box(x + r.width * 0.22f, y + 5f * u, 2.6f * u, 5f * u);
        }
        else if (kind == 2) // 소총: 긴 총신 + 몸통 + 개머리판 + 탄창
        {
            Box(x, y + 3.5f * u, r.width * 0.28f, 1.2f * u);
            Box(x + r.width * 0.22f, y + 3f * u, r.width * 0.5f, 3f * u);
            Box(x + r.width * 0.7f, y + 3f * u, r.width * 0.3f, 4f * u);
            Box(x + r.width * 0.45f, y + 6f * u, 1.8f * u, 3.5f * u);
        }
        else // 칼(마체테): 기울어진 넓은 칼날 + 손잡이
        {
            GUIUtility.RotateAroundPivot(-30f, r.center);
            Box(x + r.width * 0.3f, y + 3.5f * u, r.width * 0.62f, 3f * u);
            GUI.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, c.a);
            Box(x + r.width * 0.1f, y + 4f * u, r.width * 0.2f, 2f * u);
            GUI.matrix = m;
        }
        GUI.matrix = m;
        GUI.color = prev;
    }

    // 탄 표시: 총알 모양 작은 막대 max개, 앞쪽 have개만 밝게.
    public static void AmmoPips(Rect r, int have, int max, Color on)
    {
        var prev = GUI.color;
        if (max <= 0) return;
        float gap = Mathf.Max(2f, r.width * 0.02f);
        float pw = Mathf.Min(r.height * 0.55f, (r.width - gap * (max - 1)) / max);
        for (int i = 0; i < max; i++)
        {
            float px = r.x + i * (pw + gap);
            bool lit = i < have;
            GUI.color = lit ? on : new Color(1f, 1f, 1f, 0.12f);
            Box(px, r.y + r.height * 0.2f, pw, r.height * 0.8f);
            if (lit) { GUI.color = new Color(1f, 1f, 1f, 0.5f); Box(px, r.y, pw, r.height * 0.25f); } // 탄두 끝 반짝임
        }
        GUI.color = prev;
    }
}
