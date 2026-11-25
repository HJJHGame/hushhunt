using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 리깅된 캐릭터 + 애니메이션 상태 머신.
// 우선순위: 사실적 인간 모델(Grab3D 후드 로그, CC0, Assets/Resources/CharacterReal) →
// KayKit Rogue Hooded(CC0, Assets/Resources/Character) → 프리미티브 몸(PlayerBody).
// 애니메이션: Quaternius Universal Animation Library(CC0, Assets/Resources/Animations/UAL1_Standard)
// 클립을 Humanoid 리타깃으로 재생한다.
// 씬 수정 없이 시작할 때 적용된다.
// 1인칭에서는 머리/목 뼈를 숨겨 카메라가 머리나 후드 안쪽을 보지 않게 하고,
// 아래를 보면 실제 걷는 다리가 보인다. 멀티플레이어 원격 캐릭터에는 SetFirstPerson(false).
//
// 카메라(눈) 위치는 FirstPersonController가 관리한다. 여기서는 건드리지 않는다.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(CharacterController))]
public class PlayerCharacter : MonoBehaviour
{
    public bool firstPerson = true;
    public float targetHeight = 1.8f; // 캐릭터 키 (m). 모델 원본 크기와 무관하게 맞춘다.
    public float modelYawOffset = 0f; // 모델이 뒤를 보고 걸으면 180으로 조정

    // ---- 리소스 경로 (Assets/Resources 기준) ----
    const string RealModelPath = "CharacterReal/hooded-rogue-rigged";
    const string RealBaseColorPath = "CharacterReal/rogue_basecolor";
    const string RealNormalPath = "CharacterReal/rogue_normal";
    const string UalPath = "Animations/UAL1_Standard";
    const string KayModelPath = "Character/Rogue_Hooded";
    const string KayTexturePath = "Character/rogue_texture";
    const string KayGeneralRigPath = "Character/Rig_Medium_General";
    const string KayBasicRigPath = "Character/Rig_Medium_MovementBasic";
    const string KayAdvancedRigPath = "Character/Rig_Medium_MovementAdvanced";

    // ---- 튜닝 상수 ----
    const float FadeSeconds = 0.18f;
    const float HiddenHeadScale = 0.0001f;
    const float HeadVisibleThreshold = 0.001f;
    const float MovingSpeedThreshold = 0.3f;     // 이 속도 미만이면 서 있는 것으로 본다
    const float MaxPlausibleSpeed = 30f;         // 이보다 빠르면 순간이동으로 보고 무시한다
    const float MinPlayRate = 0.6f;
    const float MaxPlayRate = 1.7f;
    const float StrafeDirectionThreshold = 0.6f; // 이동 방향의 좌우 성분이 이 이상이면 스트레이프
    const float BackwardDirectionThreshold = -0.5f;
    const float BackClipSpeedRatio = 0.625f;     // 뒷걸음 클립이 상정하는 속도 / 걷기 속도
    const float RealisticBackShift = 0.25f;      // 몸을 뒤로 빼는 거리 (사실적 모델은 몸이 두꺼워 더 뺀다)
    const float KayKitBackShift = 0.12f;
    const float FirstPersonNearClip = 0.05f;     // 카메라 바로 앞 몸 메시가 잘려 회색 내부가 보이는 것 방지
    const float SlowIdleRate = 0.06f;            // idle 클립이 없는 모델은 걷기를 이 배속으로 재생한다

    enum State { Idle, Walk, Back, StrafeL, StrafeR, Run, Air, CrouchIdle, CrouchMove }

    // 상태별로 쓸 애니메이션 클립 묶음. 모델/에셋 조합마다 하나씩 만든다.
    sealed class ClipSet
    {
        public AnimationClip idle, walk, walkBack, strafeL, strafeR, run, air, crouchIdle, crouchMove;
        public float idleRate = 1f;

        // 최소 요건: 서기/걷기/달리기만 있으면 모델을 쓸 수 있다.
        public bool HasCore { get { return idle != null && walk != null && run != null; } }

        // 없는 클립은 가장 가까운 것으로 대체한다.
        public void FillMissing()
        {
            if (walkBack == null) walkBack = walk;
            if (strafeL == null) strafeL = walk;
            if (strafeR == null) strafeR = walk;
            if (air == null) air = idle;
            if (crouchIdle == null) crouchIdle = idle;
            if (crouchMove == null) crouchMove = walk;
        }
    }

    FirstPersonController fpc;
    CharacterController controller;
    Transform modelRoot;
    Transform headBone;
    Transform neckBone;
    bool appliedFirstPerson;
    Vector3 lastPosition;

    // 클립은 FBX 하나에 여러 개가 들어 있어 Resources.LoadAll이 무겁다 (UAL은 20MB대).
    // 같은 경로를 여러 번 읽지 않게 시작하는 동안만 캐시한다.
    readonly Dictionary<string, AnimationClip[]> clipCache = new Dictionary<string, AnimationClip[]>();

    // 애니메이션: 믹서 입력 2개를 번갈아 쓰며 크로스페이드한다.
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    readonly AnimationClipPlayable[] slot = new AnimationClipPlayable[2];
    readonly bool[] slotUsed = new bool[2];
    readonly double[] slotLength = new double[2];
    int current;   // 페이드인 되는(주가 되는) 입력
    float fade = 1f;
    ClipSet clips;
    State state = State.Idle;

    // 씬을 다시 불러올 때(재시작/타이틀로) 런타임 머티리얼이 새지 않도록 OnDestroy에서 해제한다.
    readonly List<Material> runtimeMats = new List<Material>();

    // ------------------------------------------------------------------
    // 초기화
    // ------------------------------------------------------------------

    void Start()
    {
        fpc = GetComponent<FirstPersonController>();
        controller = GetComponent<CharacterController>();
        lastPosition = transform.position;

        GameObject prefab;
        ClipSet loaded;
        bool realistic = TryLoadRealistic(out prefab, out loaded);
        if (!realistic && !TryLoadKayKit(out prefab, out loaded))
        {
            // 에셋이 빠졌을 때의 안전망: 기존 프리미티브 몸
            clipCache.Clear();
            gameObject.AddComponent<PlayerBody>();
            enabled = false;
            return;
        }
        loaded.FillMissing();
        clips = loaded;
        clipCache.Clear();

        var model = SpawnModel(prefab, realistic);
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (realistic) ApplyRealisticMaterials(renderers);
        else ApplyKayKitMaterial(renderers);

        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        // Humanoid 임포트 이후 클립은 머슬 곡선이라 아바타 없이 재생하면 스키닝이 깨진다
        // (화면이 회색으로 깜빡이는 원인). 모델 FBX의 아바타를 명시적으로 연결한다.
        if (realistic && animator.avatar == null) AssignAvatar(animator);

        FindHeadBone(animator, model.transform);
        BuildGraph(animator);

        Debug.Log("[PlayerCharacter] 모델 적용: " + (realistic ? "후드 로그(사실적)" : "KayKit 도적")
            + " / 스케일 " + model.transform.localScale.x.ToString("F4")
            + " / 클립 idle=" + clips.idle.name + " walk=" + clips.walk.name + " run=" + clips.run.name);

        appliedFirstPerson = !firstPerson; // LateUpdate에서 강제 적용
    }

    // 사실적 모델 + UAL 애니메이션. 로그와 UAL 둘 다 Humanoid 임포트라 Unity 머슬 리타깃으로 재생된다.
    // back/strafe 클립은 UAL에 없어 walk로 대체된다.
    bool TryLoadRealistic(out GameObject prefab, out ClipSet result)
    {
        result = null;
        prefab = Resources.Load<GameObject>(RealModelPath);
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerCharacter] 후드 로그 모델 로드 실패: Resources/" + RealModelPath + " 를 찾지 못함");
            return false;
        }

        var ual = new ClipSet
        {
            idle = LoadClip(UalPath, "Idle_Loop"),
            walk = LoadClip(UalPath, "Walk_Loop"),
            run = LoadClip(UalPath, "Sprint_Loop"),
            air = LoadClip(UalPath, "Jump_Loop"),
            crouchIdle = LoadClip(UalPath, "Crouch_Idle_Loop"),
            crouchMove = LoadClip(UalPath, "Crouch_Fwd_Loop"),
        };
        if (ual.HasCore)
        {
            result = ual;
            return true;
        }

        // 어떤 클립이 들어 있는지 그대로 찍어 사용자가 보낼 수 있게 한다.
        var names = new StringBuilder();
        foreach (var c in LoadClips(UalPath)) names.Append(c.name).Append(' ');
        Debug.LogWarning("[PlayerCharacter] UAL 클립 로드 실패. 실제 클립 목록: " + names);

        // 폴백: 리깅 FBX 안의 walk/run 클립 (idle 없음, 걷기를 느리게 재생)
        var embedded = new ClipSet
        {
            walk = LoadClip(RealModelPath, "walk"),
            run = LoadClip(RealModelPath, "run"),
            crouchIdle = ual.crouchIdle,
            crouchMove = ual.crouchMove,
            idleRate = SlowIdleRate,
        };
        embedded.idle = embedded.walk;
        embedded.air = embedded.walk;
        if (embedded.HasCore)
        {
            result = embedded;
            return true;
        }

        prefab = null; // 클립이 안 읽히면 KayKit 경로로
        return false;
    }

    bool TryLoadKayKit(out GameObject prefab, out ClipSet result)
    {
        prefab = Resources.Load<GameObject>(KayModelPath);
        result = new ClipSet
        {
            idle = LoadClip(KayGeneralRigPath, "Idle_A"),
            walk = LoadClip(KayBasicRigPath, "Walking_A"),
            walkBack = LoadClip(KayAdvancedRigPath, "Walking_Backwards"),
            strafeL = LoadClip(KayAdvancedRigPath, "Running_Strafe_Left"),
            strafeR = LoadClip(KayAdvancedRigPath, "Running_Strafe_Right"),
            run = LoadClip(KayBasicRigPath, "Running_A"),
            air = LoadClip(KayBasicRigPath, "Jump_Idle"),
        };
        return prefab != null && result.HasCore;
    }

    // 모델을 만들고 키/발 위치/1인칭 보정을 적용한다.
    Transform SpawnModel(GameObject prefab, bool realistic)
    {
        var model = Instantiate(prefab, transform, false);
        model.name = "Character Model";
        modelRoot = model.transform;
        if (modelYawOffset != 0f)
            modelRoot.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);

        // 모델 원본 단위가 달라도 되도록 높이를 재서 목표 키에 맞춘다.
        var rends = model.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = CombinedBounds(rends);
            if (b.size.y > 0.0001f)
                modelRoot.localScale = Vector3.one * (targetHeight / b.size.y);
            // 스케일을 바꾼 뒤 다시 재서 발바닥을 컨트롤러 밑바닥에 맞춘다.
            b = CombinedBounds(rends);
            float footY = transform.position.y + controller.center.y - controller.height * 0.5f;
            modelRoot.position += Vector3.up * (footY - b.min.y);
        }

        // 1인칭 시점 보정:
        // - 몸 모델을 뒤로 빼서 카메라가 몸/후드 안쪽에 들어가지 않게 한다.
        // - near clip을 줄여 카메라 바로 앞의 몸 메시 앞면이 잘리지 않게 한다.
        modelRoot.localPosition += new Vector3(0f, 0f, -(realistic ? RealisticBackShift : KayKitBackShift));
        if (fpc != null && fpc.cameraPivot != null)
        {
            var cam = fpc.cameraPivot.GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = FirstPersonNearClip;
            else Debug.LogWarning("[PlayerCharacter] cameraPivot에서 Camera를 찾지 못함");
        }
        return modelRoot;
    }

    // Grab3D FBX는 텍스처를 포함하지 않고 제작자 PC의 절대 경로만 참조한다.
    // 그대로 두면 몸 전체가 회색으로 보이므로, 같은 패키지(GLB)에서 뽑은
    // 텍스처를 Resources에서 읽어 연결한다.
    void ApplyRealisticMaterials(Renderer[] rends)
    {
        var baseTex = Resources.Load<Texture2D>(RealBaseColorPath);
        var normalTex = Resources.Load<Texture2D>(RealNormalPath);
        if (baseTex == null)
            Debug.LogWarning("[PlayerCharacter] 후드 로그 텍스처 로드 실패: Resources/" + RealBaseColorPath);

        int patched = 0;
        foreach (var r in rends)
        {
            // 카메라 밖으로 나가도 그림자가 유지되게 (1인칭에서 자기 그림자용)
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;

            var shared = r.sharedMaterial;
            if (shared == null || shared.mainTexture != null) continue;

            var inst = r.material; // 인스턴스화해 원본 에셋은 건드리지 않는다
            if (baseTex != null)
            {
                inst.mainTexture = baseTex;
                inst.color = Color.white;
                if (normalTex != null) inst.SetTexture("_BumpMap", normalTex);
            }
            else if (shared.color.grayscale > 0.4f)
            {
                // 텍스처마저 없으면 밝은 회색 대신 어두운 색으로라도.
                inst.color = new Color(0.23f, 0.25f, 0.2f);
            }
            runtimeMats.Add(inst);
            patched++;
        }
        if (patched > 0)
            Debug.Log("[PlayerCharacter] 무텍스처 머티리얼 " + patched + "개에 텍스처를 연결했습니다.");
    }

    // 팔레트 텍스처: 임포트 설정과 무관하게 코드에서 확실하게 연결한다.
    void ApplyKayKitMaterial(Renderer[] rends)
    {
        var tex = Resources.Load<Texture2D>(KayTexturePath);
        var mat = RuntimeMaterials.CreateLit("Rogue", Color.white, 0.2f);
        if (tex != null) mat.mainTexture = tex;
        runtimeMats.Add(mat);
        foreach (var r in rends)
        {
            r.sharedMaterial = mat;
            // 카메라 밖으로 나가도 그림자가 유지되게 (1인칭에서 자기 그림자용)
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }
    }

    // 머리 뼈: 1인칭에서는 숨긴다 (카메라가 머리 안에 있으므로)
    void FindHeadBone(Animator animator, Transform model)
    {
        // Humanoid 아바타의 뼈 매핑이 이름 검색보다 확실하다 (리그마다 뼈 이름이 다르다).
        if (animator != null && animator.isHuman)
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null) headBone = FindDeep(model, "head");
        if (headBone == null) headBone = FindDeep(model, "Head");

        // 목 뼈도 같이 숨긴다: 후드가 머리가 아니라 목 뼈에 붙은 모델은 머리만 숨기면
        // 후드가 카메라를 감싼 채 남아 아래를 볼 때 화면이 회색으로 깜빡인다.
        if (animator != null && animator.isHuman)
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (neckBone == null && headBone != null && headBone.parent != null
            && headBone.parent.name.ToLowerInvariant().Contains("neck"))
            neckBone = headBone.parent;

        if (headBone != null) return;

        // 머리 뼈를 못 찾으면 1인칭에서 머리/두건 안쪽이 보여 화면이 회색이 된다.
        // 실제 뼈 이름을 알 수 있게 위층 뼈 이름을 그대로 찍는다.
        var bones = new StringBuilder();
        DumpNames(model, bones, 0, 3, 40);
        Debug.LogWarning("[PlayerCharacter] 머리 뼈(head/Head) 탐색 실패. 모델 구조: " + bones);
    }

    void AssignAvatar(Animator animator)
    {
        var avatars = Resources.LoadAll<Avatar>(RealModelPath);
        if (avatars.Length > 0) animator.avatar = avatars[0];
        else Debug.LogWarning("[PlayerCharacter] 로그 아바타를 찾지 못함: Humanoid 클립이 깨질 수 있음");
    }

    void BuildGraph(Animator animator)
    {
        graph = PlayableGraph.Create("PlayerCharacter");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        slot[0] = AnimationClipPlayable.Create(graph, clips.idle);
        slotLength[0] = clips.idle.length;
        slotUsed[0] = true;
        graph.Connect(slot[0], 0, mixer, 0);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        current = 0;
        state = State.Idle;
        graph.Play();
    }

    // ------------------------------------------------------------------
    // 매 프레임
    // ------------------------------------------------------------------

    void LateUpdate()
    {
        if (firstPerson != appliedFirstPerson) ApplyFirstPerson();
        // 애니메이션 클립에 뼈 스케일 키가 있으면 숨김이 매 프레임 되돌아갈 수 있어 계속 강제한다.
        if (firstPerson)
        {
            if (headBone != null && headBone.localScale.x > HeadVisibleThreshold)
                headBone.localScale = Vector3.one * HiddenHeadScale;
            if (neckBone != null && neckBone.localScale.x > HeadVisibleThreshold)
                neckBone.localScale = Vector3.one * HiddenHeadScale;
        }
        if (!graph.IsValid()) return;

        LoopClips();
        AdvanceFade();
        UpdateState();
    }

    // 클립이 끝까지 가면 멈추지 않게 수동으로 감는다 (임포트 루프 설정과 무관하게)
    void LoopClips()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!slotUsed[i] || slotLength[i] <= 0.0) continue;
            double t = slot[i].GetTime();
            if (t >= slotLength[i]) slot[i].SetTime(t % slotLength[i]);
        }
    }

    // 크로스페이드 진행
    void AdvanceFade()
    {
        if (fade >= 1f) return;
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

    // 이동 상태 -> 클립 선택.
    // 속도는 CharacterController.velocity 대신 실제 위치 변화로 잰다.
    // velocity는 마지막 Move 호출 값이 남아서, 일시정지로 FirstPersonController가 꺼지면
    // 걷던 속도가 그대로 남아 멈춘 채 걷는 모션이 계속 재생됐다.
    void UpdateState()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;

        float dt = Time.deltaTime;
        float speed = dt > 0f ? delta.magnitude / dt : 0f;
        if (speed > MaxPlausibleSpeed) speed = 0f; // 순간이동/리스폰

        State next = ChooseState(speed, delta);
        CrossTo(next, PlayRate(next, speed));
    }

    State ChooseState(float speed, Vector3 delta)
    {
        if (!controller.isGrounded) return State.Air;

        bool moving = speed >= MovingSpeedThreshold;
        if (fpc != null && fpc.IsCrouching)
            return moving ? State.CrouchMove : State.CrouchIdle;
        if (!moving) return State.Idle;
        if (fpc != null && fpc.IsSprinting) return State.Run;

        Vector3 local = transform.InverseTransformDirection(delta.normalized);
        if (local.x > StrafeDirectionThreshold) return State.StrafeR;
        if (local.x < -StrafeDirectionThreshold) return State.StrafeL;
        if (local.z < BackwardDirectionThreshold) return State.Back;
        return State.Walk;
    }

    AnimationClip ClipFor(State s)
    {
        switch (s)
        {
            case State.Walk: return clips.walk;
            case State.Back: return clips.walkBack;
            case State.StrafeL: return clips.strafeL;
            case State.StrafeR: return clips.strafeR;
            case State.Run: return clips.run;
            case State.Air: return clips.air;
            case State.CrouchIdle: return clips.crouchIdle;
            case State.CrouchMove: return clips.crouchMove;
            default: return clips.idle;
        }
    }

    // 클립이 상정하는 이동 속도. 실제 속도에 맞춰 재생 속도를 조절하는 기준이다.
    float NominalSpeed(State s)
    {
        float walk = fpc != null ? fpc.walkSpeed : 4f;
        switch (s)
        {
            case State.Run: return fpc != null ? fpc.sprintSpeed : 7f;
            case State.CrouchMove: return fpc != null ? fpc.crouchSpeed : 2f;
            case State.Back: return walk * BackClipSpeedRatio;
            default: return walk;
        }
    }

    float PlayRate(State s, float speed)
    {
        switch (s)
        {
            case State.Idle: return clips.idleRate;
            case State.Air:
            case State.CrouchIdle:
                return 1f;
            default:
                return Mathf.Clamp(speed / NominalSpeed(s), MinPlayRate, MaxPlayRate);
        }
    }

    void CrossTo(State next, float rate)
    {
        if (next == state)
        {
            slot[current].SetSpeed(rate);
            return;
        }
        state = next;

        var clip = ClipFor(next);
        int free = 1 - current;
        // 이전 페이드가 끝나기 전에 또 바뀌면 남은 슬롯을 먼저 정리한다.
        if (slotUsed[free])
        {
            graph.Disconnect(mixer, free);
            graph.DestroyPlayable(slot[free]);
        }
        slot[free] = AnimationClipPlayable.Create(graph, clip);
        slot[free].SetTime(0.0);
        slot[free].SetSpeed(rate);
        slotLength[free] = clip.length;
        graph.Connect(slot[free], 0, mixer, free);
        slotUsed[free] = true;
        current = free;
        fade = 0f;
    }

    // ------------------------------------------------------------------
    // 1인칭 / 정리
    // ------------------------------------------------------------------

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
        var s = firstPerson ? Vector3.one * HiddenHeadScale : Vector3.one;
        if (headBone != null) headBone.localScale = s;
        if (neckBone != null) neckBone.localScale = s;
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        if (modelRoot != null) Destroy(modelRoot.gameObject);
        foreach (var m in runtimeMats) if (m != null) Destroy(m);
        runtimeMats.Clear();
    }

    // ------------------------------------------------------------------
    // 헬퍼
    // ------------------------------------------------------------------

    static Bounds CombinedBounds(Renderer[] rends)
    {
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
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

    static void DumpNames(Transform t, StringBuilder sb, int depth, int maxDepth, int budget)
    {
        if (depth > maxDepth || budget <= 0) return;
        sb.Append(new string('>', depth)).Append(t.name).Append(' ');
        foreach (Transform c in t) DumpNames(c, sb, depth + 1, maxDepth, budget - 1);
    }

    AnimationClip[] LoadClips(string resourcePath)
    {
        AnimationClip[] all;
        if (!clipCache.TryGetValue(resourcePath, out all))
        {
            all = Resources.LoadAll<AnimationClip>(resourcePath);
            clipCache[resourcePath] = all;
        }
        return all;
    }

    AnimationClip LoadClip(string resourcePath, string clipName)
    {
        foreach (var c in LoadClips(resourcePath))
            // 임포트된 클립 이름은 "Armature|Idle_Loop"처럼 앞에 노드명이 붙을 수 있어 꼬리로 맞춘다
            if (c.name == clipName || c.name.EndsWith("|" + clipName)) return c;
        return null;
    }
}
