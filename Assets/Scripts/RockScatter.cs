using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 나무가 아닌 소품 모델(PropLayout이 정한 자리)을 실제로 심는다. 이름은 예전 돌 흩뿌리기에서 왔다.
// 모델 위치: Assets/Models/<슬러그>/lod0.glb, lod1.glb, lod2.glb (glTFast 필요). 빌드에서는 Resources/Models/<슬러그>/lod0 (확장자 없이).
// 모델 크기는 원본 그대로(배율 1). 모델이 없는 종류는 건너뛴다. 소품마다 LODGroup(가까이/중간/멀리/안 그림).
// 콜라이더는 걷는 데 걸리는 것에만, 상자 하나로 단다 (값싸다).
[DefaultExecutionOrder(-40)] // RandomizeLayout(Awake)이 자리를 정한 뒤
public class RockScatter : MonoBehaviour
{
    class Loaded
    {
        public GameObject[] lods = new GameObject[3];
    }

    const float Sink = 0.02f;          // 땅에 묻히는 깊이(m)
    const float ColliderShrink = 0.9f; // 콜라이더는 모델보다 약간 작게

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

    void Start()
    {
        var items = PropLayout.Items;
        if (items == null || items.Count == 0) return;

        var models = new Dictionary<string, Loaded>();
        foreach (var spec in PropLayout.All)
        {
            var l = new Loaded();
            l.lods[0] = LoadModel(spec.slug, 0);
            if (l.lods[0] == null)
            {
                Debug.LogWarning("[HushHunt] 소품 모델 없음 Assets/Models/" + spec.slug + "/lod0.glb -> 이 종류는 건너뜀");
                continue;
            }
            l.lods[1] = LoadModel(spec.slug, 1);
            l.lods[2] = LoadModel(spec.slug, 2);
            models[spec.slug] = l;
        }
        if (models.Count == 0) return;

        var parent = new GameObject("Props").transform;
        int made = 0;
        foreach (var it in items)
        {
            Loaded m;
            if (!models.TryGetValue(it.spec.slug, out m)) continue;
            if (Build(it, m, parent)) made++;
        }
        Debug.Log("[HushHunt] RockScatter: 소품 " + made + "/" + items.Count + "개 배치 (모델 " + models.Count + "/" + PropLayout.All.Length + "종)");
    }

    // 가장 단순한 LOD의 메시로 모델 모양 콜라이더를 단다. 메시를 읽을 수 없으면(빌드 설정) false: 상자로 대신한다.
    static bool AddMeshColliders(List<Renderer> rends)
    {
        var filters = new List<MeshFilter>();
        foreach (var r in rends)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (!mf.sharedMesh.isReadable) return false;
            if (!filters.Contains(mf)) filters.Add(mf);
        }
        if (filters.Count == 0) return false;
        foreach (var mf in filters)
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
        return true;
    }

    static bool Build(PropLayout.Item it, Loaded m, Transform parent)
    {
        var spec = it.spec;
        var root = new GameObject("Prop " + spec.slug).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(it.box.cx, 0f, it.box.cz);

        var groups = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        for (int level = 0; level < 3; level++)
        {
            var src = m.lods[level];
            if (src == null) continue;
            var go = Instantiate(src, root);
            go.name = "PropLOD" + level;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Destroy(go); continue; }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one; // 원본 크기
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            // 바닥 중앙을 놓은 자리에 맞춘다: 치수 검사에 쓴 직사각형과 모델이 같은 곳에 오게 한다.
            go.transform.position += new Vector3(it.box.cx - b.center.x, -b.min.y - Sink, it.box.cz - b.center.z);
            groups[level].AddRange(rs);
        }
        if (groups[0].Count == 0) { Destroy(root.gameObject); return false; }
        if (groups[1].Count == 0) groups[1].AddRange(groups[0]);
        bool hasLod2 = groups[2].Count > 0;
        if (!hasLod2) groups[2].AddRange(groups[1]);
        if (hasLod2) foreach (var r in groups[2]) r.shadowCastingMode = ShadowCastingMode.Off; // 먼 LOD는 그림자 없음

        if (spec.collider && !(spec.meshCol && AddMeshColliders(groups[2])))
        {
            var box = root.gameObject.AddComponent<BoxCollider>();
            var sz = new Vector3(spec.w, spec.h, spec.d) * ColliderShrink;
            box.size = sz;
            box.center = new Vector3(0f, sz.y * 0.5f, 0f);
        }

        root.rotation = Quaternion.Euler(0f, it.yaw, 0f);

        // 화면 비율 = 가장 긴 변 / (거리 * 1.15). 소품 종류마다 정한 거리(near/mid/far)에서 전환한다.
        float dim = Mathf.Max(spec.w, Mathf.Max(spec.d, spec.h));
        var g = root.gameObject.AddComponent<LODGroup>();
        var lods = new LOD[3];
        lods[0] = new LOD(Mathf.Clamp(dim / (1.15f * spec.near), 0.002f, 0.9f), groups[0].ToArray());
        lods[1] = new LOD(Mathf.Clamp(dim / (1.15f * spec.mid), 0.001f, 0.8f), groups[1].ToArray());
        lods[2] = new LOD(Mathf.Clamp(dim / (1.15f * spec.far), 0.0005f, 0.7f), groups[2].ToArray());
        g.SetLODs(lods);
        g.fadeMode = LODFadeMode.None;
        g.RecalculateBounds();
        return true;
    }
}
