using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 숲의 모든 나무를 tevy 모델 4종(dead-spruce, young-spruce, spooky-tree, ginkgo-tree)으로 채운다.
// 모델 위치: Assets/Models/<이름>/lod0.glb, lod1.glb, lod2.glb (glTFast 패키지 필요).
// 에디터에서는 AssetDatabase로, 빌드에서는 Resources/Models/<이름>/lod0 (확장자 없이)로 찾는다.
// 모델이 하나도 없으면 아무것도 하지 않고 기존 나무(TreeModels)가 그대로 나온다.
// 나무마다 LODGroup: LOD0 가까이 / LOD1 중간 / LOD2 멀리(그림자 끔) / 그 밖은 안 그림.
// 용지(TreePaper)는 노란 은행나무 80그루 중 10그루에 붙는다 (PaperLabel). 은행나무 모델이 없으면 어린 가문비에 붙는다.
[DefaultExecutionOrder(-60)] // TreeModels(-50)보다 먼저: 성공하면 TreeModels는 건너뛴다
public class SpruceForest : MonoBehaviour
{
    public static bool Active;

    // 나무 총 수는 TreeLayout.Total(1800). 종류별 개수는 정확히 맞춘다:
    // 죽은 가문비 750, 으스스한 죽은 나무(spooky-tree) 150, 은행나무 정확히 80, 나머지 820은 가문비(young). 없는 모델의 몫은 가문비가 채운다.
    const int DeadCount = 750;
    const int SpookyCount = 150;
    const int GinkgoCount = 80;
    // 종이를 붙일 나무의 모델 이름(은행나무, 없으면 가문비). TreePaper가 읽는다.
    public static string PaperLabel;
    const float HouseMargin = 1.5f;      // 추가 나무는 집 발자국 + 이 여유(m) 안에는 안 심는다
    const float SpawnClearance = 6f;     // 플레이어 스폰 주변 비움(m)
    // 모든 나무의 최종 키(m)는 이 범위 안에서 무작위. 모델마다 실제 키(렌더러 경계)를 재서 정확히 맞춘다.
    const float MinTreeHeight = 12f;
    const float MaxTreeHeight = 15f;
    const float Lod0Height = 0.26f; // 화면 높이 비율. 키 13.5m 기준 대략 24m 안 (예전 10m 나무와 같은 거리)
    const float Lod1Height = 0.165f; // 대략 39m 안
    const float CullHeight = 0.0075f;

    const string SpookyName = "spooky-tree"; // tevy Spooky Dead Tree (CC0). 원본 7m 높이, 폭 9.6m. Assets/Models/spooky-tree/lod0.glb, lod1.glb, lod2.glb
    const string GinkgoName = "ginkgo-tree";   // tevy Ginkgo Tree (CC0). 가로수 격자/흙은 뺀 파일. Assets/Models/ginkgo-tree/lod0.glb, lod1.glb, lod2.glb
    const float GinkgoHeightMul = 1.3f;        // 원본 13.6m. 노란 눈에 띄는 표지판 역할이라 가문비 기준 키의 1.3배(약 12m) // (지금은 안 씀)
    const string YoungName = "young-spruce";
    const string DeadName = "dead-spruce";

    static GameObject LoadModel(string name, int lod)
    {
#if UNITY_EDITOR
        string path = "Assets/Models/" + name + "/lod" + lod + ".glb";
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null) return asset;
        // 메인 에셋이 GameObject가 아니면 하위 에셋 중에서 GameObject를 찾는다.
        var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        GameObject fallback = null;
        foreach (var o in all)
        {
            var go = o as GameObject;
            if (go == null) continue;
            if (go.transform.parent == null) return go;
            if (fallback == null) fallback = go;
        }
        if (fallback != null) return fallback.transform.root.gameObject;
        // 경로가 달라도 이름으로 찾아본다.
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("lod" + lod + " t:GameObject", new[] { "Assets" }))
        {
            string found = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!found.Contains("/" + name + "/")) continue;
            var g = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(found);
            if (g != null) return g;
        }
        if (lod == 0) LogWhyMissing(path, all);
#endif
        return Resources.Load<GameObject>("Models/" + name + "/lod" + lod);
    }

#if UNITY_EDITOR
    // 못 찾았을 때 원인을 콘솔에 남긴다 (파일 유무, 임포터 종류, 하위 에셋 종류).
    static void LogWhyMissing(string path, Object[] all)
    {
        bool exists = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), path));
        var imp = UnityEditor.AssetImporter.GetAtPath(path);
        var main = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
        var types = new System.Text.StringBuilder();
        foreach (var o in all) types.Append(o == null ? "null" : o.GetType().Name).Append(' ');
        Debug.LogWarning("[HushHunt] SpruceForest 진단 " + path + ": 파일존재=" + exists
            + ", 임포터=" + (imp == null ? "없음" : imp.GetType().Name)
            + ", 메인에셋=" + (main == null ? "null" : main.GetType().Name)
            + ", 하위에셋=" + all.Length + " [" + types + "]"
            + " (임포터가 DefaultImporter/없음이면 .glb.meta를 지우고 Reimport 필요)");
    }
#endif

    class Variant
    {
        public string label;
        public float heightMul = 1f;
        public int tris; // lod0 삼각형 수 (무게 비교용)
        public GameObject[] lods = new GameObject[3];
    }

    static Variant LoadVariant(string name, string label)
    {
        var v = new Variant { label = label };
        v.lods[0] = LoadModel(name, 0);
        if (v.lods[0] == null) return null;
        v.lods[1] = LoadModel(name, 1);
        v.lods[2] = LoadModel(name, 2);
        return v;
    }

    void Start()
    {
        Active = false;
        var young = LoadVariant(YoungName, "Young");
        var dead = LoadVariant(DeadName, "Dead");
        if (young == null && dead == null)
        {
            // 모델 없음: 기존 나무 유지
            Debug.LogWarning("[HushHunt] SpruceForest: 모델 파일을 못 찾음 -> 옛 숲 유지. 필요한 경로: Assets/Models/young-spruce/lod0.glb, Assets/Models/dead-spruce/lod0.glb (glTFast 임포트 확인)");
            return;
        }
        var spooky = LoadVariant(SpookyName, "Spooky");
        if (spooky == null) Debug.LogWarning("[HushHunt] SpruceForest: spooky-tree 모델 없음(Assets/Models/" + SpookyName + "/lod0.glb) -> 가문비로 채움");
        var ginkgo = LoadVariant(GinkgoName, "Ginkgo");
        if (ginkgo != null) ginkgo.heightMul = GinkgoHeightMul;
        else Debug.LogWarning("[HushHunt] SpruceForest: 은행나무 모델 없음(Assets/Models/" + GinkgoName + "/lod0.glb) -> 용지는 어린 가문비에");
        PaperLabel = ginkgo != null ? ginkgo.label : (young != null ? young.label : (dead != null ? dead.label : null));
        Debug.Log("[HushHunt] SpruceForest: 모델 로드 young=" + (young != null) + " dead=" + (dead != null) + " spooky=" + (spooky != null)
            + " ginkgo=" + (ginkgo != null) + " / 종이는 " + PaperLabel + " 나무에");

        var pines = new List<Transform>();
        foreach (var cand in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (cand.parent == null && cand.name == WorldConfig.Names.PineTree) pines.Add(cand);
        if (pines.Count == 0)
        {
            Debug.LogWarning("[HushHunt] SpruceForest: 교체할 'Pine Tree' 오브젝트가 씬에 없음");
            return;
        }
        // 검색 순서가 실행마다 달라도 같은 숲이 나오도록 위치로 정렬한다.
        pines.Sort((p1, p2) =>
        {
            int cmp = p1.position.x.CompareTo(p2.position.x);
            return cmp != 0 ? cmp : p1.position.z.CompareTo(p2.position.z);
        });

        var rand = new System.Random();
        // 슬롯 밖 나머지 나무 위치: RandomizeLayout이 이미 정해 뒀으면 그걸 쓰고, 없으면 여기서 직접 만든다.
        var extraPos = TreeLayout.Extra;
        TreeLayout.Extra = null;
        if (extraPos == null)
        {
            var houses = FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None);
            var fpc = FindFirstObjectByType<FirstPersonController>();
            var spawnPos = fpc != null ? fpc.transform.position : Vector3.zero;
            bool Blocked(float x, float z)
            {
                if (new Vector2(x - spawnPos.x, z - spawnPos.z).magnitude < SpawnClearance || Zones.Contains(x, z, 1f)) return true;
                foreach (var h in houses)
                {
                    var hp = h.transform.position;
                    if (Mathf.Abs(x - hp.x) < h.size.x * 0.5f + HouseMargin && Mathf.Abs(z - hp.z) < h.size.y * 0.5f + HouseMargin) return true;
                }
                return false;
            }
            var seeds = new List<Vector2>();
            foreach (var pt in pines) seeds.Add(new Vector2(pt.position.x, pt.position.z));
            extraPos = TreeLayout.Generate(Mathf.Max(0, TreeLayout.Total - pines.Count), Blocked, seeds, rand);
        }

        // 종류별 정확한 개수만큼 덱을 만들어 섞은 뒤 나무마다 한 장씩 뽑는다.
        int total = pines.Count + extraPos.Count;
        var deck = new List<Variant>(total);
        int rem = total;
        void AddCards(Variant vv, int q) { if (vv == null) return; for (int i = 0; i < q; i++) deck.Add(vv); rem -= q; }
        AddCards(dead, dead == null ? 0 : Mathf.Min(DeadCount, rem));
        AddCards(ginkgo, ginkgo == null ? 0 : Mathf.Min(GinkgoCount, rem)); // 은행나무는 항상 정확히 80
        AddCards(spooky, spooky == null ? 0 : Mathf.Min(SpookyCount, rem));
        AddCards(young ?? dead ?? spooky ?? ginkgo, rem); // 나머지는 가문비(없으면 다른 모델)
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            var tmp = deck[i]; deck[i] = deck[j]; deck[j] = tmp;
        }

        var counts = new Dictionary<string, int>();
        int placed = 0, di = 0;
        for (int i = 0; i < pines.Count; i++)
        {
            var v = deck[di++];
            if (BuildTree(pines[i], v, rand))
            {
                placed++;
                counts[v.label] = (counts.TryGetValue(v.label, out var n0) ? n0 : 0) + 1;
            }
        }
        int slotCount = placed;
        int extra = 0;
        var extraRoot = new GameObject("Spruce Extras").transform;
        foreach (var pos in extraPos)
        {
            var v = deck[di++];
            var go = new GameObject("Spruce Extra");
            go.transform.SetParent(extraRoot, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.y);
            if (BuildTree(go.transform, v, rand))
            {
                // 줄기 충돌체 (기존 소나무 줄기와 비슷하게)
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = 0.3f;
                cap.height = MinTreeHeight * 0.5f;
                cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
                extra++;
                counts[v.label] = (counts.TryGetValue(v.label, out var n1) ? n1 : 0) + 1;
            }
            else Destroy(go);
        }
        placed += extra;
        Active = placed > 0;
        if (Active)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
            Debug.Log("[HushHunt] SpruceForest: 나무 총 " + placed + "그루 배치 (" + sb + "목표 " + TreeLayout.Total + ", 정확히 맞음=" + (placed == TreeLayout.Total) + ") / 기존 슬롯 " + slotCount + " + 추가 " + extra);
        }
        else
            Debug.LogWarning("[HushHunt] SpruceForest: 모델은 읽었지만 배치 0그루 (렌더러 없음/크기 0) -> 옛 숲 유지");
    }

    // lod0 모델의 삼각형 수 (무게 비교용).
    static int CountTris(GameObject go)
    {
        int n = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            for (int i = 0; i < m.subMeshCount; i++) n += (int)(m.GetIndexCount(i) / 3);
        }
        return n;
    }

    bool BuildTree(Transform tree, Variant v, System.Random rand)
    {
        // 기존 소나무 모양은 숨긴다 (줄기 충돌체는 남긴다).
        float oldScale = 1f;
        var trunk = tree.Find(WorldConfig.Names.PineTrunk);
        if (trunk != null)
        {
            var tr = trunk.GetComponent<Renderer>();
            if (tr != null) tr.enabled = false;
            oldScale = trunk.localScale.x / WorldConfig.PineTrunkBaseWidth;
        }
        for (int i = 1; i <= 3; i++)
        {
            var f = tree.Find(WorldConfig.Names.PineFoliagePrefix + i);
            if (f != null) f.gameObject.SetActive(false);
        }

        float target = Mathf.Lerp(MinTreeHeight, MaxTreeHeight, (float)rand.NextDouble()); // 12~15m, 모델 종류와 무관
        float girth = 1f; // 비율 그대로: 높이만 12~15m로 맞추고 가로는 같은 배율
        var rot = Quaternion.Euler((float)(rand.NextDouble() * 4.0 - 2.0), (float)rand.NextDouble() * 360f,
            (float)(rand.NextDouble() * 4.0 - 2.0));

        var model = Instantiate(v.lods[0], tree);
        model.name = "TreeModel Spruce " + v.label; // TreePaper가 "TreeModel"로 시작하는 이름으로 줄기를 찾는다
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        var r0 = model.GetComponentsInChildren<Renderer>();
        if (r0.Length == 0) { Destroy(model); return false; }
        float h0 = Fit(tree, model.transform, r0, target, girth, rot, -1f);
        if (h0 <= 0f) { Destroy(model); return false; }

        var lod0 = new List<Renderer>(r0);
        var lod1 = new List<Renderer>();
        var lod2 = new List<Renderer>();
        for (int level = 1; level <= 2; level++)
        {
            var src = v.lods[level];
            if (src == null) continue;
            var go = Instantiate(src, tree);
            go.name = "SpruceLOD" + level; // "TreeModel"로 시작하지 않게
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0 || Fit(tree, go.transform, rs, target, girth, rot, h0) <= 0f) { Destroy(go); continue; }
            (level == 1 ? lod1 : lod2).AddRange(rs);
        }

        // LOD2가 없으면 LOD1을, LOD1도 없으면 LOD0을 이어서 쓴다.
        if (lod1.Count == 0) lod1.AddRange(lod0);
        bool hasLod2 = lod2.Count > 0;
        if (!hasLod2) lod2.AddRange(lod1);

        foreach (var r in lod0) r.shadowCastingMode = ShadowCastingMode.On;
        foreach (var r in lod1) r.shadowCastingMode = ShadowCastingMode.On;
        if (hasLod2)
            foreach (var r in lod2) r.shadowCastingMode = ShadowCastingMode.Off; // 먼 LOD는 그림자 없음

        var g = tree.gameObject.AddComponent<LODGroup>();
        var lods = new LOD[3];
        lods[0] = new LOD(Lod0Height, lod0.ToArray());
        lods[1] = new LOD(Lod1Height, lod1.ToArray());
        lods[2] = new LOD(CullHeight, lod2.ToArray());
        g.SetLODs(lods);
        g.fadeMode = LODFadeMode.None;
        g.RecalculateBounds();
        return true;
    }

    // 모델을 나무 루트 아래에 놓고 키를 맞춘 뒤 바닥을 땅에 닿게 한다. 맞춘 키(월드 m)를 돌려준다.
    // heightRef가 0 이상이면 LOD0의 키에 맞춘다 (LOD끼리 크기가 같게).
    static float Fit(Transform tree, Transform model, Renderer[] rends, float target, float girth, Quaternion rot, float heightRef)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = rot;
        model.localScale = Vector3.one;
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        if (b.size.y < 0.001f) return -1f;
        float want = heightRef > 0f ? heightRef : target;
        float k = want / b.size.y;
        model.localScale = new Vector3(k * girth, k, k * girth);
        var nb = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
        model.position += Vector3.up * (tree.position.y - nb.min.y);
        return want;
    }
}
