using UnityEngine;

// 걸을 때 발소리: 나뭇잎 밟는 소리를 런타임에 절차적으로 만들어 재생한다.
// 외부 오디오 에셋 없이 노이즈 버스트 + 크래클로 낙엽 바스락거리는 느낌을 낸다.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CharacterController))]
public class Footsteps : MonoBehaviour
{
    public float stepLength = 1.6f; // 발소리 한 번당 이동 거리(m)
    public float volume = 0.55f;

    CharacterController controller;
    FirstPersonController fpc;
    AudioSource source;
    AudioClip[] steps;
    public float GaitPhase { get; private set; } = Mathf.PI * 0.5f;
    public bool IsWalking { get; private set; }
    public Vector3 LocalMoveDirection { get; private set; }

    float distanceSinceStep;
    int contactParity;
    Vector3 lastPosition;
    readonly System.Random rng = new System.Random();

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        fpc = GetComponent<FirstPersonController>();
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f; // 2D: 자기 발소리라 공간감 없이
        source.playOnAwake = false;

        // 같은 소리 반복이 티 나지 않게 변형 4개를 미리 만든다
        steps = new AudioClip[4];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = MakeLeafStep(i);

        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        // 런타임에 만든 클립은 직접 해제한다 (씬 재시작 시 메모리 누수 방지)
        if (steps == null) return;
        foreach (var clip in steps)
            if (clip != null) Destroy(clip);
    }

    // 이동(Update)이 끝난 뒤 한 곳에서 발소리와 다리의 박자를 계산한다.
    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        float length = Mathf.Max(0.2f, stepLength);
        IsWalking = controller.isGrounded && distance > 0.0001f && distance < 2f;
        LocalMoveDirection = IsWalking ? transform.InverseTransformDirection(delta / distance) : Vector3.zero;

        if (IsWalking)
        {
            distanceSinceStep += distance;
            int contacts = Mathf.FloorToInt(distanceSinceStep / length);
            if (contacts > 0)
            {
                // 프레임마다 나머지를 버리면 낮은 FPS에서 박자가 느려진다.
                distanceSinceStep -= contacts * length;
                contactParity = (contactParity + contacts) % 2;
                source.pitch = 0.85f + (float)rng.NextDouble() * 0.3f;
                // 웅크리면 살금살금(35%), 달리면 쿵쿵(160%), 걷기는 기본.
                float state = fpc != null && fpc.IsCrouching ? 0f
                    : fpc != null && fpc.IsSprinting ? 1.6f : 1f;
                float v = volume * state * (0.8f + (float)rng.NextDouble() * 0.4f);
                source.PlayOneShot(steps[rng.Next(steps.Length)], v);
            }
        }
        else if (!controller.isGrounded || distance >= 2f)
        {
            distanceSinceStep = length * 0.5f;
        }
        // 매 pi마다 반대쪽 발 접지. 소리와 포즈가 이 위상을 공유한다.
        GaitPhase = (contactParity + distanceSinceStep / length) * Mathf.PI;
    }

    // 낙엽 바스락 소리: 빠르게 감쇠하는 노이즈 + 임의 시점의 짧은 크래클 몇 번
    static AudioClip MakeLeafStep(int seed)
    {
        const int rate = 44100;
        float length = 0.22f;
        int n = (int)(rate * length);
        var samples = new float[n];
        var r = new System.Random(1234 + seed);

        float decay = 14f + (float)r.NextDouble() * 6f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)r.NextDouble() * 2f - 1f;
            samples[i] = noise * Mathf.Exp(-t * decay);
        }

        int crackles = 3 + r.Next(3);
        for (int c = 0; c < crackles; c++)
        {
            int start = r.Next(0, n / 2);
            int len = rate / 400 + r.Next(rate / 200);
            float amp = 0.6f + (float)r.NextDouble() * 0.4f;
            for (int i = 0; i < len && start + i < n; i++)
            {
                float t = i / (float)len;
                samples[start + i] += ((float)r.NextDouble() * 2f - 1f) * (1f - t) * amp;
            }
        }

        // 클리핑 방지 정규화 + 아주 짧은 어택
        float max = 0.001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            samples[i] = samples[i] / max * 0.9f * Mathf.Clamp01(t / 0.01f);
        }

        var clip = AudioClip.Create("LeafStep" + seed, n, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
