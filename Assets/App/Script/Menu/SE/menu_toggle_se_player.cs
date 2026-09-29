using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle操作時にメニューUI用SEを再生するクラス。
/// </summary>
[RequireComponent(typeof(Toggle))]
public class MenuToggleSePlayer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool ignoreFirstValueChange = true;

    [Tooltip("有効化直後、この秒数以内の値変更では音を鳴らしません。")]
    [SerializeField] private float suppressDurationOnEnable = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private Toggle targetToggle;
    private bool hasInitialized;
    private float enabledTime;

    private void Awake()
    {
        targetToggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        if (targetToggle == null)
        {
            targetToggle = GetComponent<Toggle>();
        }

        enabledTime = Time.unscaledTime;
        hasInitialized = ignoreFirstValueChange == false;

        targetToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnDisable()
    {
        if (targetToggle != null)
        {
            targetToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }
    }

    private void OnToggleValueChanged(bool value)
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

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.Toggle);

        if (enableDebugLog)
        {
            Debug.Log($"[MenuToggleSePlayer] Toggle SE. Object = {gameObject.name}, Value = {value}");
        }
    }
}