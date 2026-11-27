using System.Collections.Generic;
using UnityEngine;

public enum Team { Hider, Seeker }

// 확정 전투 규칙 값 (체력 100). 총/칼 조각이 Health.Hit()를 부르면 이 값으로 피해가 들어간다.
public static class CombatRules
{
    public const float MaxHp = 100f;
    public const int HitsToKillHider = 3;  // 숨는 팀은 잡는 팀 총에 3발 맞으면 죽는다
    public const int HitsToKillSeeker = 2; // 잡는 팀은 총 종류 상관없이 2발 맞으면 죽는다

    // 한 발에 들어가는 피해. 올림해서 정해진 발수로 정확히 죽게 한다 (100/3 = 33.33 -> 34).
    public static float HitDamage(Team victim)
    {
        int hits = victim == Team.Hider ? HitsToKillHider : HitsToKillSeeker;
        return Mathf.Ceil(MaxHp / hits);
    }
}

// 체력과 피격 처리. 플레이어(로컬), 나중에 다른 플레이어/봇 모두 이 컴포넌트를 붙인다.
// - 피해는 TakeDamage(수치) 또는 Hit()(규칙대로 한 발)로 받는다.
// - 죽으면 이동을 멈추고, 부활은 없다. 한 팀이 전멸하면 알린다.
// - 숨는 팀이 전멸하면 잡는 팀 승리(GameManager.EndRound). 잡는 팀 전멸은 TeamWiped 이벤트만 쏜다
//   (탈출구가 열리는 조각이 이어서 처리한다).
public class Health : MonoBehaviour
{
    public Team team = Team.Hider;

    public float Hp { get; private set; }
    public float Hp01 { get { return Hp / CombatRules.MaxHp; } }
    public bool IsDead { get; private set; }

    public event System.Action<Health, float, GameObject> Damaged; // 누가, 얼마, 누구한테서
    public event System.Action<Health> Died;
    public static event System.Action<Team> TeamWiped;

    static readonly List<Health> all = new List<Health>();

    GameManager gm;

    void Awake()
    {
        Hp = CombatRules.MaxHp;
        IsDead = false;
    }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
    }

    void Update()
    {
        // 확인용: H 키로 자기 자신이 총 한 발 맞은 것으로 처리 (총이 생기기 전까지 테스트용).
        if (GetComponent<FirstPersonController>() != null && !MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.H))
            Hit(null);
    }

    // 규칙대로 총 한 발 맞는다.
    public void Hit(GameObject source)
    {
        TakeDamage(CombatRules.HitDamage(team), source);
    }

    public void TakeDamage(float amount, GameObject source)
    {
        if (IsDead || amount <= 0f) return;
        // 수색 시간에만 피해를 받는다. 숨는 시간(60초)엔 아무 피해도 없다 (테스트용 H 키 포함).
        if (gm != null && gm.CurrentPhase != GameManager.Phase.Seeking) return;

        Hp = Mathf.Max(0f, Hp - amount);
        if (Damaged != null) Damaged(this, amount, source);
        if (Hp <= 0f) Die();
    }

    // 테스트용(G 팀 전환): 죽은 상태를 풀고 체력을 채우고 다시 움직일 수 있게 한다.
    // 혼자 테스트할 때 술래로 죽은 뒤 숨는 팀으로 돌아와 탈출해 보는 용도. 실제 판에는 부활이 없다.
    public void ReviveForTest()
    {
        IsDead = false;
        Hp = CombatRules.MaxHp;
        var move = GetComponent<FirstPersonController>();
        if (move != null) move.enabled = true;
    }

    void Die()
    {
        IsDead = true;
        var move = GetComponent<FirstPersonController>();
        if (move != null) move.enabled = false;
        // 구독자(HUD/관전 등)에서 예외가 나도 아래 전멸 판정이 건너뛰어지지 않게 감싼다.
        try { if (Died != null) Died(this); } catch (System.Exception e) { Debug.LogException(e); }

        // 같은 팀이 모두 죽었는지 확인한다 (사라졌거나 꺼진 대상은 세지 않는다).
        foreach (var h in all)
            if (h != null && h.isActiveAndEnabled && h.team == team && !h.IsDead) return;
        Debug.Log("[HushHunt] " + (team == Team.Hider ? "숨는" : "잡는") + " 팀 전멸");
        try { if (TeamWiped != null) TeamWiped(team); } catch (System.Exception e) { Debug.LogException(e); }
        if (team == Team.Hider)
        {
            if (gm == null) gm = FindFirstObjectByType<GameManager>(); // 캐시가 비어 있어도 판을 끝낸다
            if (gm != null) gm.EndRound(GameManager.Outcome.SeekersWin);
        }
    }
}
