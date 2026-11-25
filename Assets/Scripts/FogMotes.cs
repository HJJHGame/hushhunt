using UnityEngine;

// 안개 속을 떠다니는 희미한 먼지/포자 입자. 플레이어를 따라다니지만 입자는 월드에 남아 천천히 흐른다.
public class FogMotes : MonoBehaviour
{
    const float HolderHeight = 3f; // 입자 상자 중심 높이 (상자 높이 8m -> 지면 아래 -1m ~ 위 7m)

    GameObject holder;
    Transform follow;
    Texture2D dot;
    Material mat;

    void Start()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        dot = BuildDot(32);
        mat = new Material(shader);
        mat.mainTexture = dot;

        holder = new GameObject("Fog Motes");
        holder.transform.SetParent(transform, false);
        holder.transform.position = new Vector3(0f, HolderHeight, 0f);

        var ps = holder.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = 10f;
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
        main.startColor = new Color(0.7f, 0.8f, 1f, 0.35f);
        main.maxParticles = 350;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 8f, 36f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var r = holder.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
    }

    // 입자 자체는 월드 공간에 남아 천천히 흐르고, 새로 생기는 위치만 플레이어를 따라간다.
    // (이 컴포넌트는 원점의 GameSystems에 붙어 있어서 따라가지 않으면 스폰 지점 주변에만 입자가 생긴다.)
    void LateUpdate()
    {
        if (holder == null) return;
        if (follow == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            follow = cam.transform;
        }
        Vector3 p = follow.position;
        holder.transform.position = new Vector3(p.x, HolderHeight, p.z);
    }

    void OnDestroy()
    {
        if (holder != null) Destroy(holder);
        if (mat != null) Destroy(mat);
        if (dot != null) Destroy(dot);
    }

    static Texture2D BuildDot(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        t.Apply(false);
        return t;
    }
}
