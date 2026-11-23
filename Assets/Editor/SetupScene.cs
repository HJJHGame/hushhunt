using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 처음 프로젝트를 열 때 Main 씬이 없으면 자동으로 만들어 준다.
// 씬 구성이 바뀌어 SceneVersion 이 올라가면 다음에 열 때 자동으로 다시 만든다.
// 메뉴 Hush Hunt > Setup Main Scene 으로 수동 재생성도 가능.
[InitializeOnLoad]
public static class SetupScene
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const int SceneVersion = 4; // 씬 구성을 바꾸면 1 올린다
    const string VersionKey = "HushHunt.SceneVersion";

    static SetupScene()
    {
        EditorApplication.delayCall += () =>
        {
            bool outdated = EditorPrefs.GetInt(VersionKey, 0) < SceneVersion;
            EnsureScene(outdated);
        };
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
        Directory.CreateDirectory("Assets/Meshes");
        // 방금 만든 폴더를 에셋 DB에 반영해야 CreateAsset/SaveScene이
        // "Parent directory must exist before creating asset" 오류 없이 동작한다.
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 분위기: 슬렌더맨 같은 공포 숲 - 짙은 안개, 키 크고 가는 나무 실루엣
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.09f, 0.11f, 0.13f); // 안개가 살짝 밝아야 나무가 실루엣으로 보임
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.08f);

        // 흐린 달빛 느낌의 디렉셔널 라이트
        var lightGo = new GameObject("Moon Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.6f, 0.7f, 0.85f);
        light.intensity = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 땅: 내장 Plane(10x10m)을 20배 -> 200x200m, 어두운 흙색
        var groundMat = MakeMaterial("Ground", new Color(0.05f, 0.06f, 0.05f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // 큰 소나무들: 갈색 원기둥 기둥 + 짙은 초록 원뿔 3단. 키 크고 가는 실루엣.
        // 유니티에 원뿔 프리미티브가 없어서 단위 원뿔 메시를 만들어 에셋으로 저장해 쓴다.
        var trunkMat = MakeMaterial("Trunk", new Color(0.10f, 0.07f, 0.05f));
        var pineMat = MakeMaterial("Pine", new Color(0.03f, 0.09f, 0.05f));
        var coneMesh = MakeConeMesh();

        var rand = new System.Random(42);
        int placed = 0;
        int attempts = 0;
        while (placed < 160 && attempts < 600)
        {
            attempts++;
            float x = (float)(rand.NextDouble() * 190.0 - 95.0);
            float z = (float)(rand.NextDouble() * 190.0 - 95.0);
            if (new Vector2(x, z).magnitude < 5f) continue; // 스폰 지점 비우기

            float scale = 0.9f + (float)rand.NextDouble() * 0.7f; // 0.9~1.6배 크기
            MakePineTree(new Vector3(x, 0f, z), scale, trunkMat, pineMat, coneMesh);
            placed++;
        }

        // 바위와 낡은 나무 울타리 (슬렌더 분위기 소품)
        var rockMat = MakeMaterial("Rock", new Color(0.08f, 0.09f, 0.09f));
        var fenceMat = MakeMaterial("Fence", new Color(0.09f, 0.07f, 0.05f));
        for (int i = 0; i < 25; i++)
        {
            float x = (float)(rand.NextDouble() * 190.0 - 95.0);
            float z = (float)(rand.NextDouble() * 190.0 - 95.0);
            if (new Vector2(x, z).magnitude < 5f) continue;

            float s = 0.5f + (float)rand.NextDouble() * 1.5f;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.position = new Vector3(x, s * 0.25f, z); // 살짝 묻힌 모양
            rock.transform.localScale = new Vector3(s * 1.4f, s * 0.8f, s);
            rock.transform.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);
            rock.GetComponent<Renderer>().sharedMaterial = rockMat;
        }

        // 낡은 울타리 두 토막: 삐뚤빼뚤한 기둥들 + 가로대 두 줄
        for (int f = 0; f < 2; f++)
        {
            float fx = (float)(rand.NextDouble() * 140.0 - 70.0);
            float fz = (float)(rand.NextDouble() * 140.0 - 70.0);
            if (new Vector2(fx, fz).magnitude < 6f) { f--; continue; }

            var fence = new GameObject("Old Fence");
            fence.transform.position = new Vector3(fx, 0f, fz);
            fence.transform.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);

            int posts = 5 + rand.Next(3);
            for (int p = 0; p < posts; p++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                post.transform.SetParent(fence.transform);
                post.transform.localPosition = new Vector3(p * 1.4f, 0.55f, 0f);
                post.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
                post.transform.localRotation = Quaternion.Euler((float)(rand.NextDouble() * 10.0 - 5.0), 0f, (float)(rand.NextDouble() * 14.0 - 7.0));
                post.GetComponent<Renderer>().sharedMaterial = fenceMat;
            }
            for (int rl = 0; rl < 2; rl++)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Rail";
                rail.transform.SetParent(fence.transform);
                rail.transform.localPosition = new Vector3((posts - 1) * 1.4f * 0.5f, 0.45f + rl * 0.45f, 0.06f);
                rail.transform.localScale = new Vector3(posts * 1.4f, 0.08f, 0.05f);
                rail.GetComponent<Renderer>().sharedMaterial = fenceMat;
            }
        }

        // 맵 가장자리 보이지 않는 벽: 200x200m 경계(±100m)를 콜라이더로 둘러 낙하 방지.
        // 렌더러가 없어서 화면과 미니맵에는 보이지 않는다.
        MakeEdgeWall(new Vector3(0f, 2f, 100f), new Vector3(202f, 4f, 1f));
        MakeEdgeWall(new Vector3(0f, 2f, -100f), new Vector3(202f, 4f, 1f));
        MakeEdgeWall(new Vector3(100f, 2f, 0f), new Vector3(1f, 4f, 202f));
        MakeEdgeWall(new Vector3(-100f, 2f, 0f), new Vector3(1f, 4f, 202f));

        // 플레이어: CharacterController + 카메라 자식 + 발소리
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
        var playerCam = camGo.AddComponent<Camera>();
        playerCam.clearFlags = CameraClearFlags.SolidColor; // 스카이박스 대신 안개색 배경 (밤 하늘)
        playerCam.backgroundColor = new Color(0.06f, 0.08f, 0.10f);
        camGo.AddComponent<AudioListener>();

        var fpc = player.AddComponent<FirstPersonController>();
        fpc.cameraPivot = camGo.transform;
        player.AddComponent<Footsteps>();

        // 미니맵: 플레이어를 따라가는 탑다운 카메라.
        // 화면 오른쪽 위 표시와 플레이어 화살표는 Minimap 스크립트가 OnGUI로 처리.
        var minimapGo = new GameObject("MinimapCamera");
        var minimapCam = minimapGo.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = 100f; // 200x200m 맵 전체가 들어옴
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = new Color(0.02f, 0.03f, 0.02f);
        minimapCam.nearClipPlane = 1f;
        minimapCam.transform.position = player.transform.position + Vector3.up * 45f;
        minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapGo.AddComponent<Minimap>().target = player.transform;

        // 라운드 진행 관리자: 숨는 시간 -> 수색 타이머 -> 종료까지 한 판의 흐름
        new GameObject("GameManager").AddComponent<GameManager>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorPrefs.SetInt(VersionKey, SceneVersion);

        // 빌드 설정에 씬 추가
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("[Hush Hunt] Main scene created at " + ScenePath + " (v" + SceneVersion + ")");
    }

    // 보이지 않는 가장자리 벽 하나: BoxCollider만 있는 빈 오브젝트.
    static void MakeEdgeWall(Vector3 center, Vector3 size)
    {
        var wall = new GameObject("Edge Wall");
        wall.transform.position = center;
        wall.AddComponent<BoxCollider>().size = size;
    }

    // 소나무 한 그루: 기둥 위에 원뿔 3단 (아래로 갈수록 넓음). 총 높이 약 9.5m x scale.
    // 키 크고 가는 실루엣이 나오도록 기둥은 가늘고 수형은 높게.
    static void MakePineTree(Vector3 pos, float scale, Material trunkMat, Material pineMat, Mesh coneMesh)
    {
        var tree = new GameObject("Pine Tree");
        tree.transform.position = pos;

        float trunkH = 3.2f * scale;
        float trunkW = 0.32f * scale;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkW, trunkH * 0.5f, trunkW);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

        float baseRadius = 1.9f * scale;
        float layerH = 2.8f * scale;
        float y = trunkH * 0.85f; // 첫 단은 기둥 꼭대기 부근부터 시작
        for (int i = 0; i < 3; i++)
        {
            float radius = baseRadius * (1f - 0.25f * i);
            var layer = new GameObject("Foliage" + (i + 1));
            layer.transform.SetParent(tree.transform);
            layer.transform.localPosition = new Vector3(0f, y, 0f);
            layer.transform.localScale = new Vector3(radius, layerH, radius);
            layer.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            layer.AddComponent<MeshRenderer>().sharedMaterial = pineMat;
            y += layerH * 0.72f; // 겹치게 쌓아 단 사이 틈이 안 보이게
        }
    }

    // 밑면 반지름 1, 높이 1인 단위 원뿔 메시. 에셋으로 저장해야 씬 저장 후에도 유지된다.
    static Mesh MakeConeMesh()
    {
        const string path = "Assets/Meshes/PineCone.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const int seg = 12;
        var verts = new List<Vector3>();
        for (int i = 0; i < seg; i++)
        {
            float a = Mathf.PI * 2f * i / seg;
            verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        int apex = verts.Count;
        verts.Add(new Vector3(0f, 1f, 0f));

        var tris = new List<int>();
        for (int i = 0; i < seg; i++)
            tris.AddRange(new[] { apex, i, (i + 1) % seg });

        var mesh = new Mesh { name = "PineCone" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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
