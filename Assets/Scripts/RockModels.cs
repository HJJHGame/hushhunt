using UnityEngine;
using System.Collections.Generic;

// 바위 개선: 씬의 납작한 구(Sphere) 바위를 울퉁불퉁한 절차적 바위 메시로 교체한다.
// Assets/Resources/Rocks 에 모델(.fbx)을 넣으면 그걸 우선 쓰고, 없으면 코드로 만든다.
// 메시와 콜라이더 크기는 기존 배치(위치/스케일)를 그대로 쓰므로 맵 데이터는 바뀌지 않는다.
public class RockModels : MonoBehaviour
{
    static Mesh[] variants;

    void Start()
    {
        var prefabs = Resources.LoadAll<GameObject>("Rocks");
        var rand = new System.Random(23);

        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != WorldConfig.Names.Rock) continue;
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) continue;

            if (prefabs != null && prefabs.Length > 0)
            {
                // 드롭인 모델: 기존 바위 크기에 맞춰 넣는다.
                var go = Instantiate(prefabs[rand.Next(prefabs.Length)], t.position, t.rotation, t.parent);
                go.name = t.name;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { Destroy(go); continue; }
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                var ob = mr.bounds;
                float want = Mathf.Max(ob.size.x, ob.size.z);
                float have = Mathf.Max(b.size.x, b.size.z);
                if (have > 0.001f) go.transform.localScale *= want / have;
                go.transform.position += Vector3.up * (ob.min.y - b.min.y * (want / have));
                mr.enabled = false; // 기존 메시는 숨기고 콜라이더만 남긴다
            }
            else
            {
                EnsureVariants();
                mf.sharedMesh = variants[rand.Next(variants.Length)];
                t.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);
            }
        }
    }

    static void EnsureVariants()
    {
        if (variants != null) return;
        variants = new Mesh[5];
        for (int i = 0; i < variants.Length; i++) variants[i] = BuildRock(300 + i * 31);
    }

    // 위도/경도 구 격자를 3D 노이즈로 찌그러뜨린 불규칙 바위. 밑면은 납작하게 눌러 땅에 앉힌다.
    static Mesh BuildRock(int seed)
    {
        var r = new System.Random(seed);
        float ox = (float)r.NextDouble() * 100f;
        float oy = (float)r.NextDouble() * 100f;
        float oz = (float)r.NextDouble() * 100f;

        const int lat = 10, lon = 16;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int i = 0; i <= lat; i++)
        {
            float phi = Mathf.PI * i / lat; // 0(북극) ~ pi(남극)
            for (int j = 0; j < lon; j++)
            {
                float theta = Mathf.PI * 2f * j / lon;
                var d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                // 옥타브 2개: 큰 요철 + 잔 요철
                float n = Noise(d.x * 1.4f + ox, d.y * 1.4f + oy, d.z * 1.4f + oz) * 0.7f
                        + Noise(d.x * 3.3f + ox, d.y * 3.3f + oy, d.z * 3.3f + oz) * 0.3f;
                float rad = 0.5f * (0.78f + 0.5f * n);
                var p = d * rad;
                if (p.y < -0.08f) p.y = -0.08f + (p.y + 0.08f) * 0.2f; // 밑면 납작하게
                verts.Add(p);
                uvs.Add(new Vector2(j / (float)lon, i / (float)lat));
            }
        }
        for (int i = 0; i < lat; i++)
        {
            for (int j = 0; j < lon; j++)
            {
                int a0 = i * lon + j, a1 = i * lon + (j + 1) % lon;
                int b0 = a0 + lon, b1 = a1 + lon;
                tris.AddRange(new[] { a0, b0, b1, a0, b1, a1 });
            }
        }

        var mesh = new Mesh { name = "Rock (procedural)" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 3D 값 노이즈 (0~1). 부드러운 삼선형 보간.
    static float Noise(float x, float y, float z)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
        float xf = x - xi, yf = y - yi, zf = z - zi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);
        float w = zf * zf * (3f - 2f * zf);

        float c00 = Mathf.Lerp(Hash(xi, yi, zi), Hash(xi + 1, yi, zi), u);
        float c10 = Mathf.Lerp(Hash(xi, yi + 1, zi), Hash(xi + 1, yi + 1, zi), u);
        float c01 = Mathf.Lerp(Hash(xi, yi, zi + 1), Hash(xi + 1, yi, zi + 1), u);
        float c11 = Mathf.Lerp(Hash(xi, yi + 1, zi + 1), Hash(xi + 1, yi + 1, zi + 1), u);
        return Mathf.Lerp(Mathf.Lerp(c00, c10, v), Mathf.Lerp(c01, c11, v), w);
    }

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177;
            h = (h ^ (h >> 13)) * 1103515245;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
