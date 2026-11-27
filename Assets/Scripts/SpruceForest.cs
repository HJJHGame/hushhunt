using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 숲의 모든 나무를 tevy 가문비 모델(young-spruce 85%, dead-spruce 15%)로 채운다.
// 모델 위치: Assets/Models/<이름>/lod0.glb, lod1.glb, lod2.glb (glTFast 패키지 필요).
// 에디터에서는 AssetDatabase로, 빌드에서는 Resources/Models/<이름>/lod0 (확장자 없이)로 찾는다.
// 모델이 하나도 없으면 아무것도 하지 않고 기존 나무(TreeModels)가 그대로 나온다.
// 나무마다 LODGroup: LOD0 가까이 / LOD1 중간 / LOD2 멀리(그림자 끔) / 그 밖은 안 그림.
// 용지(TreePaper)는 dead-spruce에 붙는다 ("TreeModel Spruce Dead" 이름을 찾는다).
[DefaultExecutionOrder(-60)] // TreeModels(-50)보다 먼저: 성공하면 TreeModels는 건너뛴다
public class SpruceForest : MonoBehaviour
{
    public static bool Active;

    const float DeadChance = 0.15f;
    const float ExtraPerSlot = 1.5f;     // 기존 소나무 자리 하나당 추가로 심는 나무 수 (1.5면 전체 약 2.5배)
    const float ExtraMinDist = 2.5f;     // 기존 자리에서 떨어뜨리는 거리(m)
    const float ExtraMaxDist = 9f;
    const float HouseMargin = 1.5f;      // 추가 나무는 집 발자국 + 이 여유(m) 안에는 안 심는다
    const float SpawnClearance = 6f;     // 플레이어 스폰 주변 비움(m)
    const float Lod0Height = 0.35f; // 화면 높이 비율. 키 10m 기준 대략 24m 안
    const float Lod1Height = 0.22f; // 대략 39m 안
    const float CullHeight = 0.01f;

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
        Debug.Log("[HushHunt] SpruceForest: 모델 로드 young=" + (young != null) + " dead=" + (dead != null));

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
        int deadIndex = rand.Next(pines.Count); // 용지용 dead-spruce는 최소 한 그루 보장
        int placed = 0, deadCount = 0;
        for (int i = 0; i < pines.Count; i++)
        {
            Variant v;
            if (young == null) v = dead;
            else if (dead == null) v = young;
            else v = (i == deadIndex || rand.NextDouble() < DeadChance) ? dead : young;
            if (BuildTree(pines[i], v, rand))
            {
                placed++;
                if (v == dead) deadCount++;
            }
        }
        int slotCount = placed;
        int extra = 0;
        if (placed > 0 && ExtraPerSlot > 0f)
        {
            // 배치 순서: 집 -> 나무 -> 바위/소품. 집 발자국, 스폰, 이미 놓인 소품 자리를 피해 빈 곳에만 심는다.
            // (SimplifiedTestMode로 집/소품이 숨겨져 있어도 위치는 그대로 있으므로 같은 기준으로 계산한다.)
            var houses = FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None);
            var fpc = FindFirstObjectByType<FirstPersonController>();
            var spawnPos = fpc != null ? fpc.transform.position : Vector3.zero;
            var props = new List<Vector3>(); // x, z, 반경
            foreach (var pt in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (pt.parent != null) continue;
                string pn = pt.name;
                float pr = pn == WorldConfig.Names.Rock ? 2f
                    : pn == WorldConfig.Names.FallenLog ? 3.5f
                    : pn == WorldConfig.Names.Stump ? 1.5f
                    : pn == WorldConfig.Names.OldFence ? 4f
                    : 0f;
                if (pr > 0f) props.Add(new Vector3(pt.position.x, pt.position.z, pr));
            }
            var extraRoot = new GameObject("Spruce Extras").transform;
            float lim = WorldConfig.Half - 3f;
            foreach (var slot in pines)
            {
                int n = (int)ExtraPerSlot + (rand.NextDouble() < ExtraPerSlot - (int)ExtraPerSlot ? 1 : 0);
                for (int k = 0; k < n; k++)
                {
                    float ang = (float)rand.NextDouble() * Mathf.PI * 2f;
                    float dist = ExtraMinDist + (float)rand.NextDouble() * (ExtraMaxDist - ExtraMinDist);
                    var pos = slot.position + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    if (Mathf.Abs(pos.x) > lim || Mathf.Abs(pos.z) > lim) continue;
                    bool blocked = new Vector2(pos.x - spawnPos.x, pos.z - spawnPos.z).magnitude < SpawnClearance || Zones.Contains(pos.x, pos.z, 1f);
                    if (!blocked)
                        foreach (var h in houses)
                        {
                            var hp = h.transform.position;
                            if (Mathf.Abs(pos.x - hp.x) < h.size.x * 0.5f + HouseMargin
                                && Mathf.Abs(pos.z - hp.z) < h.size.y * 0.5f + HouseMargin) { blocked = true; break; }
                        }
                    if (!blocked)
                        foreach (var pp in props)
                            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pp.x, pp.y)) < pp.z + 1.5f) { blocked = true; break; }
                    if (blocked) continue;
                    var go = new GameObject("Spruce Extra");
                    go.transform.SetParent(extraRoot, false);
                    go.transform.position = pos;
                    Variant ev = (dead == null) ? young : (young == null ? dead : (rand.NextDouble() < DeadChance ? dead : young));
                    if (BuildTree(go.transform, ev, rand))
                    {
                        // 줄기 충돌체 (기존 소나무 줄기와 비슷하게)
                        var cap = go.AddComponent<CapsuleCollider>();
                        cap.radius = 0.25f;
                        cap.height = WorldConfig.PineHeight * 0.6f;
                        cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
                        extra++;
                        if (ev == dead) deadCount++;
                    }
                    else Destroy(go);
                }
            }
        }
        placed += extra;
        Active = placed > 0;
        if (Active)
            Debug.Log("[HushHunt] SpruceForest: 가문비 " + placed + "그루 배치 (young " + (placed - deadCount) + ", dead " + deadCount + ") / 기존 슬롯 " + slotCount + " + 추가 " + extra);
        else
            Debug.LogWarning("[HushHunt] SpruceForest: 모델은 읽었지만 배치 0그루 (렌더러 없음/크기 0) -> 옛 숲 유지");
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

        float target = WorldConfig.PineHeight * oldScale * (0.85f + (float)rand.NextDouble() * 0.3f);
        float girth = 0.92f + (float)rand.NextDouble() * 0.16f;
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
