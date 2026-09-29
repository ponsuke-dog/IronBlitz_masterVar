using UnityEngine;
using UnityEngine.UI;

public class TackleGaugeUI : MonoBehaviour
{
    public static TackleGaugeUI Instance;

    [Header("参照")]
    [Tooltip("ゲージの中身画像。Maskの子に置く")]
    [SerializeField] private Image fillImage;

    [Tooltip("中央から左右に削るためのMask")]
    [SerializeField] private RectTransform maskRect;

    [Tooltip("ゲージUIのRoot")]
    [SerializeField] private GameObject gaugeRoot = null;

    private RectTransform fillRect;

    private Sprite defaultSprite;
    private Sprite currentSprite;

    private float initialMaskWidth;
    private float initialMaskHeight;
    private float initialFillWidth;
    private float initialFillHeight;

    private void Awake()
    {
        Instance = this;

        if (fillImage == null || maskRect == null)
        {
            Debug.LogError("TackleGaugeUI : fillImage または maskRect が設定されていません。");
            enabled = false;
            return;
        }

        fillRect = fillImage.rectTransform;

        defaultSprite = fillImage.sprite;
        currentSprite = defaultSprite;

        // レイアウトはPrefab側の設定を尊重する。
        // ここでは親子関係・位置・Pivot・Anchor・Scaleを変更しない。

        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;

        // Maskが未設定なら最低限RectMask2Dを付ける
        if (maskRect.GetComponent<RectMask2D>() == null &&
            maskRect.GetComponent<Mask>() == null)
        {
            maskRect.gameObject.AddComponent<RectMask2D>();
        }

        // 起動時のサイズを「満タン時のサイズ」として保持
        initialMaskWidth = Mathf.Max(maskRect.rect.width, maskRect.sizeDelta.x);
        initialMaskHeight = Mathf.Max(maskRect.rect.height, maskRect.sizeDelta.y);

        initialFillWidth = Mathf.Max(fillRect.rect.width, fillRect.sizeDelta.x);
        initialFillHeight = Mathf.Max(fillRect.rect.height, fillRect.sizeDelta.y);

        if (initialMaskWidth <= 0f) initialMaskWidth = 1f;
        if (initialMaskHeight <= 0f) initialMaskHeight = 1f;

        if (initialFillWidth <= 0f) initialFillWidth = initialMaskWidth;
        if (initialFillHeight <= 0f) initialFillHeight = initialMaskHeight;

        ApplyGaugeAmount(1f);
    }

    private void Update()
    {
        TackleGaugeManager gauge = TackleGaugeManager.Instance;

        if (gauge == null)
            return;

        ApplyVisual(gauge.GetCurrentVisual());
        ApplyGaugeAmount(gauge.GetNormalized());
    }

    private void ApplyVisual(TackleGaugeManager.GaugeVisualStage visual)
    {
        if (visual == null)
            return;

        // =========================
        // スプライト反映
        // =========================
        Sprite nextSprite =
            visual.sprite != null
                ? visual.sprite
                : defaultSprite;

        if (currentSprite != nextSprite)
        {
            currentSprite = nextSprite;
            fillImage.sprite = currentSprite;
        }

        // =========================
        // 色反映
        // =========================
        Color c = visual.color;
        c.a = 1f;

        fillImage.color = c;
    }

    private void ApplyGaugeAmount(float normalized)
    {
        float t = Mathf.Clamp01(normalized);

        // =========================
        // Maskだけ横幅を変える
        // =========================
        maskRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            initialMaskWidth * t);

        maskRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            initialMaskHeight);

        // =========================
        // FillImageは常に満タンサイズ
        // =========================
        if (fillRect != null)
        {
            fillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                initialFillWidth);

            fillRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                initialFillHeight);
        }
    }

    public void SetVisible(bool visible)
        {
            if (gaugeRoot != null)
            {
                gaugeRoot.SetActive(visible);
            }
    }
}