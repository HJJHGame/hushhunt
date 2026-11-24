using UnityEngine;
using UnityEngine.Rendering;

// 프리미티브 임시 캐릭터. 로컬 플레이어는 다리/발만 보이고 상체는 그림자만 남긴다.
// 이후 원격 캐릭터에는 SetFirstPerson(false)를 호출하면 전신을 표시할 수 있다.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(CharacterController))]
public class PlayerBody : MonoBehaviour
{
    public bool firstPerson = true;
    public float legSwing = 24f;

    Footsteps footsteps;
    Transform modelRoot, leftLeg, rightLeg, leftArm, rightArm;
    Renderer[] upperBody;
    Material clothes, boots, skin;
    Vector2 swing;
    bool appliedFirstPerson;

    void Awake()
    {
        footsteps = GetComponent<Footsteps>();
        clothes = MakeMaterial("Player clothes", new Color(0.24f, 0.31f, 0.37f));
        boots = MakeMaterial("Player boots", new Color(0.09f, 0.10f, 0.12f));
        skin = MakeMaterial("Player skin", new Color(0.55f, 0.42f, 0.33f));

        var root = new GameObject("Character Model").transform;
        modelRoot = root;
        root.SetParent(transform, false);
        // Controller 높이 2m, 중심 0: 발바닥 -1m, 카메라 +0.6m.
        var torso = Part(root, "Torso", PrimitiveType.Cube,
            new Vector3(0f, 0.15f, -0.06f), new Vector3(0.48f, 0.60f, 0.28f), clothes);
        var head = Part(root, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 0.66f, -0.06f), new Vector3(0.32f, 0.36f, 0.32f), skin);
        leftLeg = Limb(root, "Left Leg", new Vector3(-0.17f, -0.12f, 0f), 0.72f, 0.20f, clothes);
        rightLeg = Limb(root, "Right Leg", new Vector3(0.17f, -0.12f, 0f), 0.72f, 0.20f, clothes);
        Part(leftLeg, "Left Boot", PrimitiveType.Cube,
            new Vector3(0f, -0.80f, 0.10f), new Vector3(0.23f, 0.16f, 0.42f), boots);
        Part(rightLeg, "Right Boot", PrimitiveType.Cube,
            new Vector3(0f, -0.80f, 0.10f), new Vector3(0.23f, 0.16f, 0.42f), boots);
        leftArm = Limb(root, "Left Arm", new Vector3(-0.34f, 0.40f, -0.06f), 0.60f, 0.16f, clothes);
        rightArm = Limb(root, "Right Arm", new Vector3(0.34f, 0.40f, -0.06f), 0.60f, 0.16f, clothes);
        // 후드, 배낭, 벨트, 손: 1인칭에선 그림자만 남지만 다른 플레이어 눈에는 윤곽이 또렷해진다.
        var hood = Part(root, "Hood", PrimitiveType.Sphere,
            new Vector3(0f, 0.70f, -0.12f), new Vector3(0.38f, 0.40f, 0.40f), clothes);
        var pack = Part(root, "Backpack", PrimitiveType.Cube,
            new Vector3(0f, 0.18f, -0.30f), new Vector3(0.36f, 0.46f, 0.18f), boots);
        var belt = Part(root, "Belt", PrimitiveType.Cube,
            new Vector3(0f, -0.13f, -0.06f), new Vector3(0.50f, 0.07f, 0.30f), boots);
        var leftHand = Part(leftArm, "Left Hand", PrimitiveType.Cube,
            new Vector3(0f, -0.64f, 0f), new Vector3(0.14f, 0.14f, 0.14f), skin);
        var rightHand = Part(rightArm, "Right Hand", PrimitiveType.Cube,
            new Vector3(0f, -0.64f, 0f), new Vector3(0.14f, 0.14f, 0.14f), skin);
        upperBody = new[]
        {
            torso, head, hood, pack, belt, leftHand, rightHand,
            leftArm.GetComponentInChildren<Renderer>(), rightArm.GetComponentInChildren<Renderer>()
        };
        SetFirstPerson(firstPerson);
    }

    void LateUpdate()
    {
        if (firstPerson != appliedFirstPerson) SetFirstPerson(firstPerson);
        if (footsteps == null) footsteps = GetComponent<Footsteps>();

        if (footsteps != null && footsteps.IsWalking)
        {
            Vector3 local = footsteps.LocalMoveDirection;
            float amount = Mathf.Cos(footsteps.GaitPhase) * legSwing;
            // 이동 중에는 보간하지 않는다. 발소리 순간에 포즈가 지연되지 않게.
            swing = new Vector2(local.z * amount, -local.x * amount);
        }
        else
        {
            swing = Vector2.Lerp(swing, Vector2.zero, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }
        leftLeg.localRotation = Quaternion.Euler(swing.x, 0f, swing.y);
        rightLeg.localRotation = Quaternion.Euler(-swing.x, 0f, -swing.y);
        leftArm.localRotation = Quaternion.Euler(-swing.x * 0.6f, 0f, -swing.y * 0.4f);
        rightArm.localRotation = Quaternion.Euler(swing.x * 0.6f, 0f, swing.y * 0.4f);
    }

    // 로컬 1인칭 카메라가 머리/몸통 안쪽을 보지 않도록 상체만 숨긴다.
    // 원격 모델 표시 전환 지점이며 네트워크 동기화 자체는 아직 포함하지 않는다.
    public void SetFirstPerson(bool value)
    {
        firstPerson = value;
        appliedFirstPerson = value;
        if (upperBody == null) return;
        foreach (var part in upperBody)
            if (part != null) part.shadowCastingMode = value ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
    }

    static Transform Limb(Transform parent, string name, Vector3 hip, float length, float width, Material material)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = hip;
        Part(pivot, name + " Mesh", PrimitiveType.Cube,
            new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, width), material);
        return pivot;
    }

    static Renderer Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        // 시각 전용. 충돌 판정은 기존 CharacterController만 사용한다.
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    static Material MakeMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        // 어두운 숲에서도 발 윤곽이 보일 정도의 약한 자체 발광.
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.12f);
        return material;
    }

    void OnDestroy()
    {
        if (modelRoot != null) Destroy(modelRoot.gameObject);
        if (clothes != null) Destroy(clothes);
        if (boots != null) Destroy(boots);
        if (skin != null) Destroy(skin);
    }
}
