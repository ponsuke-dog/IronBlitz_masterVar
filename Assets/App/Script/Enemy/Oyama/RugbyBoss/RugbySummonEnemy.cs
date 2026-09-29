using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TimeAgent))]
[RequireComponent(typeof(CharacterController))]
public class RugbySummonEnemy : MonoBehaviour, IHitReceiver, IHitSource
{
    private enum SummonStateName
    {
        None,
        SpawnMotion,
        Chase,
        Airborne,
        GroundMove,
        KnockdownRecovery,
        ScrumMoveToPoint,
        ScrumIdle,
        ScrumRush,
        Dead
    }

    #region Inspector Parameter Classes

    [System.Serializable]
    private class AnimationParam
    {
        [Header("Animator")]
        [Tooltip("召喚エネミーのAnimatorです。未設定なら子から自動取得します。")]
        public Animator animator;

        [Tooltip("ONならAnimatorへCrossFadeInFixedTimeを実行します。")]
        public bool useAnimator = true;

        [Tooltip("ONならAnimator.speedにTimeAgentのTimeScaleを反映します。")]
        public bool syncAnimatorSpeedWithTimeAgent = true;

        [Tooltip("AnimatorのLayer番号です。基本は0です。")]
        public int layerIndex = 0;

        [Header("Animation State Names")]
        [Tooltip("召喚直後の出現アニメーションステート名です。")]
        public string spawnStateName = "RSE_Spawn";

        [Tooltip("プレイヤーへ走る時のアニメーションステート名です。")]
        public string runStateName = "RSE_Run";

        [Tooltip("スクラム集合地点へ走る時のアニメーションステート名です。")]
        public string scrumMoveStateName = "RSE_Scrum_Move";

        [Tooltip("吹き飛び中、空中のアニメーションステート名です。")]
        public string blowAirStateName = "RSE_Blow_Air";

        [Tooltip("着地/地面移動中のアニメーションステート名です。")]
        public string blowLandingStateName = "RSE_Blow_Landing";

        [Tooltip("起き上がりアニメーションステート名です。")]
        public string wakeUpStateName = "RSE_WakeUp";

        [Tooltip("スクラム隊列待機中のアニメーションステート名です。")]
        public string scrumIdleStateName = "RSE_Scrum_Idle";

        [Tooltip("スクラム突撃中のアニメーションステート名です。")]
        public string scrumRushStateName = "RSE_Scrum_Rush";

        [Header("CrossFade")]
        public float spawnFadeTime = 0.05f;
        public float runFadeTime = 0.05f;
        public float scrumMoveFadeTime = 0.05f;
        public float blowAirFadeTime = 0.03f;
        public float blowLandingFadeTime = 0.03f;
        public float wakeUpFadeTime = 0.05f;
        public float scrumIdleFadeTime = 0.05f;
        public float scrumRushFadeTime = 0.05f;

        [Header("Spawn Finish")]
        [Tooltip("召喚アニメーションがこのnormalizedTime以上になったら追跡へ移行します。")]
        [Range(0.1f, 1.2f)]
        public float spawnFinishNormalizedTime = 0.98f;

        [Tooltip("Animatorが遷移中の場合は召喚アニメーション終了判定を待ちます。")]
        public bool waitTransitionCompleteBeforeSpawnFinish = true;
    }

    [System.Serializable]
    private class SpawnParam
    {
        [Header("Spawn")]
        [Tooltip("ONなら召喚アニメーション終了でChaseへ移行します。OFFならfallbackSpawnDurationで移行します。")]
        public bool useAnimationFinishForSpawnTransition = true;

        [Tooltip("Animator未使用、State名未設定、またはアニメーション終了判定を使わない場合の保険時間です。")]
        public float fallbackSpawnDuration = 0.35f;

        [Tooltip("召喚直後もプレイヤー方向へY軸だけで向き続けるかどうかです。")]
        public bool facePlayerDuringSpawn = true;

        [Tooltip("召喚直後から通常タックル受けHitboxを有効にするかどうかです。")]
        public bool enableReceiveDuringSpawn = true;
    }

    [System.Serializable]
    private class ChaseParam
    {
        [Header("Chase")]
        [Tooltip("召喚敵がプレイヤーへ向かう速度です。")]
        public float chaseSpeed = 4.5f;

        [Tooltip("プレイヤー方向へY軸だけで向き直る速度です。度/秒。")]
        public float turnSpeed = 540f;

        [Tooltip("プレイヤーが見つからない場合、現在の正面方向へ進むかどうかです。")]
        public bool moveForwardWhenNoPlayer = true;


        [Header("エフェクト位置")]
        [Tooltip("右エフェクト再生位置")]
        public Transform RChaseAttack;
        [Tooltip("左エフェクト再生位置")]
        public Transform LChaseAttack;
    }

    [System.Serializable]
    private class ScrumMoveParam
    {
        [Header("Scrum Move")]
        [Tooltip("スクラム集合地点へ向かう基本速度です。Managerから上書きされる場合があります。")]
        public float defaultMoveSpeed = 6f;

        [Tooltip("集合地点へ到着したとみなす距離です。Managerから上書きされる場合があります。")]
        public float defaultArriveDistance = 0.25f;

        [Tooltip("スクラム集合地点へ向かう間、Y軸だけで進行方向へ向けます。")]
        public bool faceMoveDirection = true;
    }

    [System.Serializable]
    private class MotionParam
    {
        [Header("Basic Physics")]
        public float mass = 1f;
        public float gravity = 28f;
        public float groundStickForce = 1.5f;

        [Header("Drag")]
        public float airLinearDrag = 0.6f;
        public float groundLinearDrag = 8f;
        public float lowSpeedGroundBrake = 14f;

        [Header("Stop")]
        public float minStopSpeed = 0.35f;
        public float minStopVerticalSpeed = 0.35f;
        public float snapStopSpeed = 0.12f;
        public float stopConfirmTime = 0.12f;
        public float minAirborneTime = 0.08f;

        [Header("High Speed Move")]
        public float maxAirborneMoveStepDistance = 1.5f;
        public int maxAirborneMoveSubSteps = 12;
    }

    [System.Serializable]
    private class BounceParam
    {
        [Header("Surface Classification")]
        public float floorMaxAngle = 10f;
        public float slopeMaxAngle = 55f;
        [Range(-1f, 0f)] public float ceilingMaxNormalY = -0.35f;

        [Header("Bounce Power")]
        public float wallBouncePower = 0.8f;
        public float slopeBouncePower = 0.45f;
        public float floorBouncePower = 0.2f;
        public float ceilingBouncePower = 0.4f;

        [Header("Bounce Threshold")]
        public float minFloorBounceSpeed = 1f;
        public float minCeilingBounceSpeed = 1f;
        public float minWallImpactSpeed = 0.5f;
        public float minSlopeImpactSpeed = 0.5f;

        [Header("Contact Memory")]
        public float contactMemoryTime = 0.18f;

        [Header("Floor Bounce Stabilize")]
        public bool useControllerBelowForFloorBounce = true;
        public float floorBounceDetachDistance = 0.03f;

        [Header("Multi Bounce Guard")]
        public bool blockMultiBouncePerFrame = true;

        [Header("Clamp")]
        public float maxBounceHorizontalSpeed = 40f;
        public float maxBounceVerticalSpeed = 28f;
    }

    [System.Serializable]
    private class StatusParam
    {
        [Header("HP")]
        public float maxHP = 10f;
        public bool destroyWhenHPZero = true;

        [Header("Life")]
        public float lifeTime = 0f;
        public float maxMoveDistanceFromSpawn = 0f;

        [Header("Recovery")]
        public float knockdownRecoveryDuration = 0.6f;
    }

    [System.Serializable]
    private class HitboxGroupParam
    {
        [Header("Normal Tackle Receive Hitboxes")]
        [Tooltip("通常タックルを受けるHitbox群です。Spawn/Chase中に使います。")]
        public List<GameObject> receiveHitboxes = new List<GameObject>();

        [Header("Normal Attack / Just Tackle Receive Hitboxes")]
        [Tooltip("通常の接触攻撃Hitbox群です。Chase中だけONにします。ジャストタックル受けにも使います。")]
        public List<GameObject> attackHitboxes = new List<GameObject>();

        [Header("Chain Hitboxes")]
        [Tooltip("連鎖判定用Hitbox群です。")]
        public List<GameObject> chainHitboxes = new List<GameObject>();

        [Header("Scrum Attack Hitboxes")]
        [Tooltip("スクラム突撃中だけONにする専用攻撃Hitbox群です。イベントはManagerへ通知します。")]
        public List<GameObject> scrumAttackHitboxes = new List<GameObject>();

        [Header("Scrum Just Tackle Receive Hitboxes")]
        [Tooltip("スクラム中にジャストタックルを受ける専用Hitbox群です。空ならscrumAttackHitboxesを受けにも使います。")]
        public List<GameObject> scrumReceiveHitboxes = new List<GameObject>();

        [Header("Attack Damage")]
        public int playerDamage = 1;
        public bool hitSameTargetOncePerChase = true;
        public bool destroyAfterAttackHit = false;
    }

    [System.Serializable]
    private class LaunchReactionProfile
    {
        [Header("Launch Power")]
        public float horizontalPower = 20f;
        public float verticalPower = 8f;

        [Header("Damage")]
        public float selfDamage = 10f;

        [Header("Chain")]
        public bool canEmitChainAfterThisReaction = true;
    }

    [System.Serializable]
    private class LaunchReactionParam
    {
        [Header("Normal Tackle")]
        public LaunchReactionProfile normalTackle = new LaunchReactionProfile
        {
            horizontalPower = 20f,
            verticalPower = 8f,
            selfDamage = 10f,
            canEmitChainAfterThisReaction = true
        };

        [Header("Charge Tackle")]
        public LaunchReactionProfile chargeTackle = new LaunchReactionProfile
        {
            horizontalPower = 26f,
            verticalPower = 10f,
            selfDamage = 12f,
            canEmitChainAfterThisReaction = true
        };

        [Header("Just Normal Tackle")]
        public LaunchReactionProfile justNormalTackle = new LaunchReactionProfile
        {
            horizontalPower = 30f,
            verticalPower = 11f,
            selfDamage = 14f,
            canEmitChainAfterThisReaction = true
        };

        [Header("Just Charge Tackle")]
        public LaunchReactionProfile justChargeTackle = new LaunchReactionProfile
        {
            horizontalPower = 36f,
            verticalPower = 13f,
            selfDamage = 18f,
            canEmitChainAfterThisReaction = true
        };

        [Header("Chain")]
        public LaunchReactionProfile chain = new LaunchReactionProfile
        {
            horizontalPower = 28f,
            verticalPower = 12f,
            selfDamage = 50f,
            canEmitChainAfterThisReaction = true
        };

        [Header("Chain Emit")]
        public float minChainSpeed = 2.5f;
        public float pairCooldown = 0.15f;
        public int maxChainCount = 3;
        public float chainPayloadHorizontalPower = 10f;
        public float chainPayloadVerticalPower = 15f;
        public float chainPayloadDamage = 50f;

        [Tooltip("ONなら、この召喚エネミーが連鎖を発生させた瞬間に即爆散して破棄します。多段連鎖ヒット防止用です。")]
        public bool destroySelfAfterChainEmit = true;

        [Tooltip("連鎖発生側が即爆散する時にEffectPlayer.Playで再生するエフェクト番号です。")]
        public int chainSelfExplosionEffectIndex = 1;

        [Header("Common")]
        public float minVerticalPower = 5f;
        public bool canReceiveChain = true;
    }

    [System.Serializable]
    private class LayerParam
    {
        [Header("CharacterController Exclude Layers")]
        public LayerMask chaseExcludeLayers;
        public LayerMask airborneExcludeLayers;
        public LayerMask groundMoveExcludeLayers;
        public LayerMask recoveryExcludeLayers;
        public LayerMask scrumMoveExcludeLayers;
    }

    [System.Serializable]
    private class DebugParam
    {
        [Header("Log")]
        public bool logState = true;
        public bool logHit = true;
        public bool logLaunch = true;
        public bool logChain = true;
        public bool logAttack = true;
        public bool logHitboxSwitch = false;
        public bool logScrum = true;

        [Header("GUI")]
        public bool drawDebugGUI = false;
        public Vector3 guiOffset = new Vector3(0f, 1.8f, 0f);
    }

    #endregion

    #region Inspector Fields

    [Header("Animation")]
    [SerializeField] private AnimationParam animationParam = new AnimationParam();

    [Header("Spawn")]
    [SerializeField] private SpawnParam spawnParam = new SpawnParam();

    [Header("Chase")]
    [SerializeField] private ChaseParam chaseParam = new ChaseParam();

    [Header("Scrum Move")]
    [SerializeField] private ScrumMoveParam scrumMoveParam = new ScrumMoveParam();

    [Header("Motion")]
    [SerializeField] private MotionParam motionParam = new MotionParam();

    [Header("Bounce")]
    [SerializeField] private BounceParam bounceParam = new BounceParam();

    [Header("Status")]
    [SerializeField] private StatusParam statusParam = new StatusParam();

    [Header("Hitboxes")]
    [SerializeField] private HitboxGroupParam hitboxParam = new HitboxGroupParam();

    [Header("Launch / Damage / Chain")]
    [SerializeField] private LaunchReactionParam launchReactionParam = new LaunchReactionParam();

    [Header("Layers")]
    [SerializeField] private LayerParam layerParam = new LayerParam();

    [Header("Debug")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    #endregion

    #region Runtime Fields

    private RugbyBoss ownerBoss;
    private Transform playerTarget;
    private TimeAgent timeAgent;
    private CharacterController characterController;
    private Renderer[] cachedRenderers;
    private EffectPlayer effectPlayer;

    private SummonStateBase currentState;
    private SummonStateBase reserveState;
    private int reservePriority;
    private SummonStateName currentStateName = SummonStateName.None;

    private float hp;
    private float lifeTimer;
    private float stateTimer;
    private Vector3 spawnPosition;
    private Vector3 velocity;
    private float verticalVelocity;
    private float stopTimer;
    private float stuckRecoverTimer;
    private bool pendingDeathAfterLaunch;
    private bool currentCanEmitChain;
    private bool ignoreChainWhileCurrentLaunch;
    private int remainingChainCount;
    private int lastChainFrame = -1;
    private int lastBounceFrame = -1;
    private bool groundedThisFrame;
    private float airborneGroundedStableTimer;

    private Vector3 recentGroundNormal = Vector3.up;
    private Vector3 recentWallNormal = Vector3.zero;
    private Vector3 recentCeilingNormal = Vector3.down;
    private float groundContactTimer;
    private float wallContactTimer;
    private float ceilingContactTimer;
    private float wallDetachTimer;

    private Transform currentScrumRoot;
    private Vector3 currentScrumLocalPosition;
    private Quaternion currentScrumLocalRotation = Quaternion.identity;
    private float currentScrumMoveSpeed;
    private float currentScrumArriveDistance;

    private readonly HashSet<GameObject> hitTargetsThisChase = new HashSet<GameObject>();

    #endregion

    #region Public Properties

    public bool IsAlive => currentStateName != SummonStateName.Dead;

    public bool CanJoinScrumCommand =>
        currentStateName == SummonStateName.SpawnMotion ||
        currentStateName == SummonStateName.Chase;

    public bool IsCapturedByScrum =>
        currentStateName == SummonStateName.ScrumIdle ||
        currentStateName == SummonStateName.ScrumRush;

    public bool IsMovingToScrum => currentStateName == SummonStateName.ScrumMoveToPoint;
    public bool IsReadyForScrumFormation => IsCapturedByScrum;
    public bool IsScrumRush => currentStateName == SummonStateName.ScrumRush;

    public bool IsFlying =>
        currentStateName == SummonStateName.Airborne ||
        currentStateName == SummonStateName.GroundMove;

    public float CurrentHP => hp;
    public float MaxHP => statusParam.maxHP;
    public string CurrentStateName => currentStateName.ToString();

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    private float ScaledDeltaTime => Time.deltaTime * TimeScale;

    #endregion

    #region State Base

    private abstract class SummonStateBase
    {
        protected readonly RugbySummonEnemy enemy;

        protected SummonStateBase(RugbySummonEnemy enemy)
        {
            this.enemy = enemy;
        }

        public abstract SummonStateName StateName { get; }
        public virtual void Enter() { }
        public virtual void Update(float dt) { }
        public virtual void Exit() { }
    }

    #endregion

    #region States

    private sealed class SpawnMotionState : SummonStateBase
    {
        public SpawnMotionState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.SpawnMotion;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.spawnStateName, enemy.animationParam.spawnFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.chaseExcludeLayers);
            enemy.SetVisible(true);
            enemy.ApplySpawnHitboxMode();
            enemy.velocity = Vector3.zero;
            enemy.verticalVelocity = 0f;
            enemy.stopTimer = 0f;
            enemy.stuckRecoverTimer = 0f;

            enemy.effectPlayer.Play(3);
        }

        public override void Update(float dt)
        {
            if (enemy.spawnParam.facePlayerDuringSpawn)
                enemy.FacePlayer(dt);

            enemy.ApplyGroundStickOrGravity(dt);
            enemy.MoveByCharacterController(Vector3.up * enemy.verticalVelocity * dt);

            bool finishedByAnimation = enemy.spawnParam.useAnimationFinishForSpawnTransition &&
                                       enemy.IsAnimationFinished(
                                           enemy.animationParam.spawnStateName,
                                           enemy.animationParam.spawnFinishNormalizedTime,
                                           enemy.animationParam.waitTransitionCompleteBeforeSpawnFinish
                                       );

            bool shouldUseFallback = !enemy.spawnParam.useAnimationFinishForSpawnTransition ||
                                     !enemy.CanUseAnimationFinish(enemy.animationParam.spawnStateName);

            bool finishedByFallback = shouldUseFallback &&
                                      enemy.stateTimer >= Mathf.Max(0f, enemy.spawnParam.fallbackSpawnDuration);

            if (!finishedByAnimation && !finishedByFallback)
                return;

            enemy.ReserveState(new ChaseState(enemy), 0);
        }
    }

    private sealed class ChaseState : SummonStateBase
    {
        public ChaseState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.Chase;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.runStateName, enemy.animationParam.runFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.chaseExcludeLayers);
            enemy.SetVisible(true);
            enemy.ignoreChainWhileCurrentLaunch = false;
            enemy.ApplyChaseHitboxMode();
            enemy.ResetChainCount();
            enemy.currentCanEmitChain = true;
            enemy.hitTargetsThisChase.Clear();
            enemy.velocity = Vector3.zero;
            if (enemy.IsGrounded())
                enemy.verticalVelocity = -enemy.motionParam.groundStickForce;
            enemy.stopTimer = 0f;
            enemy.stuckRecoverTimer = 0f;
        }

        public override void Update(float dt)
        {
            Vector3 chaseDirection = enemy.GetChaseDirection();
            enemy.RotateTowardY(chaseDirection, dt);
            enemy.velocity = enemy.transform.forward * enemy.chaseParam.chaseSpeed;
            enemy.ApplyGroundStickOrGravity(dt);
            Vector3 move = enemy.velocity;
            move.y = enemy.verticalVelocity;
            enemy.MoveByCharacterController(move * dt);
        }

        public override void Exit()
        {
            enemy.hitTargetsThisChase.Clear();
        }
    }

    private sealed class ScrumMoveToPointState : SummonStateBase
    {
        public ScrumMoveToPointState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.ScrumMoveToPoint;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.scrumMoveStateName, enemy.animationParam.scrumMoveFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.scrumMoveExcludeLayers);
            enemy.SetVisible(true);
            enemy.ApplyScrumMoveHitboxMode();
            enemy.velocity = Vector3.zero;
            enemy.verticalVelocity = 0f;
            enemy.stopTimer = 0f;
            enemy.stuckRecoverTimer = 0f;
        }

        public override void Update(float dt)
        {
            Vector3 targetPosition = enemy.GetCurrentScrumWorldPosition();
            Vector3 toTarget = targetPosition - enemy.transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance <= Mathf.Max(0.01f, enemy.currentScrumArriveDistance))
            {
                enemy.CaptureIntoScrumIdle(
                    enemy.currentScrumRoot,
                    enemy.currentScrumLocalPosition,
                    enemy.currentScrumLocalRotation
                );
                return;
            }

            Vector3 direction = toTarget.normalized;
            if (enemy.scrumMoveParam.faceMoveDirection)
                enemy.RotateTowardY(direction, dt);

            enemy.ApplyGroundStickOrGravity(dt);
            Vector3 move = direction * Mathf.Max(0f, enemy.currentScrumMoveSpeed);
            move.y = enemy.verticalVelocity;
            enemy.MoveByCharacterController(move * dt);
        }
    }

    private sealed class AirborneState : SummonStateBase
    {
        public AirborneState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.Airborne;

        private EffectInstance air;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.blowAirStateName, enemy.animationParam.blowAirFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.airborneExcludeLayers);
            enemy.SetVisible(true);
            enemy.ApplyAirborneHitboxMode();
            enemy.stopTimer = 0f;
            enemy.stuckRecoverTimer = 0f;

            air = enemy.effectPlayer.Play(2);
        }

        public override void Update(float dt)
        {
            enemy.velocity = enemy.DampHorizontalSpeed(enemy.velocity, enemy.motionParam.airLinearDrag, dt);

            if (enemy.stateTimer < enemy.motionParam.minAirborneTime)
                return;

            bool groundedLike = enemy.groundedThisFrame || enemy.HasRecentGroundContact() || enemy.IsGrounded();
            enemy.airborneGroundedStableTimer = groundedLike && enemy.verticalVelocity <= 0.2f
                ? enemy.airborneGroundedStableTimer + dt
                : 0f;

            if (enemy.airborneGroundedStableTimer >= 0.02f)
                enemy.ReserveState(new GroundMoveState(enemy), 100);
        }

        public override void Exit()
        {
            if (air != null)
            {
                air.Stop();
                air = null;
            }
        }
    }

    private sealed class GroundMoveState : SummonStateBase
    {
        public GroundMoveState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.GroundMove;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.blowLandingStateName, enemy.animationParam.blowLandingFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.groundMoveExcludeLayers);
            enemy.SetVisible(true);
            enemy.ApplyGroundMoveHitboxMode();
            enemy.stopTimer = 0f;
            enemy.stuckRecoverTimer = 0f;
            enemy.airborneGroundedStableTimer = 0f;
        }

        public override void Update(float dt)
        {
            bool groundedLike = enemy.groundedThisFrame || enemy.HasRecentGroundContact() || enemy.IsGrounded();
            if (!groundedLike)
            {
                enemy.ReserveState(new AirborneState(enemy), 50);
                return;
            }

            enemy.velocity = enemy.ProjectVelocityToGround(enemy.velocity);
            float drag = enemy.motionParam.groundLinearDrag;
            if (enemy.velocity.magnitude <= 0.5f)
                drag += enemy.motionParam.lowSpeedGroundBrake;

            enemy.velocity = enemy.MoveTowardsHorizontalSpeed(enemy.velocity, drag, dt);
            if (enemy.velocity.magnitude <= enemy.motionParam.snapStopSpeed)
                enemy.velocity = Vector3.zero;
            if (enemy.verticalVelocity < 0f)
                enemy.verticalVelocity = -enemy.motionParam.groundStickForce;

            float stopCheckVerticalVelocity = enemy.verticalVelocity;
            if (groundedLike && stopCheckVerticalVelocity <= 0f)
                stopCheckVerticalVelocity = 0f;

            bool lowHorizontal = enemy.velocity.magnitude <= enemy.motionParam.minStopSpeed;
            bool lowVertical = Mathf.Abs(stopCheckVerticalVelocity) <= enemy.motionParam.minStopVerticalSpeed;

            if (lowHorizontal && lowVertical)
            {
                enemy.stopTimer += dt;
                if (enemy.stopTimer >= enemy.motionParam.stopConfirmTime)
                {
                    enemy.velocity = Vector3.zero;
                    enemy.verticalVelocity = 0f;

                    if (enemy.pendingDeathAfterLaunch || enemy.hp <= 0f && enemy.statusParam.destroyWhenHPZero)
                    {
                        enemy.ReserveState(new DeadState(enemy), int.MaxValue);
                        return;
                    }

                    enemy.ReserveState(new KnockdownRecoveryState(enemy), 0);
                }
            }
            else
            {
                enemy.stopTimer = 0f;
            }
        }
    }

    private sealed class KnockdownRecoveryState : SummonStateBase
    {
        public KnockdownRecoveryState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.KnockdownRecovery;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.wakeUpStateName, enemy.animationParam.wakeUpFadeTime);
            enemy.EnsureCharacterControllerEnabled(true);
            enemy.SetCharacterControllerExcludeLayers(enemy.layerParam.recoveryExcludeLayers);
            enemy.SetVisible(true);
            enemy.ApplyKnockdownHitboxMode();
            enemy.velocity = Vector3.zero;
            enemy.verticalVelocity = -enemy.motionParam.groundStickForce;
        }

        public override void Update(float dt)
        {
            enemy.ApplyGroundStickOrGravity(dt);
            enemy.MoveByCharacterController(Vector3.up * enemy.verticalVelocity * dt);

            if (enemy.stateTimer < enemy.statusParam.knockdownRecoveryDuration)
                return;

            if (enemy.pendingDeathAfterLaunch || enemy.hp <= 0f && enemy.statusParam.destroyWhenHPZero)
            {
                enemy.ReserveState(new DeadState(enemy), int.MaxValue);
                return;
            }

            enemy.ReserveState(new ChaseState(enemy), 0);
        }
    }

    private sealed class ScrumIdleState : SummonStateBase
    {
        public ScrumIdleState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.ScrumIdle;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.scrumIdleStateName, enemy.animationParam.scrumIdleFadeTime);
            enemy.velocity = Vector3.zero;
            enemy.verticalVelocity = 0f;
            enemy.ApplyScrumIdleHitboxMode();
            enemy.ApplyScrumCapturedCommonMode();
            enemy.ApplyScrumSlotTransform();
        }

        public override void Update(float dt)
        {
            enemy.ApplyScrumSlotTransform();
        }
    }

    private sealed class ScrumRushState : SummonStateBase
    {
        public ScrumRushState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.ScrumRush;

        private EffectInstance rushEffect = null;

        public override void Enter()
        {
            enemy.ResetStateTimer();
            enemy.CrossFadeAnimation(enemy.animationParam.scrumRushStateName, enemy.animationParam.scrumRushFadeTime);
            enemy.velocity = Vector3.zero;
            enemy.verticalVelocity = 0f;
            enemy.ApplyScrumRushHitboxMode();
            enemy.ApplyScrumCapturedCommonMode();
            enemy.ApplyScrumSlotTransform();

            // エフェクト
            if (rushEffect == null)
            {
                EffectPlayParam param = EffectPlayParam.Default;
                param.positionOffset = enemy.transform.forward * 6.0f;

                rushEffect = enemy.effectPlayer.Play(4, param);
            }
        }

        public override void Update(float dt)
        {
            enemy.ApplyScrumSlotTransform();
        }

        public override void Exit()
        {
            // エフェクト
            if (rushEffect != null)
            {
                rushEffect.StopImmediate();
                rushEffect = null;
            }
        }
    }

    private sealed class DeadState : SummonStateBase
    {
        public DeadState(RugbySummonEnemy enemy) : base(enemy) { }
        public override SummonStateName StateName => SummonStateName.Dead;

        public override void Enter()
        {
            enemy.ApplyAllHitboxesOff();
            enemy.EnsureCharacterControllerEnabled(false);
            enemy.effectPlayer.PlayAt(1, enemy.transform.position);
            Destroy(enemy.gameObject);
        }
    }

    #endregion

    #region Unity Events

    private void Awake()
    {
        timeAgent = GetComponent<TimeAgent>();
        characterController = GetComponent<CharacterController>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        effectPlayer = GetComponent<EffectPlayer>();

        if (animationParam.animator == null)
            animationParam.animator = GetComponentInChildren<Animator>();

        hp = Mathf.Max(1f, statusParam.maxHP);
        lifeTimer = 0f;
        stateTimer = 0f;
        spawnPosition = transform.position;
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        stopTimer = 0f;
        stuckRecoverTimer = 0f;
        pendingDeathAfterLaunch = false;
        currentCanEmitChain = true;
        ignoreChainWhileCurrentLaunch = false;
        remainingChainCount = 0;
        lastChainFrame = -1;
        lastBounceFrame = -1;
        groundedThisFrame = false;
        airborneGroundedStableTimer = 0f;
        currentScrumRoot = null;
        currentScrumLocalPosition = Vector3.zero;
        currentScrumLocalRotation = Quaternion.identity;
        currentScrumMoveSpeed = Mathf.Max(0f, scrumMoveParam.defaultMoveSpeed);
        currentScrumArriveDistance = Mathf.Max(0.01f, scrumMoveParam.defaultArriveDistance);
        ResetContactMemory();
        currentState = null;
        reserveState = null;
        reservePriority = int.MinValue;
        currentStateName = SummonStateName.None;
        ApplyAllHitboxesOff();
    }

    private void Update()
    {
        SyncAnimatorSpeed();

        if (currentStateName == SummonStateName.Dead)
            return;

        float dt = ScaledDeltaTime;
        if (dt <= 0f)
            return;

        reservePriority = int.MinValue;
        lifeTimer += dt;
        stateTimer += dt;

        if (ShouldDestroyByLifeLimit())
        {
            ForceDestroy();
            return;
        }

        UpdateContactTimers(dt);
        groundedThisFrame = false;

        if (IsFlying)
        {
            ApplyGravityForFlying(dt);
            ApplyFlyingMovement(dt);
            UpdateStuckRecovery(dt);
            currentState?.Update(dt);
        }
        else
        {
            currentState?.Update(dt);
        }

        ApplyReserveState();
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || !IsFlying)
            return;

        SurfaceType type = ClassifySurface(hit.normal);
        UpdateSurfaceMemory(type, hit.normal);

        if (type == SurfaceType.Wall || type == SurfaceType.Slope || type == SurfaceType.Ceiling)
            TryBounce(type, hit.normal);
    }

    #endregion

    #region Initialize

    public void Initialize(RugbyBoss owner, Transform target)
    {
        ownerBoss = owner;
        playerTarget = target;
        hp = Mathf.Max(1f, statusParam.maxHP);
        lifeTimer = 0f;
        stateTimer = 0f;
        spawnPosition = transform.position;
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        stopTimer = 0f;
        stuckRecoverTimer = 0f;
        pendingDeathAfterLaunch = false;
        currentCanEmitChain = true;
        ignoreChainWhileCurrentLaunch = false;
        lastChainFrame = -1;
        lastBounceFrame = -1;
        groundedThisFrame = false;
        currentScrumRoot = null;
        ResetContactMemory();
        ResetChainCount();
        hitTargetsThisChase.Clear();
        ReserveState(new SpawnMotionState(this), 0);
        ApplyReserveState();
    }

    #endregion

    #region State Management

    private void ReserveState(SummonStateBase nextState, int priority)
    {
        if (nextState == null)
            return;
        if (priority < reservePriority)
            return;

        reserveState = nextState;
        reservePriority = priority;
    }

    private void ApplyReserveState()
    {
        if (reserveState == null)
            return;

        SummonStateBase nextState = reserveState;
        reserveState = null;
        reservePriority = int.MinValue;

        if (currentState != null && currentState.GetType() == nextState.GetType())
            return;

        currentState?.Exit();
        currentState = nextState;
        currentStateName = nextState.StateName;
        currentState.Enter();

        if (debugParam.logState)
            Debug.Log($"{name} RugbySummonEnemy State => {currentStateName}");
    }

    private void ResetStateTimer()
    {
        stateTimer = 0f;
    }

    #endregion

    #region Hitbox Mode

    private void ApplySpawnHitboxMode()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, spawnParam.enableReceiveDuringSpawn);
        SetHitboxListActive(hitboxParam.attackHitboxes, false);
        SetHitboxListActive(hitboxParam.chainHitboxes, true);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("Spawn");
    }

    private void ApplyChaseHitboxMode()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, true);
        SetHitboxListActive(hitboxParam.attackHitboxes, true);
        SetHitboxListActive(hitboxParam.chainHitboxes, true);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("Chase");
    }

    private void ApplyAirborneHitboxMode()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, false);
        SetHitboxListActive(hitboxParam.attackHitboxes, false);
        SetHitboxListActive(hitboxParam.chainHitboxes, true);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("Airborne");
    }

    private void ApplyGroundMoveHitboxMode()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, false);
        SetHitboxListActive(hitboxParam.attackHitboxes, false);
        SetHitboxListActive(hitboxParam.chainHitboxes, true);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("GroundMove");
    }

    private void ApplyKnockdownHitboxMode()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, false);
        SetHitboxListActive(hitboxParam.attackHitboxes, false);
        SetHitboxListActive(hitboxParam.chainHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("Knockdown");
    }

    private void ApplyScrumMoveHitboxMode()
    {
        ApplyAllHitboxesOff();
        LogHitboxMode("ScrumMove");
    }

    private void ApplyScrumIdleHitboxMode()
    {
        ApplyAllHitboxesOff();
        SetScrumReceiveHitboxesActive(true);
        LogHitboxMode("ScrumIdle");
    }

    private void ApplyScrumRushHitboxMode()
    {
        ApplyAllHitboxesOff();
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, true);
        SetScrumReceiveHitboxesActive(true);
        LogHitboxMode("ScrumRush");
    }

    private void ApplyAllHitboxesOff()
    {
        SetHitboxListActive(hitboxParam.receiveHitboxes, false);
        SetHitboxListActive(hitboxParam.attackHitboxes, false);
        SetHitboxListActive(hitboxParam.chainHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, false);
        LogHitboxMode("AllOff");
    }

    private void SetScrumReceiveHitboxesActive(bool active)
    {
        if (hitboxParam.scrumReceiveHitboxes != null && hitboxParam.scrumReceiveHitboxes.Count > 0)
        {
            SetHitboxListActive(hitboxParam.scrumReceiveHitboxes, active);
            return;
        }

        SetHitboxListActive(hitboxParam.scrumAttackHitboxes, active);
    }

    private void SetHitboxListActive(List<GameObject> hitboxes, bool active)
    {
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject hitbox = hitboxes[i];
            if (hitbox == null)
                continue;
            if (hitbox.activeSelf == active)
                continue;
            hitbox.SetActive(active);
        }
    }

    private void LogHitboxMode(string mode)
    {
        if (!debugParam.logHitboxSwitch)
            return;
        Debug.Log($"{name} RugbySummonEnemy HitboxMode => {mode}");
    }

    #endregion

    #region Movement Helpers

    private Vector3 GetChaseDirection()
    {
        if (playerTarget != null)
        {
            Vector3 direction = playerTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;
        }

        if (chaseParam.moveForwardWhenNoPlayer)
            return transform.forward;

        return Vector3.zero;
    }

    private void FacePlayer(float dt)
    {
        if (playerTarget == null)
            return;

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;
        RotateTowardY(direction, dt);
    }

    private void RotateTowardY(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, chaseParam.turnSpeed * dt);
    }

    private void ApplyGroundStickOrGravity(float dt)
    {
        if (IsGrounded() && verticalVelocity < 0f)
        {
            verticalVelocity = -motionParam.groundStickForce;
            return;
        }

        verticalVelocity -= motionParam.gravity * dt;
    }

    #endregion

    #region Flying Movement

    private void ApplyGravityForFlying(float dt)
    {
        if (HasRecentGroundContact() && velocity.magnitude <= motionParam.minStopSpeed && Mathf.Abs(verticalVelocity) <= motionParam.minStopVerticalSpeed)
        {
            verticalVelocity = -motionParam.groundStickForce;
            return;
        }

        if (HasRecentGroundContact() && !(currentStateName == SummonStateName.Airborne && verticalVelocity > 0f))
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -motionParam.groundStickForce;
        }
        else
        {
            verticalVelocity -= motionParam.gravity * dt;
        }
    }

    private void ApplyFlyingMovement(float dt)
    {
        Vector3 movePerSecond = velocity;
        movePerSecond.y = verticalVelocity;

        if (wallDetachTimer > 0f && IsFlying)
            movePerSecond += recentWallNormal * 2f;

        Vector3 totalDelta = movePerSecond * dt;
        float totalDistance = totalDelta.magnitude;
        int stepCount = 1;

        if (motionParam.maxAirborneMoveStepDistance > 0f)
        {
            stepCount = Mathf.CeilToInt(totalDistance / motionParam.maxAirborneMoveStepDistance);
            stepCount = Mathf.Clamp(stepCount, 1, Mathf.Max(1, motionParam.maxAirborneMoveSubSteps));
        }

        Vector3 stepDelta = totalDelta / stepCount;
        Vector3 beforeAllPosition = transform.position;
        bool bouncedByControllerFloor = false;
        CollisionFlags combinedFlags = CollisionFlags.None;

        for (int i = 0; i < stepCount; i++)
        {
            float verticalBeforeMove = verticalVelocity;
            CollisionFlags flags = MoveByCharacterController(stepDelta);
            combinedFlags |= flags;

            bool bouncedThisStep = TryControllerFloorBounce(verticalBeforeMove, flags);
            if (bouncedThisStep)
            {
                bouncedByControllerFloor = true;
                break;
            }

            if ((flags & CollisionFlags.Below) != 0)
            {
                groundedThisFrame = true;
                recentGroundNormal = Vector3.up;
                groundContactTimer = bounceParam.contactMemoryTime;
            }
        }

        Vector3 actualDelta = transform.position - beforeAllPosition;
        if (!bouncedByControllerFloor)
            UpdateStuckRecoveryByMove(dt, movePerSecond, actualDelta, combinedFlags);
    }

    private CollisionFlags MoveByCharacterController(Vector3 delta)
    {
        if (characterController == null || !characterController.enabled)
        {
            transform.position += delta;
            return CollisionFlags.None;
        }

        return characterController.Move(delta);
    }

    private bool IsGrounded()
    {
        if (characterController == null || !characterController.enabled)
            return false;
        return characterController.isGrounded;
    }

    #endregion

    #region Surface Contact / Bounce

    private enum SurfaceType
    {
        None,
        Ground,
        Slope,
        Wall,
        Ceiling
    }

    private SurfaceType ClassifySurface(Vector3 normal)
    {
        float angleFromUp = Vector3.Angle(normal, Vector3.up);
        if (normal.y <= bounceParam.ceilingMaxNormalY)
            return SurfaceType.Ceiling;
        if (angleFromUp <= bounceParam.floorMaxAngle)
            return SurfaceType.Ground;
        if (angleFromUp <= bounceParam.slopeMaxAngle)
            return SurfaceType.Slope;
        return SurfaceType.Wall;
    }

    private void UpdateSurfaceMemory(SurfaceType type, Vector3 normal)
    {
        switch (type)
        {
            case SurfaceType.Ground:
                recentGroundNormal = normal;
                groundContactTimer = bounceParam.contactMemoryTime;
                groundedThisFrame = true;
                break;
            case SurfaceType.Slope:
            case SurfaceType.Wall:
                recentWallNormal = normal;
                wallContactTimer = bounceParam.contactMemoryTime;
                break;
            case SurfaceType.Ceiling:
                recentCeilingNormal = normal;
                ceilingContactTimer = bounceParam.contactMemoryTime;
                break;
        }
    }

    private void UpdateContactTimers(float dt)
    {
        groundContactTimer = Mathf.Max(0f, groundContactTimer - dt);
        wallContactTimer = Mathf.Max(0f, wallContactTimer - dt);
        ceilingContactTimer = Mathf.Max(0f, ceilingContactTimer - dt);
        wallDetachTimer = Mathf.Max(0f, wallDetachTimer - dt);
    }

    private bool HasRecentGroundContact()
    {
        return groundContactTimer > 0f;
    }

    private void ResetContactMemory()
    {
        recentGroundNormal = Vector3.up;
        recentWallNormal = Vector3.zero;
        recentCeilingNormal = Vector3.down;
        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;
        wallDetachTimer = 0f;
    }

    private void TryBounce(SurfaceType type, Vector3 normal)
    {
        if (bounceParam.blockMultiBouncePerFrame && lastBounceFrame == Time.frameCount)
            return;

        bool bounced = false;

        switch (type)
        {
            case SurfaceType.Wall:
                bounced = BounceWall(normal);
                break;
            case SurfaceType.Slope:
                bounced = BounceSlope(normal);
                break;
            case SurfaceType.Ceiling:
                bounced = BounceCeiling(normal);
                break;
        }

        if (bounced)
            lastBounceFrame = Time.frameCount;
    }

    private bool BounceWall(Vector3 normal)
    {
        if (!IsMovingIntoSurface(normal, bounceParam.minWallImpactSpeed))
            return false;

        Vector3 wallNormal = new Vector3(normal.x, 0f, normal.z);
        if (wallNormal.sqrMagnitude < 0.0001f)
            wallNormal = normal;
        wallNormal.Normalize();

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 reflectedHorizontal = Vector3.Reflect(horizontalVelocity, wallNormal);
        velocity = reflectedHorizontal * bounceParam.wallBouncePower;
        verticalVelocity *= bounceParam.wallBouncePower;
        ClampCurrentVelocityForBounce();
        wallDetachTimer = 0.08f;
        recentWallNormal = wallNormal;
        return true;
    }

    private bool BounceSlope(Vector3 normal)
    {
        if (!IsMovingIntoSurface(normal, bounceParam.minSlopeImpactSpeed))
            return false;

        Vector3 reflected = Vector3.Reflect(GetTotalVelocity(), normal.normalized);
        velocity = new Vector3(reflected.x, 0f, reflected.z) * bounceParam.slopeBouncePower;
        verticalVelocity = reflected.y * bounceParam.slopeBouncePower;
        ClampCurrentVelocityForBounce();
        recentWallNormal = normal;
        wallDetachTimer = 0.03f;
        return true;
    }

    private bool BounceCeiling(Vector3 normal)
    {
        if (verticalVelocity < bounceParam.minCeilingBounceSpeed)
            return false;
        if (!IsMovingIntoSurface(normal, bounceParam.minCeilingBounceSpeed))
            return false;
        verticalVelocity = -verticalVelocity * bounceParam.ceilingBouncePower;
        ClampCurrentVelocityForBounce();
        return true;
    }

    private bool TryControllerFloorBounce(float verticalBeforeMove, CollisionFlags flags)
    {
        if (!bounceParam.useControllerBelowForFloorBounce)
            return false;
        if ((flags & CollisionFlags.Below) == 0)
            return false;
        if (!IsFlying)
            return false;

        groundedThisFrame = true;
        recentGroundNormal = Vector3.up;
        groundContactTimer = bounceParam.contactMemoryTime;

        if (currentStateName == SummonStateName.GroundMove)
            return false;

        if (-verticalBeforeMove < bounceParam.minFloorBounceSpeed)
        {
            verticalVelocity = -motionParam.groundStickForce;
            return false;
        }

        if (bounceParam.blockMultiBouncePerFrame && lastBounceFrame == Time.frameCount)
            return false;

        verticalVelocity = -verticalBeforeMove * bounceParam.floorBouncePower;
        velocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        ClampCurrentVelocityForBounce();
        stopTimer = 0f;
        stuckRecoverTimer = 0f;

        if (bounceParam.floorBounceDetachDistance > 0f && characterController != null && characterController.enabled)
            characterController.Move(Vector3.up * bounceParam.floorBounceDetachDistance);

        ReserveState(new AirborneState(this), 100);
        lastBounceFrame = Time.frameCount;
        return true;
    }

    #endregion

    #region Velocity Utility / Stuck Recovery

    private Vector3 DampHorizontalSpeed(Vector3 v, float drag, float dt)
    {
        float speed = v.magnitude;
        speed = Mathf.Max(speed - drag * dt, 0f);
        if (speed <= 0f)
            return Vector3.zero;
        return v.normalized * speed;
    }

    private Vector3 MoveTowardsHorizontalSpeed(Vector3 v, float drag, float dt)
    {
        float speed = v.magnitude;
        speed = Mathf.MoveTowards(speed, 0f, drag * dt);
        if (speed <= 0f)
            return Vector3.zero;
        return v.normalized * speed;
    }

    private Vector3 ProjectVelocityToGround(Vector3 v)
    {
        if (!HasRecentGroundContact() && !IsGrounded() && !groundedThisFrame)
            return v;
        return Vector3.ProjectOnPlane(v, recentGroundNormal);
    }

    private Vector3 GetTotalVelocity()
    {
        return new Vector3(velocity.x, verticalVelocity, velocity.z);
    }

    private void ClampCurrentVelocityForBounce()
    {
        if (velocity.magnitude > bounceParam.maxBounceHorizontalSpeed)
            velocity = velocity.normalized * bounceParam.maxBounceHorizontalSpeed;

        verticalVelocity = Mathf.Clamp(verticalVelocity, -bounceParam.maxBounceVerticalSpeed, bounceParam.maxBounceVerticalSpeed);
    }

    private bool IsMovingIntoSurface(Vector3 normal, float minImpactSpeed)
    {
        Vector3 totalVelocity = GetTotalVelocity();
        if (totalVelocity.sqrMagnitude < 0.0001f)
            return false;
        float intoSpeed = Vector3.Dot(totalVelocity, -normal.normalized);
        return intoSpeed >= minImpactSpeed;
    }

    private void UpdateStuckRecovery(float dt)
    {
        if (!IsFlying)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        if (velocity.magnitude > motionParam.minStopSpeed * 1.5f)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        bool groundedLike = groundedThisFrame || HasRecentGroundContact() || IsGrounded();
        if (!groundedLike)
            return;

        stuckRecoverTimer += dt;
        if (stuckRecoverTimer < motionParam.stopConfirmTime)
            return;

        velocity = Vector3.zero;
        verticalVelocity = 0f;
        ReserveState(new KnockdownRecoveryState(this), int.MaxValue);
    }

    private void UpdateStuckRecoveryByMove(float dt, Vector3 requestedMove, Vector3 actualDelta, CollisionFlags flags)
    {
        if (!IsFlying || dt <= 0f)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        float actualSpeed = actualDelta.magnitude / dt;
        bool almostNotMoving = actualSpeed <= 0.03f;
        bool lowHorizontalSpeed = velocity.magnitude <= motionParam.minStopSpeed;
        bool hitBelow = (flags & CollisionFlags.Below) != 0;
        bool hitSide = (flags & CollisionFlags.Sides) != 0;

        bool blockedByCollision = (hitBelow || hitSide) && almostNotMoving && lowHorizontalSpeed;
        bool suspiciousAirStop = flags == CollisionFlags.None && requestedMove.y < -0.001f && almostNotMoving && lowHorizontalSpeed;

        if (!blockedByCollision && !suspiciousAirStop)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        stuckRecoverTimer += dt;
        if (stuckRecoverTimer < motionParam.stopConfirmTime)
            return;

        velocity = Vector3.zero;
        verticalVelocity = 0f;
        groundedThisFrame = true;
        groundContactTimer = bounceParam.contactMemoryTime;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;
        wallDetachTimer = 0f;
        ReserveState(new KnockdownRecoveryState(this), int.MaxValue);
    }

    #endregion

    #region Hit Receive

    public void OnHit(HitEventData data)
    {
        if (currentStateName == SummonStateName.Dead)
            return;

        if (data.payload is BlowPayload blow)
        {
            if (IsCapturedByScrum || IsMovingToScrum)
            {
                if (IsJustTackle(blow) && IsScrumReceiveHitbox(data.targetHitbox))
                {
                    if (RugbyScrumManager.Instance != null)
                        RugbyScrumManager.Instance.NotifyScrumJustTackle(this, data, blow);
                }
                return;
            }

            bool justTackle = IsJustTackle(blow);
            if (!CanReceiveBlowByCorrectHitbox(data.targetHitbox, justTackle))
                return;
            ApplyBlow(data, blow);
            return;
        }

        if (data.payload is ChainPayload chain)
        {
            if (IsCapturedByScrum || IsMovingToScrum)
                return;
            if (data.attackerObject == gameObject)
                return;
            if (ignoreChainWhileCurrentLaunch && IsRugbySummonEnemyObject(data.attackerObject))
                return;
            lastChainFrame = Time.frameCount;
            ApplyReceivedChainReaction(data, chain);
            effectPlayer.Play(0);
        }
    }

    private bool CanReceiveBlowByCorrectHitbox(GameObject targetHitbox, bool justTackle)
    {
        if (justTackle)
            return ContainsHitbox(hitboxParam.attackHitboxes, targetHitbox);

        if (hitboxParam.receiveHitboxes == null || hitboxParam.receiveHitboxes.Count == 0)
            return true;

        return ContainsHitbox(hitboxParam.receiveHitboxes, targetHitbox);
    }

    private bool IsJustTackle(BlowPayload blow)
    {
        return blow.tackleType == TackleType.JustNormal ||
               blow.tackleType == TackleType.JustCharge ||
               blow.tackleType.ToString().Contains("Just");
    }

    private void ApplyBlow(HitEventData data, BlowPayload blow)
    {
        LaunchReactionProfile profile = GetTackleProfile(blow.tackleType);
        if (profile == null)
            return;

        currentCanEmitChain = profile.canEmitChainAfterThisReaction;
        Vector3 direction = ResolveHitDirection(data, blow.powerDirection);
        ApplyLaunchProfile(profile, direction, applyDamage: true);

        if (debugParam.logLaunch)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy ApplyBlow " +
                $"type:{blow.tackleType} horizontal:{profile.horizontalPower:F2} vertical:{profile.verticalPower:F2} " +
                $"damage:{profile.selfDamage:F2} dir:{direction} hp:{hp:F1} targetHitbox:{GetObjectName(data.targetHitbox)}"
            );
        }

        ReserveState(new AirborneState(this), 100);
        ApplyReserveState();
    }

    private LaunchReactionProfile GetTackleProfile(TackleType tackleType)
    {
        switch (tackleType)
        {
            case TackleType.Normal:
                return launchReactionParam.normalTackle;
            case TackleType.Charge:
                return launchReactionParam.chargeTackle;
            case TackleType.JustNormal:
                return launchReactionParam.justNormalTackle;
            case TackleType.JustCharge:
                return launchReactionParam.justChargeTackle;
        }

        return launchReactionParam.normalTackle;
    }

    private void ApplyReceivedChainReaction(HitEventData data, ChainPayload chain)
    {
        if (ignoreChainWhileCurrentLaunch && IsRugbySummonEnemyObject(data.attackerObject))
            return;

        if (!launchReactionParam.canReceiveChain)
            return;

        LaunchReactionProfile profile = launchReactionParam.chain;
        if (profile == null)
            return;

        currentCanEmitChain = profile.canEmitChainAfterThisReaction;
        Vector3 direction = ResolveHitDirection(data, chain.direction);
        ApplyLaunchProfile(profile, direction, applyDamage: true);

        if (debugParam.logChain)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy ReceiveChain " +
                $"from:{GetObjectName(data.attackerObject)} chainIndex:{chain.chainIndex} " +
                $"horizontal:{profile.horizontalPower:F2} vertical:{profile.verticalPower:F2} damage:{profile.selfDamage:F2} " +
                $"hp:{hp:F1} state:{currentStateName}"
            );
        }

        ReserveState(new AirborneState(this), 100);
        ApplyReserveState();
    }

    private void ApplyLaunchProfile(LaunchReactionProfile profile, Vector3 direction, bool applyDamage)
    {
        if (profile == null)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;
        direction.Normalize();

        float mass = Mathf.Max(motionParam.mass, 0.01f);
        Vector3 horizontalVelocity = direction * (profile.horizontalPower / mass);

        if (Vector3.Dot(velocity, horizontalVelocity) < 0f)
            velocity = Vector3.ProjectOnPlane(velocity, direction);

        velocity += horizontalVelocity;
        verticalVelocity = Mathf.Max(verticalVelocity, Mathf.Max(profile.verticalPower, launchReactionParam.minVerticalPower));
        BeginLaunch();

        if (applyDamage)
        {
            TakeDamage(profile.selfDamage, allowImmediateDestroy: false);
            if (hp <= 0f && statusParam.destroyWhenHPZero)
                pendingDeathAfterLaunch = true;
        }
    }

    private Vector3 ResolveHitDirection(HitEventData data, Vector3 payloadDirection)
    {
        Vector3 direction = payloadDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (data.attackerObject != null)
        {
            direction = transform.position - data.attackerObject.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;
        }

        direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;
        return direction.normalized;
    }

    private void TakeDamage(float damage, bool allowImmediateDestroy)
    {
        if (damage <= 0f)
            return;

        hp -= damage;

        if (debugParam.logHit)
            Debug.Log($"{name} RugbySummonEnemy Damage damage:{damage:F1} hp:{hp:F1}/{statusParam.maxHP:F1}");

        if (hp > 0f)
            return;
        if (!statusParam.destroyWhenHPZero)
            return;

        if (allowImmediateDestroy)
        {
            ForceDestroy();
            return;
        }

        pendingDeathAfterLaunch = true;
    }

    #endregion

    #region Hit Source

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (currentStateName == SummonStateName.Dead)
            return;
        if (selfHitbox == null || other == null)
            return;

        if (IsScrumRush && ContainsHitbox(hitboxParam.scrumAttackHitboxes, selfHitbox.gameObject))
        {
            // スクラム突撃攻撃のダメージはRugbyScrumManager側で一括処理します。
            // 各召喚エネミーのHitboxは「当たった通知」だけを行い、複数Hitbox同時ヒットやジャストタックル優先はManagerで解決します。
            if (RugbyScrumManager.Instance != null)
                RugbyScrumManager.Instance.NotifyScrumAttackHit(this, selfHitbox, other);
            return;
        }

        if (IsCapturedByScrum || IsMovingToScrum)
            return;

        if (ContainsHitbox(hitboxParam.chainHitboxes, selfHitbox.gameObject))
        {
            HandleChainHit(selfHitbox, other);
            return;
        }

        if (ContainsHitbox(hitboxParam.attackHitboxes, selfHitbox.gameObject))
            HandleAttackHit(selfHitbox, other);
    }

    private void HandleAttackHit(Hitbox selfHitbox, Collider other)
    {
        if (currentStateName != SummonStateName.Chase)
            return;

        Hitbox targetHitbox = GetTargetHitbox(other);
        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        IHitReceiver receiver = targetHitbox.receiver;
        MonoBehaviour receiverMono = receiver as MonoBehaviour;
        if (receiverMono == null || receiverMono.gameObject == gameObject)
            return;

        if (hitboxParam.hitSameTargetOncePerChase && hitTargetsThisChase.Contains(receiverMono.gameObject))
            return;

        hitTargetsThisChase.Add(receiverMono.gameObject);

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,
            targetObject = receiverMono.gameObject,
            targetHitbox = targetHitbox.gameObject,
            contactPoint = targetHitbox.transform.position,
            payload = new EnemyAttackPayload
            {
                damage = hitboxParam.playerDamage
            }
        };

        if (debugParam.logAttack)
            Debug.Log($"{name} RugbySummonEnemy AttackHit target:{receiverMono.name} damage:{hitboxParam.playerDamage}");

        receiver.OnHit(data);

        if (hitboxParam.destroyAfterAttackHit)
            ForceDestroy();
    }

    private void HandleChainHit(Hitbox selfHitbox, Collider other)
    {
        if (!CanEmitChain(selfHitbox))
            return;

        if (!TryGetChainReceiver(other, out IHitReceiver receiver, out GameObject receiverObject, out Hitbox targetHitbox))
            return;

        EmitChainTo(receiver, receiverObject, targetHitbox, other, selfHitbox.gameObject);
    }

    private bool CanEmitChain(Hitbox selfHitbox)
    {
        if (selfHitbox == null)
            return false;
        if (!ContainsHitbox(hitboxParam.chainHitboxes, selfHitbox.gameObject))
            return false;
        if (remainingChainCount <= 0)
            return false;
        if (lastChainFrame == Time.frameCount)
            return false;
        if (!IsFlying)
            return false;
        if (!currentCanEmitChain)
            return false;

        float chainSpeed = GetTotalVelocity().magnitude;
        return chainSpeed >= launchReactionParam.minChainSpeed;
    }

    private bool TryGetChainReceiver(Collider other, out IHitReceiver receiver, out GameObject receiverObject, out Hitbox targetHitbox)
    {
        receiver = null;
        receiverObject = null;
        targetHitbox = null;

        if (other == null)
            return false;
        if (other.transform == transform || other.transform.IsChildOf(transform))
            return false;

        targetHitbox = GetTargetHitbox(other);
        if (targetHitbox != null && targetHitbox.receiver != null)
        {
            receiver = targetHitbox.receiver;
            Component component = receiver as Component;
            if (component != null)
                receiverObject = component.gameObject;
        }

        if (receiver == null || receiverObject == null)
        {
            receiver = other.GetComponentInParent<IHitReceiver>();
            Component component = receiver as Component;
            if (component != null)
                receiverObject = component.gameObject;
        }

        if (receiver == null || receiverObject == null)
            return false;
        if (receiverObject == gameObject)
            return false;

        if (ignoreChainWhileCurrentLaunch && IsRugbySummonEnemyObject(receiverObject))
            return false;
        return true;
    }

    private void EmitChainTo(IHitReceiver receiver, GameObject receiverObject, Hitbox targetHitbox, Collider other, GameObject attackerHitbox)
    {
        if (receiver == null || receiverObject == null || receiverObject == gameObject)
            return;
        if (ignoreChainWhileCurrentLaunch && IsRugbySummonEnemyObject(receiverObject))
            return; // EmitChainToSummonGuard

        int chainIndex = 1;
        if (ChainHitManager.Instance != null)
        {
            bool canBeginChain = ChainHitManager.Instance.TryBeginChainPair(
                gameObject,
                receiverObject,
                launchReactionParam.pairCooldown,
                out chainIndex
            );

            if (!canBeginChain)
                return;
        }

        Vector3 directionToTarget = GetChainDirectionTo(receiverObject);
        lastChainFrame = Time.frameCount;
        ConsumeChainCount();
        ApplyChainSelfReaction(directionToTarget);

        ChainPayload payload = new ChainPayload
        {
            source = this,
            chainIndex = chainIndex,
            direction = directionToTarget,
            horizontalPower = launchReactionParam.chainPayloadHorizontalPower,
            verticalPower = launchReactionParam.chainPayloadVerticalPower,
            damage = launchReactionParam.chainPayloadDamage
        };

        HitEventData hit = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = attackerHitbox,
            targetObject = receiverObject,
            targetHitbox = targetHitbox != null ? targetHitbox.gameObject : other.gameObject,
            contactPoint = other != null ? other.ClosestPoint(transform.position) : receiverObject.transform.position,
            payload = payload
        };

        if (debugParam.logChain)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy EmitChain target:{receiverObject.name} chainIndex:{chainIndex} " +
                $"dir:{directionToTarget} selfRemain:{remainingChainCount} state:{currentStateName}"
            );
        }

        receiver.OnHit(hit);

        if (ChainHitManager.Instance != null)
            ChainHitManager.Instance.NotifyChain(chainIndex);

        if (launchReactionParam.destroySelfAfterChainEmit)
            ExplodeAndDestroyAfterChainEmission(receiverObject, chainIndex);
    }

    private void ExplodeAndDestroyAfterChainEmission(GameObject receiverObject, int chainIndex)
    {
        if (currentStateName == SummonStateName.Dead)
            return;

        if (debugParam.logChain)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy ChainSelfExplosion " +
                $"target:{GetObjectName(receiverObject)} chainIndex:{chainIndex} effect:{launchReactionParam.chainSelfExplosionEffectIndex}"
            );
        }

        ApplyAllHitboxesOff();
        EnsureCharacterControllerEnabled(false);
        transform.SetParent(null, worldPositionStays: true);

        if (effectPlayer != null)
            effectPlayer.Play(Mathf.Max(0, launchReactionParam.chainSelfExplosionEffectIndex));

        AudioManager.Instance.PlaySe("RB_RPExplosion");

        currentStateName = SummonStateName.Dead;
        currentState = null;
        reserveState = null;
        reservePriority = int.MinValue;

        Destroy(gameObject);
    }

    private void ApplyChainSelfReaction(Vector3 directionToTarget)
    {
        LaunchReactionProfile profile = launchReactionParam.chain;
        if (profile == null)
            return;

        Vector3 reactionDirection = -directionToTarget;
        reactionDirection.y = 0f;
        if (reactionDirection.sqrMagnitude < 0.001f)
            reactionDirection = -transform.forward;
        reactionDirection.Normalize();

        currentCanEmitChain = profile.canEmitChainAfterThisReaction;
        ApplyLaunchProfile(profile, reactionDirection, applyDamage: true);
        ReserveState(new AirborneState(this), 100);
        ApplyReserveState();
    }

    private Vector3 GetChainDirectionTo(GameObject targetObject)
    {
        Vector3 direction = targetObject.transform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            Vector3 totalVelocity = GetTotalVelocity();
            direction = new Vector3(totalVelocity.x, 0f, totalVelocity.z);
            if (direction.sqrMagnitude < 0.001f)
                direction = transform.forward;
        }

        return direction.normalized;
    }

    private bool IsRugbySummonEnemyObject(GameObject obj)
    {
        if (obj == null)
            return false;

        RugbySummonEnemy enemy = obj.GetComponent<RugbySummonEnemy>();
        if (enemy != null)
            return true;

        enemy = obj.GetComponentInParent<RugbySummonEnemy>();
        return enemy != null;
    }

    private Hitbox GetTargetHitbox(Collider other)
    {
        if (other == null)
            return null;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();
        return targetHitbox;
    }

    #endregion

    #region Chain Count / Launch Runtime

    private void ResetChainCount()
    {
        remainingChainCount = Mathf.Max(0, launchReactionParam.maxChainCount);
        UpdateChainColliderEnabled();
    }

    private void ConsumeChainCount()
    {
        remainingChainCount = Mathf.Max(remainingChainCount - 1, 0);
        UpdateChainColliderEnabled();
    }

    private void UpdateChainColliderEnabled()
    {
        bool active = currentStateName != SummonStateName.Dead &&
                      !IsCapturedByScrum &&
                      !IsMovingToScrum &&
                      currentStateName != SummonStateName.KnockdownRecovery;

        SetHitboxListActive(hitboxParam.chainHitboxes, active);
    }

    private void BeginLaunch()
    {
        ResetContactMemory();
        groundedThisFrame = false;
        airborneGroundedStableTimer = 0f;
        stopTimer = 0f;
        stuckRecoverTimer = 0f;
        lastBounceFrame = -1;
        currentScrumRoot = null;
        ResetChainCount();
    }

    #endregion

    #region Scrum Support

    public void BeginMoveToScrumPoint(Transform scrumRoot, Vector3 localPosition, Quaternion localRotation)
    {
        BeginMoveToScrumPoint(
            scrumRoot,
            localPosition,
            localRotation,
            scrumMoveParam.defaultMoveSpeed,
            scrumMoveParam.defaultArriveDistance
        );
    }

    public void BeginMoveToScrumPoint(
        Transform scrumRoot,
        Vector3 localPosition,
        Quaternion localRotation,
        float moveSpeed,
        float arriveDistance)
    {
        if (currentStateName == SummonStateName.Dead)
            return;

        currentScrumRoot = scrumRoot;
        currentScrumLocalPosition = localPosition;
        currentScrumLocalRotation = localRotation;
        currentScrumMoveSpeed = Mathf.Max(0f, moveSpeed);
        currentScrumArriveDistance = Mathf.Max(0.01f, arriveDistance);

        transform.SetParent(null, worldPositionStays: true);
        ReserveState(new ScrumMoveToPointState(this), int.MaxValue);
        ApplyReserveState();

        if (debugParam.logScrum)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy BeginMoveToScrumPoint " +
                $"target:{GetCurrentScrumWorldPosition()} speed:{currentScrumMoveSpeed:F2} arrive:{currentScrumArriveDistance:F2}"
            );
        }
    }

    public void CaptureIntoScrumIdle(Transform scrumRoot, Vector3 localPosition, Quaternion localRotation)
    {
        if (currentStateName == SummonStateName.Dead)
            return;

        currentScrumRoot = scrumRoot;
        currentScrumLocalPosition = localPosition;
        currentScrumLocalRotation = localRotation;

        if (scrumRoot != null)
        {
            transform.SetParent(scrumRoot, worldPositionStays: false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
        }

        ReserveState(new ScrumIdleState(this), int.MaxValue);
        ApplyReserveState();

        if (debugParam.logScrum)
            Debug.Log($"{name} RugbySummonEnemy CaptureIntoScrumIdle slot:{localPosition}");
    }

    public void BeginScrumRush()
    {
        if (currentStateName == SummonStateName.Dead)
            return;
        if (!IsCapturedByScrum)
            return;

        ReserveState(new ScrumRushState(this), int.MaxValue);
        ApplyReserveState();

        if (debugParam.logScrum)
            Debug.Log($"{name} RugbySummonEnemy BeginScrumRush");
    }

    public void UpdateScrumSlot(Vector3 localPosition, Quaternion localRotation)
    {
        currentScrumLocalPosition = localPosition;
        currentScrumLocalRotation = localRotation;
        ApplyScrumSlotTransform();
    }

    public void SetIgnoreChainForCurrentLaunch(bool ignore)
    {
        // スクラムカウンター吹き飛び中は、召喚エネミー同士の連鎖だけを無視します。
        // ボスへの連鎖は通したいので、chainHitboxes自体は無効化しません。
        ignoreChainWhileCurrentLaunch = ignore;
        UpdateChainColliderEnabled();
    }

    public void ReleaseFromScrumAsLaunch(Vector3 direction, float horizontalPower, float verticalPower)
    {
        if (currentStateName == SummonStateName.Dead)
            return;

        transform.SetParent(null, worldPositionStays: true);
        currentScrumRoot = null;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        velocity = direction * Mathf.Max(0f, horizontalPower);
        verticalVelocity = Mathf.Max(launchReactionParam.minVerticalPower, verticalPower);
        BeginLaunch();
        ReserveState(new AirborneState(this), int.MaxValue);
        ApplyReserveState();

        if (debugParam.logScrum)
        {
            Debug.Log(
                $"{name} RugbySummonEnemy ReleaseFromScrumAsLaunch " +
                $"dir:{direction} horizontal:{horizontalPower:F2} vertical:{verticalPower:F2} " +
                $"IgnoreChain:{ignoreChainWhileCurrentLaunch}"
            );
        }
    }

    public void AbsorbIntoScrum()
    {
        CaptureIntoScrumIdle(null, Vector3.zero, Quaternion.identity);
    }

    private Vector3 GetCurrentScrumWorldPosition()
    {
        if (currentScrumRoot != null)
            return currentScrumRoot.TransformPoint(currentScrumLocalPosition);

        return currentScrumLocalPosition;
    }

    private void ApplyScrumCapturedCommonMode()
    {
        SetVisible(true);
        EnsureCharacterControllerEnabled(false);
    }

    private void ApplyScrumSlotTransform()
    {
        if (currentScrumRoot == null)
            return;

        transform.localPosition = currentScrumLocalPosition;
        transform.localRotation = currentScrumLocalRotation;
    }

    #endregion

    #region Destroy

    public void ForceDestroy()
    {
        if (currentStateName == SummonStateName.Dead)
            return;
        ReserveState(new DeadState(this), int.MaxValue);
        ApplyReserveState();
    }

    private bool ShouldDestroyByLifeLimit()
    {
        if (IsCapturedByScrum || IsMovingToScrum)
            return false;

        if (statusParam.lifeTime > 0f && lifeTimer >= statusParam.lifeTime)
        {
            if (debugParam.logState)
                Debug.LogWarning($"{name} RugbySummonEnemy DestroyReason: LifeTime state:{currentStateName}");
            return true;
        }

        if (statusParam.maxMoveDistanceFromSpawn > 0f)
        {
            float distance = Vector3.Distance(spawnPosition, transform.position);
            if (distance >= statusParam.maxMoveDistanceFromSpawn)
            {
                if (debugParam.logState)
                    Debug.LogWarning($"{name} RugbySummonEnemy DestroyReason: Distance distance:{distance:F2}");
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Animation

    private bool CanUseAnimationFinish(string stateName)
    {
        return animationParam.useAnimator &&
               animationParam.animator != null &&
               !string.IsNullOrEmpty(stateName);
    }

    private void CrossFadeAnimation(string stateName, float fadeTime)
    {
        if (!animationParam.useAnimator || animationParam.animator == null || string.IsNullOrEmpty(stateName))
            return;

        animationParam.animator.CrossFadeInFixedTime(
            stateName,
            Mathf.Max(0f, fadeTime),
            animationParam.layerIndex,
            0f
        );
    }

    private bool IsAnimationFinished(string stateName, float finishNormalizedTime, bool waitTransitionComplete)
    {
        if (!CanUseAnimationFinish(stateName))
            return false;

        if (waitTransitionComplete && animationParam.animator.IsInTransition(animationParam.layerIndex))
            return false;

        AnimatorStateInfo info = animationParam.animator.GetCurrentAnimatorStateInfo(animationParam.layerIndex);
        if (!IsAnimatorState(info, stateName))
            return false;

        return info.normalizedTime >= finishNormalizedTime;
    }

    private bool IsAnimatorState(AnimatorStateInfo info, string stateName)
    {
        if (info.IsName(stateName))
            return true;

        int exactHash = Animator.StringToHash(stateName);
        if (info.shortNameHash == exactHash || info.fullPathHash == exactHash)
            return true;

        int lastDot = stateName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < stateName.Length - 1)
        {
            string shortName = stateName.Substring(lastDot + 1);
            int shortHash = Animator.StringToHash(shortName);
            if (info.shortNameHash == shortHash)
                return true;
        }

        return false;
    }

    private void SyncAnimatorSpeed()
    {
        if (!animationParam.useAnimator || !animationParam.syncAnimatorSpeedWithTimeAgent || animationParam.animator == null)
            return;

        animationParam.animator.speed = TimeScale;
    }

    #endregion

    #region Animation Effect

    public void AnimEvent_RPunch()
    {
        //effectPlayer.PlayAt(5, chaseParam.RChaseAttack.position);
        effectPlayer.Play(5, chaseParam.RChaseAttack);
    }
    public void AnimEvent_LPunch()
    {
        //effectPlayer.PlayAt(5, chaseParam.LChaseAttack.position);
        effectPlayer.Play(5, chaseParam.LChaseAttack);

    }

    #endregion

    #region Utility

    private void EnsureCharacterControllerEnabled(bool enabled)
    {
        if (characterController == null)
            return;
        if (characterController.enabled == enabled)
            return;
        characterController.enabled = enabled;
    }

    private void SetCharacterControllerExcludeLayers(LayerMask mask)
    {
        if (characterController == null)
            return;
        characterController.excludeLayers = mask;
    }

    private void SetVisible(bool visible)
    {
        if (cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;
            renderer.enabled = visible;
        }
    }

    private bool IsScrumReceiveHitbox(GameObject target)
    {
        if (hitboxParam.scrumReceiveHitboxes != null && hitboxParam.scrumReceiveHitboxes.Count > 0)
            return ContainsHitbox(hitboxParam.scrumReceiveHitboxes, target);

        return ContainsHitbox(hitboxParam.scrumAttackHitboxes, target);
    }

    private bool ContainsHitbox(List<GameObject> hitboxes, GameObject target)
    {
        if (hitboxes == null || target == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject root = hitboxes[i];
            if (root == null)
                continue;
            if (target == root)
                return true;
            if (target.transform.IsChildOf(root.transform))
                return true;
            if (root.transform.IsChildOf(target.transform))
                return true;
        }

        return false;
    }

    private string GetObjectName(GameObject obj)
    {
        return obj != null ? obj.name : "null";
    }

    #endregion

    #region Debug GUI

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!debugParam.drawDebugGUI)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(transform.position + debugParam.guiOffset);
        if (screen.z <= 0f)
            return;

        Rect rect = new Rect(screen.x - 150f, Screen.height - screen.y - 18f, 370f, 160f);
        GUI.Label(
            rect,
            $"RugbySummonEnemy: {currentStateName}\n" +
            $"CanJoinScrum:{CanJoinScrumCommand} ScrumRush:{IsScrumRush}\n" +
            $"HP:{hp:F0}/{statusParam.maxHP:F0} PendingDeath:{pendingDeathAfterLaunch}\n" +
            $"Chain:{remainingChainCount} Emit:{currentCanEmitChain}\n" +
            $"Velocity:{velocity.magnitude:F1} Vertical:{verticalVelocity:F1}\n" +
            $"TimeScale:{TimeScale:F2}"
        );
#endif
    }

    #endregion
}
