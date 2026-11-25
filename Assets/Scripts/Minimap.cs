using UnityEngine;

// 미니맵: 실제 맵 데이터로 만든 지도 이미지(MapImage)를 배경으로 쓰고,
// 그 위에 플레이어 위치/방향과 북쪽 표시를 그린다.
// 기본은 오른쪽 위 작은 지도, M 키로 화면 중앙의 큰 지도를 연다.
public class Minimap : MonoBehaviour
{
    public Transform target;           // 플레이어
    MainMenu menu;

    // 메뉴는 GameSystems가 씬에 하나만 붙인다. 찾으면 캐시한다 (issue #28).
    bool MenuOpen()
    {
        if (menu == null) menu = FindFirstObjectByType<MainMenu>();
        return menu != null && menu.IsOpen;
    }

    public int smallSize = 220;        // 작은 지도 한 변 (px)
    public float bigSizeRatio = 0.75f; // 큰 지도: 화면 높이 대비 비율
    public float worldHalf = 100f;     // 맵 절반 크기 (m): 경계가 -100~100

    Texture2D mapTex;
    Texture2D arrowTex;
    GUIStyle hintStyle;
    GUIStyle northStyle;
    bool bigMap;

    void Awake()
    {
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

    void Start()
    {
        // 씬에 실제 배치된 집들을 읽어 지도 이미지를 만든다 (집과 지도가 같은 데이터를 공유)
        var houses = FindObjectsByType<HouseFootprint>(FindObjectsSortMode.None);
        mapTex = MapImage.Build(1024, worldHalf, houses);
    }

    void Update()
    {
        if (MenuOpen()) return;
        if (Input.GetKeyDown(KeyCode.M))
            bigMap = !bigMap;
    }

    void OnGUI()
    {
        if (MenuOpen()) return;
        if (Event.current.type != EventType.Repaint) return;

        int size = bigMap ? Mathf.RoundToInt(Screen.height * bigSizeRatio) : smallSize;
        float x = bigMap ? (Screen.width - size) * 0.5f : Screen.width - size - 16f;
        float y = bigMap ? (Screen.height - size) * 0.5f : 16f;
        var map = new Rect(x, y, size, size);

        // 테두리 + 지도 이미지. 지도의 사각 가장자리가 곧 실제 맵 경계(±100m)다.
        GUI.color = new Color(0f, 0f, 0f, bigMap ? 0.85f : 0.6f);
        GUI.DrawTexture(new Rect(map.x - 4f, map.y - 4f, map.width + 8f, map.height + 8f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (mapTex != null) GUI.DrawTexture(map, mapTex);

        // 플레이어 위치 + 바라보는 방향
        if (target != null)
        {
            Vector2 p = WorldToMap(target.position, map);
            var prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(target.eulerAngles.y, p);
            float s = bigMap ? 34f : 24f;
            GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), arrowTex);
            GUI.matrix = prev;
        }

        // 북쪽 표시
        if (northStyle == null)
        {
            northStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
        }
        northStyle.fontSize = bigMap ? 18 : 13;
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(map.x, map.y + 1f, map.width, 24f), "N", northStyle);
        GUI.color = Color.white;

        if (bigMap)
        {
            // 매 프레임 새로 만들면 GC가 쌓이므로 한 번만 만든다.
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
            }
            GUI.Label(new Rect(map.x, map.yMax + 6f, map.width, 22f), "M 키로 닫기", hintStyle);
        }
    }

    void OnDestroy()
    {
        if (arrowTex != null) Destroy(arrowTex);
        if (mapTex != null) Destroy(mapTex);
    }

    // 월드 좌표 -> 지도 픽셀. +Z(북쪽)가 위, +X가 오른쪽. MapImage와 같은 변환.
    Vector2 WorldToMap(Vector3 world, Rect map)
    {
        float u = Mathf.InverseLerp(-worldHalf, worldHalf, world.x);
        float v = Mathf.InverseLerp(-worldHalf, worldHalf, world.z);
        return new Vector2(map.x + u * map.width, map.y + (1f - v) * map.height);
    }
}
