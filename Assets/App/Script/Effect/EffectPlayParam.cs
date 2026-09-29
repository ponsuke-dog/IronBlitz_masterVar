using UnityEngine;

#region 再生パラメータ
/// <summary>
/// 再生時に渡すパラメータ
/// ・Dataの補正に対して追加 or 上書き
/// ・再生単位で追従設定を上書き可能
/// </summary>
public struct EffectPlayParam
{
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scale;

    public bool overridePosition;
    public bool overrideRotation;
    public bool overrideScale;

    [Header("追従上書き")]
    public bool overrideFollowTarget;
    public bool followTarget;

    public static EffectPlayParam Default => new EffectPlayParam
    {
        positionOffset = Vector3.zero,
        rotationOffset = Vector3.zero,
        scale = Vector3.one,

        overridePosition = false,
        overrideRotation = false,
        overrideScale = false,

        overrideFollowTarget = false,
        followTarget = true
    };
}
#endregion