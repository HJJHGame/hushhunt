using UnityEngine;

// 1인칭 화면에 들고 있는 무기(소총/권총/칼)를 보여 준다. 단순 도형 조립이라 외부 모델이 필요 없다.
// 무기가 바뀌면 다시 만들고, 쏘면 살짝 뒤로 밀렸다 돌아온다. 충돌체와 그림자는 없다.
[RequireComponent(typeof(GunHolder))]
public class HeldWeapon : MonoBehaviour
{
    public Vector3 holdPos = new Vector3(0.26f, -0.24f, 0.5f); // 카메라 기준 위치(오른쪽 아래 앞)
    public float kickBack = 0.09f;   // 쏠 때 뒤로 밀리는 거리(m)
    public float kickSeconds = 0.12f;

    GunHolder gun;
    GameObject view;
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
        view.transform.localPosition = holdPos + new Vector3(0f, 0f, -kickBack * k);
    }

    GameObject Build(WeaponKind kind)
    {
        var root = new GameObject("Held " + kind);
        root.transform.SetParent(camT, false);
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
