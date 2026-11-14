using UnityEngine;

// 미니맵: 탑다운 카메라가 플레이어를 따라가며 RenderTexture로 찍고,
// OnGUI로 화면 오른쪽 위에 그린다. 중앙 화살표가 플레이어 위치/방향.
// 별도 UI 패키지 없이 imgui 모듈만으로 동작한다.
[RequireComponent(typeof(Camera))]
public class Minimap : MonoBehaviour
{
    public Transform target;   // 따라갈 플레이어
    public float height = 45f; // 맵 위 카메라 높이
    public int size = 220;     // 미니맵 한 변 픽셀

    RenderTexture rt;
    Texture2D arrowTex;

    bool fogWasEnabled;
    UnityEngine.Rendering.AmbientMode ambientModeWas;
    Color ambientColorWas;

    void Awake()
    {
        rt = new RenderTexture(256, 256, 24);
        GetComponent<Camera>().targetTexture = rt;

        // 위를 향하는 삼각형 화살표 텍스처 (런타임 생성)
        const int a = 64;
        arrowTex = new Texture2D(a, a, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = new Color(0.8f, 1f, 0.3f, 1f);
        for (int y = 0; y < a; y++)
        {
            for (int x = 0; x < a; x++)
            {
                float nx = Mathf.Abs(x / (float)(a - 1) - 0.5f); // 0~0.5
                float ny = y / (float)(a - 1);                   // 0(아래)~1(위)
                bool inside = ny > 0.1f && nx < (1f - ny) * 0.45f;
                arrowTex.SetPixel(x, y, inside ? fill : clear);
            }
        }
        arrowTex.Apply();
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + Vector3.up * height;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 북쪽(+Z)이 위
    }

    // 안개와 어두운 앰비언트는 메인 시점용이라 미니맵 찍을 때만 잠시 바꾼다.
    void OnPreRender()
    {
        fogWasEnabled = RenderSettings.fog;
        RenderSettings.fog = false;
        ambientModeWas = RenderSettings.ambientMode;
        ambientColorWas = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.5f);
    }

    void OnPostRender()
    {
        RenderSettings.fog = fogWasEnabled;
        RenderSettings.ambientMode = ambientModeWas;
        RenderSettings.ambientLight = ambientColorWas;
    }

    void OnGUI()
    {
        if (rt == null || Event.current.type != EventType.Repaint) return;

        float pad = 16f;
        var frame = new Rect(Screen.width - size - pad, pad, size, size);

        // 테두리
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(frame.x - 4, frame.y - 4, frame.width + 8, frame.height + 8), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(frame, rt);

        // 플레이어는 항상 미니맵 중앙. 방향만 화살표 회전으로 표시.
        if (target != null)
        {
            var pivot = new Vector2(frame.x + frame.width / 2f, frame.y + frame.height / 2f);
            var prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(target.eulerAngles.y, pivot);
            float s = 26f;
            GUI.DrawTexture(new Rect(pivot.x - s / 2f, pivot.y - s / 2f, s, s), arrowTex);
            GUI.matrix = prev;
        }
    }
}
