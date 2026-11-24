using UnityEngine;
using UnityEngine.Rendering;

// 밤 숲 조명 개선. 씬을 다시 만들지 않아도 시작할 때 적용된다.
// 가벼운 설정만 쓴다: 방향광 1개 그림자, 환경광 그라디언트, 안개 색/농도 조정.
public class NightAtmosphere : MonoBehaviour
{
    const float BaseFogDensity = 0.028f;

    void Start()
    {
        // 하늘 배경은 안개보다 어둡게: 별/달이 보이고, 지평선 쪽은 안개 띠가 자연스럽게 이어준다.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        }

        // 환경광: 위(하늘)는 푸른 기, 옆은 어두운 청록, 아래(땅)는 거의 검정.
        // 단색 어둠 대신 위쪽 면이 더 밝아 입체감이 생긴다.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.13f, 0.17f, 0.27f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.10f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.02f, 0.03f, 0.03f);

        // 안개: 푸른 먹빛, 약간 옅게 해서 멀리 나무 실루엣이 겹쳐 보이게.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.07f, 0.10f, 0.14f);
        RenderSettings.fogDensity = BaseFogDensity;

        // 달빛: 차가운 색 + 그림자. 나무/집/캐릭터가 땅에 그림자를 드리운다.
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = new Color(0.55f, 0.68f, 0.95f);
            l.intensity = 0.85f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            break;
        }

        // 성능: 그림자는 가까운 곳만, 해상도는 보통.
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
    }

    // 안개 농도가 숨 쉬듯 천천히 변한다 (시야가 미세하게 좁아졌다 넓어졌다).
    void Update()
    {
        float t = Time.time;
        RenderSettings.fogDensity = BaseFogDensity * (1f + 0.10f * Mathf.Sin(t * 0.23f) + 0.04f * Mathf.Sin(t * 0.71f));
    }
}
