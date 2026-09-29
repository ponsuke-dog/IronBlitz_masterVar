using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class StageSlectScrool : MonoBehaviour
{
    public static StageSlectScrool Instance;

    [SerializeField] public RectTransform content;
    [SerializeField] public RectTransform viewport;

    [SerializeField] public float scrollSpeed = 50f;
    [SerializeField] public float snapSpeed = 10f;
    [SerializeField] public float buttonSpace = 100f;

    public List<RectTransform> stageItems = new List<RectTransform>();

    private bool isSnapping = false;
    private RectTransform targetItem;

    private float controllerCooldown = 0.2f;
    private float controllerTimer = 0f;
    private GameObject lastSelected;

    private void Awake()
    {
        // シングルトン
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ButtonGet()
    {
        stageItems.Clear();
 

        foreach (Transform child in content)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null)
            {
                stageItems.Add(rect);
            }
        }
        ArrangeButtons();
    }

    void ArrangeButtons()
    {
        float y = -285f;

        foreach (var item in stageItems)
        {
            item.anchoredPosition = new Vector2(item.anchoredPosition.x, -y);
            y += buttonSpace;
        }
    }

    void Update()
    {
    //    HandleMouseWheel();
    //    HandleControllerInput();
        AutoSnapSelectedButton();
        
        DetectSelectionChange();   

        if (isSnapping && targetItem != null)
            SnapToTarget();

        UpdateItemAlpha();
    }

    private void LateUpdate()
    {

        UpdateItemSlantX();
    }

    // マウスホイールでスクロール
    //void HandleMouseWheel()
    //{
    //    float wheel = Mouse.current.scroll.ReadValue().y;
    //    if (Mathf.Abs(wheel) > 0.1f)
    //    {
    //        content.anchoredPosition += new Vector2(0, wheel * scrollSpeed * Time.deltaTime);
    //        isSnapping = false;
    //    }
    //}

    // コントローラー上下入力でスクロール
    //void HandleControllerInput()
    //{
    //    controllerTimer -= Time.deltaTime;

    //    float moveY = Gamepad.current?.leftStick.ReadValue().y ?? 0f;
    //    float dpadY = Gamepad.current?.dpad.ReadValue().y ?? 0f;

    //    float input = moveY + dpadY;

    //    if (Mathf.Abs(input) > 0.5f && controllerTimer <= 0f)
    //    {
    //        content.anchoredPosition += new Vector2(0, input * scrollSpeed);
    //        controllerTimer = controllerCooldown;
    //        isSnapping = false;
    //    }
    //}


    // 選択中ボタンが画面外なら自動スクロール
    void AutoSnapSelectedButton()
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return;

        RectTransform item = selected.GetComponent<RectTransform>();
        if (item == null) return;

        // ボタンのワールド座標を取得
        Vector3 worldPos = item.transform.position;

        // Viewport の矩形内に収まっているか判定
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, worldPos))
        {
            // 画面外 → 自動スナップ
            SnapToItem(item);
        }
    }

    // 任意のボタンへスナップ
    public void SnapToItem(RectTransform item)
    {
        targetItem = item;
        isSnapping = true;
    }

    // スナップ処理
    void SnapToTarget()
    {
        float targetY = -targetItem.anchoredPosition.y;
        float newY = Mathf.Lerp(content.anchoredPosition.y, targetY, snapSpeed * Time.deltaTime);

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, newY);

        if (Mathf.Abs(content.anchoredPosition.y - targetY) < 0.5f)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
            isSnapping = false;
        }
    }

    // ボタンのAlpha変更
    void UpdateItemAlpha()
    {
        foreach (var item in stageItems)
        {
            // ボタンのワールド座標
            Vector3 worldPos = item.transform.position;

            // Viewport のローカル座標に変換
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null,
                out localPos
            );

            // 中央からの距離（localPos.y = Viewport中心が0）
            float distance = Mathf.Abs(localPos.y);

            // フェード範囲（Viewportの半分）
            float fadeRange = viewport.rect.height * 0.5f;

            // 0 = 中央、1 = 端
            float t = Mathf.Clamp01(distance / fadeRange);

            // Alpha（中央1 → 端0.3）
            float alpha = Mathf.Lerp(1f, 0.3f, t);

            // CanvasGroup に適用
            var cg = item.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = alpha;
        }
    }

    void UpdateItemSlantX()
    {
        float viewportHeight = viewport.rect.height;
        float viewportCenterY = viewportHeight / 2f;

        foreach (var item in stageItems)
        {
            if (item == null || item.Equals(null))
                continue;
            // ボタンのワールド座標
            Vector3 worldPos = item.transform.position;

            // Viewport のローカル座標に変換
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                RectTransformUtility.WorldToScreenPoint(null, worldPos),
                null,
                out localPos
            );

            // 中央からの距離（localPos.y = 0 が中央）
            float distance = Mathf.Abs(localPos.y);

            // ずらし範囲（Viewport の半分）
            float slantRange = viewportHeight * 0.5f;

            // 0 = 中央、1 = 端
            float t = Mathf.Clamp01(distance / slantRange);

            // X方向のずらし量（中央は0、端は±200px）
            float offsetX = Mathf.Lerp(0f, 200f, t);

            // 上は右、下は左にずらす
            float sign = (localPos.y > 0) ? 1f : -1f;

            // Vertical Layout Group は Y を管理するが X は自由
            item.anchoredPosition = new Vector2(sign * offsetX, item.anchoredPosition.y);
        }
    }

    void DetectSelectionChange()
    {
        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current == null)
            return;

        if (current != lastSelected)
        {
            RectTransform item = current.GetComponent<RectTransform>();
            if (item != null)
            {
                SnapToItem(item);   // ← 中央に持ってくる
            }

            lastSelected = current;
        }
    }

    public void ResetScroll()
    {
        stageItems.Clear();
        lastSelected = null;
        targetItem = null;
        isSnapping = false;
    }

}
