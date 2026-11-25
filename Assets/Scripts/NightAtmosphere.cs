using UnityEngine;
using UnityEngine.Rendering;

// 밤 숲 조명 개선. 씬을 다시 만들지 않아도 시작할 때 적용된다.
// 가벼운 설정만 쓴다: 방향광 1개 그림자, 환경광 그라디언트, 안개 색/농도 조정.
// 기본 밝기는 이전보다 어둡게(0.5배) 맞추고, 설정의 밝기 단계를 곱한다.
public class NightAtmosphere : MonoBehaviour
{
    const float BaseFogDensity = 0.045f; // 시야를 더 좁혀 손전등 의존도를 높인다
    const float Darkness = 0.32f; // 환경광 기본 어둡기 (조명 효과 극대화용)

    // 원래 기준 색 (여기에 Darkness와 설정 밝기를 곱해 쓴다)
    static readonly Color SkyBase = new Color(0.13f, 0.17f, 0.27f);
    static readonly Color EquatorBase = new Color(0.07f, 0.10f, 0.12f);
    static readonly Color GroundBase = new Color(0.02f, 0.03f, 0.03f);
    static readonly Color FogBase = new Color(0.07f, 0.10f, 0.14f);

    void Start()
    {
        // 하늘 배경은 안개보다 어둡게: 별/달이 보이고, 지평선 쪽은 안개 띠가 자연스럽게 이어준다.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            // 시야 거리를 좁힌다: 먼 곳은 안개에 묻히고 손전등이 유일한 시야 확보 수단이 된다.
            cam.farClipPlane = 80f;
        }

        // 안개: 푸른 먹빛. 멀리 나무 실루엣이 겹쳐 보이게.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = FogBase;
        RenderSettings.fogDensity = BaseFogDensity;

        // 달빛: 차가운 색 + 그림자. 어두워진 환경광과 대비되도록 강도를 올린다.
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = new Color(0.55f, 0.68f, 0.95f);
            l.intensity = 1.0f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.9f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            break;
        }

        // 성능: 그림자는 가까운 곳만, 해상도는 보통.
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.shadowResolution = ShadowResolution.Medium;

        ApplyAtmosphere();
    }

    // 환경광: 위(하늘)는 푸른 기, 옆은 어두운 청록, 아래(땅)는 거의 검정.
    void ApplyAtmosphere()
    {
        float m = Darkness;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Scale(SkyBase, m);
        RenderSettings.ambientEquatorColor = Scale(EquatorBase, m);
        RenderSettings.ambientGroundColor = Scale(GroundBase, m);
    }

    static Color Scale(Color c, float m)
    {
        return new Color(c.r * m, c.g * m, c.b * m, c.a);
    }

    // 안개 농도가 숨 쉬듯 천천히 변한다 (시야가 미세하게 좁아졌다 넓어졌다).
    void Update()
    {
        float t = Time.time;
        RenderSettings.fogDensity = BaseFogDensity * (1f + 0.10f * Mathf.Sin(t * 0.23f) + 0.04f * Mathf.Sin(t * 0.71f));
    }
}
