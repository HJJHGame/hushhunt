using System.Collections.Generic;
using UnityEngine;

// 봉쇄 구역 5곳(네 모퉁이 + 정중앙), 각 15x15m. 숨는 시간 동안 돌벽으로 막혀 있어 아무도 못 들어간다.
// 수색이 시작되면 5곳의 벽이 동시에 사라지고, 돌 갈리는 2D 소리(4초)가 나온다. 술래는 이 중 한 곳에서 같이 시작한다.
// 소리가 끝날 때까지 술래는 움직일 수 없다. 이 구역은 나중에 탈출구 후보가 된다.
public class Zones : MonoBehaviour
{
    public const float Size = 15f;        // 구역 한 변(m)
    public const float WallHeight = 4f;
    public const float WallThick = 1f;
    public const float GrindSeconds = 4f; // 돌 갈리는 소리 길이 = 술래 정지 시간

    public static Vector2[] Centers { get; private set; }
    public static int SpawnZone { get; private set; } // 이번 판 술래가 시작하는 구역(0~4)

    static bool inited;

    // 판마다 한 번 계산한다 (RandomizeLayout이 먼저 불러도 된다). 씬을 다시 불러오면 새로 계산.
    public static void Init()
    {
        if (inited && Centers != null) return;
        float c = WorldConfig.Half - 1f - Size * 0.5f; // 가장자리 벽 안쪽에 딱 붙임
        Centers = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(c, c), new Vector2(c, -c), new Vector2(-c, c), new Vector2(-c, -c),
        };
        SpawnZone = Random.Range(0, Centers.Length);
        inited = true;
    }

    // (x,z)가 구역 안(또는 margin만큼 바깥)인가. 나무/바위/숨는 팀 시작 위치를 피할 때 쓴다.
    public static bool Contains(float x, float z, float margin)
    {
        Init();
        float h = Size * 0.5f + margin;
        foreach (var c in Centers)
            if (Mathf.Abs(x - c.x) <= h && Mathf.Abs(z - c.y) <= h) return true;
        return false;
    }

    // 술래 3명이 구역 안의 서로 다른 3곳에서 시작한다. index 0~2.
    public static Vector3 SeekerSpawnPoint(int index)
    {
        Init();
        var c = Centers[SpawnZone];
        Vector2[] off = { new Vector2(-4f, -3f), new Vector2(4f, -3f), new Vector2(0f, 4f) };
        var o = off[Mathf.Abs(index) % 3];
        return new Vector3(c.x + o.x, 1.1f, c.y + o.y);
    }

    // 숨는 팀 시작 위치: 구역 밖 아무 곳 (구역에서 10m 이상 떨어진 곳).
    public static Vector3 HiderSpawnPoint()
    {
        Init();
        float lim = WorldConfig.Half - 20f;
        for (int i = 0; i < 200; i++)
        {
            float x = Random.Range(-lim, lim), z = Random.Range(-lim, lim);
            if (!Contains(x, z, 10f)) return new Vector3(x, 1.1f, z);
        }
        return new Vector3(0f, 1.1f, WorldConfig.Half * 0.5f);
    }

    // 플레이어를 그 자리로 옮긴다 (CharacterController를 잠시 꺼야 충돌 상태가 꼬이지 않는다).
    public static void Teleport(FirstPersonController fpc, Vector3 p)
    {
        if (fpc == null) return;
        var cc = fpc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        fpc.transform.position = p;
        if (cc != null) cc.enabled = true;
    }

    readonly List<GameObject> walls = new List<GameObject>();
    GameManager gm;
    AudioSource src;
    float bannerUntil = -1f;
    float freezeUntil = -1f;
    GUIStyle style;

    void Awake()
    {
        inited = false; // 새 판
        Init();
        var mat = RuntimeMaterials.CreateLit("Zone Stone", new Color(0.32f, 0.32f, 0.33f), 0.1f);
        for (int i = 0; i < Centers.Length; i++)
        {
            var c = Centers[i];
            float h = Size * 0.5f;
            Wall(c.x, c.y + h, Size + WallThick, WallThick, mat);
            Wall(c.x, c.y - h, Size + WallThick, WallThick, mat);
            Wall(c.x + h, c.y, WallThick, Size + WallThick, mat);
            Wall(c.x - h, c.y, WallThick, Size + WallThick, mat);
        }
    }

    void Wall(float x, float z, float sx, float sz, Material m)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Zone Wall";
        w.transform.SetParent(transform, false);
        w.transform.position = new Vector3(x, WallHeight * 0.5f, z);
        w.transform.localScale = new Vector3(sx, WallHeight, sz);
        w.GetComponent<Renderer>().sharedMaterial = m;
        walls.Add(w);
    }

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        if (gm != null) gm.PhaseChanged += OnPhase;
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f; // 2D: 방향 없음, 전원 같은 소리
        src.playOnAwake = false;
        src.clip = MakeGrindClip();
        src.volume = 1f;
        src.loop = false;
    }

    void OnDestroy()
    {
        if (gm != null) gm.PhaseChanged -= OnPhase;
        if (src != null && src.clip != null) Destroy(src.clip); // 런타임에 만든 클립 해제
    }

    void OnPhase(GameManager.Phase p)
    {
        if (p != GameManager.Phase.Seeking) return;
        foreach (var w in walls) if (w != null) Destroy(w);
        walls.Clear();
        if (src != null) { src.Stop(); src.Play(); } // 같은 소리가 겹쳐 두 번 울리지 않게 처음부터
        bannerUntil = Time.unscaledTime + GrindSeconds;

        // 로컬 플레이어가 술래면 구역으로 옮기고 소리가 끝날 때까지 멈춘다.
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var hp = fpc != null ? fpc.GetComponent<Health>() : null;
        if (fpc != null && hp != null && hp.team == Team.Seeker)
        {
            var cc = fpc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            fpc.transform.position = SeekerSpawnPoint(0);
            if (cc != null) cc.enabled = true;
            fpc.enabled = false;
            freezeUntil = Time.time + GrindSeconds;
        }
    }

    void Update()
    {
        if (freezeUntil > 0f && Time.time >= freezeUntil)
        {
            freezeUntil = -1f;
            var fpc = FindFirstObjectByType<FirstPersonController>();
            var hp = fpc != null ? fpc.GetComponent<Health>() : null;
            if (fpc != null && hp != null && !hp.IsDead) fpc.enabled = true;
        }
    }

    // 맷돌 갈리는 소리: 무거운 돌이 돌 위를 천천히 도는 소리. 한 바퀴(약 0.8초)마다 긁히는 소리가 커졌다 줄고,
    // 바퀴마다 둔한 "쿵"이 한 번, 낮은 울림이 계속 깔린다. 울림은 100Hz 안팎 + 배음이라 작은 스피커에서도 들린다.
    // 모두 코드로 만든다 (파일 없음). 최대 크기 0.8로 맞춰 클리핑 없음.
    static AudioClip MakeGrindClip()
    {
        int rate = 22050, n = (int)(rate * GrindSeconds);
        var data = new float[n];
        var rng = new System.Random(7);
        const float Turn = 0.8f;      // 맷돌 한 바퀴(초)
        float lp = 0f, lp2 = 0f, hp = 0f, thump = 0f;
        float lastPhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float phase = (t / Turn) % 1f;
            if (phase < lastPhase) thump = 1f; // 한 바퀴 돌 때마다 둔한 쿵
            lastPhase = phase;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.07f;      // 무거운 마찰음 (저중음)
            lp2 += (lp - lp2) * 0.25f;
            hp += (noise - hp) * 0.45f;      // 모래알 긁는 소리 (중고음)
            // 긁힘 세기: 한 바퀴 동안 한 번 커졌다 줄어든다
            float scrape = 0.45f + 0.55f * Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 2f);
            // 가끔 돌 알갱이가 튀는 소리
            float grit = rng.NextDouble() < 0.004 * scrape ? ((float)rng.NextDouble() * 2f - 1f) * 1.2f : 0f;
            float f = 95f + 8f * Mathf.Sin(2f * Mathf.PI * t / Turn);
            float rumble = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f
                         + Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.22f
                         + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.18f;
            float thumpSound = Mathf.Sin(2f * Mathf.PI * 60f * t) * thump * 0.9f;
            thump *= 0.9985f;
            data[i] = (lp * 2.6f + lp2 * 1.4f + (noise - hp) * 0.30f * scrape + grit + rumble * 0.8f) * scrape + thumpSound;
        }
        float max = 0.001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((GrindSeconds - t) / 0.6f);
            data[i] = data[i] / max * 0.8f * env;
        }
        var clip = AudioClip.Create("Stone Grind", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnGUI()
    {
        if (Time.unscaledTime >= bannerUntil || MainMenu.IsMenuOpen) return;
        if (Event.current.type != EventType.Repaint) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.fontSize = Mathf.RoundToInt(34f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
        HudText.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 60f), "술래가 스폰되었습니다", style, new Color(1f, 0.35f, 0.3f));
    }
}
