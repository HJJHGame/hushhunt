using System.Collections.Generic;
using UnityEngine;

// 총을 가질 수 있는 플레이어(주로 잡는 팀). 탄은 무기마다 고정(소총 5, 권총 10), 재장전 없음.
// 총은 종이를 찾거나(Armory), 첫 종이가 발견된 뒤 1분이 지나면 자동으로 지급된다.
// 로컬 플레이어는 좌클릭으로 쏜다(히트스캔). 수색 시간에만, 탄이 있을 때만.
public enum WeaponKind { None, Pistol, Rifle, Knife }

// 총소리: 맵 전체에서 들리고 방향이 있다. 거리에 따라 볼륨만 줄어든다(사거리로 끊기지 않음). 칼은 소리 없음.
public static class GunSound
{
    public const float MapWide = 450f; // 이 거리 밖에서도 작게 들린다
    static AudioClip rifle, pistol;
    static AnimationCurve rolloff;

    public static void Play(Vector3 pos, WeaponKind kind)
    {
        if (kind != WeaponKind.Rifle && kind != WeaponKind.Pistol) return;
        if (rifle == null) { rifle = Make(0.35f, 14f, 1f); pistol = Make(0.28f, 18f, 0.9f); }
        if (rolloff == null) rolloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(MapWide, 0.08f));
        var go = new GameObject("Gunshot");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = kind == WeaponKind.Rifle ? rifle : pistol;
        a.spatialBlend = 1f; // 방향 있음
        a.rolloffMode = AudioRolloffMode.Custom;
        a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
        a.minDistance = 2f;
        a.maxDistance = MapWide;
        a.volume = 1f;
        a.Play();
        Object.Destroy(go, a.clip.length + 0.2f);
    }

    static AudioClip Make(float len, float decay, float gain)
    {
        int rate = 22050, n = (int)(rate * len);
        var d = new float[n];
        var rng = new System.Random(3);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.5f;
            float x = (lp + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f) * Mathf.Exp(-t * decay) * gain * 2.5f;
            d[i] = Mathf.Clamp(x / (1f + Mathf.Abs(x)) * 1.6f, -1f, 1f); // 부드럽게 눌러서(포화) 같은 최대 크기에서 훨씬 크게 들리게
        }
        var c = AudioClip.Create("Gunshot", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}

[RequireComponent(typeof(Health))]
public class GunHolder : MonoBehaviour
{
    // 무기별 수치 (실제 특성에 맞춤). 팀과 상관없이 같은 무기는 같은 값.
    public const int RifleRounds = 5;         // 볼트액션 소총 탄약 (재장전 없음)
    public const int PistolRounds = 10;       // 권총 탄약
    public const float RifleDamage = 100f;    // 소총: 한 발 100 (체력 100이라 한 방에 죽음)
    public const float PistolDamage = 40f;    // 권총: 한 발 40 (세 발)
    public const float KnifeDamage = 50f;     // 칼: 한 번 50 (두 번)
    public const float RifleCooldown = 1.5f;  // 소총 발사 간격(초): 볼트를 당겨 다시 장전하는 시간
    public const float PistolCooldown = 0.35f; // 권총 발사 간격(초): 반자동
    public const float KnifeCooldown = 1.2f;  // 칼 휘두르는 간격(초)
    public int MaxAmmo { get { return Weapon == WeaponKind.Rifle ? RifleRounds : (Weapon == WeaponKind.Pistol ? PistolRounds : 0); } }

    public static float DamageOf(WeaponKind k)
    {
        return k == WeaponKind.Rifle ? RifleDamage : (k == WeaponKind.Pistol ? PistolDamage : KnifeDamage);
    }

    public static float CooldownOf(WeaponKind k)
    {
        return k == WeaponKind.Rifle ? RifleCooldown : (k == WeaponKind.Pistol ? PistolCooldown : KnifeCooldown);
    }

    public bool HasGun { get; private set; }
    public int Ammo { get; private set; }
    public bool GotGunFromPaper { get; private set; } // 종이로 총을 받았는지 (사람당 한 번)
    public float GrantedAt { get; private set; } = -100f; // 총을 받은 시각 (안내 표시용)

    public event System.Action<GunHolder> Granted;

    public static readonly List<GunHolder> All = new List<GunHolder>();

    public const float RifleRange = 1000f;  // 소총: 사거리 제한 없음
    public const float RifleSpreadDeg = 0.5f; // 소총 탄 퍼짐(도, 약간)
    public const float PistolRange = 60f;      // 권총: 사거리(m). 이 안에서는 거리와 상관없이 같은 피해
    public const float PistolSpreadDeg = 1f;   // 권총 탄 퍼짐(도, 아주 약간)
    public const float KnifeRange = 2f;     // 칼 닿는 거리(m)

    public WeaponKind Weapon { get; private set; }
    public bool Escaped { get; set; } // 탈출에 성공한 사람: 이후 피해를 받지 않는다
    public bool GotStartWeapon { get; set; } // 숨는 팀 시작 무기를 받았는지
    float nextFireAt; // 다음에 쏠 수 있는 시각 (연타 방지)
    public float LastShotAt { get; private set; } = -100f; // 마지막으로 쏜/휘두른 시각 (들고 있는 무기 연출용)

    public static string WeaponName(WeaponKind k)
    {
        switch (k) { case WeaponKind.Pistol: return "권총"; case WeaponKind.Rifle: return "소총"; case WeaponKind.Knife: return "칼"; }
        return "";
    }

    Health health;
    GameManager gm;
    float flashUntil, hitUntil;

    public bool IsSeeker { get { return health != null && health.team == Team.Seeker; } }
    public bool IsAlive { get { return health != null && !health.IsDead; } }

    void Awake() { health = GetComponent<Health>(); }
    void Start() { gm = FindFirstObjectByType<GameManager>(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        // 조작은 로컬 플레이어만.
        if (GetComponent<FirstPersonController>() == null) return;
        if (MainMenu.IsMenuOpen || !IsAlive) return;
        if (!Input.GetMouseButtonDown(0)) return;
        // 쏠 수 없는 이유를 화면에 알려 준다 (아무 반응이 없어 고장 난 것처럼 보이지 않게).
        if (gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) { Say("수색이 시작된 뒤에 쏠 수 있다"); return; }
        if (!HasGun) { Say(IsSeeker ? "총이 없다: 종이를 찾아야 한다" : "무기가 없다"); return; }
        if (Weapon != WeaponKind.Knife && Ammo <= 0) { Say("탄이 없다"); return; }
        if (Time.unscaledTime < nextFireAt) return; // 무기마다 정해진 간격이 지나야 다시 쏜다 (광클 방지)
        if (Weapon != WeaponKind.Knife && !TryUseAmmo()) return;
        nextFireAt = Time.unscaledTime + CooldownOf(Weapon);
        Fire();
    }

    // 화면 가운데로 광선을 쏴 처음 맞은 상대 팀에게 한 발 피해를 준다.
    void Fire()
    {
        GunSound.Play(transform.position + Vector3.up, Weapon);
        LastShotAt = Time.unscaledTime;
        flashUntil = Time.unscaledTime + 0.06f;
        var cam = Camera.main;
        if (cam == null) return;
        float range = Weapon == WeaponKind.Knife ? KnifeRange : (Weapon == WeaponKind.Pistol ? PistolRange : RifleRange);
        Vector3 dirF = cam.transform.forward;
        if (Weapon == WeaponKind.Rifle) dirF = Spread(cam.transform, RifleSpreadDeg);
        else if (Weapon == WeaponKind.Pistol) dirF = Spread(cam.transform, PistolSpreadDeg);
        int n = Physics.RaycastNonAlloc(cam.transform.position, dirF, hitBuf, range, ~0, QueryTriggerInteraction.Ignore);
        var target = FirstTarget(n);
        if (target != null)
        {
            target.TakeDamage(DamageOf(Weapon), gameObject);
            hitUntil = Time.unscaledTime + 0.25f;
        }
    }

    // 광선 결과 버퍼: 매번 새로 만들지 않고 재사용한다 (발사마다 쓰레기가 쌓이지 않게).
    static readonly RaycastHit[] hitBuf = new RaycastHit[32];
    static readonly System.Comparison<RaycastHit> byDistance = (a, b) => a.distance.CompareTo(b.distance);

    // 광선에 처음 막힌 곳이 상대 팀이면 그 Health를 돌려준다 (자기 몸은 통과, 아군/물체는 막고 피해 없음).
    Health FirstTarget(int count)
    {
        System.Array.Sort(hitBuf, 0, count, System.Collections.Generic.Comparer<RaycastHit>.Create(byDistance));
        for (int i = 0; i < count; i++)
        {
            var hit = hitBuf[i];
            if (hit.collider.transform.IsChildOf(transform)) continue; // 자기 몸은 통과
            var target = hit.collider.GetComponentInParent<Health>();
            if (target != null && target != health && !target.IsDead && target.team != health.team) return target;
            return null; // 처음 막힌 곳에서 끝
        }
        return null;
    }

    static Vector3 Spread(Transform cam, float deg)
    {
        var q = Quaternion.AngleAxis(Random.Range(0f, 360f), cam.forward) * Quaternion.AngleAxis(Random.Range(0f, deg), cam.right);
        return q * cam.forward;
    }

    string sayText; float sayUntil;
    GUIStyle sayStyle;
    void Say(string t) { sayText = t; sayUntil = Time.unscaledTime + 2f; }

    // 테스트용 팀 전환 때 무기 상태를 새로 시작한다: 잡는 팀은 총 없음, 숨는 팀은 시작 무기를 다시 받는다.
    public void ResetForTeamSwitch()
    {
        HasGun = false; Ammo = 0; Weapon = WeaponKind.None;
        GotGunFromPaper = false; GotStartWeapon = false; Escaped = false;
        nextFireAt = 0f;
    }

    void OnGUI()
    {
        if (GetComponent<FirstPersonController>() == null) return;
        if (Time.unscaledTime < sayUntil && !MainMenu.IsMenuOpen && Event.current.type == EventType.Repaint)
        {
            if (sayStyle == null) sayStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var st = sayStyle;
            st.fontSize = Mathf.RoundToInt(24f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
            HudText.Label(new Rect(0f, Screen.height * 0.7f, Screen.width, 40f), sayText, st);
        }
        if (Time.unscaledTime < flashUntil)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.6f, 0.18f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
        if (Time.unscaledTime < hitUntil)
        {
            var old = GUI.color;
            GUI.color = Color.red;
            float c = 7f;
            GUI.DrawTexture(new Rect(Screen.width / 2f - c, Screen.height / 2f - 1f, 2f * c, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width / 2f - 1f, Screen.height / 2f - c, 2f, 2f * c), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }

    // 총을 지급한다. 이미 있으면 아무 일도 없다.
    public void Grant(bool fromPaper, WeaponKind kind = WeaponKind.Rifle)
    {
        if (HasGun) return;
        HasGun = true;
        Weapon = kind;
        Ammo = kind == WeaponKind.Knife ? 0 : MaxAmmo; // 칼은 탄이 없다
        nextFireAt = 0f;
        if (fromPaper) GotGunFromPaper = true;
        GrantedAt = Time.unscaledTime;
        if (Granted != null) Granted(this);
    }

    // 한 발 쓴다. 쏠 수 있으면 true. (쏘기 조각이 부른다)
    public bool TryUseAmmo()
    {
        if (!HasGun) return false;
        if (Weapon == WeaponKind.Knife) return true;
        if (Ammo <= 0) return false;
        Ammo--;
        return true;
    }
}
