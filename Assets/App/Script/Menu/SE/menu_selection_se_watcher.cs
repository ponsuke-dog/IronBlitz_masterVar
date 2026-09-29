using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// EventSystemの選択中オブジェクトを監視し、選択対象が変わった時にカーソル移動SEを鳴らすクラス。
/// </summary>
public class MenuSelectionSeWatcher : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool playOnSelectableOnly = true;
    [SerializeField] private bool ignoreFirstSelection = true;

    [Tooltip("同じフレーム付近で複数回選択が変わった場合の二重再生を抑制します。")]
    [SerializeField] private float localCooldown = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private GameObject previousSelectedObject;
    private bool hasInitialized;
    private float lastPlayTime = -999.0f;

    private void OnEnable()
    {
        previousSelectedObject = GetCurrentSelectedObject();
        hasInitialized = ignoreFirstSelection == false;
        lastPlayTime = -999.0f;
    }

    private void Update()
    {
        GameObject currentSelectedObject = GetCurrentSelectedObject();

        if (currentSelectedObject == previousSelectedObject)
        {
            return;
        }

        previousSelectedObject = currentSelectedObject;

        if (currentSelectedObject == null)
        {
            return;
        }

        if (playOnSelectableOnly && IsSelectable(currentSelectedObject) == false)
        {
            return;
        }

        if (hasInitialized == false)
        {
            hasInitialized = true;
            return;
        }

        if (Time.unscaledTime - lastPlayTime < localCooldown)
        {
            return;
        }

        lastPlayTime = Time.unscaledTime;

        if (MenuUiSeController.Instance != null)
        {
            MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.CursorMove);
        }

        if (enableDebugLog)
        {
            Debug.Log($"[MenuSelectionSeWatcher] Cursor Move SE. Object = {currentSelectedObject.name}");
        }
    }

    private GameObject GetCurrentSelectedObject()
    {
        if (EventSystem.current == null)
        {
            return null;
        }

        return EventSystem.current.currentSelectedGameObject;
    }

    private bool IsSelectable(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        Selectable selectable = target.GetComponent<Selectable>();

        if (selectable == null)
        {
            return false;
        }

        return selectable.IsInteractable();
    }
}