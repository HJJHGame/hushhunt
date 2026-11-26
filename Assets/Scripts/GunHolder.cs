using System.Collections.Generic;
using UnityEngine;

// 총을 가질 수 있는 플레이어(주로 잡는 팀). 탄은 4발 고정, 재장전 없음.
// 총은 종이를 찾거나(Armory), 첫 종이가 발견된 뒤 1분이 지나면 자동으로 지급된다.
// 쏘기/명중 처리는 다음 조각. 지금은 보유 여부와 남은 탄만 관리한다.
[RequireComponent(typeof(Health))]
public class GunHolder : MonoBehaviour
{
    public const int MagazineRounds = 4; // 모든 총은 4발

    public bool HasGun { get; private set; }
    public int Ammo { get; private set; }
    public bool GotGunFromPaper { get; private set; } // 종이로 총을 받았는지 (사람당 한 번)
    public float GrantedAt { get; private set; } = -100f; // 총을 받은 시각 (안내 표시용)

    public event System.Action<GunHolder> Granted;

    public static readonly List<GunHolder> All = new List<GunHolder>();

    Health health;

    public bool IsSeeker { get { return health != null && health.team == Team.Seeker; } }
    public bool IsAlive { get { return health != null && !health.IsDead; } }

    void Awake() { health = GetComponent<Health>(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // 총을 지급한다. 이미 있으면 아무 일도 없다.
    public void Grant(bool fromPaper)
    {
        if (HasGun) return;
        HasGun = true;
        Ammo = MagazineRounds;
        if (fromPaper) GotGunFromPaper = true;
        GrantedAt = Time.unscaledTime;
        if (Granted != null) Granted(this);
    }

    // 한 발 쓴다. 쏠 수 있으면 true. (쏘기 조각이 부른다)
    public bool TryUseAmmo()
    {
        if (!HasGun || Ammo <= 0) return false;
        Ammo--;
        return true;
    }
}
