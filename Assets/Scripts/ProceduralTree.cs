using UnityEngine;
using System.Collections.Generic;

// 코드로 만드는 소나무: 둥글고 위로 갈수록 가늘어지는 기둥 + 뿌리 부풀림 + 가지 몇 개 + 어두운 잎 단층.
// 외부 모델 파일 없이 시작할 때 만든다. Assets/Resources/Trees 에 모델을 넣으면 그것을 우선 쓴다.
// 나무 100그루가 공유할 수 있게 변형 3세트(껍질 메시 + 잎 메시)만 미리 만든다.
public static class ProceduralTree
{
    const float H = 9.5f; // 표준 높이: WorldConfig.PineHeight 와 같게 한다.

    class Variant { public Mesh bark; public Mesh foliage; }
    static Variant[] variants;

    // tree 아래에 TreeModel 한 개를 단다. height: 실제 나무 높이(m), girth: 굵기 배율.
    public static void Build(Transform tree, float height, float girth, Material barkMat, Material leafMat, System.Random rand)
    {
        EnsureVariants();
        var v = variants[rand.Next(variants.Length)];

        var root = new GameObject("TreeModel");
        root.transform.SetParent(tree, false);
        root.transform.localPosition = Vector3.zero;
        // 방향은 제각각, 살짝 기울어 획일적인 줄맞춤 느낌을 없앤다.
        root.transform.localRotation = Quaternion.Euler(
            (float)(rand.NextDouble() * 6.0 - 3.0), (float)rand.NextDouble() * 360f,
            (float)(rand.NextDouble() * 6.0 - 3.0));
        float k = height / H;
        root.transform.localScale = new Vector3(k * girth, k, k * girth);

        AddPart(root.transform, "Trunk", v.bark, barkMat);
        AddPart(root.transform, "Foliage", v.foliage, leafMat);
    }

    static void AddPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void EnsureVariants()
    {
        if (variants != null) return;
        variants = new Variant[5];
        for (int i = 0; i < variants.Length; i++)
        {
            var r = new System.Random(100 + i * 17);
            variants[i] = new Variant { bark = BuildBark(r), foliage = BuildFoliage(r) };
        }
    }

    // 높이(m) -> 기둥 반지름(m): 뿌리에서 부풀고 위로 갈수록 가늘어진다.
    static readonly float[] trunkY = { 0f, 0.35f, 1.0f, 1.8f, 2.8f, 4.0f, 5.2f, 6.5f };
    static readonly float[] trunkR = { 0.30f, 0.23f, 0.195f, 0.175f, 0.155f, 0.125f, 0.10f, 0.075f };

    static float RadiusAt(float y)
    {
        for (int i = 0; i < trunkY.Length - 1; i++)
        {
            if (y <= trunkY[i + 1])
            {
                float t = (y - trunkY[i]) / (trunkY[i + 1] - trunkY[i]);
                return Mathf.Lerp(trunkR[i], trunkR[i + 1], t);
            }
        }
        return trunkR[trunkR.Length - 1];
    }

    // 기둥 + 뿌리 부풀림 + 가지를 하나의 껍질 메시로 합친다 (나무당 그리기 호출을 줄이려고).
    static Mesh BuildBark(System.Random r)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        const int seg = 14;
        for (int ring = 0; ring < trunkY.Length; ring++)
        {
            for (int s = 0; s < seg; s++)
            {
                float a = Mathf.PI * 2f * s / seg;
                float wobble = 1f + ((float)r.NextDouble() * 2f - 1f) * 0.07f;
                float rad = trunkR[ring] * wobble;
                verts.Add(new Vector3(Mathf.Cos(a) * rad, trunkY[ring], Mathf.Sin(a) * rad));
                uvs.Add(new Vector2(s / (float)seg * 2f, trunkY[ring] * 0.8f));
            }
        }
        for (int ring = 0; ring < trunkY.Length - 1; ring++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a0 = ring * seg + s, a1 = ring * seg + (s + 1) % seg;
                int b0 = a0 + seg, b1 = a1 + seg;
                tris.AddRange(new[] { a0, b0, b1, a0, b1, a1 });
            }
        }
        // 기둥 꼭대기 뚜껑 (잎 아래라 거의 안 보이지만 아래서 올려다볼 때를 대비)
        {
            int center = verts.Count;
            verts.Add(new Vector3(0f, trunkY[trunkY.Length - 1], 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int top = (trunkY.Length - 1) * seg;
            for (int s = 0; s < seg; s++)
                tris.AddRange(new[] { center, top + (s + 1) % seg, top + s });
        }

        // 가지: 황금각으로 돌아가며 6~8개, 위로 55~70도 기울어진 가는 테이퍼 원기둥.
        // 잎 단층 사이와 아래쪽에서 실루엣에 드러난다.
        int branches = 6 + r.Next(3);
        for (int i = 0; i < branches; i++)
        {
            float h = 2.1f + (float)r.NextDouble() * 4.2f;
            float az = (float)(i * 137.5 + r.NextDouble() * 40.0) * Mathf.Deg2Rad;
            float tilt = (float)(55.0 + r.NextDouble() * 15.0) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(az) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(az) * Mathf.Sin(tilt));
            var start = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az)) * RadiusAt(h) * 0.8f + Vector3.up * h;
            float len = 1.1f + (float)r.NextDouble() * 0.7f;
            AddTaperedLimb(verts, uvs, tris, start, dir, len, 0.045f, 0.012f);
        }

        var mesh = new Mesh { name = "Pine Bark (procedural)" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // start에서 dir 방향으로 len만큼 뻗는 가는 원기둥 (가지용).
    static void AddTaperedLimb(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector3 start, Vector3 dir, float len, float r0, float r1)
    {
        const int seg = 6;
        var axisU = Vector3.Normalize(Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right));
        var axisV = Vector3.Cross(dir, axisU);
        int baseIndex = verts.Count;
        for (int ring = 0; ring < 2; ring++)
        {
            float rad = ring == 0 ? r0 : r1;
            var c = start + dir * (len * ring);
            for (int s = 0; s < seg; s++)
            {
                float a = Mathf.PI * 2f * s / seg;
                verts.Add(c + (axisU * Mathf.Cos(a) + axisV * Mathf.Sin(a)) * rad);
                uvs.Add(new Vector2(s / (float)seg, ring * len));
            }
        }
        for (int s = 0; s < seg; s++)
        {
            int a0 = baseIndex + s, a1 = baseIndex + (s + 1) % seg;
            tris.AddRange(new[] { a0, a0 + seg, a1 + seg, a0, a1 + seg, a1 });
        }
    }

    // ---- 잎: 바늘잎 가지 카드(알파 컷아웃) ----
    // 줄기에서 돌려나는 가지 약 70개를 카드 2장(교차)으로 만들고, 속은 가는 어두운 원뿔로 채운다.
    // 카드 텍스처는 코드로 그린 바늘잎 가지 (줄기 + 잔가지 + 바늘). 컷아웃이라 그림자에도 바늘 모양이 남는다.
    static Texture2D needleTex;

    // leafInst 같은 잎 재질을 바늘잎 컷아웃으로 바꾼다. 색(어두운 밤 톤)은 재질 색을 그대로 쓴다.
    public static void ConfigureLeafMaterial(Material m)
    {
        if (m == null) return;
        m.mainTexture = NeedleTexture();
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f); // Standard: Cutout
        m.SetOverrideTag("RenderType", "TransparentCutout");
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", 1);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", 0);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.42f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 2450;
        // 바늘 텍스처 평균 밝기(약 0.26)만큼 어두워지므로 색을 올려 기존 밤 톤(0.03,0.09,0.05)을 유지한다.
        var c = m.color; m.color = new Color(Mathf.Min(1f, c.r * 3f), Mathf.Min(1f, c.g * 3f), Mathf.Min(1f, c.b * 3f), 1f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
    }

    public static void ReleaseTexture()
    {
        if (needleTex != null) Object.Destroy(needleTex);
        needleTex = null;
    }

    static void Stamp(Color32[] px, int W, int H, float x, float y, byte g, float thick)
    {
        int r = thick > 1.2f ? 1 : 0;
        for (int dy = 0; dy <= r; dy++)
        for (int dx = 0; dx <= r; dx++)
        {
            int ix = Mathf.RoundToInt(x) + dx, iy = Mathf.RoundToInt(y) + dy;
            if (ix < 0 || iy < 0 || ix >= W || iy >= H) continue;
            px[iy * W + ix] = new Color32(g, g, g, 255);
        }
    }

    static void Line(Color32[] px, int W, int H, float x0, float y0, float x1, float y1, byte g0, byte g1, float thick)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 1.5f));
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Stamp(px, W, H, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), (byte)Mathf.Lerp(g0, g1, t), thick);
        }
    }

    // 256x256, u 가로 / v 세로(아래=줄기쪽, 위=끝). 줄기는 u=0.55. 왼쪽 가장자리 띠는 속 원뿔용 꽉 찬 어두운 면.
    static Texture2D NeedleTexture()
    {
        if (needleTex != null) return needleTex;
        const int W = 256, H = 256;
        var px = new Color32[W * H]; // 기본 alpha 0
        var rand = new System.Random(77);
        // 속 원뿔용 꽉 찬 띠
        for (int y = 0; y < H; y++)
            for (int x = 0; x < 20; x++) px[y * W + x] = new Color32(70, 70, 70, 255);

        float cx = 0.55f * W;
        Line(px, W, H, cx, 2, cx, H - 3, 90, 120, 2f); // 중심 줄기
        for (float y = 8; y < H - 10; y += 5.5f)
        {
            float t = y / H;
            float reach = (0.44f * W) * Mathf.Pow(1f - t, 0.55f) + 8f; // 끝으로 갈수록 짧은 잔가지
            for (int side = -1; side <= 1; side += 2)
            {
                float len = reach * (0.75f + (float)rand.NextDouble() * 0.3f);
                float ang = (float)(0.75 + rand.NextDouble() * 0.25); // 잔가지가 끝쪽으로 휘는 정도
                float ex = cx + side * len, ey = y + len * ang * 0.55f - 4f;
                Line(px, W, H, cx, y, ex, ey, 100, 150, 1.3f);
                // 바늘: 잔가지를 따라 양쪽으로 촘촘히
                for (float k = 0.08f; k < 1f; k += 0.065f)
                {
                    float bx = Mathf.Lerp(cx, ex, k), by = Mathf.Lerp(y, ey, k);
                    float nl = 7f + 8f * (1f - k) + (float)rand.NextDouble() * 4f;
                    byte gA = (byte)(150 + rand.Next(90));
                    byte gB = (byte)Mathf.Min(255, gA + 20);
                    // 끝쪽(위)으로 비스듬히 뻗는 바늘 한 쌍
                    Line(px, W, H, bx, by, bx + side * nl * 0.35f, by + nl * 0.95f, gA, gB, 1.0f);
                    Line(px, W, H, bx, by, bx - side * nl * 0.2f, by + nl * 0.85f, gA, gB, 1.0f);
                    Line(px, W, H, bx, by, bx + side * nl * 0.75f, by + nl * 0.5f, gA, gB, 1.0f);
                }
            }
        }
        // 맨 끝 순
        for (int i = 0; i < 9; i++)
        {
            float a = (-1f + i * 0.25f);
            Line(px, W, H, cx, H - 40, cx + a * 22f, H - 4f - Mathf.Abs(a) * 8f, 160, 230, 1.0f);
        }
        needleTex = new Texture2D(W, H, TextureFormat.RGBA32, true);
        needleTex.name = "Needle Branch (procedural)";
        needleTex.wrapMode = TextureWrapMode.Clamp;
        needleTex.filterMode = FilterMode.Trilinear;
        needleTex.anisoLevel = 4;
        needleTex.SetPixels32(px);
        needleTex.Apply(true);
        return needleTex;
    }

    static void AddCard(List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris,
        Vector3 a, Vector3 b, float hw, Vector3 side, bool flip)
    {
        int i0 = verts.Count;
        var p = new[] { a - side * hw, a + side * hw, b - side * hw, b + side * hw };
        float u0 = flip ? 1f : 0.1f, u1 = flip ? 0.1f : 1f;
        // 줄기(u=0.55)가 카드 가운데에 오도록 u 범위를 잡는다.
        var uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f) };
        for (int k = 0; k < 4; k++)
        {
            verts.Add(p[k]);
            uvs.Add(uv[k]);
            var n = new Vector3(p[k].x, 0f, p[k].z);
            norms.Add((n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.up) + Vector3.up * 0.7f);
        }
        // 양면 (Standard 는 뒷면을 그리지 않으므로 반대 감기 삼각형을 더한다)
        tris.AddRange(new[] { i0, i0 + 2, i0 + 3, i0, i0 + 3, i0 + 1 });
        tris.AddRange(new[] { i0, i0 + 3, i0 + 2, i0, i0 + 1, i0 + 3 });
    }

    static Mesh BuildFoliage(System.Random r)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        // 1) 속 원뿔 (바늘 카드 사이로 하늘/기둥이 비치지 않게 어둡게 채운다)
        float[] ys = { 2.8f, 4.1f, 5.3f, 6.4f, 7.4f };
        float[] rads = { 1.05f, 0.88f, 0.70f, 0.52f, 0.32f };
        float[] hts = { 2.4f, 2.3f, 2.2f, 2.1f, 2.3f };
        const int seg = 8;
        for (int c = 0; c < ys.Length; c++)
        {
            int ringStart = verts.Count;
            for (int s = 0; s < seg; s++)
            {
                float a = Mathf.PI * 2f * s / seg;
                float rad = rads[c] * (1f + ((float)r.NextDouble() * 2f - 1f) * 0.15f);
                var v = new Vector3(Mathf.Cos(a) * rad, ys[c], Mathf.Sin(a) * rad);
                verts.Add(v); uvs.Add(new Vector2(0.03f, 0.5f));
                norms.Add(new Vector3(v.x, 0f, v.z).normalized + Vector3.up * 0.7f);
            }
            int apex = verts.Count;
            verts.Add(new Vector3(0f, ys[c] + hts[c], 0f)); uvs.Add(new Vector2(0.03f, 0.5f)); norms.Add(Vector3.up);
            for (int s = 0; s < seg; s++)
                tris.AddRange(new[] { apex, ringStart + s, ringStart + (s + 1) % seg });
        }

        // 2) 바늘잎 가지: 층마다 돌려나기, 아래는 길고 처지고 위는 짧고 들린다.
        const int whorls = 17;
        float az0 = (float)(r.NextDouble() * 6.28);
        for (int w = 0; w < whorls; w++)
        {
            float t = w / (float)(whorls - 1);
            float y0 = Mathf.Lerp(2.2f, 8.8f, t) + ((float)r.NextDouble() - 0.5f) * 0.25f;
            int n = Mathf.RoundToInt(Mathf.Lerp(7.4f, 3f, t));
            float lmax = Mathf.Lerp(2.55f, 0.55f, Mathf.Pow(t, 0.9f));
            for (int i = 0; i < n; i++)
            {
                float az = az0 + w * 2.399f + i * Mathf.PI * 2f / n + ((float)r.NextDouble() - 0.5f) * 0.5f;
                float L = lmax * (0.8f + (float)r.NextDouble() * 0.35f);
                float pitch = Mathf.Lerp(-0.45f, 0.5f, t) + ((float)r.NextDouble() - 0.5f) * 0.25f;
                var d = new Vector3(Mathf.Cos(az) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Sin(az) * Mathf.Cos(pitch));
                var start = new Vector3(0f, y0, 0f) + d * 0.1f;
                var tip = start + d * L + Vector3.down * 0.12f * L; // 끝이 살짝 처진다
                var side = Vector3.Cross(d, Vector3.up).normalized;
                float hw = L * (0.42f + (float)r.NextDouble() * 0.12f);
                AddCard(verts, uvs, norms, tris, start, tip, hw, side, r.Next(2) == 0);
                // 같은 가지를 d축으로 ~65도 돌린 두 번째 카드: 위/아래에서도 입체감이 나게 한다.
                var side2 = (side * Mathf.Cos(1.1f) + Vector3.up * Mathf.Sin(1.1f)).normalized;
                AddCard(verts, uvs, norms, tris, start, tip, hw * 0.9f, side2, r.Next(2) == 0);
            }
        }
        // 3) 꼭대기 순: 세 장이 교차
        for (int i = 0; i < 3; i++)
        {
            float az = i * Mathf.PI / 3f;
            var side = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
            AddCard(verts, uvs, norms, tris, new Vector3(0f, 7.6f, 0f), new Vector3(0f, 9.6f, 0f), 0.5f, side, false);
        }

        var mesh = new Mesh { name = "Pine Foliage (needle cards)" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        for (int i = 0; i < norms.Count; i++) norms[i] = norms[i].normalized;
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
