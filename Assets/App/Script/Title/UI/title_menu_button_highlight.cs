using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// タイトルメニューのボタン選択状態に応じて見た目を強調します。
/// 選択中のボタンを拡大し、画像色を明るくします。
/// </summary>
[RequireComponent(typeof(Selectable))]
public sealed class TitleMenuButtonHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 selectedScale = new Vector3(1.08f, 1.08f, 1.0f);

    [Header("Color")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1.25f, 1.25f, 1.25f, 1.0f);

    [Header("Animation")]
    [SerializeField] private float changeSpeed = 12.0f;

    private bool isSelected;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        ApplyImmediate(false);
    }

    private void Update()
    {
        Vector3 targetScale = isSelected ? selectedScale : normalScale;
        Color targetColor = isSelected ? selectedColor : normalColor;

        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            Time.unscaledDeltaTime * changeSpeed
        );

        if (targetImage != null)
        {
            targetImage.color = Color.Lerp(
                targetImage.color,
                targetColor,
                Time.unscaledDeltaTime * changeSpeed
            );
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
        isSelected = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            return;
        }

        isSelected = false;
    }

    private void ApplyImmediate(bool selected)
    {
        isSelected = selected;

        if (rectTransform != null)
        {
            rectTransform.localScale = selected ? selectedScale : normalScale;
        }

        if (targetImage != null)
        {
            targetImage.color = selected ? selectedColor : normalColor;
        }
    }
}
