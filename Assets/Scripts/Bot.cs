using UnityEngine;

// 혼자 테스트용 봇 하나. 걸음 소리는 기본 걸음 범위(12m) 안에서만 3D로 들린다.
// 플레이어와 같은 몸 모델을 쓰고, 계속 돌아다니며, 몸이 빛나서 멀리서도 잘 보인다.
// G(팀 전환)를 누르면 플레이어와 팀/위치를 서로 바꾼다. 쏘지는 않는다.
// Health/GunHolder를 그대로 써서 피해, 숨는 시간 규칙, 사망, 관전 규칙이 플레이어와 같다.
[DefaultExecutionOrder(50)]
public class Bot : MonoBehaviour
{
    public static bool Enabled = true;     // false면 봇을 만들지 않는다
    public static Bot Instance { get; private set; }

    public const float Speed = 3.5f;       // 걷는 속도(m/s)
    public const float RetargetSeconds = 8f; // 이 시간 안에 못 가면 새 목적지
    public const float WanderRadius = 40f; // 한 번에 정하는 목적지 거리(m)
    public const float GrindWait = 4f;     // 수색 시작 뒤 술래가 멈춰 있는 시간 (Zones 돌 소리와 같게)

    Health health;
    GunHolder gun;
    CharacterController cc;
    GameManager gm;
    Transform visual;
    readonly System.Collections.Generic.List<Material> mats = new System.Collections.Generic.List<Material>();
    readonly System.Collections.Generic.List<Color> baseColors = new System.Collections.Generic.List<Color>();
    Light glow;
    Vector3 target;
    Vector3 lastPos;
    float retargetAt, nextStuckCheck, vy, seekFrom = -100f;
    bool tipped, hiddenNow;

    // 씬마다 한 번 만든다 (GameSystems가 부른다).
    public static void EnsureSpawned()
    {
        if (!Enabled || Object.FindFirstObjectByType<Bot>() != null) return;
        var player = Object.FindFirstObjectByType<FirstPersonController>();
        var ph = player != null ? player.GetComponent<Health>() : null;
        Team botTeam = (ph != null && ph.team == Team.Seeker) ? Team.Hider : Team.Seeker; // 플레이어의 상대 팀

        var go = new GameObject("Bot");
        go.transform.position = botTeam == Team.Seeker ? Zones.SeekerSpawnPoint(0) : Zones.HiderSpawnPoint();
        var c = go.AddComponent<CharacterController>();
        c.height = 2f; c.radius = 0.4f; c.center = Vector3.zero;
        var h = go.AddComponent<Health>();
        h.team = botTeam;
        go.AddComponent<GunHolder>();
        go.AddComponent<Bot>();
    }

    void Awake()
    {
        Instance = this;
        health = GetComponent<Health>();
        gun = GetComponent<GunHolder>();
        cc = GetComponent<CharacterController>();
        BuildVisual();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (gm != null) gm.PhaseChanged -= OnPhase;
    }

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        if (gm != null) gm.PhaseChanged += OnPhase;
        health.Died += OnDied;
        lastPos = transform.position;
        Refresh();
        PickTarget();
    }

    void OnPhase(GameManager.Phase p)
    {
        if (p == GameManager.Phase.Seeking) seekFrom = Time.time;
    }

    void OnDied(Health h)
    {
        // 쓰러진 모습: 눕히고 불빛을 끈다. 몸은 남는다.
        tipped = true;
        if (visual != null) { visual.localRotation = Quaternion.Euler(90f, 0f, 0f); visual.localPosition = new Vector3(0f, -0.8f, 0f); }
        if (glow != null) glow.enabled = false;
        foreach (var m in mats) RuntimeMaterials.SetEmission(m, Color.black);
    }

    // 팀 색: 잡는 팀 빨강, 숨는 팀 청록. 살아나면 다시 세우고 불을 켠다.
    void Refresh()
    {
        Color col = health.team == Team.Seeker ? new Color(1f, 0.15f, 0.1f) : new Color(0.1f, 1f, 0.8f);
        // 플레이어와 같은 몸 모델(PlayerBody)의 재질에 팀 색을 섞고 자체 발광을 준다.
        for (int i = 0; i < mats.Count; i++)
        {
            mats[i].color = Color.Lerp(baseColors[i], col, 0.55f);
            RuntimeMaterials.SetEmission(mats[i], col * 1.6f);
        }
        if (glow != null) { glow.color = col; glow.enabled = !hiddenNow; }
        if (tipped && visual != null) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
        tipped = false;
    }

    void BuildVisual()
    {
        // 플레이어와 같은 몸(PlayerBody: 몸통/머리/후드/배낭/팔다리, 걸을 때 다리 흔들림). 봇은 전신이 보인다.
        var foot = GetComponent<Footsteps>();
        if (foot == null) { foot = gameObject.AddComponent<Footsteps>(); foot.volume = 1f; foot.RemoteNoiseRadius = 12f; } // 다른 사람이 듣는 발소리: 기본 걸음 12m 안에서만 들린다
        var body = GetComponent<PlayerBody>();
        if (body == null) body = gameObject.AddComponent<PlayerBody>();
        body.SetFirstPerson(false);
        visual = transform.Find("Character Model");
        if (visual != null)
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var m = r.sharedMaterial;
                if (m != null && !mats.Contains(m)) { mats.Add(m); baseColors.Add(m.color); }
            }

        var lg = new GameObject("Bot Glow");
        lg.transform.SetParent(transform, false);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 9f;
        glow.intensity = 1.6f;
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        if (health == null || MainMenu.IsMenuOpen) return;
        if (gm == null) gm = FindFirstObjectByType<GameManager>();
        // 숨는 시간 동안 잡는 팀 봇은 보이면 안 된다 (맵 밖에 있는 것으로 친다).
        bool hide = gm != null && gm.CurrentPhase == GameManager.Phase.Hiding && health.team == Team.Seeker;
        if (hide != hiddenNow)
        {
            hiddenNow = hide;
            if (visual != null) visual.gameObject.SetActive(!hide);
            if (glow != null) glow.enabled = !hide && !health.IsDead;
        }
        if (health.IsDead) return;
        if (gm == null || gm.paused) return;
        var ph = gm.CurrentPhase;
        if (ph != GameManager.Phase.Hiding && ph != GameManager.Phase.Seeking) return;
        // 잡는 팀은 숨는 시간엔 움직이지 못하고, 수색이 시작돼도 돌 소리가 끝날 때까지 멈춘다 (플레이어와 같은 규칙).
        if (health.team == Team.Seeker)
        {
            if (ph == GameManager.Phase.Hiding) return;
            if (Time.time < seekFrom + GrindWait) return;
        }

        Vector3 to = target - transform.position; to.y = 0f;
        if (to.magnitude < 1.5f || Time.time > retargetAt) PickTarget();
        Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;

        // 막혀서 못 움직이면(나무 등) 새 목적지로.
        if (Time.time >= nextStuckCheck)
        {
            Vector3 d = transform.position - lastPos; d.y = 0f;
            if (d.magnitude < 0.35f) PickTarget();
            lastPos = transform.position;
            nextStuckCheck = Time.time + 0.8f;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * Time.deltaTime));
        vy = cc.isGrounded ? -1f : vy - 20f * Time.deltaTime;
        Vector3 v = transform.forward * Speed;
        v.y = vy;
        cc.Move(v * Time.deltaTime);
    }

    void PickTarget()
    {
        float lim = WorldConfig.Half - 8f;
        Vector3 p = transform.position;
        for (int i = 0; i < 30; i++)
        {
            Vector2 o = Random.insideUnitCircle * WanderRadius;
            float x = Mathf.Clamp(p.x + o.x, -lim, lim), z = Mathf.Clamp(p.z + o.y, -lim, lim);
            if (health.team == Team.Hider && Zones.Contains(x, z, 3f)) continue; // 숨는 팀은 봉쇄 구역 밖에서만
            target = new Vector3(x, p.y, z);
            break;
        }
        retargetAt = Time.time + RetargetSeconds;
    }

    void MoveTo(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
        lastPos = pos;
        vy = 0f;
        PickTarget();
    }

    // G(팀 전환): 플레이어와 팀과 위치를 서로 바꾼다. 봇이 없으면 false.
    // 부르는 시점: 플레이어 팀은 이미 바뀌었고 위치는 아직 그대로다.
    public static bool SwapWithPlayer(FirstPersonController player, Team playerNewTeam)
    {
        var b = Instance;
        if (b == null || player == null) return false;
        Vector3 botPos = b.transform.position;
        Vector3 playerPos = player.transform.position;
        Zones.Teleport(player, botPos);
        b.MoveTo(playerPos);
        b.health.team = playerNewTeam == Team.Hider ? Team.Seeker : Team.Hider;
        b.health.ReviveForTest();
        b.gun.ResetForTeamSwitch();
        b.Refresh();
        return true;
    }
}
