using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 実際の衝突検知を行うクラス。
/// Hitbox と同じ GameObject に付ける。
/// </summary>
public class TriggerDetector : MonoBehaviour
{
    private Hitbox hitbox;

    [Header("Stay通知")]
    [Tooltip("ONにすると、すでに重なっている相手にも毎フレーム通知する。連鎖用HitboxではON推奨")]
    [SerializeField] private bool notifyStay = false;

    // 同一フレーム内の多重ヒット防止
    private readonly HashSet<Collider> frameHits = new HashSet<Collider>();

    private void Awake()
    {
        hitbox = GetComponent<Hitbox>();
    }

    private void LateUpdate()
    {
        frameHits.Clear();
    }

    private void OnDisable()
    {
        frameHits.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Notify(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!notifyStay) return;
        Notify(other);
    }

    private void Notify(Collider other)
    {
        if (hitbox == null) return;
        if (hitbox.source == null) return;
        if (other == null) return;

        // 同一フレームで同じ対象は無視
        if (frameHits.Contains(other)) return;
        frameHits.Add(other);

        hitbox.source.OnHitDetected(hitbox, other);
    }
}