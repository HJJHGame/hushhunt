using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 처음 프로젝트를 열 때 Main 씬이 없으면 자동으로 만들어 준다.
// 메뉴 Hush Hunt > Setup Main Scene 으로 수동 재생성도 가능.
[InitializeOnLoad]
public static class SetupScene
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static SetupScene()
    {
        EditorApplication.delayCall += () => EnsureScene(false);
    }

    [MenuItem("Hush Hunt/Setup Main Scene")]
    static void ForceCreate()
    {
        EnsureScene(true);
    }

    static void EnsureScene(bool force)
    {
        if (!force && File.Exists(ScenePath)) return;

        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 분위기: 어둡고 축축한 숲
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.05f, 0.08f, 0.06f);
        RenderSettings.fogDensity = 0.045f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.11f, 0.09f);

        // 흐린 달빛 느낌의 디렉셔널 라이트
        var lightGo = new GameObject("Moon Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.55f, 0.65f, 0.7f);
        light.intensity = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 땅: 내장 Plane(10x10m)을 6배 -> 60x60m, 어두운 흙색
        var groundMat = MakeMaterial("Ground", new Color(0.07f, 0.10f, 0.06f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // 나무 기둥들 (원기둥 늘린 것, 자리 표시용)
        var trunkMat = MakeMaterial("Trunk", new Color(0.09f, 0.07f, 0.05f));
        var rand = new System.Random(42);
        for (int i = 0; i < 25; i++)
        {
            float x = (float)(rand.NextDouble() * 56.0 - 28.0);
            float z = (float)(rand.NextDouble() * 56.0 - 28.0);
            if (new Vector2(x, z).magnitude < 3f) continue; // 스폰 지점 비우기

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            float h = 3.5f + (float)rand.NextDouble() * 2.5f;
            float w = 0.3f + (float)rand.NextDouble() * 0.25f;
            trunk.transform.position = new Vector3(x, h * 0.5f, z);
            trunk.transform.localScale = new Vector3(w, h * 0.5f, w);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
        }

        // 플레이어: CharacterController + 카메라 자식
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.1f, 0f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;

        var camGo = new GameObject("PlayerCamera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();

        var fpc = player.AddComponent<FirstPersonController>();
        fpc.cameraPivot = camGo.transform;

        EditorSceneManager.SaveScene(scene, ScenePath);

        // 빌드 설정에 씬 추가
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("[Hush Hunt] Main scene created at " + ScenePath);
    }

    static Material MakeMaterial(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
