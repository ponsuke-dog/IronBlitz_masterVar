using UnityEngine;

[RequireComponent(typeof(Animator))]
public class MissileEnemyAnimatorDriver : MonoBehaviour
{
    [System.Serializable]
    public class ParamNames
    {
        public string moveSpeed = "MoveSpeed";
        public string isAttacking = "IsAttacking";
        public string isLaunched = "IsLaunched";
        public string isKnocked = "IsKnocked";
        public string isGettingUp = "IsGettingUp";
    }

    [System.Serializable]
    public class StateNames
    {
        public string idle = "DB_Idle";
        public string fly = "DB_Fly_01";

        public string found = "DB_Found";

        // 既存のChargeアニメーション
        public string preCharge = "DB_Charge";

        // 新規のループChargeアニメーション
        public string chargeLoop = "DB_ChargeLoop";

        public string attack = "DB_Attack";

        // 攻撃終了モーション
        public string attackCooldown = "DB_AttackCooldown";

        public string launch = "DB_Launch";
        public string layDown = "DB_Laydown";
        public string getUp = "DB_GetUp";
    }

    [System.Serializable]
    public class AnimParam
    {
        [Header("Move")]
        public float moveSpeedDamp = 0.1f;
        public float flyThreshold = 0.05f;

        [Header("Cross Fade")]
        public float idleFlyBlendTime = 0.08f;
        public float foundBlendTime = 0.05f;
        public float preChargeBlendTime = 0.05f;
        public float chargeLoopBlendTime = 0.05f;
        public float attackBlendTime = 0.03f;
        public float attackCooldownBlendTime = 0.05f;
        public float launchBlendTime = 0.03f;
        public float layDownBlendTime = 0.08f;
        public float getUpBlendTime = 0.08f;
    }

    [SerializeField] private Animator animator;
    [SerializeField] private ParamNames paramNames = new ParamNames();
    [SerializeField] private StateNames stateNames = new StateNames();
    [SerializeField] private AnimParam animParam = new AnimParam();

    private MissileEnemyMotor motor;

    private int moveSpeedHash;
    private int isAttackingHash;
    private int isLaunchedHash;
    private int isKnockedHash;
    private int isGettingUpHash;

    private bool resetAttackNextTick;


    public string FoundStateName => stateNames.found;
    public string PreChargeStateName => stateNames.preCharge;
    public string ChargeLoopStateName => stateNames.chargeLoop;
    public string AttackCooldownStateName => stateNames.attackCooldown;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        moveSpeedHash = Animator.StringToHash(paramNames.moveSpeed);
        isAttackingHash = Animator.StringToHash(paramNames.isAttacking);
        isLaunchedHash = Animator.StringToHash(paramNames.isLaunched);
        isKnockedHash = Animator.StringToHash(paramNames.isKnocked);
        isGettingUpHash = Animator.StringToHash(paramNames.isGettingUp);
    }

    public void Initialize(MissileEnemyMotor missileMotor)
    {
        motor = missileMotor;
    }

    public void Tick(bool allowMoveAnimation)
    {
        if (animator == null || motor == null)
            return;

        float speed = allowMoveAnimation ? motor.CurrentSpeed : 0f;

        animator.SetFloat(
            moveSpeedHash,
            speed,
            animParam.moveSpeedDamp,
            Time.deltaTime
        );

        if (resetAttackNextTick)
        {
            animator.SetBool(isAttackingHash, false);
            resetAttackNextTick = false;
        }
    }

    public void EnterIdle()
    {
        if (animator == null)
            return;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.CrossFadeInFixedTime(stateNames.idle, animParam.idleFlyBlendTime);
    }

    public void EnterFly()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.CrossFadeInFixedTime(stateNames.fly, animParam.idleFlyBlendTime);
    }


    public void PlayFound()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.CrossFadeInFixedTime(
            stateNames.found,
            animParam.foundBlendTime,
            0,
            0f
        );
    }

    public void EnterPreCharge()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        resetAttackNextTick = false;

        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isAttackingHash, true);

        animator.CrossFadeInFixedTime(
            stateNames.preCharge,
            animParam.preChargeBlendTime,
            0,
            0f
        );
    }

    public void EnterChargeLoop()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        resetAttackNextTick = false;

        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isAttackingHash, true);

        animator.CrossFadeInFixedTime(
            stateNames.chargeLoop,
            animParam.chargeLoopBlendTime,
            0,
            0f
        );
    }

    public void PlayAttackCooldown()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.CrossFadeInFixedTime(
            stateNames.attackCooldown,
            animParam.attackCooldownBlendTime,
            0,
            0f
        );
    }

    //public void EnterCharge()
    //{
    //    if (animator == null)
    //        return;

    //    if (IsKnockbackAnimationPlaying())
    //        return;

    //    animator.SetBool(isLaunchedHash, false);
    //    animator.SetBool(isKnockedHash, false);
    //    animator.SetBool(isGettingUpHash, false);
    //    animator.SetBool(isAttackingHash, true);

    //    animator.CrossFadeInFixedTime(stateNames.charge, animParam.chargeBlendTime);
    //}

    public void PlayAttack()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isAttackingHash, true);

        animator.CrossFadeInFixedTime(stateNames.attack, animParam.attackBlendTime);

        // Attack本編後に通常状態へ戻したいケース用
        resetAttackNextTick = true;
    }

    public void PlayLoopAttack()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        // 重要：Attack中は勝手にIsAttackingをfalseに戻さない
        resetAttackNextTick = false;

        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isAttackingHash, true);

        animator.CrossFadeInFixedTime(
            stateNames.attack,
            animParam.attackBlendTime,
            0,
            0f
        );
    }
    public void HoldAttackPosture()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;

        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isAttackingHash, true);

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(stateNames.attack))
        {
            animator.CrossFadeInFixedTime(stateNames.attack, animParam.attackBlendTime);
        }
    }

    public void PlayLaunch()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isLaunchedHash, true);

        animator.CrossFadeInFixedTime(stateNames.launch, animParam.launchBlendTime);
    }

    public void EnterLayDown()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isKnockedHash, true);

        animator.CrossFadeInFixedTime(stateNames.layDown, animParam.layDownBlendTime);
    }

    public void BeginGetUp()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, true);

        animator.CrossFadeInFixedTime(stateNames.getUp, animParam.getUpBlendTime);
    }

    //public bool IsChargeFinished()
    //{
    //    if (animator == null)
    //        return true;

    //    AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
    //    return info.IsName(stateNames.charge) && info.normalizedTime >= 0.98f;
    //}

    public bool IsGetUpFinished()
    {
        if (animator == null)
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.IsName(stateNames.getUp) && info.normalizedTime >= 0.98f)
            return true;

        if (info.IsName(stateNames.idle) || info.IsName(stateNames.fly))
            return true;

        return false;
    }

    public bool IsStateFinished(string stateName, float endNormalizedTime = 0.98f)
    {
        if (animator == null)
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName(stateName))
        {
            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);

            if (!nextInfo.IsName(stateName))
                return false;

            info = nextInfo;
        }

        return info.normalizedTime >= endNormalizedTime;
    }
    public void ExitAttackLike()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;
        animator.SetBool(isAttackingHash, false);
    }

    public void ForceIdle()
    {
        if (animator == null)
            return;

        resetAttackNextTick = false;

        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.Play(stateNames.idle, 0, 0f);
    }

    public bool IsKnockbackAnimationPlaying()
    {
        if (animator == null)
            return false;

        return animator.GetBool(isLaunchedHash) ||
               animator.GetBool(isKnockedHash) ||
               animator.GetBool(isGettingUpHash);
    }
}