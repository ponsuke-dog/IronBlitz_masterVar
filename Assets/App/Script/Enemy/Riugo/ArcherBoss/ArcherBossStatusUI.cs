using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArcherBossStatusUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("References")]
    [SerializeField] private ArcherBoss boss;

    [Header("Visibility")]
    [Tooltip("ONならBoss側のWaitForExternalStart / StartBoss状態に合わせて表示する")]
    [SerializeField] private bool followBossStartVisibility = true;

    [SerializeField] private bool hideWhenBossDead = false;

    [Tooltip("未設定ならこのGameObjectのCanvasGroupを使う")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Name")]
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private string bossName = "ARCHER BOSS";

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

    [Tooltip("Stun残り時間のゲージ。Stun中に満タンから徐々に減る")]
    [SerializeField] private Image stunFillImage;

    [Header("Text")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text angryText;

    [Header("Display")]
    [SerializeField] private bool showValueText = false;
    [SerializeField] private bool showAngryText = true;

    [Tooltip("ONならStunゲージを使う")]
    [SerializeField] private bool showStunGauge = true;

    [Tooltip("ONならStun中だけStunゲージを表示する")]
    [SerializeField] private bool showStunGaugeOnlyWhenStunned = true;

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

    [Tooltip("Stun開始時に0から滑らかに増やさず、即満タンにする")]
    [SerializeField] private bool instantFillStunOnEnter = true;

    #endregion

    #region Runtime Fields

    private float displayedHpRate = 1f;
    private float displayedHpDamageRate = 1f;
    private float previousTargetHpRate = 1f;
    private float hpDamageDelayTimer = 0f;

    private float displayedStunRate = 0f;

    #endregion

    #region Unity Events

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

        UpdateTexts();
        RefreshVisibility();
        RefreshStunGaugeVisibility();
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
        UpdateTexts();
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

        // ダメージ残りゲージが現在HPゲージより短くならないようにする
        if (displayedHpDamageRate < displayedHpRate)
            displayedHpDamageRate = displayedHpRate;

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);

        previousTargetHpRate = targetHpRate;
    }

    private void UpdateStunGauge()
    {
        RefreshStunGaugeVisibility();

        if (!ShouldShowStunGaugeNow())
        {
            displayedStunRate = 0f;
            SetFillAmount(stunFillImage, displayedStunRate);
            return;
        }

        float targetStunRate = boss.StunDisplayRate;

        // Stun開始時は、0からじわっと満タンになると分かりにくいので即満タンにする
        if (instantFillStunOnEnter &&
            targetStunRate > displayedStunRate + 0.15f)
        {
            displayedStunRate = targetStunRate;
        }
        else if (smoothStunGauge)
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

    #region Text

    private void UpdateTexts()
    {
        if (hpText != null)
        {
            hpText.gameObject.SetActive(showValueText);

            if (showValueText && boss != null)
                hpText.text = $"{boss.CurrentHP:F0} / {boss.MaxHP:F0}";
        }

        if (angryText != null)
        {
            bool show = showAngryText && boss != null && boss.IsAngry;

            angryText.gameObject.SetActive(show);

            if (show)
                angryText.text = "ANGRY";
        }
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

    private bool ShouldShowStunGaugeNow()
    {
        if (!showStunGauge)
            return false;

        if (boss == null)
            return false;

        if (!showStunGaugeOnlyWhenStunned)
            return true;

        return boss.IsStunnedForUI;
    }

    private void RefreshStunGaugeVisibility()
    {
        bool visible = ShouldShowStunGaugeNow();

        if (stunFillImage != null)
            stunFillImage.gameObject.SetActive(visible);

        if (stunBgImage != null)
            stunBgImage.gameObject.SetActive(visible);
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

    public void SetBoss(ArcherBoss newBoss)
    {
        boss = newBoss;

        if (boss != null)
        {
            displayedHpRate = boss.HPRate;
            displayedHpDamageRate = boss.HPRate;
            previousTargetHpRate = boss.HPRate;
            displayedStunRate = boss.StunDisplayRate;
        }
        else
        {
            displayedHpRate = 1f;
            displayedHpDamageRate = 1f;
            previousTargetHpRate = 1f;
            displayedStunRate = 0f;
        }

        hpDamageDelayTimer = 0f;

        SetFillAmount(hpFillImage, displayedHpRate);
        SetFillAmount(hpDamageImage, displayedHpDamageRate);
        SetFillAmount(stunFillImage, displayedStunRate);

        UpdateTexts();
        RefreshVisibility();
        RefreshStunGaugeVisibility();
    }

    #endregion
}