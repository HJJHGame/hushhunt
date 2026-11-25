using UnityEngine;

// 나무 한 그루에 깨끗한 흰색 A4 용지 한 장을 붙인다 (슬렌더 페이지). 매 판 무작위 나무/무작위 방향.
// 가까이 가면 용지가 어둠 속에서 환하게 빛나 눈에 띄고, E를 누르면 줍는다. 이 게임의 목표.
public class TreePaper : MonoBehaviour
{
    // 종이 소리는 잠시 꺼둠. 다시 켜려면 false를 true로 바꾼다.
    static bool PAPER_SOUND = false;

    GameObject paper;
    Renderer paperRenderer;
    Material litMat;
    Material glowMat;
    bool glowing;
    Transform playerT;
    GUIStyle style;
    bool taken;
    float takenAt;
    AudioClip rustle;

    void Start()
    {
        var trees = new System.Collections.Generic.List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.parent == null && t.name == WorldConfig.Names.PineTree) trees.Add(t);
        if (trees.Count == 0) return;

        var rand = new System.Random();
        var tree = trees[rand.Next(trees.Count)];

        // 줄기 굵기에 맞춰 껍질에서 살짝 띄운다.
        float radius = 0.25f;
        var trunk = tree.Find(WorldConfig.Names.PineTrunk);
        if (trunk != null) radius = trunk.localScale.x * 0.5f + 0.03f;

        float yaw = (float)rand.NextDouble() * 360f;
        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // 얇은 상자: 어느 쪽에서 봐도 보인다 (Quad는 한쪽 면만 보임).
        paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "A4 Page";
        Destroy(paper.GetComponent<Collider>()); // 걸리적거리지 않게
        paper.transform.SetParent(tree, false);
        paper.transform.localPosition = new Vector3(dir.x * radius, 1.5f, dir.z * radius);
        paper.transform.localRotation = Quaternion.Euler(
            (float)(rand.NextDouble() * 8.0 - 4.0), yaw, (float)(rand.NextDouble() * 6.0 - 3.0));
        paper.transform.localScale = new Vector3(0.21f, 0.297f, 0.003f); // A4 (m)

        // 무늬 없는 깨끗한 흰색. glowMat는 빛이 없어도 항상 밝은 흰색 (가까이 왔을 때 표시용).
        litMat = RuntimeMaterials.CreateLit("A4 Page", new Color(0.98f, 0.98f, 0.96f), 0.02f);
        var unlit = Shader.Find("Unlit/Color");
        if (unlit == null) unlit = Shader.Find("Unlit/Texture");
        glowMat = new Material(unlit) { name = "A4 Page Glow", color = Color.white };
        paperRenderer = paper.GetComponent<Renderer>();
        paperRenderer.sharedMaterial = litMat;
        paperRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (PAPER_SOUND)
        {
            // 종이 바스락 소리. 완전 3D라 멀리서도 어느 방향인지 들리고, 가까워질수록 커진다.
            rustle = MakeRustle();
            var src = paper.AddComponent<AudioSource>();
            src.clip = rustle;
            src.loop = true;
            src.spatialBlend = 1f; // 완전 3D
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;  // 이 안에서는 최대 음량
            src.maxDistance = 30f; // 30m 밖에서는 들리지 않는다
            src.dopplerLevel = 0f;
            src.volume = 0.9f;
            src.Play();
        }

        Debug.Log("[HushHunt] A4 용지 부착: " + tree.name + " 위치 (" + tree.position.x.ToString("0.0")
            + ", " + tree.position.z.ToString("0.0") + ")");
    }

    // 가까이 가면 용지가 눈에 띄고, 2m 안에서 E를 누르면 줍는다.
    void Update()
    {
        if (taken || paper == null) return;
        if (playerT == null)
        {
            var f = FindFirstObjectByType<FirstPersonController>();
            if (f != null) playerT = f.transform;
            return;
        }
        if (MainMenu.IsMenuOpen) return;
        Vector3 d = paper.transform.position - playerT.position;
        d.y = 0f;
        float dist = d.magnitude;

        // 가까이 오면 용지만 환하게 빛나 어둠 속에서 눈에 띈다. 빛을 쏘지 않아 주변은 밝아지지 않는다.
        bool near = dist < 8f;
        if (near != glowing)
        {
            glowing = near;
            paperRenderer.sharedMaterial = near ? glowMat : litMat;
        }

        if (dist < 2.2f && Input.GetKeyDown(KeyCode.E))
        {
            taken = true;
            takenAt = Time.unscaledTime;
            Destroy(paper);
            Debug.Log("[HushHunt] A4 용지를 찾았다!");
        }
    }

    // 근처에 오면 "용지 줍기 [E]", 주우면 잠시 "찾았다" 표시. 검정 화면에서도 읽히게 그림자를 깐다.
    void OnGUI()
    {
        if (MainMenu.IsMenuOpen) return;
        if (Event.current.type != EventType.Repaint) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            style.fontSize = Mathf.RoundToInt(20f * Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f));
        }
        if (taken)
        {
            if (Time.unscaledTime - takenAt < 6f) ShadowText("A4 용지를 찾았다!");
            return;
        }
        if (paper == null || playerT == null) return;
        Vector3 d = paper.transform.position - playerT.position;
        d.y = 0f;
        if (d.magnitude < 2.2f) ShadowText("용지 줍기 [E]");
    }

    void ShadowText(string msg)
    {
        var r = new Rect(0f, Screen.height * 0.62f, Screen.width, 40f);
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), msg, style);
        GUI.color = Color.white;
        GUI.Label(r, msg, style);
    }

    void OnDestroy()
    {
        if (rustle != null) Destroy(rustle);
        if (glowMat != null) Destroy(glowMat);
    }

    // 6초짜리 종이 바스락 소리. 바람에 펄럭이는 종이: 중역 노이즈 + 빠른 떨림 + 가끔 큰 펄럭임.
    // 모든 진동이 6초의 정수배 주기라 반복해도 자연스럽고, 앞뒤 크로스페이드로 이음매를 지운다
    // (AmbientSound.MakeWind 와 같은 방식).
    static AudioClip MakeRustle()
    {
        const int rate = 22050;
        const int n = rate * 6;
        const int xf = rate / 2;
        var buf = new float[n + xf];
        var r = new System.Random(7);
        float lo = 0f, mid = 0f, hi = 0f;

        for (int i = 0; i < n + xf; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lo += (noise - lo) * 0.02f;
            mid += (noise - mid) * 0.12f;
            hi += (noise - hi) * 0.45f;
            // 플러터: 초당 약 11번 떨림 (6초에 66주기 = 정수배)
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 66f * t / 6f
                + Mathf.Sin(2f * Mathf.PI * 6f * t / 6f));
            // 펄럭임: 6초에 3번 살짝 더 크게 (정수배)
            float flap = 1f + 0.8f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 3f * t / 6f + 0.7f));
            buf[i] = ((mid - lo) * 1.1f + hi * 0.5f) * flutter * flap;
        }

        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = buf[i];
        for (int i = 0; i < xf; i++)
            s[i] = Mathf.Lerp(buf[n + i], buf[i], i / (float)xf);

        float max = 0.001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
        float k = 0.8f / max;
        for (int i = 0; i < n; i++) s[i] *= k;

        var clip = AudioClip.Create("Paper Rustle", n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }
}
