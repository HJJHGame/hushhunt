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
        variants = new Variant[3];
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

    // 어두운 잎 단층: 아래로 갈수록 넓은 원뿔 5단. 가장자리를 흔들어 실루엣을 거칠게 한다.
    static Mesh BuildFoliage(System.Random r)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        float[] ys = { 2.6f, 3.9f, 5.1f, 6.2f, 7.2f };
        float[] rads = { 2.05f, 1.70f, 1.35f, 1.00f, 0.62f };
        float[] hts = { 2.4f, 2.3f, 2.2f, 2.1f, 2.3f };
        const int seg = 12;
        for (int c = 0; c < ys.Length; c++)
        {
            int ringStart = verts.Count;
            for (int s = 0; s < seg; s++)
            {
                float a = Mathf.PI * 2f * s / seg;
                float wobble = 1f + ((float)r.NextDouble() * 2f - 1f) * 0.22f;
                float rad = rads[c] * wobble;
                float y = ys[c] + ((float)r.NextDouble() * 2f - 1f) * 0.25f;
                verts.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad));
            }
            int apex = verts.Count;
            verts.Add(new Vector3(0f, ys[c] + hts[c], 0f));
            for (int s = 0; s < seg; s++)
                tris.AddRange(new[] { apex, ringStart + s, ringStart + (s + 1) % seg });
        }

        var mesh = new Mesh { name = "Pine Foliage (procedural)" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
