using UnityEngine;

// 손전등 (F 키). 카메라에 붙은 스포트라이트. 가끔 깜빡인다.
// 어둠 속에서 보이게 해주지만 빛이 켜져 있으면 멀리서도 눈에 띈다는 점이 PvP 숨바꼭질의 긴장 요소.
public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = true;
    public float intensity = 2.4f;
    public float range = 38f;
    public float spotAngle = 55f;

    Light lamp;
    AudioSource clickSource;
    AudioClip clickClip;
    float dipUntil;

    public bool IsOn { get { return lamp != null && lamp.enabled; } }

    void Start()
    {
        Transform pivot = null;
        var fpc = GetComponent<FirstPersonController>();
        if (fpc != null) pivot = fpc.cameraPivot;
        if (pivot == null && Camera.main != null) pivot = Camera.main.transform;
        if (pivot == null) { enabled = false; return; }

        var go = new GameObject("Flashlight");
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = new Vector3(0.2f, -0.18f, 0.25f);
        go.transform.localRotation = Quaternion.identity;

        lamp = go.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = range;
        lamp.spotAngle = spotAngle;
        lamp.innerSpotAngle = spotAngle * 0.5f;
        lamp.intensity = intensity;
        lamp.color = new Color(1f, 0.93f, 0.78f);
        lamp.shadows = LightShadows.None;
        lamp.enabled = startOn;

        clickClip = MakeClick();
        clickSource = gameObject.AddComponent<AudioSource>();
        clickSource.spatialBlend = 0f;
        clickSource.playOnAwake = false;
    }

    void Update()
    {
        if (lamp == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            lamp.enabled = !lamp.enabled;
            clickSource.PlayOneShot(clickClip, 0.6f);
        }

        if (!lamp.enabled) return;

        if (Time.time < dipUntil)
        {
            lamp.intensity = intensity * 0.25f;
        }
        else
        {
            lamp.intensity = intensity * (1f + 0.03f * Mathf.Sin(Time.time * 23f));
            // 가끔 순간적으로 꺼질 듯 깜빡인다 (대략 10초에 한 번)
            if (Random.value < 0.1f * Time.deltaTime)
                dipUntil = Time.time + Random.Range(0.05f, 0.15f);
        }
    }

    void OnDestroy()
    {
        if (clickClip != null) Destroy(clickClip);
    }

    static AudioClip MakeClick()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.06f);
        var s = new float[n];
        var r = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            s[i] = (noise * 0.6f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f) * Mathf.Exp(-t * 90f);
        }
        var clip = AudioClip.Create("FlashlightClick", n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }
}
