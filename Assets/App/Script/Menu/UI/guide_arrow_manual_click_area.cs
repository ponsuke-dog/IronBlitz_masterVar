using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// EventSystemの選択状態に影響を与えずに、ガイド矢印のクリックだけを検知するクラス。
/// 
/// 【仕様】
/// - Buttonコンポーネントを使わない
/// - IPointerClickHandlerなどのEventSystem系イベントを使わない
/// - RectTransformの範囲内にマウスがあるかを自前で判定する
/// - 矢印をクリックしても、現在選択中のメニューボタンに影響を出さない
/// </summary>
public class GuideArrowManualClickArea : MonoBehaviour
{
    [Header("Click Event")]
    [SerializeField] private UnityEvent onClick;

    [Header("View")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;
    [SerializeField] private Sprite pressedSprite;

    [Header("Settings")]
    [Tooltip("ONにすると、このオブジェクト配下のGraphicのRaycast Targetを自動でOFFにします。")]
    [SerializeField] private bool disableRaycastTargetsOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Camera uiCamera;

    private bool isPointerInside;
    private bool isPressedInside;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (normalSprite == null && targetImage != null)
        {
            normalSprite = targetImage.sprite;
        }

        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (disableRaycastTargetsOnAwake)
        {
            DisableRaycastTargets();
        }

        ApplySprite(normalSprite);
    }

    private void Update()
    {
        if (rectTransform == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        isPointerInside = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            mousePosition,
            uiCamera
        );

        UpdateClick(mousePosition);
        UpdateVisual();
    }

    /// <summary>
    /// マウスクリックを自前で判定する。
    /// EventSystemを使わないため、メニューボタンの選択状態を奪わない。
    /// </summary>
    private void UpdateClick(Vector2 mousePosition)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isPressedInside = isPointerInside;

            if (enableDebugLog && isPressedInside)
            {
                Debug.Log($"[GuideArrowManualClickArea] Pressed: {gameObject.name}");
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bool shouldClick = isPressedInside && isPointerInside;

            isPressedInside = false;

            if (shouldClick)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[GuideArrowManualClickArea] Click: {gameObject.name}");
                }

                onClick?.Invoke();
            }
        }
    }

    /// <summary>
    /// マウス位置と押下状態に応じて見た目だけを切り替える。
    /// EventSystemの選択状態は変更しない。
    /// </summary>
    private void UpdateVisual()
    {
        if (isPressedInside && isPointerInside)
        {
            ApplySprite(pressedSprite != null ? pressedSprite : highlightedSprite);
            return;
        }

        if (isPointerInside)
        {
            ApplySprite(highlightedSprite != null ? highlightedSprite : normalSprite);
            return;
        }

        ApplySprite(normalSprite);
    }

    /// <summary>
    /// 表示Spriteを変更する。
    /// </summary>
    private void ApplySprite(Sprite sprite)
    {
        if (targetImage == null || sprite == null)
        {
            return;
        }

        if (targetImage.sprite == sprite)
        {
            return;
        }

        targetImage.sprite = sprite;
    }

    /// <summary>
    /// 矢印配下のGraphicがEventSystemのRaycast対象にならないようにする。
    /// これによりクリックしてもUI選択状態を奪いにくくする。
    /// </summary>
    private void DisableRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }
}