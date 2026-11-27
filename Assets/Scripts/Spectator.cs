using System.Collections.Generic;
using UnityEngine;

// 사망 후 관전: 같은 팀 생존자의 1인칭 시점만 본다. 상대 팀에게는 아무 영향이 없다.
// 좌클릭으로 다음 생존자로 바꾼다. 관전 중 소리는 관전 대상 기준(카메라/AudioListener가 따라간다).
// 같은 팀 생존자가 없으면(예: 혼자 테스트) 죽은 자리에서 그대로 둔다.
public class Spectator : MonoBehaviour
{
    FirstPersonController fpc;
    Health me;
    Transform cam, originalParent;
    Vector3 originalLocalPos;
    Quaternion originalLocalRot;
    GunHolder target;
    GUIStyle style;

    void Update()
    {
        if (fpc == null)
        {
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null) me = fpc.GetComponent<Health>();
            return;
        }
        if (me == null) return;
        var c = Camera.main;
        if (c == null) return;

        if (!me.IsDead)
        {
            if (target != null) Restore(c.transform); // 새 판 등으로 되살아난 경우
            return;
        }

        if (target != null && !target.IsAlive) target = null;
        if (target == null) target = NextTarget(null);
        if (target == null) return;

        if (cam == null)
        {
            cam = c.transform;
            originalParent = cam.parent; originalLocalPos = cam.localPosition; originalLocalRot = cam.localRotation;
        }
        // 생존자 눈 높이에 카메라를 붙인다 (생존자 시점의 시선 방향은 그 사람의 몸 방향을 따른다).
        // 카메라 피벗(눈)에 붙이면 좌우뿐 아니라 위아래 시선도 그 사람과 똑같이 따라간다.
        var tf = target.GetComponent<FirstPersonController>();
        Transform anchor = (tf != null && tf.cameraPivot != null) ? tf.cameraPivot : target.transform;
        if (cam.parent != anchor)
        {
            cam.SetParent(anchor, false);
            cam.localPosition = anchor == target.transform ? new Vector3(0f, 0.72f, 0.12f) : Vector3.zero;
            cam.localRotation = Quaternion.identity;
        }
        if (!MainMenu.IsMenuOpen && Input.GetMouseButtonDown(0))
        {
            var next = NextTarget(target);
            if (next != null) target = next;
        }
    }

    GunHolder NextTarget(GunHolder after)
    {
        var list = new List<GunHolder>();
        foreach (var g in GunHolder.All)
            if (g != null && g.IsAlive && g.transform != fpc.transform && g.GetComponent<Health>().team == me.team) list.Add(g);
        if (list.Count == 0) return null;
        int i = after == null ? -1 : list.IndexOf(after);
        return list[(i + 1) % list.Count];
    }

    void Restore(Transform c)
    {
        if (cam != null && originalParent != null)
        {
            cam.SetParent(originalParent, false);
            cam.localPosition = originalLocalPos;
            cam.localRotation = originalLocalRot;
        }
        target = null;
    }

    void OnGUI()
    {
        if (target == null || MainMenu.IsMenuOpen || Event.current.type != EventType.Repaint) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        style.fontSize = Mathf.RoundToInt(24f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
        HudText.Label(new Rect(0f, Screen.height * 0.9f, Screen.width, 40f), "관전 중 (좌클릭: 다음 생존자)", style);
    }
}
