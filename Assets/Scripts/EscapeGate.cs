using System.Collections.Generic;
using UnityEngine;

// 탈출구. 잡는 팀이 모두 죽으면 열리고, 살아 있는 숨는 팀 전원이 나가면 숨는 팀 승리.
// - 맵 가장자리 한 곳에 판마다 무작위로 생긴다. 닫혀 있을 땐 붉은 기둥, 열리면 초록 빛기둥이 솟는다.
// - 열리면 화면 위쪽에 방향과 거리가 표시된다 (위치 공개).
// - 열린 탈출구 반경 안에 들어가면 그 사람은 탈출한다. 탈출하면 움직일 수 없다.
public class EscapeGate : MonoBehaviour
{
    public const float EdgeInset = 12f;   // 맵 끝에서 안쪽으로(m)
    public const float ExitRadius = 3.5f; // 이 안에 들어오면 탈출(m)

    public bool IsOpen { get; private set; }

    Vector3 pos;
    GameObject beam;
    Material openMat;
    GameManager gm;
    readonly HashSet<GunHolder> escaped = new HashSet<GunHolder>();
    float bannerUntil;
    GUIStyle style;

    void OnEnable() { Health.TeamWiped += OnTeamWiped; }
    void OnDisable() { Health.TeamWiped -= OnTeamWiped; }

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        // 맵 네 변 중 하나, 변을 따라 -80~80m 사이 아무 곳.
        int side = Random.Range(0, 4);
        float along = Random.Range(-80f, 80f);
        float e = WorldConfig.Half - EdgeInset;
        switch (side)
        {
            case 0: pos = new Vector3(along, 0f, e); break;
            case 1: pos = new Vector3(along, 0f, -e); break;
            case 2: pos = new Vector3(e, 0f, along); break;
            default: pos = new Vector3(-e, 0f, along); break;
        }
        Build();
    }

    void Build()
    {
        var unlit = Shader.Find("Unlit/Color");
        var closedMat = new Material(unlit) { name = "Gate Closed", color = new Color(0.55f, 0.05f, 0.05f) };
        openMat = new Material(unlit) { name = "Gate Open", color = new Color(0.2f, 1f, 0.35f) };

        var root = new GameObject("Escape Gate");
        root.transform.position = pos;
        // 문틀: 기둥 둘 + 윗보. 안쪽(맵 중심)을 향해 서 있다.
        root.transform.rotation = Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z).normalized);
        Piece(root, "Left", new Vector3(-2.5f, 2.5f, 0f), new Vector3(0.8f, 5f, 0.8f), closedMat);
        Piece(root, "Right", new Vector3(2.5f, 2.5f, 0f), new Vector3(0.8f, 5f, 0.8f), closedMat);
        Piece(root, "Top", new Vector3(0f, 5.2f, 0f), new Vector3(6.1f, 0.7f, 0.8f), closedMat);

        beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Gate Beam";
        beam.transform.SetParent(root.transform, false);
        beam.transform.localPosition = new Vector3(0f, 40f, 0f);
        beam.transform.localScale = new Vector3(0.7f, 80f, 0.7f);
        beam.GetComponent<Renderer>().sharedMaterial = openMat;
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(beam.GetComponent<Collider>());
        beam.SetActive(false);
    }

    static void Piece(GameObject parent, string name, Vector3 local, Vector3 scale, Material m)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        p.transform.SetParent(parent.transform, false);
        p.transform.localPosition = local;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = m;
    }

    void OnTeamWiped(Team t)
    {
        if (t == Team.Seeker) Open();
    }

    void Open()
    {
        if (IsOpen || gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) return;
        IsOpen = true;
        bannerUntil = Time.unscaledTime + 5f;
        if (beam != null) beam.SetActive(true);
        // 기둥도 초록으로
        if (beam != null)
            foreach (var r in beam.transform.parent.GetComponentsInChildren<Renderer>())
                if (r.gameObject != beam) r.sharedMaterial = openMat;
    }

    void Update()
    {
        if (gm == null) return;

        // 확인용(혼자 테스트): 수색 시간에 X 키로 탈출구를 강제로 연다. 상대 플레이어가 생기면 지운다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.X)) Open();

        if (!IsOpen || gm.CurrentPhase != GameManager.Phase.Seeking) return;

        // 열린 탈출구에 닿은 살아 있는 숨는 팀은 탈출한다.
        foreach (var h in GunHolder.All)
        {
            if (h == null || !h.IsAlive || h.IsSeeker || escaped.Contains(h)) continue;
            Vector3 d = h.transform.position - pos;
            d.y = 0f;
            if (d.magnitude > ExitRadius) continue;
            escaped.Add(h);
            var move = h.GetComponent<FirstPersonController>();
            if (move != null) move.enabled = false;
        }

        // 살아 있는 숨는 팀이 모두 탈출했으면 숨는 팀 승리.
        int alive = 0, out_ = 0;
        foreach (var h in GunHolder.All)
        {
            if (h == null || !h.IsAlive || h.IsSeeker) continue;
            alive++;
            if (escaped.Contains(h)) out_++;
        }
        if (alive > 0 && alive == out_) gm.EndRound(GameManager.Outcome.HidersWin);
    }

    void OnGUI()
    {
        if (!IsOpen || MainMenu.IsMenuOpen || gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) return;
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var me = fpc != null ? fpc.GetComponent<GunHolder>() : null;
        var cam = Camera.main;
        if (me == null || cam == null || me.IsSeeker) return;

        if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        float scale = Screen.height / 1080f;
        style.fontSize = Mathf.RoundToInt(24f * scale);

        string text;
        if (escaped.Contains(me)) text = "탈출 성공! 다른 사람을 기다리는 중";
        else
        {
            Vector3 to = pos - cam.transform.position;
            to.y = 0f;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            float ang = Vector3.SignedAngle(fwd, to, Vector3.up); // +면 오른쪽
            string arrow = Mathf.Abs(ang) < 20f ? "↑" : (Mathf.Abs(ang) > 140f ? "↓" : (ang > 0f ? "→" : "←"));
            text = (Time.unscaledTime < bannerUntil ? "잡는 팀 전멸! 탈출구가 열렸다\n" : "") + "탈출구 " + arrow + " " + Mathf.RoundToInt(to.magnitude) + "m";
        }
        HudText.Label(new Rect(0f, Screen.height * 0.16f, Screen.width, 100f * scale), text, style);
    }
}
