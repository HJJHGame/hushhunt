using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 화강암 돌 3종과 통나무 더미(tevy, CC0)를 맵 곳곳에 흩뿌린다. 돌마다 LODGroup(가까이 / 중간 / 멀리 / 안 그림)이 붙는다.
// 모델 위치: Assets/Models/<이름>/lod0.glb, lod1.glb, lod2.glb (glTFast 필요). 빌드에서는 Resources/Models/<이름>/lod0 (확장자 없이).
// 모델이 하나도 없으면 아무것도 하지 않는다.
// 피하는 곳: 술래 봉쇄 구역 5곳, 집, 나무, 플레이어 시작 위치, 기존 바위/통나무/그루터기/울타리. 큰 물건(통나무 더미)은 pad만큼 더 넓게 비운다.
[DefaultExecutionOrder(-40)] // RandomizeLayout(Awake)이 끝난 뒤, 나무/집 위치가 정해진 다음
public class RockScatter : MonoBehaviour
{
    // 종류별 폴더 이름, 가장 긴 변 목표 크기(m, 최소~최대), 개수
    class Kind
    {
        public string name; public float minSize, maxSize; public int count;
        public float pad = 0f;                 // 큰 물건이 장애물과 더 떨어지도록 더하는 여유(m)
        public float lod0 = Lod0Height, lod1 = Lod1Height, cull = CullHeight; // 화면 비율. 큰 물건은 더 크게 잡아 같은 거리에서 전환
        public GameObject[] lods = new GameObject[3];
        public Vector3 size0; // 배율 1일 때 lod0 크기(m)
    }

    // 조절값: 개수와 크기. 원본 크기는 0.4~0.9m라 그대로면 숲에서 안 보여서 키워 쓴다.
    static readonly Kind[] Kinds =
    {
        // 통나무 더미: 원본 5.2 x 2.1 x 1.2m. 큰 물건이라 먼저 놓는다. LOD0 약 25m, LOD1 약 55m, 약 100m 밖은 안 그림.
        new Kind { name = "log-pile", minSize = 4.6f, maxSize = 5.6f, count = 25, pad = 2.2f, lod0 = 0.18f, lod1 = 0.08f, cull = 0.04f },
        new Kind { name = "granite-pond-edge-stone-1",   minSize = 1.4f, maxSize = 2.6f, count = 80 },
        new Kind { name = "granite-pond-edge-stone-4",   minSize = 1.4f, maxSize = 2.6f, count = 80 },
        new Kind { name = "granite-river-stone-veined",  minSize = 0.8f, maxSize = 1.6f, count = 80 },
    };

    const float TreeClear = 2.0f;     // 나무와 최소 거리(m)
    const float HouseClear = 2.0f;    // 집 발자국 바깥 여유(m)
    const float PlayerClear = 6f;     // 플레이어 시작 위치 주변 비움(m)
    const float PropClear = 3f;       // 기존 소품과 최소 거리(m)
    const float RockClear = 6f;       // 돌끼리 최소 거리(m)
    const float ZoneMargin = 2f;      // 봉쇄 구역 바깥 여유(m)
    const float Sink = 0.08f;         // 땅에 묻히는 비율(돌 높이 대비)
    const float ColliderShrink = 0.8f; // 콜라이더는 돌보다 약간 작게
    const float Lod0Height = 0.10f;   // 화면 높이 비율. 1m 돌 기준 대략 8m 안
    const float Lod1Height = 0.04f;   // 대략 20m 안
    const float CullHeight = 0.012f;  // 대략 70m 밖은 안 그림

    static GameObject LoadModel(string name, int lod)
    {
#if UNITY_EDITOR
        string path = "Assets/Models/" + name + "/lod" + lod + ".glb";
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null) return asset;
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var go = o as GameObject;
            if (go != null && go.transform.parent == null) return go;
        }
#endif
        return Resources.Load<GameObject>("Models/" + name + "/lod" + lod);
    }

    static bool Measure(GameObject src, out Vector3 size)
    {
        size = Vector3.zero;
        var tmp = Instantiate(src);
        tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        tmp.transform.localScale = Vector3.one;
        var rs = tmp.GetComponentsInChildren<Renderer>();
        bool ok = rs.Length > 0;
        if (ok)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            size = b.size;
        }
        Destroy(tmp);
        return ok && size.y > 0.001f && Mathf.Max(size.x, size.z) > 0.001f;
    }

    void Start()
    {
        var kinds = new List<Kind>();
        foreach (var k in Kinds)
        {
            k.lods[0] = LoadModel(k.name, 0);
            if (k.lods[0] == null) { Debug.LogWarning("[HushHunt] RockScatter: 돌 모델 없음 Assets/Models/" + k.name + "/lod0.glb -> 건너뜀"); continue; }
            k.lods[1] = LoadModel(k.name, 1);
            k.lods[2] = LoadModel(k.name, 2);
            if (!Measure(k.lods[0], out k.size0)) continue;
            kinds.Add(k);
        }
        if (kinds.Count == 0) return;

        // 피할 것들 모으기
        var trees = new PlacementGrid();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name == WorldConfig.Names.PineTree) trees.Add(new Vector2(t.position.x, t.position.z));
        if (TreeLayout.Extra != null) foreach (var p in TreeLayout.Extra) trees.Add(p);

        var houses = new List<Vector4>(); // x, z, 반폭, 반깊이
        foreach (var fp in FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None))
            houses.Add(new Vector4(fp.transform.position.x, fp.transform.position.z, fp.size.x * 0.5f, fp.size.y * 0.5f));

        var props = new PlacementGrid();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.parent != null) continue;
            string n = t.name;
            if (n == WorldConfig.Names.Rock || n == WorldConfig.Names.FallenLog || n == WorldConfig.Names.Stump || n == WorldConfig.Names.OldFence)
                props.Add(new Vector2(t.position.x, t.position.z));
        }

        Vector2 player = new Vector2(9999f, 9999f);
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) player = new Vector2(fpc.transform.position.x, fpc.transform.position.z);

        var placed = new PlacementGrid();
        var rand = new System.Random();
        float lim = WorldConfig.Half - 6f;
        var parent = new GameObject("Stone Scatter").transform;
        int total = 0;

        foreach (var k in kinds)
        {
            int made = 0;
            for (int tries = 0; tries < k.count * 40 && made < k.count; tries++)
            {
                float x = (float)(rand.NextDouble() * 2.0 - 1.0) * lim;
                float z = (float)(rand.NextDouble() * 2.0 - 1.0) * lim;
                var p = new Vector2(x, z);
                if (Zones.Contains(x, z, ZoneMargin + k.pad)) continue;
                if (Vector2.Distance(p, player) < PlayerClear + k.pad) continue;
                bool bad = false;
                foreach (var h in houses)
                    if (Mathf.Abs(x - h.x) < h.z + HouseClear + k.pad && Mathf.Abs(z - h.y) < h.w + HouseClear + k.pad) { bad = true; break; }
                if (bad) continue;
                if (trees.Nearest(p, TreeClear + k.pad) < TreeClear + k.pad) continue;
                if (props.Nearest(p, PropClear + k.pad) < PropClear + k.pad) continue;
                if (placed.Nearest(p, RockClear + k.pad) < RockClear + k.pad) continue;

                float want = Mathf.Lerp(k.minSize, k.maxSize, (float)rand.NextDouble());
                float yaw = (float)rand.NextDouble() * 360f;
                Build(k, parent, x, z, want, yaw);
                placed.Add(p);
                made++;
            }
            total += made;
        }
        Debug.Log("[HushHunt] RockScatter: 돌/통나무 더미 " + total + "개 배치 (모델 " + kinds.Count + "종)");
    }

    static void Build(Kind k, Transform parent, float x, float z, float want, float yaw)
    {
        float s = want / Mathf.Max(k.size0.x, k.size0.z);
        var root = new GameObject("Stone " + k.name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(x, 0f, z);

        var groups = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        for (int level = 0; level < 3; level++)
        {
            var src = k.lods[level];
            if (src == null) continue;
            var go = Instantiate(src, root);
            go.name = "StoneLOD" + level;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Destroy(go); continue; }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * s;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            // 바닥을 땅(y=0)에 닿게 하고 살짝 묻는다
            go.transform.position += Vector3.up * (-b.min.y - Sink * b.size.y);
            groups[level].AddRange(rs);
        }
        if (groups[0].Count == 0) { Destroy(root.gameObject); return; }
        if (groups[1].Count == 0) groups[1].AddRange(groups[0]);
        bool hasLod2 = groups[2].Count > 0;
        if (!hasLod2) groups[2].AddRange(groups[1]);
        if (hasLod2) foreach (var r in groups[2]) r.shadowCastingMode = ShadowCastingMode.Off; // 먼 LOD는 그림자 없음

        // 간단한 상자 콜라이더 (정적, 값싸다)
        var box = root.gameObject.AddComponent<BoxCollider>();
        var sz = k.size0 * s * ColliderShrink;
        box.size = sz;
        box.center = new Vector3(0f, sz.y * 0.5f - Sink * k.size0.y * s, 0f);

        root.rotation = Quaternion.Euler(0f, yaw, 0f);

        var g = root.gameObject.AddComponent<LODGroup>();
        var lods = new LOD[3];
        lods[0] = new LOD(k.lod0, groups[0].ToArray());
        lods[1] = new LOD(k.lod1, groups[1].ToArray());
        lods[2] = new LOD(k.cull, groups[2].ToArray());
        g.SetLODs(lods);
        g.fadeMode = LODFadeMode.None;
        g.RecalculateBounds();
    }
}
