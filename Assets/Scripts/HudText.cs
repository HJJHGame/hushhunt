using UnityEngine;

// 화면 위 글씨 공용 그리기: 진한 검정 외곽선(8방향 2px) + 완전 불투명 글씨.
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
        GUI.color = Color.black;
        for (int dx = -2; dx <= 2; dx += 2)
        {
            for (int dy = -2; dy <= 2; dy += 2)
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
