using System.Collections.Generic;
using UnityEngine;

public class TackleGaugeManager : MonoBehaviour
{
    public static TackleGaugeManager Instance { get; private set; }

    [Header("ゲージ基本")]
    [Tooltip("最大ゲージ量")]
    public float maxGauge = 100f;

    [Tooltip("現在ゲージ量")]
    public float currentGauge = 100f;

    [Header("消費設定")]
    [Tooltip("通常タックル1回の消費量")]
    public float normalCost = 20f;

    [Tooltip("チャージ中の秒間消費量")]
    public float chargeCostPerSecond = 40f;

    [Header("回復設定")]
    [Tooltip("通常時の回復速度。タックルしていない時のみ回復する")]
    public float recoverySpeed = 15f;

    [Tooltip("オーバーヒート回復中の回復速度")]
    public float recoverySpeedOverheat = 40f;

    [Header("ジャスト回復")]
    [Tooltip("ジャスト成功時の回復量")]
    public float justRecoverAmount = 25f;

    [Header("状態")]
    [Tooltip("オーバーヒート直後かどうか")]
    public bool isOverheat = false;

    [System.Serializable]
    public class GaugeVisualStage
    {
        [Tooltip("現在ゲージ割合がこの値以下なら、この見た目になる。例：0.25なら25%以下")]
        [Range(0f, 1f)]
        public float threshold = 1f;

        [Tooltip("この段階でゲージ画像に乗算する色。テクスチャ本来の色を出したい場合は白")]
        public Color color = Color.white;

        [Tooltip("この段階で使うスプライト。未設定ならUI側の初期スプライトを使う")]
        public Sprite sprite;
    }

    [Header("ゲージ表示設定")]
    [Tooltip("ゲージの見た目段階。並び順に依存せず、現在値以上のthresholdのうち一番小さいものを採用する")]
    public List<GaugeVisualStage> visualStages = new List<GaugeVisualStage>();

    [Header("オーバーヒート回復中表示")]
    [Tooltip("オーバーヒート回復中にゲージ画像へ乗算する色")]
    public Color overheatRecoverColor = Color.yellow;

    [Tooltip("オーバーヒート回復中に使うスプライト。未設定ならUI側の初期スプライトを使う")]
    public Sprite overheatRecoverSprite;

    private bool wasOverheat = false;

    // ★ 状態制御
    private bool isTackling = false;
    private bool isCharging = false;
    private bool isChargeComplete = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // タックル・チャージ中は回復しない
        if (!isTackling && !isCharging)
        {
            Recover(dt);
        }
    }

    // =========================
    // 状態制御
    // =========================
    public void SetTackling(bool value)
    {
        isTackling = value;
    }

    public void SetCharging(bool value)
    {
        isCharging = value;

        if (!value)
        {
            isChargeComplete = false;
        }
    }

    public void SetChargeComplete()
    {
        isChargeComplete = true;
    }

    // =========================
    // 通常タックル
    // =========================
    public bool TryConsumeNormal()
    {
        if (!CanUse()) return false;

        currentGauge -= normalCost;

        CheckOverheat();

        return true;
    }

    // =========================
    // チャージ消費
    // =========================
    public bool ConsumeCharge(float dt)
    {
        if (!CanUse()) return false;

        // 完了後は消費しない
        if (isChargeComplete) return false;

        currentGauge -= chargeCostPerSecond * dt;

        CheckOverheat();

        return currentGauge <= 0f;
    }

    // =========================
    // 回復
    // =========================
    private void Recover(float dt)
    {
        float speed = wasOverheat ? recoverySpeedOverheat : recoverySpeed;

        currentGauge += speed * dt;
        currentGauge = Mathf.Min(currentGauge, maxGauge);

        // オーバーヒート直後、0から少しでも回復したら回復中へ
        if (isOverheat && currentGauge > 0f)
        {
            isOverheat = false;
            wasOverheat = true;
        }

        // 満タンで完全復帰
        if (currentGauge >= maxGauge)
        {
            currentGauge = maxGauge;
            isOverheat = false;
            wasOverheat = false;
        }
    }

    // =========================
    // ジャスト回復
    // =========================
    public void RecoverByJust()
    {
        currentGauge += justRecoverAmount;
        currentGauge = Mathf.Min(currentGauge, maxGauge);
    }

    public bool CanUse()
    {
        // オーバーヒート中・回復中は使用不可
        return !isOverheat && !wasOverheat;
    }

    private void CheckOverheat()
    {
        if (currentGauge <= 0f)
        {
            currentGauge = 0f;
            isOverheat = true;
            wasOverheat = true;
        }
    }

    public float GetNormalized()
    {
        if (maxGauge <= 0f)
            return 0f;

        return Mathf.Clamp01(currentGauge / maxGauge);
    }

    public GaugeVisualStage GetCurrentVisual()
    {
        float t = GetNormalized();

        // =========================
        // オーバーヒート回復中
        // =========================
        if (wasOverheat)
        {
            return new GaugeVisualStage
            {
                threshold = 1f,
                color = overheatRecoverColor,
                sprite = overheatRecoverSprite
            };
        }

        if (visualStages == null || visualStages.Count <= 0)
            return null;

        GaugeVisualStage selected = null;
        float selectedThreshold = float.MaxValue;

        // 現在値以上のthresholdの中で、一番小さいthresholdを採用
        // 例：
        //  t = 0.30
        //  0.25 は通過しない
        //  0.45 が採用される
        //  1.00 は候補だが0.45より大きいので採用されない
        for (int i = 0; i < visualStages.Count; i++)
        {
            GaugeVisualStage stage = visualStages[i];

            if (stage == null)
                continue;

            if (t <= stage.threshold && stage.threshold < selectedThreshold)
            {
                selected = stage;
                selectedThreshold = stage.threshold;
            }
        }

        if (selected != null)
            return selected;

        // 念のため、どれにも該当しない場合はthreshold最大のもの
        GaugeVisualStage fallback = null;
        float maxThreshold = float.MinValue;

        for (int i = 0; i < visualStages.Count; i++)
        {
            GaugeVisualStage stage = visualStages[i];

            if (stage == null)
                continue;

            if (stage.threshold > maxThreshold)
            {
                fallback = stage;
                maxThreshold = stage.threshold;
            }
        }

        return fallback;
    }
}