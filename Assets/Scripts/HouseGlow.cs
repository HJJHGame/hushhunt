using UnityEngine;

// 집 안에 희미한 불빛을 켠다. 창문과 문은 실제로 뚫려 있는 구멍이라
// 안쪽 점광원 하나만으로 빛이 밖으로 새어 나와 멀리서도 집이 보인다.
// 어두워진 맵에서 랜드마크 역할을 한다. 씬을 다시 만들지 않아도 시작할 때 적용된다.
public class HouseGlow : MonoBehaviour
{
    public Color lightColor = new Color(1f, 0.72f, 0.42f); // 촛불/랜턴 느낌
    public float intensity = 0.55f; // 랜드마크 역할은 유지하되 주변을 밝히지 않을 정도로
    public float range = 7f;

    void Start()
    {
        foreach (var h in FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None))
        {
            if (h == null) continue;
            var go = new GameObject("House Glow");
            go.transform.SetParent(h.transform, false);
            // 벽 높이가 3.5~5m로 집마다 달라 중간 높이에 둔다.
            go.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lightColor;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None; // 성능: 그림자는 달빛과 손전등만
        }
    }
}
