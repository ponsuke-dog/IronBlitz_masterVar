using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(TimeAgent))]
public class BlowObjectController : MonoBehaviour, IHitSource, IHitReceiver
{
    #region コンポーネント

    private CharacterController controller;
    private TimeAgent agent;
    private EffectPlayer effectPlayer;

    #endregion

    #region Inspector パラメーター

    #region 当たり判定 / レイヤー

    [System.Serializable]
    private class HitParam
    {
        [Header("当たり判定")]
        [Tooltip("通常時に被弾できる当たり判定オブジェクト。GameObject自体は常にActiveのまま使う")]
        public GameObject hitCollider;

        [Tooltip("吹き飛び中に連続ヒットしない再被弾間隔")]
        public float reHitInterval = 0.15f;

        [Header("CharacterController 除外レイヤー")]
        [Tooltip("吹き飛び中に CharacterController が無視するレイヤー")]
        public LayerMask ignoreLayerAir;

        [Tooltip("Idle時に CharacterController が無視するレイヤー")]
        public LayerMask ignoreLayerIdle;

        [Header("復帰判定")]
        [Tooltip("この水平速度以下なら Idle 相当とみなしやすくする")]
        public float idleLayerReturnSpeed = 0.4f;

        [Tooltip("この垂直速度以下なら Idle 相当とみなしやすくする")]
        public float idleLayerReturnVerticalSpeed = 0.4f;

        [Tooltip("床に接していてこの程度の上向き速度までなら被弾受付を戻してよいとみなす")]
        public float groundedHitReceiveVerticalAllowance = 2.0f;
    }
    [SerializeField] private HitParam hitParam = new HitParam();

    #endregion

    #region 移動 / 基本物理

    [System.Serializable]
    private class MotionParam
    {
        [Header("基本物理")]
        [Tooltip("質量。大きいほど吹き飛びにくい")]
        public float mass = 1f;

        [Tooltip("重力加速度")]
        public float gravity = 28f;

        [Tooltip("地面に張り付かせるための下向き速度")]
        public float groundStickForce = 1.5f;

        [Tooltip("吹き飛び威力に掛ける倍率")]
        public float blowPowerMultiplier = 5f;

        [Tooltip("吹き飛び時に最低限与える上向き速度")]
        public float minUpwardPower = 4f;

        [Header("移動減衰")]
        [Tooltip("空中で水平速度を減衰させる強さ")]
        public float airLinearDrag = 3f;

        [Tooltip("地上で水平速度を減衰させる強さ")]
        public float groundLinearDrag = 12f;

        [Tooltip("低速時に追加する地上ブレーキ")]
        public float lowSpeedGroundBrake = 20f;

        [Header("停止判定")]
        [Tooltip("この水平速度以下で停止扱いに近づく")]
        public float minStopSpeed = 0.35f;

        [Tooltip("この垂直速度以下で停止扱いに近づく")]
        public float minStopVerticalSpeed = 0.35f;

        [Tooltip("この速度以下なら水平速度を0にする")]
        public float snapStopSpeed = 0.12f;

        [Tooltip("停止判定をこの秒数維持したら Idle に戻す")]
        public float stopConfirmTime = 0.12f;
    }
    [SerializeField] private MotionParam motionParam = new MotionParam();

    #endregion

    #region 反射 / 接地


    [System.Serializable]
    private class BounceParam
    {
        [Header("床 / 坂 / 壁 / 天井の判定")]
        [Tooltip("この角度以下なら床扱い")]
        public float floorMaxAngle = 10f;

        [Tooltip("この角度以下なら坂扱い。これを超える面は壁扱い")]
        public float slopeMaxAngle = 55f;

        [Tooltip("法線Yがこれ以下なら天井扱い")]
        [Range(-1f, 0f)] public float ceilingMaxNormalY = -0.35f;

        [Header("反射係数")]
        [Tooltip("壁反射係数。大きいほど強く跳ね返る")]
        public float wallBouncePower = 0.8f;

        [Tooltip("坂反射係数。壁より弱く、床より強いくらいが扱いやすい")]
        public float slopeBouncePower = 0.45f;

        [Tooltip("床反射係数。小さいとすぐ止まりやすい")]
        public float floorBouncePower = 0.2f;

        [Tooltip("天井反射係数")]
        public float ceilingBouncePower = 0.4f;

        [Header("反射しきい値")]
        [Tooltip("これ未満の落下速度では床反射しない")]
        public float minFloorBounceSpeed = 1.0f;

        [Tooltip("これ未満の上昇速度では天井反射しない")]
        public float minCeilingBounceSpeed = 1.0f;

        [Header("接触保持")]
        [Tooltip("地面接触を何秒保持するか。小さすぎると床判定が不安定になる")]
        public float contactMemoryTime = 0.08f;

        [Header("床反射安定化")]
        [Tooltip("CharacterController.Move の Below 判定でも床バウンスを行う")]
        public bool useControllerBelowForFloorBounce = true;

        [Tooltip("床バウンス直後に少し上へ離す距離。床に張り付いて次フレームでGroundMove化するのを防ぐ")]
        public float floorBounceDetachDistance = 0.03f;

        [Header("多重反射防止")]
        [Tooltip("同一フレームでの多重反射を防ぐ")]
        public bool blockMultiBouncePerFrame = true;


        [Header("地形ヒット通知")]
       
        [Tooltip("この速度未満では地形ヒット通知を出さない。静止状態で破壊可能壁を壊さないためのしきい値")]
        public float minSurfaceHitNotifySpeed = 2.0f;

        [Tooltip("床接触でも地形ヒット通知を出すか。基本OFF推奨")]
        public bool notifyGroundSurfaceHit = false;


        [Header("反射安定化")]
        [Tooltip("反射後の水平速度上限")]
        public float maxBounceHorizontalSpeed = 18f;

        [Tooltip("反射後の垂直速度上限")]
        public float maxBounceVerticalSpeed = 14f;

        [Tooltip("壁に向かっている速度がこの値未満なら壁反射しない")]
        public float minWallImpactSpeed = 0.5f;

        [Tooltip("坂に向かっている速度がこの値未満なら坂反射しない")]
        public float minSlopeImpactSpeed = 0.5f;

    }

    [SerializeField] private BounceParam bounceParam = new BounceParam();

    #endregion

    #region 連鎖

    [System.Serializable]
    private class ChainParam
    {
        [Header("連鎖判定")]
        [Tooltip("通常連鎖判定用 Hitbox のルート。爆弾タイプでは爆発Colliderとは別扱い")]
        public GameObject chainCollider;

        [Tooltip("この速度以上で通常連鎖を発火できる")]
        public float minChainSpeed = 2.5f;

        [Tooltip("同一ペアの再連鎖を防ぐ時間")]
        public float pairCooldown = 0.15f;

        [Header("連鎖回数")]
        [Tooltip("Idleに戻ったときに回復する通常連鎖発火回数")]
        public int maxChainCount = 3;

        [Header("自分への反動")]
        [Tooltip("自分が相手に連鎖ヒットした時、自分が反対方向へ跳ね返る水平速度")]
        public float selfHorizontalPower = 3.0f;

        [Tooltip("自分が相手に連鎖ヒットした時、自分が上に跳ねる垂直速度")]
        public float selfVerticalPower = 8.0f;

        [Tooltip("自分が相手に連鎖ヒットした時、自分が受けるダメージ")]
        public float selfDamage = 15f;

        [Header("相手へ送る連鎖衝撃")]
        [Tooltip("自分が相手に連鎖ヒットした時、相手へ送る水平衝撃")]
        public float targetHorizontalPower = 4.5f;

        [Tooltip("自分が相手に連鎖ヒットした時、相手へ送る垂直衝撃")]
        public float targetVerticalPower = 12f;

        [Tooltip("自分が相手に連鎖ヒットした時、相手へ送るダメージ")]
        public float targetDamage = 50f;
    }

    [SerializeField] private ChainParam chainParam = new ChainParam();

    #endregion

    #region 回転

    [System.Serializable]
    private class RotationParam
    {
        [Header("初期回転")]
        [Tooltip("吹き飛び時に与える主回転速度（度/秒）")]
        public float blowSpinSpeed = 300f;

        [Tooltip("吹き飛び時の補助的なひねり速度（度/秒）")]
        public float blowTwistSpeed = 60f;

        [Header("反射回転")]
        [Tooltip("壁 / 天井反射時に追加する回転速度（度/秒）")]
        public float bounceSpinAdd = 90f;

        [Header("制限")]
        [Tooltip("回転速度の最大値（度/秒）")]
        public float maxSpinSpeed = 360f;

        [Header("減衰")]
        [Tooltip("空中での回転減衰")]
        public float airSpinDrag = 100f;

        [Tooltip("地上での回転減衰")]
        public float groundSpinDrag = 260f;

        [Tooltip("低速時に追加する回転減衰")]
        public float lowSpeedExtraSpinDrag = 340f;

        [Tooltip("この移動速度以下なら回転をかなり止めやすくする")]
        public float spinStopMoveSpeed = 0.25f;

        [Tooltip("この回転速度以下なら回転を0にする")]
        public float snapStopSpinSpeed = 8f;

        [Tooltip("Idleに入る時にどれだけ回転を残すか")]
        [Range(0f, 1f)] public float idleEnterSpinKeep = 0.06f;
    }
    [SerializeField] private RotationParam rotationParam = new RotationParam();

    #endregion

    #region HP / 破壊

    [System.Serializable]
    private class HPParam
    {
        [Header("破壊可否")]
        public bool destructible = true;

        [Header("HP")]
        public float maxHP = 100f;

        [Header("ダメージ量")]
        public float damageOnBlow = 20f;
        public float damageOnWallBounce = 10f;
        public float damageOnCeilingBounce = 8f;

        [Header("HP0後の猶予")]
        public float disableDelay = 1.5f;
    }

    [SerializeField] private HPParam hpParam = new HPParam();

    #endregion

    #region 反射エフェクト

    [System.Serializable]
    private class BounceEffectParam
    {
        [Header("Effect Index")]
        [Tooltip("壁反射時に再生するEffectPlayerのindex")]
        public int wallEffectIndex = 1;

        [Tooltip("天井反射時に再生するEffectPlayerのindex")]
        public int ceilingEffectIndex = 1;

        [Header("配置")]
        [Tooltip("壁・天井面から少し浮かせる距離")]
        public float surfaceOffset = 0.03f;

        [Tooltip("エフェクトスケール")]
        public Vector3 scale = Vector3.one;

        [Header("回転")]
        [Tooltip("Prefab側の向き補正。EffectData側で調整しているなら0でOK")]
        public Vector3 rotationOffset = Vector3.zero;

    }

    [SerializeField]
    [Header("反射エフェクト")]
    private BounceEffectParam bounceEffect = new BounceEffectParam();

    #endregion

    #region オブジェクトタイプ

    private enum BlowObjectType
    {
        Light,
        Heavy,
        Bomb,
        Sticky
    }

    [System.Serializable]
    private class TackleReactionParam
    {
        [Header("吹き飛び倍率")]
        [Tooltip("水平吹き飛び倍率")]
        public float horizontalPowerRate = 1f;

        [Tooltip("上方向吹き飛び倍率")]
        public float upwardPowerRate = 1f;

        [Tooltip("ダメージ倍率")]
        public float damageRate = 1f;

        [Header("連鎖")]
        [Tooltip("このタックル種別で連鎖を発火できるか")]
        public bool canEmitChain = true;
    }

    [System.Serializable]
    private class TackleReactionSet
    {
        [Header("通常タックル")]
        public TackleReactionParam normal = new TackleReactionParam
        {
            horizontalPowerRate = 1f,
            upwardPowerRate = 1f,
            damageRate = 1f,
            canEmitChain = true
        };

        [Header("チャージタックル")]
        public TackleReactionParam charge = new TackleReactionParam
        {
            horizontalPowerRate = 1.2f,
            upwardPowerRate = 1.1f,
            damageRate = 1f,
            canEmitChain = true
        };

        [Header("ジャスト通常タックル")]
        public TackleReactionParam justNormal = new TackleReactionParam
        {
            horizontalPowerRate = 1.35f,
            upwardPowerRate = 1.15f,
            damageRate = 1.2f,
            canEmitChain = true
        };

        [Header("ジャストチャージタックル")]
        public TackleReactionParam justCharge = new TackleReactionParam
        {
            horizontalPowerRate = 1.6f,
            upwardPowerRate = 1.25f,
            damageRate = 1.4f,
            canEmitChain = true
        };
    }

    [System.Serializable]
    private class ChainReceiveReactionParam
    {
        [Header("連鎖を受けた時の補正")]

        [Tooltip("受け取った連鎖の水平衝撃に掛ける倍率")]
        public float horizontalPowerRate = 1f;

        [Tooltip("受け取った連鎖の垂直衝撃に掛ける倍率")]
        public float verticalPowerRate = 1f;

        [Tooltip("受け取った連鎖ダメージに掛ける倍率")]
        public float damageRate = 1f;

        [Tooltip("連鎖で吹き飛ばされるか")]
        public bool canReceiveChain = true;

        [Tooltip("この連鎖で吹き飛ばされた後、自分も通常連鎖を発火できるか")]
        public bool canEmitChainAfterReceived = true;
    }

    [System.Serializable]
    private class LightObjectParam
    {
        [Header("軽いオブジェクト設定")]
        public TackleReactionSet reactions = new TackleReactionSet();

        [Tooltip("壁破壊可能か。現状は参照用フラグ")]
        public bool canBreakWall = false;

        [Header("連鎖を受けた時")]
        public ChainReceiveReactionParam chainReceiveReaction = new ChainReceiveReactionParam
        {
            horizontalPowerRate = 1.1f,
            verticalPowerRate = 1.0f,
            damageRate = 1.0f,
            canReceiveChain = true,
            canEmitChainAfterReceived = true
        };
    }

    [System.Serializable]
    private class HeavyObjectParam
    {
        [Header("重いオブジェクト設定")]
        public TackleReactionSet reactions = new TackleReactionSet
        {
            normal = new TackleReactionParam
            {
                horizontalPowerRate = 0.35f,
                upwardPowerRate = 0.45f,
                damageRate = 1f,
                canEmitChain = true
            },
            charge = new TackleReactionParam
            {
                horizontalPowerRate = 1.25f,
                upwardPowerRate = 1.0f,
                damageRate = 1f,
                canEmitChain = true
            },
            justNormal = new TackleReactionParam
            {
                horizontalPowerRate = 0.65f,
                upwardPowerRate = 0.75f,
                damageRate = 1.1f,
                canEmitChain = true
            },
            justCharge = new TackleReactionParam
            {
                horizontalPowerRate = 1.75f,
                upwardPowerRate = 1.25f,
                damageRate = 1.3f,
                canEmitChain = true
            }
        };

        [Tooltip("壁破壊可能か。現状は参照用フラグ")]
        public bool canBreakWall = false;


        [Header("連鎖を受けた時")]
        public ChainReceiveReactionParam chainReceiveReaction = new ChainReceiveReactionParam
        {
            horizontalPowerRate = 0.35f,
            verticalPowerRate = 0.45f,
            damageRate = 0.8f,
            canReceiveChain = true,
            canEmitChainAfterReceived = true
        };
    }


    [System.Serializable]
    private class BombObjectParam
    {
        [Header("爆弾オブジェクト設定")]
        public TackleReactionSet reactions = new TackleReactionSet();


        [Tooltip("壁破壊可能か")]
        public bool canBreakWall = false;


        [Header("爆発")]
        [Tooltip("爆発判定用コライダー。通常はOFF。爆発時だけONにする")]
        public GameObject explosionCollider;

        [Header("連鎖接触爆発")]
        [Tooltip("ONなら、爆弾タイプは通常連鎖Colliderで相手に触れた時、通常連鎖ではなく爆発する")]
        public bool explodeOnChainContact = true;

        [Tooltip("連鎖接触爆発に必要な最低速度")]
        public float chainContactExplosionMinSpeed = 2.5f;

        [Tooltip("連鎖接触で爆発した瞬間、接触相手へ即座に爆発ヒットを送る")]
        public bool hitContactTargetImmediatelyOnExplosion = true;

        [Tooltip("爆発判定をONにしておく時間")]
        public float explosionActiveTime = 0.12f;

        [Tooltip("爆発後に自身を無効化する")]
        public bool disableSelfAfterExplosion = true;

        [Tooltip("爆発エフェクトindex。-1なら再生しない")]
        public int explosionEffectIndex = -1;

        [Header("初期爆発無視")]
        [Tooltip("吹き飛ばし直後、この時間は爆発しない")]
        public float initialExplosionIgnoreTime = 0.15f;

        [Tooltip("一度地面から離れるまでは地面接触で爆発しない")]
        public bool ignoreGroundUntilLeaveInitialGround = true;

        [Tooltip("この上向き速度以上なら地面から離れたとみなす")]
        public float leaveGroundVerticalSpeed = 0.5f;

        [Header("連鎖を受けた時")]
        public ChainReceiveReactionParam chainReceiveReaction = new ChainReceiveReactionParam
        {
            horizontalPowerRate = 1.0f,
            verticalPowerRate = 0.9f,
            damageRate = 1.0f,
            canReceiveChain = true,
            canEmitChainAfterReceived = false
        };

        [Header("爆発ヒット")]
        [Tooltip("爆発で相手へ送る水平衝撃")]
        public float explosionHorizontalPower = 8f;

        [Tooltip("爆発で相手へ送る垂直衝撃")]
        public float explosionVerticalPower = 12f;

        [Tooltip("爆発で相手へ与えるダメージ")]
        public float explosionDamage = 80f;

        [Tooltip("爆発で同じ相手へ連続ヒットするのを防ぐ時間")]
        public float explosionPairCooldown = 0.15f;
    }

    [System.Serializable]
    private class StickyObjectParam
    {
        [Header("粘着オブジェクト設定")]
        public TackleReactionSet reactions = new TackleReactionSet
        {
            normal = new TackleReactionParam
            {
                horizontalPowerRate = 0.85f,
                upwardPowerRate = 0.65f,
                damageRate = 1f,
                canEmitChain = false
            },
            charge = new TackleReactionParam
            {
                horizontalPowerRate = 1.0f,
                upwardPowerRate = 0.75f,
                damageRate = 1f,
                canEmitChain = false
            },
            justNormal = new TackleReactionParam
            {
                horizontalPowerRate = 1.0f,
                upwardPowerRate = 0.8f,
                damageRate = 1.1f,
                canEmitChain = false
            },
            justCharge = new TackleReactionParam
            {
                horizontalPowerRate = 1.2f,
                upwardPowerRate = 0.9f,
                damageRate = 1.2f,
                canEmitChain = false
            }
        };


        [Tooltip("壁破壊可能か")]
        public bool canBreakWall = false;


        [Header("粘着")]
        [Tooltip("接触面から少し浮かせる距離")]
        public float stickSurfaceOffset = 0.03f;

        [Tooltip("くっついたら連鎖ColliderをOFFにする")]
        public bool disableChainColliderOnStick = true;

        [Tooltip("くっついたら被弾受付を戻す")]
        public bool enableHitReceiveOnStick = true;

        [Header("初期接地無視")]
        [Tooltip("吹き飛ばし直後、この時間は粘着しない")]
        public float initialStickIgnoreTime = 0.15f;

        [Tooltip("一度地面から離れるまでは地面への粘着を無視する")]
        public bool ignoreGroundUntilLeaveInitialGround = true;

        [Tooltip("この上向き速度以上なら地面から離れたとみなす")]
        public float leaveGroundVerticalSpeed = 0.5f;


        [Header("粘着条件")]
        [Tooltip("この速度未満では粘着しない")]
        public float minStickSpeed = 1.5f;

        [Tooltip("地面にも粘着するか")]
        public bool allowStickToGround = false;

        [Tooltip("他のBlowObjectには粘着しない")]
        public bool ignoreOtherBlowObjects = true;


        [Header("連鎖を受けた時")]
        public ChainReceiveReactionParam chainReceiveReaction = new ChainReceiveReactionParam
        {
            horizontalPowerRate = 0.7f,
            verticalPowerRate = 0.6f,
            damageRate = 0.8f,
            canReceiveChain = true,
            canEmitChainAfterReceived = false
        };

    }

    [SerializeField]
    [Header("オブジェクトタイプ")]
    private BlowObjectType objectType = BlowObjectType.Light;

    [SerializeField] private LightObjectParam lightObject = new LightObjectParam();
    [SerializeField] private HeavyObjectParam heavyObject = new HeavyObjectParam();
    [SerializeField] private BombObjectParam bombObject = new BombObjectParam();
    [SerializeField] private StickyObjectParam stickyObject = new StickyObjectParam();

    #endregion

    #endregion

    #region 物理状態

    private Vector3 velocity;
    private float verticalVelocity;

    private Vector3 spinAxis = Vector3.right;
    private float spinSpeed = 0f;

    private float stopTimer;
    private float idleReadyTimer;

    private float stuckRecoverTimer;

    #endregion

    #region タイプ別状態

    private TackleType lastReceivedTackleType = TackleType.Normal;

    private bool isBombExploding = false;
    private float bombExplosionTimer = 0f;

    private bool isStickyStuck = false;

    private float bombIgnoreTimer = 0f;
    private bool bombLeftInitialGround = false;


    private float stickyIgnoreTimer = 0f;
    private bool stickyLeftInitialGround = false;


    private bool currentCanEmitChain = true;

    private readonly HashSet<int> explosionHitTargetIds = new HashSet<int>();

    private float reHitTimer = 0f;

    #endregion

    #region ヒット受付管理

    private Collider[] hitReceiveColliders;
    private Hitbox[] hitReceiveHitboxes;

    #endregion

    #region 接触情報


    private enum SurfaceType
    {
        None,
        Ground,
        Slope,
        Wall,
        Ceiling
    }


    private Vector3 recentGroundNormal = Vector3.up;
    private Vector3 recentWallNormal = Vector3.zero;
    private Vector3 recentCeilingNormal = Vector3.down;

    private float groundContactTimer = 0f;
    private float wallContactTimer = 0f;
    private float ceilingContactTimer = 0f;

    private float wallDetachTimer = 0f;

    private int lastBounceFrame = -1;

    #endregion

    #region HP 状態

    [SerializeField] private float currentHP;
    private float hpZeroTimer = -1f;
    private bool isHPZero => currentHP <= 0f;

    // Destroy予約後に同フレーム中の処理が走り続けないようにする
    private bool isDestroying = false;

    #endregion

    #region 連鎖状態

    [SerializeField] private int remainingChainCount;

    private int lastChainFrame = -1;

    #endregion

    #region ステート管理

    private State nowState;
    private State reserveState;
    private int reservePriority;

    public bool IsFlying => nowState is Airborne || nowState is GroundMove;

    #endregion

    #region ステート基底

    private abstract class State
    {
        protected BlowObjectController obj;
        public void Set(BlowObjectController o) => obj = o;

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }

    #endregion

    #region ステート

    private class Idle : State
    {
        public override void Enter()
        {
            obj.velocity = Vector3.zero;
            obj.verticalVelocity = 0f;
            obj.spinSpeed *= obj.rotationParam.idleEnterSpinKeep;
            obj.stopTimer = 0f;
            obj.idleReadyTimer = 0f;
            obj.wallDetachTimer = 0f;
            obj.stuckRecoverTimer = 0f;


            obj.ResetChainCount();
        }

        public override void Update()
        {
            float dt = Time.deltaTime * obj.agent.TimeScale;

         
            obj.spinSpeed = obj.DampScalar(
                obj.spinSpeed,
                obj.rotationParam.groundSpinDrag,
                dt
            );

            if (obj.spinSpeed <= obj.rotationParam.snapStopSpinSpeed)
                obj.spinSpeed = 0f;
        }
    }

    private class Airborne : State
    {
        public override void Enter()
        {
            obj.stopTimer = 0f;
            obj.idleReadyTimer = 0f;
        }

        public override void Update()
        {
            float dt = Time.deltaTime * obj.agent.TimeScale;

            obj.velocity = obj.DampHorizontalSpeed(obj.velocity, obj.motionParam.airLinearDrag, dt);

            if (obj.HasRecentGroundContact() && obj.verticalVelocity <= 0.2f)
            {
                obj.Reserve(new GroundMove(), 50);
                return;
            }
        }
    }


    private class GroundMove : State
    {
        public override void Enter()
        {
            obj.stopTimer = 0f;
            obj.idleReadyTimer = 0f;
        }

        public override void Update()
        {
            float dt = Time.deltaTime * obj.agent.TimeScale;

            if (!obj.HasRecentGroundContact())
            {
                obj.Reserve(new Airborne(), 50);
                return;
            }

            obj.velocity = obj.ProjectVelocityToGround(obj.velocity);

            float drag = obj.motionParam.groundLinearDrag;
            float lowSpeedBrake = obj.motionParam.lowSpeedGroundBrake;


            if (obj.velocity.magnitude <= 0.5f)
                drag += lowSpeedBrake;

            obj.velocity = obj.MoveTowardsHorizontalSpeed(obj.velocity, drag, dt);


            if (obj.velocity.magnitude <= obj.motionParam.snapStopSpeed)
            {
                obj.velocity = Vector3.zero;
            }



            if (obj.verticalVelocity < 0f)
                obj.verticalVelocity = -obj.motionParam.groundStickForce;

            // 接地中の -groundStickForce は停止判定用には 0 扱いにする
            float stopCheckVerticalVelocity = obj.verticalVelocity;

            if (obj.HasRecentGroundContact() && stopCheckVerticalVelocity <= 0f)
            {
                stopCheckVerticalVelocity = 0f;
            }


            if (obj.velocity.magnitude <= obj.motionParam.minStopSpeed &&
                Mathf.Abs(stopCheckVerticalVelocity) <= obj.motionParam.minStopVerticalSpeed)
            {
                obj.stopTimer += dt;

                if (obj.stopTimer >= obj.motionParam.stopConfirmTime)
                {
                    obj.velocity = Vector3.zero;
                    obj.verticalVelocity = 0f;
                    obj.Reserve(new Idle(), 0);
                }
            }
            else
            {
                obj.stopTimer = 0f;
            }

        }
    }


    #endregion

    #region Unity 初期化

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();

        if (hitParam.hitCollider != null)
        {
            hitParam.hitCollider.SetActive(true);
            hitReceiveColliders = hitParam.hitCollider.GetComponentsInChildren<Collider>(true);
            hitReceiveHitboxes = hitParam.hitCollider.GetComponentsInChildren<Hitbox>(true);
        }
        else
        {
            hitReceiveColliders = System.Array.Empty<Collider>();
            hitReceiveHitboxes = System.Array.Empty<Hitbox>();
        }

        currentHP = hpParam.maxHP;
        ResetChainCount();

        Reserve(new Idle(), 0);
        ApplyReserve();

        SyncRuntimeState();
    }

    private void OnEnable()
    {

        if (controller != null)
            controller.enabled = true;

        if (hitParam.hitCollider != null)
            hitParam.hitCollider.SetActive(true);


        if (bombObject.explosionCollider != null)
            bombObject.explosionCollider.SetActive(false);


        isBombExploding = false;
        bombExplosionTimer = 0f;
        bombIgnoreTimer = 0f;
        bombLeftInitialGround = false;
        explosionHitTargetIds.Clear();

        isStickyStuck = false;
        stickyIgnoreTimer = 0f;
        stickyLeftInitialGround = false;


        ResetChainCount();
        SyncRuntimeState();
    }

    #endregion

    #region 更新処理

    private void Update()
    {
        if (agent.TimeScale <= 0f)
        {
            SyncRuntimeState();
            return;
        }

        float dt = Time.deltaTime * agent.TimeScale;
        reservePriority = int.MinValue;


        UpdateContactTimers(dt);

        nowState?.Update();

        ApplyReserve();

        ApplyGravity(dt);
        ApplyMovement(dt);
        ApplyRotation(dt);

        UpdateIdleReadyTimer(dt);
        RecoverStateIfNeeded();


        SyncRuntimeState();
        UpdateHP(dt);

        // Destroy予約後はこのフレームの残り処理を止める
        if (isDestroying)
            return;

        UpdateBombState(dt);
        UpdateBombExplosion(dt);

        // 爆発後Destroyされた場合も止める
        if (isDestroying)
            return;

        UpdateStickyState(dt);

    }

    private void LateUpdate()
    {
        SyncRuntimeState();
    }

    #endregion

    #region 強制同期 / 回復

    private bool CanReceiveHitsNow()
    {
        if (isStickyStuck)
            return true;

        if (nowState is Idle) return true;

        bool groundedLike = HasRecentGroundContact();
        bool lowHorizontal = velocity.magnitude <= hitParam.idleLayerReturnSpeed;
        bool acceptableVertical = Mathf.Abs(verticalVelocity) <= hitParam.groundedHitReceiveVerticalAllowance;

        return groundedLike && lowHorizontal && acceptableVertical;
    }


    private void UpdateIdleReadyTimer(float dt)
    {
        if (CanReceiveHitsNow())
            idleReadyTimer += dt;
        else
            idleReadyTimer = 0f;
    }

    private void SyncRuntimeState()
    {
        bool shouldBeIdleLike =
            isStickyStuck || CanReceiveHitsNow();

        //if (hitParam.hitCollider != null && !hitParam.hitCollider.activeSelf)
        //{
        //    hitParam.hitCollider.SetActive(true);
        //}

        //SetHitReceiveEnabled(shouldBeIdleLike);

        if (hitParam.hitCollider != null && !hitParam.hitCollider.activeSelf)
        {
            hitParam.hitCollider.SetActive(true);
        }

        SetHitReceiveEnabled(true);

        if (controller != null)
        {
            controller.excludeLayers = shouldBeIdleLike
                ? hitParam.ignoreLayerIdle
                : hitParam.ignoreLayerAir;
        }

    }

    private void RecoverStateIfNeeded()
    {
        if (nowState is Idle) return;

        if (idleReadyTimer < motionParam.stopConfirmTime) return;

        if (HasRecentGroundContact() && velocity.magnitude <= hitParam.idleLayerReturnSpeed)
        {
            velocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        reserveState = new Idle();
        reservePriority = int.MaxValue;
        ApplyReserve();

        SyncRuntimeState();
    }

    #endregion

    #region 接触保持タイマー

    private void UpdateContactTimers(float dt)
    {
        if (groundContactTimer > 0f) groundContactTimer -= dt;
        if (wallContactTimer > 0f) wallContactTimer -= dt;
        if (ceilingContactTimer > 0f) ceilingContactTimer -= dt;
        if (wallDetachTimer > 0f) wallDetachTimer -= dt;

        if (groundContactTimer < 0f) groundContactTimer = 0f;
        if (wallContactTimer < 0f) wallContactTimer = 0f;
        if (ceilingContactTimer < 0f) ceilingContactTimer = 0f;
        if (wallDetachTimer < 0f) wallDetachTimer = 0f;
        if (reHitTimer > 0f) reHitTimer -= dt;
    }

    #endregion

    #region Trigger 接触通知

    public void NotifySurfaceTrigger(Collider selfTrigger, Collider other)
    {
        if (selfTrigger == null || other == null) return;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return;

        if (other.isTrigger) return;

        if (!Physics.ComputePenetration(
                selfTrigger, selfTrigger.transform.position, selfTrigger.transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 pushDir, out float pushDistance))
        {
            return;
        }

        if (pushDistance <= 0f) return;

        Vector3 normal = pushDir.normalized;
        SurfaceType surfaceType = ClassifySurface(normal);


        UpdateSurfaceMemory(surfaceType, normal);

        if (IsFlying)
        {
            if (TryHandleSpecialFlyingContact(surfaceType, normal, other, pushDistance))
                return;

            if (surfaceType == SurfaceType.Wall &&
                wallDetachTimer > 0f &&
                Vector3.Dot(normal, recentWallNormal) > 0.6f)
            {
                return;
            }

            if (nowState is GroundMove && surfaceType == SurfaceType.Ground)
                return;

            // ★反射前に相手へヒット通知
            TryNotifySurfaceHitReceiver(surfaceType, normal, other);

            TryBounce(surfaceType, normal);
        }

    }

    private bool TryNotifySurfaceHitReceiver(
     SurfaceType surfaceType,
     Vector3 surfaceNormal,
     Collider other)
    {
        // ObjectType側のcanBreakWallに統一
        if (!CanCurrentTypeBreakWall())
            return false;

        if (other == null)
            return false;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return false;

        // 床は基本通知しない
        if (surfaceType == SurfaceType.Ground && !bounceParam.notifyGroundSurfaceHit)
            return false;

        // 静止・低速では壊さない
        if (!CanNotifySurfaceHit(surfaceType))
            return false;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox == null)
            return false;

        IHitReceiver receiver = null;
        GameObject receiverObject = null;

        if (targetHitbox.receiver != null)
        {
            receiver = targetHitbox.receiver;

            MonoBehaviour receiverMono = receiver as MonoBehaviour;
            if (receiverMono != null)
                receiverObject = receiverMono.gameObject;
        }

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

        Vector3 totalVelocity = GetTotalVelocity();

        Vector3 hitDir = totalVelocity;

        if (hitDir.sqrMagnitude < 0.0001f)
            hitDir = -surfaceNormal;

        hitDir.y = 0f;

        if (hitDir.sqrMagnitude < 0.0001f)
            hitDir = transform.forward;

        hitDir.Normalize();

        float power = totalVelocity.magnitude;

        BlowPayload payload = new BlowPayload
        {
            tackleType = lastReceivedTackleType,

            // 壁側はtackleTypeとdamageConstantを見て壊すか判断できる
            damageConstant = hpParam.damageOnWallBounce,

            powerConstant = power,
            powerRate = 1f,
            powerDirection = hitDir
        };

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,

            attackerHitbox =
                chainParam.chainCollider != null
                    ? chainParam.chainCollider
                    : gameObject,

            targetObject = receiverObject,
            targetHitbox = targetHitbox.gameObject,
            payload = payload,

            contactPoint =
                controller != null
                    ? other.ClosestPoint(controller.bounds.center)
                    : other.ClosestPoint(transform.position)
        };

        receiver.OnHit(data);

        return true;
    }


    private bool TryHandleSpecialFlyingContact(
    SurfaceType surfaceType,
    Vector3 normal,
    Collider other,
    float pushDistance)
    {

        if (IsBombType())
        {
            if (CanExplodeOnSurface(surfaceType))
            {
                ExplodeBomb();
                return true;
            }

            return false;
        }


        if (IsStickyType())
        {
            if (CanStickToSurface(surfaceType, other))
            {
                StickToSurface(normal, other, pushDistance);
                return true;
            }

            return false;
        }

        return false;
    }

    private bool CanNotifySurfaceHit(SurfaceType surfaceType)
    {
        if (!IsFlying)
            return false;

        Vector3 totalVelocity = GetTotalVelocity();

        float speed = totalVelocity.magnitude;

        if (speed < bounceParam.minSurfaceHitNotifySpeed)
            return false;

        // 地面に転がって止まりかけている状態では壊さない
        if (nowState is GroundMove)
        {
            if (speed < bounceParam.minSurfaceHitNotifySpeed * 1.25f)
                return false;
        }

        // Idleでは絶対に通知しない
        if (nowState is Idle)
            return false;

        return true;
    }

    private void SetHitReceiveEnabled(bool enabled)
    {
        if (hitReceiveColliders != null)
        {
            for (int i = 0; i < hitReceiveColliders.Length; i++)
            {
                Collider col = hitReceiveColliders[i];
                if (col == null) continue;


                if (IsChainColliderObject(col.gameObject))
                    continue;

                if (IsExplosionColliderObject(col.gameObject))
                    continue;


                if (col.enabled != enabled)
                    col.enabled = enabled;
            }
        }

        if (hitReceiveHitboxes != null)
        {
            for (int i = 0; i < hitReceiveHitboxes.Length; i++)
            {
                Hitbox hb = hitReceiveHitboxes[i];
                if (hb == null) continue;


                if (IsChainColliderObject(hb.gameObject))
                    continue;

                if (IsExplosionColliderObject(hb.gameObject))
                    continue;


                if (hb.enabled != enabled)
                    hb.enabled = enabled;
            }
        }
    }

    private bool IsHitReceiveActuallyEnabled()
    {
        if (hitReceiveColliders != null)
        {
            for (int i = 0; i < hitReceiveColliders.Length; i++)
            {
                if (hitReceiveColliders[i] != null && hitReceiveColliders[i].enabled)
                    return true;
            }
        }

        if (hitReceiveHitboxes != null)
        {
            for (int i = 0; i < hitReceiveHitboxes.Length; i++)
            {
                if (hitReceiveHitboxes[i] != null && hitReceiveHitboxes[i].enabled)
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region 接触分類


    private SurfaceType ClassifySurface(Vector3 normal)
    {
        float angleFromUp = Vector3.Angle(normal, Vector3.up);

        // 天井判定を最優先
        if (normal.y <= bounceParam.ceilingMaxNormalY)
            return SurfaceType.Ceiling;

        // ほぼ水平なら床
        if (angleFromUp <= bounceParam.floorMaxAngle)
            return SurfaceType.Ground;

        // 床より急だが、壁ほどではないなら坂
        if (angleFromUp <= bounceParam.slopeMaxAngle)
            return SurfaceType.Slope;

        // それ以上は壁
        return SurfaceType.Wall;
    }


    private void UpdateSurfaceMemory(SurfaceType type, Vector3 normal)
    {
        switch (type)
        {
            case SurfaceType.Ground:
                recentGroundNormal = normal;
                groundContactTimer = bounceParam.contactMemoryTime;
                break;



            case SurfaceType.Slope:
                // 坂は床扱いにはしない。
                // 最近接触した壁寄りの面として保持する。
                recentWallNormal = normal;
                wallContactTimer = bounceParam.contactMemoryTime;
                break;



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

    private bool HasRecentGroundContact()
    {
        return groundContactTimer > 0f;
    }

    #endregion

    #region 重力・移動

    private void ApplyGravity(float dt)
    {
        if (isStickyStuck)
            return;


        if (HasRecentGroundContact() &&
            velocity.magnitude <= hitParam.idleLayerReturnSpeed &&
            Mathf.Abs(verticalVelocity) <= hitParam.groundedHitReceiveVerticalAllowance)
        {
            verticalVelocity = -motionParam.groundStickForce;
            return;
        }

        if (HasRecentGroundContact() && !(nowState is Airborne && verticalVelocity > 0f))
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -motionParam.groundStickForce;
        }
        else
        {
            verticalVelocity -= motionParam.gravity * dt;
        }
    }

    private void ApplyMovement(float dt)
    {
        if (isStickyStuck)
            return;

        Vector3 move = velocity;

        move.y = verticalVelocity;

        if (wallDetachTimer > 0f && IsFlying)
        {
            move += recentWallNormal * 2.0f;
        }

        Vector3 beforePosition = transform.position;

        // Move前の垂直速度を保存しておく。
        // Move後だと床接触処理や補正で値が変わっている可能性がある。
        float verticalBeforeMove = verticalVelocity;

        // CharacterController の衝突結果を取得する
        CollisionFlags flags = controller.Move(move * dt);

        Vector3 actualDelta = transform.position - beforePosition;

        // CharacterController の Below でも床バウンスを拾う。
        // Trigger通知が抜けたフレームでも床反射できるようにする。
        bool bouncedByControllerFloor =
            TryControllerFloorBounce(verticalBeforeMove, flags);



        if (!bouncedByControllerFloor && (flags & CollisionFlags.Below) != 0)
        {
            recentGroundNormal = Vector3.up;
            groundContactTimer = bounceParam.contactMemoryTime;
        }



        // バウンス直後は張り付き復帰判定を走らせない
        if (!bouncedByControllerFloor)
        {
            UpdateStuckRecovery(dt, move, actualDelta, flags);
        }
    }

    /// <summary>
    /// 他の吹き飛びオブジェクト上に乗ったり、密集状態で押し止められて、
    /// 見た目は止まっているのに Airborne / GroundMove から戻れない場合の保険処理。
    /// 
    /// 壁・床反射の処理は変更せず、CharacterController.Move の実移動量を見て、
    /// 「動こうとしているのに実際にはほぼ動けていない」状態が少し続いたら Idle に戻す。
    /// </summary>
    private void UpdateStuckRecovery(
        float dt,
        Vector3 requestedMove,
        Vector3 actualDelta,
        CollisionFlags flags)
    {
        if (nowState is Idle)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        if (dt <= 0f)
            return;

        float actualSpeed = actualDelta.magnitude / dt;

        bool almostNotMovingInWorld = actualSpeed <= 0.03f;
        bool lowHorizontalSpeed = velocity.magnitude <= motionParam.minStopSpeed;

        bool hitBelow = (flags & CollisionFlags.Below) != 0;
        bool hitSide = (flags & CollisionFlags.Sides) != 0;

        // 通常ケース：
        // 何かに乗っている、または横から押し止められていて、実移動がほぼ無い
        bool blockedByCollision =
            (hitBelow || hitSide) &&
            almostNotMovingInWorld &&
            lowHorizontalSpeed;

        // 保険ケース：
        // CollisionFlags が取れなくても、下に動こうとしているのに実際には動いていない
        // CharacterController同士や複雑な重なりで起こることがある
        bool suspiciousAirStop =
            flags == CollisionFlags.None &&
            requestedMove.y < -0.001f &&
            almostNotMovingInWorld &&
            lowHorizontalSpeed;

        if (!blockedByCollision && !suspiciousAirStop)
        {
            stuckRecoverTimer = 0f;
            return;
        }

        stuckRecoverTimer += dt;

        if (stuckRecoverTimer < motionParam.stopConfirmTime)
            return;

        // ここまで来たら「物理的には止まっている」とみなして強制復帰
        velocity = Vector3.zero;
        verticalVelocity = 0f;

        groundContactTimer = bounceParam.contactMemoryTime;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;
        wallDetachTimer = 0f;

        stopTimer = 0f;
        idleReadyTimer = 0f;
        stuckRecoverTimer = 0f;

        reserveState = new Idle();
        reservePriority = int.MaxValue;
        ApplyReserve();

        SyncRuntimeState();
    }

    #endregion

    #region 反射処理

    private void PlaySurfaceBounceEffect(int effectIndex, Vector3 normal, Vector3 moveDir)
    {
        if (effectPlayer == null)
            return;

        if (effectIndex < 0)
            return;

        if (normal.sqrMagnitude < 0.0001f)
            return;

        normal.Normalize();

        Vector3 position = GetSurfaceEffectPosition(normal);
        Quaternion rotation = CreateSurfaceEffectRotation(normal, moveDir);

        EffectPlayParam param = EffectPlayParam.Default;
        param.rotationOffset = bounceEffect.rotationOffset;
        param.scale = bounceEffect.scale;

        // PlayAtは追従しないので、壁・天井に貼り付いた一瞬のエフェクト向きとして使いやすい
        effectPlayer.PlayAt(
            effectIndex,
            position,
            rotation,
            Vector3.one,
            param
        );
    }

    private Vector3 GetSurfaceEffectPosition(Vector3 normal)
    {
        Vector3 center = controller != null
            ? controller.bounds.center
            : transform.position;

        float radius = controller != null
            ? controller.radius
            : 0.5f;

        // normal は「面からオブジェクト側へ向く法線」として扱う。
        // なので center - normal * radius で面側へ寄せる。
        return center - normal.normalized * radius + normal.normalized * bounceEffect.surfaceOffset;
    }

    private Quaternion CreateSurfaceEffectRotation(Vector3 normal, Vector3 moveDir)
    {
        normal.Normalize();

        // エフェクトの面を壁/天井に平行にする。
        // 前提：Prefabのローカル +Z が面法線方向。
        // つまり、PrefabのXY平面が壁/天井面に乗る。
        Vector3 tangent = Vector3.ProjectOnPlane(moveDir, normal);

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.ProjectOnPlane(Vector3.up, normal);

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);

        tangent.Normalize();

        return Quaternion.LookRotation(normal, tangent);
    }

    private void TryBounce(SurfaceType type, Vector3 normal)
    {
        if (bounceParam.blockMultiBouncePerFrame && lastBounceFrame == Time.frameCount)
            return;

        switch (type)
        {
            case SurfaceType.Wall:
                BounceWall(normal);
                break;

            case SurfaceType.Slope:
                BounceSlope(normal);
                break;

            case SurfaceType.Ground:
                BounceFloor(normal);
                break;

            case SurfaceType.Ceiling:
                BounceCeiling(normal);
                break;
        }

        lastBounceFrame = Time.frameCount;
    }

    private void BounceWall(Vector3 normal)
    {
        Vector3 totalVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);

        if (totalVelocity.sqrMagnitude <= 0.0001f)
            return;

        if (!IsMovingIntoSurface(normal, bounceParam.minWallImpactSpeed))
            return;

        // 壁反射は水平法線に寄せる。角・段差で上方向に暴れるのを抑える。
        Vector3 wallNormal = new Vector3(normal.x, 0f, normal.z);

        if (wallNormal.sqrMagnitude < 0.0001f)
            wallNormal = normal;

        wallNormal.Normalize();

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        Vector3 reflectedHorizontal = Vector3.Reflect(horizontalVelocity, wallNormal);

        velocity = reflectedHorizontal * bounceParam.wallBouncePower;

        // 壁で縦速度を過剰に増やさない
        verticalVelocity *= bounceParam.wallBouncePower;

        ClampCurrentVelocityForBounce();

        AddWallBounceSpin(new Vector3(velocity.x, verticalVelocity, velocity.z));

        wallDetachTimer = 0.08f;
        recentWallNormal = wallNormal;

        DamageHP(hpParam.damageOnWallBounce);

        PlaySurfaceBounceEffect(
            bounceEffect.wallEffectIndex,
            wallNormal,
            GetTotalVelocity()
        );
    }

    private void BounceSlope(Vector3 normal)
    {
        Vector3 totalVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);

        if (totalVelocity.sqrMagnitude <= 0.0001f)
            return;

        if (!IsMovingIntoSurface(normal, bounceParam.minSlopeImpactSpeed))
            return;

        Vector3 reflected = Vector3.Reflect(totalVelocity, normal.normalized);

        velocity = new Vector3(reflected.x, 0f, reflected.z) * bounceParam.slopeBouncePower;
        verticalVelocity = reflected.y * bounceParam.slopeBouncePower;

        ClampCurrentVelocityForBounce();

        AddWallBounceSpin(GetTotalVelocity());

        recentWallNormal = normal;
        wallDetachTimer = 0.03f;
    }

    private void BounceFloor(Vector3 normal)
    {
        if (-verticalVelocity >= bounceParam.minFloorBounceSpeed)
        {

            verticalVelocity = -verticalVelocity * bounceParam.floorBouncePower;
            velocity = Vector3.ProjectOnPlane(velocity, normal);


            // 床バウンス直後に接地記憶を残すと、
            // 次フレームでGroundMove化して跳ねが潰れることがある。
            groundContactTimer = 0f;
            wallContactTimer = 0f;
            ceilingContactTimer = 0f;

            stopTimer = 0f;
            idleReadyTimer = 0f;
            stuckRecoverTimer = 0f;

            if (bounceParam.floorBounceDetachDistance > 0f)
            {
                controller.Move(Vector3.up * bounceParam.floorBounceDetachDistance);
            }

            Reserve(new Airborne(), 100);
        }
        else
        {
            velocity = Vector3.ProjectOnPlane(velocity, normal);

            if (velocity.magnitude <= motionParam.snapStopSpeed * 1.5f)
                velocity = Vector3.zero;
        }
    }

    private void BounceCeiling(Vector3 normal)
    {
        if (verticalVelocity < bounceParam.minCeilingBounceSpeed)
            return;

        if (!IsMovingIntoSurface(normal, bounceParam.minCeilingBounceSpeed))
            return;

        verticalVelocity = -verticalVelocity * bounceParam.ceilingBouncePower;

        ClampCurrentVelocityForBounce();

        AddCeilingBounceSpin();

        DamageHP(hpParam.damageOnCeilingBounce);

        Vector3 effectDir = GetTotalVelocity();

        if (effectDir.sqrMagnitude < 0.0001f)
            effectDir = Vector3.down;

        PlaySurfaceBounceEffect(
            bounceEffect.ceilingEffectIndex,
            normal,
            effectDir
        );
    }

    private bool TryControllerFloorBounce(float verticalBeforeMove, CollisionFlags flags)
    {
        if (!bounceParam.useControllerBelowForFloorBounce)
            return false;

        if ((flags & CollisionFlags.Below) == 0)
            return false;

        if (!IsFlying)
            return false;

        // GroundMove中は通常の地面移動に任せる
        if (nowState is GroundMove)
            return false;

        // 落下速度が足りないなら床反射しない
        if (-verticalBeforeMove < bounceParam.minFloorBounceSpeed)
            return false;

        // Trigger側で同フレームにすでに反射しているなら二重反射しない
        if (bounceParam.blockMultiBouncePerFrame &&
            lastBounceFrame == Time.frameCount)
        {
            return false;
        }

        recentGroundNormal = Vector3.up;

        verticalVelocity = -verticalBeforeMove * bounceParam.floorBouncePower;
        velocity = Vector3.ProjectOnPlane(velocity, Vector3.up);

        // バウンス直後は接地記憶を残さない。
        // これを残すと次フレームでGroundMove化して跳ねが潰れることがある。
        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;

        stopTimer = 0f;
        idleReadyTimer = 0f;
        stuckRecoverTimer = 0f;

        if (bounceParam.floorBounceDetachDistance > 0f)
        {
            controller.Move(Vector3.up * bounceParam.floorBounceDetachDistance);
        }

        Reserve(new Airborne(), 100);

        lastBounceFrame = Time.frameCount;

        return true;
    }

    #endregion

    #region 回転
    private void ApplyRotation(float dt)
    {
        if (isStickyStuck)
            return;

        float drag = (nowState is GroundMove || nowState is Idle)
            ? rotationParam.groundSpinDrag
            : rotationParam.airSpinDrag;


        if (velocity.magnitude <= rotationParam.spinStopMoveSpeed && HasRecentGroundContact())
        {
            drag += rotationParam.lowSpeedExtraSpinDrag;
        }

        float speedRate = Mathf.Clamp01(velocity.magnitude / 3f);
        drag += (1f - speedRate) * rotationParam.lowSpeedExtraSpinDrag;

        spinSpeed = DampScalar(spinSpeed, drag, dt);

        if (velocity.magnitude <= rotationParam.spinStopMoveSpeed &&
            HasRecentGroundContact() &&
            spinSpeed <= rotationParam.snapStopSpinSpeed)
        {
            spinSpeed = 0f;
        }

        if (spinSpeed > rotationParam.maxSpinSpeed)
            spinSpeed = rotationParam.maxSpinSpeed;

        if (spinSpeed > 0.01f && spinAxis.sqrMagnitude > 0.0001f)
        {
            transform.rotation =
                Quaternion.AngleAxis(spinSpeed * dt, spinAxis) *
                transform.rotation;
        }
    }

    private void AddInitialSpin(Vector3 horizontalDir, float power)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, horizontalDir);
        if (axis.sqrMagnitude < 0.001f)
            axis = transform.right;

        axis.Normalize();
        spinAxis = axis;

        float spin = rotationParam.blowSpinSpeed * Mathf.Clamp01(power * 0.05f);
        float twist = Random.Range(-rotationParam.blowTwistSpeed, rotationParam.blowTwistSpeed);

        spinSpeed += spin + Mathf.Abs(twist);
        ClampSpinSpeed();
    }

    private void AddWallBounceSpin(Vector3 reflectedDir)
    {
        Vector3 flatDir = new Vector3(reflectedDir.x, 0f, reflectedDir.z);
        if (flatDir.sqrMagnitude < 0.001f) return;

        flatDir.Normalize();

        Vector3 axis = Vector3.Cross(Vector3.up, flatDir);
        if (axis.sqrMagnitude < 0.001f)
            axis = transform.right;

        axis.Normalize();
        spinAxis = axis;
        spinSpeed += rotationParam.bounceSpinAdd;

        ClampSpinSpeed();
    }

    private void AddCeilingBounceSpin()
    {
        Vector3 moveDir = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
        Vector3 axis = Vector3.Cross(Vector3.up, moveDir);

        if (axis.sqrMagnitude < 0.001f)
            axis = transform.right;

        axis.Normalize();
        spinAxis = axis;
        spinSpeed += rotationParam.bounceSpinAdd;

        ClampSpinSpeed();
    }

    private void ClampSpinSpeed()
    {
        if (spinSpeed > rotationParam.maxSpinSpeed)
            spinSpeed = rotationParam.maxSpinSpeed;
    }

    #endregion

    #region 速度補助

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

    private float DampScalar(float value, float drag, float dt)
    {
        return Mathf.MoveTowards(value, 0f, drag * dt);
    }

    private Vector3 ProjectVelocityToGround(Vector3 v)
    {
        if (!HasRecentGroundContact())
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
        {
            velocity = velocity.normalized * bounceParam.maxBounceHorizontalSpeed;
        }

        verticalVelocity = Mathf.Clamp(
            verticalVelocity,
            -bounceParam.maxBounceVerticalSpeed,
            bounceParam.maxBounceVerticalSpeed
        );
    }

    private bool IsMovingIntoSurface(Vector3 normal, float minImpactSpeed)
    {
        Vector3 totalVelocity = GetTotalVelocity();

        if (totalVelocity.sqrMagnitude < 0.0001f)
            return false;

        float intoSpeed = Vector3.Dot(totalVelocity, -normal.normalized);

        return intoSpeed >= minImpactSpeed;
    }

    #endregion

    #region タイプ別ヘルパー

    private TackleReactionParam GetReaction(TackleType tackleType)
    {
        TackleReactionSet set = GetCurrentReactionSet();

        switch (tackleType)
        {
            case TackleType.Normal:
                return set.normal;

            case TackleType.Charge:
                return set.charge;

            case TackleType.JustNormal:
                return set.justNormal;

            case TackleType.JustCharge:
                return set.justCharge;
        }

        return set.normal;
    }

    private TackleReactionSet GetCurrentReactionSet()
    {
        switch (objectType)
        {
            case BlowObjectType.Light:
                return lightObject.reactions;

            case BlowObjectType.Heavy:
                return heavyObject.reactions;

            case BlowObjectType.Bomb:
                return bombObject.reactions;

            case BlowObjectType.Sticky:
                return stickyObject.reactions;
        }

        return lightObject.reactions;
    }

    private bool CanCurrentTypeEmitChain()
    {
        TackleReactionParam reaction = GetReaction(lastReceivedTackleType);
        return reaction != null && reaction.canEmitChain;
    }
    private bool IsBombType()
    {
        return objectType == BlowObjectType.Bomb;
    }

    private bool IsStickyType()
    {
        return objectType == BlowObjectType.Sticky;
    }
    private ChainReceiveReactionParam GetChainReceiveReaction()
    {
        switch (objectType)
        {
            case BlowObjectType.Light:
                return lightObject.chainReceiveReaction;

            case BlowObjectType.Heavy:
                return heavyObject.chainReceiveReaction;

            case BlowObjectType.Bomb:
                return bombObject.chainReceiveReaction;

            case BlowObjectType.Sticky:
                return stickyObject.chainReceiveReaction;
        }

        return lightObject.chainReceiveReaction;
    }

    private bool CanCurrentTypeBreakWall()
    {
        switch (objectType)
        {
            case BlowObjectType.Light:
                return lightObject.canBreakWall;

            case BlowObjectType.Heavy:
                return heavyObject.canBreakWall;

            case BlowObjectType.Bomb:
                return bombObject.canBreakWall;

            case BlowObjectType.Sticky:
                return stickyObject.canBreakWall;
        }

        return false;
    }

    #endregion

    #region 吹き飛び適用


    private void ApplyBlow(BlowPayload blow)
    {

        lastReceivedTackleType = blow.tackleType;

        isStickyStuck = false;

        if (controller != null)
        {
            controller.enabled = true;
            controller.excludeLayers = hitParam.ignoreLayerAir;
        }


        TackleReactionParam reaction = GetReaction(blow.tackleType);
        currentCanEmitChain = reaction != null && reaction.canEmitChain;

        Vector3 dir = blow.powerDirection;
        float power = blow.powerConstant * blow.powerRate;

        Vector3 horizontal = new Vector3(dir.x, 0f, dir.z);

        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = transform.forward;

        horizontal.Normalize();

        float finalPower =
            power *
            reaction.horizontalPowerRate *
            motionParam.blowPowerMultiplier /
            Mathf.Max(motionParam.mass, 0.01f);


        velocity += horizontal * finalPower;

        float upward =
            Mathf.Max(
                finalPower * 0.25f * reaction.upwardPowerRate,
                motionParam.minUpwardPower * reaction.upwardPowerRate
            );

        verticalVelocity = Mathf.Max(verticalVelocity, upward);


        AddInitialSpin(horizontal, finalPower);

        stopTimer = 0f;
        idleReadyTimer = 0f;
        wallDetachTimer = 0f;
        stuckRecoverTimer = 0f;


        // 吹き飛ばし直後は古い接地・壁接触を捨てる。
        // Stickyが初期接地を拾って即くっつく問題の対策にもなる。
        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;



        if (IsStickyType())
        {
            isStickyStuck = false;
            stickyIgnoreTimer = Mathf.Max(stickyObject.initialStickIgnoreTime, 0f);
            stickyLeftInitialGround = false;
        }


        if (IsBombType())
        {
            isBombExploding = false;
            bombIgnoreTimer = Mathf.Max(bombObject.initialExplosionIgnoreTime, 0f);
            bombLeftInitialGround = false;

            if (bombObject.explosionCollider != null)
                bombObject.explosionCollider.SetActive(false);
        }



        DamageHP(blow.damageConstant * reaction.damageRate);

        Reserve(new Airborne(), 100);
    }

    #endregion

    #region 連鎖処理

    private bool IsChainColliderObject(GameObject obj)
    {
        if (chainParam.chainCollider == null || obj == null)
            return false;

        if (obj == chainParam.chainCollider)
            return true;

        return obj.transform.IsChildOf(chainParam.chainCollider.transform);
    }

    private bool IsExplosionColliderObject(GameObject obj)
    {
        if (bombObject.explosionCollider == null || obj == null)
            return false;

        if (obj == bombObject.explosionCollider)
            return true;

        return obj.transform.IsChildOf(bombObject.explosionCollider.transform);
    }

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


    private bool CanEmitChain(Hitbox selfHitbox)
    {
        if (selfHitbox == null)
            return false;

        if (!IsChainColliderObject(selfHitbox.gameObject))
            return false;

        if (chainParam.chainCollider == null || !chainParam.chainCollider.activeInHierarchy)
            return false;

        if (remainingChainCount <= 0)
            return false;

        if (lastChainFrame == Time.frameCount)
            return false;


        if (!IsFlying)
            return false;

        // 爆弾は通常の連鎖Colliderでは連鎖を発火しない。
        // 爆発Colliderを実質的な攻撃判定として使う。
        if (IsBombType())
            return false;


        if (!currentCanEmitChain)
            return false;



        if (isStickyStuck)
            return false;


        Vector3 totalVelocity = GetTotalVelocity();
        Vector3 horizontalVelocity = new Vector3(totalVelocity.x, 0f, totalVelocity.z);

        // 横速度か全体速度のどちらかが条件を満たせば連鎖可能
        // 密集状態で縦方向に跳ねているだけの瞬間も拾いやすくする
        float chainSpeed = Mathf.Max(horizontalVelocity.magnitude, totalVelocity.magnitude);

        if (chainSpeed < chainParam.minChainSpeed)
            return false;

        return true;
    }


    private bool TryGetHitReceiver(Collider other, out IHitReceiver receiver, out GameObject receiverObject)
    {
        receiver = null;
        receiverObject = null;

        if (other == null)
            return false;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return false;

        receiver = other.GetComponentInParent<IHitReceiver>();
        if (receiver == null)
            return false;

        Component receiverComponent = receiver as Component;
        receiverObject = receiverComponent != null ? receiverComponent.gameObject : other.gameObject;

        if (receiverObject == gameObject)
            return false;

        return true;
    }

    private Vector3 GetChainDirectionTo(GameObject targetObject)
    {
        Vector3 dir = targetObject.transform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
        {
            Vector3 totalVelocity = GetTotalVelocity();
            dir = new Vector3(totalVelocity.x, 0f, totalVelocity.z);

            if (dir.sqrMagnitude < 0.001f)
                dir = transform.forward;
        }

        return dir.normalized;
    }

    private void ApplyChainReaction(Vector3 horizontalDir, float horizontalPower, float verticalPower, float damage)
    {
        Vector3 horizontal = new Vector3(horizontalDir.x, 0f, horizontalDir.z);

        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = transform.forward;

        horizontal.Normalize();

        velocity = horizontal * horizontalPower;
        verticalVelocity = Mathf.Max(verticalPower, motionParam.minUpwardPower);

        spinAxis = Vector3.Cross(Vector3.up, horizontal);
        if (spinAxis.sqrMagnitude < 0.001f)
            spinAxis = transform.right;

        spinAxis.Normalize();
        spinSpeed = Mathf.Clamp(rotationParam.blowSpinSpeed, 0f, rotationParam.maxSpinSpeed);

        stopTimer = 0f;
        idleReadyTimer = 0f;
        wallDetachTimer = 0f;
        stuckRecoverTimer = 0f;

        // 連鎖直後は古い接触記憶を捨てる
        // これをしないと、直前の地面・壁・天井判定を引きずって詰まりやすい
        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;

        DamageHP(damage);

        if (effectPlayer)
            effectPlayer.Play(0);

        Reserve(new Airborne(), 100);
    }

    private void ApplyReceivedChainReaction(ChainPayload chain)
    {
        ChainReceiveReactionParam reaction = GetChainReceiveReaction();

        if (reaction == null)
            return;

        if (!reaction.canReceiveChain)
            return;

        isStickyStuck = false;

        if (controller != null)
        {
            controller.enabled = true;
            controller.excludeLayers = hitParam.ignoreLayerAir;
        }

        currentCanEmitChain = reaction.canEmitChainAfterReceived;

        float horizontalPower =
            chain.horizontalPower *
            reaction.horizontalPowerRate /
            Mathf.Max(motionParam.mass, 0.01f);

        float verticalPower =
            chain.verticalPower *
            reaction.verticalPowerRate;

        float damage =
            chain.damage *
            reaction.damageRate;

        ApplyChainReaction(
            chain.direction,
            horizontalPower,
            verticalPower,
            damage
        );
    }

    private void EmitChainTo(IHitReceiver receiver, GameObject receiverObject , Collider other)
    {
        if (receiver == null || receiverObject == null)
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

        Vector3 directionToTarget = GetChainDirectionTo(receiverObject);

        lastChainFrame = Time.frameCount;
        ConsumeChainCount();

        ApplyChainReaction(
            -directionToTarget,
            chainParam.selfHorizontalPower,
            chainParam.selfVerticalPower,
            chainParam.selfDamage
        );

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

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
            targetObject = receiverObject,
            targetHitbox = targetHitbox.gameObject,
            payload = payload
        };

        receiver.OnHit(hit);

        if (ChainHitManager.Instance != null)
            ChainHitManager.Instance.NotifyChain(chainIndex);
    }

    #endregion

    #region ステート管理

    private void Reserve(State s, int priority)
    {
        if (priority < reservePriority) return;
        reserveState = s;
        reservePriority = priority;
    }

    private void ApplyReserve()
    {
        if (reserveState == null) return;

        nowState?.Exit();
        nowState = reserveState;
        nowState.Set(this);
        reserveState = null;
        nowState.Enter();

        SyncRuntimeState();
    }

    #endregion

    #region 爆弾処理

    private void ExplodeBomb()
    {
        if (!IsBombType())
            return;

        if (isBombExploding)
            return;

        isBombExploding = true;
        explosionHitTargetIds.Clear();

        bombExplosionTimer = Mathf.Max(bombObject.explosionActiveTime, 0f);

        velocity = Vector3.zero;
        verticalVelocity = 0f;
        spinSpeed = 0f;

        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;
        wallDetachTimer = 0f;

        if (bombObject.explosionCollider != null)
            bombObject.explosionCollider.SetActive(true);

        if (effectPlayer != null && bombObject.explosionEffectIndex >= 0)
            effectPlayer.Play(bombObject.explosionEffectIndex);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(false);

        SetHitReceiveEnabled(false);

        reserveState = new Idle();
        reservePriority = int.MaxValue;
        ApplyReserve();
    }

    private void UpdateBombState(float dt)
    {
        if (!IsBombType())
            return;

        if (bombIgnoreTimer > 0f)
        {
            bombIgnoreTimer -= dt;

            if (bombIgnoreTimer < 0f)
                bombIgnoreTimer = 0f;
        }

        if (!bombLeftInitialGround)
        {
            if (!HasRecentGroundContact() ||
                verticalVelocity >= bombObject.leaveGroundVerticalSpeed)
            {
                bombLeftInitialGround = true;
            }
        }
    }

    private void UpdateBombExplosion(float dt)
    {
        if (!isBombExploding)
            return;

        bombExplosionTimer -= dt;

        if (bombExplosionTimer > 0f)
            return;

        bombExplosionTimer = 0f;
        isBombExploding = false;
        explosionHitTargetIds.Clear();

        if (bombObject.explosionCollider != null)
            bombObject.explosionCollider.SetActive(false);

        if (bombObject.disableSelfAfterExplosion)
        {
            DestroyByExplosion();
        }
    }

    private void DestroyByExplosion()
    {
        if (isDestroying)
            return;

        isDestroying = true;

        SetHitReceiveEnabled(false);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(false);

        if (bombObject.explosionCollider != null)
            bombObject.explosionCollider.SetActive(false);

        if (controller != null)
            controller.enabled = false;

        Destroy(gameObject);
    }

    private bool TryHandleExplosionHit(Hitbox selfHitbox, Collider other)
    {
        if (!IsBombType())
            return false;

        if (!isBombExploding)
            return false;

        if (selfHitbox == null || other == null)
            return false;

        if (!IsExplosionColliderObject(selfHitbox.gameObject))
            return false;

        if (!TryGetHitReceiver(other, out IHitReceiver receiver, out GameObject receiverObject))
            return false;

        return EmitExplosionHitTo(receiver, receiverObject);
    }
    private bool CanExplodeOnSurface(SurfaceType surfaceType)
    {
        if (!IsBombType())
            return false;

        if (isBombExploding)
            return false;

        if (bombIgnoreTimer > 0f)
            return false;

        if (bombObject.ignoreGroundUntilLeaveInitialGround &&
            surfaceType == SurfaceType.Ground &&
            !bombLeftInitialGround)
        {
            return false;
        }

        return true;
    }

    private bool EmitExplosionHitTo(IHitReceiver receiver, GameObject receiverObject)
    {
        if (receiver == null || receiverObject == null)
            return false;

        int targetId = receiverObject.GetInstanceID();

        if (explosionHitTargetIds.Contains(targetId))
            return true;

        int chainIndex = 1;

        if (ChainHitManager.Instance != null)
        {
            if (!ChainHitManager.Instance.TryBeginChainPair(
                    gameObject,
                    receiverObject,
                    bombObject.explosionPairCooldown,
                    out chainIndex))
            {
                return true;
            }
        }

        explosionHitTargetIds.Add(targetId);

        Vector3 dir = receiverObject.transform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        dir.Normalize();

        ChainPayload payload = new ChainPayload
        {
            source = this,
            chainIndex = chainIndex,
            direction = dir,
            horizontalPower = bombObject.explosionHorizontalPower,
            verticalPower = bombObject.explosionVerticalPower,
            damage = bombObject.explosionDamage
        };

        HitEventData hit = new HitEventData
        {
            attackerObject = gameObject,
            targetObject = receiverObject,
            payload = payload
        };

        receiver.OnHit(hit);

        if (ChainHitManager.Instance != null)
            ChainHitManager.Instance.NotifyChain(chainIndex);

        return true;
    }
    private bool TryHandleBombChainContactExplosion(Hitbox selfHitbox, Collider other)
    {
        if (!IsBombType())
            return false;

        if (!bombObject.explodeOnChainContact)
            return false;

        if (isBombExploding)
            return false;

        if (selfHitbox == null || other == null)
            return false;

        // 通常連鎖Colliderで触れた時だけ爆発へ変換する
        if (!IsChainColliderObject(selfHitbox.gameObject))
            return false;

        if (!IsFlying)
            return false;

        Debug.Log("爆発処理中盤");

        Vector3 totalVelocity = GetTotalVelocity();
        Vector3 horizontalVelocity = new Vector3(totalVelocity.x, 0f, totalVelocity.z);

        float contactSpeed = Mathf.Max(
            horizontalVelocity.magnitude,
            totalVelocity.magnitude
        );

        if (contactSpeed < bombObject.chainContactExplosionMinSpeed)
            return false;

        if (!TryGetHitReceiver(other, out IHitReceiver receiver, out GameObject receiverObject))
            return false;

        // 通常連鎖ではなく爆発する
        ExplodeBomb();

        // 爆発ColliderのOnTriggerEnterが接触相手を拾えないケースがあるので、
        // 接触した相手には即時で爆発ヒットを保証する。
        if (bombObject.hitContactTargetImmediatelyOnExplosion)
        {
            EmitExplosionHitTo(receiver, receiverObject);
        }

        return true;
    }
    #endregion

    #region 粘着処理

    private void StickToSurface(Vector3 normal, Collider other, float pushDistance)
    {
        if (!IsStickyType())
            return;

        if (isStickyStuck)
            return;

        isStickyStuck = true;

        normal.Normalize();

        // ComputePenetration の normal は「押し出す方向」。
        // その方向へ pushDistance + offset だけ逃がして、壁から少し浮かせて固定する。
        transform.position += normal * (pushDistance + stickyObject.stickSurfaceOffset);

        velocity = Vector3.zero;
        verticalVelocity = 0f;
        spinSpeed = 0f;


        groundContactTimer = 0f;
        wallContactTimer = 0f;
        ceilingContactTimer = 0f;
        wallDetachTimer = 0f;
        lastBounceFrame = Time.frameCount;


        stopTimer = 0f;
        idleReadyTimer = 0f;
        stuckRecoverTimer = 0f;

        if (stickyObject.disableChainColliderOnStick &&
            chainParam.chainCollider != null)
        {
            chainParam.chainCollider.SetActive(false);
        }

        if (stickyObject.enableHitReceiveOnStick)
        {
            SetHitReceiveEnabled(true);
        }



        if (controller != null)
        {
            controller.excludeLayers = hitParam.ignoreLayerIdle;
        }



        Reserve(new Idle(), int.MaxValue);
        ApplyReserve();

        SyncRuntimeState();
    }

    private void UpdateStickyState(float dt)
    {
        if (!IsStickyType())
            return;

        if (stickyIgnoreTimer > 0f)
        {
            stickyIgnoreTimer -= dt;

            if (stickyIgnoreTimer < 0f)
                stickyIgnoreTimer = 0f;
        }

        if (!stickyLeftInitialGround)
        {
            if (!HasRecentGroundContact() ||
                verticalVelocity >= stickyObject.leaveGroundVerticalSpeed)
            {
                stickyLeftInitialGround = true;
            }
        }
    }


    private bool CanStickToSurface(SurfaceType surfaceType, Collider other)
    {
        if (!IsStickyType())
            return false;

        if (isStickyStuck)
            return false;

        if (stickyIgnoreTimer > 0f)
            return false;

        if (other == null)
            return false;

        if (stickyObject.ignoreOtherBlowObjects)
        {
            BlowObjectController otherBlow =
                other.GetComponentInParent<BlowObjectController>();

            if (otherBlow != null && otherBlow != this)
                return false;
        }

        if (surfaceType == SurfaceType.Ground && !stickyObject.allowStickToGround)
            return false;

        if (stickyObject.ignoreGroundUntilLeaveInitialGround &&
            surfaceType == SurfaceType.Ground &&
            !stickyLeftInitialGround)
        {
            return false;
        }

        Vector3 totalVelocity = GetTotalVelocity();

        if (totalVelocity.magnitude < stickyObject.minStickSpeed)
            return false;

        // Noneは不可。基本は壁・坂・天井だけ。
        if (surfaceType == SurfaceType.None)
            return false;

        return true;
    }


    #endregion

    #region HP処理

    private void DamageHP(float damage)
    {
        if (!hpParam.destructible) return;
        if (currentHP <= 0f) return;
        if (isDestroying) return;

        currentHP -= damage;

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            hpZeroTimer = hpParam.disableDelay;
        }
    }

    private void UpdateHP(float dt)
    {
        if (isDestroying)
            return;

        if (hpZeroTimer < 0f)
            return;

        hpZeroTimer -= dt;

        if (hpZeroTimer <= 0f)
        {
            DestroyByHP();
        }
    }

    private void DestroyByHP()
    {
        if (isDestroying)
            return;

        isDestroying = true;

        SetHitReceiveEnabled(false);

        if (chainParam.chainCollider != null)
            chainParam.chainCollider.SetActive(false);

        if (bombObject.explosionCollider != null)
            bombObject.explosionCollider.SetActive(false);

        if (controller != null)
            controller.enabled = false;

        Destroy(gameObject);
    }

   

#endregion

    #region 当たり判定（インターフェース）

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        // 爆発中の爆発Colliderヒット
        if (TryHandleExplosionHit(selfHitbox, other))
            return;

        // 爆弾タイプの通常chainCollider接触は、
        // 通常連鎖ではなく爆発へ変換する
        if (TryHandleBombChainContactExplosion(selfHitbox, other))
            return;

        // 通常連鎖
        if (!CanEmitChain(selfHitbox))
            return;

        if (!TryGetHitReceiver(other, out IHitReceiver receiver, out GameObject receiverObject))
            return;

        EmitChainTo(receiver, receiverObject ,other);
    }

    public void OnHit(HitEventData data)
    {
        if (reHitTimer > 0f)
            return;

        reHitTimer = hitParam.reHitInterval;

        if (data.payload is BlowPayload blow)
        {
            ApplyBlow(blow);
            return;
        }

        if (data.payload is ChainPayload chain)
        {
            lastChainFrame = Time.frameCount;

            ApplyReceivedChainReaction(chain);

            return;
        }

    }

    #endregion
}