using UnityEngine;

// 런타임에 코드로 만드는 머티리얼의 공용 생성기.
// Shader.Find("Standard")는 빌드에서 셰이더가 빠지면 null을 돌려주고,
// 그 상태로 new Material(null)을 하면 예외가 난다. 한 곳에서 폴백을 처리한다.
public static class RuntimeMaterials
{
    static Shader litShader;

    static Shader LitShader
    {
        get
        {
            if (litShader != null) return litShader;
            litShader = Shader.Find("Standard");
            if (litShader == null)
            {
                Debug.LogWarning("[RuntimeMaterials] Standard 셰이더를 찾지 못해 Sprites/Default로 대체합니다. "
                    + "Project Settings > Graphics > Always Included Shaders에 Standard를 추가하세요.");
                litShader = Shader.Find("Sprites/Default");
            }
            return litShader;
        }
    }

    // 조명을 받는 불투명 머티리얼. smoothness를 생략하면 셰이더 기본값을 그대로 쓴다.
    public static Material CreateLit(string name, Color color, float? smoothness = null)
    {
        var material = new Material(LitShader) { name = name, color = color };
        if (smoothness.HasValue && material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness.Value);
        return material;
    }

    // 자체 발광. 이 셰이더에 발광 속성이 없으면 아무것도 하지 않는다.
    public static void SetEmission(Material material, Color color)
    {
        if (material == null || !material.HasProperty("_EmissionColor")) return;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color);
    }
}
