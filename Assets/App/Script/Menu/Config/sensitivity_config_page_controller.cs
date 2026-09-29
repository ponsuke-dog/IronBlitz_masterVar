using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マウス・コントローラー感度設定ページを制御するクラス。
/// InputSensitivityServiceとSliderを接続する。
/// </summary>
public class SensitivityConfigPageController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Slider controllerSensitivitySlider;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;

    [Header("Sensitivity Service")]
    [SerializeField] private InputSensitivityService inputSensitivityService;

    private void Awake()
    {
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        controllerSensitivitySlider.onValueChanged.AddListener(OnControllerSensitivityChanged);

        resetButton.onClick.AddListener(OnClickReset);
    }

    private void Start()
    {
        ApplyCurrentValuesToSliders();
    }

    /// <summary>
    /// 現在保存されている感度値をSliderへ反映する。
    /// </summary>
    private void ApplyCurrentValuesToSliders()
    {
        InputSensitivityData data = inputSensitivityService.Load();

        mouseSensitivitySlider.SetValueWithoutNotify(data.mouseSensitivity);
        controllerSensitivitySlider.SetValueWithoutNotify(data.controllerSensitivity);
    }

    private void OnMouseSensitivityChanged(float value)
    {
        inputSensitivityService.SetMouseSensitivity(value);
    }

    private void OnControllerSensitivityChanged(float value)
    {
        inputSensitivityService.SetControllerSensitivity(value);
    }

    private void OnClickReset()
    {
        inputSensitivityService.ResetSensitivity();
        ApplyCurrentValuesToSliders();
    }
}