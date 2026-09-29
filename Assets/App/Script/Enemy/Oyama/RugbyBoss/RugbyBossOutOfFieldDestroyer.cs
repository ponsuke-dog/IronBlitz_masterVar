using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RugbyBossOutOfFieldDestroyer : MonoBehaviour
{
    [Header("対象")]
    [SerializeField]
    [Tooltip("ONならRugbySummonEnemyだけを破棄対象にします。基本ONでOKです。")]
    private bool destroyRugbySummonEnemy = true;

    [SerializeField]
    [Tooltip("ONなら吹き飛び中の召喚エネミーだけ破棄します。OFFなら状態に関係なく範囲外に入った召喚エネミーを破棄します。")]
    private bool destroyOnlyFlyingEnemy = false;

    [Header("判定")]
    [SerializeField]
    [Tooltip("OnTriggerEnterだけでなくOnTriggerStayでも破棄判定します。高速移動や初期重なり対策としてON推奨です。")]
    private bool useTriggerStay = true;

    [SerializeField]
    [Tooltip("同じRugbySummonEnemyに対して複数回ForceDestroyを呼ばないようにします。")]
    private bool preventDuplicateDestroy = true;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("ONなら破棄ログを出します。")]
    private bool logDestroy = true;

    [SerializeField]
    [Tooltip("ONなら対象外Colliderが入った時もログを出します。確認用です。")]
    private bool logIgnoredCollider = false;

    private readonly HashSet<RugbySummonEnemy> destroyedEnemies = new HashSet<RugbySummonEnemy>();

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{name} RugbyBossOutOfFieldDestroyer: ColliderのIsTriggerがOFFです。ONに変更してください。",
                this
            );
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = true;
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();

        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        TryDestroyOutOfFieldTarget(other, "OnTriggerEnter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!useTriggerStay)
            return;

        TryDestroyOutOfFieldTarget(other, "OnTriggerStay");
    }

    private void TryDestroyOutOfFieldTarget(Collider other, string reason)
    {
        if (other == null)
            return;

        if (destroyRugbySummonEnemy)
        {
            RugbySummonEnemy summonEnemy = FindRugbySummonEnemy(other);

            if (summonEnemy != null)
            {
                TryDestroySummonEnemy(summonEnemy, other, reason);
                return;
            }
        }

        if (logIgnoredCollider)
        {
            Debug.Log(
                $"{name} OutOfField ignored collider:{other.name} reason:{reason}",
                this
            );
        }
    }

    private RugbySummonEnemy FindRugbySummonEnemy(Collider other)
    {
        if (other == null)
            return null;

        RugbySummonEnemy summonEnemy = other.GetComponent<RugbySummonEnemy>();

        if (summonEnemy != null)
            return summonEnemy;

        summonEnemy = other.GetComponentInParent<RugbySummonEnemy>();

        if (summonEnemy != null)
            return summonEnemy;

        return other.GetComponentInChildren<RugbySummonEnemy>();
    }

    private void TryDestroySummonEnemy(RugbySummonEnemy summonEnemy, Collider hitCollider, string reason)
    {
        if (summonEnemy == null)
            return;

        if (!summonEnemy.IsAlive)
            return;

        if (destroyOnlyFlyingEnemy && !summonEnemy.IsFlying)
        {
            if (logIgnoredCollider)
            {
                Debug.Log(
                    $"{name} OutOfField skip summonEnemy:{summonEnemy.name} " +
                    $"state:{summonEnemy.CurrentStateName} reason:not flying collider:{hitCollider.name}",
                    this
                );
            }

            return;
        }

        if (preventDuplicateDestroy)
        {
            if (destroyedEnemies.Contains(summonEnemy))
                return;

            destroyedEnemies.Add(summonEnemy);
        }

        if (logDestroy)
        {
            Debug.Log(
                $"{name} OutOfField destroy RugbySummonEnemy:{summonEnemy.name} " +
                $"state:{summonEnemy.CurrentStateName} collider:{hitCollider.name} reason:{reason}",
                this
            );
        }

        summonEnemy.ForceDestroy();
    }

    public void ClearDestroyedCache()
    {
        destroyedEnemies.Clear();
    }
}