using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Names = WorldConfig.Names;

// 처음 프로젝트를 열 때 Main 씬이 없으면 자동으로 만들어 준다.
// 파일은 있는데 아무 씬도 열려 있지 않은 채 시작한 경우(Untitled)에는 열어 준다.
// 씬 구성이 바뀌어 SceneVersion 이 올라가면 다음에 열 때 자동으로 다시 만든다.
// 메뉴 Hush Hunt > Setup Main Scene 으로 수동 재생성도 가능.
[InitializeOnLoad]
public static class SetupScene
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const int SceneVersion = 13; // 씬 구성을 바꾸면 1 올린다 (v13: 집 10채 확실히 배치)
    const string VersionKey = "HushHunt.SceneVersion";

    static SetupScene()
    {
        EditorApplication.delayCall += () => EnsureIfNeeded();
        // 플레이 모드 진입 시에도 도메인 리로드로 이 생성자가 다시 실행되는데,
        // 플레이 중에는 씬을 다시 만들 수 없으므로 에디터로 돌아왔을 때도 확인한다.
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                EnsureIfNeeded();
        };
    }

    static void EnsureIfNeeded()
    {
        bool outdated = EditorPrefs.GetInt(VersionKey, 0) < SceneVersion;
        EnsureScene(outdated);
    }

    [MenuItem("Hush Hunt/Setup Main Scene")]
    static void ForceCreate()
    {
        EnsureScene(true);
    }

    static void EnsureScene(bool force)
    {
        // 플레이 모드(또는 진입 직전)에서는 EditorSceneManager.NewScene/SaveScene을
        // 쓸 수 없다 (InvalidOperationException). 에디터 모드일 때만 만든다.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (!force && File.Exists(ScenePath))
        {
            // 아무 씬도 열려 있지 않은 채 시작한 경우(Untitled) Main을 열어 준다.
            // 저장하지 않은 Untitled 작업이 있으면 덮지 않고 그대로 둔다.
            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        // NewScene(Single)은 현재 열린 씬을 그대로 버린다. 저장하지 않은 수정이 있으면 먼저 묻는다.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 수동 수정 보호: 재생성으로 기존 씬을 덮어쓰기 전에 백업해 둔다.
        if (force && File.Exists(ScenePath))
        {
            string backupPath = ScenePath.Replace(".unity", ".backup.unity");
            File.Copy(ScenePath, backupPath, true);
            Debug.LogWarning("[Hush Hunt] 씬을 다시 만들기 전에 기존 씬을 백업했습니다: " + backupPath);
        }

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
        RenderSettings.fogColor = new Color(0.07f, 0.10f, 0.14f); // 안개가 살짝 밝아야 나무가 실루엣으로 보임 (NightAtmosphere와 같은 값)
        RenderSettings.fogDensity = 0.028f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.08f);

        // 흐린 달빛 느낌의 디렉셔널 라이트
        var lightGo = new GameObject("Moon Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.6f, 0.7f, 0.85f);
        light.intensity = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(32f, -30f, 0f); // 낮게 걸린 달: 그림자가 길고 하늘에서 달이 보인다

        // 땅: 내장 Plane(10x10m)을 40배 -> 400x400m, 어두운 흙색
        var groundMat = MakeMaterial("Ground", new Color(0.05f, 0.06f, 0.05f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = Names.Ground;
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // 큰 소나무들: 갈색 원기둥 기둥 + 짙은 초록 원뿔 3단. 키 크고 가는 실루엣.
        // 유니티에 원뿔 프리미티브가 없어서 단위 원뿔 메시를 만들어 에셋으로 저장해 쓴다.
        var trunkMat = MakeMaterial("Trunk", new Color(0.10f, 0.07f, 0.05f));
        var pineMat = MakeMaterial("Pine", new Color(0.03f, 0.09f, 0.05f));
        var coneMesh = MakeConeMesh();

        var rand = new System.Random(42);
        var props = new List<Vector3>(); // x, z, 대략 반경: 집 배치 때 겹침 방지용

        // 스폰: 네 모서리(벽에서 15m 안쪽) 중 하나 또는 정중앙. 모든 배치가 이 주변을 비운다.
        float corner = WorldConfig.Half - 15f;
        var spawnChoices = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(corner, corner), new Vector2(corner, -corner),
            new Vector2(-corner, corner), new Vector2(-corner, -corner),
        };
        var spawn = spawnChoices[rand.Next(spawnChoices.Length)];
        bool NearSpawn(float x, float z, float r) => new Vector2(x - spawn.x, z - spawn.y).magnitude < r;
        float spread = WorldConfig.Half - 5f; // 배치 가능 범위 (벽 안쪽)

        int placed = 0;
        int attempts = 0;
        while (placed < 320 && attempts < 1400)
        {
            attempts++;
            float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
            float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
            if (NearSpawn(x, z, 6f)) continue; // 스폰 지점 비우기

            float scale = 0.9f + (float)rand.NextDouble() * 0.7f; // 0.9~1.6배 크기
            MakePineTree(new Vector3(x, 0f, z), scale, trunkMat, pineMat, coneMesh);
            props.Add(new Vector3(x, z, 2.5f));
            placed++;
        }

        // 바위와 낡은 나무 울타리 (슬렌더 분위기 소품)
        var rockMat = MakeMaterial("Rock", new Color(0.08f, 0.09f, 0.09f));
        var fenceMat = MakeMaterial("Fence", new Color(0.09f, 0.07f, 0.05f));
        for (int i = 0; i < 50; i++)
        {
            float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
            float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
            if (NearSpawn(x, z, 6f)) continue;

            float s = 0.5f + (float)rand.NextDouble() * 1.5f;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = Names.Rock;
            rock.transform.position = new Vector3(x, s * 0.25f, z); // 살짝 묻힌 모양
            rock.transform.localScale = new Vector3(s * 1.4f, s * 0.8f, s);
            rock.transform.rotation = Quaternion.Euler(0f, (float)rand.NextDouble() * 360f, 0f);
            rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            props.Add(new Vector3(x, z, s * 0.7f + 0.5f));
        }

        // 낡은 울타리 두 토막: 삐뚤빼뚤한 기둥들 + 가로대 두 줄
        for (int f = 0; f < 4; f++)
        {
            float fx = (float)(rand.NextDouble() * 2.0) * spread * 0.7f - spread * 0.7f;
            float fz = (float)(rand.NextDouble() * 2.0) * spread * 0.7f - spread * 0.7f;
            if (NearSpawn(fx, fz, 8f)) { f--; continue; }

            var fence = new GameObject(Names.OldFence);
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
            props.Add(new Vector3(fx, fz, posts * 0.7f + 1f));
        }

        // 쓰러진 통나무와 그루터기: 숲 바닥에 변화를 주고, 낮은 엄폐물/점프 장애물이 된다.
        var logMat = MakeMaterial("Log", new Color(0.08f, 0.06f, 0.045f));
        for (int i = 0; i < 44; i++)
        {
            float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
            float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
            if (NearSpawn(x, z, 8f)) continue;

            float len = 2.5f + (float)rand.NextDouble() * 3f;
            float rad = 0.25f + (float)rand.NextDouble() * 0.2f;
            float yaw = (float)rand.NextDouble() * 360f;
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = Names.FallenLog;
            log.transform.position = new Vector3(x, rad * 0.9f, z);
            log.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
            log.transform.localScale = new Vector3(rad * 2f, len * 0.5f, rad * 2f);
            log.GetComponent<Renderer>().sharedMaterial = logMat;
            props.Add(new Vector3(x, z, len * 0.5f + 0.5f));
        }
        for (int i = 0; i < 60; i++)
        {
            float x = (float)(rand.NextDouble() * 2.0) * spread - spread;
            float z = (float)(rand.NextDouble() * 2.0) * spread - spread;
            if (NearSpawn(x, z, 8f)) continue;

            float hh = 0.25f + (float)rand.NextDouble() * 0.2f;   // 절반 높이
            float rr = 0.3f + (float)rand.NextDouble() * 0.25f;    // 반지름
            var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = Names.Stump;
            stump.transform.position = new Vector3(x, hh * 0.9f, z);
            stump.transform.localScale = new Vector3(rr * 2f, hh, rr * 2f);
            stump.GetComponent<Renderer>().sharedMaterial = logMat;
            props.Add(new Vector3(x, z, rr + 0.5f));
        }

        // 집들: 크기가 다른 속 빈 집 10채(최소 9채)를 맵 곳곳에 배치한다.
        // 문(폭 1.4m, 높이 2.4m)으로 출입 가능. 스폰 지점/다른 집/나무/바위/울타리와 안 겹치게.
        var wallMats = new[]
        {
            MakeMaterial("House A", new Color(0.09f, 0.07f, 0.06f)),
            MakeMaterial("House B", new Color(0.07f, 0.07f, 0.075f)),
            MakeMaterial("House C", new Color(0.10f, 0.08f, 0.05f)),
        };
        var roofMat = MakeMaterial("Roof", new Color(0.05f, 0.05f, 0.07f));
        var trimMat = MakeMaterial("Trim", new Color(0.04f, 0.03f, 0.025f));
        var floorMat = MakeMaterial("Floor", new Color(0.07f, 0.05f, 0.035f));
        var furnMat = MakeMaterial("Furniture", new Color(0.08f, 0.06f, 0.04f));
        var clothMat = MakeMaterial("Cloth", new Color(0.13f, 0.05f, 0.05f));
        var gableMesh = MakeGableMesh();
        var houseRects = new List<Vector4>(); // x, z, 반폭, 반깊이
        int housesPlaced = 0;
        int houseAttempts = 0;
        while (housesPlaced < 10 && houseAttempts < 1200)
        {
            houseAttempts++;
            float hw = 3f + (float)rand.NextDouble() * 3f; // 반폭 3~6m -> 폭 6~12m
            float hd = 3f + (float)rand.NextDouble() * 3f;
            float range = WorldConfig.Half - 14f - Mathf.Max(hw, hd); // 경계 벽과 여유 확보
            float hx = (float)(rand.NextDouble() * 2.0) * range - range;
            float hz = (float)(rand.NextDouble() * 2.0) * range - range;

            if (NearSpawn(hx, hz, 9f + Mathf.Max(hw, hd))) continue; // 스폰 비우기

            bool overlaps = false;
            foreach (var r in houseRects)
            {
                if (Mathf.Abs(hx - r.x) < hw + r.z + 3f && Mathf.Abs(hz - r.y) < hd + r.w + 3f)
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) continue;

            float houseRadius = Mathf.Sqrt(hw * hw + hd * hd);
            foreach (var p in props)
            {
                if (Vector2.Distance(new Vector2(hx, hz), new Vector2(p.x, p.y)) < houseRadius + p.z + 1f)
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) continue;

            float wallH = 3.5f + (float)rand.NextDouble() * 1.5f; // 벽 높이 3.5~5m
            MakeHouse(new Vector3(hx, 0f, hz), hw * 2f, hd * 2f, wallH,
                wallMats[rand.Next(wallMats.Length)], roofMat, trimMat, floorMat, furnMat, clothMat, gableMesh, rand);
            houseRects.Add(new Vector4(hx, hz, hw, hd));
            housesPlaced++;
        }

        // 맵 가장자리 벽: 400x400m 경계(±Half)를 어두운 벽 네 면으로 둘러싼다.
        // 땅과 같은 재질이라 안개 속에서 지형처럼 보이고, 미니맵에는 맵 경계로 보인다.
        // 벽 안쪽 면이 정확히 ±200m(Half)라 플레이어는 절대 맵 밖으로 못 나간다.
        float wallOffset = WorldConfig.Half + 0.5f;   // 벽 두께 1m의 중심: 안쪽 면이 ±Half
        float wallLength = WorldConfig.Half * 2f + 3f; // 모서리가 서로 겹쳐 틈이 없게
        MakeEdgeWall(new Vector3(0f, 2f, wallOffset), new Vector3(wallLength, 4f, 1f), groundMat);
        MakeEdgeWall(new Vector3(0f, 2f, -wallOffset), new Vector3(wallLength, 4f, 1f), groundMat);
        MakeEdgeWall(new Vector3(wallOffset, 2f, 0f), new Vector3(1f, 4f, wallLength), groundMat);
        MakeEdgeWall(new Vector3(-wallOffset, 2f, 0f), new Vector3(1f, 4f, wallLength), groundMat);

        // 플레이어: CharacterController + 카메라 자식 + 발소리
        var player = new GameObject("Player");
        player.transform.position = new Vector3(spawn.x, 1.1f, spawn.y); // 네 모서리 중 하나 또는 정중앙
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
        playerCam.nearClipPlane = 0.1f;
        playerCam.farClipPlane = 150f; // 짙은 안개로 먼 곳은 안 보이므로 그리지 않는다 (성능)
        camGo.AddComponent<AudioListener>();

        var fpc = player.AddComponent<FirstPersonController>();
        fpc.cameraPivot = camGo.transform;
        player.AddComponent<Footsteps>();

        // 미니맵: 실제 맵 데이터(경계/집/플레이어)를 그대로 그리는 오버레이.
        // 오른쪽 위 작은 지도가 기본, M 키로 큰 지도를 연다.
        var minimapGo = new GameObject("Minimap");
        minimapGo.AddComponent<Minimap>().target = player.transform;

        // 라운드 진행 관리자: 숨는 시간 -> 수색 타이머 -> 종료까지 한 판의 흐름
        new GameObject("GameManager").AddComponent<GameManager>();

        MarkStatic();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorPrefs.SetInt(VersionKey, SceneVersion);

        RegisterInBuildSettings();

        Debug.Log("[Hush Hunt] Main scene created at " + ScenePath + " (v" + SceneVersion + ")");
    }

    // 빌드 설정에 Main 씬이 없으면 맨 앞에 추가한다. 이미 등록된 다른 씬은 건드리지 않는다.
    static void RegisterInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == ScenePath) return;
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // 움직이지 않는 오브젝트(땅/집/바위/울타리/경계벽)는 정적 배칭 대상으로 표시해 렌더링 비용을 줄인다.
    // 나무는 시작할 때 모델로 교체되므로 제외한다.
    static void MarkStatic()
    {
        const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            // 집(Names.House)은 빼야 한다: 정적 배치로 굳으면 RandomizeLayout 이 위치를 바꿔도 화면의 집이 안 움직인다.
            if (n == Names.Ground || n == Names.Rock || n == Names.EdgeWall || n == Names.OldFence
                || n == Names.FallenLog || n == Names.Stump)
            {
                foreach (var child in t.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }
    }

    // 속 빈 집 한 채: 벽 + 문 개구부(+Z) + 양옆 창문 + 박공 지붕 + 문틀/현관 + 나무 바닥 + 가구.
    // 창문은 높이 1.1m라 총/시선은 통하지만 사람은 지나갈 수 없다.
    static void MakeHouse(Vector3 pos, float w, float d, float wallH,
        Material wallMat, Material roofMat, Material trimMat, Material floorMat,
        Material furnMat, Material clothMat, Mesh gableMesh, System.Random rand)
    {
        var house = new GameObject(Names.House);
        house.transform.position = pos;
        var hp = house.transform;

        const float t = 0.3f;     // 벽 두께
        const float doorW = 1.4f; // 문 폭 (플레이어 반경 0.4m보다 넉넉히)
        const float doorH = 2.4f; // 문 높이 (플레이어 키 2m보다 넉넉히)

        // 나무 바닥
        MakeBox(hp, new Vector3(0f, 0.03f, 0f), new Vector3(w - 2f * t, 0.06f, d - 2f * t), floorMat);

        // 앞벽(+Z): 문 개구부 좌우 두 조각 + 문 위 상인방
        float sideW = (w - doorW) * 0.5f;
        MakeBox(hp, new Vector3(-(w + doorW) * 0.25f, wallH * 0.5f, (d - t) * 0.5f), new Vector3(sideW, wallH, t), wallMat);
        MakeBox(hp, new Vector3((w + doorW) * 0.25f, wallH * 0.5f, (d - t) * 0.5f), new Vector3(sideW, wallH, t), wallMat);
        MakeBox(hp, new Vector3(0f, (doorH + wallH) * 0.5f, (d - t) * 0.5f), new Vector3(doorW, wallH - doorH, t), wallMat);
        // 뒷벽(-Z)
        MakeBox(hp, new Vector3(0f, wallH * 0.5f, -(d - t) * 0.5f), new Vector3(w, wallH, t), wallMat);
        // 좌우벽 (앞뒤 벽 사이에 맞춤), 각각 창문 하나
        MakeSideWall(hp, -(w - t) * 0.5f, d - 2f * t, wallH, t, wallMat, trimMat, -1f);
        MakeSideWall(hp, (w - t) * 0.5f, d - 2f * t, wallH, t, wallMat, trimMat, 1f);

        // 문틀(기둥 2 + 상인방)과 현관 계단
        float frontZ = d * 0.5f + 0.04f;
        MakeBox(hp, new Vector3(-(doorW * 0.5f + 0.07f), doorH * 0.5f, frontZ), new Vector3(0.14f, doorH, 0.10f), trimMat);
        MakeBox(hp, new Vector3((doorW * 0.5f + 0.07f), doorH * 0.5f, frontZ), new Vector3(0.14f, doorH, 0.10f), trimMat);
        MakeBox(hp, new Vector3(0f, doorH + 0.07f, frontZ), new Vector3(doorW + 0.28f, 0.14f, 0.10f), trimMat);
        MakeBox(hp, new Vector3(0f, 0.08f, d * 0.5f + 0.55f), new Vector3(doorW + 1.2f, 0.16f, 1.1f), trimMat);

        // 반쯤 열린 문짝 (절반 정도의 집에만): 경첩 쪽에서 안쪽으로 80도 열려 있다.
        if (rand.NextDouble() < 0.5)
        {
            var hinge = new GameObject("Door").transform;
            hinge.SetParent(hp, false);
            hinge.localPosition = new Vector3(-doorW * 0.5f + 0.02f, 0f, d * 0.5f - t - 0.05f);
            hinge.localRotation = Quaternion.Euler(0f, 80f, 0f);
            MakeBox(hinge, new Vector3((doorW - 0.1f) * 0.5f, doorH * 0.5f - 0.05f, 0f), new Vector3(doorW - 0.1f, doorH - 0.1f, 0.06f), trimMat);
        }

        // 박공 지붕: 경사 슬래브 2장 + 양끝 삼각형. 용마루는 X축 방향.
        float rise = Mathf.Min(2.4f, d * 0.32f);
        float halfD = d * 0.5f;
        float slope = rise / halfD;
        const float overhang = 0.3f;
        float eaveY = wallH - slope * overhang;
        float ridgeY = wallH + rise + 0.05f;
        float dz = halfD + overhang;
        float dy = ridgeY - eaveY;
        float slabLen = Mathf.Sqrt(dz * dz + dy * dy);
        float ang = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg;
        var roofMid = new Vector3(0f, (ridgeY + eaveY) * 0.5f, dz * 0.5f);
        var slabSize = new Vector3(w + 0.6f, 0.16f, slabLen);
        MakeBoxRot(hp, roofMid, slabSize, Quaternion.Euler(ang, 0f, 0f), roofMat);
        MakeBoxRot(hp, new Vector3(roofMid.x, roofMid.y, -roofMid.z), slabSize, Quaternion.Euler(-ang, 0f, 0f), roofMat);
        MakeGable(hp, new Vector3(-(w * 0.5f - t * 0.5f), wallH, 0f), d, rise, gableMesh, wallMat);
        MakeGable(hp, new Vector3((w * 0.5f - t * 0.5f), wallH, 0f), d, rise, gableMesh, wallMat);

        Furnish(hp, w, d, t, rand, furnMat, clothMat);

        house.AddComponent<HouseFootprint>().size = new Vector2(w, d);
    }

    // Z축 방향 옆벽 한 장. 가운데에 창문 구멍을 낸다. outward는 바깥쪽 방향(-1/+1).
    static void MakeSideWall(Transform parent, float x, float length, float wallH, float t,
        Material wallMat, Material trimMat, float outward)
    {
        const float winW = 1.3f, sill = 1.0f, winH = 1.1f;
        if (length < winW + 1.2f || wallH < sill + winH + 0.8f)
        {
            MakeBox(parent, new Vector3(x, wallH * 0.5f, 0f), new Vector3(t, wallH, length), wallMat);
            return;
        }

        float topH = wallH - sill - winH;
        float sideLen = (length - winW) * 0.5f;
        float off = (winW + sideLen) * 0.5f;
        // 창 아래 / 창 위 / 창 좌우
        MakeBox(parent, new Vector3(x, sill * 0.5f, 0f), new Vector3(t, sill, length), wallMat);
        MakeBox(parent, new Vector3(x, sill + winH + topH * 0.5f, 0f), new Vector3(t, topH, length), wallMat);
        MakeBox(parent, new Vector3(x, sill + winH * 0.5f, off), new Vector3(t, winH, sideLen), wallMat);
        MakeBox(parent, new Vector3(x, sill + winH * 0.5f, -off), new Vector3(t, winH, sideLen), wallMat);
        // 바깥쪽 창턱
        MakeBox(parent, new Vector3(x + outward * (t * 0.5f + 0.1f), sill - 0.03f, 0f), new Vector3(0.24f, 0.07f, winW + 0.3f), trimMat);
    }

    // 박공 삼각형 (양면). 단위 삼각형 메시를 집 깊이/지붕 높이에 맞게 늘려 쓴다.
    static void MakeGable(Transform parent, Vector3 localPos, float depth, float rise, Mesh mesh, Material mat)
    {
        var go = new GameObject("Gable");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        go.transform.localScale = new Vector3(depth, rise, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // 집 안 가구: 침대, 탁자, 상자, 선반. 문 앞 통로는 비워 둔다. 숨을 곳/엄폐물이 된다.
    static void Furnish(Transform house, float w, float d, float t, System.Random rand, Material wood, Material cloth)
    {
        float ix = w * 0.5f - t - 0.3f; // 안쪽 사용 가능 반폭
        float iz = d * 0.5f - t - 0.3f;

        // 침대 (뒤쪽 모서리 한쪽)
        float side = rand.NextDouble() < 0.5 ? -1f : 1f;
        MakeBox(house, new Vector3(side * (ix - 0.5f), 0.2f, -iz + 1.0f), new Vector3(1.0f, 0.4f, 2.0f), wood);
        MakeBox(house, new Vector3(side * (ix - 0.5f), 0.46f, -iz + 1.2f), new Vector3(0.9f, 0.14f, 1.5f), cloth);

        // 탁자 (뒤쪽 절반 안에서 임의 위치)
        float tx = Mathf.Lerp(-ix + 0.8f, ix - 0.8f, (float)rand.NextDouble());
        float tz = Mathf.Lerp(-iz + 0.6f, -0.3f, (float)rand.NextDouble());
        if (Mathf.Abs(tx - side * (ix - 0.5f)) < 1.4f) tx = -side * (ix - 1.2f); // 침대와 겹치지 않게
        MakeBox(house, new Vector3(tx, 0.76f, tz), new Vector3(1.2f, 0.08f, 0.8f), wood);
        float[] lx = { -0.52f, 0.52f };
        float[] lz = { -0.32f, 0.32f };
        foreach (float ox in lx)
            foreach (float oz in lz)
                MakeBox(house, new Vector3(tx + ox, 0.36f, tz + oz), new Vector3(0.08f, 0.72f, 0.08f), wood);

        // 상자 2~3개 (뒷벽 쪽)
        int crates = 2 + rand.Next(2);
        for (int i = 0; i < crates; i++)
        {
            float cs = 0.55f + (float)rand.NextDouble() * 0.35f;
            float cx = Mathf.Lerp(-ix + 0.4f, ix - 0.4f, (float)rand.NextDouble());
            MakeBox(house, new Vector3(cx, cs * 0.5f, -iz + cs * 0.5f), new Vector3(cs, cs, cs), wood);
        }

        // 선반 (창문 위치를 피해 옆벽 앞/뒤쪽)
        if (rand.NextDouble() < 0.7)
        {
            float shelfSide = -side;
            float sz = (rand.NextDouble() < 0.5 ? -1f : 1f) * Mathf.Min(iz - 0.8f, 1.3f + (float)rand.NextDouble());
            if (Mathf.Abs(sz) < 1.0f) sz = Mathf.Sign(sz == 0f ? 1f : sz) * 1.2f;
            MakeBox(house, new Vector3(shelfSide * (ix - 0.15f), 0.9f, sz), new Vector3(0.3f, 1.8f, 1.2f), wood);
        }
    }

    // 회전이 있는 박스 (지붕 경사면용)
    static void MakeBoxRot(Transform parent, Vector3 localPos, Vector3 size, Quaternion localRot, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = localRot;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 밑변 1, 높이 1의 양면 삼각형 메시 (박공벽용). 에셋으로 저장해야 씬 저장 후에도 유지된다.
    static Mesh MakeGableMesh()
    {
        const string path = "Assets/Meshes/GableTri.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var mesh = new Mesh { name = "GableTri" };
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 1f, 0f),
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 1f, 0f),
        });
        mesh.SetTriangles(new List<int> { 0, 1, 2, 3, 5, 4 }, 0);
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void MakeBox(Transform parent, Vector3 localPos, Vector3 size, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 가장자리 벽 하나: 큐브 프리미티브(메시 + BoxCollider)에 어두운 재질.
    static void MakeEdgeWall(Vector3 center, Vector3 size, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = Names.EdgeWall;
        wall.transform.position = center;
        wall.transform.localScale = size;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 소나무 한 그루: 기둥 위에 원뿔 3단 (아래로 갈수록 넓음). 총 높이 약 9.5m x scale.
    // 키 크고 가는 실루엣이 나오도록 기둥은 가늘고 수형은 높게.
    static void MakePineTree(Vector3 pos, float scale, Material trunkMat, Material pineMat, Mesh coneMesh)
    {
        var tree = new GameObject(Names.PineTree);
        tree.transform.position = pos;

        float trunkH = 3.2f * scale;
        float trunkW = WorldConfig.PineTrunkBaseWidth * scale;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = Names.PineTrunk;
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
            var layer = new GameObject(Names.PineFoliagePrefix + (i + 1));
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
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f); // 플라스틱처럼 번들거리지 않게
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
