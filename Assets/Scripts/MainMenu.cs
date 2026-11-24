using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임 메인 화면: 타이틀(모드 선택, 설정)과 일시정지 메뉴.
// uGUI를 전부 코드로 만든다. 씬 수정 없이 시작할 때 적용된다.
// 게임 시작 시 타이틀이 열리고 커서가 풀린다. Esc로 일시정지를 연다.
// 온라인 모드는 멀티플레이어 작업 전까지 비활성으로 표시한다.
public class MainMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    enum Mode { Hidden, Title, Pause, Settings }

    Mode mode = Mode.Hidden;
    Mode settingsReturn = Mode.Title;

    Font font;
    Canvas canvas;
    GameManager gm;
    FirstPersonController fpc;
    RectTransform titlePanel, pausePanel, settingsPanel;
    Text sensValue, volValue;
    Button[] brightnessButtons;

    static readonly Color BgTitle = new Color(0.015f, 0.02f, 0.035f, 0.93f);
    static readonly Color BgPause = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color PanelBox = new Color(0.05f, 0.06f, 0.08f, 0.95f);
    static readonly Color ButtonNormal = new Color(0.16f, 0.20f, 0.26f, 1f);
    static readonly Color ButtonHover = new Color(0.26f, 0.36f, 0.44f, 1f);
    static readonly Color ButtonPressed = new Color(0.32f, 0.45f, 0.52f, 1f);
    static readonly Color ButtonDisabled = new Color(0.10f, 0.11f, 0.13f, 0.7f);
    static readonly Color ButtonSelected = new Color(0.30f, 0.46f, 0.50f, 1f);
    static readonly Color TextMain = new Color(0.92f, 0.95f, 1f);
    static readonly Color TextSub = new Color(0.65f, 0.72f, 0.8f);

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        fpc = GetComponent<FirstPersonController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Build();
        GameSettings.Apply();
        OpenTitle();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        switch (mode)
        {
            case Mode.Hidden:
                // 종료 화면에서는 Esc를 열지 않는다 (R 재시작 안내와 겹치지 않게)
                if (gm == null || gm.CurrentPhase != GameManager.Phase.Ended) OpenPause();
                break;
            case Mode.Pause:
                Resume();
                break;
            case Mode.Settings:
                CloseSettings();
                break;
        }
    }

    // ---- 화면 전환 ----

    void OpenTitle()
    {
        ShowOnly(titlePanel);
        SetOpen(true, Mode.Title);
    }

    void OpenPause()
    {
        ShowOnly(pausePanel);
        SetOpen(true, Mode.Pause);
    }

    void OpenSettings(Mode returnTo)
    {
        settingsReturn = returnTo;
        RefreshSettings();
        ShowOnly(settingsPanel);
        mode = Mode.Settings; // IsOpen은 그대로
    }

    void CloseSettings()
    {
        if (settingsReturn == Mode.Pause) OpenPause();
        else OpenTitle();
    }

    void Resume()
    {
        ShowOnly(null);
        SetOpen(false, Mode.Hidden);
    }

    void StartPractice()
    {
        ShowOnly(null);
        SetOpen(false, Mode.Hidden);
        if (gm != null) gm.BeginRound();
    }

    void BackToTitle()
    {
        // 씬을 다시 불러오면 타이틀이 기본 상태로 열린다.
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    void ShowOnly(RectTransform panel)
    {
        if (titlePanel != null) titlePanel.gameObject.SetActive(panel == titlePanel);
        if (pausePanel != null) pausePanel.gameObject.SetActive(panel == pausePanel);
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(panel == settingsPanel);
    }

    void SetOpen(bool open, Mode m)
    {
        mode = m;
        IsOpen = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (fpc != null) fpc.enabled = !open;
        if (gm != null) gm.paused = open;
    }

    // ---- UI 만들기 ----

    void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var cgo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildTitle();
        BuildPause();
        BuildSettings();
    }

    void BuildTitle()
    {
        titlePanel = FullScreenPanel("Title Panel", BgTitle);

        var title = MakeText(titlePanel, "HUSH HUNT", 120, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
        Place(title.rectTransform, 0f, 300f, 1200f, 160f);
        var sub = MakeText(titlePanel, "숨거나, 사냥하거나.", 30, TextAnchor.MiddleCenter, TextSub);
        Place(sub.rectTransform, 0f, 190f, 800f, 50f);

        MakeButton(titlePanel, "연습 시작", 20f, true, StartPractice);
        MakeButton(titlePanel, "온라인 (준비 중)", -80f, false, null);
        MakeButton(titlePanel, "설정", -180f, true, () => OpenSettings(Mode.Title));

        var ver = MakeText(titlePanel, "Unity 6 · 싱글 프로토타입", 18, TextAnchor.MiddleCenter, TextSub);
        Place(ver.rectTransform, 0f, -480f, 600f, 30f);
    }

    void BuildPause()
    {
        pausePanel = FullScreenPanel("Pause Panel", BgPause);

        var box = MakeBox(pausePanel, 0f, 0f, 520f, 560f);
        var title = MakeText(box, "일시정지", 44, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
        Place(title.rectTransform, 0f, 200f, 480f, 70f);
        MakeButton(box, "계속하기", 80f, true, Resume);
        MakeButton(box, "설정", 0f, true, () => OpenSettings(Mode.Pause));
        MakeButton(box, "타이틀로", -80f, true, BackToTitle);
        var hint = MakeText(box, "Esc로 닫기", 18, TextAnchor.MiddleCenter, TextSub);
        Place(hint.rectTransform, 0f, -220f, 480f, 30f);
    }

    void BuildSettings()
    {
        settingsPanel = FullScreenPanel("Settings Panel", BgPause);

        var box = MakeBox(settingsPanel, 0f, 0f, 640f, 720f);
        var title = MakeText(box, "설정", 44, TextAnchor.MiddleCenter, TextMain, FontStyle.Bold);
        Place(title.rectTransform, 0f, 300f, 480f, 70f);

        sensValue = MakeSlider(box, "마우스 감도", 190f, 0.5f, 6f, GameSettings.Sensitivity,
            v => GameSettings.SetSensitivity(v), v => v.ToString("0.0"));
        volValue = MakeSlider(box, "볼륨", 70f, 0f, 1f, GameSettings.Volume,
            v => GameSettings.SetVolume(v), v => Mathf.RoundToInt(v * 100f) + "%");

        var brightLabel = MakeText(box, "밝기 (분위기)", 20, TextAnchor.MiddleLeft, TextSub);
        Place(brightLabel.rectTransform, -80f, -60f, 300f, 30f);
        brightnessButtons = new Button[3];
        string[] names = { "어둡게", "보통", "밝게" };
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            var b = MakeButton(box, names[i], -120f, true, () =>
            {
                GameSettings.SetBrightness(choice);
                RefreshSettings();
            });
            var rt = (RectTransform)b.transform;
            rt.sizeDelta = new Vector2(150f, 56f);
            rt.anchoredPosition = new Vector2((i - 1) * 170f, -120f);
            brightnessButtons[i] = b.GetComponent<Button>();
        }

        MakeButton(box, "뒤로", -260f, true, CloseSettings);
    }

    void RefreshSettings()
    {
        if (brightnessButtons != null)
            for (int i = 0; i < brightnessButtons.Length; i++)
                if (brightnessButtons[i] != null)
                {
                    // Button의 ColorTint가 targetGraphic 색을 덮어쓰므로 normalColor 자체를 바꾼다.
                    Color c = i == GameSettings.Brightness ? ButtonSelected : ButtonNormal;
                    var cols = brightnessButtons[i].colors;
                    cols.normalColor = c;
                    cols.selectedColor = c;
                    brightnessButtons[i].colors = cols;
                    if (brightnessButtons[i].targetGraphic != null)
                        brightnessButtons[i].targetGraphic.color = c;
                }
        if (sensValue != null) sensValue.text = GameSettings.Sensitivity.ToString("0.0");
        if (volValue != null) volValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
    }

    // ---- 조각 helpers ----

    RectTransform FullScreenPanel(string name, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = bg;
        return rt;
    }

    RectTransform MakeBox(Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        go.GetComponent<Image>().color = PanelBox;
        Place(rt, x, y, w, h);
        return rt;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    Text MakeText(Transform parent, string content, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        rt.sizeDelta = new Vector2(600f, size * 1.6f);
        return t;
    }

    GameObject MakeButton(Transform parent, string label, float y, bool interactable, UnityAction onClick)
    {
        var go = new GameObject("Button " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, 0f, y, 380f, 64f);

        var img = go.GetComponent<Image>();
        img.color = Color.white; // 실제 색은 Button의 ColorTint가 관리한다

        var b = go.GetComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonNormal;
        colors.disabledColor = ButtonDisabled;
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.interactable = interactable;
        if (onClick != null) b.onClick.AddListener(onClick);

        var text = MakeText(go.transform, label, 24, TextAnchor.MiddleCenter,
            interactable ? TextMain : TextSub);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return go;
    }

    // 슬라이더 하나: 이름 + 현재값 + 본체. 현재값 Text를 돌려준다.
    Text MakeSlider(Transform parent, string label, float y, float min, float max, float value,
        UnityAction<float> onChange, System.Func<float, string> format)
    {
        var labelText = MakeText(parent, label, 20, TextAnchor.MiddleLeft, TextSub);
        Place(labelText.rectTransform, -80f, y + 36f, 300f, 30f);
        var valueText = MakeText(parent, format(value), 20, TextAnchor.MiddleRight, TextMain);
        Place(valueText.rectTransform, 140f, y + 36f, 140f, 30f);

        var root = new GameObject("Slider " + label, typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.SetParent(parent, false);
        Place(rt, 0f, y, 460f, 24f);

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bgRt = (RectTransform)bg.transform;
        bgRt.SetParent(rt, false);
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        var fillAreaRt = (RectTransform)fillArea.transform;
        fillAreaRt.SetParent(rt, false);
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRt = (RectTransform)fill.transform;
        fillRt.SetParent(fillAreaRt, false);
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.45f, 0.66f, 0.6f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        var handleAreaRt = (RectTransform)handleArea.transform;
        handleAreaRt.SetParent(rt, false);
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        var handleRt = (RectTransform)handle.transform;
        handleRt.SetParent(handleAreaRt, false);
        handleRt.sizeDelta = new Vector2(20f, 0f);
        handle.GetComponent<Image>().color = Color.white;

        var s = root.AddComponent<Slider>();
        s.fillRect = fillRt;
        s.handleRect = handleRt;
        s.targetGraphic = handle.GetComponent<Image>();
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = false;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(v =>
        {
            onChange(v);
            valueText.text = format(v);
        });
        return valueText;
    }
}
