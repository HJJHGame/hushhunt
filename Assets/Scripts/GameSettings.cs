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

    public static float Sensitivity { get; private set; }
    public static float Volume { get; private set; }

    public static event System.Action Changed;

    static bool dirty;

    static GameSettings()
    {
        // 저장된 값이 손상됐어도(수동 편집 등) 허용 범위를 벗어나지 않게 한다.
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, SensitivityDefault), SensitivityMin, SensitivityMax);
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, VolumeDefault));
    }

    // 게임 시작 시 한 번 호출: 저장된 값을 실제 시스템(볼륨 등)에 반영한다.
    public static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
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
