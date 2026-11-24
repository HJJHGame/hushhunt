using UnityEngine;

// 소나무를 실제 3D 모델(Kenney Nature Kit, CC0)로 교체한다.
// 모델은 Assets/Resources/Trees 에서 불러온다. 씬을 다시 만들지 않아도 시작할 때 적용되고,
// 모델 파일이 없으면 아무것도 바꾸지 않아 기존 나무가 그대로 남는다.
public class TreeModels : MonoBehaviour
{
    void Start()
    {
        var prefabs = Resources.LoadAll<GameObject>("Trees");
        if (prefabs == null || prefabs.Length == 0) return;

        var rand = new System.Random(11);
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var pines = new System.Collections.Generic.List<Transform>();
        foreach (var cand in all) if (cand.name == "Pine Tree") pines.Add(cand);
        // 검색 순서가 실행마다 달라도 같은 숲이 나오도록 위치로 정렬한다.
        pines.Sort((p1, p2) =>
        {
            int cmp = p1.position.x.CompareTo(p2.position.x);
            return cmp != 0 ? cmp : p1.position.z.CompareTo(p2.position.z);
        });
        foreach (var t in pines)
        {

            // 기존 원뿔 나무의 재질(씬의 어두운 색)을 그대로 써서 안개/밤 분위기와 맞춘다.
            Material leafMat = null, barkMat = null;
            float oldScale = 1f;
            var trunk = t.Find("Trunk");
            if (trunk != null)
            {
                var tr = trunk.GetComponent<Renderer>();
                if (tr != null) { barkMat = tr.sharedMaterial; tr.enabled = false; } // 충돌체는 남김
                oldScale = trunk.localScale.x / 0.32f;
            }
            for (int i = 1; i <= 3; i++)
            {
                var f = t.Find("Foliage" + i);
                if (f == null) continue;
                var fr = f.GetComponent<Renderer>();
                if (leafMat == null && fr != null) leafMat = fr.sharedMaterial;
                f.gameObject.SetActive(false);
            }

            var prefab = prefabs[rand.Next(prefabs.Length)];
            var go = Instantiate(prefab, t);
            go.name = "TreeModel";
            go.transform.localPosition = Vector3.zero;
            // 방향은 제각각, 살짝 기울어진 나무로 획일적인 줄맞춤 느낌을 없앤다.
            float tiltX = (float)(rand.NextDouble() * 6.0 - 3.0);
            float tiltZ = (float)(rand.NextDouble() * 6.0 - 3.0);
            go.transform.localRotation = Quaternion.Euler(tiltX, (float)rand.NextDouble() * 360f, tiltZ);
            go.transform.localScale = Vector3.one;

            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            // 모델 단위가 파일마다 달라도 되도록 높이를 재서 기존 나무 높이(약 9.5m x 크기)에 맞춘다.
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(go); continue; }
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.001f)
            {
                float target = 9.5f * oldScale;
                float k = target / b.size.y;
                float girth = 0.9f + (float)rand.NextDouble() * 0.25f; // 굵기 변화
                go.transform.localScale = new Vector3(k * girth, k, k * girth);

                // 모델 원점이 밑바닥이 아니어도 땅에 박히거나 뜨지 않게 가장 낮은 점을 땅에 맞춘다.
                var nb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
                go.transform.position += Vector3.up * (t.position.y - nb.min.y);
            }

            // 재질: 이름에 wood/bark 가 있으면 줄기색, 나머지는 잎색.
            foreach (var r in rends)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    bool wood = n.Contains("wood") || n.Contains("bark") || n.Contains("trunk");
                    var pick = wood ? barkMat : leafMat;
                    if (pick != null) mats[i] = pick;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
