using System.Collections.Generic;
using UnityEngine;

// 다른 사람이 켠 손전등은 거리 제한 없이 멀리서도 "빛의 점"으로 보인다.
// 점은 화면 위에 직접 그린다(깊이 검사 없음): 나뭇잎/가지는 줄기 충돌체가 아니라서 줄기 사이로 불빛이 보이는데,
// 예전에는 3D 판을 세워서 앞에 있는 나뭇잎 모양에 가려져 보이지 않았다.
// 나와 불빛 사이를 줄기/벽/바위 같은 충돌체가 막으면 보이지 않는다.
// 로컬 플레이어 자신의 손전등은 해당 없음. (상대 플레이어가 생기면 자동으로 동작한다.)
public class FlashlightBeacons : MonoBehaviour
{
    public const float MinDistance = 8f;    // 이보다 가까우면 진짜 빛이 보인다
    public const float DotMeters = 0.5f;    // 불빛의 실제 크기(m): 멀수록 작게 보인다
    public const float MinDotPixels = 4f;   // 너무 작아지지 않게

    struct Dot { public Vector2 pos; public float size; }
    readonly List<Dot> dots = new List<Dot>();
    GameManager gm;

    void LateUpdate()
    {
        dots.Clear();
        var cam = Camera.main;
        if (cam == null) return;
        Transform camT = cam.transform;
        float pxPerRad = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
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
            if (d < MinDistance) continue;
            if (Physics.Linecast(camT.position, target, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(f.transform)) continue; // 막혔다
            Vector3 sp = cam.WorldToScreenPoint(target);
            if (sp.z <= 0f) continue; // 카메라 뒤
            dots.Add(new Dot { pos = new Vector2(sp.x, Screen.height - sp.y), size = Mathf.Max(DotMeters / d * pxPerRad, MinDotPixels) });
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || dots.Count == 0 || MainMenu.IsMenuOpen) return;
        var prev = GUI.color;
        GUI.color = new Color(1f, 0.93f, 0.7f, 1f);
        foreach (var d in dots)
            GUI.DrawTexture(new Rect(d.pos.x - d.size * 0.5f, d.pos.y - d.size * 0.5f, d.size, d.size), Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
