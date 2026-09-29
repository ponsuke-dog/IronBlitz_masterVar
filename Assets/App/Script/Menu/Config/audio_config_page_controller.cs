using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Audio設定ページを制御するクラス。
/// Master / BGM / SE / UI のSliderをAudioVolumeServiceへ接続する。
/// </summary>
public class AudioConfigPageController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Slider uiSlider;


    [Header("Audio Service")]
    [SerializeField] private AudioVolumeService audioVolumeService;


    private void Awake()
    {

        if (audioVolumeService == null)
        {
            audioVolumeService = FindFirstObjectByType<AudioVolumeService>();
        }

        masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        seSlider.onValueChanged.AddListener(OnSeVolumeChanged);
        uiSlider.onValueChanged.AddListener(OnUiVolumeChanged);

    }

    private void Start()
    {
        if (audioVolumeService == null)
        {
            Debug.LogWarning("AudioVolumeService がシーン内に見つかりません。AudioSystem が配置されているか確認してください。");
            return;
        }

        ApplyCurrentValuesToSliders();
    }

    /// <summary>
    /// 現在保存されている音量値をSliderへ反映する。
    /// </summary>
    private void ApplyCurrentValuesToSliders()
    {
        // AudioVolumeService内で現在値を読み込む
        audioVolumeService.LoadVolumes();

        // LoadVolumes後に保持されている現在値を取得する
        AudioVolumeData data = audioVolumeService.CurrentVolumeData;

        masterSlider.SetValueWithoutNotify(data.masterVolume);
        bgmSlider.SetValueWithoutNotify(data.bgmVolume);
        seSlider.SetValueWithoutNotify(data.seVolume);
        uiSlider.SetValueWithoutNotify(data.uiVolume);
    }

    private void OnMasterVolumeChanged(float value)
    {
        audioVolumeService.SetMasterVolume(value);
    }

    private void OnBgmVolumeChanged(float value)
    {
        audioVolumeService.SetBgmVolume(value);
    }

    private void OnSeVolumeChanged(float value)
    {
        audioVolumeService.SetSeVolume(value);
    }

    private void OnUiVolumeChanged(float value)
    {
        audioVolumeService.SetUiVolume(value);
    }

}