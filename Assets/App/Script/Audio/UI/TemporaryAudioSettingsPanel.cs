using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仮の音量調整画面
/// 表示とイベント通知だけを担当する
/// </summary>
public class TemporaryAudioSettingsPanel : MonoBehaviour, IAudioSettingsView
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Slider uiSlider;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    public event Action<float> OnMasterChanged;
    public event Action<float> OnBgmChanged;
    public event Action<float> OnSeChanged;
    public event Action<float> OnUiChanged;
    public event Action OnResetClicked;
    public event Action OnCloseClicked;

    private void Awake()
    {
        masterSlider.onValueChanged.AddListener(HandleMasterChanged);
        bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
        seSlider.onValueChanged.AddListener(HandleSeChanged);
        uiSlider.onValueChanged.AddListener(HandleUiChanged);

        resetButton.onClick.AddListener(RequestReset);
        closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDestroy()
    {
        masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
        bgmSlider.onValueChanged.RemoveListener(HandleBgmChanged);
        seSlider.onValueChanged.RemoveListener(HandleSeChanged);
        uiSlider.onValueChanged.RemoveListener(HandleUiChanged);

        resetButton.onClick.RemoveListener(RequestReset);
        closeButton.onClick.RemoveListener(RequestClose);
    }

    public void SetValues(AudioVolumeData data)
    {
        masterSlider.SetValueWithoutNotify(data.masterVolume);
        bgmSlider.SetValueWithoutNotify(data.bgmVolume);
        seSlider.SetValueWithoutNotify(data.seVolume);
        uiSlider.SetValueWithoutNotify(data.uiVolume);
    }

    public void Show()
    {
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    /// <summary>
    /// Unity Button の OnClick からも呼べるリセット処理
    /// </summary>
    public void RequestReset()
    {
        OnResetClicked?.Invoke();
    }

    /// <summary>
    /// Unity Button の OnClick からも呼べる閉じる処理
    /// </summary>
    public void RequestClose()
    {
        OnCloseClicked?.Invoke();
    }

    private void HandleMasterChanged(float value)
    {
        OnMasterChanged?.Invoke(value);
    }

    private void HandleBgmChanged(float value)
    {
        OnBgmChanged?.Invoke(value);
    }

    private void HandleSeChanged(float value)
    {
        OnSeChanged?.Invoke(value);
    }

    private void HandleUiChanged(float value)
    {
        OnUiChanged?.Invoke(value);
    }
}