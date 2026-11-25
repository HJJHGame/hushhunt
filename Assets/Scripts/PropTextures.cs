using System.Collections.Generic;
using UnityEngine;

// 집/바위/울타리/통나무에 코드로 만든 질감을 입힌다. 이미지 파일 없이 시작할 때 적용된다.
// 씬의 머티리얼 에셋을 직접 바꾸면 플레이 모드를 나와도 변경이 남으므로(에디터 주의점),
// 이름이 같은 런타임 복제 머티리얼을 만들어 그걸로 갈아 끼운다.
public class PropTextures : MonoBehaviour
{
    readonly List<Texture2D> texes = new List<Texture2D>();
    readonly List<Material> mats = new List<Material>();
    readonly HashSet<Texture2D> photos = new HashSet<Texture2D>(); // Resources 에서 불러온 사진 (지우지 않는다)

    void Start()
    {
        // SetupScene이 만든 머티리얼 이름 -> 텍스처. Assets/Resources/Textures 에 실사 사진
        // (ambientCG CC0)이 있으면 그걸 쓰고, 없으면 절차적 텍스처로 버틴다.
        // 절차적 텍스처는 회색조라 머티리얼 색이 입히고, 사진은 풀컬러라 흰색 그대로 둔다.
        var texByName = new Dictionary<string, Texture2D>
        {
            { "House A", PhotoOr("Siding", () => Siding(11, true)) },
            { "House B", PhotoOr("Siding", () => Siding(23, true)) },
            { "House C", PhotoOr("Siding", () => Siding(37, true)) },
            { "Roof", PhotoOr("Roof", () => Shingles(5)) },
            { "Trim", PhotoOr("Planks", () => WoodGrain(7, true)) },
            { "Floor", PhotoOr("Planks", () => Siding(13, false)) },   // 가로 판자 바닥
            { "Furniture", PhotoOr("Planks", () => WoodGrain(17, false)) },
            { "Rock", PhotoOr("Rock", () => Rock(29)) },
            { "Fence", PhotoOr("Planks", () => WoodGrain(31, true)) },
            { "Log", PhotoOr("Bark", () => WoodGrain(41, false)) },
        };

        var replByName = new Dictionary<string, Material>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null || !texByName.ContainsKey(m.name)) continue;
            if (!replByName.TryGetValue(m.name, out var repl))
            {
                var tint = photos.Contains(texByName[m.name]) ? Color.white : m.color;
                repl = RuntimeMaterials.CreateLit(m.name + " (textured)", tint, 0.05f);
                repl.mainTexture = texByName[m.name];
                replByName[m.name] = repl;
                mats.Add(repl);
            }
            r.sharedMaterial = repl;
        }
    }

    void OnDestroy()
    {
        foreach (var m in mats) if (m != null) Destroy(m);
        foreach (var t in texes) if (t != null) Destroy(t);
        mats.Clear();
        texes.Clear();
    }

    // Resources/Textures/<resName> 사진이 있으면 그걸 쓰고 (공유 에셋이라 OnDestroy 에서 지우지 않는다),
    // 없으면 fallback()으로 절차적 텍스처를 만든다.
    Texture2D PhotoOr(string resName, System.Func<Texture2D> fallback)
    {
        var t = Resources.Load<Texture2D>("Textures/" + resName);
        if (t != null) { photos.Add(t); return t; }
        return fallback();
    }

    Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGB24, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 4;
        texes.Add(t);
        return t;
    }

    // 가장자리가 이어지는(타일링) 노이즈. GroundTexture와 같은 기법.
    static float TileN(float u, float v, float fx, float fy, float seed)
    {
        float a = Mathf.PerlinNoise(seed + u * fx, seed + v * fy);
        float b = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + v * fy);
        float c = Mathf.PerlinNoise(seed + u * fx, seed + (v - 1f) * fy);
        float d = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + (v - 1f) * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // 판자 벽/바닥: 판마다 밝기가 다르고 판 사이 틈이 어둡다. vertical이면 세로 판.
    Texture2D Siding(int seed, bool vertical)
    {
        const int W = 256, H = 256, P = 8;
        var tex = NewTex(W, H);
        var rand = new System.Random(seed);
        float[] shade = new float[P];
        for (int i = 0; i < P; i++) shade[i] = 0.75f + (float)rand.NextDouble() * 0.35f;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)W, v = y / (float)H;
            float along = vertical ? u : v, across = vertical ? v : u;
            int p = Mathf.FloorToInt(along * P) % P;
            float seam = Mathf.Abs(along * P - Mathf.Floor(along * P) - 0.5f) * 2f; // 판 중심 1, 가장자리 0
            float g = shade[p] * (0.55f + 0.45f * Mathf.Clamp01((seam - 0.80f) / 0.20f));
            float grain = TileN(u, v, vertical ? 24f : 3f, vertical ? 3f : 24f, seed) * 0.35f
                        + TileN(u, v, vertical ? 60f : 9f, vertical ? 9f : 60f, seed + 50) * 0.2f;
            float s = Mathf.Clamp01(g * (0.72f + grain));
            px[y * W + x] = new Color(s, s, s);
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // 기와/널 지붕: 가로 줄마다 밝기와 가로 오프셋이 다르고 줄 사이가 어둡다.
    Texture2D Shingles(int seed)
    {
        const int W = 256, H = 256, R = 10;
        var tex = NewTex(W, H);
        var rand = new System.Random(seed);
        float[] shade = new float[R];
        int[] off = new int[R];
        for (int i = 0; i < R; i++) { shade[i] = 0.7f + (float)rand.NextDouble() * 0.4f; off[i] = rand.Next(64); }
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)W, v = y / (float)H;
            int r = Mathf.FloorToInt(v * R) % R;
            float rowEdge = Mathf.Abs(v * R - Mathf.Floor(v * R) - 0.5f) * 2f;
            float g = shade[r] * (0.5f + 0.5f * Mathf.Clamp01((rowEdge - 0.75f) / 0.25f));
            float xo = ((x + off[r]) % 64) / 64f;
            float gap = Mathf.Clamp01((Mathf.Abs(xo - 0.5f) * 2f - 0.9f) / 0.1f); // 널 사이 세로 틈
            float n = TileN(u, v, 24f, 6f, seed) * 0.3f;
            float s = Mathf.Clamp01(g * (0.7f + n)) * (0.55f + 0.45f * gap);
            px[y * W + x] = new Color(s, s, s);
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // 길쭉한 나무 결 (울타리/가구/통나무/문틀).
    Texture2D WoodGrain(int seed, bool vertical)
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float u = x / (float)S, v = y / (float)S;
            float a = vertical ? TileN(u, v, 18f, 2f, seed) : TileN(u, v, 2f, 18f, seed);
            float ridge = Mathf.Abs(a - 0.5f) * 2f;
            float fine = vertical ? TileN(u, v, 40f, 12f, seed + 7) : TileN(u, v, 12f, 40f, seed + 7);
            float s = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01((1f - ridge) * (0.6f + fine * 0.7f)));
            px[y * S + x] = new Color(s, s, s);
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // 얼룩 + 잔돌 느낌의 바위.
    Texture2D Rock(int seed)
    {
        const int S = 128;
        var tex = NewTex(S, S);
        var rand = new System.Random(seed);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float u = x / (float)S, v = y / (float)S;
            float big = TileN(u, v, 4f, 4f, seed);
            float mid = TileN(u, v, 14f, 14f, seed + 3);
            float s = Mathf.Lerp(0.35f, 0.8f, Mathf.Clamp01(big * 0.7f + mid * 0.5f));
            px[y * S + x] = new Color(s, s, s);
        }
        for (int i = 0; i < 350; i++) // 잔돌 점
        {
            int cx = rand.Next(S), cy = rand.Next(S);
            float g = 0.5f + (float)rand.NextDouble() * 0.5f;
            px[cy * S + cx] = new Color(g, g, g);
        }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }
}
