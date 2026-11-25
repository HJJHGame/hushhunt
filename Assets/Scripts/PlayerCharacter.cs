using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 리깅된 캐릭터 + 애니메이션 상태 머신.
// 우선순위: 사실적 인간 모델(Grab3D 후드 로그, CC0, Assets/Resources/CharacterReal) →
// KayKit Rogue Hooded(CC0, Assets/Resources/Character) → 프리미티브 몸(PlayerBody).
// 애니메이션: Quaternius Universal Animation Library(CC0, Assets/Resources/Animations/UAL1_Standard)
// 클립을 Humanoid 리타깃으로 재생한다.
// 씬 수정 없이 시작할 때 적용된다.
// 1인칭에서는 머리 뼈를 숨겨 카메라가 머리 안쪽을 보지 않게 하고,
// 아래를 보면 실제 걷는 다리가 보인다. 멀티플레이어 원격 캐릭터에는 SetFirstPerson(false).
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(CharacterController))]
public class PlayerCharacter : MonoBehaviour
{
    public bool firstPerson = true;
    public float targetHeight = 1.8f; // 캐릭터 키 (m). 모델 원본 크기와 무관하게 맞춘다.
    public float modelYawOffset = 0f; // 모델이 뒤를 보고 걸으면 180으로 조정

    const float FadeSeconds = 0.18f;

    Footsteps footsteps;
    FirstPersonController fpc;
    CharacterController controller;
    Transform modelRoot;
    Transform headBone;
    bool appliedFirstPerson;

    // 애니메이션: 믹서 입력 2개를 번갈아 쓰며 크로스페이드한다.
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    readonly AnimationClipPlayable[] slot = new AnimationClipPlayable[2];
    readonly bool[] slotUsed = new bool[2];
    readonly double[] slotLength = new double[2];
    int current;   // 페이드인 되는(주가 되는) 입력
    float fade = 1f;

    AnimationClip clipIdle, clipWalk, clipWalkBack, clipStrafeL, clipStrafeR, clipRun, clipAir, clipCrouchIdle, clipCrouchMove;
    string currentState = "";
    float idleSpeed = 1f; // 사실적 모델은 idle 클립이 없어 걷기를 느리게 재생한다

    void Start()
    {
        footsteps = GetComponent<Footsteps>();
        fpc = GetComponent<FirstPersonController>();
        controller = GetComponent<CharacterController>();

        // 사실적 인간 모델(Grab3D 후드 로그, CC0)을 우선 쓰고, 없으면 KayKit 도적,
        // 그것도 없으면 프리미티브 몸으로 되돌아간다.
        bool realistic = false;
        idleSpeed = 1f;
        var prefab = Resources.Load<GameObject>("CharacterReal/hooded-rogue-rigged");
        if (prefab == null)
            Debug.LogWarning("[PlayerCharacter] 후드 로그 모델 로드 실패: Resources/CharacterReal/hooded-rogue-rigged 를 찾지 못함");
        if (prefab != null)
        {
            // 애니메이션은 Quaternius UAL(CC0)에서 가져온다. 로그와 UAL 둘 다 Humanoid
            // 임포트라 Unity 머슬 리타깃으로 재생된다. back/strafe 클립은 UAL에 없어 walk 폴백.
            clipIdle = LoadClip("Animations/UAL1_Standard", "Idle_Loop");
            clipWalk = LoadClip("Animations/UAL1_Standard", "Walk_Loop");
            clipRun = LoadClip("Animations/UAL1_Standard", "Sprint_Loop"); // sprint 7f 상태용
            clipAir = LoadClip("Animations/UAL1_Standard", "Jump_Loop");
            clipCrouchIdle = LoadClip("Animations/UAL1_Standard", "Crouch_Idle_Loop");
            clipCrouchMove = LoadClip("Animations/UAL1_Standard", "Crouch_Fwd_Loop");
            if (clipIdle != null && clipWalk != null && clipRun != null)
            {
                realistic = true;
                clipWalkBack = clipWalk;
                clipStrafeL = clipWalk;
                clipStrafeR = clipWalk;
                if (clipAir == null) clipAir = clipIdle;
                idleSpeed = 1f;
            }
            else
            {
                // 어떤 클립이 들어 있는지 그대로 찍어 사용자가 보낼 수 있게 한다.
                var names = new System.Text.StringBuilder();
                foreach (var c in Resources.LoadAll<AnimationClip>("Animations/UAL1_Standard"))
                    names.Append(c.name).Append(' ');
                Debug.LogWarning("[PlayerCharacter] UAL 클립 로드 실패. 실제 클립 목록: " + names);
                // 폴백: 리깅 FBX 안의 walk/run 클립 (idle 없음, 걷기를 느리게 재생)
                clipWalk = LoadClip("CharacterReal/hooded-rogue-rigged", "walk");
                clipRun = LoadClip("CharacterReal/hooded-rogue-rigged", "run");
                if (clipWalk != null && clipRun != null)
                {
                    realistic = true;
                    clipIdle = clipWalk;
                    clipWalkBack = clipWalk;
                    clipStrafeL = clipWalk;
                    clipStrafeR = clipWalk;
                    clipAir = clipWalk;
                    idleSpeed = 0.06f;
                }
                else
                {
                    prefab = null; // 클립이 안 읽히면 KayKit 경로로
                }
            }
        }
        if (!realistic)
        {
            prefab = Resources.Load<GameObject>("Character/Rogue_Hooded");
            clipIdle = LoadClip("Character/Rig_Medium_General", "Idle_A");
            clipWalk = LoadClip("Character/Rig_Medium_MovementBasic", "Walking_A");
            clipWalkBack = LoadClip("Character/Rig_Medium_MovementAdvanced", "Walking_Backwards");
            clipStrafeL = LoadClip("Character/Rig_Medium_MovementAdvanced", "Running_Strafe_Left");
            clipStrafeR = LoadClip("Character/Rig_Medium_MovementAdvanced", "Running_Strafe_Right");
            clipRun = LoadClip("Character/Rig_Medium_MovementBasic", "Running_A");
            clipAir = LoadClip("Character/Rig_Medium_MovementBasic", "Jump_Idle");
        }

        if (prefab == null || clipIdle == null || clipWalk == null || clipRun == null)
        {
            // 에셋이 빠졌을 때의 안전망: 기존 프리미티브 몸
            gameObject.AddComponent<PlayerBody>();
            enabled = false;
            return;
        }
        // 일부 클립이 없으면 가장 가까운 것으로 대체
        if (clipWalkBack == null) clipWalkBack = clipWalk;
        if (clipStrafeL == null) clipStrafeL = clipWalk;
        if (clipStrafeR == null) clipStrafeR = clipWalk;
        if (clipAir == null) clipAir = clipIdle;
        if (clipCrouchIdle == null) clipCrouchIdle = clipIdle;
        if (clipCrouchMove == null) clipCrouchMove = clipWalk;

        var model = Instantiate(prefab, transform, false);
        model.name = "Character Model";
        modelRoot = model.transform;
        if (modelYawOffset != 0f)
            model.transform.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);

        // 모델 원본 단위가 달라도 되도록 높이를 재서 목표 키에 맞춘다.
        var rends = model.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.0001f)
                model.transform.localScale = Vector3.one * (targetHeight / b.size.y);
            // 스케일을 바꾼 뒤 다시 재서 발바닥을 컨트롤러 밑바닥에 맞춘다.
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float footY = transform.position.y - controller.height * 0.5f;
            model.transform.position += Vector3.up * (footY - b.min.y);
        }

        // 1인칭 시점 보정:
        // - 몸 모델을 뒤로 빼서 카메라가 몸/헬멧 안쪽에 들어가지 않게 한다.
        //   병사 모델은 플레이트 캐리어로 몸이 두꺼워 더 많이 뺀다.
        // - near clip을 줄여 카메라 바로 앞의 몸 메시 앞면이 잘리지 않게 한다
        //   (앞면이 잘리면 회색 내부가 보인다).
        // - 눈 높이를 실제 모델 키에 맞춘다(발바닥 기준 약 92%).
        float backShift = realistic ? 0.25f : 0.12f;
        model.transform.localPosition += new Vector3(0f, 0f, -backShift);
        if (fpc != null && fpc.cameraPivot != null)
        {
            var cam = fpc.cameraPivot.GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = 0.05f;
            else Debug.LogWarning("[PlayerCharacter] cameraPivot에서 Camera를 찾지 못함");
            if (firstPerson)
            {
                float eyeY = -controller.height * 0.5f + targetHeight * 0.92f;
                fpc.cameraPivot.localPosition = new Vector3(0f, eyeY, 0.12f);
            }
        }

        if (realistic)
        {
            // Grab3D FBX는 텍스처를 포함하지 않고 제작자 PC의 절대 경로만 참조한다.
            // 그대로 두면 몸 전체가 회색으로 보이므로, 같은 패키지(GLB)에서 뽑은
            // 텍스처를 Resources에서 읽어 연결한다.
            var baseTex = Resources.Load<Texture2D>("CharacterReal/rogue_basecolor");
            var normalTex = Resources.Load<Texture2D>("CharacterReal/rogue_normal");
            if (baseTex == null)
                Debug.LogWarning("[PlayerCharacter] 후드 로그 텍스처 로드 실패: Resources/CharacterReal/rogue_basecolor");
            foreach (var r in rends)
            {
                // 카메라 밖으로 나가도 그림자가 유지되게 (1인칭에서 자기 그림자용)
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (m.mainTexture == null)
                {
                    var inst = r.material; // 인스턴스화해 원본 에셋은 건드리지 않는다
                    if (baseTex != null)
                    {
                        inst.mainTexture = baseTex;
                        inst.color = Color.white;
                        if (normalTex != null) inst.SetTexture("_BumpMap", normalTex);
                    }
                    else if (m.color.grayscale > 0.4f)
                    {
                        // 텍스처마저 없으면 밝은 회색 대신 어두운 색으로라도.
                        inst.color = new Color(0.23f, 0.25f, 0.2f);
                    }
                    runtimeMats.Add(inst);
                    Debug.Log("[PlayerCharacter] 머티리얼 무텍스처 감지 -> 텍스처 연결: " + r.name + " / " + m.name);
                }
            }
        }
        else
        {
        // 팔레트 텍스처: 임포트 설정과 무관하게 코드에서 확실하게 연결한다.
        var tex = Resources.Load<Texture2D>("Character/rogue_texture");
        runtimeMat = new Material(Shader.Find("Standard"));
        var mat = runtimeMat;
        mat.name = "Rogue";
        if (tex != null) mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.2f);
        foreach (var r in rends)
        {
            r.sharedMaterial = mat;
            // 카메라 밖으로 나가도 그림자가 유지되게 (1인칭에서 자기 그림자용)
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }
        }

        // 머리 뼈: 1인칭에서는 숨긴다 (카메라가 머리 안에 있으므로)
        headBone = FindDeep(model.transform, "head");
        if (headBone == null) headBone = FindDeep(model.transform, "Head");
        // 머리 뼈를 못 찾으면 1인칭에서 머리/두건 안쪽이 보여 화면이 회색이 된다.
        // 실제 뼈 이름을 알 수 있게 위층 뼈 이름을 그대로 찍는다.
        if (headBone == null)
        {
            var bones = new System.Text.StringBuilder();
            DumpNames(model.transform, bones, 0, 3, 40);
            Debug.LogWarning("[PlayerCharacter] 머리 뼈(head/Head) 탐색 실패. 모델 구조: " + bones);
        }

        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        // Humanoid 임포트 이후 클립은 머슬 곡선이라 아바타 없이 재생하면 스키닝이 깨진다
        // (화면이 회색으로 깜빡이는 원인). 모델 FBX의 아바타를 명시적으로 연결한다.
        if (realistic && animator.avatar == null)
        {
            var avatars = Resources.LoadAll<Avatar>("CharacterReal/hooded-rogue-rigged");
            if (avatars.Length > 0) animator.avatar = avatars[0];
            else Debug.LogWarning("[PlayerCharacter] 로그 아바타를 찾지 못함: Humanoid 클립이 깨질 수 있음");
        }

        graph = PlayableGraph.Create("PlayerCharacter");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        slot[0] = AnimationClipPlayable.Create(graph, clipIdle);
        slotLength[0] = clipIdle.length;
        slotUsed[0] = true;
        graph.Connect(slot[0], 0, mixer, 0);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        current = 0;
        currentState = "idle";
        graph.Play();

        Debug.Log("[PlayerCharacter] 모델 적용: " + (realistic ? "후드 로그(사실적)" : "KayKit 도적")
            + " / 스케일 " + model.transform.localScale.x.ToString("F4")
            + " / 클립 idle=" + (clipIdle != null ? clipIdle.name : "없음")
            + " walk=" + (clipWalk != null ? clipWalk.name : "없음")
            + " run=" + (clipRun != null ? clipRun.name : "없음"));

        appliedFirstPerson = !firstPerson; // LateUpdate에서 강제 적용
    }

    void LateUpdate()
    {
        if (firstPerson != appliedFirstPerson) ApplyFirstPerson();
        // 애니메이션 클립에 뼈 스케일 키가 있으면 머리 숨김이 매 프레임 되돌아갈 수 있어 계속 강제한다.
        if (firstPerson && headBone != null && headBone.localScale.x > 0.001f)
            headBone.localScale = Vector3.one * 0.0001f;
        if (!graph.IsValid()) return;

        // 클립이 끝까지 가면 멈추지 않게 수동으로 감는다 (임포트 루프 설정과 무관하게)
        for (int i = 0; i < 2; i++)
        {
            if (!slotUsed[i] || slotLength[i] <= 0.0) continue;
            double t = slot[i].GetTime();
            if (t >= slotLength[i]) slot[i].SetTime(t % slotLength[i]);
        }

        // 크로스페이드 진행
        if (fade < 1f)
        {
            fade = Mathf.Min(1f, fade + Time.deltaTime / FadeSeconds);
            mixer.SetInputWeight(current, fade);
            mixer.SetInputWeight(1 - current, 1f - fade);
            if (fade >= 1f && slotUsed[1 - current])
            {
                // 다 섞였으면 이전 슬롯을 끊고 정리한다.
                graph.Disconnect(mixer, 1 - current);
                graph.DestroyPlayable(slot[1 - current]);
                slotUsed[1 - current] = false;
            }
        }

        // 이동 상태 -> 클립 선택
        Vector3 vel = controller.velocity;
        vel.y = 0f;
        float speed = vel.magnitude;

        AnimationClip clip;
        string state;
        float nominal; // 클립이 상정하는 이동 속도: 재생 속도를 실제 속도에 맞춘다
        if (!controller.isGrounded)
        {
            clip = clipAir; state = "air"; nominal = 1f;
        }
        else if (fpc != null && fpc.IsCrouching)
        {
            if (speed < 0.3f) { clip = clipCrouchIdle; state = "crouchIdle"; nominal = 1f; }
            else { clip = clipCrouchMove; state = "crouch"; nominal = 2f; }
        }
        else if (speed < 0.3f)
        {
            clip = clipIdle; state = "idle"; nominal = 1f;
        }
        else if (fpc != null && fpc.IsSprinting)
        {
            clip = clipRun; state = "run"; nominal = 7f;
        }
        else
        {
            Vector3 local = transform.InverseTransformDirection(vel / speed);
            if (local.x > 0.6f) { clip = clipStrafeR; state = "strafeR"; nominal = 4f; }
            else if (local.x < -0.6f) { clip = clipStrafeL; state = "strafeL"; nominal = 4f; }
            else if (local.z < -0.5f) { clip = clipWalkBack; state = "back"; nominal = 2.5f; }
            else { clip = clipWalk; state = "walk"; nominal = 4f; }
        }

        float playSpeed = nominal > 0.1f && state != "idle" && state != "air" && state != "crouchIdle"
            ? Mathf.Clamp(speed / nominal, 0.6f, 1.7f)
            : (state == "idle" ? idleSpeed : 1f);
        CrossTo(clip, state, playSpeed);
    }

    void CrossTo(AnimationClip clip, string state, float speed)
    {
        if (state == currentState)
        {
            slot[current].SetSpeed(speed);
            return;
        }
        currentState = state;

        int next = 1 - current;
        // 이전 페이드가 끝나기 전에 또 바뀌면 남은 슬롯을 먼저 정리한다.
        if (slotUsed[next])
        {
            graph.Disconnect(mixer, next);
            graph.DestroyPlayable(slot[next]);
        }
        slot[next] = AnimationClipPlayable.Create(graph, clip);
        slot[next].SetTime(0.0);
        slot[next].SetSpeed(speed);
        slotLength[next] = clip.length;
        graph.Connect(slot[next], 0, mixer, next);
        slotUsed[next] = true;
        current = next;
        fade = 0f;
    }

    // 로컬 1인칭에서는 머리를 숨겨 카메라 안쪽이 보이지 않게 한다.
    // 원격(다른 플레이어 화면)에서는 전신을 보이게 되돌린다.
    public void SetFirstPerson(bool value)
    {
        firstPerson = value;
        ApplyFirstPerson();
    }

    void ApplyFirstPerson()
    {
        appliedFirstPerson = firstPerson;
        if (headBone != null)
            headBone.localScale = firstPerson ? Vector3.one * 0.0001f : Vector3.one;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void DumpNames(Transform t, System.Text.StringBuilder sb, int depth, int maxDepth, int budget)
    {
        if (depth > maxDepth || budget <= 0) return;
        sb.Append(new string('>', depth)).Append(t.name).Append(' ');
        foreach (Transform c in t) DumpNames(c, sb, depth + 1, maxDepth, budget - 1);
    }

    static AnimationClip LoadClip(string resourcePath, string clipName)
    {
        var clips = Resources.LoadAll<AnimationClip>(resourcePath);
        foreach (var c in clips)
            // 임포트된 클립 이름은 "Armature|Idle_Loop"처럼 앞에 노드명이 붙을 수 있어 꼬리로 맞춘다
            if (c.name == clipName || c.name.EndsWith("|" + clipName)) return c;
        return null;
    }

    // 씬을 다시 불러올 때(재시작/타이틀로) 런타임 머티리얼이 새지 않도록 OnDestroy에서 해제한다.
    Material runtimeMat;
    readonly System.Collections.Generic.List<Material> runtimeMats = new System.Collections.Generic.List<Material>();

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (modelRoot != null) Destroy(modelRoot.gameObject);
        if (runtimeMat != null) Destroy(runtimeMat);
        foreach (var m in runtimeMats) if (m != null) Destroy(m);
        runtimeMats.Clear();
    }
}


