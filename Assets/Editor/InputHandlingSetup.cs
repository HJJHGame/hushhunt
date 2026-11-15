using UnityEditor;
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
        EditorApplication.delayCall += () =>
            EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
    }
}
