using UnityEngine;

// 통나무/그루터기 개선: Assets/Resources/Logs, Assets/Resources/Stumps 에 모델(.fbx)이
// 있으면 기존 원기둥 소품을 그 모델로 바꾼다. 없으면 아무것도 하지 않는다 (기존 모양 유지).
public class PropModels : MonoBehaviour
{
    void Start()
    {
        SwapAll(WorldConfig.Names.FallenLog, Resources.LoadAll<GameObject>("Logs"));
        SwapAll(WorldConfig.Names.Stump, Resources.LoadAll<GameObject>("Stumps"));
    }

    void SwapAll(string propName, GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        var rand = new System.Random(propName.GetHashCode());
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != propName) continue;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            var go = Instantiate(prefabs[rand.Next(prefabs.Length)], t.position, t.rotation, t.parent);
            go.name = propName;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(go); continue; }

            // 가장 긴 차원을 기존 소품에 맞추고 (통나무 길이, 그루터기 높이), 가장 낮은 점을 맞춘다.
            var b = BoundsOf(rends);
            var ob = mr.bounds;
            float want = Mathf.Max(ob.size.x, Mathf.Max(ob.size.y, ob.size.z));
            float have = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (have > 0.001f) go.transform.localScale *= want / have;
            var nb = BoundsOf(rends);
            go.transform.position += Vector3.up * (ob.min.y - nb.min.y);
            mr.enabled = false; // 기존 메시는 숨기고 콜라이더만 남긴다
        }
    }

    static Bounds BoundsOf(Renderer[] rends)
    {
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
