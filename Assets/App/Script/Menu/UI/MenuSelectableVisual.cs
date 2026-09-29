using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 選択中、またはマウスカーソルが乗っているUIを強調表示するクラス。
/// 
/// 【今回の追加仕様】
/// - マウスカーソルがボタンに乗った時、そのボタンをEventSystemの選択対象にする
/// - キー操作とマウス操作で「選択中ボタン」の見た目がズレないようにする
/// - Sprite SwapのSelected Spriteもマウス位置に追従するようにする
/// </summary>
public class MenuSelectableVisual : MonoBehaviour,
    ISelectHandler,
    IDeselectHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("見た目変更対象")]
    [SerializeField] private Graphic targetGraphic;

    [Header("色")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = new Color(1.0f, 0.82f, 0.2f, 1.0f);

    [Header("スケール")]
    [SerializeField] private float normalScale = 1.0f;
    [SerializeField] private float highlightedScale = 1.08f;

    [Header("マウス操作")]
    [SerializeField] private bool selectOnPointerEnter = true;

    private bool isSelected;
    private bool isPointerOver;

    private void Reset()
    {
        targetGraphic = GetComponent<Graphic>();
    }

    private void Awake()
    {
        ApplyVisual();
    }

    private void OnDisable()
    {
        isSelected = false;
        isPointerOver = false;
        ApplyVisual();
    }

    /// <summary>
    /// キーボード / ゲームパッド操作で選択された時に呼ばれる。
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        isPointerOver = false;

        ApplyVisual();
    }

    /// <summary>
    /// キーボード / ゲームパッド操作で選択が外れた時に呼ばれる。
    /// </summary>
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        ApplyVisual();
    }

    /// <summary>
    /// マウスカーソルが乗った時に呼ばれる。
    /// 
    /// ここでEventSystemの選択対象もこのUIに変更する。
    /// これにより、ButtonのSelected Spriteもカーソル位置に追従する。
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
        {
            return;
        }

        isPointerOver = true;

        if (selectOnPointerEnter)
        {
            SelectThisObject();
        }
        else
        {
            ApplyVisual();
        }
    }

    /// <summary>
    /// マウスカーソルが外れた時に呼ばれる。
    /// 
    /// EventSystemの選択は解除しない。
    /// 解除すると、カーソルが少し外れただけで選択状態が消えて操作感が悪くなるため。
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        ApplyVisual();
    }

    /// <summary>
    /// このUIをEventSystem上の選択対象にする。
    /// </summary>
    private void SelectThisObject()
    {
        if (EventSystem.current == null)
        {
            ApplyVisual();
            return;
        }

        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            ApplyVisual();
            return;
        }

        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    /// <summary>
    /// Selectableが操作可能か確認する。
    /// </summary>
    private bool IsInteractable()
    {
        Selectable selectable = GetComponent<Selectable>();

        if (selectable == null)
        {
            return true;
        }

        return selectable.interactable;
    }

    /// <summary>
    /// 現在の状態に応じて見た目を反映する。
    /// </summary>
    private void ApplyVisual()
    {
        bool highlighted = isSelected || isPointerOver;

        if (targetGraphic != null)
        {
            targetGraphic.color = highlighted
                ? highlightedColor
                : normalColor;
        }

        transform.localScale = Vector3.one *
            (highlighted ? highlightedScale : normalScale);
    }
}