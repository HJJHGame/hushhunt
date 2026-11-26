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
    const float Lod0Height = 0.35f; // 화면 높이 비율. 키 10m 기준 대략 24m 안
    const float Lod1Height = 0.22f; // 대략 39m 안
    const float CullHeight = 0.01f;

    const string YoungName = "young-spruce";
    const string DeadName = "dead-spruce";

    static GameObject LoadModel(string name, int lod)
    {
#if UNITY_EDITOR
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + name + "/lod" + lod + ".glb");
        if (asset != null) return asset;
#endif
        return Resources.Load<GameObject>("Models/" + name + "/lod" + lod);
    }

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
        if (young == null && dead == null) return; // 모델 없음: 기존 나무 유지

        var pines = new List<Transform>();
        foreach (var cand in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (cand.parent == null && cand.name == WorldConfig.Names.PineTree) pines.Add(cand);
        if (pines.Count == 0) return;
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
        Active = placed > 0;
        Debug.Log("[HushHunt] SpruceForest: 나무 " + placed + "그루 (dead " + deadCount + ")");
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
