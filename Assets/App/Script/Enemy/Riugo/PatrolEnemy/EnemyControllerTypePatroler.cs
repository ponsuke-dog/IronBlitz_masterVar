using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(EnemyNavMotor))]
[RequireComponent(typeof(EnemyVisionSensor))]
public class EnemyControllerTypePatroler : MonoBehaviour, IHitSource, IHitReceiver
{
    [System.Serializable]
    private class EnemyParam
    {
        [Header("Group")]
        public EnemyData enemyData;

        [Header("Status")]
        public float maxHP = 10f;

        [Header("Move Speed")]
        public float patrolSpeed = 3.5f;
        public float chaseSpeed = 5.0f;

        [Header("Lost Sight")]
        public float lostSightDuration = 1.5f;
        public float loseSightGraceTime = 0.2f;

        [Header("Battle")]
        public float attackRange = 2.0f;
        public float attackStartRange = 2.8f;
        public float attackInterval = 1.2f;
        public float attackRecoveryDuration = 0.35f;
        public int attackDamage = 1;

        [Header("Death")]
        [Tooltip("死亡演出後、Destroyするまでの秒数")]
        public float deathDestroyDelay = 1.2f;
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

        [Tooltip("Finish Mode が AnimationEnd の時、この再生割合で終了扱いにする。0.95なら95%再生で終了")]
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
    private class ChargeParam
    {
        [Header("Charge")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();

        public bool facePlayerWhileCharge = true;

        [Tooltip("Charge終了時に攻撃距離外ならChaseへ戻る")]
        public bool cancelAttackIfPlayerOutOfRange = true;
    }

    [System.Serializable]
    private class AttackAnimationParam
    {
        [Header("Attack Animation Finish")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();
    }

    [System.Serializable]
    private class AttackCooldownParam
    {
        [Header("Attack Cooldown")]
        public StateAnimationFinishParam animation = new StateAnimationFinishParam();

        [Tooltip("クールダウン終了後、視界にプレイヤーがいればFoundを挟む")]
        public bool useFoundAfterCooldownIfVisible = true;
    }

    [System.Serializable]
    private class PatrolRecoveryParam
    {
        [Header("Return Route Search")]
        [Tooltip("Return開始時にHomeRouteを最優先で試す")]
        public bool preferHomeRouteOnReturn = true;

        [Tooltip("Fallbackルートに到達したら、そのルートを新しいHomeRouteにする")]
        public bool adoptFallbackRouteAsHome = true;

        [Header("Return Progress Check")]
        [Tooltip("この秒数ごとにReturn対象との距離が縮んでいるか確認する")]
        public float routeDistanceCheckInterval = 0.4f;

        [Tooltip("確認間隔ごとにこの距離以上縮んでいれば進行中とみなす")]
        public float minRouteDistanceDecrease = 0.12f;

        [Tooltip("この秒数以上、対象との距離が縮まらなければ次ルートへ切り替える")]
        public float routeNoProgressDuration = 1.2f;

        [Header("Return Finish")]
        [Tooltip("ReturnToPatrol中に帰還完了とみなす距離")]
        public float returnReachDistance = 0.45f;

        [Tooltip("Return開始直後、この秒数は到達扱いにしない")]
        public float minReturnStateDuration = 0.25f;

        [Header("Debug")]
        public bool logReturnRoute = true;
    }

    [System.Serializable]
    private class KnockdownParam
    {
        [Header("Knockdown")]
        public float layDownMinDuration = 0.2f;
        public float getUpStartSpeedThreshold = 0.01f;
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
    private class AttackHitParam
    {
        [Header("Target Layer")]
        public LayerMask targetLayer;

        [Header("Debug")]
        public bool logAttack = false;
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
    private class PunchObjectSet
    {
        [Header("Attack Objects")]
        public GameObject rightPunchObject;
        public GameObject leftPunchObject;
    }

    [System.Serializable]
    private class ExcludeLayerStateParam
    {
        [Header("CharacterController Exclude Layers")]
        public LayerMask patrolExcludeLayers;
        public LayerMask chaseExcludeLayers;
        public LayerMask lostSightChaseExcludeLayers;
        public LayerMask attackExcludeLayers;
        public LayerMask attackRecoveryExcludeLayers;
        public LayerMask returnToPatrolExcludeLayers;
        public LayerMask launchExcludeLayers;
        public LayerMask knockdownExcludeLayers;
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

    [SerializeField] private ChainParam chainParam = new ChainParam();

    [SerializeField] private EnemyParam enemyParam = new EnemyParam();
    [SerializeField] private PatrolRecoveryParam patrolRecoveryParam = new PatrolRecoveryParam();
    [SerializeField] private KnockdownParam knockdownParam = new KnockdownParam();
    [SerializeField] private ForcedGroundRecoveryParam forcedGroundRecoveryParam = new ForcedGroundRecoveryParam();
    [SerializeField] private FoundParam foundParam = new FoundParam();
    [SerializeField] private ChargeParam chargeParam = new ChargeParam();
    [SerializeField] private AttackAnimationParam attackAnimationParam = new AttackAnimationParam();
    [SerializeField] private AttackCooldownParam attackCooldownParam = new AttackCooldownParam();

    [Header("Attack Hit")]
    [SerializeField] private AttackHitParam attackHitParam = new AttackHitParam();
    [SerializeField] private PunchObjectSet punchObjects = new PunchObjectSet();

    [Header("Surface Hit Notify")]
    [SerializeField] private SurfaceHitParam surfaceHitParam = new SurfaceHitParam();

    [Header("CharacterController Layer Override")]
    [SerializeField] private ExcludeLayerStateParam excludeLayerStateParam = new ExcludeLayerStateParam();

    [Header("Receive Blow")]
    [SerializeField] private bool logReceivedDamage = false;

    [Header("HitBoxes")]
    [SerializeField] private GameObject receiveHitBox;
    [SerializeField] private GameObject bounceHitBox;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    [Header("Route Settings")]
    [SerializeField] private EnemyPatrolRoute homeRoute;
    [SerializeField] private EnemyPatrolRoute currentRoute;
    [SerializeField] private EnemyPatrolRouteManager routeManager;

    [Header("Vision Debug")]
    [SerializeField] private EnemyVisionDebugRenderer visionDebugRenderer;

    [Header("Animation")]
    [SerializeField] private EnemyAnimatorDriver animDriver;

    [Header("Attack")]
    [SerializeField] private EnemyAttackLungeMotor attackLungeMotor;

    [Header("State Debug")]
    [SerializeField] private bool drawStateLabel = true;
    [SerializeField] private Vector3 stateLabelOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Death Effect")]
    [SerializeField] private DeathEffectParam deathEffectParam = new DeathEffectParam();

    private EnemyNavMotor motor;
    private EnemyVisionSensor sensor;
    private EffectPlayer effectPlayer;
    private AudioManager audioManager;

    private float hp;
    private float lostSightTimer;
    private float loseSightGraceTimer;
    private Vector3 lastKnownPlayerPosition;
    private int patrolIndex;

    private readonly List<EnemyPatrolRoute> returnRejectedRoutes = new List<EnemyPatrolRoute>();
    private readonly List<EnemyPatrolRoute> returnCandidateRoutes = new List<EnemyPatrolRoute>();

    private EnemyPatrolRoute lastRejectedReturnRoute;
    private bool pendingAdoptCurrentRouteAsHome;
    private EnemyPatrolRoute initialHomeRoute;

    private float returnStateElapsedTime;
    private float routeDistanceCheckTimer;
    private float routeNoProgressTimer;
    private float lastReturnDistanceToTarget = -1f;

    private bool pendingKnockdownRecovery;
    private float groundedRecoveryTimer = 0f;

    private int remainingChainCount;
    private int lastChainFrame = -1;

    private bool foundAnimationFinished;
    private bool chargeAnimationFinished;

    private bool attackCooldownRequested;

    private bool attackWindowOpen;
    private bool attackSequenceFinished;
    private readonly HashSet<GameObject> activeAttackObjects = new HashSet<GameObject>();
    private readonly HashSet<GameObject> hitTargetsThisAttack = new HashSet<GameObject>();

    private State nowState;
    private string currentStateName = "None";

    #region ステート

    private abstract class State
    {
        protected EnemyControllerTypePatroler enemy;

        public void Set(EnemyControllerTypePatroler e)
        {
            enemy = e;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }

    private class Patrol : State
    {
        public override void Enter()
        {
            enemy.ResetChaseTimers();
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.patrolExcludeLayers);

            if (enemy.animDriver != null)
                enemy.animDriver.ReturnToLocomotion();

            if (enemy.HasCurrentRoute())
            {
                enemy.patrolIndex = Mathf.Clamp(enemy.patrolIndex, 0, enemy.currentRoute.Count - 1);
                enemy.motor.RequestMove(enemy.GetCurrentPatrolPoint(), enemy.enemyParam.patrolSpeed);
            }
            else
            {
                enemy.motor.StopPlanarVelocity();
            }
        }

        public override void Update()
        {
            if (enemy.TryDetectPlayer())
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.ChangeState(new Found());
                return;
            }

            if (!enemy.HasCurrentRoute())
            {
                enemy.motor.StopPlanarVelocity();
                return;
            }

            Vector3 target = enemy.GetCurrentPatrolPoint();
            enemy.motor.RequestMove(target, enemy.enemyParam.patrolSpeed);

            if (enemy.motor.IsReached(target))
                enemy.patrolIndex = enemy.currentRoute.GetNextIndex(enemy.patrolIndex);
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }

    private class Found : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;
            enemy.foundAnimationFinished = false;

            enemy.ResetChaseTimers();
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chaseExcludeLayers);

            enemy.motor.StopPlanarVelocity();
            enemy.motor.ResetPath();


            if (enemy.effectPlayer != null)
                enemy.effectPlayer.Play(3);

            if (enemy.playerTarget != null)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.FaceTo(enemy.playerTarget.position);
            }

            if (enemy.animDriver != null)
                enemy.animDriver.PlayFound();
        }

        public override void Update()
        {
            timer += Time.deltaTime * enemy.motor.TimeScale;

            if (enemy.playerTarget != null && enemy.foundParam.facePlayerWhileFound)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.FaceTo(enemy.playerTarget.position);
            }

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

            if (enemy.playerTarget != null && enemy.TryDetectPlayer())
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.ChangeState(new Chase());
            }
            else
            {
                enemy.ChangeState(new ReturnToPatrol());
            }
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }
    private class Chase : State
    {
        public override void Enter()
        {
            enemy.loseSightGraceTimer = 0f;
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.chaseExcludeLayers);

            // 追加：Found/Charge/Attack系から通常移動アニメへ戻す
            if (enemy.animDriver != null)
                enemy.animDriver.ReturnToLocomotion();

            if (enemy.playerTarget != null)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.motor.RequestMove(enemy.playerTarget.position, enemy.enemyParam.chaseSpeed);
            }
        }

        public override void Update()
        {
            if (enemy.playerTarget == null)
            {
                enemy.ChangeState(new ReturnToPatrol());
                return;
            }

            bool canSeePlayer = enemy.TryDetectPlayer();

            if (canSeePlayer)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.loseSightGraceTimer = 0f;

                float dist = enemy.GetDistanceToPlayer();
                if (dist <= enemy.enemyParam.attackStartRange)
                {
                    enemy.ChangeState(new Charge());
                    return;
                }

                enemy.motor.RequestMove(enemy.playerTarget.position, enemy.enemyParam.chaseSpeed);
                return;
            }

            enemy.loseSightGraceTimer += Time.deltaTime * enemy.motor.TimeScale;

            if (enemy.loseSightGraceTimer < enemy.enemyParam.loseSightGraceTime)
            {
                enemy.motor.RequestMove(enemy.lastKnownPlayerPosition, enemy.enemyParam.chaseSpeed);
                return;
            }

            enemy.ChangeState(new LostSightChase());
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }

    private class LostSightChase : State
    {
        public override void Enter()
        {
            enemy.lostSightTimer = 0f;
            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.lostSightChaseExcludeLayers);
        }

        public override void Update()
        {
            if (enemy.TryDetectPlayer())
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.ChangeState(new Chase());
                return;
            }

            enemy.lostSightTimer += Time.deltaTime * enemy.motor.TimeScale;
            enemy.motor.RequestMove(enemy.lastKnownPlayerPosition, enemy.enemyParam.chaseSpeed);

            if (enemy.motor.IsReached(enemy.lastKnownPlayerPosition))
                enemy.motor.StopPlanarVelocity();

            if (enemy.lostSightTimer >= enemy.enemyParam.lostSightDuration)
                enemy.ChangeState(new ReturnToPatrol());
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }

    private class Charge : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;
            enemy.chargeAnimationFinished = false;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.attackExcludeLayers);

            enemy.motor.StopPlanarVelocity();
            enemy.motor.ResetPath();
            enemy.ForceEndAttackSequence();

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.playerTarget != null)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.FaceTo(enemy.playerTarget.position);
            }

            if (enemy.animDriver != null)
                enemy.animDriver.PlayCharge();
        }

        public override void Update()
        {
            timer += Time.deltaTime * enemy.motor.TimeScale;

            if (enemy.playerTarget != null && enemy.chargeParam.facePlayerWhileCharge)
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.FaceTo(enemy.playerTarget.position);
            }

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.ChargeStateName
                : "";

            bool finished = enemy.IsStateAnimationFinished(
                enemy.chargeParam.animation,
                timer,
                enemy.chargeAnimationFinished,
                stateName
            );

            if (!finished)
                return;

            if (enemy.playerTarget == null)
            {
                enemy.ChangeState(new ReturnToPatrol());
                return;
            }

            if (enemy.chargeParam.cancelAttackIfPlayerOutOfRange)
            {
                float dist = enemy.GetDistanceToPlayer();

                if (dist > enemy.enemyParam.attackStartRange)
                {
                    enemy.ChangeState(new Chase());
                    return;
                }
            }

            enemy.ChangeState(new Attack());
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }
    private class Attack : State
    {
        public override void Enter()
        {
            enemy.attackCooldownRequested = false;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.attackExcludeLayers);

            enemy.motor.StopPlanarVelocity();
            enemy.motor.ResetPath();

            enemy.ForceEndAttackSequence();

            enemy.audioManager.PlaySe("PE_Punch", enemy.deathEffectParam.effectPoint.position);

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.effectPlayer != null)
                enemy.effectPlayer.Play(4);

            Vector3 targetPosition = enemy.playerTarget != null
                ? enemy.playerTarget.position
                : enemy.lastKnownPlayerPosition;

            targetPosition.y = enemy.transform.position.y;

            Vector3 dir = targetPosition - enemy.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
                dir = enemy.transform.forward;

            dir.Normalize();

            enemy.FaceTo(enemy.transform.position + dir);

            // 攻撃判定の準備だけ行う
            enemy.StartAttackSequence();

            if (enemy.animDriver != null)
                enemy.animDriver.PlayAttack();

            // Attack開始時点のプレイヤー位置方向へ固定Lunge
            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.BeginLungeFixedDirection(dir);
        }

        public override void Update()
        {
            if (enemy.attackCooldownRequested)
            {
                enemy.ChangeState(new AttackCooldown());
                return;
            }

            if (enemy.attackLungeMotor != null &&
                enemy.attackLungeMotor.IsLungeFinished)
            {
                enemy.ChangeState(new AttackCooldown());
                return;
            }
        }

        public override void Exit()
        {
            enemy.ForceEndAttackSequence();

            enemy.audioManager.StopSe("PE_Punch");

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            enemy.attackCooldownRequested = false;
        }
    }

    private class AttackCooldown : State
    {
        private float timer;

        public override void Enter()
        {
            timer = 0f;

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.attackRecoveryExcludeLayers);

            enemy.motor.StopPlanarVelocity();
            enemy.motor.ResetPath();

            enemy.ForceEndAttackSequence();

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.animDriver != null)
                enemy.animDriver.PlayAttackCooldown();
        }

        public override void Update()
        {
            timer += Time.deltaTime * enemy.motor.TimeScale;

            string stateName = enemy.animDriver != null
                ? enemy.animDriver.AttackCooldownStateName
                : "";

            bool finished = enemy.IsStateAnimationFinished(
                enemy.attackCooldownParam.animation,
                timer,
                false,
                stateName
            );

            if (!finished)
                return;

            if (enemy.playerTarget != null && enemy.TryDetectPlayer())
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.ChangeState(new Chase());
            }
            else
            {
                enemy.ChangeState(new LostSightChase());
            }
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
        }
    }

    private class ReturnToPatrol : State
    {
        public override void Enter()
        {
            enemy.ResetChaseTimers();
            enemy.ResetReturnRecoveryState();

            enemy.ResolveInitialReturnRoute();

            enemy.ApplyNormalHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.returnToPatrolExcludeLayers);

            if (enemy.animDriver != null)
                enemy.animDriver.ReturnToLocomotion();

            if (enemy.HasCurrentRoute())
            {
                enemy.patrolIndex = Mathf.Clamp(enemy.patrolIndex, 0, enemy.currentRoute.Count - 1);

                Vector3 target = enemy.currentRoute.GetPoint(enemy.patrolIndex);
                enemy.motor.RequestMove(target, enemy.enemyParam.patrolSpeed);
            }
            else
            {
                enemy.motor.StopPlanarVelocity();
            }
        }

        public override void Update()
        {
            if (enemy.TryDetectPlayer())
            {
                enemy.lastKnownPlayerPosition = enemy.playerTarget.position;
                enemy.ChangeState(new Chase());
                return;
            }

            if (!enemy.HasCurrentRoute())
            {
                enemy.motor.StopPlanarVelocity();
                return;
            }

            Vector3 target = enemy.GetCurrentPatrolPoint();
            enemy.motor.RequestMove(target, enemy.enemyParam.patrolSpeed);

            enemy.returnStateElapsedTime += Time.deltaTime * enemy.motor.TimeScale;

            float returnDistance = enemy.GetPlanarDistance(enemy.transform.position, target);

            bool canFinishReturn =
                enemy.returnStateElapsedTime >= enemy.patrolRecoveryParam.minReturnStateDuration;

            if (canFinishReturn &&
                returnDistance <= enemy.patrolRecoveryParam.returnReachDistance)
            {
                if (enemy.patrolRecoveryParam.logReturnRoute)
                {
                    Debug.Log(
                        $"{enemy.name} ReturnToPatrol reached " +
                        $"route:{enemy.currentRoute.name} index:{enemy.patrolIndex} " +
                        $"distance:{returnDistance:F2} adoptHome:{enemy.pendingAdoptCurrentRouteAsHome}"
                    );
                }

                if (enemy.pendingAdoptCurrentRouteAsHome &&
                    enemy.currentRoute != null &&
                    enemy.patrolRecoveryParam.adoptFallbackRouteAsHome)
                {
                    enemy.homeRoute = enemy.currentRoute;

                    if (enemy.patrolRecoveryParam.logReturnRoute)
                        Debug.Log($"{enemy.name} HomeRoute updated to {enemy.homeRoute.name}");
                }

                enemy.pendingAdoptCurrentRouteAsHome = false;

                enemy.ResetReturnRecoveryState();
                enemy.ChangeState(new Patrol());
                return;
            }

            enemy.UpdateReturnRecoveryProgress(target);
        }

        public override void Exit()
        {
            enemy.motor.StopPlanarVelocity();
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

            enemy.ForceEndAttackSequence();

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.animDriver != null)
                enemy.animDriver.PlayLaunch();
        }

        public override void Update()
        {
            failSafeTimer += Time.deltaTime * enemy.motor.TimeScale;

            if (!enemy.pendingKnockdownRecovery)
                return;

            if (enemy.motor.HasLandedOnce || (!enemy.motor.IsFlying && failSafeTimer > 0.05f))
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

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.animDriver != null)
                enemy.animDriver.EnterLayDown();
        }

        public override void Update()
        {
            if (!enteredGetUp)
            {
                layDownTimer += Time.deltaTime * enemy.motor.TimeScale;

                if (layDownTimer < enemy.knockdownParam.layDownMinDuration)
                    return;

                if (!enemy.motor.IsFlying &&
                    enemy.motor.CurrentPlanarSpeed <= enemy.knockdownParam.getUpStartSpeedThreshold)
                {
                    enteredGetUp = true;

                    if (enemy.animDriver != null)
                        enemy.animDriver.BeginGetUp();

                    enemy.ResetChainCount();
                }

                return;
            }

            if (enemy.animDriver == null || enemy.animDriver.IsGetUpFinished())
            {
                if (enemy.animDriver != null)
                    enemy.animDriver.ForceIdle();


                enemy.ChangeState(new ReturnToPatrol());
            }
        }
    }

    private class Death : State
    {
        private bool hasStopped;
        private bool deathEffectPlayed;
        private float deathTimer;
        private bool soundOnce;

        public override void Enter()
        {
            deathTimer = 0f;
            hasStopped = false;
            deathEffectPlayed = false;
            soundOnce = false;

            enemy.ApplyKnockdownHitMode();
            enemy.ApplyStateExcludeLayers(enemy.excludeLayerStateParam.knockdownExcludeLayers);

            if (enemy.attackLungeMotor != null)
                enemy.attackLungeMotor.ForceStop();

            if (enemy.animDriver != null)
                enemy.animDriver.EnterLayDown();
        }

        public override void Update()
        {
            float dt = Time.deltaTime * enemy.motor.TimeScale;

            if (!hasStopped)
            {
                if (!enemy.motor.IsFlying &&
                    enemy.motor.CurrentPlanarSpeed <= enemy.knockdownParam.getUpStartSpeedThreshold)
                {
                    hasStopped = true;

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
    }

    #endregion

    private void Awake()
    {
        motor = GetComponent<EnemyNavMotor>();
        sensor = GetComponent<EnemyVisionSensor>();
        effectPlayer = GetComponent<EffectPlayer>();

        initialHomeRoute = homeRoute;

        if (animDriver == null)
            animDriver = GetComponentInChildren<EnemyAnimatorDriver>(true);

        hp = enemyParam.maxHP;

        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTarget = p.transform;
        }

        if (currentRoute == null)
            currentRoute = homeRoute;

        if (visionDebugRenderer != null && sensor != null)
            visionDebugRenderer.Initialize(transform, sensor.EyePoint);

        if (animDriver != null)
            animDriver.Initialize(motor);

        DisableAllAttackObjects();

        if (attackLungeMotor != null)
            attackLungeMotor.OnLungeFinished += HandleLungeFinished;

        ResetChainCount();

        motor.OnFirstGroundedAfterLaunch += HandleFirstGroundedAfterLaunch;
        motor.OnReflectionSelfDamage += HandleReflectionSelfDamage;

    }

    private void Start()
    {
        motor.TrySnapToNavMesh(2.0f);

        audioManager = AudioManager.Instance;

        if (currentRoute == null && homeRoute != null)
            currentRoute = homeRoute;

        if (HasCurrentRoute())
            patrolIndex = currentRoute.FindNearestIndex(transform.position);

        ChangeState(new Patrol());

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.AddTotalEnemy();
        }
    }

    public void InitializeFromSpawner(
    EnemyPatrolRoute route,
    EnemyPatrolRouteManager manager,
    Transform target = null,
    bool restartPatrol = true)
    {
        routeManager = manager;

        if (route != null)
        {
            homeRoute = route;
            currentRoute = route;
            initialHomeRoute = route;
        }

        if (target != null)
            playerTarget = target;

        if (HasCurrentRoute())
            patrolIndex = currentRoute.FindNearestIndex(transform.position);

        ResetReturnRecoveryState();

        if (motor != null)
            motor.ResetPath();

        // すでに吹き飛び中なら、Spawner初期化で状態を上書きしない
        if (motor != null && motor.IsFlying)
            return;

        // Start後に呼ばれた場合の保険
        if (restartPatrol && nowState != null)
        {
            ChangeState(new Patrol());
        }
    }

    private void OnDestroy()
    {
        if (motor != null)
        {
            motor.OnFirstGroundedAfterLaunch -= HandleFirstGroundedAfterLaunch;
            motor.OnReflectionSelfDamage -= HandleReflectionSelfDamage;
        }

        if (attackLungeMotor != null)
            attackLungeMotor.OnLungeFinished -= HandleLungeFinished;
    }

    private void Update()
    {
        if (motor.TimeScale <= 0f)
            return;

        float dt = Time.deltaTime * motor.TimeScale;

        motor.TickBegin(dt);

        if (!motor.IsFlying || nowState is LaunchAirborne || nowState is KnockdownRecovery)
            nowState?.Update();

        if (attackLungeMotor != null)
            attackLungeMotor.Tick(dt);

        motor.TickEnd(dt);

        UpdateForcedGroundRecovery(dt);

        if (animDriver != null)
        {
            bool allowMoveAnimation =
                !motor.IsFlying &&
                !(nowState is LaunchAirborne) &&
                !(nowState is KnockdownRecovery) &&
                !(nowState is Found) &&
                !(nowState is Charge) &&
                !(nowState is Attack) &&
                !(nowState is AttackCooldown);

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

    private void HandleFirstGroundedAfterLaunch()
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

    private void ChangeState(State next)
    {
        nowState?.Exit();

        groundedRecoveryTimer = 0f;

        nowState = next;
        nowState.Set(this);
        nowState.Enter();

        currentStateName = next.GetType().Name;

        if (sensor != null)
            sensor.SetChasing(IsBattleVisionState());
    }

    private void UpdateForcedGroundRecovery(float dt)
    {
        if (!forcedGroundRecoveryParam.enable)
            return;

        if (motor == null)
            return;

        if (!(nowState is LaunchAirborne))
        {
            groundedRecoveryTimer = 0f;
            return;
        }

        bool groundedLike =
            motor.IsGrounded &&
            motor.CurrentPlanarSpeed <= forcedGroundRecoveryParam.groundedPlanarSpeedThreshold &&
            motor.CurrentTotalVelocity.y <= forcedGroundRecoveryParam.groundedVerticalVelocityThreshold;

        if (!groundedLike)
        {
            groundedRecoveryTimer = 0f;
            return;
        }

        groundedRecoveryTimer += dt;

        if (groundedRecoveryTimer < forcedGroundRecoveryParam.groundedRecoveryDelay)
            return;

        motor.ForceEndFlying();

        pendingKnockdownRecovery = false;
        ChangeState(new KnockdownRecovery());
        groundedRecoveryTimer = 0f;
    }

    private void ApplyNormalHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(true);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(false);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(true);

        motor.ApplyNormalContactMode();
    }

    private void ApplyLaunchHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(false);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(true);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(true);

        motor.ApplyLaunchKnockdownContactMode();
    }

    private void ApplyKnockdownHitMode()
    {
        if (receiveHitBox != null)
            receiveHitBox.SetActive(false);

        if (bounceHitBox != null)
            bounceHitBox.SetActive(true);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(false);

        motor.ApplyLaunchKnockdownContactMode();
    }

    private void ApplyStateExcludeLayers(LayerMask mask)
    {
        if (motor != null)
            motor.SetCharacterControllerExcludeLayers(mask);
    }

    private void ResetChaseTimers()
    {
        lostSightTimer = 0f;
        loseSightGraceTimer = 0f;
    }

    private void ResetReturnRecoveryState()
    {
        returnRejectedRoutes.Clear();
        returnCandidateRoutes.Clear();

        lastRejectedReturnRoute = null;

        pendingAdoptCurrentRouteAsHome = false;

        returnStateElapsedTime = 0f;
        routeDistanceCheckTimer = 0f;
        routeNoProgressTimer = 0f;
        lastReturnDistanceToTarget = -1f;
    }

    private void ResetReturnProgressTimersOnly()
    {
        returnStateElapsedTime = 0f;
        routeDistanceCheckTimer = 0f;
        routeNoProgressTimer = 0f;
        lastReturnDistanceToTarget = -1f;
    }

    private bool TryDetectPlayer()
    {
        if (sensor == null || playerTarget == null)
            return false;

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
    private float GetDistanceToPlayer()
    {
        if (playerTarget == null)
            return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = playerTarget.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void ResolveInitialReturnRoute()
    {
        LogReturnRoute(
            $"ResolveInitialReturnRoute " +
            $"initialHome:{(initialHomeRoute != null ? initialHomeRoute.name : "null")} " +
            $"runtimeHome:{(homeRoute != null ? homeRoute.name : "null")} " +
            $"current:{(currentRoute != null ? currentRoute.name : "null")}"
        );

        // Return開始時は、現在のHomeRouteを最優先で試す
        // homeRouteがnullの場合のみinitialHomeRouteを使う
        if (patrolRecoveryParam.preferHomeRouteOnReturn &&
            TryStartReturnToHomeRoute())
        {
            return;
        }

        if (TryStartNextReturnCandidate())
            return;

        LogReturnRoute("Initial failed: no route found");

        if (currentRoute != null && currentRoute.HasPoints)
            patrolIndex = currentRoute.FindNearestIndex(transform.position);
    }

    private bool TryStartReturnToHomeRoute()
    {
        bool hasRuntimeHome =
            homeRoute != null &&
            homeRoute.HasPoints;

        bool hasInitialHome =
            initialHomeRoute != null &&
            initialHomeRoute.HasPoints;

        bool runtimeHomeIsDifferent =
            hasRuntimeHome &&
            hasInitialHome &&
            homeRoute != initialHomeRoute;

        // 1. HomeRoute が InitialHomeRoute と違う場合、
        //    まず更新後 HomeRoute を「到達可能判定あり」で試す
        if (runtimeHomeIsDifferent)
        {
            if (TryStartReturnToRoute(
                homeRoute,
                adoptAsHomeOnReach: false,
                reason: "Runtime HOME"))
            {
                return true;
            }

            if (!returnRejectedRoutes.Contains(homeRoute))
                returnRejectedRoutes.Add(homeRoute);

            LogReturnRoute($"Runtime HOME rejected route:{homeRoute.name}");
        }

        // 2. 次に InitialHomeRoute を一度試す
        //    ここは「まず戻ろうとする」ため、CanReachで弾かずForceStartにする
        EnemyPatrolRoute fallbackHomeRoute = hasInitialHome
            ? initialHomeRoute
            : homeRoute;

        if (fallbackHomeRoute == null)
        {
            LogReturnRoute("HOME failed: homeRoute and initialHomeRoute are null");
            return false;
        }

        if (!fallbackHomeRoute.HasPoints)
        {
            LogReturnRoute($"HOME failed: route has no points route:{fallbackHomeRoute.name}");
            return false;
        }

        return ForceStartReturnToRoute(
            fallbackHomeRoute,
            adoptAsHomeOnReach: fallbackHomeRoute != homeRoute,
            reason: "Initial HOME ForceStart");
    }

    private bool ForceStartReturnToRoute(
    EnemyPatrolRoute route,
    bool adoptAsHomeOnReach,
    string reason)
    {
        if (route == null)
        {
            LogReturnRoute($"{reason} failed: route is null");
            return false;
        }

        if (!route.HasPoints)
        {
            LogReturnRoute($"{reason} failed: route has no points route:{route.name}");
            return false;
        }

        int nearestIndex = route.FindNearestIndex(transform.position);
        Vector3 point = route.GetPoint(nearestIndex);

        currentRoute = route;
        patrolIndex = nearestIndex;
        pendingAdoptCurrentRouteAsHome = adoptAsHomeOnReach;

        motor.ResetPath();
        ResetReturnProgressTimersOnly();

        float directDistance = GetPlanarDistance(transform.position, point);
        float pathLength = motor.GetPathLength(point);

        LogReturnRoute(
            $"{reason} " +
            $"route:{route.name} index:{patrolIndex} " +
            $"adoptHome:{pendingAdoptCurrentRouteAsHome} " +
            $"direct:{directDistance:F2} path:{pathLength:F2} " +
            $"runtimeHome:{(homeRoute != null ? homeRoute.name : "null")} " +
            $"initialHome:{(initialHomeRoute != null ? initialHomeRoute.name : "null")}"
        );

        return true;
    }

    private bool TryStartReturnToRoute(
    EnemyPatrolRoute route,
    bool adoptAsHomeOnReach,
    string reason)
    {
        if (route == null)
        {
            LogReturnRoute($"{reason} failed: route is null");
            return false;
        }

        if (!route.HasPoints)
        {
            LogReturnRoute($"{reason} failed: route has no points route:{route.name}");
            return false;
        }

        int reachableIndex = route.FindNearestReachableIndex(transform.position, motor);

        if (reachableIndex < 0)
        {
            int nearestIndex = route.FindNearestIndex(transform.position);
            Vector3 nearestPoint = route.GetPoint(nearestIndex);
            float nearestDistance = GetPlanarDistance(transform.position, nearestPoint);

            LogReturnRoute(
                $"{reason} failed: no reachable nearest point " +
                $"route:{route.name} nearestIndex:{nearestIndex} dist:{nearestDistance:F2}"
            );

            return false;
        }

        Vector3 point = route.GetPoint(reachableIndex);
        float pathLength = motor.GetPathLength(point);

        if (pathLength >= float.MaxValue)
        {
            LogReturnRoute(
                $"{reason} failed: path length invalid " +
                $"route:{route.name} index:{reachableIndex}"
            );

            return false;
        }

        currentRoute = route;
        patrolIndex = reachableIndex;
        pendingAdoptCurrentRouteAsHome = adoptAsHomeOnReach;

        motor.ResetPath();
        ResetReturnProgressTimersOnly();

        LogReturnRoute(
            $"{reason} Start " +
            $"route:{route.name} index:{patrolIndex} " +
            $"adoptHome:{pendingAdoptCurrentRouteAsHome} path:{pathLength:F2}"
        );

        return true;
    }

    private bool TryStartNextReturnCandidate()
    {
        if (routeManager == null || routeManager.Routes == null)
        {
            LogReturnRoute("NextCandidate failed: routeManager is null");
            return false;
        }

        bool hasOtherRoute = HasRouteOtherThanHomePair();

        // 1. HomeRoute / InitialHomeRoute 以外のルートがあるなら、まずそこを探す
        if (hasOtherRoute)
        {
            if (TryStartCandidatePass(
                excludeHomePair: true,
                respectRejectedRoutes: true,
                avoidLastRejectedRoute: true,
                reason: "Candidate ExcludeHomePair"))
            {
                return true;
            }

            LogReturnRoute("Candidate ExcludeHomePair failed");
        }
        else
        {
            LogReturnRoute("Candidate ExcludeHomePair skipped: no other routes");
        }

        // 2. 次に、HomePairも含めた全ルートを探す
        //    ただし、失敗済みルートはまだ除外する
        if (TryStartCandidatePass(
            excludeHomePair: false,
            respectRejectedRoutes: true,
            avoidLastRejectedRoute: true,
            reason: "Candidate AllRoutes RespectRejected"))
        {
            return true;
        }

        LogReturnRoute("Candidate AllRoutes RespectRejected failed");

        // 3. 全ルートが失敗済みなら、ここで初めて失敗リストをリセットする
        //    ただし直前に詰まったルートだけは一度避ける
        LogReturnRoute("Candidate Reset rejected routes and retry, avoid last rejected once");

        returnRejectedRoutes.Clear();

        if (TryStartCandidatePass(
            excludeHomePair: false,
            respectRejectedRoutes: false,
            avoidLastRejectedRoute: true,
            reason: "Candidate AllRoutes ResetAvoidLast"))
        {
            return true;
        }

        // 4. それでも無理なら、最後の最後に完全全検索
        LogReturnRoute("Candidate Final AllRoutes retry");

        if (TryStartCandidatePass(
            excludeHomePair: false,
            respectRejectedRoutes: false,
            avoidLastRejectedRoute: false,
            reason: "Candidate Final AllRoutes"))
        {
            return true;
        }

        LogReturnRoute("Candidate AllRoutes failed: no reachable route");

        return false;
    }

    private bool TryStartCandidatePass(
    bool excludeHomePair,
    bool respectRejectedRoutes,
    bool avoidLastRejectedRoute,
    string reason)
    {
        BuildSortedReturnCandidatesByPathLength();

        for (int i = 0; i < returnCandidateRoutes.Count; i++)
        {
            EnemyPatrolRoute route = returnCandidateRoutes[i];

            if (route == null || !route.HasPoints)
                continue;

            if (excludeHomePair && IsHomePairRoute(route))
                continue;

            if (respectRejectedRoutes && returnRejectedRoutes.Contains(route))
                continue;

            if (avoidLastRejectedRoute && route == lastRejectedReturnRoute)
                continue;

            bool adoptAsHome = route != homeRoute;

            if (TryStartReturnToRoute(
                route,
                adoptAsHome,
                reason))
            {
                return true;
            }

            if (!returnRejectedRoutes.Contains(route))
                returnRejectedRoutes.Add(route);
        }

        return false;
    }
    private void BuildSortedReturnCandidatesByPathLength()
    {
        returnCandidateRoutes.Clear();

        if (routeManager == null || routeManager.Routes == null)
            return;

        foreach (EnemyPatrolRoute route in routeManager.Routes)
        {
            if (route == null || !route.HasPoints)
                continue;

            if (!returnCandidateRoutes.Contains(route))
                returnCandidateRoutes.Add(route);
        }

        returnCandidateRoutes.Sort((a, b) =>
        {
            float pa = GetRoutePathLengthForSort(a);
            float pb = GetRoutePathLengthForSort(b);
            return pa.CompareTo(pb);
        });
    }

    private float GetRoutePathLengthForSort(EnemyPatrolRoute route)
    {
        if (route == null || !route.HasPoints)
            return float.MaxValue;

        int index = route.FindNearestReachableIndex(transform.position, motor);

        if (index < 0)
            return float.MaxValue;

        Vector3 point = route.GetPoint(index);
        return motor.GetPathLength(point);
    }

    private void UpdateReturnRecoveryProgress(Vector3 currentReturnTarget)
    {
        float dt = Time.deltaTime * motor.TimeScale;

        routeDistanceCheckTimer += dt;

        if (routeDistanceCheckTimer < patrolRecoveryParam.routeDistanceCheckInterval)
            return;

        routeDistanceCheckTimer = 0f;

        float currentDistance = GetPlanarDistance(transform.position, currentReturnTarget);

        if (lastReturnDistanceToTarget < 0f)
        {
            lastReturnDistanceToTarget = currentDistance;
            return;
        }

        float decreased = lastReturnDistanceToTarget - currentDistance;

        if (decreased >= patrolRecoveryParam.minRouteDistanceDecrease)
        {
            routeNoProgressTimer = 0f;
            lastReturnDistanceToTarget = currentDistance;
            return;
        }

        routeNoProgressTimer += patrolRecoveryParam.routeDistanceCheckInterval;
        lastReturnDistanceToTarget = currentDistance;

        LogReturnRoute(
            $"NoProgress route:{currentRoute.name} " +
            $"dist:{currentDistance:F2} decreased:{decreased:F2} " +
            $"timer:{routeNoProgressTimer:F2}"
        );

        if (routeNoProgressTimer < patrolRecoveryParam.routeNoProgressDuration)
            return;

        if (currentRoute != null)
        {
            lastRejectedReturnRoute = currentRoute;

            if (!returnRejectedRoutes.Contains(currentRoute))
                returnRejectedRoutes.Add(currentRoute);

            LogReturnRoute($"Reject route:{currentRoute.name}");
        }

        if (TryStartNextReturnCandidate())
            return;

        // 候補がない場合は少し待って再判定
        routeNoProgressTimer = 0f;
    }

    public void SetRoute(EnemyPatrolRoute newRoute, bool snapToNearestPoint = true, bool restartPatrol = true)
    {
        currentRoute = newRoute;

        if (!HasCurrentRoute())
        {
            motor.StopPlanarVelocity();
            return;
        }

        patrolIndex = snapToNearestPoint
            ? currentRoute.FindNearestIndex(transform.position)
            : Mathf.Clamp(patrolIndex, 0, currentRoute.Count - 1);

        if (restartPatrol)
            ChangeState(new Patrol());
    }

    private bool HasCurrentRoute()
    {
        return currentRoute != null && currentRoute.HasPoints;
    }

    private Vector3 GetCurrentPatrolPoint()
    {
        if (!HasCurrentRoute())
            return transform.position;

        return currentRoute.GetPoint(patrolIndex);
    }

    private void FaceTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            10f * Time.deltaTime);
    }

    private bool IsBattleVisionState()
    {
        return nowState is Found ||
               nowState is Chase ||
               nowState is LostSightChase ||
               nowState is Charge ||
               nowState is Attack ||
               nowState is AttackCooldown;
    }

    private void HandleLungeFinished()
    {
        if (nowState is Attack)
            attackCooldownRequested = true;
    }

    public bool IsAttackSequenceFinished => attackSequenceFinished;
    public bool IsAttackWindowOpen => attackWindowOpen;

    private void StartAttackSequence()
    {
        attackWindowOpen = false;
        attackSequenceFinished = false;
        hitTargetsThisAttack.Clear();
        DisableAllAttackObjects();

        if (attackHitParam.logAttack)
            Debug.Log($"{name} StartAttackSequence");
    }

    private void ForceEndAttackSequence()
    {
        attackWindowOpen = false;
        attackSequenceFinished = true;
        hitTargetsThisAttack.Clear();
        DisableAllAttackObjects();

        if (attackLungeMotor != null)
            attackLungeMotor.ForceStop();

        if (attackHitParam.logAttack)
            Debug.Log($"{name} ForceEndAttackSequence");
    }

    public void AE_BeginAttack()
    {
        attackWindowOpen = true;
        attackSequenceFinished = false;
        hitTargetsThisAttack.Clear();
        DisableAllAttackObjects();

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_BeginAttack");
    }

    public void AE_EndAttack()
    {
        attackWindowOpen = false;
        attackSequenceFinished = true;
        DisableAllAttackObjects();

        if (attackLungeMotor != null)
            attackLungeMotor.ForceStop();

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_EndAttack");
    }

    public void AE_BeginLunge()
    {
        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_BeginLunge");
    }

    public void AE_EnableRightPunch()
    {
        DisableAllAttackObjects();
        SetAttackObjectActive(punchObjects.rightPunchObject, true);

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_EnableRightPunch");
    }

    public void AE_DisableRightPunch()
    {
        SetAttackObjectActive(punchObjects.rightPunchObject, false);

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_DisableRightPunch");
    }

    public void AE_EnableLeftPunch()
    {
        DisableAllAttackObjects();
        SetAttackObjectActive(punchObjects.leftPunchObject, true);

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_EnableLeftPunch");
    }

    public void AE_DisableLeftPunch()
    {
        SetAttackObjectActive(punchObjects.leftPunchObject, false);

        if (attackHitParam.logAttack)
            Debug.Log($"{name} AE_DisableLeftPunch");
    }

    public void AE_EndFound()
    {
        foundAnimationFinished = true;
    }

    public void AE_EndCharge()
    {
        chargeAnimationFinished = true;
    }

    private void SetAttackObjectActive(GameObject attackObject, bool value)
    {
        if (attackObject == null)
            return;

        attackObject.SetActive(value);

        if (value)
            activeAttackObjects.Add(attackObject);
        else
            activeAttackObjects.Remove(attackObject);
    }

    private void DisableAllAttackObjects()
    {
        SetAttackObjectActive(punchObjects.rightPunchObject, false);
        SetAttackObjectActive(punchObjects.leftPunchObject, false);
    }

    private float ResolveDamageFromBlow(BlowPayload blow)
    {
        float rate = Mathf.Clamp01(blow.powerRate);
        return Mathf.Max(0f, blow.powerConstant * rate);
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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

    public void TakeDamage(float damage)
    {
        hp -= damage;

        if (hp <= 0f && !KillCountFlg)
        {
            KillCountFlg = true;
            MissionManager.Instance.AddKill(enemyParam.enemyData);
            //    //if (effectPlayer != null)
            //    //{
            //    //    EffectPlayParam param = EffectPlayParam.Default;
            //    //    param.scale *= 2.0f;
            //    //    //effectPlayer.Play(0, param);
            //    //}

            //    //Destroy(gameObject);
        }
    }
    public void Destroy()
    {
        Destroy(gameObject);
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

    private void LogReturnRoute(string message)
    {
        if (!patrolRecoveryParam.logReturnRoute)
            return;

        Debug.Log($"{name} ReturnRoute {message}");
    }

    private bool IsHomePairRoute(EnemyPatrolRoute route)
    {
        if (route == null)
            return false;

        return route == homeRoute || route == initialHomeRoute;
    }

    private bool HasRouteOtherThanHomePair()
    {
        if (routeManager == null || routeManager.Routes == null)
            return false;

        foreach (EnemyPatrolRoute route in routeManager.Routes)
        {
            if (route == null || !route.HasPoints)
                continue;

            if (!IsHomePairRoute(route))
                return true;
        }

        return false;
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

    private void FinishAttackSequenceByState()
    {
        attackWindowOpen = false;
        attackSequenceFinished = true;
        DisableAllAttackObjects();

        if (attackLungeMotor != null)
            attackLungeMotor.ForceStop();

        if (attackHitParam.logAttack)
            Debug.Log($"{name} FinishAttackSequenceByState");
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
    public void OnHitObject(EnemyControllerTypePatroler other)
    {
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

        // エネミーの攻撃判定
        if (selfHitbox.gameObject == punchObjects.rightPunchObject ||
            selfHitbox.gameObject == punchObjects.leftPunchObject)
        {
            if (!attackWindowOpen)
                return;

            GameObject attackObject = selfHitbox.gameObject;
            if (!activeAttackObjects.Contains(attackObject))
                return;

            if (hitTargetsThisAttack.Contains(targetHitbox.gameObject))
                return;

            hitTargetsThisAttack.Add(targetHitbox.gameObject);

            if (effectPlayer != null)
                effectPlayer.Play(5, targetHitbox.transform);

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

            if (attackHitParam.logAttack)
            {
                Debug.Log(
                    $"{name} Hit target:{targetHitbox.name} " +
                    $"with:{attackObject.name} damage:{enemyParam.attackDamage}"
                );
            }

            targetHitbox.receiver.OnHit(data);

            if (nowState is Attack)
                attackCooldownRequested = true;

            return;
        }

        // エネミーの連鎖判定
        if (selfHitbox.gameObject == chainParam.chainCollider)
        {
            if (remainingChainCount <= 0)
                return;

            if (lastChainFrame == Time.frameCount)
                return;

            if (!motor.IsEnemyFlying())
                return;

            IHitReceiver receiver = other.GetComponentInParent<IHitReceiver>();
            if (receiver == null)
                return;

            Component receiverComponent = receiver as Component;
            GameObject receiverObject = receiverComponent != null
                ? receiverComponent.gameObject
                : other.gameObject;

            if (receiverObject == gameObject)
                return;

            int chainIndex = 1;

            if (ChainHitManager.Instance != null)
            {
                if (!ChainHitManager.Instance.TryBeginChainPair(
                    gameObject,
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

            motor.ApplyChainReaction(
                -directionToTarget,
                chainParam.selfHorizontalPower,
                chainParam.selfVerticalPower
            );

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

    public void OnHit(HitEventData data)
    {
        if (data.payload is BlowPayload blow)
        {
            if (receiveHitBox != null && data.targetHitbox == receiveHitBox)
            {
                HandleReceivedBlow(blow);
                return;
            }

            if (punchObjects.leftPunchObject != null && data.targetHitbox == punchObjects.leftPunchObject)
            {
                HandleReceivedBlow(blow);
                return;
            }

            if (punchObjects.rightPunchObject != null && data.targetHitbox == punchObjects.rightPunchObject)
            {
                HandleReceivedBlow(blow);
                return;
            }
        }

        if (data.payload is ChainPayload chain)
        {
            lastChainFrame = Time.frameCount;

            TakeDamage(chain.damage);

            motor.ApplyChainReaction(
                chain.direction,
                chain.horizontalPower,
                chain.verticalPower
            );

            pendingKnockdownRecovery = true;
            ChangeState(new LaunchAirborne());

            return;
        }
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

        float x = screenPos.x - 60f;
        float y = Screen.height - screenPos.y - 10f;

        GUI.color = Color.white;
        GUI.Box(new Rect(x, y, 120f, 22f), currentStateName);
#endif
    }
}