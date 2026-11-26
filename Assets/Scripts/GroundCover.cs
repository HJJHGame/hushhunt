using UnityEngine;

// 숲 바닥 덮개: Assets/Resources/GroundCover 에 모델(고사리/풀/새싹 소나무 등 .fbx)이
// 있으면 맵 전체에 흩뿌린다. 없으면 아무것도 하지 않는다 (기존 숲 그대로).
// 충돌체는 만들지 않아 지나다닐 수 있고, 집/나무/바위/스폰 자리는 피한다. 빈 공간을 채우도록 많이 뿌린다.
public class GroundCover : MonoBehaviour
{
    const int CoverLayer = 29; // 30은 전나무 원거리 컬링, 31은 용지 레이캐스트가 쓴다
    const float CoverCullDistance = 25f; // 안개(지수 0.075)로 25m 밖은 거의 안 보인다
    void Start()
    {
        var prefabs = Resources.LoadAll<GameObject>("GroundCover");
        if (prefabs == null || prefabs.Length == 0) return;

        var rand = new System.Random(97);
        float half = WorldConfig.Half - 6f;
        int placed = 0;
        // 너무 많이 심으면 그리기 호출이 늘어 느려진다: 350개 상한.
        for (int i = 0; i < 1500 && placed < 350; i++)
        {
            float x = ((float)rand.NextDouble() * 2f - 1f) * half;
            float z = ((float)rand.NextDouble() * 2f - 1f) * half;
            if (x * x + z * z < 36f) continue; // 스폰 주변
            if (Physics.CheckSphere(new Vector3(x, 0.5f, z), 1.2f)) continue; // 집/나무/바위 안

            var go = Instantiate(prefabs[rand.Next(prefabs.Length)],
                new Vector3(x, 0f, z), Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f));
            go.name = "Ground Cover";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Destroy(go); continue; }
            foreach (var r in rends)
            {
                r.gameObject.layer = CoverLayer; // 가까운 것만 그린다 (샘플링 모델이 수십만~수백만 삼각형이라 전부 그리면 느리다)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var b = BoundsOf(rends);
            if (b.size.y > 2f) go.transform.localScale *= 2f / b.size.y; // 바닥 덮개는 2m 이하로
            var nb = BoundsOf(rends);
            go.transform.position += Vector3.up * (0f - nb.min.y); // 가장 낮은 점을 땅에
            placed++;
        }
        if (placed > 0) SetupCoverCulling();
        Debug.Log("[HushHunt] 바닥 덮개 " + placed + "개 배치");
    }

    static void SetupCoverCulling()
    {
        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null) return;
        var d = cam.layerCullDistances;
        if (d == null || d.Length != 32) d = new float[32];
        d[CoverLayer] = CoverCullDistance;
        cam.layerCullDistances = d;
        cam.layerCullSpherical = true;
    }

    static Bounds BoundsOf(Renderer[] rends)
    {
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
