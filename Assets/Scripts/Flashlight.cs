using UnityEngine;

// 손전등 (F 키). 카메라에 붙은 스포트라이트. 가끔 깜빡인다.
// 그림자를 켜서 빛이 벽과 나무를 통과하지 않게 한다: 어둠 속에서 보이게 해주지만
// 빛이 켜져 있으면 멀리서도 눈에 띈다는 점이 PvP 숨바꼭질의 긴장 요소.
public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = true;
    public float intensity = 2.6f;
    public float range = 38f;
    public float spotAngle = 55f;

    Light lamp;
    AudioSource clickSource;
    MainMenu menu;

    // 메뉴는 GameSystems가 씬에 하나만 붙인다. 찾으면 캐시한다 (issue #28).
    bool MenuOpen()
    {
        if (menu == null) menu = FindFirstObjectByType<MainMenu>();
        return menu != null && menu.IsOpen;
    }

    AudioClip clickClip;
    Material lensMat;
    readonly System.Collections.Generic.List<Material> propMats = new System.Collections.Generic.List<Material>();

    public bool IsOn { get { return lamp != null && lamp.enabled; } }

    void Start()
    {
        Transform pivot = null;
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) pivot = fpc.cameraPivot;
        if (pivot == null && Camera.main != null) pivot = Camera.main.transform;
        if (pivot == null) { enabled = false; return; }

        // 왼손에 들린 손전등 모형: 카메라 왼쪽 아래에 간단한 프리미티브로 만든다.
        // KayKit 전신 애니메이션은 팔을 크게 흔들어 손 뼈에 붙이면 화면 밖으로 벗어나므로
        // 1인칭 소품 표준처럼 카메라 피벗에 붙인다.
        var prop = new GameObject("Flashlight Prop");
        prop.transform.SetParent(pivot, false);
        prop.transform.localPosition = new Vector3(-0.24f, -0.19f, 0.38f);
        prop.transform.localRotation = Quaternion.identity;
        BuildProp(prop.transform);

        var go = new GameObject("Flashlight");
        go.transform.SetParent(prop.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.13f);
        go.transform.localRotation = Quaternion.identity;

        lamp = go.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = range;
        lamp.spotAngle = spotAngle;
        lamp.innerSpotAngle = spotAngle * 0.5f;
        lamp.intensity = intensity;
        lamp.color = new Color(1f, 0.93f, 0.78f);
        // 스포트라이트 하나의 그림자라 비용이 크지 않고, 빛의 존재감이 크게 달라진다.
        lamp.shadows = LightShadows.Soft;
        lamp.shadowStrength = 0.95f;
        lamp.shadowBias = 0.05f;
        lamp.enabled = startOn;

        clickClip = MakeClick();
        clickSource = gameObject.AddComponent<AudioSource>();
        clickSource.spatialBlend = 0f;
        clickSource.playOnAwake = false;
    }

    void Update()
    {
        if (lamp == null) return;

        // 메뉴(타이틀/일시정지)가 열려 있으면 토글 입력만 무시한다. 깜빡임 연출은 유지.
        if (!MenuOpen() && Input.GetKeyDown(toggleKey))
        {
            lamp.enabled = !lamp.enabled;
            clickSource.PlayOneShot(clickClip, 0.6f);
        }

        if (!lamp.enabled) { SetLens(false); return; }
        SetLens(true);

        // ~10초 간격의 큰 강도 하락(75%)은 화면이 회색으로 깜빡이는 것처럼 보인다는
        // 보고가 있어 제거. 미세한 밝기 떨림만 남긴다.
        lamp.intensity = intensity * (1f + 0.03f * Mathf.Sin(Time.time * 23f));
    }

    // 켜지면 렌즈가 발광하고 꺼지면 꺼진다.
    void SetLens(bool on)
    {
        if (lensMat != null)
            lensMat.SetColor("_EmissionColor", on ? new Color(1f, 0.93f, 0.7f) * 1.6f : Color.black);
    }

    void BuildProp(Transform root)
    {
        // 몸통: 길이 16cm, 지름 3cm의 어두운 금속 통
        var bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = new Color(0.13f, 0.13f, 0.14f);
        bodyMat.SetFloat("_Glossiness", 0.55f);
        propMats.Add(bodyMat);
        AddPart(root, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.08f, 0.03f), bodyMat);

        // 머리(베젤): 지름 5cm, 살짝 넓어진다
        AddPart(root, "Head", new Vector3(0f, 0f, 0.09f), new Vector3(0.05f, 0.03f, 0.05f), bodyMat);

        // 렌즈: 손전등 상태에 따라 발광한다
        lensMat = new Material(Shader.Find("Standard"));
        lensMat.EnableKeyword("_EMISSION");
        propMats.Add(lensMat);
        AddPart(root, "Lens", new Vector3(0f, 0f, 0.122f), new Vector3(0.042f, 0.006f, 0.042f), lensMat);

        // 모형 그림자는 끈다 (손전등 빛에 자기 그림자가 생기는 것 방지)
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void AddPart(Transform root, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        Destroy(part.GetComponent<Collider>()); // 프리미티브 기본 콜라이더는 필요 없다
        part.transform.SetParent(root, false);
        part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 통 길이 방향을 앞(Z)으로
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void OnDestroy()
    {
        if (clickClip != null) Destroy(clickClip);
        foreach (var m in propMats) if (m != null) Destroy(m);
        propMats.Clear();
    }

    static AudioClip MakeClick()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.06f);
        var s = new float[n];
        var r = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            s[i] = (noise(r) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f) * Mathf.Exp(-t * 90f);
        }
        var clip = AudioClip.Create("FlashlightClick", n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static float noise(System.Random r)
    {
        return (float)r.NextDouble() * 2f - 1f;
    }
}
