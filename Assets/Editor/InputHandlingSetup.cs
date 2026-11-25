using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 이 프로젝트의 Input.GetAxis/GetKey 코드를 위해 레거시 입력을 활성화한다.
// activeInputHandler는 공개 PlayerSettings API가 아닌 직렬화된 설정이다.
[InitializeOnLoad]
public static class InputHandlingSetup
{
    const int Both = 2; // 0=Input Manager, 1=Input System, 2=Both
    const string SettingsPath = "ProjectSettings/ProjectSettings.asset";

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
        // 플레이 모드 진입 중에는 설정을 바꾸거나 에디터를 다시 열면 안 된다.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var assets = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
        if (assets == null || assets.Length == 0 || assets[0] == null)
        {
            Debug.LogError("[Hush Hunt] Player settings could not be loaded: " + SettingsPath);
            return;
        }

        using (var settings = new SerializedObject(assets[0]))
        {
            var inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                Debug.LogError("[Hush Hunt] activeInputHandler was not found in Player settings.");
                return;
            }

            if (inputHandler.intValue == Both)
            {
                if (manual) Debug.Log("[Hush Hunt] Active Input Handling is already Both.");
                return;
            }

            inputHandler.intValue = Both;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();

        // 입력 백엔드 변경은 에디터를 다시 열어 적용한다. 재실행 시 위 검사에서 종료된다.
        Debug.Log("[Hush Hunt] Active Input Handling set to Both. Restarting the editor once to apply it.");
        EditorApplication.delayCall += RestartEditor;
    }

    static void RestartEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // 재시작하면 저장하지 않은 씬 수정이 사라진다. 먼저 저장 여부를 묻고, 취소하면 재시작하지 않는다.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[Hush Hunt] Input handling was changed but the editor was not restarted. "
                + "Restart Unity manually to apply it.");
            return;
        }
        EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
    }
}
