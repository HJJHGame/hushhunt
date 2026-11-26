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
        // 판이 돌아가는 동안(숨는 시간/수색 시간)에만 피해를 받는다.
        if (gm != null && gm.CurrentPhase != GameManager.Phase.Hiding && gm.CurrentPhase != GameManager.Phase.Seeking) return;

        Hp = Mathf.Max(0f, Hp - amount);
        if (Damaged != null) Damaged(this, amount, source);
        if (Hp <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;
        var move = GetComponent<FirstPersonController>();
        if (move != null) move.enabled = false;
        if (Died != null) Died(this);

        // 같은 팀이 모두 죽었는지 확인한다.
        foreach (var h in all)
            if (h != null && h.team == team && !h.IsDead) return;
        if (TeamWiped != null) TeamWiped(team);
        if (team == Team.Hider && gm != null) gm.EndRound(GameManager.Outcome.SeekersWin);
    }
}
