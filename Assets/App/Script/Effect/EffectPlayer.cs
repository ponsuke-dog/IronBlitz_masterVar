using System.Collections.Generic;
using UnityEngine;

#region エフェクト発生器
/// <summary>
/// プレイヤーなどに付ける
/// エフェクトを外部から呼び出すための窓口
/// </summary>
public class EffectPlayer : MonoBehaviour
{
    [Header("使用エフェクト一覧")]
    [SerializeField]
    private List<EffectData> effects = new();

    #region 再生

    /// <summary>
    /// 自分のTransform基準で再生。
    /// </summary>
    public EffectInstance Play(int index)
    {
        return Play(index, transform, EffectPlayParam.Default);
    }

    /// <summary>
    /// 自分のTransform基準で再生。
    /// </summary>
    public EffectInstance Play(int index, EffectPlayParam param)
    {
        return Play(index, transform, param);
    }

    /// <summary>
    /// 任意Transform基準で再生。
    /// data.followTarget が true なら追従する。
    /// </summary>
    public EffectInstance Play(int index, Transform target)
    {
        return Play(index, target, EffectPlayParam.Default);
    }

    /// <summary>
    /// 任意Transform基準で再生。
    /// EffectPlayParam.overrideFollowTarget で追従ON/OFFを再生ごとに上書き可能。
    /// </summary>
    public EffectInstance Play(int index, Transform target, EffectPlayParam param)
    {
        EffectData data = GetEffectData(index);
        if (data == null)
            return null;

        if (target == null)
        {
            Debug.LogWarning("Effect target is null");
            return null;
        }

        EffectInstance inst = EffectManager.Instance.Spawn(data);
        inst.Play(target, param);

        return inst;
    }

    /// <summary>
    /// 任意World座標で再生。
    /// 回転はIdentity、スケールはOne。
    /// </summary>
    public EffectInstance PlayAt(int index, Vector3 position)
    {
        return PlayAt(
            index,
            position,
            Quaternion.identity,
            Vector3.one,
            EffectPlayParam.Default
        );
    }

    /// <summary>
    /// 任意World座標・回転で再生。
    /// スケールはOne。
    /// </summary>
    public EffectInstance PlayAt(int index, Vector3 position, Quaternion rotation)
    {
        return PlayAt(
            index,
            position,
            rotation,
            Vector3.one,
            EffectPlayParam.Default
        );
    }

    /// <summary>
    /// 任意World座標・回転・スケールで再生。
    /// </summary>
    public EffectInstance PlayAt(
        int index,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale)
    {
        return PlayAt(
            index,
            position,
            rotation,
            scale,
            EffectPlayParam.Default
        );
    }

    /// <summary>
    /// 任意World座標・回転・スケールで再生。
    /// 追従はしない。
    /// </summary>
    public EffectInstance PlayAt(
        int index,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        EffectPlayParam param)
    {
        EffectData data = GetEffectData(index);
        if (data == null)
            return null;

        EffectInstance inst = EffectManager.Instance.Spawn(data);
        inst.PlayAt(position, rotation, scale, param);

        return inst;
    }

    private EffectData GetEffectData(int index)
    {
        if (index < 0 || index >= effects.Count)
        {
            Debug.LogWarning("Effect index out of range");
            return null;
        }

        EffectData data = effects[index];

        if (data == null)
        {
            Debug.LogWarning("EffectData is null");
            return null;
        }

        if (data.prefab == null)
        {
            Debug.LogWarning("Effect prefab is null");
            return null;
        }

        if (EffectManager.Instance == null)
        {
            Debug.LogWarning("EffectManager.Instance is null");
            return null;
        }

        return data;
    }

    #endregion
}
#endregion