using System.Collections.Generic;
using UnityEngine;

// ============================================
// 各オブジェクトの時間制御
// ============================================

public class TimeAgent : MonoBehaviour
{
    /// <summary>
    /// このオブジェクトが属する時間グループ
    /// </summary>
    public TimeGroupType group = TimeGroupType.Stage;

    /// <summary>
    /// 上位レイヤー
    /// </summary>
    public TimeLayerType layer = TimeLayerType.Gameplay;


    /// <summary>
    /// 演出チャンネル
    /// </summary>
    public TimeChannelType channel = TimeChannelType.Default;


    /// <summary>
    /// 最終的な時間倍率
    /// </summary>
    public float TimeScale { get; private set; } = 1f;


    /// <summary>
    /// Update用DeltaTime
    /// </summary>
    public float LocalDeltaTime { get; private set; }


    /// <summary>
    /// 物理用DeltaTime
    /// </summary>
    public float LocalFixedDeltaTime { get; private set; }



    /// <summary>
    ///このオブジェクトに適用される
    // すべての時間Modifier
    /// </summary>
    List<TimeModifier> modifiers = new List<TimeModifier>();


    void OnEnable()
    {
        TryRegister();
    }

    void Start()
    {
        TryRegister();
    }

    void TryRegister()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.RegisterAgent(this);
        }
    }

    void OnDisable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.UnregisterAgent(this);
        }
    }


    void Update()
    {
        // Modifier更新
        UpdateModifiers();

        // ローカル時間計算
        LocalDeltaTime =
            Time.deltaTime * TimeScale;
    }


    void FixedUpdate()
    {
        LocalFixedDeltaTime =
            Time.fixedDeltaTime * TimeScale;
    }


    // ====================================
    // Modifier追加
    // ====================================
    // scale     : 時間倍率
    // duration  : 継続時間
    // mode      : 合成方式
    // priority  : Override時の優先順位
    // ====================================

    public TimeHandle AddModifier(
        float scale,
        float duration,
        TimeModifierMode mode =
        TimeModifierMode.Multiply,
        int priority = 0)
    {
        // Modifier生成
        var mod =
            new TimeModifier(
                scale,
                duration,
                mode,
                priority);

        // リスト追加
        modifiers.Add(mod);

        // 外部制御用Handle返却
        return new TimeHandle(mod);
    }


    // ====================================
    // Modifier更新
    // ====================================

    void UpdateModifiers()
    {
        float multiply = 1f;
        float min = 1f;

        float overrideScale = 1f;
        int overridePriority = int.MinValue;
        bool overrideUsed = false;

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            var mod = modifiers[i];

            if (mod.IsExpired())
            {
                modifiers.RemoveAt(i);
                continue;
            }

            float scale =
                (mod is TimeBlendModifier blend)
                ? blend.GetCurrentScale()
                : mod.scale;

            switch (mod.mode)
            {
                case TimeModifierMode.Multiply:

                    multiply *= scale;
                    break;


                case TimeModifierMode.Min:

                    min = Mathf.Min(min, scale);
                    break;


                case TimeModifierMode.Override:

                    if (mod.priority > overridePriority)
                    {
                        overridePriority = mod.priority;
                        overrideScale = scale;
                        overrideUsed = true;
                    }

                    break;
            }
        }

        float groupScale =
            GameTimeManager.Instance.GetGroupScale(group);

        float layerScale =
            GameTimeManager.Instance.GetLayerScale(layer);

        float channelScale =
            GameTimeManager.Instance.GetChannelScale(channel);

        float baseScale =
            GameTimeManager.Instance.GlobalScale *
            layerScale *
            groupScale *
            channelScale *
            multiply *
            min;

        TimeScale =
            overrideUsed ? overrideScale : baseScale;
    }
}