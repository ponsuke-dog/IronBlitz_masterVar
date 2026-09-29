using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SoccerBossAnimatorDriver : MonoBehaviour
{
    [System.Serializable]
    public class ParamNames
    {
        public string isAngry = "IsAngry";
        public string actionSpeed = "ActionSpeed";
    }

    [System.Serializable]
    public class StateNames
    {
        public string idle = "Boss_Idle";
        public string kickArc = "Boss_Kick";
        public string slide = "Boss_Slide";
        public string overheadKick = "Boss_OverheadKick";
        public string stunned = "Boss_Stunned";
        public string recover = "Boss_Recover";
        public string dead = "Boss_Dead";
    }

    [System.Serializable]
    public class BlendParam
    {
        public float idleBlend = 0.05f;
        public float kickBlend = 0.05f;
        public float slideBlend = 0.05f;
        public float overheadBlend = 0.05f;
        public float stunnedBlend = 0.08f;
        public float recoverBlend = 0.08f;
        public float deadBlend = 0.1f;
    }

    [SerializeField] private Animator animator;
    [SerializeField] private ParamNames paramNames = new ParamNames();
    [SerializeField] private StateNames stateNames = new StateNames();
    [SerializeField] private BlendParam blendParam = new BlendParam();

    private int isAngryHash;
    private int actionSpeedHash;

    public string KickArcStateName => stateNames.kickArc;
    public string SlideStateName => stateNames.slide;
    public string OverheadKickStateName => stateNames.overheadKick;
    public string StunnedStateName => stateNames.stunned;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        isAngryHash = Animator.StringToHash(paramNames.isAngry);
        actionSpeedHash = Animator.StringToHash(paramNames.actionSpeed);
    }

    public void SetAngry(bool angry)
    {
        if (animator == null)
            return;

        animator.SetBool(isAngryHash, angry);
    }

    public void PlayIdle()
    {
        CrossFade(stateNames.idle, blendParam.idleBlend, 1f);
    }

    public void PlayKickArc(float speedRate)
    {
        CrossFade(stateNames.kickArc, blendParam.kickBlend, speedRate);
    }

    public void PlaySlide(float speedRate)
    {
        CrossFade(stateNames.slide, blendParam.slideBlend, speedRate);
    }

    public void PlayOverheadKick(float speedRate)
    {
        CrossFade(stateNames.overheadKick, blendParam.overheadBlend, speedRate);
    }

    public void PlayStunned()
    {
        CrossFade(stateNames.stunned, blendParam.stunnedBlend, 1f);
    }

    public void PlayRecover()
    {
        CrossFade(stateNames.recover, blendParam.recoverBlend, 1f);
    }

    public void PlayDead()
    {
        CrossFade(stateNames.dead, blendParam.deadBlend, 1f);
    }

    public bool IsStateFinished(string stateName, float endNormalizedTime = 0.95f)
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

    private void CrossFade(string stateName, float blendTime, float speedRate)
    {
        if (animator == null)
            return;

        animator.SetFloat(actionSpeedHash, speedRate);
        animator.speed = speedRate;

        animator.CrossFadeInFixedTime(
            stateName,
            blendTime,
            0,
            0f
        );
    }
}