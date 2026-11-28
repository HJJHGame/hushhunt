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
}
