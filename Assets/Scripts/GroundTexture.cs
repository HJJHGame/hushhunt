using UnityEngine;

// 바닥 개선: 단색 땅 대신 코드로 만든 숲 바닥 텍스처(흙, 이끼, 낙엽, 잔돌 느낌)를 타일로 깐다.
// 이미지 파일이 필요 없고, 씬을 다시 만들지 않아도 시작할 때 "Ground" 오브젝트에 적용된다.
public class GroundTexture : MonoBehaviour
{
    const int Size = 256;
    const float TilesAcrossGround = 70f; // 200m 땅에 70번 반복 = 타일 하나가 약 2.9m

    void Start()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) return;
        var rend = ground.GetComponent<Renderer>();
        if (rend == null) return;

        var tex = BuildTexture();
        var mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(TilesAcrossGround, TilesAcrossGround);
        mat.color = Color.white;
        mat.SetFloat("_Glossiness", 0.05f); // 젖은 흙이지만 반짝이지 않게
        mat.SetFloat("_Metallic", 0f);
        rend.material = mat;
    }

    // 가장자리가 이어지는(타일링) 노이즈: 네 방향 오프셋 샘플을 섞는다.
    static float Tile(float x, float y, float freq, float seed)
    {
        float u = x / Size, v = y / Size;
        float a = Mathf.PerlinNoise(seed + u * freq, seed + v * freq);
        float b = Mathf.PerlinNoise(seed + (u - 1f) * freq, seed + v * freq);
        float c = Mathf.PerlinNoise(seed + u * freq, seed + (v - 1f) * freq);
        float d = Mathf.PerlinNoise(seed + (u - 1f) * freq, seed + (v - 1f) * freq);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static Texture2D BuildTexture()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;

        var rand = new System.Random(5);
        var px = new Color[Size * Size];

        var soil = new Color(0.16f, 0.12f, 0.09f);
        var dark = new Color(0.07f, 0.06f, 0.05f);
        var moss = new Color(0.10f, 0.17f, 0.08f);
        var leaf = new Color(0.27f, 0.17f, 0.07f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float big = Tile(x, y, 3f, 10f);      // 큰 얼룩 (이끼 vs 흙)
                float mid = Tile(x, y, 12f, 40f);     // 중간 덩어리
                float fine = Tile(x, y, 48f, 90f);    // 잔 알갱이

                var c = Color.Lerp(soil, dark, Mathf.Clamp01(mid * 1.2f - 0.1f));
                c = Color.Lerp(c, moss, Mathf.SmoothStep(0.45f, 0.75f, big) * 0.8f);
                c *= 0.75f + fine * 0.5f; // 알갱이 명암
                px[y * Size + x] = c;
            }
        }

        // 낙엽/솔잎 조각: 작은 갈색 점들을 타일 경계에서 감싸 이어지게 찍는다.
        for (int i = 0; i < 900; i++)
        {
            int cx = rand.Next(Size), cy = rand.Next(Size);
            int w = 1 + rand.Next(3), h = 1 + rand.Next(2);
            float shade = 0.6f + (float)rand.NextDouble() * 0.6f;
            var lc = Color.Lerp(leaf, dark, (float)rand.NextDouble() * 0.5f) * shade;
            for (int dy = 0; dy <= h; dy++)
                for (int dx = 0; dx <= w; dx++)
                    px[((cy + dy) % Size) * Size + ((cx + dx) % Size)] = lc;
        }

        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
