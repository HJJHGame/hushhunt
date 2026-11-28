using UnityEngine;

// 1인칭 화면에 들고 있는 무기(소총/권총/칼)를 보여 준다. Poly Haven(CC0) 모델을 쓰고, 파일이 없으면 단순 도형 조립으로 대신한다.
// 모델 파일: Assets/Models/weapons/pistol.glb, rifle.glb, machete.glb (glTFast 필요). 빌드에서는 Resources/Models/weapons/<이름>(확장자 없이).
// 세 파일 모두 총구/칼끝이 +Z(앞), 위가 +Y, 손잡이 중심이 원점이 되게 맞춰 두었다.
// 무기가 바뀌면 다시 만들고, 쏘면 살짝 뒤로 밀렸다 돌아온다. 충돌체와 그림자는 없다.
[RequireComponent(typeof(GunHolder))]
public class HeldWeapon : MonoBehaviour
{
    public Vector3 holdPos = new Vector3(0.26f, -0.24f, 0.5f); // 카메라 기준 위치(오른쪽 아래 앞)
    public float kickBack = 0.09f;   // 쏠 때 뒤로 밀리는 거리(m)
    public float kickSeconds = 0.12f;

    // 모델별 조절값: 카메라 기준 손 위치 보정, 회전(도), 크기 배율.
    public Vector3 pistolOffset = new Vector3(0f, -0.04f, 0.1f);
    public Vector3 rifleOffset = new Vector3(0f, 0f, 0.15f);
    public Vector3 macheteOffset = new Vector3(-0.04f, 0.04f, 0.05f); // 오른쪽 아래, 칼날 전체가 화면에 들어오게
    public Vector3 pistolEuler = new Vector3(-2f, -4f, 0f);
    public Vector3 rifleEuler = new Vector3(0f, -4f, 0f);
    public Vector3 macheteEuler = new Vector3(-25f, -8f, 15f); // 칼날이 앞으로 살짝 위를 향하게 (x가 음수일수록 칼끝이 위로)
    public float pistolScale = 1f;
    public float rifleScale = 0.8f; // 실제 길이 1.2m라 화면을 가로지르지 않게 줄인다
    public float macheteScale = 0.7f; // 원본 길이 약 0.75m라 화면을 덮지 않게 줄인다

    GunHolder gun;
    GameObject view;
    Vector3 viewBase; // 반동이 없을 때의 카메라 기준 위치
    WeaponKind shown = WeaponKind.None;
    Transform camT;

    void Awake() { gun = GetComponent<GunHolder>(); }

    void LateUpdate()
    {
        if (GetComponent<FirstPersonController>() == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (camT != cam.transform) { camT = cam.transform; if (view != null) Destroy(view); view = null; shown = WeaponKind.None; }

        WeaponKind want = gun.HasGun && gun.IsAlive ? gun.Weapon : WeaponKind.None;
        if (want != shown)
        {
            if (view != null) Destroy(view);
            view = want == WeaponKind.None ? null : Build(want);
            shown = want;
        }
        if (view == null) return;

        // 메뉴가 열려 있으면 숨긴다.
        view.SetActive(!MainMenu.IsMenuOpen);

        float since = Time.unscaledTime - gun.LastShotAt;
        float k = since < kickSeconds ? 1f - since / kickSeconds : 0f;
        view.transform.localPosition = viewBase + new Vector3(0f, 0f, -kickBack * k);
    }

    static GameObject LoadModel(string file)
    {
#if UNITY_EDITOR
        string path = "Assets/Models/weapons/" + file + ".glb";
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null) return asset;
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var go = o as GameObject;
            if (go != null && go.transform.parent == null) return go;
        }
#endif
        return Resources.Load<GameObject>("Models/weapons/" + file);
    }

    // 모델이 있으면 그걸 붙인 루트를 돌려준다. 없으면 null (옛 도형 조립으로 대신).
    GameObject BuildModel(WeaponKind kind)
    {
        string file = kind == WeaponKind.Rifle ? "rifle" : kind == WeaponKind.Pistol ? "pistol" : "machete";
        var prefab = LoadModel(file);
        if (prefab == null) return null;
        Vector3 off = kind == WeaponKind.Rifle ? rifleOffset : kind == WeaponKind.Pistol ? pistolOffset : macheteOffset;
        Vector3 eul = kind == WeaponKind.Rifle ? rifleEuler : kind == WeaponKind.Pistol ? pistolEuler : macheteEuler;
        float sc = kind == WeaponKind.Rifle ? rifleScale : kind == WeaponKind.Pistol ? pistolScale : macheteScale;
        var root = new GameObject("Held " + kind);
        root.transform.SetParent(camT, false);
        viewBase = holdPos + off;
        root.transform.localPosition = viewBase;
        root.transform.localRotation = Quaternion.Euler(eul);
        var m = Instantiate(prefab, root.transform);
        m.name = "Model";
        m.transform.localPosition = Vector3.zero;
        m.transform.localRotation = Quaternion.identity;
        m.transform.localScale = Vector3.one * sc;
        foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return root;
    }

    GameObject Build(WeaponKind kind)
    {
        var modelRoot = BuildModel(kind);
        if (modelRoot != null) return modelRoot;
        var root = new GameObject("Held " + kind);
        root.transform.SetParent(camT, false);
        viewBase = holdPos;
        root.transform.localPosition = holdPos;
        root.transform.localRotation = Quaternion.Euler(0f, -4f, 0f);

        Color metal = new Color(0.12f, 0.12f, 0.13f), wood = new Color(0.30f, 0.18f, 0.09f), steel = new Color(0.55f, 0.57f, 0.6f);
        switch (kind)
        {
            case WeaponKind.Rifle:
                Part(root, "Body", new Vector3(0f, 0f, 0.15f), new Vector3(0.06f, 0.09f, 0.5f), metal);
                Part(root, "Barrel", new Vector3(0f, 0.015f, 0.55f), new Vector3(0.025f, 0.025f, 0.35f), metal);
                Part(root, "Stock", new Vector3(0f, -0.02f, -0.18f), new Vector3(0.05f, 0.1f, 0.22f), wood);
                Part(root, "Mag", new Vector3(0f, -0.11f, 0.12f), new Vector3(0.04f, 0.12f, 0.07f), metal);
                break;
            case WeaponKind.Pistol:
                Part(root, "Slide", new Vector3(0f, 0.02f, 0.2f), new Vector3(0.04f, 0.05f, 0.24f), metal);
                Part(root, "Grip", new Vector3(0f, -0.06f, 0.1f), new Vector3(0.04f, 0.12f, 0.06f), wood);
                break;
            default: // Knife
                root.transform.localRotation = Quaternion.Euler(-70f, -10f, 10f);
                Part(root, "Blade", new Vector3(0f, 0f, 0.22f), new Vector3(0.015f, 0.05f, 0.3f), steel);
                Part(root, "Handle", new Vector3(0f, 0f, -0.02f), new Vector3(0.03f, 0.035f, 0.14f), wood);
                break;
        }
        return root;
    }

    static void Part(GameObject parent, string name, Vector3 pos, Vector3 scale, Color c)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        p.transform.SetParent(parent.transform, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        Destroy(p.GetComponent<Collider>());
        var r = p.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        r.sharedMaterial = new Material(sh) { color = c };
    }

    void OnDestroy() { if (view != null) Destroy(view); }
}
