using UnityEngine;

[RequireComponent(typeof(MissileEnemyMotor))]
[RequireComponent(typeof(EnemyVisionSensor))]
public class EnemyControllerTypeMissile : MonoBehaviour, IHitSource, IHitReceiver
{
    [System.Serializable]
    private class EnemyParam
    {
        [Header("Group")]
        public EnemyData enemyData;

        [Header("Activation / Sphere Detect")]
        public float wakeRange = 14f;

        [Header("Chase")]
        public float chaseSpeed = 5f;
        public float chaseLoseDelay = 0.35f;
        public float chaseHeightOffset = 1.8f;

        [Header("Attack")]
        public float attackStartRange = 5f;
        public float chargeDuration = 0.45f;
        public float chargeRiseHeight = 0.7f;
        public float chargeRiseSpeed = 4.5f;
        public float attackCooldownDuration = 0.8f;

        [Header("Return")]
        public float returnSpeed = 4f;
        public float returnHeightOffset = 1.8f;
        public float homeArriveDistance = 0.4f;

        [Header("Attack After Impact")]
        public float postImpactPause = 0.18f;

        [Header("Knockdown")]
        public float layDownMinDuration = 0.2f;
        public float forcedGetUpDelay = 1.2f;
        public float getUpStartSpeedThreshold = 0.08f;

        [Header("Status")]
        public float maxHP = 5f;
        public int attackDamage = 1;

        [Header("Death")]
        [Tooltip("死亡演出後、Destroyするまでの秒数")]
        public float deathDestroyDelay = 1.2f;

        [Header("Hover Recover")]
        public float hoverRecoverTolerance = 0.15f;
        public float hoverRecoverTimeout = 1.2f;
    }
  
    // enemyが死んだ瞬間のみにキルカウントを増やさせるためのフラグ
    private bool KillCountFlg = false;
    
    private enum AnimationFinishMode
    {
        Timer,
        AnimationEnd,
        AnimationEvent
    }

    [System.Serializable]
    private class StateAnimationFinishParam
    {
        [Header("Finish")]
        public AnimationFinishMode finishMode = AnimationFinishMode.AnimationEnd;

        [Tooltip("Finish Mode が Timer の時、この秒数で終了扱いにする")]
        public float duration = 0.6f;

        [Tooltip("Finish Mode が AnimationEnd の時、この再生割合で終了扱いにする")]
        [Range(0f, 1.2f)]
        public float endNormalizedTime = 0.95f;
    }

    [System.Serializable]
    private class FoundParam
    {
        [Header("Found")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();
        public bool facePlayerWhileFound = true;
    }

    [System.Serializable]
    private class PreChargeParam
    {
        [Header("PreCharge")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();

        [Tooltip("PreCharge中にプレイヤー方向を向き続ける")]
        public bool facePlayerWhilePreCharge = true;
    }

    [System.Serializable]
    private class ChargeLoopParam
    {
        [Header("Charge Loop")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();

        [Tooltip("Charge中にプレイヤー方向を向き続ける")]
        public bool facePlayerWhileCharge = true;
    }

    [System.Serializable]
    private class AttackCooldownAnimParam
    {
        [Header("Attack Cooldown Animation")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();
    }
    [System.Serializable]
    private class ForcedGroundRecoveryParam
    {
        [Header("Forced Ground Recovery")]
        public bool enable = true;
        public float groundedRecoveryDelay = 0.08f;
        public float groundedPlanarSpeedThreshold = 8.0f;
        public float groundedVerticalVelocityThreshold = 0.2f;
    }

    [System.Serializable]
    private class VisualParam
    {
        [Header("Knockdown Visual Offset")]
        public float layDownSinkDepth = 0.18f;
        public float sinkSpeed = 6f;
        public float getUpRecoverSpeed = 5f;
    }

    [System.Serializable]
    private class ChainParam
    {
        [Header("連鎖判定")]
        [Tooltip("連鎖判定用 Hitbox のルート。子にHitbox/Colliderがある場合もOK")]
        public GameObject chainCollider;

        [Tooltip("この速度以上で連鎖を発火できる")]
        public float minChainSpeed = 2.5f;

        [Tooltip("同一ペアの再連鎖を防ぐ時間")]
        public float pairCooldown = 0.15f;

        [Header("連鎖回数")]
        [Tooltip("Idleに戻ったときに回復する連鎖発火回数")]
        public int maxChainCount = 3;

        [Header("当てた側の反応")]
        [Tooltip("当てた側が反対方向へ跳ね返る水平速度")]
        public float selfHorizontalPower = 3.0f;

        [Tooltip("当てた側が上に跳ねる垂直速度")]
        public float selfVerticalPower = 8.0f;

        [Tooltip("当てた側が受ける連鎖ダメージ")]
        public float selfDamage = 15f;

        [Header("当てられた側の反応")]
        [Tooltip("相手を押し出す水平速度")]
        public float targetHorizontalPower = 4.5f;

        [Tooltip("相手を上に跳ね上げる垂直速度")]
        public float targetVerticalPower = 12f;

        [Tooltip("相手に与える連鎖ダメージ")]
        public float targetDamage = 50f;
    }

    [System.Serializable]
    private class SurfaceHitParam
    {
        [Header("Surface Hit Notify")]
        [Tooltip("吹き飛び反射時に、接触先のIHitReceiverへHitEventを送る")]
        public bool notifyHitReceiverOnSurfaceBounce = true;

        [Tooltip("この速度未満では通知しない。低速で壁に触れただけで壊さないため")]
        public float minSurfaceHitNotifySpeed = 2.0f;

        [Tooltip("床への反射でも通知するか。基本OFF推奨")]
        public bool notifyFloorSurfaceHit = false;

        [Header("Debug")]
        public bool logSurfaceHitNotify = false;
    }

    [System.Serializable]
    private class ExcludeLayerStateParam
    {
        [Header("CharacterController Exclude Layers")]
        public LayerMask sleepExcludeLayers;
        public LayerMask chaseExcludeLayers;
        public LayerMask chargeExcludeLayers;
        public LayerMask dashattackExcludeLayers;
        public LayerMask impactpauseExcludeLayers;
        public LayerMask attackCoolDownExcludeLayers;
        public LayerMask returnhomeExcludeLayers;
        public LayerMask launchExcludeLayers;
        public LayerMask knockdownExcludeLayers;
    }

    [System.Serializable]
    private class DeathEffectParam
    {
        [Header("Death Effect")]
        public int effectIndex = 6;

        [Tooltip("設定されていれば、このTransform位置に爆発を出す")]
        public Transform effectPoint;

        [Tooltip("effectPointがない場合に使うOffset")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("effectPoint再生時のScale")]
        public float scale = 1.0f;
    }

    [Header("Chain Param")]
    [SerializeField] private ChainParam chainParam = new ChainParam();

    [Header("Enemy Param")]
    [SerializeField] private EnemyParam enemyParam = new EnemyParam();

    [Header("Animation Params")]
    [SerializeField] private FoundParam foundParam = new FoundParam();
    [SerializeField] private PreChargeParam preChargeParam = new PreChargeParam();
    [SerializeField] private ChargeLoopParam chargeLoopParam = new ChargeLoopParam();
    [SerializeField] private AttackCooldownAnimParam attackCooldownAnimParam = new AttackCooldownAnimParam();

    [Header("Forced Ground Recovery")]
    [SerializeField] private ForcedGroundRecoveryParam forcedGroundRecoveryParam = new ForcedGroundRecoveryParam();

    [Header("Visual Param")]
    [SerializeField] private VisualParam visualParam = new VisualParam();

    [Header("Receive Blow")]
    [SerializeField] private bool logReceivedDamage = false;

    [Header("CharacterController Layer Override")]
    [SerializeField] private ExcludeLayerStateParam excludeLayerStateParam = new ExcludeLayerStateParam();

    [Header("References")]
    [SerializeField] private MissileEnemyMotor motor;
    [SerializeField] private EnemyVisionSensor sensor;
    [SerializeField] private EnemyVisionDebugRenderer visionDebugRenderer;
    [SerializeField] private MissileEnemyAnimatorDriver animDriver;
    [SerializeField] private Transform meshRoot;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    [Header("HitBoxes")]
    [SerializeField] private GameObject receiveHitBox;
    [SerializeField] private GameObject bounceHitBox;
    [SerializeField] private GameObject dashAttackHitBox;

    [Header("Surface Hit Notify")]
    [SerializeField] private SurfaceHitParam surfaceHitParam = new SurfaceHitParam();

    [Header("Debug")]
    [SerializeField] private bool drawStateLabel = true;
    [SerializeField] private bool drawWakeRange = true;
    [SerializeField] private Vector3 stateLabelOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Death Effect")]
    [SerializeField] private DeathEffectParam deathEffectParam = new DeathEffectParam();

    private EffectPlayer effectPlayer;
    private EffectBonePlayer effectBonePlayer;
    private AudioManager audioManager;

    private float hp;
    private float loseSightTimer;
    private float nextAttackAllowedTime;
    private bool pendingKnockdownRecovery;
    private float groundedRecoveryTimer;
    private State currentState;
    private string currentStateName = "None";
    private Vector3 lockedDashTargetPosition;
    private Vector3 chargeAnchorPosition;

    private bool foundAnimationFinished;
    private bool preChargeAnimationFinished;
    private bool dashAttackHitTargetRequested;
    private bool chargeAnimationFinished;

    private int remainingChainCount;
    private int lastChainFrame = -1;

    private Vector3 meshRootDefaultLocalPos;
    private float currentMeshYOffset;

    #region　ステート
    private abstract class State
    {
        protected EnemyControllerTypeMissile enemy;

        public void SetOwner(EnemyControllerTypeMissile owner)
        {
            enemy = owner;
        }

        public virtual void Enter() { }
        public virtual void Update(float dt) { }
        public virtual void Exit() { }

        public virtual bool AllowMoveAnimation() => true;
        public virtual float GetTargetMeshYOffset() => 0f;
        public virtual float GetMeshYOffsetLerpSpeed() => enemy.visualParam.getUpRecoverSpeed;
    }

    private class SleepHover : State
    {
        public override void Enter()
        {
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.sleepExcludeLayers);
            enemy.motor.StopSpecialMotion();
            enemy.animDriver?.EnterIdle();
            enemy.effectBonePlayer.StopEffect(0);
            enemy.effectBonePlayer.PlayEffect(7);
        }

        public override void Update(float dt)
        {
            enemy.motor.HoverAtHome(dt);

            if (enemy.CanDetectPlayer())
                enemy.ChangeState(new Found());
        }
    }

    private class Found : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;
            enemy.foundAnimationFinished = false;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chaseExcludeLayers);

            enemy.motor.StopSpecialMotion();

            enemy.effectPlayer.Play(3);

            if (enemy.playerTarget != null)
                enemy.motor.FaceToPointSmooth(enemy.playerTarget.position, 1f, 10f);

            enemy.animDriver?.PlayFound();
        }

        public override void Update(float dt)
        {
            timer += dt;

            if (enemy.playerTarget != null && enemy.foundParam.facePlayerWhileFound)
                enemy.motor.FaceToPointSmooth(enemy.playerTarget.position, dt, 2.0f);

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.FoundStateName
                : "";

            bool finished = enemy.IsStateAnimationFinished(
                enemy.foundParam.animation,
                timer,
                enemy.foundAnimationFinished,
                stateName
            );

            if (!finished)
                return;

            if (enemy.CanDetectPlayer())
                enemy.ChangeState(new Chase());
            else
                enemy.ChangeState(new ReturnHome());
        }

        public override void Exit()
        {
            enemy.effectBonePlayer.StopEffect(7);
            enemy.effectBonePlayer.PlayEffect(0);
        }
        public override bool AllowMoveAnimation() => false;
    }

    private class Chase : State
    {
        public override void Enter()
        {
            enemy.loseSightTimer = 0f;
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chaseExcludeLayers);
            enemy.animDriver?.EnterFly();

            
        }

        public override void Update(float dt)
        {
            if (enemy.playerTarget == null)
            {
                enemy.ChangeState(new ReturnHome());
                return;
            }

            bool canDetectPlayer = enemy.CanDetectPlayer();

            // 壁で見えない / 視野外 / 距離外 の場合
            if (!canDetectPlayer)
            {
                enemy.loseSightTimer += dt;

                if (enemy.loseSightTimer >= enemy.enemyParam.chaseLoseDelay)
                {
                    enemy.ChangeState(new ReturnHome());
                    return;
                }

                // 見失い猶予中は、高さだけ整えながら待つ
                enemy.motor.RecoverHoverHeight(dt);

                return;
            }

            // 見えているので見失いタイマーをリセット
            enemy.loseSightTimer = 0f;

            float dist = enemy.DistanceToPlayer();

            if (dist <= enemy.enemyParam.attackStartRange &&
                Time.time >= enemy.nextAttackAllowedTime)
            {
                bool heightReady =
                    enemy.motor.IsNearHoverHeight(enemy.enemyParam.hoverRecoverTolerance);

                if (!heightReady)
                {
                    // 攻撃範囲内でも、まだ通常Hover高さに戻っていないなら先に高さを戻す
                    enemy.motor.RecoverHoverHeight(dt);

                    enemy.motor.FaceToPointSmooth(
                        enemy.playerTarget.position,
                        dt,
                        1.5f
                    );

                    return;
                }

                enemy.ChangeState(new PreCharge());
                return;
            }

            enemy.motor.MoveTowardPoint(
                enemy.playerTarget.position,
                enemy.enemyParam.chaseSpeed,
                enemy.enemyParam.chaseHeightOffset,
                true,
                dt
            );
        }
    }

    private class PreCharge : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;
            enemy.preChargeAnimationFinished = false;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chargeExcludeLayers);

            enemy.motor.StopSpecialMotion();
            enemy.chargeAnchorPosition = enemy.transform.position;

            enemy.animDriver?.EnterPreCharge();
        }

        public override void Update(float dt)
        {
            timer += dt;

            enemy.motor.HoldHoverHeightPlus(
                enemy.enemyParam.chargeRiseHeight,
                enemy.enemyParam.chargeRiseSpeed,
                dt
            );

            if (enemy.playerTarget != null && enemy.preChargeParam.facePlayerWhilePreCharge)
                enemy.motor.FaceToPointSmooth(enemy.playerTarget.position, dt, 2.0f);

            bool heightReady =
                enemy.motor.IsNearHoverHeightPlus(
                    enemy.enemyParam.chargeRiseHeight,
                    enemy.enemyParam.hoverRecoverTolerance
                );

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.PreChargeStateName
                : "";

            bool animFinished = enemy.IsStateAnimationFinished(
                enemy.preChargeParam.animation,
                timer,
                enemy.preChargeAnimationFinished,
                stateName
            );

            if (!heightReady || !animFinished)
                return;

            enemy.ChangeState(new Charge());
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class Charge : State
    {
        private float timer;
        private int chargeHandle = -1;

        public override void Enter()
        {
            timer = 0f;
            enemy.chargeAnimationFinished = false;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chargeExcludeLayers);

            enemy.motor.StopSpecialMotion();

            chargeHandle = enemy.audioManager.PlaySeWithHandle("ME_Charge", enemy.deathEffectParam.effectPoint.position);

            enemy.animDriver?.EnterChargeLoop();

            enemy.effectBonePlayer.PlayEffect(1);
            enemy.effectBonePlayer.PlayEffect(2);
            enemy.effectBonePlayer.PlayEffect(3);
            enemy.effectBonePlayer.PlayEffect(4);
            enemy.effectBonePlayer.PlayEffect(5);
            enemy.effectBonePlayer.PlayEffect(6);
        }

        public override void Update(float dt)
        {
            timer += dt;

            enemy.motor.HoldHoverHeightPlus(
                enemy.enemyParam.chargeRiseHeight,
                enemy.enemyParam.chargeRiseSpeed,
                dt
            );

            if (enemy.playerTarget != null && enemy.chargeLoopParam.facePlayerWhileCharge)
            {
                enemy.motor.FaceToPointSmooth(
                    enemy.playerTarget.position,
                    dt,
                    2.0f
                );
            }

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.ChargeLoopStateName
                : "";

            bool finished = enemy.IsStateAnimationFinished(
                enemy.chargeLoopParam.animation,
                timer,
                enemy.chargeAnimationFinished,
                stateName
            );

            if (!finished)
                return;

            enemy.lockedDashTargetPosition =
                enemy.playerTarget != null
                    ? enemy.playerTarget.position
                    : enemy.transform.position;

            enemy.ChangeState(new DashAttack());
        }

        public override void Exit()
        {
            enemy.audioManager.StopSe(chargeHandle);
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class DashAttack : State
    {
        public override void Enter()
        {
            enemy.ApplyDashAttackMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.dashattackExcludeLayers);
            enemy.animDriver?.PlayLoopAttack();
            enemy.motor.BeginDash(enemy.lockedDashTargetPosition);
            enemy.effectPlayer.Play(4);
            enemy.audioManager.PlaySe("ME_Attack", enemy.deathEffectParam.effectPoint.position);
        }

        public override void Update(float dt)
        {
            enemy.motor.UpdateDash(dt);

            if (enemy.motor.DashHitEnvironmentThisFrame)
            {
                enemy.ChangeState(new ImpactPause());
                return;
            }

            if (!enemy.motor.IsDashing)
                enemy.ChangeState(new ImpactPause());
        }

        public override void Exit()
        {
            enemy.audioManager.StopSe("ME_Attack");
            enemy.ApplyNormalHitMode();
            enemy.dashAttackHitTargetRequested = false;
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class ImpactPause : State
    {
        private float timer;

        public override void Enter()
        {
            timer = enemy.enemyParam.postImpactPause;

            enemy.motor.StopSpecialMotion();
            enemy.nextAttackAllowedTime = Time.time + enemy.enemyParam.attackCooldownDuration;
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.impactpauseExcludeLayers);

            // 追加：ImpactPause中はAttack姿勢を維持
            enemy.animDriver?.HoldAttackPosture();

            enemy.effectBonePlayer.StopEffect(1);
            enemy.effectBonePlayer.StopEffect(2);
            enemy.effectBonePlayer.StopEffect(3);
            enemy.effectBonePlayer.StopEffect(4);
            enemy.effectBonePlayer.StopEffect(5);
            enemy.effectBonePlayer.StopEffect(6);
        }

        public override void Update(float dt)
        {
            timer -= dt;

            if (timer <= 0f)
                enemy.ChangeState(new AttackCooldown());
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class AttackCooldown : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.attackCoolDownExcludeLayers);

            enemy.motor.StopSpecialMotion();

            enemy.nextAttackAllowedTime =
                Time.time + enemy.enemyParam.attackCooldownDuration;

            enemy.animDriver?.PlayAttackCooldown();
        }

        public override void Update(float dt)
        {
            timer += dt;

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.AttackCooldownStateName
                : "";

            bool finished = enemy.IsStateAnimationFinished(
                enemy.attackCooldownAnimParam.animation,
                timer,
                false,
                stateName
            );

            if (!finished)
                return;

            enemy.animDriver?.ExitAttackLike();
            enemy.animDriver?.ForceIdle();

            enemy.ChangeState(new RecoverHover());
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class ReturnHome : State
    {
        public override void Enter()
        {
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.returnhomeExcludeLayers);
            enemy.animDriver?.EnterFly();
        }

        public override void Update(float dt)
        {
            if (enemy.CanDetectPlayer())
            {
                enemy.ChangeState(new Chase());
                return;
            }

            enemy.motor.MoveTowardPoint(
                enemy.motor.HomePosition,
                enemy.enemyParam.returnSpeed,
                enemy.enemyParam.returnHeightOffset,
                true,
                dt
            );

            float planarDistanceToHome = enemy.GetPlanarDistance(
                enemy.transform.position,
                enemy.motor.HomePosition
            );

            if (planarDistanceToHome <= enemy.enemyParam.homeArriveDistance)
            {
                enemy.ChangeState(new SleepHover());
            }
        }
    }

    private class LaunchAirborne : State
    {
        private float failSafeTimer;

        public override void Enter()
        {
            failSafeTimer = 0f;
            enemy.ApplyLaunchHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.launchExcludeLayers);
            enemy.motor.StopSpecialMotion();
            enemy.animDriver?.PlayLaunch();

            enemy.effectBonePlayer.StopEffect(0);
            enemy.effectBonePlayer.StopEffect(1);
            enemy.effectBonePlayer.StopEffect(2);
            enemy.effectBonePlayer.StopEffect(3);
            enemy.effectBonePlayer.StopEffect(4);
            enemy.effectBonePlayer.StopEffect(5);
            enemy.effectBonePlayer.StopEffect(6);
            enemy.effectBonePlayer.StopEffect(7);

            //enemy.audioManager.StopSe("ME_Charge");
          
        }

        public override void Update(float dt)
        {
            failSafeTimer += Time.deltaTime * enemy.motor.TimeScale;
            enemy.motor.UpdateBlow(dt);

            if (!enemy.pendingKnockdownRecovery)
                return;

            if (enemy.motor.IsKnockdownGroundNotified ||(!enemy.motor.IsBlown && failSafeTimer > 0.05f && enemy.motor.IsGrounded))
            {
                enemy.pendingKnockdownRecovery = false;

                if (enemy.hp <= 0f)
                {
                    enemy.ChangeState(new Death());
                }
                else
                {
                    enemy.ChangeState(new KnockdownRecovery());
                }
            }
        }

        public override bool AllowMoveAnimation() => false;
    }

    private class KnockdownRecovery : State
    {
        private bool enteredGetUp;
        private float layDownTimer;

        public override void Enter()
        {
            enteredGetUp = false;
            layDownTimer = 0f;

            enemy.ApplyKnockdownHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.knockdownExcludeLayers);
            enemy.motor.StopSpecialMotion();
            enemy.animDriver?.EnterLayDown();
        }

        public override void Update(float dt)
        {
            // Knockdown中もBounce/吹っ飛び物理は継続
            enemy.motor.UpdateBlow(dt);

            if (!enteredGetUp)
            {
                layDownTimer += Time.deltaTime * enemy.motor.TimeScale;

                if (layDownTimer < enemy.enemyParam.layDownMinDuration)
                    return;

                bool stoppedEnough =
                    enemy.motor.IsLayDownReady() ||
                    (!enemy.motor.IsBlown &&
                     enemy.motor.CurrentPlanarSpeed <= enemy.enemyParam.getUpStartSpeedThreshold);

                bool forceGetUp =
                    layDownTimer >= enemy.enemyParam.forcedGetUpDelay;

                if (stoppedEnough || forceGetUp)
                {
                    enteredGetUp = true;

                    enemy.effectBonePlayer.PlayEffect(0);
                    enemy.motor.ForceEndBlow();
                    enemy.animDriver?.BeginGetUp();
                    enemy.ResetChainCount();
                }

                return;
            }

            if (enemy.animDriver == null || enemy.animDriver.IsGetUpFinished())
            {
                enemy.motor.ForceEndBlow();
                enemy.animDriver?.ForceIdle();

                // ここで直接Chase/ReturnHomeへ行かず、
                // 一度通常Hover高さへ戻す
                enemy.ChangeState(new RecoverHover());
            }
        }

        public override bool AllowMoveAnimation() => false;

        public override float GetTargetMeshYOffset()
        {
            return enteredGetUp ? 0f : -enemy.visualParam.layDownSinkDepth;
        }

        public override float GetMeshYOffsetLerpSpeed()
        {
            return enteredGetUp
                ? enemy.visualParam.getUpRecoverSpeed
                : enemy.visualParam.sinkSpeed;
        }
    }

    private class Death : State
    {
        private bool hasStopped;
        private bool deathEffectPlayed;
        private float deathTimer;
        private float layDownTimer;
        private bool soundOnce;

        public override void Enter()
        {
            deathTimer = 0f;
            layDownTimer = 0f;
            hasStopped = false;
            deathEffectPlayed = false;
            soundOnce = false;

            enemy.ApplyKnockdownHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.knockdownExcludeLayers);

            if (enemy.animDriver != null)
                enemy.animDriver.EnterLayDown();
        }

        public override void Update(float dt)
        {
            enemy.motor.UpdateBlow(dt);

            if (!hasStopped)
            {
                layDownTimer += dt;

                bool stoppedEnough =
                    enemy.motor.IsLayDownReady() ||
                    (!enemy.motor.IsBlown &&
                     enemy.motor.CurrentPlanarSpeed <= enemy.enemyParam.getUpStartSpeedThreshold);

                bool forceStop =
                    layDownTimer >= enemy.enemyParam.forcedGetUpDelay;

                if (stoppedEnough || forceStop)
                {
                    hasStopped = true;

                    enemy.motor.ForceEndBlow();

                    if (!deathEffectPlayed)
                    {
                        deathEffectPlayed = true;
                        enemy.PlayDeathEffect();
                    }
                }

                return;
            }

            deathTimer += dt;

            if (deathTimer >= enemy.enemyParam.deathDestroyDelay - 0.5f && !soundOnce)
            {
                enemy.audioManager.PlaySe("E_Explosion", enemy.deathEffectParam.effectPoint.position);
                soundOnce = true;
            }

            if (deathTimer >= enemy.enemyParam.deathDestroyDelay)
            {
                enemy.Destroy();
            }
        }

        public override bool AllowMoveAnimation() => false;
    }
    private class RecoverHover : State
    {
        private float timer;

        public override void Enter()
        {
            timer = enemy.enemyParam.hoverRecoverTimeout;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chaseExcludeLayers);
            enemy.motor.StopSpecialMotion();
            enemy.animDriver?.EnterFly();
        }

        public override void Update(float dt)
        {
            timer -= dt;

            enemy.motor.RecoverHoverHeight(dt);

            if (enemy.playerTarget != null)
                enemy.motor.FaceToPointSmooth(enemy.playerTarget.position, dt, 1.5f);

            bool heightReady =
                enemy.motor.IsNearHoverHeight(enemy.enemyParam.hoverRecoverTolerance);

            bool timeout = timer <= 0f;

            if (!heightReady && !timeout)
                return;

            if (enemy.CanDetectPlayer())
                enemy.ChangeState(new Chase());
            else
                enemy.ChangeState(new ReturnHome());
        }
    }

    #endregion
    private void Awake()
    {
        if (motor == null)
            motor = GetComponent<MissileEnemyMotor>();

        if (sensor == null)
            sensor = GetComponent<EnemyVisionSensor>();

        if (animDriver == null)
            animDriver = GetComponentInChildren<MissileEnemyAnimatorDriver>(true);


        effectPlayer = GetComponent<EffectPlayer>();

        if(effectBonePlayer == null)
            effectBonePlayer = GetComponentInChildren<EffectBonePlayer>();

        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTarget = p.transform;
        }

        if (meshRoot == null)
        {
            Transform mesh = transform.Find("Mesh");
            if (mesh != null)
                meshRoot = mesh;
        }

        if (meshRoot != null)
            meshRootDefaultLocalPos = meshRoot.localPosition;

        hp = enemyParam.maxHP;

        if (visionDebugRenderer != null && sensor != null)
            visionDebugRenderer.Initialize(transform, sensor.EyePoint);

        if (animDriver != null)
            animDriver.Initialize(motor);

        if (dashAttackHitBox != null)
            dashAttackHitBox.SetActive(false);

        ResetChainCount();

        motor.OnFirstGroundContactDuringBlow += HandleFirstGroundContactDuringBlow;
        motor.OnReflectionSelfDamage += HandleReflectionSelfDamage;
    }

    private void Start()
    {
        ChangeState(new SleepHover());

        audioManager = AudioManager.Instance;

        MissionManager.Instance.AddTotalEnemy();
    }
    public void InitializeFromSpawner(Transform target = null)
    {
        if (target != null)
            playerTarget = target;
    }


    private void OnDestroy()
    {
        if (motor != null)
        {
            motor.OnFirstGroundContactDuringBlow -= HandleFirstGroundContactDuringBlow;
            motor.OnReflectionSelfDamage -= HandleReflectionSelfDamage;
        }
    }

    private void Update()
    {
        if (motor.TimeScale <= 0f)
            return;

        float dt = Time.deltaTime * motor.TimeScale;

        motor.TickBegin(dt);
        currentState?.Update(dt);

        UpdateForcedGroundRecovery(dt);
        UpdateMeshRootOffset(dt);

        if (animDriver != null)
        {
            bool allowMoveAnimation =
                currentState == null || currentState.AllowMoveAnimation();

            animDriver.Tick(allowMoveAnimation);
        }

        if (visionDebugRenderer != null && sensor != null)
        {
            visionDebugRenderer.SetVision(
                sensor.DetectRange,
                sensor.ViewAngle,
                IsBattleVisionState()
            );
        }
    }

    public void AE_EndFound()
    {
        foundAnimationFinished = true;
    }

    public void AE_EndPreCharge()
    {
        preChargeAnimationFinished = true;
    }

    public void AE_EndCharge()
    {
        chargeAnimationFinished = true;
    }

    private void UpdateForcedGroundRecovery(float dt)
    {
        if (!forcedGroundRecoveryParam.enable)
            return;

        if (motor == null)
            return;

        if (!(currentState is LaunchAirborne))
        {
            groundedRecoveryTimer = 0f;
            return;
        }

        bool groundedLike = motor.CanEnterKnockdownRecovery;

        if (!groundedLike)
        {
            groundedRecoveryTimer = 0f;
            return;
        }

        groundedRecoveryTimer += dt;

        if (groundedRecoveryTimer < forcedGroundRecoveryParam.groundedRecoveryDelay)
            return;

        motor.ForceEndBlow();
        pendingKnockdownRecovery = false;
        if (hp <= 0f)
        {
            ChangeState(new Death());
        }
        else
        {
            ChangeState(new KnockdownRecovery());
        }
        groundedRecoveryTimer = 0f;
    }

    private void UpdateMeshRootOffset(float dt)
    {
        if (meshRoot == null)
            return;

        float targetOffset = currentState != null ? currentState.GetTargetMeshYOffset() : 0f;
        float lerpSpeed = currentState != null ? currentState.GetMeshYOffsetLerpSpeed() : visualParam.getUpRecoverSpeed;

        currentMeshYOffset = Mathf.MoveTowards(
            currentMeshYOffset,
            targetOffset,
            lerpSpeed * dt
        );

        Vector3 pos = meshRootDefaultLocalPos;
        pos.y += currentMeshYOffset;
        meshRoot.localPosition = pos;
    }

    private void HandleFirstGroundContactDuringBlow()
    {
        if (!pendingKnockdownRecovery)
            return;

        pendingKnockdownRecovery = false;
        if (hp <= 0f)
        {
            ChangeState(new Death());
        }
        else
        {
            ChangeState(new KnockdownRecovery());
        }
    }

    private void HandleReflectionSelfDamage(float damage, EnemySurfaceKind kind)
    {
        if (damage <= 0f)
            return;

        if (logReceivedDamage)
        {
            Debug.Log(
                $"{name} Reflection Damage " +
                $"kind:{kind} damage:{damage:F2}"
            );
        }

        TakeDamage(damage);
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
    private void ChangeState(State next)
    {
        currentState?.Exit();
        groundedRecoveryTimer = 0f;

        currentState = next;
        currentState.SetOwner(this);
        currentState.Enter();

        currentStateName = next.GetType().Name;

        if (sensor != null)
            sensor.SetChasing(IsBattleVisionState());
    }

    private bool IsPlayerInsideWakeSphere()
    {
        if (playerTarget == null)
            return false;

        return Vector3.Distance(transform.position, playerTarget.position) <= enemyParam.wakeRange;
    }

    private bool CanDetectPlayer()
    {
        if (playerTarget == null)
            return false;

        // wakeRange は最大検知距離として使う
        if (Vector3.Distance(transform.position, playerTarget.position) > enemyParam.wakeRange)
            return false;

        // sensor が無い場合は距離判定だけで通す
        if (sensor == null)
            return true;

        // 視野角 + 壁遮蔽判定
        return sensor.CanSeeTarget(transform, playerTarget);
    }
    private bool IsStateAnimationFinished(
    StateAnimationFinishParam param,
    float timer,
    bool eventFinished,
    string animatorStateName)
    {
        if (param == null)
            return true;

        switch (param.finishMode)
        {
            case AnimationFinishMode.Timer:
                return timer >= param.duration;

            case AnimationFinishMode.AnimationEvent:
                return eventFinished;

            case AnimationFinishMode.AnimationEnd:
                if (animDriver == null)
                    return timer >= param.duration;

                return animDriver.IsStateFinished(
                    animatorStateName,
                    param.endNormalizedTime
                );
        }

        return false;
    }

    private float DistanceToPlayer()
    {
        if (playerTarget == null)
            return float.MaxValue;

        return Vector3.Distance(transform.position, playerTarget.position);
    }

    private void ApplyNormalHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(true);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(false);

        if (dashAttackHitBox != null)
            dashAttackHitBox.SetActive(false);
    }

    private void ApplyDashAttackMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(true);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(false);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(true);

        if (dashAttackHitBox != null)
            dashAttackHitBox.SetActive(true);
    }

    private void ApplyLaunchHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(false);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(true);

        if(chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(true);

        if (dashAttackHitBox != null)
            dashAttackHitBox.SetActive(false);
    }
     private void ApplyKnockdownHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(false);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(true);

        if(chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(false);

        if (dashAttackHitBox != null)
            dashAttackHitBox.SetActive(false);
    }

    private void ApplyStateExcludeLayers(LayerMask mask)
    {
        if (motor != null)
            motor.SetCharacterControllerExcludeLayers(mask);
    }

    private void PlayDeathEffect()
    {
        if (effectPlayer == null)
            return;

        // effectPointがあるなら、そのWorld座標に爆発を出す
        if (deathEffectParam.effectPoint != null)
        {
            effectPlayer.PlayAt(
                deathEffectParam.effectIndex,
                deathEffectParam.effectPoint.position,
                Quaternion.identity,
                Vector3.one * deathEffectParam.scale
            );

            return;
        }

        // effectPointがないなら、今まで通り自分基準のPlayで再生
        EffectPlayParam param = EffectPlayParam.Default;
        param.positionOffset = deathEffectParam.positionOffset;

        effectPlayer.Play(
            deathEffectParam.effectIndex,
            param
        );
    }

    #region 連鎖関係Chain

    private void ResetChainCount()
    {
        remainingChainCount = Mathf.Max(0, chainParam.maxChainCount);
        UpdateChainColliderEnabled();
    }

    private void ConsumeChainCount()
    {
        remainingChainCount = Mathf.Max(remainingChainCount - 1, 0);
        UpdateChainColliderEnabled();
    }

    private void UpdateChainColliderEnabled()
    {
        if (chainParam.chainCollider == null)
            return;

        bool enabled = remainingChainCount > 0;
        if (chainParam.chainCollider.activeSelf != enabled)
            chainParam.chainCollider.SetActive(enabled);
    }

    #endregion

    public bool TryNotifySurfaceHitReceiver(
   Collider selfTrigger,
   Collider other,
   EnemySurfaceHit surfaceHit)
    {
        if (!surfaceHitParam.notifyHitReceiverOnSurfaceBounce)
            return false;

        if (selfTrigger == null || other == null)
            return false;

        if (motor == null)
            return false;

        // 吹き飛び中以外は通知しない
        if (!motor.IsEnemyFlying())
            return false;

        // 自分自身・自分の子は除外
        if (other.transform == transform || other.transform.IsChildOf(transform))
            return false;

        // Trigger相手には通知しない
        if (other.isTrigger)
            return false;

        // 床は基本通知しない
        if (surfaceHit.kind == EnemySurfaceKind.Floor &&
            !surfaceHitParam.notifyFloorSurfaceHit)
        {
            return false;
        }

        // 低速では破壊可能ブロックなどを壊さない
        Vector3 totalVelocity = motor.CurrentTotalVelocity;
        float speed = totalVelocity.magnitude;

        if (speed < surfaceHitParam.minSurfaceHitNotifySpeed)
            return false;

        // 相手側Hitbox取得
        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox == null)
            return false;

        // 相手側Receiver取得
        IHitReceiver receiver = targetHitbox.receiver;
        GameObject receiverObject = null;

        if (receiver != null)
        {
            MonoBehaviour receiverMono = receiver as MonoBehaviour;
            if (receiverMono != null)
                receiverObject = receiverMono.gameObject;
        }

        // Hitbox.receiver が未設定だった場合の保険
        if (receiver == null)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];

                if (behaviour == null)
                    continue;

                if (behaviour == this)
                    continue;

                if (behaviour is Hitbox)
                    continue;

                if (behaviour is IHitReceiver foundReceiver)
                {
                    receiver = foundReceiver;
                    receiverObject = behaviour.gameObject;
                    break;
                }
            }
        }

        if (receiver == null || receiverObject == null)
            return false;

        if (receiverObject == gameObject)
            return false;

        Vector3 hitDir = totalVelocity;

        if (hitDir.sqrMagnitude < 0.0001f)
            hitDir = -surfaceHit.normal;

        hitDir.y = 0f;

        if (hitDir.sqrMagnitude < 0.0001f)
            hitDir = transform.forward;

        hitDir.Normalize();

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,

            // 攻撃側Hitboxとして、反射判定用Hitboxを優先
            attackerHitbox =
                bounceHitBox != null
                    ? bounceHitBox
                    : gameObject,

            targetObject = receiverObject,
            targetHitbox = targetHitbox.gameObject,

            contactPoint = other.ClosestPoint(selfTrigger.bounds.center),

            // BlowObjectController側と同じく、ここではpayloadなし
            // 破壊可能ブロック側が「HitEventを受けたら壊れる」設計ならこれでOK
            payload = null
        };

        if (surfaceHitParam.logSurfaceHitNotify)
        {
            Debug.Log(
                $"{name} SurfaceHitNotify " +
                $"target:{receiverObject.name} " +
                $"hitbox:{targetHitbox.name} " +
                $"surface:{surfaceHit.kind} " +
                $"speed:{speed:F2} " +
                $"normal:{surfaceHit.normal}"
            );
        }

        receiver.OnHit(data);
        return true;
    }
    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {

        if (selfHitbox == null || other == null)
            return;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        if(selfHitbox.gameObject == dashAttackHitBox)
        {
            HitEventData data = new HitEventData
            {
                attackerObject = this.gameObject,
                attackerHitbox = selfHitbox.gameObject,
                targetHitbox = targetHitbox.gameObject,
                payload = new EnemyAttackPayload
                {
                    damage = enemyParam.attackDamage
                }
            };

            effectPlayer.Play(5, targetHitbox.transform);

            targetHitbox.receiver.OnHit(data);
        }

        if (selfHitbox.gameObject == chainParam.chainCollider)
        {
            if (remainingChainCount <= 0)
                return;

            if (lastChainFrame == Time.frameCount)
                return;

            if (!motor.IsEnemyFlying())
                return;

            IHitReceiver receiver = null;
            GameObject receiverObject = null;

            receiver = other.GetComponentInParent<IHitReceiver>();
            if (receiver == null) return;

            Component receiverComponent = receiver as Component;
            receiverObject = receiverComponent != null ? receiverComponent.gameObject : other.gameObject;

            if (receiverObject == gameObject) return;

            int chainIndex = 1;

            if (ChainHitManager.Instance != null)
            {
                if (!ChainHitManager.Instance.TryBeginChainPair
                    (gameObject,
                    receiverObject,
                    chainParam.pairCooldown,
                    out chainIndex))
                {
                    return;
                }
            }

            Vector3 directionToTarget = motor.GetChainDirectionTo(receiverObject);

            lastChainFrame = Time.frameCount;
            ConsumeChainCount();

            motor.ApplyChainReaction(-directionToTarget, chainParam.selfHorizontalPower, chainParam.selfVerticalPower);

            TakeDamage(chainParam.selfDamage);
            pendingKnockdownRecovery = true;
            ChangeState(new LaunchAirborne());

            ChainPayload payload = new ChainPayload
            {
                source = this,
                chainIndex = chainIndex,
                direction = directionToTarget,
                horizontalPower = chainParam.targetHorizontalPower,
                verticalPower = chainParam.targetVerticalPower,
                damage = chainParam.targetDamage
            };

            HitEventData hit = new HitEventData
            {
                attackerObject = gameObject,
                attackerHitbox = selfHitbox.gameObject,
                targetObject = receiverObject,
                targetHitbox = targetHitbox.gameObject,
                payload = payload
            };

            receiver.OnHit(hit);

            if (ChainHitManager.Instance != null)
                ChainHitManager.Instance.NotifyChain(chainIndex);
        }
       
    }

    private float ResolveDamageFromBlow(BlowPayload blow)
    {
        float rate = Mathf.Clamp01(blow.powerRate);
        return Mathf.Max(0f, blow.powerConstant * rate);
    }

    private void HandleReceivedBlow(BlowPayload blow)
    {
        TackleType tackleType = blow.tackleType;
        float finalDamage = ResolveDamageFromBlow(blow);

        if (logReceivedDamage)
        {
            Debug.Log(
                $"{name} ReceiveBlow " +
                $"type:{tackleType} " +
                $"powerConst:{blow.powerConstant:F2} " +
                $"powerRate:{blow.powerRate:F2} " +
                $"finalDamage:{finalDamage:F2}"
            );
        }

        motor.ApplyBlow(
            blow.powerDirection,
            blow.powerConstant,
            blow.powerRate,
            tackleType
        );

        pendingKnockdownRecovery = true;
        ChangeState(new LaunchAirborne());

        TakeDamage(finalDamage);
    }

    public void ApplyBlow(Vector3 dir, float powerC, float powerR)
    {
        BlowPayload blow = new BlowPayload
        {
            powerDirection = dir,
            powerConstant = powerC,
            powerRate = powerR
        };

        HandleReceivedBlow(blow);
    }

    public void OnHit(HitEventData data)
    {
        if (data.payload is BlowPayload blow)
        {
            if (receiveHitBox != null && data.targetHitbox == receiveHitBox)
            {
                HandleReceivedBlow(blow);
                return;
            }

            if (dashAttackHitBox != null && data.targetHitbox == dashAttackHitBox)
            {
                HandleReceivedBlow(blow);
                return;
            }
        }

        if (data.payload is ChainPayload chain)
        {
            lastChainFrame = Time.frameCount;

            TakeDamage(chain.damage);

            motor.ApplyChainReaction(chain.direction, chain.horizontalPower, chain.verticalPower);
            pendingKnockdownRecovery = true;
            ChangeState(new LaunchAirborne());

            return;
        }

    }

    private bool IsBattleVisionState()
    {
        return currentState is Found ||
               currentState is Chase ||
               currentState is PreCharge ||
               currentState is Charge ||
               currentState is DashAttack ||
               currentState is ImpactPause ||
               currentState is AttackCooldown ||
               currentState is RecoverHover;
    }

    private void TakeDamage(float damage)
    {
        hp -= damage;
        if (hp <= 0f && !KillCountFlg)
        {
            KillCountFlg = true;
            MissionManager.Instance.AddKill(enemyParam.enemyData);
        }
        //if (hp > 0f)
        //    return;

        ////if (effectPlayer != null)
        ////    effectPlayer.Play(0);

        //Destroy(gameObject);
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawWakeRange)
            return;

        Gizmos.color = new Color(0.4f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, enemyParam.wakeRange);
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!drawStateLabel)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 worldPos = transform.position + stateLabelOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0f)
            return;

        float x = screenPos.x - 70f;
        float y = Screen.height - screenPos.y - 10f;

        GUI.Box(new Rect(x, y, 140f, 22f), currentStateName);
#endif
    }
}