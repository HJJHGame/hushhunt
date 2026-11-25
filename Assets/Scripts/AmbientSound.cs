using UnityEngine;

// 환경음: 끊김 없이 반복되는 바람 소리만 남긴다 (먼 소리 효과음은 제거).
// 런타임에 코드로 만들기 때문에 오디오 파일이 필요 없다.
public class AmbientSound : MonoBehaviour
{
    public float windVolume = 0.30f;

    AudioClip wind;

    void Start()
    {
        wind = MakeWind();

        var windSource = gameObject.AddComponent<AudioSource>();
        windSource.clip = wind;
        windSource.loop = true;
        windSource.volume = windVolume;
        windSource.spatialBlend = 0f;
        windSource.playOnAwake = false;
        windSource.Play();
    }

    void OnDestroy()
    {
        if (wind != null) Destroy(wind);
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
}
