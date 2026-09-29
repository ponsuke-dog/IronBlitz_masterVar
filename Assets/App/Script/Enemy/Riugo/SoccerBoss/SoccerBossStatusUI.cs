using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoccerBossStatusUI : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [SerializeField] private SoccerBoss boss;

    [Header("Visibility")]
    [Tooltip("ONならBoss側のWaitForExternalStart / StartBoss状態に合わせて表示する")]
    [SerializeField] private bool followBossStartVisibility = true;

    [Tooltip("未設定ならこのGameObjectのCanvasGroupを使う")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Name")]
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private string bossName = "SOCCER BOSS";

    [Header("Frame")]
    [Tooltip("BossUI全体のフレーム画像。表示制御のみで、Fill処理はしない")]
    [SerializeField] private Image bossFrameImage;

    [Header("HP Images")]
    [Tooltip("HP背景")]
    [SerializeField] private Image hpBgImage;

    [Tooltip("削られた分を一瞬残すゲージ")]
    [SerializeField] private Image hpDamageImage;

    [Tooltip("現在HPのゲージ")]
    [SerializeField] private Image hpFillImage;

    [Header("Stun Images")]
    [Tooltip("Stun背景")]
    [SerializeField] private Image stunBgImage;

    [Tooltip("現在Stunのゲージ")]
    [SerializeField] private Image stunFillImage;

    [Header("Display")]
    [SerializeField] private bool hideWhenBossDead = false;

    [Tooltip("ONならStunゲージを表示する")]
    [SerializeField] private bool showStunGauge = true;

    [Header("HP Smooth")]
    [SerializeField] private bool smoothHpFill = true;

    [Tooltip("現在HPゲージの追従速度")]
    [SerializeField] private float hpFillLerpSpeed = 18f;

    [Tooltip("削られた分ゲージが減り始めるまでの待ち時間")]
    [SerializeField] private float hpDamageDelay = 0.35f;

    [Tooltip("削られた分ゲージの追従速度")]
    [SerializeField] private float hpDamageLerpSpeed = 4f;

    [Header("Stun Smooth")]
    [SerializeField] private bool smoothStunGauge = true;

    [Tooltip("Stunゲージの追従速度")]
    [SerializeField] private float stunLerpSpeed = 14f;

    #endregion

    #region Runtime

    private float displayedHpRate = 1f;
    private float displayedHpDamageRate = 1f;
    private float previousTargetHpRate = 1f;
    private float hpDamageDelayTimer = 0f;

    private float displayedStunRate = 0f;

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
        SetupStaticImage(stunBgImage);

        SetupFillImage(hpDamageImage);
        SetupFillImage(hpFillImage);
        SetupFillImage(stunFillImage);

        if (boss != null)
        {
            displayedHpRate = boss.HPRate;
            displayedHpDamageRate = boss.HPRate;
            previousTargetHpRate = boss.HPRate;
            displayedStunRate = boss.StunDisplayRate;
        }

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        SetFillAmount(stunFillImage, displayedStunRate);

        RefreshVisibility();
    }

    private void Update()
    {
        if (boss == null)
        {
            SetVisible(false);
            return;
        }

        RefreshVisibility();

        if (!IsVisibleNow())
            return;

        UpdateHpGauge();
        UpdateStunGauge();
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
        {
            displayedHpDamageRate = targetHpRate;
        }

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

    private void UpdateStunGauge()
    {
        if (stunFillImage != null)
            stunFillImage.gameObject.SetActive(showStunGauge);

        if (stunBgImage != null)
            stunBgImage.gameObject.SetActive(showStunGauge);

        if (!showStunGauge)
            return;

        float targetStunRate = boss.StunDisplayRate;

        if (smoothStunGauge)
        {
            float t = 1f - Mathf.Exp(-stunLerpSpeed * Time.unscaledDeltaTime);
            displayedStunRate = Mathf.Lerp(displayedStunRate, targetStunRate, t);
        }
        else
        {
            displayedStunRate = targetStunRate;
        }

        SetFillAmount(stunFillImage, displayedStunRate);
    }

    #endregion

    #region Visibility

    private void RefreshVisibility()
    {
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

    public void SetBoss(SoccerBoss newBoss)
    {
        boss = newBoss;

        if (boss != null)
        {
            displayedHpRate = boss.HPRate;
            displayedHpDamageRate = boss.HPRate;
            previousTargetHpRate = boss.HPRate;
            displayedStunRate = boss.StunDisplayRate;
        }

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        SetFillAmount(stunFillImage, displayedStunRate);

        RefreshVisibility();
    }

    #endregion
}