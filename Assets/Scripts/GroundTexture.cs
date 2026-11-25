using UnityEngine;

// 바닥 개선: 단색 땅 대신 코드로 만든 숲 바닥 텍스처(흙, 이끼, 낙엽, 잔돌 느낌)를 타일로 깐다.
// 이미지 파일이 필요 없고, 씬을 다시 만들지 않아도 시작할 때 "Ground" 오브젝트에 적용된다.
public class GroundTexture : MonoBehaviour
{
    const int Size = 256;
    const float TilesAcrossGround = 140f; // 400m 땅에 140번 반복 = 타일 하나가 약 2.9m

    Texture2D tex;
    Material mat;
    bool texOwned = true; // Resources 에서 불러온 공유 에셋이면 Destroy 하면 안 된다

    void Start()
    {
        var ground = GameObject.Find(WorldConfig.Names.Ground);
        if (ground == null) return;
        var rend = ground.GetComponent<Renderer>();
        if (rend == null) return;

        // 실사 숲 바닥 사진(ambientCG CC0)이 있으면 그걸 쓰고, 없으면 코드로 만든 텍스처로 버틴다.
        tex = Resources.Load<Texture2D>("Textures/Ground");
        texOwned = tex == null;
        if (tex == null) tex = BuildTexture();
        mat = RuntimeMaterials.CreateLit("Ground (runtime)", Color.white, 0.05f); // 젖은 흙이지만 반짝이지 않게
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(TilesAcrossGround, TilesAcrossGround);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        rend.material = mat;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (tex != null && texOwned) Destroy(tex);
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

        var soil = new Color(0.10f, 0.075f, 0.055f);
        var dark = new Color(0.045f, 0.038f, 0.032f);
        var moss = new Color(0.06f, 0.11f, 0.05f);
        var leaf = new Color(0.17f, 0.11f, 0.045f);

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

        // 잔디 포기: 짧은 세로 초록 획을 심는다.
        var grass = new Color(0.09f, 0.14f, 0.065f);
        for (int i = 0; i < 700; i++)
        {
            int cx = rand.Next(Size), cy = rand.Next(Size);
            int len = 2 + rand.Next(3);
            float shade = 0.5f + (float)rand.NextDouble() * 0.7f;
            var gc = grass * shade;
            for (int dy = 0; dy < len; dy++)
                px[((cy + dy) % Size) * Size + cx] = Color.Lerp(gc, px[((cy + dy) % Size) * Size + cx], 0.25f);
        }

        // 잔돌: 밝은 회색 점을 흩뿌린다.
        var gravel = new Color(0.22f, 0.22f, 0.23f);
        for (int i = 0; i < 500; i++)
        {
            float shade = 0.5f + (float)rand.NextDouble() * 0.8f;
            px[rand.Next(Size) * Size + rand.Next(Size)] = gravel * shade;
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
