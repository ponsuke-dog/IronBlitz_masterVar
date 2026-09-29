using UnityEngine;

// ============================================
// 個別オブジェクトの時間変更データ
// ============================================

public class TimeModifier
{

    /// <summary>
    /// 時間倍率
    /// </summary>
    public float scale;

    /// <summary>
    /// 終了時間
    /// </summary>
    public float endTime;

    /// <summary>
    /// 合成モード
    /// </summary>
    public TimeModifierMode mode;

    /// <summary>
    /// Override優先順位
    /// </summary>
    public int priority;


    /// <summary>
    /// 手動終了フラグ
    /// </summary>
    bool manual = false;


    // ====================================
    // duration型
    // ====================================

    public TimeModifier(
        float scale,
        float duration,
        TimeModifierMode mode,
        int priority = 0)
    {
        this.scale = scale;
        this.mode = mode;
        this.priority = priority;

        // unscaledTime使用
        // TimeScaleの影響を受けない
        endTime =
            Time.unscaledTime + duration;
    }


    // ====================================
    // 手動型Modifier
    // ====================================

    public TimeModifier(
        float scale,
        TimeModifierMode mode,
        int priority = 0)
    {
        this.scale = scale;
        this.mode = mode;
        this.priority = priority;

        manual = true;
    }


    // ====================================
    // 期限チェック
    // ====================================

    public bool IsExpired()
    {
        if (manual) return false;

        return Time.unscaledTime > endTime;
    }


    // ====================================
    // 強制終了
    // ====================================

    public void ForceExpire()
    {
        endTime = 0;
        manual = false;
    }
}

// ============================================
// 時間ブレンドModifier
// スローや加速を滑らかに変化させる
// ============================================
public class TimeBlendModifier : TimeModifier
{
    // ----------------------------------------
    // 変化開始時間
    // ----------------------------------------
    float startTime;

    // ----------------------------------------
    // 変化前スケール
    // ----------------------------------------
    float startScale;

    // ----------------------------------------
    // 変化後スケール
    // ----------------------------------------
    float targetScale;

    // ----------------------------------------
    // カーブ
    // ----------------------------------------
    AnimationCurve curve;

    // ----------------------------------------
    // 継続時間
    // ----------------------------------------
    float duration;


    public TimeBlendModifier(
        float start,
        float target,
        float duration,
        AnimationCurve curve,
        TimeModifierMode mode,
        int priority = 0)

        : base(start, duration, mode, priority)
    {
        startScale = start;
        targetScale = target;
        this.duration = duration;
        this.curve = curve;

        startTime = Time.unscaledTime;
    }


    // ========================================
    // 現在スケール取得
    // ========================================

    public float GetCurrentScale()
    {
        float t =
            (Time.unscaledTime - startTime)
            / duration;

        t = Mathf.Clamp01(t);

        float curveT = curve.Evaluate(t);

        return Mathf.Lerp(
            startScale,
            targetScale,
            curveT);
    }
}