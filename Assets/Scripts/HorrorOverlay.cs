using UnityEngine;

// 화면 전체 연출: 어두운 비네팅 + 필름 그레인.
// 포스트 프로세싱 패키지 없이 OnGUI로 그리므로 어떤 렌더 파이프라인에서도 동작한다.
// 달리는 중에는 비네팅이 숨 가쁜 것처럼 맥동한다.
public class HorrorOverlay : MonoBehaviour
{
    public float vignetteStrength = 0.85f;
    public float grainStrength = 0.04f;

    Texture2D vignette;
    Texture2D[] grain;
    FirstPersonController fpc;
    float grainTimer;
    int grainIndex;
    Rect grainUv = new Rect(0f, 0f, 1f, 1f);

    void Awake()
    {
        vignette = BuildVignette(128);
        grain = new Texture2D[4];
        for (int i = 0; i < grain.Length; i++) grain[i] = BuildGrain(64, 100 + i);
    }

    void Start()
    {
        fpc = GetComponent<FirstPersonController>();
    }

    void OnDestroy()
    {
        if (vignette != null) Destroy(vignette);
        if (grain != null)
            foreach (var g in grain) if (g != null) Destroy(g);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        GUI.depth = 100; // 다른 UI보다 뒤에 그린다
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        Color prev = GUI.color;

        float strength = vignetteStrength;
        if (fpc != null && fpc.IsSprinting)
            strength = Mathf.Min(1f, strength * (1.1f + 0.12f * Mathf.Sin(Time.time * 9f)));
        GUI.color = new Color(1f, 1f, 1f, strength);
        GUI.DrawTexture(screen, vignette, ScaleMode.StretchToFill);

        grainTimer -= Time.unscaledDeltaTime;
        if (grainTimer <= 0f)
        {
            grainTimer = 1f / 14f;
            grainIndex = Random.Range(0, grain.Length);
            grainUv = new Rect(Random.value, Random.value, Screen.width / 128f, Screen.height / 128f);
        }
        GUI.color = new Color(1f, 1f, 1f, grainStrength);
        GUI.DrawTextureWithTexCoords(screen, grain[grainIndex], grainUv);

        GUI.color = prev;
    }

    static Texture2D BuildVignette(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x / (float)(s - 1) - 0.5f) * 2f;
                float dy = (y / (float)(s - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, r));
                t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        t.Apply(false);
        return t;
    }

    static Texture2D BuildGrain(int s, int seed)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Point;
        var r = new System.Random(seed);
        var px = new Color[s * s];
        for (int i = 0; i < px.Length; i++)
        {
            float g = (float)r.NextDouble();
            px[i] = new Color(g, g, g, 1f);
        }
        t.SetPixels(px);
        t.Apply(false);
        return t;
    }
}
