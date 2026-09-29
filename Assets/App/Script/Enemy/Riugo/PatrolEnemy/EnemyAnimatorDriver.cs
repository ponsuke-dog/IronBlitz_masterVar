using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimatorDriver : MonoBehaviour
{
    [System.Serializable]
    public class ParamNames
    {
        public string moveSpeed = "MoveSpeed";
        public string isAttacking = "IsAttacking";
        public string isLaunched = "IsLaunched";
        public string isKnockedDown = "IsKnockedDown";
        public string isGettingUp = "IsGettingUp";

        public string isFound = "IsFound";
        public string isCharging = "IsCharging";
    }

    [System.Serializable]
    public class StateNames
    {
        public string idle = "KB_Idle";
        public string walk = "KB_Walk_02";

        public string found = "KB_FoundTarget_02";
        public string charge = "KB_Charge_02";
        public string attack = "KB_Attack_02";

        public string attackCooldown = "KB_Follow";

        public string launch = "KB_Launch_03";
        public string layDown = "KB_LayDown";
        public string getUp = "KB_GetUp";
    }

    [System.Serializable]
    public class AnimParam
    {
        [Header("Move")]
        public float moveSpeedDamp = 0.1f;
        public float walkThreshold = 0.1f;

        [Header("Cross Fade")]
        public float locomotionBlendTime = 0.05f;
        public float foundBlendTime = 0.05f;
        public float chargeBlendTime = 0.05f;
        public float attackBlendTime = 0.05f;
        public float attackCooldownBlendTime = 0.05f;
        public float launchBlendTime = 0.03f;
        public float layDownBlendTime = 0.08f;
        public float getUpBlendTime = 0.08f;
    }

    [SerializeField] private Animator animator;
    [SerializeField] private ParamNames paramNames = new ParamNames();
    [SerializeField] private StateNames stateNames = new StateNames();
    [SerializeField] private AnimParam animParam = new AnimParam();

    private EnemyNavMotor motor;

    private int moveSpeedHash;
    private int isAttackingHash;
    private int isLaunchedHash;
    private int isKnockedDownHash;
    private int isGettingUpHash;
    private int isFoundHash;
    private int isChargingHash;

    private bool resetAttackNextTick;
    private bool resetFoundNextTick;
    private bool resetChargeNextTick;

    public string FoundStateName => stateNames.found;
    public string ChargeStateName => stateNames.charge;
    public string AttackStateName => stateNames.attack;
    public string AttackCooldownStateName => stateNames.attackCooldown;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        moveSpeedHash = Animator.StringToHash(paramNames.moveSpeed);
        isAttackingHash = Animator.StringToHash(paramNames.isAttacking);
        isLaunchedHash = Animator.StringToHash(paramNames.isLaunched);
        isKnockedDownHash = Animator.StringToHash(paramNames.isKnockedDown);
        isGettingUpHash = Animator.StringToHash(paramNames.isGettingUp);
        isFoundHash = Animator.StringToHash(paramNames.isFound);
        isChargingHash = Animator.StringToHash(paramNames.isCharging);
    }

    public void Initialize(EnemyNavMotor navMotor)
    {
        motor = navMotor;
    }

    public void Tick(bool allowMoveAnimation)
    {
        if (animator == null || motor == null)
            return;

        float speed = allowMoveAnimation ? motor.CurrentPlanarSpeed : 0f;
        animator.SetFloat(moveSpeedHash, speed, animParam.moveSpeedDamp, Time.deltaTime);

        if (resetAttackNextTick)
        {
            animator.SetBool(isAttackingHash, false);
            resetAttackNextTick = false;
        }

        if (resetFoundNextTick)
        {
            animator.SetBool(isFoundHash, false);
            resetFoundNextTick = false;
        }

        if (resetChargeNextTick)
        {
            animator.SetBool(isChargingHash, false);
            resetChargeNextTick = false;
        }
    }

    public void PlayAttackCooldown()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        ResetBattleAndKnockbackBools();

        animator.CrossFadeInFixedTime(
            stateNames.attackCooldown,
            animParam.attackCooldownBlendTime,
            0,
            0f
        );
    }
    public void PlayFound()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        ResetBattleAndKnockbackBools();

        animator.SetBool(isFoundHash, true);
        animator.CrossFadeInFixedTime(stateNames.found, animParam.foundBlendTime, 0, 0f);

        resetFoundNextTick = true;
    }

    public void PlayCharge()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        ResetBattleAndKnockbackBools();

        animator.SetBool(isChargingHash, true);
        animator.CrossFadeInFixedTime(stateNames.charge, animParam.chargeBlendTime ,0 ,0f);

        resetChargeNextTick = true;
    }

    public void PlayAttack()
    {
        if (animator == null)
            return;

        if (IsKnockbackAnimationPlaying())
            return;

        ResetBattleAndKnockbackBools();

        animator.SetBool(isAttackingHash, true);
        animator.CrossFadeInFixedTime(stateNames.attack, animParam.attackBlendTime, 0, 0f);

        resetAttackNextTick = true;
    }

    public void PlayLaunch()
    {
        if (animator == null)
            return;

        animator.SetBool(isFoundHash, false);
        animator.SetBool(isChargingHash, false);
        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isKnockedDownHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isLaunchedHash, true);

        animator.CrossFadeInFixedTime(stateNames.launch, animParam.launchBlendTime, 0, 0f);
    }

    public void EnterLayDown()
    {
        if (animator == null)
            return;

        animator.SetBool(isFoundHash, false);
        animator.SetBool(isChargingHash, false);
        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isGettingUpHash, false);
        animator.SetBool(isKnockedDownHash, true);

        animator.CrossFadeInFixedTime(stateNames.layDown, animParam.layDownBlendTime);
    }

    public void BeginGetUp()
    {
        if (animator == null)
            return;

        animator.SetBool(isFoundHash, false);
        animator.SetBool(isChargingHash, false);
        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedDownHash, false);
        animator.SetBool(isGettingUpHash, true);

        animator.CrossFadeInFixedTime(stateNames.getUp, animParam.getUpBlendTime);
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
    public bool IsGetUpFinished()
    {
        if (animator == null)
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (info.IsName(stateNames.getUp) && info.normalizedTime >= 0.98f)
            return true;

        if (info.IsName(stateNames.idle) || info.IsName(stateNames.walk))
            return true;

        return false;
    }

    public void ForceIdle()
    {
        if (animator == null)
            return;

        animator.SetBool(isFoundHash, false);
        animator.SetBool(isChargingHash, false);
        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedDownHash, false);
        animator.SetBool(isGettingUpHash, false);

        animator.Play(stateNames.idle, 0, 0f);
    }

    public void ReturnToLocomotion()
    {
        if (animator == null)
            return;

        ResetBattleAndKnockbackBools();

        animator.CrossFadeInFixedTime(stateNames.idle, animParam.locomotionBlendTime, 0, 0f);
    }
    public bool IsKnockbackAnimationPlaying()
    {
        if (animator == null)
            return false;

        return animator.GetBool(isLaunchedHash) ||
               animator.GetBool(isKnockedDownHash) ||
               animator.GetBool(isGettingUpHash);
    }

    private void ResetBattleAndKnockbackBools()
    {
        animator.SetBool(isFoundHash, false);
        animator.SetBool(isChargingHash, false);
        animator.SetBool(isAttackingHash, false);
        animator.SetBool(isLaunchedHash, false);
        animator.SetBool(isKnockedDownHash, false);
        animator.SetBool(isGettingUpHash, false);
    }
}