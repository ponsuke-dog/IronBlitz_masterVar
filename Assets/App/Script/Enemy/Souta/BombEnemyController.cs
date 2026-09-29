using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(EnemyNavMotor))]
[RequireComponent(typeof(EnemyVisionSensor))]
public class BombEnemyController : MonoBehaviour, IHitReceiver, IHitSource
{
    [Header("Status")]
    [SerializeField] private float hp = 10f;

    [Header("Move")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;

    [Header("Explosion")]
    [SerializeField] private float explodeRange = 2f;
    [SerializeField] private float explodeRadius = 5f;
    [SerializeField] private float explodePower = 10f;
    [SerializeField] private float explodeDelay = 2f;
    [SerializeField] private float explodeDamage = 5f;
    [SerializeField] private float selfDestructTime = 5f;
    [SerializeField] private float explosionHitDuration = 0.15f;
    [SerializeField] private float explosionAirborneTime = 5.0f;
    [SerializeField] private float explosionWarningTime = 1.0f;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    [Header("Explosion Collision")]
    [SerializeField] private GameObject explosionToEnemy;
    [SerializeField] private GameObject explosionToPlayer;

    [Header("Model")]
    [SerializeField] public GameObject enemyModel;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    private static readonly int IsRunningHash =
    Animator.StringToHash("IsRunning");

    [Header("Effect")]
    [SerializeField] private EffectPlayer effectPlayer;

    private static readonly int AirborneHash = Animator.StringToHash("Airborne");
    private static readonly int ExplosionHash = Animator.StringToHash("Explosion");

    private EnemyNavMotor motor;
    private EnemyVisionSensor sensor;

    private BombEnemyState currentState;

    private float explodeTimer;
    private bool explosionTriggered;

    #region State Machine
    //========================================================
    // State Base
    //========================================================

    private abstract class BombEnemyState
    {
        protected BombEnemyController owner;

        protected EnemyNavMotor Motor => owner.motor;

        protected Transform Player => owner.playerTarget;

        public void Set(BombEnemyController owner)
        {
            this.owner = owner;
        }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public virtual void Update(float dt) { }
    }

    //========================================================
    // Patrol
    //========================================================

    private class StatePatrol : BombEnemyState
    {
        public override void Enter()
        {
            owner.PlayIdleAnimation();
        }

        public override void Update(float dt)
        {
            if (owner.CanSeePlayer())
            {
                owner.ChangeState(
                    new StateChase());
            }
        }
    }

    //========================================================
    // Chase
    //========================================================

    private class StateChase : BombEnemyState
    {
        private float chaseTimer;
        private bool warningStarted;

        public override void Enter()
        {
            chaseTimer = 0f;
            warningStarted = false;

            owner.PlayRunAnimation();
        }

        public override void Update(float dt)
        {
            if (Player == null)
                return;

            Motor.RequestMove(
                Player.position,
                owner.chaseSpeed
            );

            chaseTimer += dt;

            float remainingTime =
                owner.selfDestructTime - chaseTimer;

            // 爆発前の予告アニメーション開始
            if (!warningStarted &&
                remainingTime <= owner.explosionWarningTime)
            {
                warningStarted = true;
                owner.PlayExplosionAnimation();
            }

            // Animation Eventで爆発済みなら終了
            if (owner.explosionTriggered)
            {
                owner.ChangeState(new StateExplosionHit());
                return;
            }

            // Eventが来なかった場合の保険
            if (chaseTimer >= owner.selfDestructTime)
            {
                owner.AE_Explosion();
                owner.ChangeState(new StateExplosionHit());
                return;
            }

            float distance = Vector3.Distance(
                owner.transform.position,
                Player.position
            );

            // 距離による起爆
            if (distance <= owner.explodeRange &&
                !warningStarted)
            {
                warningStarted = true;
                owner.PlayExplosionAnimation();
            }
        }
    }

    //========================================================
    // Explode
    //========================================================

    private class StateExplode : BombEnemyState
    {
        private float hitTimer;
        private float failSafeTimer;
        private bool hitTimerStarted;

        public override void Enter()
        {
            owner.explosionTriggered = false;

            hitTimer = 0f;
            hitTimerStarted = false;

            // Animation Eventが呼ばれない場合の保険
            failSafeTimer = owner.explodeDelay + 1f;

            owner.PlayExplosionAnimation();
        }

        public override void Update(float dt)
        {
            failSafeTimer -= dt;

            if (!owner.explosionTriggered)
            {
                // 地上ならPlayerを追い続ける
                if (!Motor.IsFlying && Player != null)
                {
                    Motor.RequestMove(
                        Player.position,
                        owner.chaseSpeed
                    );
                }

                // Airborne中はEnemyNavMotorの吹き飛び速度で動き続ける
                // RequestMoveは呼ばなくてよい

                if (failSafeTimer <= 0f)
                {
                    owner.AE_Explosion();
                }

                return;
            }

            if (!hitTimerStarted)
            {
                hitTimerStarted = true;
                hitTimer = owner.explosionHitDuration;
            }

            hitTimer -= dt;

            if (hitTimer <= 0f)
            {
                owner.Disable();
            }
        }
    }

    //========================================================
    // Airborne
    //========================================================

    private class StateAirborne : BombEnemyState
    {
        private float countdownTimer;
        private bool warningStarted;

        public override void Enter()
        {
            countdownTimer = owner.explosionAirborneTime;
            warningStarted = false;

            owner.explosionTriggered = false;
            owner.PlayAirborneAnimation();
        }

        public override void Update(float dt)
        {
            countdownTimer -= dt;

            // 爆発前の予告アニメーションへ切り替える
            if (!warningStarted &&
                countdownTimer <= owner.explosionWarningTime)
            {
                warningStarted = true;
                owner.PlayExplosionAnimation();
            }

            // Animation Eventで発火済み
            if (owner.explosionTriggered)
            {
                owner.ChangeState(new StateExplosionHit());
                return;
            }

            // Eventが来ない場合の保険
            if (countdownTimer <= 0f)
            {
                owner.AE_Explosion();
                owner.ChangeState(new StateExplosionHit());
            }
        }
    }

    private class StateExplosionHit : BombEnemyState
    {
        private float hitTimer;

        public override void Enter()
        {
            hitTimer = owner.explosionHitDuration;
        }

        public override void Update(float dt)
        {
            hitTimer -= dt;

            if (hitTimer <= 0f)
            {
                owner.Disable();
            }
        }
    }

    private void PlayIdleAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(IsRunningHash, false);
    }

    private void PlayRunAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(IsRunningHash, true);
    }

    private void StopRunAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(IsRunningHash, false);
    }

    private void PlayAirborneAnimation()
    {
        if (animator == null)
            return;

        animator.SetBool(IsRunningHash, false);

        animator.SetTrigger(AirborneHash);
    }

    private void PlayExplosionAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(AirborneHash);
        animator.SetTrigger(ExplosionHash);
    }

   
    #endregion

    private void Awake()
    {
        motor = GetComponent<EnemyNavMotor>();
        sensor = GetComponent<EnemyVisionSensor>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (effectPlayer == null)
        {
            effectPlayer = GetComponentInChildren<EffectPlayer>();
        }

        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");

            if (p != null)
                playerTarget = p.transform;
        }

        SetupExplosionObject(explosionToEnemy);
        SetupExplosionObject(explosionToPlayer);

        //motor.OnFirstGroundedAfterLaunch += HandleGrounded;
    }

    private void Start()
    {
        ChangeState(new StatePatrol());
    }

    private void Update()
    {
        float dt = Time.deltaTime * motor.TimeScale;

        motor.TickBegin(dt);

        currentState?.Update(dt);

        motor.TickEnd(dt);
    }

    private void BeginExplode()
    {
        ChangeState(new StateExplode());
    }

    private void Disable()
    {
      this.gameObject.SetActive(false);
    }

    private bool CanSeePlayer()
    {
        if (sensor == null || playerTarget == null)
            return false;

        return sensor.CanSeeTarget(transform, playerTarget);
    }

    private void EnableExplosionCollisions()
    {
        if (explosionToEnemy != null)
            explosionToEnemy.SetActive(true);

        if (explosionToPlayer != null)
            explosionToPlayer.SetActive(true);
    }

    private void SetupExplosionObject(GameObject explosionObject)
    {
        if (explosionObject == null)
            return;

        SphereCollider sphere =
            explosionObject.GetComponent<SphereCollider>();

        if (sphere != null)
            sphere.radius = explodeRadius;

        explosionObject.SetActive(false);
    }

    public void AE_Explosion()
    {
        if (explosionTriggered)
            return;

        explosionTriggered = true;

        // 地上なら止まる
        // 空中ではStopPlanarVelocityが無視されるため飛び続ける
        motor.StopPlanarVelocity();

        if (enemyModel != null)
            enemyModel.SetActive(false);

        if (effectPlayer != null)
            effectPlayer.Play(4);

        AudioManager.Instance.PlaySe("BE_Explosion", gameObject.transform.position);

        EnableExplosionCollisions();
    }

    private void ChangeState(BombEnemyState next)
    {
        currentState?.Exit();

        currentState = next;
        currentState.Set(this);
        currentState.Enter();

    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (selfHitbox == null || other == null)
            return;

        Hitbox targetHitbox = FindTargetHitbox(other);

        if (targetHitbox == null ||
            targetHitbox.receiver == null)
        {
            return;
        }

        if (selfHitbox.gameObject == explosionToPlayer)
        {
            SendPlayerExplosionHit(
                selfHitbox,
                targetHitbox,
                other
            );

            return;
        }

        if (selfHitbox.gameObject == explosionToEnemy)
        {
            SendEnemyExplosionHit(
                selfHitbox,
                targetHitbox,
                other
            );

            return;
        }
    }

    public void OnHit(HitEventData data)
    {
        Debug.Log("BombEnemy OnHit: " + data.attackerObject.name);
        if (data.payload is BlowPayload blow)
        {
            TackleType tackleType = blow.tackleType;

            motor.ApplyBlow(
                blow.powerDirection,
                blow.powerConstant * 2f,
                blow.powerRate,
                tackleType);

            ChangeState(new StateAirborne());
        }
    }

    //Hitboxを探す処理
    private Hitbox FindTargetHitbox(Collider other)
    {
        if (other == null)
            return null;

        Hitbox targetHitbox =
            other.GetComponent<Hitbox>();

        if (targetHitbox == null)
        {
            targetHitbox =
                other.GetComponentInParent<Hitbox>();
        }

        return targetHitbox;
    }

    private GameObject GetReceiverObject(
    Hitbox targetHitbox)
    {
        if (targetHitbox.receiver
            is MonoBehaviour receiverBehaviour)
        {
            return receiverBehaviour.gameObject;
        }

        return targetHitbox.gameObject;
    }

    //Playerに対する爆発処理
    private void SendPlayerExplosionHit(
    Hitbox selfHitbox,
    Hitbox targetHitbox,
    Collider other)
    {
        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,

            targetObject = GetReceiverObject(targetHitbox),
            targetHitbox = targetHitbox.gameObject,

            contactPoint =
                other.ClosestPoint(transform.position),

            payload = new EnemyAttackPayload
            {
                damage = Mathf.RoundToInt(explodeDamage)
            }
        };

        targetHitbox.receiver.OnHit(data);
    }

    //Enemyに対する爆発処理
    private void SendEnemyExplosionHit(
    Hitbox selfHitbox,
    Hitbox targetHitbox,
    Collider other)
    {
        Vector3 direction =
            targetHitbox.transform.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        direction.Normalize();

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,

            targetObject = GetReceiverObject(targetHitbox),
            targetHitbox = targetHitbox.gameObject,

            contactPoint =
                other.ClosestPoint(transform.position),

            payload = new BlowPayload
            {
                tackleType = TackleType.Normal,
                damageConstant = explodeDamage,
                powerConstant = explodePower,
                powerRate = 1f,
                powerDirection = direction
            }
        };

        targetHitbox.receiver.OnHit(data);
    }

    private void OnDrawGizmos()
    {
        // 爆発範囲
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            explodeRadius);

        // 起爆開始距離
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            explodeRange);
    }

}