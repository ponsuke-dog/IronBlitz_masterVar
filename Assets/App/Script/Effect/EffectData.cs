using UnityEngine;

#region エフェクトデータ
/// <summary>
/// エフェクト設定データ（デザイナー用）
/// ・見た目補正
/// ・挙動設定
/// ・制御設定
/// </summary>
[CreateAssetMenu(menuName = "Effect/EffectData")]
public class EffectData : ScriptableObject
{
    [Header("Prefab")]
    [Tooltip("生成するエフェクトプレハブ")]
    public GameObject prefab;

    [Header("Transform補正（デザイナー用）")]
    [Tooltip("見た目の位置補正")]
    public Vector3 positionOffset;

    [Tooltip("見た目の回転補正")]
    public Vector3 rotationOffset;

    [Tooltip("見た目のスケール補正")]
    public Vector3 scale = Vector3.one;

    [Header("挙動")]
    [Tooltip("ターゲットに追従するか")]
    public bool followTarget = true;

    [Header("制御設定")]
    [Tooltip("自動で終了するか（Particleの終了に従う）")]
    public bool autoRelease = true;

    [Tooltip("強制終了時間（-1で無効）")]
    public float forceLifeTime = -1f;

    [Header("プール設定")]
    [Tooltip("再利用する数（必ず1以上）")]
    public int maxPoolSize = 20;


}
#endregion
