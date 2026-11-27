using UnityEngine;
using UnityEngine.Rendering;

// 프리미티브로 만든 마인크래프트 비율의 블록형 캐릭터. 로컬 플레이어는 다리/발만 보이고 상체는 그림자만 남긴다.
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
    Material clothes, pants, boots, skin, hair;
    Vector2 swing;
    bool appliedFirstPerson;

    void Awake()
    {
        footsteps = GetComponent<Footsteps>();
        // 마인크래프트 비율의 블록 인간형 (단위 u = 약 5.8cm, 전체 32u = 약 1.86m).
        // 머리 8x8x8, 몸통 8x12x4, 팔/다리 4x12x4. 외부 에셋/텍스처 없이 기본 도형만 쓴다.
        const float u = 0.058f;
        clothes = MakeMaterial("Player shirt", new Color(0.16f, 0.58f, 0.62f));   // 청록 셔츠
        pants = MakeMaterial("Player pants", new Color(0.20f, 0.22f, 0.55f));     // 남색 바지
        boots = MakeMaterial("Player shoes", new Color(0.30f, 0.30f, 0.32f));     // 회색 신발
        skin = MakeMaterial("Player skin", new Color(0.72f, 0.52f, 0.40f));
        hair = MakeMaterial("Player hair", new Color(0.18f, 0.11f, 0.06f));

        var root = new GameObject("Character Model").transform;
        modelRoot = root;
        root.SetParent(transform, false);
        // 컨트롤러 높이 2m, 중심 0: 발바닥 -1m. 눈(카메라)은 +0.72m로 머리 안쪽 높이다.
        float feet = -1f;
        var torso = Part(root, "Torso", PrimitiveType.Cube,
            new Vector3(0f, feet + 18f * u, 0f), new Vector3(8f * u, 12f * u, 4f * u), clothes);
        var head = Part(root, "Head", PrimitiveType.Cube,
            new Vector3(0f, feet + 28f * u, 0f), new Vector3(8f * u, 8f * u, 8f * u), skin);
        var hairTop = Part(root, "Hair", PrimitiveType.Cube,
            new Vector3(0f, feet + 31.6f * u, -0.3f * u), new Vector3(8.3f * u, 1.6f * u, 8.3f * u), hair);
        var hairBack = Part(root, "Hair Back", PrimitiveType.Cube,
            new Vector3(0f, feet + 28.6f * u, -4f * u), new Vector3(8.3f * u, 6.6f * u, 0.6f * u), hair);
        var eyeL = Part(root, "Eye L", PrimitiveType.Cube,
            new Vector3(-2f * u, feet + 28.6f * u, 4.05f * u), new Vector3(1.6f * u, 1.2f * u, 0.3f * u), hair);
        var eyeR = Part(root, "Eye R", PrimitiveType.Cube,
            new Vector3(2f * u, feet + 28.6f * u, 4.05f * u), new Vector3(1.6f * u, 1.2f * u, 0.3f * u), hair);
        leftLeg = Limb(root, "Left Leg", new Vector3(-2f * u, feet + 12f * u, 0f), 12f * u, 4f * u, pants);
        rightLeg = Limb(root, "Right Leg", new Vector3(2f * u, feet + 12f * u, 0f), 12f * u, 4f * u, pants);
        Part(leftLeg, "Left Shoe", PrimitiveType.Cube,
            new Vector3(0f, -11f * u, 0.4f * u), new Vector3(4.2f * u, 2f * u, 4.8f * u), boots);
        Part(rightLeg, "Right Shoe", PrimitiveType.Cube,
            new Vector3(0f, -11f * u, 0.4f * u), new Vector3(4.2f * u, 2f * u, 4.8f * u), boots);
        leftArm = Limb(root, "Left Arm", new Vector3(-6f * u, feet + 24f * u, 0f), 12f * u, 4f * u, clothes);
        rightArm = Limb(root, "Right Arm", new Vector3(6f * u, feet + 24f * u, 0f), 12f * u, 4f * u, clothes);
        // 손: 팔 아래쪽 4u는 피부색
        var leftHand = Part(leftArm, "Left Hand", PrimitiveType.Cube,
            new Vector3(0f, -10f * u, 0f), new Vector3(4.05f * u, 4f * u, 4.05f * u), skin);
        var rightHand = Part(rightArm, "Right Hand", PrimitiveType.Cube,
            new Vector3(0f, -10f * u, 0f), new Vector3(4.05f * u, 4f * u, 4.05f * u), skin);
        upperBody = new[]
        {
            torso, head, hairTop, hairBack, eyeL, eyeR, leftHand, rightHand,
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
        Object.Destroy(part.GetComponent<Collider>());
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    static Material MakeMaterial(string name, Color color)
    {
        var material = RuntimeMaterials.CreateLit(name, color);
        // 어두운 숲에서도 발 윤곽이 보일 정도의 약한 자체 발광.
        RuntimeMaterials.SetEmission(material, color * 0.12f);
        return material;
    }

    void OnDestroy()
    {
        if (modelRoot != null) Destroy(modelRoot.gameObject);
        if (clothes != null) Destroy(clothes);
        if (boots != null) Destroy(boots);
        if (skin != null) Destroy(skin);
        if (pants != null) Destroy(pants);
        if (hair != null) Destroy(hair);
    }
}
