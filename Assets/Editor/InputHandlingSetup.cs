using UnityEditor;
using UnityEngine;

// Unity 6.3부터 프로젝트 설정이 새로 생성될 때 Active Input Handling 기본값이
// "Input System (New)"라서 기존 Input.GetAxis/GetKey 코드가 InvalidOperationException을 낸다.
// (6.0까지는 기본값이 Both였다.) 이 프로젝트는 레거시 Input Manager를 쓰므로,
// 첫 실행 때 자동으로 Both로 바꾸고 에디터를 한 번 재시작해서 바로 Play가 되게 한다.
// 수동 적용: 메뉴 Hush Hunt > Fix Active Input Handling
[InitializeOnLoad]
public static class InputHandlingSetup
{
    const int Both = 2; // PlayerSettings.activeInputHandler: 0=레거시, 1=신규, 2=Both

    static InputHandlingSetup()
    {
        EditorApplication.delayCall += () => Ensure(false);
    }

    [MenuItem("Hush Hunt/Fix Active Input Handling")]
    static void ForceFix()
    {
        Ensure(true);
    }

    static void Ensure(bool manual)
    {
        if (PlayerSettings.activeInputHandler == Both)
        {
            if (manual) Debug.Log("[Hush Hunt] Active Input Handling이 이미 Both입니다.");
            return;
        }

        PlayerSettings.activeInputHandler = Both;
        AssetDatabase.SaveAssets(); // ProjectSettings.asset 을 디스크에 기록

        // 입력 백엔드는 에디터 시작 시 초기화되므로 변경 적용에 재시작이 필요하다.
        Debug.Log("[Hush Hunt] Active Input Handling을 Both로 변경했습니다. 적용을 위해 에디터를 한 번 재시작합니다.");
        EditorApplication.delayCall += () =>
            EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
    }
}
