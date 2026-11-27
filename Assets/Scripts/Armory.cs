using UnityEngine;

// 종이와 총 지급 규칙.
// - 종이를 찾은 잡는 팀 사람은 즉시 총(4발)을 받는다. 종이로 받는 건 사람당 한 번이다.
// - 누군가 첫 종이를 찾으면 그 시점부터 1분 뒤, 아직 총이 없는 잡는 팀 모두에게 자동 지급한다.
// - 종이를 이미 찾은(총이 있는) 사람은 다른 종이를 주울 수 없다.
public class Armory : MonoBehaviour
{
    public const float AutoGrantDelay = 60f; // 첫 종이 발견 후 자동 지급까지(초)

    float firstPaperAt = -1f;

    // 첫 종이가 발견됐고 자동 지급 시각이 아직 안 지났으면 남은 초, 아니면 -1.
    public float AutoGrantLeft
    {
        get
        {
            if (firstPaperAt < 0f) return -1f;
            float left = firstPaperAt + AutoGrantDelay - Time.time;
            return left > 0f ? left : -1f;
        }
    }

    public bool CanTakePaper(GunHolder who)
    {
        return who != null && who.IsAlive && who.IsSeeker && !who.HasGun;
    }

    // 종이를 주웠다.
    public void OnPaperTaken(GunHolder who)
    {
        if (!CanTakePaper(who)) return;
        if (firstPaperAt < 0f) firstPaperAt = Time.time;
        who.Grant(true);
    }

    GameManager gm0;
    static readonly WeaponKind[] kinds3 = { WeaponKind.Shotgun, WeaponKind.Rifle, WeaponKind.Knife }; // 자동 지급은 랜덤 무기

    void Update()
    {
        // 숨는 팀은 판이 시작되면 무기(샷건/소총/칼) 중 하나를 무작위로 하나 받는다.
        if (gm0 == null) gm0 = FindFirstObjectByType<GameManager>();
        if (gm0 != null && (gm0.CurrentPhase == GameManager.Phase.Hiding || gm0.CurrentPhase == GameManager.Phase.Seeking))
        {
            foreach (var h in GunHolder.All)
            {
                if (h == null || !h.IsAlive || h.IsSeeker || h.GotStartWeapon) continue;
                h.GotStartWeapon = true;
                if (h.HasGun) continue;
                var kinds = new[] { WeaponKind.Shotgun, WeaponKind.Rifle, WeaponKind.Knife };
                h.Grant(false, kinds[Random.Range(0, kinds.Length)]);
            }
        }

        // 1분이 지나면 총이 없는 잡는 팀 모두에게 지급한다 (늦게 합류한 사람도 같다).
        if (firstPaperAt >= 0f && Time.time >= firstPaperAt + AutoGrantDelay)
            foreach (var h in GunHolder.All)
                if (h != null && h.IsAlive && h.IsSeeker && !h.HasGun) h.Grant(false, kinds3[Random.Range(0, kinds3.Length)]);

        // 확인용(혼자 테스트): 수색 시간에 G 키로 나를 숨는 팀/잡는 팀으로 바꾼다.
        // 잡는 팀일 때만 종이를 줍고 총을 받는다. 상대 플레이어가 생기면 지운다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.G))
        {
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null && gm.CurrentPhase == GameManager.Phase.Seeking)
            {
                foreach (var h in GunHolder.All)
                {
                    if (h == null || h.GetComponent<FirstPersonController>() == null) continue;
                    var hp = h.GetComponent<Health>();
                    hp.team = hp.team == Team.Hider ? Team.Seeker : Team.Hider;
                    Debug.Log("[HushHunt] 테스트: 내 팀을 " + (hp.team == Team.Seeker ? "잡는 팀" : "숨는 팀") + "으로 바꿈");
                }
            }
        }
    }
}
