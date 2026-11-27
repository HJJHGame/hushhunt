using UnityEngine;

// 밤하늘: 별, 달(달빛 방향과 일치), 지평선 안개 띠를 코드로 만든 텍스처로 그린다.
// 카메라를 따라다니는 돔 하나만 쓰고, 안개/조명의 영향을 받지 않는 Sprites/Default 셰이더를 쓴다.
// 달 위치는 씬의 방향광(달빛) 반대 방향이라 그림자 방향과 맞는다.
public class NightSky : MonoBehaviour
{
    public float domeRadius = 45f; // 카메라 먼 거리(50m)보다 안쪽이어야 그려진다
    public int starCount = 1400;
    public float moonSize = 0.055f; // 라디안 (달 반지름)

    Transform dome;
    Camera cam;
    Texture2D tex;
    Material mat;
    Mesh mesh;

    void Start()
    {
        cam = Camera.main;
        // 씬에 저장된 값(예전 100m)이 남아 있어도 먼 거리 안으로 맞춘다.
        if (cam != null) domeRadius = Mathf.Min(domeRadius, cam.farClipPlane * 0.9f);

        Vector3 moonDir = new Vector3(-0.35f, 0.55f, 0.75f).normalized;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            moonDir = -l.transform.forward;
            break;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        tex = BuildTexture(moonDir);
        mesh = BuildDomeMesh();
        mat = new Material(shader);
        mat.mainTexture = tex;
        mat.renderQueue = 1000; // 배경: 다른 모든 물체가 위에 그려진다

        var go = new GameObject("Night Sky");
        dome = go.transform;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        dome.localScale = Vector3.one * domeRadius;
    }

    void LateUpdate()
    {
        if (dome == null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null) dome.position = cam.transform.position;
    }

    void OnDestroy()
    {
        if (dome != null) Destroy(dome.gameObject);
        if (mat != null) Destroy(mat);
        if (tex != null) Destroy(tex);
        if (mesh != null) Destroy(mesh);
    }

    // 알파 합성 (src를 dst 위에)
    static void Over(Color[] px, int i, Color src)
    {
        Color dst = px[i];
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0.0001f) { px[i] = new Color(0f, 0f, 0f, 0f); return; }
        float inv = 1f / a;
        px[i] = new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) * inv,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) * inv,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) * inv,
            a);
    }

    Texture2D BuildTexture(Vector3 moonDir)
    {
        const int W = 1024, H = 512;
        var px = new Color[W * H];

        // 1) 지평선 안개 띠: 안개색과 같아서 멀리 나무 실루엣이 자연스럽게 이어진다.
        for (int y = 0; y < H; y++)
        {
            float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
            float a = Mathf.Clamp01(1f - Mathf.Abs(lat) / 0.5f);
            a = a * a * 0.9f;
            for (int x = 0; x < W; x++)
                px[y * W + x] = new Color(0.004f, 0.005f, 0.007f, a); // NightAtmosphere의 안개색과 동일하게 유지
        }

        // 2) 별: 위쪽 반구에 고르게. 극 근처는 가로로 늘어나므로 그만큼 넓게 찍는다.
        var rng = new System.Random(7);
        for (int i = 0; i < starCount; i++)
        {
            float y = 0.04f + (float)rng.NextDouble() * 0.96f;
            float u = (float)rng.NextDouble();
            float lat = Mathf.Asin(y);
            float v = lat / Mathf.PI + 0.5f;
            int cx = Mathf.Min(W - 1, (int)(u * W));
            int cy = Mathf.Clamp((int)(v * H), 0, H - 1);
            float cosLat = Mathf.Max(0.12f, Mathf.Cos(lat));
            int half = Mathf.Clamp(Mathf.RoundToInt(0.5f / cosLat - 0.5f), 0, 6);
            float r = (float)rng.NextDouble();
            float b = 0.25f + r * r * r * 0.75f;
            Color tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.92f, 0.8f), (float)rng.NextDouble());
            for (int dx = -half; dx <= half; dx++)
            {
                int xx = (cx + dx + W) % W;
                Over(px, cy * W + xx, new Color(tint.r, tint.g, tint.b, b));
            }
        }

        // 3) 달 + 달무리
        float mLat = Mathf.Asin(Mathf.Clamp(moonDir.y, -1f, 1f));
        const float glowR = 0.45f;
        for (int y = 0; y < H; y++)
        {
            float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
            if (Mathf.Abs(lat - mLat) > glowR) continue;
            float cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
            for (int x = 0; x < W; x++)
            {
                float phi = (x + 0.5f) / W * Mathf.PI * 2f;
                var d = new Vector3(cl * Mathf.Cos(phi), sl, cl * Mathf.Sin(phi));
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, moonDir), -1f, 1f));
                if (ang > glowR) continue;

                float g = 1f - (Mathf.Max(ang, moonSize) - moonSize) / (glowR - moonSize);
                g = g * g * g * 0.35f; // 달무리도 어둡게
                Over(px, y * W + x, new Color(0.55f, 0.68f, 0.95f, g));

                if (ang < moonSize)
                {
                    float shade = 0.80f + 0.20f * Mathf.PerlinNoise(x * 0.12f, y * 0.12f); // 달 표면 얼룩
                    float edge = Mathf.Clamp01((moonSize - ang) / 0.004f);
                    Over(px, y * W + x, new Color(0.86f * shade, 0.9f * shade, shade, edge));
                }
            }
        }

        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.wrapModeU = TextureWrapMode.Repeat;
        t.wrapModeV = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(px);
        t.Apply(false);
        return t;
    }

    // 반지름 1짜리 구. UV: u = 방위각, v = 위도.
    static Mesh BuildDomeMesh()
    {
        const int rings = 24, segs = 48;
        var verts = new Vector3[(rings + 1) * (segs + 1)];
        var uvs = new Vector2[verts.Length];
        for (int r = 0; r <= rings; r++)
        {
            float theta = r / (float)rings * Mathf.PI; // 0 = 꼭대기
            for (int s = 0; s <= segs; s++)
            {
                float phi = s / (float)segs * Mathf.PI * 2f;
                int i = r * (segs + 1) + s;
                verts[i] = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                uvs[i] = new Vector2(s / (float)segs, 1f - r / (float)rings);
            }
        }

        var tris = new int[rings * segs * 6];
        int k = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                int a = r * (segs + 1) + s;
                int b = a + segs + 1;
                tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
            }
        }

        var m = new Mesh { name = "NightSkyDome" };
        m.vertices = verts;
        m.uv = uvs;
        m.triangles = tris;
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return m;
    }
}
