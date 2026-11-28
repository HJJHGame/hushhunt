using UnityEngine;

// 심장박동: 숨는 팀(로컬 플레이어)만 듣는다. 가장 가까운 술래가 15m 안에 오면 시작하고, 가까울수록 커지고 빨라진다.
// 방향 정보는 없다(2D 소리). 수색 시간에만 작동한다.
public class Heartbeat : MonoBehaviour
{
    public const float Range = 15f; // 이 안에 술래가 오면 들리기 시작
    public const float MinVolume = 0.6f; // 15m 끝에서의 볼륨 (전에는 0.25). 바로 옆은 1.0
    const float Gain = 1.8f;             // 소리 자체를 키우는 배율 (전에는 0.9 상당). 1을 넘는 부분은 잘라서 묵직하게 들린다

    FirstPersonController fpc;
    Health me;
    GameManager gm;
    AudioSource src;
    AudioClip thump;
    float nextBeat;

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        thump = MakeThump();
    }

    // 가장 가까운 살아 있는 술래까지 거리. 없으면 큰 값.
    public static float NearestSeeker(Vector3 from)
    {
        float best = 9999f;
        foreach (var g in GunHolder.All)
        {
            if (g == null || !g.IsAlive || !g.IsSeeker) continue;
            float d = Vector3.Distance(from, g.transform.position);
            if (d < best) best = d;
        }
        return best;
    }

    void Update()
    {
        if (fpc == null)
        {
            fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null) me = fpc.GetComponent<Health>();
            return;
        }
        if (me == null || me.IsDead || me.team != Team.Hider) return;
        if (gm == null || gm.CurrentPhase != GameManager.Phase.Seeking || MainMenu.IsMenuOpen) return;

        float d = NearestSeeker(fpc.transform.position);
        if (d >= Range) return;
        float near = 1f - d / Range;            // 0(15m) ~ 1(바로 옆)
        if (Time.time < nextBeat) return;
        src.PlayOneShot(thump, MinVolume + (1f - MinVolume) * near);
        // 15m에서 약 1.0초 간격, 바로 옆에서 0.4초 간격
        nextBeat = Time.time + Mathf.Lerp(1.0f, 0.4f, near);
    }

    // 쿵 하는 낮은 소리 한 번 (이중 박동)
    static AudioClip MakeThump()
    {
        int rate = 22050, n = (int)(rate * 0.35f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float a = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 22f);
            float t2 = t - 0.14f;
            float b = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 48f * t2) * Mathf.Exp(-t2 * 26f) * 0.8f : 0f;
            d[i] = Mathf.Clamp((a + b) * Gain, -1f, 1f);
        }
        var clip = AudioClip.Create("Heartbeat", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
