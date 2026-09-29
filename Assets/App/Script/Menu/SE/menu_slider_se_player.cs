using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slider操作時にメニューUI用SEを再生するクラス。
/// </summary>
[RequireComponent(typeof(Slider))]
public class MenuSliderSePlayer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool ignoreFirstValueChange = true;

    [Tooltip("有効化直後、この秒数以内の値変更では音を鳴らしません。")]
    [SerializeField] private float suppressDurationOnEnable = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private Slider targetSlider;
    private bool hasInitialized;
    private float enabledTime;

    private void Awake()
    {
        targetSlider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }

        enabledTime = Time.unscaledTime;
        hasInitialized = ignoreFirstValueChange == false;

        targetSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnDisable()
    {
        if (targetSlider != null)
        {
            targetSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }

    private void OnSliderValueChanged(float value)
    {
        if (Time.unscaledTime - enabledTime < suppressDurationOnEnable)
        {
            return;
        }

        if (hasInitialized == false)
        {
            hasInitialized = true;
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.Slider);

        if (enableDebugLog)
        {
            Debug.Log($"[MenuSliderSePlayer] Slider SE. Object = {gameObject.name}, Value = {value}");
        }
    }
}