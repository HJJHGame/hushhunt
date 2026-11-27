using System.Collections.Generic;
using UnityEngine;

// 모든 벽에 질감을 입힌다 (이미지 파일 없이 코드로 만든다).
// - 집 벽: 이미 있는 판자 질감을 그대로 쓰되, 큐브 하나가 질감 한 장으로 늘어나던 것을 벽 크기에 맞춰 이어 붙인다.
// - 맵 가장자리 벽(4면)과 봉쇄 구역 돌벽: 돌블록 질감을 깐다.
// 질감 크기는 월드 미터 기준이라 벽이 길어도 늘어나지 않는다 (면마다 UV를 크기에 맞춰 다시 계산).
// 실사 사진이 있으면 Resources/Textures/WallStone(.jpg/.png)을 대신 쓴다.
public class WallTextures : MonoBehaviour
{
    const float HouseTile = 2f;   // 집 벽 질감 한 장이 덮는 길이(m)
    const float StoneTile = 3f;   // 돌벽 질감 한 장이 덮는 길이(m)

    readonly List<Object> owned = new List<Object>();
    Material stoneMat;

    void Start()
    {
        var visualRoot = new GameObject("Wall Visuals").transform;
        visualRoot.SetParent(transform, false);

        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
            string on = r.gameObject.name;
            string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
            if (on == WorldConfig.Names.EdgeWall)
            {
                // 가장자리 벽은 정적 배칭이라 메시를 못 바꾼다: 원래 렌더러는 끄고 같은 자리에 질감 입힌 겉모습을 놓는다.
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(vis.GetComponent<Collider>());
                vis.name = "Edge Wall Visual";
                vis.transform.SetParent(visualRoot, false);
                vis.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                vis.transform.localScale = r.transform.lossyScale;
                var vf = vis.GetComponent<MeshFilter>();
                vf.sharedMesh = WorldUvCube(vf.sharedMesh, r.transform.lossyScale, StoneTile);
                vis.GetComponent<Renderer>().sharedMaterial = StoneMaterial();
                r.enabled = false;
            }
            else if (on == "Zone Wall")
            {
                mf.sharedMesh = WorldUvCube(mf.sharedMesh, r.transform.lossyScale, StoneTile);
                r.sharedMaterial = StoneMaterial();
            }
            else if (mn.StartsWith("House") && !r.isPartOfStaticBatch)
            {
                mf.sharedMesh = WorldUvCube(mf.sharedMesh, r.transform.lossyScale, HouseTile);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var o in owned) if (o != null) Destroy(o);
        owned.Clear();
    }

    // 큐브 복사본: 각 면의 UV를 (면의 가로, 세로 길이(m)) / tile 로 다시 계산한다.
    Mesh WorldUvCube(Mesh src, Vector3 s, float tile)
    {
        var m = Instantiate(src);
        m.name = "Cube (world UV)";
        owned.Add(m);
        var v = m.vertices;
        var n = m.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var a = Vector3.Scale(v[i], s);
            float ax = Mathf.Abs(n[i].x), ay = Mathf.Abs(n[i].y), az = Mathf.Abs(n[i].z);
            Vector2 p;
            if (ax >= ay && ax >= az) p = new Vector2(a.z, a.y);      // 좌우 면: 가로=z, 세로=y
            else if (az >= ay) p = new Vector2(a.x, a.y);              // 앞뒤 면: 가로=x, 세로=y
            else p = new Vector2(a.x, a.z);                            // 위아래 면
            uv[i] = p / tile;
        }
        m.uv = uv;
        return m;
    }

    Material StoneMaterial()
    {
        if (stoneMat != null) return stoneMat;
        var photo = Resources.Load<Texture2D>("Textures/WallStone");
        var tex = photo != null ? photo : BuildStone();
        if (photo == null) owned.Add(tex);
        stoneMat = RuntimeMaterials.CreateLit("Wall Stone", photo != null ? Color.white : new Color(0.55f, 0.56f, 0.55f), 0.05f);
        stoneMat.mainTexture = tex;
        if (stoneMat.HasProperty("_Metallic")) stoneMat.SetFloat("_Metallic", 0f);
        owned.Add(stoneMat);
        return stoneMat;
    }

    // 가장자리가 이어지는(타일링) 노이즈.
    static float TileN(float u, float v, float fx, float fy, float seed)
    {
        float a = Mathf.PerlinNoise(seed + u * fx, seed + v * fy);
        float b = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + v * fy);
        float c = Mathf.PerlinNoise(seed + u * fx, seed + (v - 1f) * fy);
        float d = Mathf.PerlinNoise(seed + (u - 1f) * fx, seed + (v - 1f) * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // 돌블록 벽: 4줄, 줄마다 반 칸씩 어긋나고, 블록마다 밝기가 다르며 이음새와 모서리가 어둡다.
    static Texture2D BuildStone()
    {
        const int W = 256, H = 256, Rows = 4, BlockW = 128, Mortar = 3;
        int rowH = H / Rows;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        var rand = new System.Random(61);
        var shade = new float[Rows, 3];
        for (int r = 0; r < Rows; r++) for (int b = 0; b < 3; b++) shade[r, b] = 0.7f + (float)rand.NextDouble() * 0.3f;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int row = y / rowH;
                int xs = (x + (row % 2) * (BlockW / 2)) % W;
                int block = xs / BlockW;
                int xo = xs % BlockW, yo = y % rowH;
                float u = x / (float)W, v = y / (float)H;
                float dEdge = Mathf.Min(Mathf.Min(xo, BlockW - 1 - xo), Mathf.Min(yo, rowH - 1 - yo));
                float edge = Mathf.Clamp01(dEdge / 10f);
                float noise = TileN(u, v, 10f, 10f, 3f) * 0.5f + TileN(u, v, 40f, 40f, 17f) * 0.3f;
                float s = shade[row, block] * (0.55f + noise) * (0.78f + 0.22f * edge);
                if (xo < Mortar || yo < Mortar) s = 0.18f; // 이음새
                s = Mathf.Clamp01(s);
                px[y * W + x] = new Color(s, s, s);
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
