using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RugbyBossStatusUI : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [SerializeField] private RugbyBoss boss;

    [Header("Visibility")]
    [Tooltip("ONならBoss側のStartBoss状態に合わせて表示します。")]
    [SerializeField] private bool followBossStartVisibility = true;

    [Tooltip("未設定ならこのGameObjectのCanvasGroupを使います。")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Name")]
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private string bossName = "RUGBY BOSS";

    [Header("Frame")]
    [Tooltip("BossUI全体のフレーム画像。表示制御のみで、Fill処理はしません。")]
    [SerializeField] private Image bossFrameImage;

    [Header("HP Images")]
    [Tooltip("HP背景")]
    [SerializeField] private Image hpBgImage;

    [Tooltip("削られた分を一瞬残すゲージ")]
    [SerializeField] private Image hpDamageImage;

    [Tooltip("現在HPのゲージ")]
    [SerializeField] private Image hpFillImage;

    [Header("Down Images")]
    [Tooltip("Down背景")]
    [SerializeField] private Image downBgImage;

    [Tooltip("現在Downのゲージ")]
    [SerializeField] private Image downFillImage;

    [Header("Display")]
    [SerializeField] private bool hideWhenBossDead = false;

    [Tooltip("ONならDownゲージを表示します。")]
    [SerializeField] private bool showDownGauge = true;

    [Header("HP Smooth")]
    [SerializeField] private bool smoothHpFill = true;

    [Tooltip("現在HPゲージの追従速度")]
    [SerializeField] private float hpFillLerpSpeed = 18f;

    [Tooltip("削られた分ゲージが減り始めるまでの待ち時間")]
    [SerializeField] private float hpDamageDelay = 0.35f;

    [Tooltip("削られた分ゲージの追従速度")]
    [SerializeField] private float hpDamageLerpSpeed = 4f;

    [Header("Down Smooth")]
    [SerializeField] private bool smoothDownGauge = true;

    [Tooltip("Downゲージの追従速度")]
    [SerializeField] private float downLerpSpeed = 14f;

    #endregion

    #region Runtime

    private float displayedHpRate = 1f;
    private float displayedHpDamageRate = 1f;
    private float previousTargetHpRate = 1f;
    private float hpDamageDelayTimer = 0f;
    private float displayedDownRate = 0f;

    private bool externalVisibilityOverride = false;
    private bool externalVisible = true;

    #endregion

    #region Unity

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (bossNameText != null)
            bossNameText.text = bossName;

        SetupStaticImage(bossFrameImage);
        SetupStaticImage(hpBgImage);
        SetupStaticImage(downBgImage);
        SetupFillImage(hpDamageImage);
        SetupFillImage(hpFillImage);
        SetupFillImage(downFillImage);

        if (boss != null)
        {
            displayedHpRate = boss.HPRate;
            displayedHpDamageRate = boss.HPRate;
            previousTargetHpRate = boss.HPRate;
            displayedDownRate = boss.DownDisplayRate;
        }

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        SetFillAmount(downFillImage, displayedDownRate);
        RefreshVisibility();
    }

    private void Update()
    {
        if (boss == null)
        {
            if (!externalVisibilityOverride)
                SetVisible(false);
            else
                RefreshVisibility();

            return;
        }

        RefreshVisibility();

        if (!IsVisibleNow())
            return;

        UpdateHpGauge();
        UpdateDownGauge();
    }

    #endregion

    #region Update Gauge

    private void UpdateHpGauge()
    {
        float targetHpRate = boss.HPRate;
        bool hpDecreased = targetHpRate < previousTargetHpRate - 0.0001f;
        bool hpIncreased = targetHpRate > previousTargetHpRate + 0.0001f;

        if (hpDecreased)
        {
            hpDamageDelayTimer = hpDamageDelay;

            if (displayedHpDamageRate < previousTargetHpRate)
                displayedHpDamageRate = previousTargetHpRate;
        }

        if (hpIncreased)
            displayedHpDamageRate = targetHpRate;

        if (smoothHpFill)
        {
            float t = 1f - Mathf.Exp(-hpFillLerpSpeed * Time.unscaledDeltaTime);
            displayedHpRate = Mathf.Lerp(displayedHpRate, targetHpRate, t);
        }
        else
        {
            displayedHpRate = targetHpRate;
        }

        if (hpDamageDelayTimer > 0f)
        {
            hpDamageDelayTimer -= Time.unscaledDeltaTime;
        }
        else
        {
            float t = 1f - Mathf.Exp(-hpDamageLerpSpeed * Time.unscaledDeltaTime);
            displayedHpDamageRate = Mathf.Lerp(displayedHpDamageRate, targetHpRate, t);
        }

        if (displayedHpDamageRate < displayedHpRate)
            displayedHpDamageRate = displayedHpRate;

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        previousTargetHpRate = targetHpRate;
    }

    private void UpdateDownGauge()
    {
        if (downFillImage != null)
            downFillImage.gameObject.SetActive(showDownGauge);

        if (downBgImage != null)
            downBgImage.gameObject.SetActive(showDownGauge);

        if (!showDownGauge)
            return;

        float targetDownRate = boss.DownDisplayRate;

        if (smoothDownGauge)
        {
            float t = 1f - Mathf.Exp(-downLerpSpeed * Time.unscaledDeltaTime);
            displayedDownRate = Mathf.Lerp(displayedDownRate, targetDownRate, t);
        }
        else
        {
            displayedDownRate = targetDownRate;
        }

        SetFillAmount(downFillImage, displayedDownRate);
    }

    #endregion

    #region Visibility

    private void RefreshVisibility()
    {
        if (externalVisibilityOverride)
        {
            SetVisible(externalVisible);
            return;
        }

        if (boss == null)
        {
            SetVisible(false);
            return;
        }

        bool visible = true;

        if (followBossStartVisibility)
            visible = boss.ShouldShowBossUI;

        if (hideWhenBossDead && boss.IsBossDead)
            visible = false;

        SetVisible(visible);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private bool IsVisibleNow()
    {
        if (canvasGroup == null)
            return true;

        return canvasGroup.alpha > 0.01f;
    }

    #endregion

    #region Image Setup

    private void SetupStaticImage(Image image)
    {
        if (image == null)
            return;

        image.raycastTarget = false;
    }

    private void SetupFillImage(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.raycastTarget = false;
    }

    private void SetFillAmount(Image image, float value)
    {
        if (image == null)
            return;

        image.fillAmount = Mathf.Clamp01(value);
    }

    #endregion

    #region Public

    public void SetBoss(RugbyBoss newBoss)
    {
        boss = newBoss;

        if (boss != null)
        {
            displayedHpRate = boss.HPRate;
            displayedHpDamageRate = boss.HPRate;
            previousTargetHpRate = boss.HPRate;
            displayedDownRate = boss.DownDisplayRate;
        }

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        SetFillAmount(downFillImage, displayedDownRate);
        RefreshVisibility();
    }

    public void SetExternalVisible(bool visible)
    {
        externalVisibilityOverride = true;
        externalVisible = visible;
        SetVisible(visible);
    }

    public void ShowExternal()
    {
        SetExternalVisible(true);
    }

    public void HideExternal()
    {
        SetExternalVisible(false);
    }

    public void ClearExternalVisibilityOverride()
    {
        externalVisibilityOverride = false;
        RefreshVisibility();
    }

    #endregion
}
