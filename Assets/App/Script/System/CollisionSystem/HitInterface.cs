//ヒットイベント用インターフェース

using UnityEngine;


/// 当たり判定を発生させる側（攻撃側など）
public interface IHitSource
{
    /// TriggerDetectorから通知される
    /// <param name="selfHitbox">自分のHitbox</param>
    /// <param name="other">当たったCollider</param>
    void OnHitDetected(Hitbox selfHitbox, Collider other);
}

/// 当たり判定を受ける側（被弾側など）
public interface IHitReceiver
{
    /// ヒットイベント受信
    void OnHit(HitEventData data);
}

/// ヒットイベント基本データ
public struct HitEventData
{
    // ===== 攻撃側 =====
    public GameObject attackerObject;   // Playerなど
    public GameObject attackerHitbox;   // どの部位か

    // ===== 被弾側 =====
    public GameObject targetObject;     // Enemyなど
    public GameObject targetHitbox;     // どの部位か

    // ===== 拡張データ =====
    public object payload; // 任意の追加データ

    // ===== 接触点 =====
    public Vector3 contactPoint; // 接触点
}