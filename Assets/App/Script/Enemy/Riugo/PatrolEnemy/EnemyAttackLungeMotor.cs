using System;
using UnityEngine;

public class EnemyAttackLungeMotor : MonoBehaviour
{
    [System.Serializable]
    public class LungeParam
    {
        [Header("Lunge Fixed Direction")]
        [Tooltip("Lunge‚ÌˆÚ“®‘¬“x")]
        public float baseLungeSpeed = 6.0f;

        [Tooltip("Lunge‚Åi‚ÞÅ‘å‹——£")]
        public float lungeDistance = 4.0f;

        [Tooltip("‚±‚Ì‹——£ˆÈ‰º‚ÌŽc‚è‹——£‚É‚È‚Á‚½‚ç“ž’Bˆµ‚¢")]
        public float arriveDistance = 0.05f;

        [Header("Block Detect")]
        [Tooltip("ˆÚ“®—\’è—Ê‚É‘Î‚µ‚ÄŽÀÛ‚Éi‚ß‚½Š„‡‚ª‚±‚êˆÈ‰º‚È‚ç‹l‚Ü‚Á‚½ˆµ‚¢")]
        [Range(0f, 1f)]
        public float blockedMoveRatio = 0.25f;

        [Tooltip("‰½•bˆÈã‹l‚Ü‚è‘±‚¯‚½‚çLungeI—¹ˆµ‚¢‚É‚·‚é‚©")]
        public float blockedDuration = 0.08f;

        [Header("Rotation")]
        public bool snapToLungeDirectionOnBegin = true;
        public bool rotateDuringLunge = true;
        public float lungeRotateSpeed = 28f;

        [Header("Debug")]
        public bool logLunge = false;
    }

    [SerializeField] private LungeParam lungeParam = new LungeParam();
    [SerializeField] private EnemyNavMotor motor;

    private bool isLunging;
    private bool isLungeFinished = true;

    private Vector3 lungeDirection;
    private float movedDistance;
    private float blockedTimer;

    public bool IsLunging => isLunging;
    public bool IsLungeFinished => isLungeFinished;

    public event Action OnLungeFinished;

    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<EnemyNavMotor>();
    }

    public void BeginLungeFixedDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        lungeDirection = direction.normalized;

        movedDistance = 0f;
        blockedTimer = 0f;

        isLunging = true;
        isLungeFinished = false;

        if (lungeParam.snapToLungeDirectionOnBegin)
            transform.rotation = Quaternion.LookRotation(lungeDirection);

        if (lungeParam.logLunge)
        {
            Debug.Log(
                $"{name} BeginLungeFixedDirection " +
                $"dir:{lungeDirection} distance:{lungeParam.lungeDistance:F2} speed:{lungeParam.baseLungeSpeed:F2}"
            );
        }
    }

    public void Tick(float dt)
    {
        if (!isLunging || motor == null)
            return;

        float remainDistance = lungeParam.lungeDistance - movedDistance;

        if (remainDistance <= lungeParam.arriveDistance)
        {
            FinishLunge("ReachedDistance");
            return;
        }

        if (lungeParam.rotateDuringLunge && lungeDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lungeDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                lungeParam.lungeRotateSpeed * dt
            );
        }

        float stepDistance = Mathf.Min(
            lungeParam.baseLungeSpeed * dt * motor.TimeScale,
            remainDistance
        );

        Vector3 displacement = lungeDirection * stepDistance;

        Vector3 before = transform.position;

        motor.AddExternalPlanarDisplacement(displacement);

        Vector3 after = transform.position;

        Vector3 actualMove = after - before;
        actualMove.y = 0f;

        float actualForwardMove = Vector3.Dot(actualMove, lungeDirection);
        actualForwardMove = Mathf.Max(0f, actualForwardMove);

        movedDistance += actualForwardMove;

        bool blocked =
            stepDistance > 0.001f &&
            actualForwardMove <= stepDistance * lungeParam.blockedMoveRatio;

        if (blocked)
        {
            blockedTimer += dt;

            if (blockedTimer >= lungeParam.blockedDuration)
            {
                FinishLunge("Blocked");
                return;
            }
        }
        else
        {
            blockedTimer = 0f;
        }

        if (movedDistance >= lungeParam.lungeDistance - lungeParam.arriveDistance)
        {
            FinishLunge("ReachedDistance");
        }
    }

    public void ForceStop()
    {
        if (!isLunging && isLungeFinished)
            return;

        isLunging = false;
        isLungeFinished = true;
        blockedTimer = 0f;
    }

    private void FinishLunge(string reason)
    {
        if (!isLunging)
            return;

        isLunging = false;
        isLungeFinished = true;
        blockedTimer = 0f;

        if (lungeParam.logLunge)
        {
            Debug.Log(
                $"{name} FinishLunge reason:{reason} moved:{movedDistance:F2}/{lungeParam.lungeDistance:F2}"
            );
        }

        OnLungeFinished?.Invoke();
    }
}