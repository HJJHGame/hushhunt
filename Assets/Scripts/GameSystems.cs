using UnityEngine;

// 게임/환경/UI 컴포넌트의 부트스트랩 (issue #28).
// 예전에는 FirstPersonController.Awake가 플레이어 오브젝트에 전부 붙였다.
// 이제 이 클래스가 씬 시작 시 한 번 실행돼 역할을 나눈다.
// - 환경/UI: "GameSystems" 오브젝트에 씬당 하나씩만
// - 플레이어 본인의 몸/장비(PlayerCharacter, Flashlight): 플레이어 오브젝트에
//   (멀티플레이어가 되면 이 둘은 플레이어 프리팹 책임으로 옮긴다)
public static class GameSystems
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var root = new GameObject("GameSystems");
        AddOnce<Crosshair>(root);      // 크로스헤어
        AddOnce<TreeModels>(root);     // 소나무 3D 모델 (없으면 기존 나무 유지)
        AddOnce<NightAtmosphere>(root);// 밤 조명/안개
        AddOnce<HouseGlow>(root);      // 집 창문 빛
        AddOnce<GroundTexture>(root);  // 바닥 텍스처
        AddOnce<NightSky>(root);       // 별/달
        AddOnce<HorrorOverlay>(root);  // 비네팅/그레인
        AddOnce<FogMotes>(root);       // 떠다니는 먼지 입자
        AddOnce<AmbientSound>(root);   // 바람/먼 소리
        AddOnce<PlayerHud>(root);      // 스태미나/손전등 표시
        AddOnce<MainMenu>(root);       // 타이틀/일시정지/설정 메뉴

        var fpc = Object.FindFirstObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            if (fpc.GetComponent<PlayerCharacter>() == null) fpc.gameObject.AddComponent<PlayerCharacter>();
            if (fpc.GetComponent<Flashlight>() == null) fpc.gameObject.AddComponent<Flashlight>();
        }
    }

    static void AddOnce<T>(GameObject root) where T : Component
    {
        // 기존 씬에 이미 같은 컴포넌트가 있으면 중복으로 붙이지 않는다.
        if (Object.FindFirstObjectByType<T>() != null) return;
        root.AddComponent<T>();
    }
}
