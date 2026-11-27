using UnityEngine;

// 종이와 총 지급 규칙.
// - 종이를 찾은 잡는 팀 사람은 즉시 총(4발)을 받는다. 종이로 받는 건 사람당 한 번이다.
// - 누군가 첫 종이를 찾으면 그 시점부터 1분 뒤, 아직 총이 없는 잡는 팀 모두에게 자동 지급한다.
// - 종이를 이미 찾은(총이 있는) 사람은 다른 종이를 주울 수 없다.
public class Armory : MonoBehaviour
{
    public const float AutoGrantDelay = 60f; // 첫 종이 발견 후 자동 지급까지(초)

    float firstPaperAt = -1f;
    float clock; // 일시정지 중엔 멈추는 게임 시계(초). 자동 지급 타이머가 쓴다.

    // 첫 종이가 발견됐고 자동 지급 시각이 아직 안 지났으면 남은 초, 아니면 -1.
    public float AutoGrantLeft
    {
        get
        {
            if (firstPaperAt < 0f) return -1f;
            float left = firstPaperAt + AutoGrantDelay - clock;
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
        if (firstPaperAt < 0f) firstPaperAt = clock;
        who.Grant(true);
    }

    GameManager gm0;
    string msg; float msgUntil;
    GUIStyle msgStyle;

    void OnGUI()
    {
        if (Time.unscaledTime >= msgUntil || MainMenu.IsMenuOpen || Event.current.type != EventType.Repaint) return;
        if (msgStyle == null) msgStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        msgStyle.fontSize = Mathf.RoundToInt(26f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
        HudText.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 44f), msg, msgStyle);
    }
    static readonly WeaponKind[] kinds3 = { WeaponKind.Shotgun, WeaponKind.Rifle, WeaponKind.Knife }; // 자동 지급은 랜덤 무기

    void Update()
    {
        // 숨는 팀은 판이 시작되면 무기(샷건/소총/칼) 중 하나를 무작위로 하나 받는다.
        if (gm0 == null) gm0 = FindFirstObjectByType<GameManager>();
        if (!MainMenu.IsMenuOpen && (gm0 == null || !gm0.paused)) clock += Time.deltaTime;
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
        if (firstPaperAt >= 0f && clock >= firstPaperAt + AutoGrantDelay)
            foreach (var h in GunHolder.All)
                if (h != null && h.IsAlive && h.IsSeeker && !h.HasGun) h.Grant(false, kinds3[Random.Range(0, kinds3.Length)]);

        // 확인용(혼자 테스트): 수색 시간에 G 키로 나를 숨는 팀/잡는 팀으로 바꾼다.
        // 잡는 팀일 때만 종이를 줍고 총을 받는다. 상대 플레이어가 생기면 지운다.
        if (!MainMenu.IsMenuOpen && Input.GetKeyDown(KeyCode.G))
        {
            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null && (gm.CurrentPhase == GameManager.Phase.Seeking || gm.CurrentPhase == GameManager.Phase.Hiding))
            {
                foreach (var h in GunHolder.All)
                {
                    if (h == null || h.GetComponent<FirstPersonController>() == null) continue;
                    var hp = h.GetComponent<Health>();
                    hp.team = hp.team == Team.Hider ? Team.Seeker : Team.Hider;
                    GameManager.LocalRole = hp.team == Team.Seeker ? GameManager.Role.Seeker : GameManager.Role.Hider; // 숨는 시간 검은 화면이 같은 팀을 보게
                    hp.ReviveForTest(); // 죽어 있었어도 다시 움직일 수 있게
                    h.ResetForTeamSwitch();
                    // 팀에 맞는 시작 위치로: 잡는 팀은 술래 구역, 숨는 팀은 구역 밖 아무 곳.
                    // 봇이 있으면 봇과 팀/위치를 서로 바꾼다. 없으면 팀 시작 위치로.
                    bool swapped = Bot.SwapWithPlayer(h.GetComponent<FirstPersonController>(), hp.team);
                    if (!swapped) Zones.Teleport(h.GetComponent<FirstPersonController>(), hp.team == Team.Seeker ? Zones.SeekerSpawnPoint(0) : Zones.HiderSpawnPoint());
                    msg = "테스트: 내 팀 = " + (hp.team == Team.Seeker ? "잡는 팀 (종이를 찾으세요)" : "숨는 팀 (무기 새로 받음)") + (swapped ? " / 봇과 자리 교환" : "");
                    msgUntil = Time.unscaledTime + 3f;
                    Debug.Log("[HushHunt] 테스트: 내 팀을 " + (hp.team == Team.Seeker ? "잡는 팀" : "숨는 팀") + "으로 바꿈");
                }
            }
        }
    }
}
