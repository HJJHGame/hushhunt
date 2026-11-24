using UnityEngine;

// 환경음: 끊김 없이 반복되는 바람 소리 + 가끔 멀리서 들리는 나뭇가지 부러지는 소리/쿵 소리.
// 모두 런타임에 코드로 만들기 때문에 오디오 파일이 필요 없다.
public class AmbientSound : MonoBehaviour
{
    public float windVolume = 0.30f;
    public Vector2 eventInterval = new Vector2(9f, 26f); // 먼 소리 간격(초)

    AudioClip wind;
    AudioClip[] events;
    AudioSource windSource;
    float nextEvent;

    void Start()
    {
        wind = MakeWind();
        events = new[] { MakeSnap(1), MakeSnap(2), MakeThud(3) };

        windSource = gameObject.AddComponent<AudioSource>();
        windSource.clip = wind;
        windSource.loop = true;
        windSource.volume = windVolume;
        windSource.spatialBlend = 0f;
        windSource.playOnAwake = false;
        windSource.Play();

        nextEvent = Time.time + Random.Range(eventInterval.x, eventInterval.y);
    }

    void Update()
    {
        if (Time.time < nextEvent) return;
        nextEvent = Time.time + Random.Range(eventInterval.x, eventInterval.y);
        PlayDistant(events[Random.Range(0, events.Length)]);
    }

    void OnDestroy()
    {
        if (wind != null) Destroy(wind);
        if (events != null)
            foreach (var e in events) if (e != null) Destroy(e);
    }

    void PlayDistant(AudioClip clip)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(18f, 45f);
        Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        pos.y = 1f;

        var go = new GameObject("Distant Sound");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 8f;
        src.maxDistance = 70f;
        src.volume = Random.Range(0.5f, 0.9f);
        src.pitch = Random.Range(0.85f, 1.1f);
        src.Play();
        Destroy(go, clip.length / src.pitch + 0.5f);
    }

    // 12초짜리 바람. 앞뒤를 크로스페이드해서 반복해도 소리가 끊기지 않는다.
    static AudioClip MakeWind()
    {
        const int rate = 22050;
        const int n = rate * 12;
        const int xf = rate; // 1초 크로스페이드
        var buf = new float[n + xf];
        var r = new System.Random(99);
        float lo = 0f, mid = 0f;

        for (int i = 0; i < n + xf; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lo += (noise - lo) * 0.015f;
            mid += (noise - mid) * 0.10f;
            // 12초 주기의 세기 변화(돌풍). 주기가 12초라 이음매에서도 이어진다.
            float gust = 0.55f + 0.30f * Mathf.Sin(2f * Mathf.PI * t / 12f)
                       + 0.15f * Mathf.Sin(2f * Mathf.PI * t * 3f / 12f + 1.3f);
            buf[i] = (lo * 3.2f + (mid - lo) * 0.8f) * gust;
        }

        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = buf[i];
        for (int i = 0; i < xf; i++)
            s[i] = Mathf.Lerp(buf[n + i], buf[i], i / (float)xf);

        float max = 0.001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
        float k = 0.7f / max;
        for (int i = 0; i < n; i++) s[i] *= k;

        var clip = AudioClip.Create("Wind", n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    // 마른 나뭇가지가 부러지는 소리
    static AudioClip MakeSnap(int seed)
    {
        const int rate = 44100;
        int n = (int)(rate * 0.35f);
        var s = new float[n];
        var r = new System.Random(300 + seed);
        float prev = 0f;
        float tone = 1500f + seed * 220f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            float hp = noise - prev; // 고음만 남기는 간단한 하이패스
            prev = noise;
            float v = hp * Mathf.Exp(-t * 45f) + Mathf.Sin(2f * Mathf.PI * tone * t) * 0.5f * Mathf.Exp(-t * 70f);
            if (t > 0.09f) // 두 번째 작은 꺾이는 소리
            {
                float t2 = t - 0.09f;
                v += hp * 0.6f * Mathf.Exp(-t2 * 60f);
            }
            s[i] = v;
        }
        Normalize(s, 0.9f);

        var clip = AudioClip.Create("Snap" + seed, n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    // 멀리서 무언가 쿵 하고 떨어지거나 밟는 소리
    static AudioClip MakeThud(int seed)
    {
        const int rate = 44100;
        int n = (int)(rate * 0.7f);
        var s = new float[n];
        var r = new System.Random(500 + seed);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.05f;
            float v = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 12f) + lp * 2f * Mathf.Exp(-t * 20f);
            if (t > 0.35f)
            {
                float t2 = t - 0.35f;
                v += (Mathf.Sin(2f * Mathf.PI * 62f * t2) * Mathf.Exp(-t2 * 14f) + lp * 1.5f * Mathf.Exp(-t2 * 22f)) * 0.6f;
            }
            s[i] = v;
        }
        Normalize(s, 0.9f);

        var clip = AudioClip.Create("Thud" + seed, n, 1, rate, false);
        clip.SetData(s, 0);
        return clip;
    }

    static void Normalize(float[] s, float peak)
    {
        float max = 0.001f;
        for (int i = 0; i < s.Length; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
        float k = peak / max;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
    }
}
