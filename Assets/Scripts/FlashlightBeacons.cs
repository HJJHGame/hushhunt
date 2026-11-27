using System.Collections.Generic;
using UnityEngine;

// 다른 사람이 켠 손전등은 거리 제한 없이 멀리서도 "빛의 점"으로 보인다.
// 시야(카메라 먼 거리 50m, 검은 시야 제한) 안에 못 들어오는 먼 불빛은, 같은 방향 가까운 거리(최대 40m)에
// 같은 각도 크기의 작은 빛 판을 세워 보여 준다. 나와 불빛 사이를 나무/벽이 막으면 보이지 않는다.
// 로컬 플레이어 자신의 손전등은 해당 없음. (상대 플레이어가 생기면 자동으로 동작한다.)
public class FlashlightBeacons : MonoBehaviour
{
    public const float ShowDistance = 40f;  // 표시용 판을 놓는 최대 거리
    public const float DotMeters = 0.5f;    // 불빛의 실제 크기(m): 멀수록 작게 보인다

    readonly List<Transform> pool = new List<Transform>();
    Material mat;
    Transform camT;
    GameManager gm;

    void Start()
    {
        mat = new Material(Shader.Find("Sprites/Default")); // 안개 영향 없음, 항상 밝게
        mat.color = new Color(1f, 0.93f, 0.7f, 1f);
    }

    Transform Get(int i)
    {
        while (pool.Count <= i)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Flashlight Beacon";
            Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            q.SetActive(false);
            pool.Add(q.transform);
        }
        return pool[i];
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        camT = cam.transform;
        int used = 0;
        foreach (var f in Flashlight.All)
        {
            if (f == null || !f.IsOn || f.GetComponent<FirstPersonController>() != null) continue;
            // 숨는 시간 동안 잡는 팀은 맵 밖에 있는 것으로 친다: 손전등 빛도 보이면 안 된다.
            if (gm == null) gm = FindFirstObjectByType<GameManager>();
            var fhp = f.GetComponent<Health>();
            if (gm != null && gm.CurrentPhase == GameManager.Phase.Hiding && fhp != null && fhp.team == Team.Seeker) continue;
            Vector3 target = f.LampPosition;
            Vector3 to = target - camT.position;
            float d = to.magnitude;
            if (d < 8f) continue; // 가까우면 진짜 빛이 보인다
            if (Physics.Linecast(camT.position, target, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(f.transform)) continue; // 막혔다
            var q = Get(used++);
            float shown = Mathf.Min(d, ShowDistance);
            q.gameObject.SetActive(true);
            q.position = camT.position + to / d * shown;
            q.rotation = camT.rotation;
            float size = Mathf.Max(DotMeters * shown / d, 0.04f * shown / 10f); // 같은 각도, 너무 작아지지 않게
            q.localScale = new Vector3(size, size, 1f);
        }
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }
}
