using UnityEngine;

// 게임 설정: 마우스 감도, 볼륨. PlayerPrefs에 저장한다.
// 메뉴의 설정 화면이 이 값들을 바꾸고, 각 시스템은 Changed를 구독하거나 매 프레임 읽어 반영한다.
//
// 슬라이더를 끄는 동안 값이 초당 수십 번 바뀌므로 Set*은 메모리 값만 바꾸고,
// 디스크 쓰기(PlayerPrefs.Save)는 설정 화면을 나갈 때 Save()로 한 번만 한다.
public static class GameSettings
{
    public const float SensitivityDefault = 2f;
    public const float SensitivityMin = 0.5f;
    public const float SensitivityMax = 6f;
    public const float VolumeDefault = 1f;

    const string KeySensitivity = "hh.sensitivity";
    const string KeyVolume = "hh.volume";
    const string KeyMaxFps = "hh.maxfps";

    public static float Sensitivity { get; private set; }
    public static float Volume { get; private set; }
    public const int MaxFpsDefault = 60;
    public static int MaxFps { get; private set; } // 60 또는 0(제한 없음)

    public static event System.Action Changed;

    static bool dirty;

    static GameSettings()
    {
        // 저장된 값이 손상됐어도(수동 편집 등) 허용 범위를 벗어나지 않게 한다.
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, SensitivityDefault), SensitivityMin, SensitivityMax);
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, VolumeDefault));
        MaxFps = PlayerPrefs.GetInt(KeyMaxFps, MaxFpsDefault) == 0 ? 0 : 60;
    }

    // 어느 씬에서 Play를 눌러도(메뉴를 거치지 않아도) 시작할 때 저장된 최대 FPS를 적용한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = MaxFps == 0 ? -1 : MaxFps;
    }

    public static void SetMaxFps(int value)
    {
        MaxFps = value == 0 ? 0 : 60;
        PlayerPrefs.SetInt(KeyMaxFps, MaxFps);
        ApplyFrameRate();
        dirty = true;
        Changed?.Invoke();
    }

    // 게임 시작 시 한 번 호출: 저장된 값을 실제 시스템(볼륨 등)에 반영한다.
    public static void Apply()
    {
        ApplyFrameRate();
        AudioListener.volume = Volume;
        Changed?.Invoke();
    }

    public static void SetSensitivity(float value)
    {
        Sensitivity = Mathf.Clamp(value, SensitivityMin, SensitivityMax);
        PlayerPrefs.SetFloat(KeySensitivity, Sensitivity);
        dirty = true;
        Changed?.Invoke();
    }

    public static void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyVolume, Volume);
        AudioListener.volume = Volume;
        dirty = true;
        Changed?.Invoke();
    }

    // 바뀐 값이 있을 때만 디스크에 쓴다.
    public static void Save()
    {
        if (!dirty) return;
        PlayerPrefs.Save();
        dirty = false;
    }
}
