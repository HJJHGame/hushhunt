using UnityEngine;

// 게임 설정: 마우스 감도, 볼륨, 밝기(어둡기). PlayerPrefs에 저장한다.
// 메뉴의 설정 화면이 이 값들을 바꾸고, 각 시스템은 Changed를 구독하거나 매 프레임 읽어 반영한다.
public static class GameSettings
{
    public const float SensitivityDefault = 2f;
    public const float VolumeDefault = 1f;
    public const int BrightnessDefault = 1; // 0 어둡게, 1 보통, 2 밝게

    public static float Sensitivity { get; private set; } = SensitivityDefault;
    public static float Volume { get; private set; } = VolumeDefault;
    public static int Brightness { get; private set; } = BrightnessDefault;

    // 환경광에 곱하는 값: 어둡게는 공포 분위기용, 밝게는 잘 안 보일 때용.
    public static float BrightnessMultiplier
    {
        get
        {
            if (Brightness == 0) return 0.6f;
            if (Brightness == 2) return 1.5f;
            return 1f;
        }
    }

    public static event System.Action Changed;

    static GameSettings()
    {
        Sensitivity = PlayerPrefs.GetFloat("hh.sensitivity", SensitivityDefault);
        Volume = PlayerPrefs.GetFloat("hh.volume", VolumeDefault);
        Brightness = PlayerPrefs.GetInt("hh.brightness", BrightnessDefault);
    }

    // 게임 시작 시 한 번 호출: 저장된 값을 실제 시스템(볼륨 등)에 반영한다.
    public static void Apply()
    {
        AudioListener.volume = Volume;
        Changed?.Invoke();
    }

    public static void SetSensitivity(float value)
    {
        Sensitivity = Mathf.Clamp(value, 0.5f, 6f);
        PlayerPrefs.SetFloat("hh.sensitivity", Sensitivity);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("hh.volume", Volume);
        PlayerPrefs.Save();
        AudioListener.volume = Volume;
        Changed?.Invoke();
    }

    public static void SetBrightness(int value)
    {
        Brightness = Mathf.Clamp(value, 0, 2);
        PlayerPrefs.SetInt("hh.brightness", Brightness);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
