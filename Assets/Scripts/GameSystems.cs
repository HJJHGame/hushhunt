using UnityEngine;
using UnityEngine.SceneManagement;

// 게임/환경/UI 컴포넌트의 부트스트랩 (issue #28).
// 예전에는 FirstPersonController.Awake가 플레이어 오브젝트에 전부 붙였다.
// 이제 이 클래스가 씬이 로드될 때마다 한 번 실행돼 역할을 나눈다.
// - 환경/UI: "GameSystems" 오브젝트에 씬당 하나씩만
// - 플레이어 본인의 몸/장비(PlayerBody, Flashlight): 플레이어 오브젝트에
//   (멀티플레이어가 되면 이 둘은 플레이어 프리팹 책임으로 옮긴다)
//
// RuntimeInitializeOnLoadMethod는 앱 시작 후 첫 씬에서 딱 한 번만 호출된다.
// 그래서 R 재시작/타이틀로(SceneManager.LoadScene) 뒤에는 sceneLoaded로 다시 붙여야 한다.
// 그러지 않으면 재시작한 씬에 메뉴/HUD/조명 연출이 하나도 없고 라운드도 시작되지 않는다.
public static class GameSystems
{
    const string RootName = "GameSystems";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // 도메인 리로드를 끈 에디터 설정에서도 핸들러가 중복 등록되지 않게 먼저 뺀다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Setup();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive 로드(보조 씬)에는 시스템을 다시 붙이지 않는다.
        if (mode == LoadSceneMode.Single) Setup();
    }

    // 여러 번 불려도 안전하다 (이미 있는 컴포넌트는 건너뛴다).
    static void Setup()
    {
        // 플레이어가 없는 씬(타이틀 전용 씬 등)에는 게임 시스템이 필요 없다.
        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        if (fpc == null) return;

        var root = GameObject.Find(RootName);
        if (root == null) root = new GameObject(RootName);

        AddOnce<Crosshair>(root);       // 크로스헤어
        AddOnce<SpruceForest>(root);    // 가문비 모델 숲 (모델 없으면 아무것도 안 함)
        AddOnce<NightAtmosphere>(root); // 밤 조명/안개
        AddOnce<HouseGlow>(root);       // 집 창문 빛
        AddOnce<GroundTexture>(root);   // 바닥 텍스처
        AddOnce<PropTextures>(root);    // 집/소품 질감
        AddOnce<RockModels>(root);      // 바위 모델/절차적 메시
        AddOnce<PropModels>(root);      // 통나무/그루터기 모델 (폴더에 넣으면 교체)
        AddOnce<GroundCover>(root);     // 숲 바닥 덮개 (폴더에 넣으면 흩뿌림)
        AddOnce<Zones>(root);           // 봉쇄 구역 5곳 (돌벽, 수색 시작 때 사라짐)
        AddOnce<RandomizeLayout>(root); // 매 판 집/나무/스폰 재배치
        AddOnce<Armory>(root);          // 종이 -> 총 지급 규칙
        AddOnce<EscapeGate>(root);      // 탈출구 (잡는 팀 전멸 시 열림)
        AddOnce<TreePaper>(root);       // 나무에 붙은 A4 용지
        AddOnce<SimplifiedTestMode>(root); // 테스트: 나무/땅만 남기기 (WorldConfig.SimplifiedTestMode)
        AddOnce<NightSky>(root);        // 별/달
        AddOnce<HorrorOverlay>(root);   // 비네팅/그레인
        AddOnce<FogMotes>(root);        // 떠다니는 먼지 입자
        AddOnce<AmbientSound>(root);    // 바람/먼 소리
        AddOnce<Inventory>(root);       // 인벤토리 5칸 (E)
        AddOnce<PlayerHud>(root);       // 스태미나/손전등 표시
        AddOnce<MainMenu>(root);        // 타이틀/일시정지/설정 메뉴

        if (fpc.GetComponent<PlayerBody>() == null) fpc.gameObject.AddComponent<PlayerBody>();
        if (fpc.GetComponent<Flashlight>() == null) fpc.gameObject.AddComponent<Flashlight>();
        if (fpc.GetComponent<Health>() == null) fpc.gameObject.AddComponent<Health>(); // 체력 100 (숨는 팀)
        if (fpc.GetComponent<GunHolder>() == null) fpc.gameObject.AddComponent<GunHolder>(); // 총/탄
        if (fpc.GetComponent<HeldWeapon>() == null) fpc.gameObject.AddComponent<HeldWeapon>(); // 들고 있는 무기 표시
    }

    static void AddOnce<T>(GameObject root) where T : Component
    {
        // 기존 씬에 이미 같은 컴포넌트가 있으면 중복으로 붙이지 않는다.
        if (Object.FindFirstObjectByType<T>() != null) return;
        root.AddComponent<T>();
    }
}
