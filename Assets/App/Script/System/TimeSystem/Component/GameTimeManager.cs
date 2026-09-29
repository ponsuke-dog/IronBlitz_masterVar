using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================
// ゲーム全体の時間管理
// ============================================

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    // ----------------------------------------
    // 全体時間
    // ----------------------------------------

    public float GlobalScale = 1f;



    static readonly int GROUP_COUNT =
        Enum.GetValues(typeof(TimeGroupType)).Length;

    static readonly int LAYER_COUNT =
        Enum.GetValues(typeof(TimeLayerType)).Length;

    static readonly int CHANNEL_COUNT =
        Enum.GetValues(typeof(TimeChannelType)).Length;


    float[] groupScales =
        new float[GROUP_COUNT];

    float[] layerScales =
        new float[LAYER_COUNT];

    float[] channelScales =
        new float[CHANNEL_COUNT];


    List<TimeAgent> agents =
        new List<TimeAgent>();


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        for (int i = 0; i < GROUP_COUNT; i++)
            groupScales[i] = 1f;

        for (int i = 0; i < LAYER_COUNT; i++)
            layerScales[i] = 1f;

        for (int i = 0; i < CHANNEL_COUNT; i++)
            channelScales[i] = 1f;
    }


    // ====================================
    // Agent登録
    // ====================================

    public void RegisterAgent(TimeAgent agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
            //Debug.Log("RegisterAgent : " + agent.name);
        }
    }

    public void UnregisterAgent(TimeAgent agent)
    {
        agents.Remove(agent);
    }


    // ====================================
    // Scale取得
    // ====================================

    public float GetGroupScale(TimeGroupType group)
    {
        return groupScales[(int)group];
    }

    public float GetLayerScale(TimeLayerType layer)
    {
        return layerScales[(int)layer];
    }

    public float GetChannelScale(TimeChannelType channel)
    {
        return channelScales[(int)channel];
    }


    // ====================================
    // Scale設定
    // ====================================

    public void SetGroupScale(TimeGroupType group, float scale)
    {
        groupScales[(int)group] = scale;
    }

    public void SetLayerScale(TimeLayerType layer, float scale)
    {
        layerScales[(int)layer] = scale;
    }

    public void SetChannelScale(TimeChannelType channel, float scale)
    {
        channelScales[(int)channel] = scale;
    }


    // ====================================
    // 個別ヒットストップ
    // ====================================

    public void HitStop(TimeAgent agent, float duration)
    {
        if (agent == null) return;

        // priority100は
        // 通常スローより優先される
        agent.AddModifier(
            0f,
            duration,
            TimeModifierMode.Override,
            100);
    }


    // ====================================
    // Groupヒットストップ
    // ====================================

    public void HitStopGroup(
        TimeGroupType group,
        float duration)
    {
        foreach (var agent in agents)
        {
            if (agent.group == group)
                HitStop(agent, duration);
        }
    }


    // ====================================
    // Layerヒットストップ
    // ====================================

    public void HitStopLayer(
        TimeLayerType layer,
        float duration)
    {
        foreach (var agent in agents)
        {
            if (agent.layer == layer)
                HitStop(agent, duration);
        }
    }


    // ====================================
    // 範囲ヒットストップ
    // ====================================

    public void HitStopInRadius(
    Vector3 center,
    float radius,
    float duration,
    bool includeCenter = true)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius);

        HashSet<TimeAgent> hitAgents = new HashSet<TimeAgent>();

        foreach (var col in cols)
        {
            TimeAgent agent =
                col.GetComponentInParent<TimeAgent>();

            if (agent == null)
                continue;

            if (!includeCenter &&
                (agent.transform.position - center).sqrMagnitude < 0.0001f)
                continue;

            if (hitAgents.Add(agent))
            {
                HitStop(agent, duration);

                Debug.Log($"HitStop Radius Hit : {agent.name}");
            }
        }
    }


    // ====================================
    // Groupスロー
    // ====================================

    public void SlowGroup(
        TimeGroupType group,
        float scale)
    {
        SetGroupScale(group, scale);
    }


    // ====================================
    // Layerスロー
    // ====================================

    public void SlowLayer(
        TimeLayerType layer,
        float scale)
    {
        SetLayerScale(layer, scale);
    }


    // ====================================
    // Channelスロー
    // ====================================

    public void SlowChannel(
        TimeChannelType channel,
        float scale)
    {
        SetChannelScale(channel, scale);
    }

    // ====================================
    // 滑らかスロー
    // ====================================

    public void BlendSlowGroup(
    TimeGroupType group,
    float start,
    float target,
    float duration,
    AnimationCurve curve)
    {
        StartCoroutine(
            BlendSlowGroupCoroutine(
                group,
                start,
                target,
                duration,
                curve));
    }

    IEnumerator BlendSlowGroupCoroutine(
    TimeGroupType group,
    float start,
    float target,
    float duration,
    AnimationCurve curve)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = time / duration;

            float curveValue = curve.Evaluate(t);

            float scale =
                Mathf.Lerp(start, target, curveValue);

            SetGroupScale(group, scale);

            yield return null;
        }

        SetGroupScale(group, target);
    }

    // ====================================
    // 個別スロー duration付き
    // ====================================

    public TimeHandle SlowAgentForSeconds(
        TimeAgent agent,
        float scale,
        float duration,
        TimeModifierMode mode = TimeModifierMode.Min,
        TimePriority priority = TimePriority.GameplaySlow)
    {
        if (agent == null) return null;

        scale = Mathf.Max(0f, scale);
        duration = Mathf.Max(0f, duration);

        return agent.AddModifier(
            scale,
            duration,
            mode,
            (int)priority);
    }


    // ====================================
    // Groupスロー duration付き
    // ====================================

    public List<TimeHandle> SlowGroupForSeconds(
        TimeGroupType group,
        float scale,
        float duration,
        TimeModifierMode mode = TimeModifierMode.Min,
        TimePriority priority = TimePriority.GameplaySlow)
    {
        List<TimeHandle> handles = new List<TimeHandle>();

        scale = Mathf.Max(0f, scale);
        duration = Mathf.Max(0f, duration);

        foreach (var agent in agents)
        {
            if (agent == null) continue;

            if (agent.group == group)
            {
                TimeHandle handle =
                    agent.AddModifier(
                        scale,
                        duration,
                        mode,
                        (int)priority);

                handles.Add(handle);
            }
        }

        return handles;
    }


    // ====================================
    // Layerスロー duration付き
    // ====================================

    public List<TimeHandle> SlowLayerForSeconds(
        TimeLayerType layer,
        float scale,
        float duration,
        TimeModifierMode mode = TimeModifierMode.Min,
        TimePriority priority = TimePriority.GameplaySlow)
    {
        List<TimeHandle> handles = new List<TimeHandle>();

        scale = Mathf.Max(0f, scale);
        duration = Mathf.Max(0f, duration);

        foreach (var agent in agents)
        {
            if (agent == null) continue;

            if (agent.layer == layer)
            {
                TimeHandle handle =
                    agent.AddModifier(
                        scale,
                        duration,
                        mode,
                        (int)priority);

                handles.Add(handle);
            }
        }

        return handles;
    }


    // ====================================
    // Channelスロー duration付き
    // ====================================

    public List<TimeHandle> SlowChannelForSeconds(
        TimeChannelType channel,
        float scale,
        float duration,
        TimeModifierMode mode = TimeModifierMode.Min,
        TimePriority priority = TimePriority.GameplaySlow)
    {
        List<TimeHandle> handles = new List<TimeHandle>();

        scale = Mathf.Max(0f, scale);
        duration = Mathf.Max(0f, duration);

        foreach (var agent in agents)
        {
            if (agent == null) continue;

            if (agent.channel == channel)
            {
                TimeHandle handle =
                    agent.AddModifier(
                        scale,
                        duration,
                        mode,
                        (int)priority);

                handles.Add(handle);
            }
        }

        return handles;
    }


    // ====================================
    // 範囲スロー duration付き
    // ====================================

    public List<TimeHandle> SlowInRadiusForSeconds(
        Vector3 center,
        float radius,
        float scale,
        float duration,
        bool includeCenter = true,
        TimeModifierMode mode = TimeModifierMode.Min,
        TimePriority priority = TimePriority.GameplaySlow)
    {
        List<TimeHandle> handles = new List<TimeHandle>();

        scale = Mathf.Max(0f, scale);
        duration = Mathf.Max(0f, duration);

        Collider[] cols = Physics.OverlapSphere(center, radius);

        HashSet<TimeAgent> hitAgents =
            new HashSet<TimeAgent>();

        foreach (var col in cols)
        {
            TimeAgent agent =
                col.GetComponentInParent<TimeAgent>();

            if (agent == null)
                continue;

            if (!includeCenter &&
                (agent.transform.position - center).sqrMagnitude < 0.0001f)
                continue;

            if (hitAgents.Add(agent))
            {
                TimeHandle handle =
                    agent.AddModifier(
                        scale,
                        duration,
                        mode,
                        (int)priority);

                handles.Add(handle);

                Debug.Log($"Slow Radius Hit : {agent.name}");
            }
        }

        return handles;
    }
}