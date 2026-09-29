using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [System.Serializable]
    public class AttackParam
    {
        [Header("Damage")]
        public int damage = 1;

        [Header("Target Layer")]
        public LayerMask targetLayer;

        [Header("Loop Attack")]
        [Tooltip("攻撃開始時に右パンチ判定を常時ONにする")]
        public bool enableRightPunchOnStart = false;

        [Tooltip("攻撃開始時に左パンチ判定を常時ONにする")]
        public bool enableLeftPunchOnStart = false;

        [Header("Debug")]
        public bool logAttack = false;
    }

    [System.Serializable]
    public class PunchSet
    {
        public Hitbox rightPunchHitbox;
        public Hitbox leftPunchHitbox;
    }

    [SerializeField] private AttackParam attackParam = new AttackParam();
    [SerializeField] private PunchSet punchSet = new PunchSet();

    [Header("References")]
    [SerializeField] private EnemyAttackLungeMotor lungeMotor;

    private bool attackWindowOpen;
    private bool attackSequenceFinished;

    private readonly HashSet<Hitbox> activeHitboxes = new HashSet<Hitbox>();
    private readonly HashSet<Collider> hitTargetsThisAttack = new HashSet<Collider>();

    public bool IsAttackSequenceFinished => attackSequenceFinished;
    public bool IsAttackWindowOpen => attackWindowOpen;

    public event Action OnAttackHit;

    private void Awake()
    {
        if (lungeMotor == null)
            lungeMotor = GetComponent<EnemyAttackLungeMotor>();

        DisableAllPunchHitboxes();
    }

    public void StartLoopAttack(Vector3 lungeDirection)
    {
        attackWindowOpen = true;
        attackSequenceFinished = false;
        hitTargetsThisAttack.Clear();
        DisableAllPunchHitboxes();

        if (attackParam.enableRightPunchOnStart)
            SetPunchHitbox(punchSet.rightPunchHitbox, true);

        if (attackParam.enableLeftPunchOnStart)
            SetPunchHitbox(punchSet.leftPunchHitbox, true);

        if (lungeMotor != null)
            lungeMotor.BeginLungeFixedDirection(lungeDirection);

        if (attackParam.logAttack)
            Debug.Log($"{name} StartLoopAttack dir:{lungeDirection}");
    }

    public void ForceEndAttackSequence()
    {
        attackWindowOpen = false;
        attackSequenceFinished = true;
        hitTargetsThisAttack.Clear();
        DisableAllPunchHitboxes();

        if (lungeMotor != null)
            lungeMotor.ForceStop();

        if (attackParam.logAttack)
            Debug.Log($"{name} ForceEndAttackSequence");
    }

    public void AE_BeginAttack()
    {
        attackWindowOpen = true;
        attackSequenceFinished = false;
        hitTargetsThisAttack.Clear();

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_BeginAttack");
    }

    public void AE_EndAttack()
    {
        attackWindowOpen = false;
        attackSequenceFinished = true;
        DisableAllPunchHitboxes();

        if (lungeMotor != null)
            lungeMotor.ForceStop();

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_EndAttack");
    }

    public void AE_EnableRightPunch()
    {
        DisableAllPunchHitboxes();
        SetPunchHitbox(punchSet.rightPunchHitbox, true);

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_EnableRightPunch");
    }

    public void AE_DisableRightPunch()
    {
        SetPunchHitbox(punchSet.rightPunchHitbox, false);

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_DisableRightPunch");
    }

    public void AE_EnableLeftPunch()
    {
        DisableAllPunchHitboxes();
        SetPunchHitbox(punchSet.leftPunchHitbox, true);

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_EnableLeftPunch");
    }

    public void AE_DisableLeftPunch()
    {
        SetPunchHitbox(punchSet.leftPunchHitbox, false);

        if (attackParam.logAttack)
            Debug.Log($"{name} AE_DisableLeftPunch");
    }

    public void TryHit(Hitbox selfHitbox, Collider other)
    {
        if (!attackWindowOpen)
            return;

        if (selfHitbox == null || other == null)
            return;

        if (!activeHitboxes.Contains(selfHitbox))
            return;

        if (((1 << other.gameObject.layer) & attackParam.targetLayer.value) == 0)
            return;

        if (hitTargetsThisAttack.Contains(other))
            return;

        hitTargetsThisAttack.Add(other);

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        HitEventData data = new HitEventData
        {
            attackerHitbox = selfHitbox.gameObject,
            targetHitbox = targetHitbox.gameObject,
            payload = new EnemyAttackPayload
            {
                damage = attackParam.damage
            }
        };

        if (attackParam.logAttack)
        {
            Debug.Log(
                $"{name} Hit target:{other.name} " +
                $"with:{selfHitbox.name} damage:{attackParam.damage}"
            );
        }

        targetHitbox.receiver.OnHit(data);

        OnAttackHit?.Invoke();
    }

    private void SetPunchHitbox(Hitbox hitbox, bool value)
    {
        if (hitbox == null)
            return;

        Collider col = hitbox.GetComponent<Collider>();
        if (col != null)
            col.enabled = value;

        if (value)
            activeHitboxes.Add(hitbox);
        else
            activeHitboxes.Remove(hitbox);
    }

    public bool IsAttackHitbox(Hitbox hitbox)
    {
        if (hitbox == null)
            return false;

        return hitbox == punchSet.rightPunchHitbox ||
               hitbox == punchSet.leftPunchHitbox;
    }
    private void DisableAllPunchHitboxes()
    {
        SetPunchHitbox(punchSet.rightPunchHitbox, false);
        SetPunchHitbox(punchSet.leftPunchHitbox, false);
    }
}