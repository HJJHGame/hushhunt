using UnityEngine;

// 미니맵 배경 지도 이미지를 런타임에 만든다.
// 씬에 실제 배치된 집(HouseFootprint)과 맵 경계 데이터를 그대로 그리므로
// 지도가 실제 맵과 어긋날 일이 없다. 나무/바위는 규칙대로 넣지 않는다.
//
// 1024x1024는 100만 픽셀이라 SetPixel/GetPixel을 픽셀마다 부르면 시작이 눈에 띄게 느려진다.
// 배열에 그려서 SetPixels와 Apply를 한 번만 한다.
public static class MapImage
{
    // 월드 좌표(-worldHalf ~ +worldHalf) -> 텍스처 픽셀. +Z(북쪽)가 위.
    public static Texture2D Build(int size, float worldHalf, HouseFootprint[] houses)
    {
        var px = new Color[size * size];

        PaintGround(px, size);
        PaintGrid(px, size);
        PaintHouses(px, size, worldHalf, houses);
        PaintFrame(px, size);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        tex.SetPixels(px);
        tex.Apply(false);
        return tex;
    }

    // 1) 바닥: 절차적 노이즈로 어두운 숲 땅 느낌
    static void PaintGround(Color[] px, int size)
    {
        var ground = new Color(0.055f, 0.075f, 0.065f);
        var groundDark = new Color(0.035f, 0.05f, 0.045f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 7f) * 0.65f
                        + Mathf.PerlinNoise(u * 31f + 11f, v * 31f + 5f) * 0.35f;
                px[y * size + x] = Color.Lerp(groundDark, ground, n);
            }
        }
    }

    // 2) 격자: 50m 간격 (4등분)
    static void PaintGrid(Color[] px, int size)
    {
        var grid = new Color(1f, 1f, 1f, 0.06f);
        for (int i = 1; i < 4; i++)
        {
            int p = Mathf.RoundToInt(i * size / 4f);
            for (int k = 0; k < size; k++)
            {
                px[k * size + p] = Over(px[k * size + p], grid); // 세로선
                px[p * size + k] = Over(px[p * size + k], grid); // 가로선
            }
        }
    }

    // 3) 집들: 실제 위치/크기. 테두리 + 채움 + 지붕 용마루 선.
    static void PaintHouses(Color[] px, int size, float worldHalf, HouseFootprint[] houses)
    {
        if (houses == null) return;

        var houseFill = new Color(0.45f, 0.36f, 0.25f);
        var houseEdge = new Color(0.16f, 0.12f, 0.08f);
        var ridge = new Color(0.28f, 0.22f, 0.15f);
        float worldSize = worldHalf * 2f;

        foreach (var h in houses)
        {
            if (h == null) continue;
            int cx = Mathf.RoundToInt((h.transform.position.x + worldHalf) / worldSize * (size - 1));
            // 텍스처 y축은 위로 갈수록 행 번호가 커진다. 북쪽(+Z)이 위로 오게 그대로 매핑 (Minimap.WorldToMap과 같은 기준)
            int cy = Mathf.RoundToInt((h.transform.position.z + worldHalf) / worldSize * (size - 1));
            int hw = Mathf.Max(2, Mathf.RoundToInt(h.size.x / worldSize * size / 2f));
            int hd = Mathf.Max(2, Mathf.RoundToInt(h.size.y / worldSize * size / 2f));

            for (int y = cy - hd; y <= cy + hd; y++)
            {
                for (int x = cx - hw; x <= cx + hw; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    bool edge = x == cx - hw || x == cx + hw || y == cy - hd || y == cy + hd;
                    px[y * size + x] = edge ? houseEdge : houseFill;
                }
            }
            // 용마루: 실제 지붕과 같이 X축 방향
            if (cy < 0 || cy >= size) continue;
            for (int x = cx - hw + 1; x <= cx + hw - 1; x++)
                if (x >= 0 && x < size) px[cy * size + x] = ridge;
        }
    }

    // 4) 경계 프레임: 지도 가장자리가 곧 맵 경계(±100m 벽)
    static void PaintFrame(Color[] px, int size)
    {
        var frame = new Color(0.02f, 0.025f, 0.025f);
        int t = Mathf.Max(2, size / 256);
        for (int k = 0; k < size; k++)
        {
            for (int i = 0; i < t; i++)
            {
                px[i * size + k] = frame;                 // 아래
                px[(size - 1 - i) * size + k] = frame;    // 위
                px[k * size + i] = frame;                 // 왼쪽
                px[k * size + (size - 1 - i)] = frame;    // 오른쪽
            }
        }
    }

    static Color Over(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float inv = 1f / a;
        return new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) * inv,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) * inv,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) * inv,
            a);
    }
}
