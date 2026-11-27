using System.Collections.Generic;
using UnityEngine;

// 총을 가질 수 있는 플레이어(주로 잡는 팀). 탄은 4발 고정, 재장전 없음.
// 총은 종이를 찾거나(Armory), 첫 종이가 발견된 뒤 1분이 지나면 자동으로 지급된다.
// 로컬 플레이어는 좌클릭으로 쏜다(히트스캔). 수색 시간에만, 탄이 있을 때만.
public enum WeaponKind { None, Shotgun, Rifle, Knife }

[RequireComponent(typeof(Health))]
public class GunHolder : MonoBehaviour
{
    public const int HiderRounds = 4;  // 숨는 팀 탄약 (재장전 없음)
    public const int SeekerRounds = 6; // 잡는 팀 탄약
    public int MaxAmmo { get { return IsSeeker ? SeekerRounds : HiderRounds; } }

    public bool HasGun { get; private set; }
    public int Ammo { get; private set; }
    public bool GotGunFromPaper { get; private set; } // 종이로 총을 받았는지 (사람당 한 번)
    public float GrantedAt { get; private set; } = -100f; // 총을 받은 시각 (안내 표시용)

    public event System.Action<GunHolder> Granted;

    public static readonly List<GunHolder> All = new List<GunHolder>();

    public const float RifleRange = 1000f;  // 소총: 사거리 제한 없음
    public const float RifleSpreadDeg = 0.5f; // 소총 탄 퍼짐(도, 약간)
    public const int ShotgunPellets = 8;    // 샷건 펠릿 수
    public const float ShotgunSpreadDeg = 4f;
    public const float ShotgunFullRange = 5f;  // 이 안은 풀데미지
    public const float ShotgunZeroRange = 12f; // 여기서 데미지 0 (사이는 직선으로 감소)
    public const float KnifeRange = 2f;     // 칼 닿는 거리(m)
    public const float KnifeCooldown = 1f;  // 칼 휘두르는 간격(초)

    public WeaponKind Weapon { get; private set; }
    public bool GotStartWeapon { get; set; } // 숨는 팀 시작 무기를 받았는지
    float nextKnifeAt;
    public float LastShotAt { get; private set; } = -100f; // 마지막으로 쏜/휘두른 시각 (들고 있는 무기 연출용)

    public static string WeaponName(WeaponKind k)
    {
        switch (k) { case WeaponKind.Shotgun: return "샷건"; case WeaponKind.Rifle: return "소총"; case WeaponKind.Knife: return "칼"; }
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
        if (MainMenu.IsMenuOpen || !IsAlive || !HasGun) return;
        if (gm == null || gm.CurrentPhase != GameManager.Phase.Seeking) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (Weapon == WeaponKind.Knife)
        {
            if (Time.unscaledTime < nextKnifeAt) return;
            nextKnifeAt = Time.unscaledTime + KnifeCooldown;
        }
        else if (!TryUseAmmo()) return;
        Fire();
    }

    // 화면 가운데로 광선을 쏴 처음 맞은 상대 팀에게 한 발 피해를 준다.
    void Fire()
    {
        LastShotAt = Time.unscaledTime;
        flashUntil = Time.unscaledTime + 0.06f;
        var cam = Camera.main;
        if (cam == null) return;
        if (Weapon == WeaponKind.Shotgun) { FireShotgun(cam); return; }
        float range = Weapon == WeaponKind.Knife ? KnifeRange : RifleRange;
        Vector3 dirF = cam.transform.forward;
        if (Weapon == WeaponKind.Rifle) dirF = Spread(cam.transform, RifleSpreadDeg);
        var hits = Physics.RaycastAll(cam.transform.position, dirF, range, ~0, QueryTriggerInteraction.Ignore);
        var target = FirstTarget(hits);
        if (target != null)
        {
            target.Hit(gameObject);
            hitUntil = Time.unscaledTime + 0.25f;
        }
    }

    // 광선에 처음 막힌 곳이 상대 팀이면 그 Health를 돌려준다 (자기 몸/아군은 통과 아님: 아군은 막고 피해 없음).
    Health FirstTarget(RaycastHit[] hits)
    {
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
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

    // 샷건: 펠릿마다 광선을 쏜다. 한 발의 피해는 맞은 펠릿 비율 x 거리 감소(5m 풀, 12m에서 0).
    void FireShotgun(Camera cam)
    {
        var dmg = new Dictionary<Health, float>();
        for (int i = 0; i < ShotgunPellets; i++)
        {
            var hits = Physics.RaycastAll(cam.transform.position, Spread(cam.transform, ShotgunSpreadDeg), ShotgunZeroRange, ~0, QueryTriggerInteraction.Ignore);
            var t = FirstTarget(hits);
            if (t == null) continue;
            float d = Vector3.Distance(cam.transform.position, t.transform.position);
            float f = Mathf.Clamp01(1f - (d - ShotgunFullRange) / (ShotgunZeroRange - ShotgunFullRange));
            float add = CombatRules.HitDamage(t.team) * f / ShotgunPellets;
            float cur; dmg.TryGetValue(t, out cur);
            dmg[t] = cur + add;
        }
        foreach (var kv in dmg)
            if (kv.Value > 0f) { kv.Key.TakeDamage(kv.Value, gameObject); hitUntil = Time.unscaledTime + 0.25f; }
    }

    void OnGUI()
    {
        if (GetComponent<FirstPersonController>() == null) return;
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
