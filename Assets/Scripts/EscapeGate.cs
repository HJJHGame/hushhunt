using System.Collections.Generic;
using UnityEngine;

// 탈출구. 술래가 전멸하면, 처음 술래가 스폰할 수 있던 5곳(Zones) 중 랜덤 한 곳이 탈출구가 되고 위치가 모두에게 공개된다.
// 살아 있는 숨는 팀 전원이 제한시간 안에 그 구역에 들어가 F를 3초 꾹 눌러 탈출해야 숨는 팀 승리.
// 시간 안에 못 나가면 숨는 팀 패배(GameManager가 IsOpen을 보고 처리).
public class EscapeGate : MonoBehaviour
{
    public const float HoldSeconds = 3f; // 구역 안에서 F를 누르고 있어야 하는 시간

    public bool IsOpen { get; private set; }

    Vector3 pos;
    GameObject beam;
    GameManager gm;
    readonly HashSet<GunHolder> escaped = new HashSet<GunHolder>();
    float bannerUntil;
    float holdStart = -1f;
    GUIStyle style;

    void OnEnable() { Health.TeamWiped += OnTeamWiped; }
    void OnDisable() { Health.TeamWiped -= OnTeamWiped; }

    void Start() { gm = FindFirstObjectByType<GameManager>(); }

    void OnTeamWiped(Team t)
    {
        if (t == Team.Seeker) Open();
    }

    static bool InsideZone(Vector3 p, Vector3 center)
    {
        float h = Zones.Size * 0.5f;
        return Mathf.Abs(p.x - center.x) <= h && Mathf.Abs(p.z - center.z) <= h;
    }

    void Open()
    {
        if (IsOpen || gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) return;
        Zones.Init();
        var c = Zones.Centers[Random.Range(0, Zones.Centers.Length)];
        pos = new Vector3(c.x, 0f, c.y);
        IsOpen = true;
        bannerUntil = Time.unscaledTime + 5f;

        // 먼 곳에서도 보이는 초록 빛기둥
        var unlit = Shader.Find("Unlit/Color");
        var mat = new Material(unlit) { name = "Gate Open", color = new Color(0.2f, 1f, 0.35f) };
        beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Gate Beam";
        beam.transform.position = pos + Vector3.up * 40f;
        beam.transform.localScale = new Vector3(1f, 80f, 1f);
        beam.GetComponent<Renderer>().sharedMaterial = mat;
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(beam.GetComponent<Collider>());
    }

    void Update()
    {
        if (gm == null) return;

        // 확인용(혼자 테스트): 수색 시간에 O 키로 탈출구를 강제로 연다. 상대 플레이어가 생기면 지운다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.O)) Open();

        if (!IsOpen || gm.CurrentPhase != GameManager.Phase.Seeking) return;

        // 로컬 플레이어: 구역 안에서 F를 3초 누르면 탈출
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var me = fpc != null ? fpc.GetComponent<GunHolder>() : null;
        if (me != null && me.IsAlive && !me.IsSeeker && !escaped.Contains(me) && !MainMenu.IsMenuOpen
            && InsideZone(me.transform.position, pos) && Input.GetKey(KeyCode.F))
        {
            if (holdStart < 0f) holdStart = Time.unscaledTime;
            if (Time.unscaledTime - holdStart >= HoldSeconds)
            {
                escaped.Add(me);
                me.Escaped = true; // 탈출 뒤에는 피해를 받지 않는다
                fpc.enabled = false;
                holdStart = -1f;
            }
        }
        else holdStart = -1f;

        // 살아 있는 숨는 팀이 모두 탈출했으면 숨는 팀 승리.
        int alive = 0, outCount = 0;
        foreach (var h in GunHolder.All)
        {
            if (h == null || !h.IsAlive || h.IsSeeker || h.GetComponent<Bot>() != null) continue; // 봇은 탈출하지 않으니 세지 않는다
            alive++;
            if (escaped.Contains(h)) outCount++;
        }
        if (alive > 0 && alive == outCount) gm.EndRound(GameManager.Outcome.HidersEscaped);
    }

    void OnGUI()
    {
        if (!IsOpen || MainMenu.IsMenuOpen || gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) return;
        if (Event.current.type != EventType.Repaint) return;
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var me = fpc != null ? fpc.GetComponent<GunHolder>() : null;
        var cam = Camera.main;
        if (me == null || cam == null) return; // 위치는 팀 상관없이 전원에게 공개

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
            text = (Time.unscaledTime < bannerUntil ? "술래 전멸! 탈출구가 열렸다\n" : "") + "탈출구 " + arrow + " " + Mathf.RoundToInt(to.magnitude) + "m";
            if (!me.IsSeeker && InsideZone(me.transform.position, pos))
                text += holdStart >= 0f
                    ? "\n탈출 중... " + Mathf.RoundToInt(Mathf.Clamp01((Time.unscaledTime - holdStart) / HoldSeconds) * 100f) + "%"
                    : "\nF를 3초 꾹 눌러 탈출";
        }
        HudText.Label(new Rect(0f, Screen.height * 0.16f, Screen.width, 120f * scale), text, style);
    }
}
