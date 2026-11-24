using UnityEngine;

// 화면 정중앙의 흰색 동그란 크로스헤어. 코드로 그려서 이미지 파일이 필요 없다.
public class Crosshair : MonoBehaviour
{
    public int size = 28;          // 지름(px)
    public float thickness = 2f;   // 링 두께(px)

    Texture2D ring;

    void Awake()
    {
        ring = BuildRing(size, thickness);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        // Esc로 커서를 풀었을 때는 숨긴다.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float x = Screen.width * 0.5f - size * 0.5f;
        float y = Screen.height * 0.5f - size * 0.5f;
        GUI.DrawTexture(new Rect(x, y, size, size), ring);
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
