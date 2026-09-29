using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(TimeAgent))]
public class PlayerController : MonoBehaviour, IHitSource, IHitReceiver
{
    #region コンポーネント
    private CharacterController controller; // キャラクターコントローラー
    private TimeAgent agent; // 時間制御用
    private Animator animator;
    private AudioManager audioManager;
    private AfterimageRenderer afterimageRenderer;

    #endregion

    #region パラメータクラス（Inspector用）

    #region 移動用パラメーター

    [System.Serializable]
    private class MoveParam
    {
        [Tooltip("移動速度")] public float moveSpeed = 10f;
        [Tooltip("入力デッドゾーン")] public float deadZone = 0.1f;
        [Tooltip("移動加速度")] public float accel = 20f;
        [Tooltip("移動減速度")] public float decel = 25f;

        [Header("移動時エフェクト")]
        [Tooltip("左足エフェクト")] public Transform LEffect;
        [Tooltip("右足エフェクト")] public Transform REffect;
    }
    [SerializeField][Header("移動用パラメーター")] private MoveParam moveParam = new MoveParam();
    #endregion

    #region 通常移動RootMotionパラメーター

    [System.Serializable]
    private class RunRootMotionParam
    {
        [Tooltip("通常移動でRootMotionを使う")]
        public bool enable = true;

        [Tooltip("RootMotion移動倍率")]
        public float moveScale = 1f;

        [Tooltip("Y成分も使うか（通常移動ではOFF推奨）")]
        public bool useRootMotionY = false;

        [Tooltip("回転もRootMotionから適用するか（通常移動ではOFF推奨）")]
        public bool useRootMotionRotation = false;


        #region アニメーションベース移動切り替え

        [Header("アニメーションベース移動切り替え")]
        [Tooltip("将来のために処理は残すが、現状はOFFでスクリプト移動に固定する")]
        public bool useAnimationBasedMovement = false;

        #endregion

    }
    [SerializeField][Header("通常移動RootMotion")] private RunRootMotionParam runRootMotion = new RunRootMotionParam();

    #endregion

    #region 坂移動用パラメーター

    [System.Serializable]
    private class SlopeParam
    {
        [Tooltip("坂上昇")] public float slopeUpFactor = 0.6f;
        [Tooltip("坂下り抵抗力")] public float slopeDownFactor = 1.3f;
        [Tooltip("地面張り付きパワー")] public float groundStickForce = 5f;
    }
    [SerializeField][Header("坂移動用パラメーター")] private SlopeParam slopeParam = new SlopeParam();

    #endregion

    #region 壁判定パラメーター

    [System.Serializable]
    private class WallCheckParam
    {
        [Header("対象レイヤー")]
        [Tooltip("壁・床・天井判定に使うレイヤー")]
        public LayerMask Layer = ~0;

        [Header("タックル壁判定")]
        [Tooltip("タックル壁判定の半径")]
        public float Radius = 0.5f;

        [Tooltip("タックル壁判定の距離")]
        public float Distance = 0.8f;

        [Tooltip("タックル壁判定の高さ。CharacterController中心から上方向に足す")]
        public float wallCheckHeightOffset = 0.2f;


        [Header("天井判定")]
        [Tooltip("天井判定の半径。0以下ならCharacterController半径基準")]
        public float ceilingRadius = 0f;

        [Tooltip("天井判定の距離")]
        public float ceilingDistance = 0.25f;

        [Tooltip("天井判定の開始位置を少し下げる量")]
        public float ceilingStartDownOffset = 0.02f;

        [Tooltip("天井に当たった瞬間に下向きへ切り替える速度")]
        public float ceilingHitDownVelocity = 2.0f;

        [Tooltip("天井ヒット時に少し下へ押し戻す距離。張り付き防止用")]
        public float ceilingDetachDistance = 0.03f;


        [Header("接地補助判定")]
        [Tooltip("接地SphereCastの半径倍率")]
        public float groundRadiusRate = 0.95f;

        [Tooltip("接地SphereCastの追加距離")]
        public float groundExtraDistance = 0.12f;

        [Tooltip("接地判定として許容する追加傾斜角")]
        public float groundSlopeExtraAngle = 5f;

        [Header("ハイブリッド判定")]
        [Tooltip("CharacterController.MoveのCollisionFlagsも判定に使う")]
        public bool useControllerCollisionFlags = true;
    }
    [SerializeField][Header("壁判定")] private WallCheckParam WallCheck = new WallCheckParam();

    #endregion

    #region　ジャンプパラメーター

    [System.Serializable]
    private class JumpParam
    {
        [Tooltip("ジャンプ力")] public float jumpPower = 8f;
        [Tooltip("空中移動抵抗力")] public float airControl = 0.3f;
        [Tooltip("ジャンプ入力受付")] public float jumpBufferTime = 0.15f;
        [Tooltip("上昇時重力")] public float gravityUp = 15f;
        [Tooltip("下降時重力")] public float gravityDown = 30f;
        [Tooltip("コヨーテタイム(落下してもまだジャンプできる時間)")] public float coyoteTime = 0.1f;

        [Header("ジャンプ直後の接地無視")]
        [Tooltip("ジャンプ開始直後、この時間だけ接地判定を無視します。上昇中に床を拾って空中で接地扱いになる問題の対策です。")]
        public float jumpGroundIgnoreTime = 0.08f;

        [Tooltip("この速度以下になったら接地判定を許可します。0以下推奨です。正の値にすると上昇中に接地扱いになりやすいです。")]
        public float groundedCheckMaxUpVelocity = 0.0f;

        [Header("空中段差補正")]
        [Tooltip("ONなら空中ではCharacterControllerのStepOffsetを無効化する")]
        public bool disableStepOffsetWhileAirborne = true;

        [Tooltip("空中時のStepOffset")]
        public float airborneStepOffset = 0f;

        [Header("着地直後タックル補助")]
        [Tooltip("空中中に通常タックル入力を離した場合、着地後この時間だけ通常タックルを受け付けます。")]
        public float landingTackleBufferTime = 0.12f;


    }
    [SerializeField][Header("ジャンプパラメーター")] private JumpParam jumpParam = new JumpParam();

    #endregion

    #region タックル基本パラメーター

    [System.Serializable]
    private class TackleParam
    {
        [Tooltip("単押し判定閾値")]
        public float tapChargeTime = 0.03f;

        [Header("タックルアニメーション")]

        [Tooltip("通常タックルアニメーション数")]
        public int tackleAnimationCount = 4;

        [Tooltip("この時間以内に次の通常タックルを出したら連続タックル扱い")]
        public float tackleAnimationChainTime = 0.8f;

        [Header("タックル命中方向制限")]

        [Tooltip("ONなら、プレイヤー前方角度内の対象だけタックル命中候補にする")]
        public bool useFrontHitAngleLimit = true;

        [Tooltip("プレイヤー前方から左右何度まで命中を許可するか。90なら前方180度、60なら前方120度")]
        [Range(0f, 180f)]
        public float frontHitHalfAngle = 80f;

        [Header("タックル壁終了制御")]

        [Tooltip("タックル開始直後、この時間内は壁判定で終了しない")]
        public float wallEndIgnoreTime = 0.05f;

        [Tooltip("タックル開始後、この距離を進むまでは壁判定で終了しない")]
        public float wallEndIgnoreDistance = 0.15f;

        [Tooltip("タックルヒット候補がある場合、壁終了よりヒット処理を優先する")]
        public bool prioritizeHitBeforeWallEnd = true;

        [SerializeField]
        [Header("暴発補正")]
        [Tooltip("暴発時の速度倍率")]
        public float overheatSpeedRate = 0.6f;

        [SerializeField]
        [Tooltip("暴発時の時間倍率")]
        public float overheatDurationRate = 0.6f;
    }
    [SerializeField][Header("タックル基本パラメーター")] private TackleParam tackleParam = new TackleParam();

    #endregion

    #region 通常タックルパラメーター
    [System.Serializable]
    private class NormalTackle
    {
        [Tooltip("タックル時間")] public float tackleDuration = 0.5f;
        [Tooltip("タックルスピード")] public float tackleSpeed = 15f;
        [Tooltip("タックルクールタイム")] public float tackleCooldown = 0.3f;
        [Tooltip("タックルダメージ")] public float tackleDamage = 10.0f;

        [Tooltip("空中タックル可否")] public bool allowAirTackle = true;

        [Tooltip("タックル威力")] public float basePower = 10f;

        [Tooltip("威力カーブ（0→1）")] public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 1f);

        [Tooltip("通常タックルコライダーオブジェクト")] public GameObject tackleHitCollider;

        [Tooltip("ヒットストップ時間")] public float hitStopTime = 0.06f;

        [Tooltip("カメラシェイクパワー")] public float shakePower = 0.3f;
        [Tooltip("カメラシェイク時間")] public float shakeDuration = 0.2f;

        [Header("衝突制御")]

        [Tooltip("ONなら通常タックル中はCharacterControllerのSides判定で停止しない。坂・段差・オブジェクト端でタックルがキャンセルされるのを防ぐ")]
        public bool ignoreControllerSideWallStop = true;

        [Tooltip("ONなら通常タックル中もSphereCastによるステージ壁判定で停止する。OFFなら壁・坂・オブジェクトでは通常タックルを終了しない")]
        public bool stopOnStageWall = false;
    }
    [SerializeField][Header("通常タックルパラメーター")] private NormalTackle normalTackle = new NormalTackle();
    #endregion

    #region チャージタックルパラメーター

    [System.Serializable]
    private class ChargeTackle
    {
        [Tooltip("チャージ成立時間")] public float chargeTime = 0.5f;
        [Tooltip("タックル時間")] public float tackleDuration = 0.8f;
        [Tooltip("タックルスピード")] public float tackleSpeed = 20f;
        [Tooltip("タックルダメージ")] public float tackleDamage = 10.0f;
        [Tooltip("タックル威力")] public float basePower = 10f;
        [Tooltip("威力カーブ（0→1）")] public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 1f);
        [Tooltip("タックル中の移動倍率")] public float moveRate = 0.3f;

        [Header("衝突制御")]

        [Tooltip("ONならチャージタックル中はCharacterControllerのSides判定で停止しない。敵への物理接触で止まるのを防ぐ")]
        public bool ignoreControllerSideWallStop = true;

        [Tooltip("ONならチャージタックル中もステージ壁SphereCastでは停止する")]
        public bool stopOnStageWall = true;
    }
    [SerializeField][Header("チャージタックルパラメーター")] private ChargeTackle chargeTackle = new ChargeTackle();



    #endregion

    #region ジャストタックルパラメーター

    [System.Serializable]
    private class JustTackleParam
    {
        [Header("判定ウィンドウ")]

        [Tooltip("タックル開始からジャスト受付が有効な時間（秒）。60fps換算で0.1秒は約6フレーム")]
        public float activeTime = 0.12f;

        [Header("判定コライダー")]

        [Tooltip("ジャストタックル判定用コライダー")]
        public GameObject justTackleHitCollider;

        [Header("成功時効果")]

        [Tooltip("通常タックル威力倍率に対する追加倍率")]
        public float powerMultiplier = 1.8f;

        [Tooltip("ジャストタックル成功時の最低威力倍率。タックル開始直後でもこの倍率以上を保証する")]
        [Range(0f, 1f)]
        public float minPowerRate = 0.8f;

        [Tooltip("吹き飛ばし方向補正（0でカメラ前方、1でタックル方向）")]
        [Range(0f, 1f)]
        public float directionOverrideRate = 1.0f;

        [Tooltip("成功時ヒットストップ時間")]
        public float hitStopTime = 0.12f;

        [Tooltip("成功時カメラシェイク強度")]
        public float shakePower = 0.6f;

        [Tooltip("成功時カメラシェイク時間")]
        public float shakeDuration = 0.25f;

        [Header("時間演出")]

        [Tooltip("成功時にプレイヤーへかけるスロー倍率")]
        public float selfSlowScale = 0.1f;

        [Tooltip("成功時にプレイヤーへかけるスロー時間")]
        public float selfSlowTime = 0.2f;

        [Tooltip("成功時にゲーム全体へかけるスロー倍率")]
        public float worldSlowScale = 0.3f;

        [Tooltip("成功時にゲーム全体へかけるスロー時間")]
        public float worldSlowTime = 0.3f;
    }

    [SerializeField]
    [Header("ジャストタックルパラメーター")]
    private JustTackleParam justTackle = new JustTackleParam();

    #endregion

    #region カメラパラメーター

    private enum CameraRotationMode
    {
        // 現在の方式。
        // カメラYaw = Player本体Yaw。
        [Tooltip("カメラ回転＝プレイヤー回転")]
        PlayerYawCamera,

        // BotW系。
        // カメラYaw/PitchとPlayer本体Yawを分離する。
        // Playerは移動方向へ向く。
        [Tooltip("カメラ回転！＝プレイヤー回転")]
        SeparatedThirdPerson
    }

    private enum SeparatedTackleDirectionMode
    {
        [Tooltip("追加カメラモード中もカメラ前方へタックルします。")]
        CameraForward,

        [Tooltip("追加カメラモード中はプレイヤー本体の前方へタックルします。")]
        PlayerForward
    }


    [System.Serializable]
    private class CameraParam
    {
        [Header("カメラモード")]
        [Tooltip("PlayerYawCamera: 現在の方式。SeparatedThirdPerson: カメラとプレイヤー回転を分離する方式。")]
        public CameraRotationMode rotationMode = CameraRotationMode.PlayerYawCamera;

        [Tooltip("視点用子オブジェクト。PlayerYawCameraでは従来通り使用します。")]
        public Transform eye;

        [Tooltip("SeparatedThirdPerson用のカメラ回転ピボット。Playerの子ではなく、シーン上の独立Object推奨です。")]
        public Transform thirdPersonPivot;

        [Tooltip("SeparatedThirdPerson用のピボット位置オフセット。プレイヤー基準で頭付近に置きます。")]
        public Vector3 thirdPersonPivotOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("SeparatedThirdPerson中、移動入力方向へPlayerを回転させる")]
        public bool rotatePlayerByMoveDirection = true;

        [Tooltip("SeparatedThirdPerson中、Playerが移動方向へ向く回転速度。度/秒")]
        public float playerTurnSpeed = 720f;

        [Header("SeparatedThirdPerson タックル方向")]
        [Tooltip("SeparatedThirdPerson中の通常タックル/チャージタックル方向。CameraForwardならカメラ前方、PlayerForwardならプレイヤー前方。")]
        public SeparatedTackleDirectionMode separatedTackleDirectionMode =
            SeparatedTackleDirectionMode.CameraForward;

        [Header("チャージ中回転")]
        [Tooltip("ONならSeparatedThirdPerson中でもチャージ中だけ、カメラYawにPlayer本体Yawを同期します。")]
        public bool syncPlayerYawToCameraWhileCharging = true;

        [Tooltip("カメラオブジェクト")]
        public Transform cameraRoot;

        [Tooltip("カメラコントローラー")]
        public CameraController cameraController;

        [Tooltip("カメラ操作設定。設定画面と共有するScriptableObject")]
        public CameraControlSettings controlSettings;
    }

    [SerializeField][Header("カメラパラメーター")] CameraParam cameraParam = new CameraParam();

    #endregion

    #region HP / 被ダメパラメーター

    [System.Serializable]
    private class PlayerHPParam
    {
        [Header("通常被ダメ判定")]
        [Tooltip("通常被ダメ用の子コライダーオブジェクト。通常攻撃はこのHitboxに当たった時だけ被ダメします。")]
        public GameObject damageHitCollider;

        [Header("防御不可被ダメ判定")]
        [Tooltip("防御不可攻撃を受ける専用の子コライダーオブジェクト。ここに当たった攻撃はタックル/ジャスト/無敵補助を貫通します。")]
        public GameObject unblockableDamageHitCollider;

        [Tooltip("防御不可攻撃の連続多段ヒット防止用クールタイムです。0なら無効です。")]
        public float unblockableDamageCooldownTime = 0.15f;

        [Tooltip("ONなら防御不可攻撃はノックバック中/復帰中/復帰後無敵中でもダメージを通します。")]
        public bool unblockableIgnoresDamageState = true;

        [Header("ジャストタックル被ダメ保険")]
        [Tooltip("ジャストタックル成功直後、この時間だけ通常被ダメを無効化します。敵攻撃とジャスト成功が同フレーム付近で重なった時の保険です。")]
        public float justTackleDamageGuardTime = 0.12f;

        [Header("通常タックルヒット後の余韻")]
        [Tooltip("通常タックルが敵にヒットした後、即終了せずこの時間だけタックル移動を維持します。0なら即終了です。")]
        public float normalTacklePostHitKeepTime = 0.10f;

        [Header("ノックバック")]
        [Tooltip("ノックバック水平速度。knockbackTime の間、この速度で移動し続ける")]
        public float knockbackPower = 8f;

        [Tooltip("防御不可攻撃用のノックバック水平速度。0以下なら通常ノックバックを使います。")]
        public float unblockableKnockbackPower = 10f;

        [Tooltip("ノックバック移動が継続する時間")]
        public float knockbackTime = 0.25f;

        [Tooltip("防御不可攻撃用のノックバック時間。0以下なら通常ノックバック時間を使います。")]
        public float unblockableKnockbackTime = 0.3f;

        [Header("復帰")]
        [Tooltip("ノックバック終了後、操作可能に戻るまでの時間")]
        public float damageRecoverTime = 0.45f;

        [Header("復帰後無敵")]
        [Tooltip("復帰後に入る無敵時間。この間は通常被ダメ用コライダーをOFFにする")]
        public float postRecoverInvincibleTime = 0.5f;

        [Header("ハイパーアーマー")]
        [Tooltip("ONならタックル・チャージタックル中は通常攻撃ではノックバックしない。ただしダメージは受ける")]
        public bool enableTackleHyperArmor = true;

        [Header("死亡")]
        [Tooltip("死亡時にPlayer InputActionMapを無効化する")]
        public bool disableInputOnDeath = true;
    }

    [SerializeField]
    [Header("HP / 被ダメパラメーター")]
    private PlayerHPParam hpParam = new PlayerHPParam();

    #endregion

    #endregion

    #region メンバ変数

    #region 入力
    private InputActionMap playerInputMap;
    private InputAction moveInput;
    private InputAction jumpInput;
    private InputAction tackleInput;
    private InputAction lookInput;

    private Vector2 inputMoveDir;
    private bool jumpPressed;
    private Vector2 inputLook;

    private bool tackleDown;
    private bool tackleUp;
    private bool tackleHold;
    private float tacklePressTimer;
    private bool ignoreTackleUntilReleased = false;

    private Vector2 moveBlendDirection;
    #endregion

    #region ジャンプ管理
    private float jumpBufferTimer;
    private bool jumpRequest;

    // ジャンプ直後に床を拾わないための接地無視タイマー
    private float jumpGroundIgnoreTimer;
    #endregion

    #region 接地安定化

    // 安定した接地判定
    private bool isGroundedStable;

    // コヨーテタイム
    private float coyoteTimer;

    #endregion

    #region 移動

    private Vector3 currentVelocity;
    private float verticalVelocity;
    private float defaultStepOffset;

    #endregion

    #region ワープ管理

    private bool hasPendingWarp = false;
    private Vector3 pendingWarpPosition;
    private Quaternion pendingWarpRotation;
    private bool pendingWarpApplyRotation = false;
    private int pendingWarpDamage = 0;

    #endregion

    #region 足音制御

    [SerializeField]
    [Header("足音制御")]
    [Tooltip("足音の最小再生間隔")]
    private float footstepInterval = 0.18f;

    [SerializeField]
    [Tooltip("接地してからこの時間経つまで足音を鳴らさない。接地判定のカチカチ対策")]
    private float footstepGroundStableTime = 0.05f;

    [SerializeField]
    [Tooltip("地面を離れてすぐの短時間は足音を鳴らさない。ジャンプ直後や段差際の誤発音対策")]
    private float footstepBlockTimeAfterLeaveGround = 0.08f;

    [SerializeField]
    [Tooltip("使用する足音SE名")]
    private string footstepSeName = "PL_Walk";

    private float footstepTimer = 0f;
    private float groundedStableTimer = 0f;
    private float airborneTimer = 0f;

    #endregion

    #region リクエスト
    private struct MoveRequest
    {
        public Vector3 moveDir;
        //public Vector3 faceDir;
        public float speedRate;
        public void Clear()
        {
            moveDir = Vector3.zero;
            //faceDir = Vector3.zero;
            speedRate = 1f;
        }
    }
    private MoveRequest request;
    #endregion

    #region MotionPolicy

    private struct MotionPolicy
    {
        public bool useRootMotionPosition;
        public bool useRootMotionRotation;
        public bool useRootMotionY;
        public bool ignoreGravity;
        public float rootMotionScale;

        public static MotionPolicy Default => new MotionPolicy
        {
            useRootMotionPosition = false,
            useRootMotionRotation = false,
            useRootMotionY = false,
            ignoreGravity = false,
            rootMotionScale = 1f
        };
    }

    private MotionPolicy currentMotionPolicy;

    #endregion

    #region タックル管理
    private bool isTackling = false;
    private float tackleTimer = 0f;
    private Vector3 tackleDir;
    private float tackleCooldownTimer = 0f;
    private bool canAirTackle = false;

    private float tackleElapsedTimer = 0f;
    private float tackleMovedDistance = 0f;
    private float tackleStuckTimer = 0f;
    public bool IsTackling => isTackling;

    private int tackleAnimIndex = 0;
    private int nextNormalTackleAnimIndex = 0;
    private float lastNormalTackleEndTime = -999f;

    private bool didPlayShakeThisFrame = false;
    private float hitStopCooldownTimer = 0f;

    private float tackleTapBufferTimer = 0f;
    #endregion

    #region ジャストタックル管理

    private bool isJustTackleWindow = false;
    private bool justTackleSucceeded = false;
    private float justTackleTimer = 0f;

    #endregion

    #region タックルヒット候補管理

    private enum TackleHitKind
    {
        Normal,
        Just
    }

    private class TackleHitCandidate
    {
        public TackleHitKind kind;

        public Hitbox selfHitbox;
        public Hitbox targetHitbox;

        public IHitReceiver receiver;
        public MonoBehaviour receiverMono;

        public GameObject targetObject;
        public int targetId;

        public Vector3 contactPoint;

        public bool valid;
    }

    // このタックル中に検出された候補。
    // 毎フレームクリアしない。
    // タックル終了時にクリアする。
    private readonly List<TackleHitCandidate> tackleHitCandidates =
        new List<TackleHitCandidate>();

    // まだ解決していない候補の開始位置。
    private int tackleHitProcessIndex = 0;

    // 同一タックル中にすでに処理済みの対象。
    // 同じ敵へ多重ヒットしないために使う。
    private readonly HashSet<int> processedTackleTargetIds =
        new HashSet<int>();

    #endregion

    #region　チャージ

    private bool isCharging = false;
    private float chargeTimer = 0f;
    private bool isChargeTackle = false;

    #endregion

    #region ステート
    private PlayerState nowState;
    private PlayerState reserveState;
    private int reservePriority;
    #endregion

    #region エフェクト

    private EffectPlayer effectPlayer;
    private EffectInstance tackleEffect = null;
    private EffectBonePlayer effectBonePlayer;

    #endregion

    #region HP / 被ダメ管理

    // 復帰後無敵専用タイマー。
    // ノックバック中・復帰中には使わない。
    private float invincibleTimer = 0f;

    // ノックバック移動時間
    private float knockbackTimer = 0f;

    // ノックバック終了後の復帰硬直時間
    private float damageRecoverTimer = 0f;

    // ノックバック中に使う一定速度
    private Vector3 knockbackVelocity = Vector3.zero;

    // 被ダメフェーズ管理
    private bool isInKnockback = false;
    private bool isInRecover = false;
    private bool isInInvincible = false;

    // ジャストタックル成功直後の通常被ダメ無効タイマー。
    // 防御不可攻撃には効かない。
    private float justTackleDamageGuardTimer = 0f;

    // 防御不可攻撃の連続ヒット防止タイマー。
    private float unblockableDamageCooldownTimer = 0f;

    // 通常タックルヒット後、少しだけタックル移動を残している状態。
    // この間は追加ヒットしないようにタックルHitboxはOFFにする。
    private bool isNormalTacklePostHitKeeping = false;

    public int CurrentHP => PlayerHPManager.Instance.CurrentHP;
    public bool IsDead => PlayerHPManager.Instance.IsDead;

    #endregion

    #region カメラ回転補正

    private Vector2 smoothedLookInput;
    private Vector2 lookInputVelocity;

    private bool cameraRotationInitialized = false;
    private float currentCameraYaw;
    private float currentCameraPitch;

    // コントローラー用の現在回転速度。
    // マウスは基本的に速度ではなく、入力差分をそのまま角度へ反映する。
    private float currentControllerYawSpeed;
    private float currentControllerPitchSpeed;

    #endregion


    #endregion

    #region 初期化
    private void Awake()
    {


        controller = GetComponent<CharacterController>();
        defaultStepOffset = controller.stepOffset;

        animator = GetComponent<Animator>();
        agent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();
        effectBonePlayer = GetComponent<EffectBonePlayer>();
        afterimageRenderer = GetComponent<AfterimageRenderer>();

        animator.applyRootMotion = runRootMotion.useAnimationBasedMovement;

        request.Clear();


        invincibleTimer = 0f;
        damageRecoverTimer = 0f;
        knockbackTimer = 0f;
        knockbackVelocity = Vector3.zero;

        isInKnockback = false;
        isInRecover = false;
        isInInvincible = false;

        ReserveState(new Idle(), 0);
        ApplyReserveState();

    }

    private void Start()
    {
        // InputSystem取得
        playerInputMap = InputSystem.actions.FindActionMap("Player");
        //playerInputMap.Enable();

        moveInput = playerInputMap.FindAction("Move");
        jumpInput = playerInputMap.FindAction("Jump");
        tackleInput = playerInputMap.FindAction("Tackle");
        lookInput = playerInputMap.FindAction("Look");

        UpdateDamageColliderEnabled();

        audioManager = AudioManager.Instance;

        PlayerHPManager.Instance.OnDead += () =>
        {
            ReserveState(new DeadState(), 10000);
        };


    }

    private void OnDisable()
    {
        SetNormalTackleCollider(false);
        SetJustTackleCollider(false);

        ResetJustTackleState();
        ResetTackleHitCandidates();

        isTackling = false;
        isChargeTackle = false;

        footstepTimer = 0f;
        groundedStableTimer = 0f;
        airborneTimer = 0f;
    }

    #endregion

    #region メイン
    private void Update()
    {

        // ワープ予約がある場合は、移動・入力・ステート更新より先に処理する。
        if (ProcessPendingWarp())
        {
            return;
        }

        if (agent.TimeScale <= 0f || Time.timeScale <= 0f)
        {
            animator.speed = 0f;
            CancelChargeForPauseIfNeeded();
            return;
        }

        // 死亡中は入力・移動更新を止める
        if (IsDead)
        {
            animator.speed = agent.TimeScale;
            UpdateAnimator();
            return;
        }

        request.Clear();
        reservePriority = int.MinValue;


        float dt = Time.deltaTime * agent.TimeScale;

        UpdateJumpGroundIgnoreTimer(dt);

        if (tackleCooldownTimer > 0f)
            tackleCooldownTimer -= dt;

        UpdateDamageTimers(dt);

        UpdateInput();
        UpdateInputBuffer();

        UpdateCameraRotation();

        nowState?.UpdateState();
        ApplyReserveState();

        if (runRootMotion.useAnimationBasedMovement && nowState != null)
            currentMotionPolicy = nowState.GetMotionPolicy();
        else
            currentMotionPolicy = MotionPolicy.Default;

       

        ApplyMovement();
        UpdateAnimator();


        UpdateFootstepState(dt);


        ResolvePendingTackleHits();
        didPlayShakeThisFrame = false;

        if (hitStopCooldownTimer > 0f)
            hitStopCooldownTimer -= dt;


        UpdateDamageColliderEnabled();

    }
    private void LateUpdate()
    {
        if (cameraParam.rotationMode == CameraRotationMode.SeparatedThirdPerson)
        {
            ApplyThirdPersonCameraTransform();

            SyncPlayerYawToCameraWhileCharging();
        }
    }

    private void CancelChargeForPauseIfNeeded()
    {
        // チャージ中以外は何もしない。
        if (!isCharging && nowState is not Charge)
            return;

        // 入力状態を明示的に切る。
        tackleDown = false;
        tackleUp = false;
        tackleHold = false;
        tacklePressTimer = 0f;
        tackleTapBufferTimer = 0f;

        ignoreTackleUntilReleased = true;

        request.Clear();
        currentVelocity = Vector3.zero;

        reservePriority = int.MinValue;
        ReserveState(new Idle(), int.MaxValue);
        ApplyReserveState();

        UpdateAnimator();
    }

    #endregion

    #region 入力処理

    // 入力情報更新
    private void UpdateInput()
    {
        // 左スティック情報取得
        inputMoveDir = moveInput.ReadValue<Vector2>();

        // デッドゾーンで閾値判定
        if (inputMoveDir.magnitude < moveParam.deadZone)
            inputMoveDir = Vector2.zero;
        else
            inputMoveDir.Normalize();

        // ジャンプ入力情報取得
        jumpPressed = jumpInput.WasPressedThisFrame();
        if (jumpPressed)
            jumpBufferTimer = jumpParam.jumpBufferTime;

        bool rawTackleDown = tackleInput.WasPressedThisFrame();
        bool rawTackleUp = tackleInput.WasReleasedThisFrame();
        bool rawTackleHold = tackleInput.IsPressed();

        // ポーズ中にチャージ解除した後は、
        // タックルボタンを一度離すまでタックル入力を完全に無視する。
        if (ignoreTackleUntilReleased)
        {
            tackleDown = false;
            tackleUp = false;
            tackleHold = false;
            tacklePressTimer = 0f;

            // 物理的にボタンが離れたら通常入力へ復帰。
            if (!rawTackleHold)
            {
                ignoreTackleUntilReleased = false;
            }
        }
        else
        {
            tackleDown = rawTackleDown;
            tackleUp = rawTackleUp;
            tackleHold = rawTackleHold;

            if (tackleDown)
                tacklePressTimer = 0f;

            if (tackleHold)
                tacklePressTimer += Time.deltaTime * agent.TimeScale;

            // 通常タックルの入力バッファ。
            // 空中中に短押しを離してしまった場合でも、着地直後に拾えるようにする。
            if (tackleUp && tacklePressTimer < tackleParam.tapChargeTime)
            {
                tackleTapBufferTimer = Mathf.Max(jumpParam.landingTackleBufferTime, 0f);
            }
        }

        // タックル中は視点入力を完全停止
        if (isTackling)
        {
            inputLook = Vector2.zero;
            smoothedLookInput = Vector2.zero;
            lookInputVelocity = Vector2.zero;

            currentControllerYawSpeed = 0f;
            currentControllerPitchSpeed = 0f;

            return;
        }


        inputLook = ReadProcessedLookInput();
    }

    //ジャンプ入力バッファ更新
    private void UpdateInputBuffer()
    {
        float dt = Time.deltaTime * agent.TimeScale;

        // ジャンプバッファ
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= dt;
            jumpRequest = true;
        }
        else
        {
            jumpRequest = false;
        }

        // 通常タックル着地バッファ
        if (tackleTapBufferTimer > 0f)
        {
            tackleTapBufferTimer -= dt;

            if (tackleTapBufferTimer < 0f)
                tackleTapBufferTimer = 0f;
        }
    }

    public void DisableInputFromManager()
    {
        if (playerInputMap != null)
            playerInputMap.Disable();
    }

    #endregion

    #region ジャンプ

    //ジャンプ初期設定
    public void StartJump()
    {
        verticalVelocity = jumpParam.jumpPower;

        jumpBufferTimer = 0f;
        jumpRequest = false;

        canAirTackle = true;

        // ジャンプ開始直後は明確に空中扱いにする
        isGroundedStable = false;
        coyoteTimer = 0f;

        // ジャンプ直後にSphereCastが床を拾ってしまうのを防ぐ
        jumpGroundIgnoreTimer = Mathf.Max(jumpParam.jumpGroundIgnoreTime, 0f);

        if (jumpParam.disableStepOffsetWhileAirborne && controller != null)
            controller.stepOffset = jumpParam.airborneStepOffset;

        groundedStableTimer = 0f;
        airborneTimer = footstepBlockTimeAfterLeaveGround;
    }
    #endregion

    #region 地面判定

    //地面法線取得
    private Vector3 GetGroundNormal()
    {

        if (!isGroundedStable)
            return Vector3.up;


        Vector3 sum = Vector3.zero;
        int count = 0;
        float radius = controller.radius * 0.8f;
        Vector3[] offsets = { Vector3.zero, new Vector3(radius, 0, 0), new Vector3(-radius, 0, 0), new Vector3(0, 0, radius), new Vector3(0, 0, -radius) };
        foreach (var o in offsets) { if (Physics.Raycast(transform.position + o, Vector3.down, out RaycastHit hit, 2f)) { sum += hit.normal; count++; } }
        if (count == 0) return Vector3.up;
        return (sum / count).normalized;
    }

    //接地判定
    private bool CheckGroundStable()
    {
        if (controller == null)
            return false;

        // 上昇中/ジャンプ直後はここで接地を受け付けない
        if (!CanAcceptGroundNow())
            return false;

        float radius = controller.radius * WallCheck.groundRadiusRate;

        // CharacterController中心から、足元Sphereの中心までの距離
        float halfHeight = controller.height * 0.5f;
        float footSphereCenterDistance = Mathf.Max(halfHeight - radius, 0f);

        // 足元Sphere中心まで + 追加の接地許容距離
        float castDistance = footSphereCenterDistance + WallCheck.groundExtraDistance;

        Vector3 origin = GetControllerCenter();

        if (Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                WallCheck.Layer,
                QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);

            if (angle > controller.slopeLimit + WallCheck.groundSlopeExtraAngle)
                return false;

            return true;
        }

        return false;
    }

    private void UpdateJumpGroundIgnoreTimer(float dt)
    {
        if (jumpGroundIgnoreTimer <= 0f)
            return;

        jumpGroundIgnoreTimer -= dt;

        if (jumpGroundIgnoreTimer < 0f)
            jumpGroundIgnoreTimer = 0f;
    }

    private bool CanAcceptGroundNow()
    {
        // ジャンプ直後は床を拾わない
        if (jumpGroundIgnoreTimer > 0f)
            return false;

        // 上昇中は接地扱いにしない
        // groundedCheckMaxUpVelocity は基本 0 以下推奨
        if (verticalVelocity > jumpParam.groundedCheckMaxUpVelocity)
            return false;

        return true;
    }

    #endregion

    #region 壁判定
    private Vector3 GetControllerCenter()
    {
        return transform.position + controller.center;
    }

    private bool CheckTackleWallHit()
    {
        Vector3 origin =
            GetControllerCenter() +
            Vector3.up * WallCheck.wallCheckHeightOffset;

        Vector3 dir = tackleDir;

        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        dir.y = 0f;
        dir.Normalize();

        if (!Physics.SphereCast(
                origin,
                WallCheck.Radius,
                dir,
                out RaycastHit hit,
                WallCheck.Distance,
                WallCheck.Layer,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // ===== 坂・床は壁扱いしない =====
        float angleFromUp = Vector3.Angle(hit.normal, Vector3.up);

        // slopeLimit以下 + 余裕角は坂なので無視
        if (angleFromUp <= controller.slopeLimit + WallCheck.groundSlopeExtraAngle)
            return false;

        // それ以上のみ「壁」
        return true;
    }

    private bool CheckHeadHit()
    {
        float radius =
            WallCheck.ceilingRadius > 0f
                ? WallCheck.ceilingRadius
                : controller.radius * 0.95f;

        Vector3 origin =
            GetControllerCenter() +
            Vector3.up * (controller.height * 0.5f - controller.radius - WallCheck.ceilingStartDownOffset);

        return Physics.SphereCast(
            origin,
            radius,
            Vector3.up,
            out RaycastHit hit,
            WallCheck.ceilingDistance,
            WallCheck.Layer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void ResolveCeilingHit(bool hitCeiling)
    {
        if (!hitCeiling)
            return;

        // 上昇中に天井へ当たったら、0ではなく下向き速度へ切り替える
        if (verticalVelocity > 0f)
        {
            verticalVelocity = -Mathf.Max(WallCheck.ceilingHitDownVelocity, 0f);
        }

        // 天井に当たっている時点で接地扱いにはしない
        isGroundedStable = false;
        coyoteTimer = 0f;

        // SphereCastが天井を拾い続ける張り付き対策として少し下へ離す
        if (WallCheck.ceilingDetachDistance > 0f)
        {
            controller.Move(Vector3.down * WallCheck.ceilingDetachDistance);
        }
    }

    private void OnDrawGizmos()
    {
        if (controller == null) return;

        Gizmos.color = Color.green;
        // ===== 地面判定 =====
        {
            float radius = controller.radius * WallCheck.groundRadiusRate;

            float halfHeight = controller.height * 0.5f;
            float footSphereCenterDistance = Mathf.Max(halfHeight - radius, 0f);
            float distance = footSphereCenterDistance + WallCheck.groundExtraDistance;

            Vector3 origin = GetControllerCenter();

            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawLine(origin, origin + Vector3.down * distance);
            Gizmos.DrawWireSphere(origin + Vector3.down * distance, radius);
        }

        // ===== 上判定 =====
        {
            Gizmos.color = Color.red;

            float radius =
                WallCheck.ceilingRadius > 0f
                    ? WallCheck.ceilingRadius
                    : controller.radius * 0.95f;

            Vector3 origin =
                GetControllerCenter() +
                Vector3.up * (controller.height * 0.5f - controller.radius - WallCheck.ceilingStartDownOffset);

            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawLine(origin, origin + Vector3.up * WallCheck.ceilingDistance);
        }

        // ===== 壁判定 =====
        {
            Gizmos.color = Color.blue;

            Vector3 origin =
                GetControllerCenter() +
                Vector3.up * WallCheck.wallCheckHeightOffset;

            Vector3 dir = Application.isPlaying ? tackleDir : transform.forward;

            if (dir.sqrMagnitude < 0.001f)
                dir = transform.forward;

            dir.y = 0f;
            dir.Normalize();

            Gizmos.DrawWireSphere(origin, WallCheck.Radius);
            Gizmos.DrawLine(origin, origin + dir * WallCheck.Distance);
        }
    }

    #endregion

    #region 移動適用

    //移動適用処理
    private void ApplyMovement()
    {
        //経過時間取得
        float dt = Time.deltaTime * agent.TimeScale;

        UpdateStepOffsetByAirState();

        // 被ダメノックバック中
        if (knockbackTimer > 0f)
        {
            ApplyDamageKnockbackMovement(dt);
            return;
        }

        //タックル中
        if (isTackling)
        {
            UpdateJustTackleWindow(dt);

            tackleElapsedTimer += dt;

            // タックル時間を減少
            tackleTimer -= dt;

            if (tackleTimer <= 0f)
            {
                isNormalTacklePostHitKeeping = false;
            }

            float speed = isChargeTackle ? chargeTackle.tackleSpeed : normalTackle.tackleSpeed;

            Vector3 move = tackleDir * speed;
            currentVelocity = move;

            move.y = 0f;

            Vector3 moveDelta = move * dt;
            CollisionFlags tackleFlags = controller.Move(moveDelta);

            tackleMovedDistance += new Vector3(moveDelta.x, 0f, moveDelta.z).magnitude;

            // ============================
            // 壁判定
            // ============================
            bool canUseControllerSideWall =
                isChargeTackle
                    ? !chargeTackle.ignoreControllerSideWallStop
                    : !normalTackle.ignoreControllerSideWallStop;

            bool hitWallByMove =
                canUseControllerSideWall &&
                WallCheck.useControllerCollisionFlags &&
                (tackleFlags & CollisionFlags.Sides) != 0;

            // SphereCastによる壁判定
            bool canUseCastWall =
                isChargeTackle
                    ? chargeTackle.stopOnStageWall
                    : normalTackle.stopOnStageWall;

            bool hitWallByCast =
                canUseCastWall &&
                CheckTackleWallHit();

            // ============================
            // 壁終了判定
            // ============================
            if (isChargeTackle)
            {
                // ===== チャージタックル用：詰まり判定 =====
                Vector3 actualMove =
                    new Vector3(moveDelta.x, 0f, moveDelta.z);

                if (actualMove.sqrMagnitude < 0.00001f)
                {
                    tackleStuckTimer += dt;
                }
                else
                {
                    tackleStuckTimer = 0f;
                }

                // 一定時間ほぼ動けなければ終了
                if (tackleStuckTimer >= 0.15f)
                {
                    ReserveState(new Idle(), 100);
                    return;
                }
            }
            else
            {
                // ===== 通常タックル =====
                bool hitWall = hitWallByCast; // ← Sidesは使わない

                if (hitWall)
                {
                    if (tackleParam.prioritizeHitBeforeWallEnd && HasPendingTackleHitCandidates())
                    {
                        ResolvePendingTackleHits();
                        if (!isTackling)
                            return;
                    }

                    if (CanEndTackleByWall())
                    {
                        ReserveState(new Idle(), 100);
                    }
                }
            }

            return;
        }

       
        //空中か否かで移動制御を抵抗力を検出
        float control = isGroundedStable ? 1f : jumpParam.airControl;

        //地面法線取得
        Vector3 groundNormal = GetGroundNormal();

        //坂角度を取得
        Vector3 slopeDir = Vector3.ProjectOnPlane(request.moveDir, groundNormal).normalized;
        //坂の内積計算
        float slopeDot = Vector3.Dot(slopeDir, Vector3.up);
        float slopeTarget = 1f;

        //接地時の坂移動抵抗力
        if (isGroundedStable)
        {
            //上り時なら上昇抵抗力、下り時なら下降抵抗力をセット
            if (slopeDot > 0.1f) slopeTarget = slopeParam.slopeUpFactor;
            else if (slopeDot < -0.1f) slopeTarget = slopeParam.slopeDownFactor;
        }

        //坂の角度、移動速度、希望速度割合、坂用抵抗力、空中抵抗力で目標速度を算出
        Vector3 targetVelocity = slopeDir * moveParam.moveSpeed * request.speedRate * slopeTarget * control;
        if (nowState is Charge)
        {
            targetVelocity *= chargeTackle.moveRate;
        }

        //目標速度に加速、減速を加味した速度反映を行う
        currentVelocity = (slopeDir.sqrMagnitude > 0.001f) ?
        Vector3.Lerp(currentVelocity, targetVelocity, moveParam.accel * dt) :
        Vector3.Lerp(currentVelocity, Vector3.zero, moveParam.decel * dt);

        // SeparatedThirdPersonでは、カメラ操作ではPlayerを回さず、移動方向に応じてPlayer本体を回す。
        UpdatePlayerFacingByMoveDirection(dt);


        //接地判定による接地時、空中時の重力制御
        if (isGroundedStable)
        {
            // y速度が0以下の場合、吸着処理を入れる
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -slopeParam.groundStickForce;
            }

            //空中タックルを再度可能にする
            canAirTackle = true;
        }
        //空中時は上昇か落下に応じて重力を加える
        else
        {
            verticalVelocity -= (verticalVelocity > 0 ? jumpParam.gravityUp : jumpParam.gravityDown) * dt;
        }

        //合計の移動量を算出
        Vector3 moveTotal = currentVelocity + Vector3.up * verticalVelocity;

        //CharacterControllerを移動させ、強化した接地判定による処理を行う

        CollisionFlags moveFlags = controller.Move(moveTotal * dt);

        // ===== 天井判定 =====
        // CharacterControllerのAbove判定 + 補助SphereCastのハイブリッド
        bool hitCeilingByMove =
            WallCheck.useControllerCollisionFlags &&
            (moveFlags & CollisionFlags.Above) != 0;

        bool hitCeilingByCast = CheckHeadHit();

        ResolveCeilingHit(hitCeilingByMove || hitCeilingByCast);

        bool canAcceptGround = CanAcceptGroundNow();


        bool groundedByMove =
            canAcceptGround &&
            WallCheck.useControllerCollisionFlags &&
            (moveFlags & CollisionFlags.Below) != 0;

        bool groundedByCast =
            canAcceptGround &&
            CheckGroundStable();

        isGroundedStable = groundedByMove || groundedByCast;

        // ===== コヨーテタイム =====
        if (isGroundedStable)
            coyoteTimer = jumpParam.coyoteTime;
        else
            coyoteTimer -= dt;

    }

    // RootMotion移動適用
    private void ApplyRootMotionMove(Vector3 rootDelta, Quaternion rootDeltaRot)
    {
        float dt = Time.deltaTime * agent.TimeScale;

        // ===== 地上水平移動を坂に沿わせる =====
        if (isGroundedStable)
        {
            Vector3 planar = new Vector3(rootDelta.x, 0f, rootDelta.z);

            if (planar.sqrMagnitude > 0.000001f)
            {
                Vector3 groundNormal = GetGroundNormal();
                Vector3 slopeDir = Vector3.ProjectOnPlane(planar, groundNormal).normalized;

                float slopeDot = Vector3.Dot(slopeDir, Vector3.up);
                float slopeFactor = 1f;

                if (slopeDot > 0.1f) slopeFactor = slopeParam.slopeUpFactor;
                else if (slopeDot < -0.1f) slopeFactor = slopeParam.slopeDownFactor;

                float planarLength = planar.magnitude;
                planar = slopeDir * planarLength * slopeFactor;

                rootDelta.x = planar.x;
                rootDelta.z = planar.z;
            }
        }

        // ===== 重力 =====
        if (!currentMotionPolicy.ignoreGravity)
        {
            if (isGroundedStable)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -slopeParam.groundStickForce;

                canAirTackle = true;
            }
            else
            {
                verticalVelocity -=
                    (verticalVelocity > 0 ? jumpParam.gravityUp : jumpParam.gravityDown) * dt;
            }

            rootDelta += Vector3.up * (verticalVelocity * dt);
        }

        // 現在速度（アニメ速度ベース）
        if (dt > 0f)
        {
            Vector3 horizontal = new Vector3(rootDelta.x, 0f, rootDelta.z);
            currentVelocity = horizontal / dt;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        CollisionFlags flags = controller.Move(rootDelta);


        bool hitCeilingByMove =
    WallCheck.useControllerCollisionFlags &&
    (flags & CollisionFlags.Above) != 0;

        bool hitCeilingByCast = CheckHeadHit();

        ResolveCeilingHit(hitCeilingByMove || hitCeilingByCast);


        // 今回は通常移動では基本OFF想定
        if (currentMotionPolicy.useRootMotionRotation)
        {
            transform.rotation = rootDeltaRot * transform.rotation;
        }

        bool canAcceptGround = CanAcceptGroundNow();

        bool groundedByMove =
            canAcceptGround &&
            WallCheck.useControllerCollisionFlags &&
            (flags & CollisionFlags.Below) != 0;

        bool groundedByCast =
            canAcceptGround &&
            CheckGroundStable();

        isGroundedStable = groundedByMove || groundedByCast;

        if (isGroundedStable)
            coyoteTimer = jumpParam.coyoteTime;
        else
            coyoteTimer -= dt;
    }

    private void UpdateStepOffsetByAirState()
    {
        if (controller == null)
            return;

        if (!jumpParam.disableStepOffsetWhileAirborne)
            return;

        bool canUseStepOffset =
            isGroundedStable &&
            verticalVelocity <= jumpParam.groundedCheckMaxUpVelocity &&
            !isTackling &&
            !isInKnockback;

        controller.stepOffset =
            canUseStepOffset
                ? defaultStepOffset
                : jumpParam.airborneStepOffset;
    }

    private void OnAnimatorMove()
    {
        if (!runRootMotion.useAnimationBasedMovement) return;

        if (agent == null || animator == null) return;
        if (agent.TimeScale <= 0f) return;

        if (!currentMotionPolicy.useRootMotionPosition &&
            !currentMotionPolicy.useRootMotionRotation)
            return;

        Vector3 deltaPos = animator.deltaPosition * currentMotionPolicy.rootMotionScale;
        Quaternion deltaRot = animator.deltaRotation;

        // 通常移動ではYを使わない
        if (!currentMotionPolicy.useRootMotionY)
            deltaPos.y = 0f;

        ApplyRootMotionMove(deltaPos, deltaRot);
    }

    /// 被ダメ時のノックバック移動。
    private void ApplyDamageKnockbackMovement(float dt)
    {
        if (!isInKnockback)
            return;

        if (knockbackTimer <= 0f)
            return;

        knockbackTimer -= dt;

        // ノックバック中も重力は通常通り処理する
        if (isGroundedStable && verticalVelocity <= 0f)
        {
            verticalVelocity = -slopeParam.groundStickForce;
        }
        else
        {
            verticalVelocity -= jumpParam.gravityDown * dt;
        }

        // knockbackTime 中は一定速度で押し続ける
        Vector3 move = knockbackVelocity;
        move.y = verticalVelocity;

        CollisionFlags flags = controller.Move(move * dt);


        bool canAcceptGround = CanAcceptGroundNow();

        bool groundedByMove =
            canAcceptGround &&
            WallCheck.useControllerCollisionFlags &&
            (flags & CollisionFlags.Below) != 0;

        bool groundedByCast =
            canAcceptGround &&
            CheckGroundStable();

        isGroundedStable = groundedByMove || groundedByCast;

        if (isGroundedStable)
            coyoteTimer = jumpParam.coyoteTime;
        else
            coyoteTimer -= dt;

        currentVelocity = knockbackVelocity;

        // ===== ノックバック終了 =====
        if (knockbackTimer <= 0f)
        {
            knockbackTimer = 0f;
            knockbackVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;

            isInKnockback = false;

            isInRecover = true;
            damageRecoverTimer = Mathf.Max(hpParam.damageRecoverTime, 0f);

            if (isGroundedStable && verticalVelocity < 0f)
            {
                verticalVelocity = -slopeParam.groundStickForce;
            }

            // コライダー状態を即同期
            UpdateDamageColliderEnabled();

            // 復帰時間が0なら即座に復帰後無敵へ移行
            if (damageRecoverTimer <= 0f)
            {
                isInRecover = false;
                StartPostRecoverInvincible();
            }
        }
    }

    private void UpdatePlayerFacingByMoveDirection(float dt)
    {
        if (cameraParam.rotationMode != CameraRotationMode.SeparatedThirdPerson)
            return;

        if (!cameraParam.rotatePlayerByMoveDirection)
            return;

        if (isTackling || isCharging || isInKnockback || IsDead)
            return;

        Vector3 faceDir = request.moveDir;
        faceDir.y = 0f;

        if (faceDir.sqrMagnitude < 0.0001f)
            return;

        faceDir.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(faceDir, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(cameraParam.playerTurnSpeed, 0f) * dt
        );
    }

    #endregion

    #region カメラ
    private bool IsLookInputFromMouse()
    {
        if (lookInput == null)
            return false;

        if (lookInput.activeControl == null)
            return false;

        return lookInput.activeControl.device is Mouse;
    }
    private CameraControlSettings GetCameraControlSettings()
    {
        return cameraParam.controlSettings;
    }
    private Vector2 ReadProcessedLookInput()
    {
        if (lookInput == null)
            return Vector2.zero;

        Vector2 rawInput = lookInput.ReadValue<Vector2>();
        bool isMouse = IsLookInputFromMouse();

        CameraControlSettings settings = GetCameraControlSettings();

        if (settings == null)
            return rawInput;

        Vector2 result = rawInput;

        if (isMouse)
            result = ApplyMouseLookSettings(result, settings);
        else
            result = ApplyControllerLookSettings(result, settings);

        result = ApplyOptionalLookInputSmoothing(result, isMouse, settings);

        return result;
    }

    private Vector2 ApplyMouseLookSettings(Vector2 rawInput, CameraControlSettings settings)
    {
        Vector2 result = rawInput;

        float deadZone = Mathf.Max(settings.mouseDeadZone, 0f);

        if (result.magnitude < deadZone)
            return Vector2.zero;

        if (settings.limitMouseInputMagnitude)
        {
            float max = Mathf.Max(settings.maxMouseInputMagnitude, 0.01f);

            if (result.sqrMagnitude > max * max)
                result = result.normalized * max;
        }

        result.x *= settings.MouseHorizontalDegreesPerInput;
        result.y *= settings.MouseVerticalDegreesPerInput;

        if (settings.invertMouseX)
            result.x *= -1f;

        if (settings.invertMouseY)
            result.y *= -1f;

        return result;
    }

    private Vector2 ApplyControllerLookSettings(Vector2 rawInput, CameraControlSettings settings)
    {
        Vector2 result = rawInput;

        float deadZone = Mathf.Max(settings.stickDeadZone, 0f);

        if (result.magnitude < deadZone)
            return Vector2.zero;

        result *= Mathf.Max(settings.stickInputScale, 0f);

        if (settings.limitStickInputMagnitude)
        {
            float max = Mathf.Max(settings.maxStickInputMagnitude, 0.01f);

            if (result.sqrMagnitude > max * max)
                result = result.normalized * max;
        }

        if (settings.invertControllerX)
            result.x *= -1f;

        if (settings.invertControllerY)
            result.y *= -1f;

        return result;
    }

    private Vector2 ApplyOptionalLookInputSmoothing(
        Vector2 targetInput,
        bool isMouse,
        CameraControlSettings settings)
    {
        bool useSmoothing = isMouse
            ? settings.smoothMouseInput
            : settings.smoothStickInput;

        if (!useSmoothing)
        {
            smoothedLookInput = targetInput;
            lookInputVelocity = Vector2.zero;
            return targetInput;
        }

        float smoothTime = isMouse
            ? settings.mouseSmoothTime
            : settings.stickSmoothTime;

        float dt = Time.deltaTime * agent.TimeScale;

        smoothedLookInput = Vector2.SmoothDamp(
            smoothedLookInput,
            targetInput,
            ref lookInputVelocity,
            Mathf.Max(smoothTime, 0.001f),
            Mathf.Infinity,
            dt
        );

        return smoothedLookInput;
    }

    // カメラ移動方向を取得
    private Vector3 GetCameraMoveDir()
    {
        Vector3 f = GetCameraForward();
        Vector3 r = GetCameraRight();

        return f * inputMoveDir.y + r * inputMoveDir.x;
    }

    private Transform GetCameraBasisTransform()
    {
        if (cameraParam.rotationMode == CameraRotationMode.SeparatedThirdPerson)
        {
            if (cameraParam.thirdPersonPivot != null)
                return cameraParam.thirdPersonPivot;
        }

        if (cameraParam.eye != null)
            return cameraParam.eye;

        return transform;
    }

    /// カメラの前方向（水平）
    private Vector3 GetCameraForward()
    {
        Transform basis = GetCameraBasisTransform();

        Vector3 forward;

        if (basis != null)
        {
            forward = basis.forward;
        }
        else
        {
            forward = Quaternion.Euler(0f, currentCameraYaw, 0f) * Vector3.forward;
        }

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return transform.forward;

        return forward.normalized;
    }

    /// カメラの右方向（水平）
    private Vector3 GetCameraRight()
    {
        Transform basis = GetCameraBasisTransform();

        Vector3 right;

        if (basis != null)
        {
            right = basis.right;
        }
        else
        {
            right = Quaternion.Euler(0f, currentCameraYaw, 0f) * Vector3.right;
        }

        right.y = 0f;

        if (right.sqrMagnitude < 0.0001f)
            return transform.right;

        return right.normalized;
    }

    private Vector3 GetTackleDirection()
    {
        // 従来モードでは今まで通りカメラ前方。
        if (cameraParam.rotationMode != CameraRotationMode.SeparatedThirdPerson)
            return GetCameraForward();

        switch (cameraParam.separatedTackleDirectionMode)
        {
            case SeparatedTackleDirectionMode.PlayerForward:
                {
                    Vector3 dir = transform.forward;
                    dir.y = 0f;

                    if (dir.sqrMagnitude < 0.0001f)
                        return GetCameraForward();

                    return dir.normalized;
                }

            case SeparatedTackleDirectionMode.CameraForward:
            default:
                return GetCameraForward();
        }
    }

    // カメラの回転処理
    private void UpdateCameraRotation()
    {
        if (isTackling)
            return;


        float dt = Time.deltaTime * agent.TimeScale;

        if (dt <= 0f)
            return;

        InitializeCameraRotationIfNeeded();

        CameraControlSettings settings = GetCameraControlSettings();
        bool isMouse = IsLookInputFromMouse();

        if (settings == null)
        {
            ApplyFallbackCameraRotation(dt);
            ApplyCameraRotationToTransforms();
            return;
        }

        if (isMouse)
            UpdateMouseCameraRotation(settings);
        else
            UpdateControllerCameraRotation(settings, dt);

        currentCameraPitch = Mathf.Clamp(
            currentCameraPitch,
            settings.minPitch,
            settings.maxPitch
        );

        ApplyCameraRotationToTransforms();
    }

    private void InitializeCameraRotationIfNeeded()
    {
        if (cameraRotationInitialized)
            return;

        if (cameraParam.rotationMode == CameraRotationMode.SeparatedThirdPerson &&
            cameraParam.thirdPersonPivot != null)
        {
            currentCameraYaw = cameraParam.thirdPersonPivot.eulerAngles.y;
            currentCameraPitch = NormalizeAngle(cameraParam.thirdPersonPivot.eulerAngles.x);
        }
        else
        {
            currentCameraYaw = transform.eulerAngles.y;

            if (cameraParam.eye != null)
                currentCameraPitch = NormalizeAngle(cameraParam.eye.localEulerAngles.x);
            else
                currentCameraPitch = 0f;
        }

        currentControllerYawSpeed = 0f;
        currentControllerPitchSpeed = 0f;

        cameraRotationInitialized = true;
    }

    private void UpdateMouseCameraRotation(CameraControlSettings settings)
    {
        currentCameraYaw += inputLook.x;
        currentCameraPitch -= inputLook.y;

        currentControllerYawSpeed = 0f;
        currentControllerPitchSpeed = 0f;
    }

    private void UpdateControllerCameraRotation(CameraControlSettings settings, float dt)
    {
        float targetYawSpeed =
            inputLook.x * Mathf.Max(settings.ControllerHorizontalDegreesPerSecond, 0f);

        float targetPitchSpeed =
            -inputLook.y * Mathf.Max(settings.ControllerVerticalDegreesPerSecond, 0f);

        if (settings.useControllerAcceleration)
        {
            float yawRate = Mathf.Abs(targetYawSpeed) > Mathf.Abs(currentControllerYawSpeed)
                ? settings.ControllerAccelerationPerSecond
                : settings.ControllerDecelerationPerSecond;

            float pitchRate = Mathf.Abs(targetPitchSpeed) > Mathf.Abs(currentControllerPitchSpeed)
                ? settings.ControllerAccelerationPerSecond
                : settings.ControllerDecelerationPerSecond;

            currentControllerYawSpeed = Mathf.MoveTowards(
                currentControllerYawSpeed,
                targetYawSpeed,
                Mathf.Max(yawRate, 0f) * dt
            );

            currentControllerPitchSpeed = Mathf.MoveTowards(
                currentControllerPitchSpeed,
                targetPitchSpeed,
                Mathf.Max(pitchRate, 0f) * dt
            );
        }
        else
        {
            currentControllerYawSpeed = targetYawSpeed;
            currentControllerPitchSpeed = targetPitchSpeed;
        }

        currentCameraYaw += currentControllerYawSpeed * dt;
        currentCameraPitch += currentControllerPitchSpeed * dt;
    }

    private void ApplyFallbackCameraRotation(float dt)
    {
        // CameraControlSettings未設定時の保険。
        currentCameraYaw += inputLook.x * 120f * dt;
        currentCameraPitch -= inputLook.y * 90f * dt;
        currentCameraPitch = Mathf.Clamp(currentCameraPitch, -35f, 65f);
    }

    private void ApplyCameraRotationToTransforms()
    {
        if (cameraParam.rotationMode == CameraRotationMode.SeparatedThirdPerson)
        {
            ApplyThirdPersonCameraTransform();

            SyncPlayerYawToCameraWhileCharging();

            return;
        }

        // 従来モード
        transform.rotation =
            Quaternion.AngleAxis(currentCameraYaw, Vector3.up);

        if (cameraParam.eye != null)
        {
            cameraParam.eye.localRotation =
                Quaternion.AngleAxis(currentCameraPitch, Vector3.right);
        }
    }

    private void ApplyThirdPersonCameraTransform()
    {
        if (cameraParam.thirdPersonPivot == null)
            return;

        cameraParam.thirdPersonPivot.position =
            transform.position + cameraParam.thirdPersonPivotOffset;

        cameraParam.thirdPersonPivot.rotation =
            Quaternion.Euler(currentCameraPitch, currentCameraYaw, 0f);
    }

    private void SyncPlayerYawToCameraWhileCharging()
    {
        if (cameraParam.rotationMode != CameraRotationMode.SeparatedThirdPerson)
            return;

        if (!cameraParam.syncPlayerYawToCameraWhileCharging)
            return;

        if (!isCharging)
            return;

        transform.rotation =
            Quaternion.AngleAxis(currentCameraYaw, Vector3.up);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    #endregion

    #region タックル操作

    #region タックルアニメーション
    private int DecideNormalTackleAnimIndex()
    {
        int count = Mathf.Max(tackleParam.tackleAnimationCount, 1);

        float chainLimit = Mathf.Max(tackleParam.tackleAnimationChainTime, 0f);
        bool isChain =
            Time.time - lastNormalTackleEndTime <= chainLimit;

        if (!isChain)
            nextNormalTackleAnimIndex = 0;

        int result = nextNormalTackleAnimIndex;

        nextNormalTackleAnimIndex++;
        if (nextNormalTackleAnimIndex >= count)
            nextNormalTackleAnimIndex = 0;

        return result;
    }

    private void SetTackleAnimationIndex(int index)
    {
        tackleAnimIndex = Mathf.Clamp(index, 0, Mathf.Max(tackleParam.tackleAnimationCount - 1, 0));

        if (animator != null)
            animator.SetInteger("TackleAnimIndex", tackleAnimIndex);
    }

    #endregion

    #region 水平方向タックル
    public void StartTackle(float duration = -1f)
    {
        if (!TackleGaugeManager.Instance.TryConsumeNormal())
            return;

        TackleGaugeManager.Instance.SetTackling(true);

        // 既存移動を完全停止
        currentVelocity = Vector3.zero;

        // タックル時間
        tackleTimer = (duration > 0f) ? duration : normalTackle.tackleDuration;

        // タックル方向決定。
        tackleDir = GetTackleDirection();

        // タックル方向にプレイヤーを即回転させる
        transform.rotation = Quaternion.LookRotation(tackleDir);

        tackleElapsedTimer = 0f;
        tackleMovedDistance = 0f;

        isTackling = true;
        isChargeTackle = false;

        SetTackleAnimationIndex(DecideNormalTackleAnimIndex());

        // タックル用コライダー開始
        BeginTackleHitboxes();

        // エフェクト
        if (tackleEffect == null)
        {
            EffectPlayParam param = EffectPlayParam.Default;
            param.positionOffset = transform.forward * 6.0f;

            tackleEffect = effectPlayer.Play(2, param);
        }

        // 空中制御
        if (!isGroundedStable)
            canAirTackle = false;

        // 軽いカメラ揺れ
        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.OnTackleStart();

            cameraParam.cameraController.PlayShake(
                Vector3.up,
                normalTackle.shakePower * 0.1f,
                normalTackle.shakeDuration * 0.1f
            );
        }

        if (afterimageRenderer)
        {
            afterimageRenderer.BeginAfterimage();
        }
    }

    public void EndTackle()
    {
        TackleGaugeManager.Instance.SetTackling(false);

        // タックル系コライダーとジャスト状態を必ずリセット
        EndTackleHitboxes();

        // タックル中に蓄積した候補も必ず破棄
        ResetTackleHitCandidates();

        bool wasChargeTackle = isChargeTackle;

        isTackling = false;
        isChargeTackle = false;
        isNormalTacklePostHitKeeping = false;

        if (!wasChargeTackle)
            lastNormalTackleEndTime = Time.time;

        tackleElapsedTimer = 0f;
        tackleMovedDistance = 0f;

        tackleTimer = 0f;
        currentVelocity = Vector3.zero;
        tackleCooldownTimer = normalTackle.tackleCooldown;

        verticalVelocity = Mathf.Min(verticalVelocity, 0f);

        if (tackleEffect != null)
        {
            tackleEffect.StopImmediate();
            tackleEffect = null;
        }

        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.OnTackleEnd();

        }

        if (afterimageRenderer)
        {
            afterimageRenderer.EndAfterimage();
        }
    }

    public bool CanTackle()
    {
        // クールタイムは常にチェックする
        if (tackleCooldownTimer > 0f)
            return false;

        // 空中条件
        if (!isGroundedStable)
        {
            return normalTackle.allowAirTackle && canAirTackle;
        }

        // 地上
        return true;
    }

    private bool HasBufferedNormalTackle()
    {
        return tackleTapBufferTimer > 0f;
    }

    private void ConsumeBufferedNormalTackle()
    {
        tackleTapBufferTimer = 0f;
    }
    private float GetPowerRate()
    {
        if (!isTackling) return 0f;

        float progress = GetPowerProgress();
        if (progress <= 0.0f) return 0.0f;

        float curveValue;

        if (!isChargeTackle)
        {
            curveValue = normalTackle.powerCurve.Evaluate(progress);
            return curveValue;
        }
        else
        {
            curveValue = chargeTackle.powerCurve.Evaluate(progress);
        }

        return curveValue;
    }

    private float GetPowerProgress()
    {
        float duration;
        float progress;

        duration = isChargeTackle ?
        chargeTackle.tackleDuration :
        normalTackle.tackleDuration;

        if (duration <= 0f) return 0f;
        progress = Mathf.Clamp01(1f - (tackleTimer / duration));

        return progress;
    }

    #endregion

    #region タックルヒットイベント

    private bool IsTackleTargetInFront(Collider other)
    {
        if (!tackleParam.useFrontHitAngleLimit)
            return true;

        if (other == null)
            return false;

        Vector3 origin = GetControllerCenter();

        Vector3 targetPos = other.bounds.center;

        Vector3 toTarget = targetPos - origin;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            return true;

        toTarget.Normalize();

        Vector3 forward = tackleDir;

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return true;

        forward.Normalize();

        float angle = Vector3.Angle(forward, toTarget);

        return angle <= tackleParam.frontHitHalfAngle;
    }

    void HandleAttack(TackleHitCandidate candidate)
    {
        var self = candidate.selfHitbox;
        var target = candidate.targetHitbox;

        if (!isTackling) return;
        if (justTackleSucceeded) return;
        if (target == null) return;
        if (target.receiver == null) return;


        Vector3 powerDir = tackleDir;
        powerDir.y = 0f;

        if (powerDir.sqrMagnitude < 0.0001f)
            powerDir = GetTackleDirection();

        powerDir.Normalize();

        BlowPayload payload = new BlowPayload
        {
            tackleType = isChargeTackle ? TackleType.Charge : TackleType.Normal,
            damageConstant = isChargeTackle ? chargeTackle.tackleDamage : normalTackle.tackleDamage,
            powerConstant = isChargeTackle ? chargeTackle.basePower : normalTackle.basePower,
            powerRate = GetPowerRate(),
            powerDirection = powerDir
        };


        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = self.gameObject,
            targetObject = (target.receiver as MonoBehaviour).gameObject,
            targetHitbox = target.gameObject,
            payload = payload,
            contactPoint = candidate.contactPoint
        };

        target.receiver.OnHit(data);

        // ===== エフェクト =====
        PlayTackleHitEffect(candidate);

        audioManager.PlaySe("PL_TackleHit");

        // ===== ヒットストップ =====
        if (hitStopCooldownTimer <= 0f)
        {
            GameTimeManager.Instance.HitStopLayer(
                TimeLayerType.Gameplay,
                normalTackle.hitStopTime
            );

            hitStopCooldownTimer = normalTackle.hitStopTime;
        }

        // ===== カメラシェイク =====
        if (cameraParam.cameraController != null && !didPlayShakeThisFrame)
        {

            cameraParam.cameraController.PlayShake(
                powerDir,
                normalTackle.shakePower,
                normalTackle.shakeDuration
            );

            didPlayShakeThisFrame = true;
        }
    }

    private void PlayTackleHitEffect(TackleHitCandidate candidate)
    {
        if (candidate == null)
            return;

        if (effectPlayer == null)
            return;

        Vector3 point = candidate.contactPoint;

        // contactPointが不正だった場合の保険。
        if (!IsValidVector3(point))
        {
            if (candidate.targetHitbox != null)
            {
                Collider targetCollider = candidate.targetHitbox.GetComponent<Collider>();
                if (targetCollider != null)
                    point = targetCollider.bounds.center;
                else if (candidate.targetObject != null)
                    point = candidate.targetObject.transform.position;
                else
                    point = transform.position;
            }
            else
            {
                point = transform.position;
            }
        }

        effectPlayer.PlayAt(
            isChargeTackle ? 7 : 3,
            point
        );
    }

    #endregion

    #region チャージタックル
    public void StartChargeTackle(bool isOverheat = false)
    {
        currentVelocity = Vector3.zero;

        float duration = chargeTackle.tackleDuration;
        float speed = chargeTackle.tackleSpeed;

        if (isOverheat)
        {
            duration *= tackleParam.overheatDurationRate;
            speed *= tackleParam.overheatSpeedRate;
        }


        tackleTimer = duration;

        // チャージタックル方向。
        tackleDir = GetTackleDirection();

        transform.rotation = Quaternion.LookRotation(tackleDir);

        tackleElapsedTimer = 0f;
        tackleMovedDistance = 0f;
        tackleStuckTimer = 0f;

        isTackling = true;
        isChargeTackle = true;

        // チャージタックルは必ず0番
        SetTackleAnimationIndex(0);


        BeginTackleHitboxes();

        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.OnTackleStart();
        }

        if (tackleEffect == null)
        {
            EffectPlayParam param = EffectPlayParam.Default;
            param.positionOffset = transform.forward * 6.0f;

            tackleEffect = effectPlayer.Play(6, param);
        }

        if (afterimageRenderer)
        {
            afterimageRenderer.BeginAfterimage();
        }
    }


    #endregion

    #region ジャストタックル
    private void BeginTackleHitboxes()
    {
        // 念のため、開始時に一度すべてOFFにして前回状態を完全に切る
        SetNormalTackleCollider(false);
        SetJustTackleCollider(false);

        // タックル開始時に状態を完全初期化
        ResetJustTackleState();
        ResetTackleHitCandidates();

        bool canUseJustTackle =
            justTackle.justTackleHitCollider != null &&
            justTackle.activeTime > 0f;

        justTackleSucceeded = false;
        isJustTackleWindow = canUseJustTackle;
        justTackleTimer = canUseJustTackle ? justTackle.activeTime : 0f;

        // 通常タックル判定はタックル中ずっとON
        SetNormalTackleCollider(true);

        // ジャストタックル判定は受付時間中のみON
        SetJustTackleCollider(canUseJustTackle);
    }

    private void EndTackleHitboxes()
    {
        // タックル終了時は必ず両方OFF
        SetNormalTackleCollider(false);
        SetJustTackleCollider(false);

        // ジャスト受付状態を完全リセット
        ResetJustTackleState();

        // 候補も残さない
        ResetTackleHitCandidates();
    }

    private void ResetJustTackleState()
    {
        isJustTackleWindow = false;
        justTackleSucceeded = false;
        justTackleTimer = 0f;
    }

    private bool HasPendingTackleHitCandidates()
    {
        return tackleHitProcessIndex < tackleHitCandidates.Count;
    }

    private bool CanEndTackleByWall()
    {
        if (!isTackling)
            return false;

        if (isChargeTackle)
            return false;

        if (tackleParam.prioritizeHitBeforeWallEnd && HasPendingTackleHitCandidates())
            return false;

        if (tackleElapsedTimer < tackleParam.wallEndIgnoreTime)
            return false;

        if (tackleMovedDistance < tackleParam.wallEndIgnoreDistance)
            return false;

        return true;
    }

    private void FinishTackleByHit()
    {
        // コライダー、ジャスト状態、候補を確実に閉じる
        EndTackle();

        ReserveState(new Idle(), int.MaxValue);
    }


    void UpdateJustTackleWindow(float dt)
    {
        if (!isJustTackleWindow)
            return;

        justTackleTimer -= dt;

        if (justTackleTimer > 0f)
            return;

        isJustTackleWindow = false;
        justTackleTimer = 0f;

        SetJustTackleCollider(false);
    }

    private void SetNormalTackleCollider(bool enable)
    {
        if (normalTackle.tackleHitCollider == null)
            return;

        if (normalTackle.tackleHitCollider.activeSelf != enable)
            normalTackle.tackleHitCollider.SetActive(enable);
    }

    private void SetJustTackleCollider(bool enable)
    {
        if (justTackle.justTackleHitCollider == null)
            return;

        if (justTackle.justTackleHitCollider.activeSelf != enable)
            justTackle.justTackleHitCollider.SetActive(enable);
    }

    private void ResetTackleHitCandidates()
    {
        tackleHitCandidates.Clear();
        tackleHitProcessIndex = 0;
        processedTackleTargetIds.Clear();
    }

    private void StackTackleHitCandidate(
        TackleHitKind kind,
        Hitbox selfHitbox,
        Hitbox targetHitbox,
        Vector3 contactPoint)
    {
        if (!isTackling)
            return;

        if (selfHitbox == null || targetHitbox == null)
            return;

        if (!TryResolveTackleTarget(
            targetHitbox,
            out IHitReceiver receiver,
            out MonoBehaviour receiverMono,
            out GameObject targetObject,
            out int targetId))
        {
            return;
        }

        if (processedTackleTargetIds.Contains(targetId))
            return;

        TackleHitCandidate candidate = new TackleHitCandidate
        {
            kind = kind,
            selfHitbox = selfHitbox,
            targetHitbox = targetHitbox,
            receiver = receiver,
            receiverMono = receiverMono,
            targetObject = targetObject,
            targetId = targetId,
            contactPoint = contactPoint,
            valid = true
        };

        tackleHitCandidates.Add(candidate);
    }

    private bool TryResolveTackleTarget(
        Hitbox hitbox,
        out IHitReceiver receiver,
        out MonoBehaviour receiverMono,
        out GameObject targetObject,
        out int targetId)
    {
        receiver = null;
        receiverMono = null;
        targetObject = null;
        targetId = 0;

        if (hitbox == null)
            return false;

        // Hitboxが明示的に持っているreceiverを優先する
        if (hitbox.receiver != null)
        {
            receiver = hitbox.receiver;
            receiverMono = receiver as MonoBehaviour;

            if (receiverMono != null && receiverMono != this)
            {
                targetObject = receiverMono.gameObject;
                targetId = targetObject.GetInstanceID();
                return true;
            }
        }

        // receiverが攻撃Hitbox側に無い/適切でない場合は親方向から探索
        MonoBehaviour[] behaviours =
            hitbox.GetComponentsInParent<MonoBehaviour>();

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
                receiverMono = behaviour;
                targetObject = behaviour.gameObject;
                targetId = targetObject.GetInstanceID();
                return true;
            }
        }

        return false;
    }

    private void ResolvePendingTackleHits()
    {
        if (!isTackling)
            return;

        if (isNormalTacklePostHitKeeping)
            return;

        if (tackleHitProcessIndex >= tackleHitCandidates.Count)
            return;

        // 今回新しく追加された候補範囲だけを見る
        int startIndex = tackleHitProcessIndex;
        int endIndex = tackleHitCandidates.Count;

        // 対象ごとに、通常候補/ジャスト候補をまとめる
        Dictionary<int, TackleHitCandidate> normalMap =
            new Dictionary<int, TackleHitCandidate>();

        Dictionary<int, TackleHitCandidate> justMap =
            new Dictionary<int, TackleHitCandidate>();

        for (int i = startIndex; i < endIndex; i++)
        {
            TackleHitCandidate candidate = tackleHitCandidates[i];

            if (candidate == null || !candidate.valid)
                continue;

            if (processedTackleTargetIds.Contains(candidate.targetId))
                continue;

            if (candidate.kind == TackleHitKind.Just)
            {
                // 同じ対象に複数ジャスト候補があっても最初だけ採用
                if (!justMap.ContainsKey(candidate.targetId))
                    justMap.Add(candidate.targetId, candidate);
            }
            else
            {
                // 同じ対象に複数通常候補があっても最初だけ採用
                if (!normalMap.ContainsKey(candidate.targetId))
                    normalMap.Add(candidate.targetId, candidate);
            }
        }

        // ここまでの候補は今回処理対象にしたので、次回はこの後ろから見る
        tackleHitProcessIndex = endIndex;

        // まずジャストを優先処理
        foreach (var pair in justMap)
        {
            if (!isTackling)
                break;

            int targetId = pair.Key;

            if (processedTackleTargetIds.Contains(targetId))
                continue;

            TackleHitCandidate candidate = pair.Value;

            processedTackleTargetIds.Add(targetId);

            ApplyJustTackleCandidate(candidate);

            // 現状ジャスト成功時はタックル終了するので、ここで抜ける
            break;
        }

        if (!isTackling)
            return;

        // 次に通常/チャージタックルを処理
        bool appliedNormalOrChargeHit = false;

        foreach (var pair in normalMap)
        {
            if (!isTackling)
                break;

            int targetId = pair.Key;

            // ジャストで処理済みなら通常は適用しない
            if (processedTackleTargetIds.Contains(targetId))
                continue;

            TackleHitCandidate candidate = pair.Value;

            processedTackleTargetIds.Add(targetId);

            HandleAttack(candidate);
            appliedNormalOrChargeHit = true;
        }

        if (!appliedNormalOrChargeHit)
            return;

        // チャージタックルは今まで通り維持する。
        if (isChargeTackle)
            return;

        // 通常タックルは即終了せず、ほんの少しだけ余韻を持たせる。
        BeginNormalTacklePostHitKeep();
    }

    private void BeginNormalTacklePostHitKeep()
    {
        float keepTime = Mathf.Max(hpParam.normalTacklePostHitKeepTime, 0f);

        SetNormalTackleCollider(false);
        SetJustTackleCollider(false);

        isJustTackleWindow = false;
        justTackleTimer = 0f;

        tackleHitCandidates.Clear();
        tackleHitProcessIndex = 0;

        if (keepTime <= 0f)
        {
            FinishTackleByHit();
            return;
        }

        isNormalTacklePostHitKeeping = true;

        if (tackleTimer > keepTime)
            tackleTimer = keepTime;

        // ヒット後の余韻中は、壁終了判定で即Idleに戻らないようにする。
        tackleStuckTimer = 0f;
    }

    private void ApplyJustTackleCandidate(TackleHitCandidate candidate)
    {
        TackleGaugeManager.Instance.RecoverByJust();

        if (candidate == null)
            return;

        if (!isTackling)
            return;

        if (justTackleSucceeded)
            return;

        if (!isJustTackleWindow)
            return;


        Debug.Log("ジャストタックル成功");

        justTackleSucceeded = true;
        StartJustTackleDamageGuard();

        isJustTackleWindow = false;
        justTackleTimer = 0f;
        SetJustTackleCollider(false);

        if (candidate.receiver == null || candidate.receiverMono == null)
        {
            FinishTackleByHit();
            return;
        }


        float basePower =
            isChargeTackle ? chargeTackle.basePower : normalTackle.basePower;

        float baseRate = GetPowerRate();
        float justRate = Mathf.Max(baseRate, justTackle.minPowerRate);

        Vector3 dir =
            Vector3.Lerp(
                GetCameraForward(),
                tackleDir,
                justTackle.directionOverrideRate);

        if (dir.sqrMagnitude < 0.001f)
            dir = tackleDir;

        dir.Normalize();

        BlowPayload payload = new BlowPayload
        {
            tackleType = isChargeTackle ? TackleType.JustCharge : TackleType.JustNormal,
            damageConstant = isChargeTackle ? chargeTackle.tackleDamage * justTackle.powerMultiplier : normalTackle.tackleDamage * justTackle.powerMultiplier,
            powerConstant = basePower,
            powerRate = justRate * justTackle.powerMultiplier,
            powerDirection = dir
        };

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = candidate.selfHitbox.gameObject,

            // 攻撃Hitboxではなく、解決済みreceiverの本体を対象にする
            targetObject = candidate.receiverMono.gameObject,

            targetHitbox = candidate.targetHitbox.gameObject,

            payload = payload
        };

        candidate.receiver.OnHit(data);


        Vector3 justEffectPoint = candidate.contactPoint;

        if (!IsValidVector3(justEffectPoint))
        {
            if (candidate.targetHitbox != null)
            {
                Collider targetCollider = candidate.targetHitbox.GetComponent<Collider>();
                justEffectPoint = targetCollider != null
                    ? targetCollider.bounds.center
                    : candidate.targetObject.transform.position;
            }
            else if (candidate.targetObject != null)
            {
                justEffectPoint = candidate.targetObject.transform.position;
            }
            else
            {
                justEffectPoint = transform.position;
            }
        }
        Quaternion efRot = Quaternion.LookRotation(-dir);
        effectPlayer.PlayAt(8, justEffectPoint,efRot);



        audioManager.PlaySe("PL_JustHit");
        audioManager.PlaySe("PL_JustHit_B");

        Debug.Log(
            $"JustTackle Target : {candidate.receiverMono.name}, base={payload.powerConstant}, rate={payload.powerRate}, effective={payload.powerConstant * payload.powerRate}");


        if (hitStopCooldownTimer <= 0f)
        {
            GameTimeManager.Instance.HitStopLayer(
                TimeLayerType.Gameplay,
                justTackle.hitStopTime
            );

            hitStopCooldownTimer = justTackle.hitStopTime;
        }


        GameTimeManager.Instance.SlowGroupForSeconds(
            TimeGroupType.Enemy,
            justTackle.selfSlowScale,
            justTackle.selfSlowTime);

        GameTimeManager.Instance.SlowLayerForSeconds(
            TimeLayerType.Gameplay,
            justTackle.worldSlowScale,
            justTackle.worldSlowTime);


        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.PlayShake(
                dir,
                justTackle.shakePower,
                justTackle.shakeDuration);
        }


        FinishTackleByHit();
    }

    #endregion

    #endregion

    #region アニメーション
    private void UpdateAnimator()
    {
        // 独自TimeScaleをAnimatorにも反映
        animator.speed = agent.TimeScale;

        float speed = currentVelocity.magnitude / moveParam.moveSpeed;

        if (speed < 0.1f) speed = 0f;

        if (isTackling || IsDead) speed = 0f;

        animator.SetFloat("Speed", speed);

        Vector2 dirVecTor = CalculateMoveBlendDirection();

        animator.SetFloat("Front", dirVecTor.y);
        animator.SetFloat("Side", dirVecTor.x);


        animator.SetBool("IsTackling", isTackling);
        animator.SetInteger("TackleAnimIndex", tackleAnimIndex);
        animator.SetBool("IsGrounded", isGroundedStable);
        animator.SetBool("IsChaging", isCharging);
        //animator.SetBool("IsDamage", isDamage);

        float animVertical = isGroundedStable ? 0f : verticalVelocity;

        animator.SetFloat("VerticalVelocity", animVertical);
    }

    private Vector2 CalculateMoveBlendDirection()
    {
        float front = 0f;
        float side = 0f;


        if (isInKnockback ||
            inputMoveDir.sqrMagnitude > 0.0001f)
        {

            Vector3 camForward = GetCameraForward();
            Vector3 camRight = GetCameraRight();


            Vector3 worldMoveDir = Vector3.zero;

            if (isInKnockback)
            {
                worldMoveDir = knockbackVelocity;
            }
            else
            {
                worldMoveDir =
                    camForward * inputMoveDir.y +
                    camRight * inputMoveDir.x;
            }


            Vector3 localMoveDir =
                transform.InverseTransformDirection(worldMoveDir);

            localMoveDir.y = 0f;
            localMoveDir.Normalize();

            front = localMoveDir.z;
            side = localMoveDir.x;
        }

        front *= 0.5f;
        side *= 0.5f;

        Vector2 targetBlend =
            new Vector2(side, front);

        float blendSpeed = 10f;

        moveBlendDirection = Vector2.Lerp(
            moveBlendDirection,
            targetBlend,
            Time.deltaTime * blendSpeed);

        return moveBlendDirection;
    }

    #endregion

    #region ステート

    #region 基底ステート
    private abstract class PlayerState
    {
        protected PlayerController player;
        public void SetPlayerController(PlayerController p) => player = p;
        public virtual void EnterState() { }
        public virtual void UpdateState() { }
        public virtual void ExitState() { }

        public virtual MotionPolicy GetMotionPolicy()
        {
            return MotionPolicy.Default;
        }
    }
    #endregion

    #region Idle
    private class Idle : PlayerState
    {
        bool oldGrounded = true;
        public override void UpdateState()
        {

            if (player.isGroundedStable == true && oldGrounded == false)
            {
                player.effectPlayer.Play(9);
            }
            
            oldGrounded = player.isGroundedStable;

            // 着地直後などの通常タックルバッファ
            if (player.HasBufferedNormalTackle() &&
                player.CanTackle() &&
                TackleGaugeManager.Instance.CanUse())
            {
                player.ConsumeBufferedNormalTackle();
                player.ReserveState(new Tackle(), 100);
                return;
            }

            // 離した瞬間
            if (player.tackleUp && player.CanTackle() && TackleGaugeManager.Instance.CanUse())
            {
                if (player.tacklePressTimer < player.tackleParam.tapChargeTime)
                {
                    player.ConsumeBufferedNormalTackle();
                    player.ReserveState(new Tackle(), 100);
                    return;
                }
            }

            // 押しっぱなし中
            if (player.isGroundedStable)
            {
                if (player.tackleHold && player.CanTackle() && TackleGaugeManager.Instance.CanUse())
                {
                    if (player.tacklePressTimer >= player.tackleParam.tapChargeTime)
                    {
                        player.ReserveState(new Charge(), 50);
                        return;
                    }
                }
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.speedRate = 1f;

            if (player.jumpRequest && player.coyoteTimer > 0f)
            {
                player.ReserveState(new Jump(), 10);
                return;
            }

            if (dir.sqrMagnitude > 0.001f)
            {
                player.ReserveState(new Run(), 1);
            }
        }
    }
    #endregion

    #region Run
    private class Run : PlayerState
    {
        private EffectInstance LrunEffect = null;
        private EffectInstance RrunEffect = null;

        private GameObject ArmBoosterL = null;
        private GameObject ArmBoosterR = null;

        private bool oldGrounded = true;

        public override void EnterState()
        {
            ArmBoosterL = player.effectBonePlayer.GetEffect(2);
            ArmBoosterR = player.effectBonePlayer.GetEffect(3);
        }

        public override void UpdateState()
        {

            if(player.isGroundedStable == true && oldGrounded == false)
            {
                player.effectPlayer.Play(9);
            }
            
            oldGrounded = player.isGroundedStable;

            if (player.isGroundedStable)
            {
                

                if (LrunEffect == null && RrunEffect == null)
                {
                    //EffectPlayParam param = EffectPlayParam.Default;
                    //param.positionOffset.y = -1.0f;

                    LrunEffect = player.effectPlayer.Play(0);

                    LrunEffect.Attach(
                        player.moveParam.LEffect,
                        player.transform);

                    RrunEffect = player.effectPlayer.Play(0);

                    RrunEffect.Attach(
                        player.moveParam.REffect,
                        player.transform);
                    player.effectBonePlayer.PlayEffect(0);
                    player.effectBonePlayer.PlayEffect(1);
                    player.effectBonePlayer.PlayEffect(2);
                    player.effectBonePlayer.PlayEffect(3);

                }
            }
            else
            {
                if (LrunEffect != null && RrunEffect != null)
                {
                    LrunEffect.Stop();
                    LrunEffect = null;

                    RrunEffect.Stop();
                    RrunEffect = null;

                    //player.effectBonePlayer.StopEffect(0);
                    //player.effectBonePlayer.StopEffect(1);
                    //player.effectBonePlayer.StopEffect(2);
                    //player.effectBonePlayer.StopEffect(3);
                }
            }

            if (LrunEffect && RrunEffect)
            {
                LrunEffect.SetYawOffset(
                       player.inputMoveDir);

                RrunEffect.SetYawOffset(
                       player.inputMoveDir);
            }

            updateBoosterSize();

            // 着地直後などの通常タックルバッファ
            if (player.HasBufferedNormalTackle() &&
                player.CanTackle() &&
                TackleGaugeManager.Instance.CanUse())
            {
                player.ConsumeBufferedNormalTackle();
                player.ReserveState(new Tackle(), 100);
                return;
            }

            // 離した瞬間
            if (player.tackleUp && player.CanTackle() && TackleGaugeManager.Instance.CanUse())
            {
                if (player.tacklePressTimer < player.tackleParam.tapChargeTime)
                {
                    player.ConsumeBufferedNormalTackle();
                    player.ReserveState(new Tackle(), 100);
                    return;
                }
            }

            // 押しっぱなし中
            if (player.isGroundedStable)
            {
                if (player.tackleHold && player.CanTackle() && TackleGaugeManager.Instance.CanUse())
                {
                    if (player.tacklePressTimer >= player.tackleParam.tapChargeTime)
                    {
                        player.ReserveState(new Charge(), 50);
                        return;
                    }
                }
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.speedRate = 1f;

            if (player.jumpRequest && player.coyoteTimer > 0f)
            {
                player.ReserveState(new Jump(), 10);
                return;
            }

            if (dir.sqrMagnitude < 0.001f)
            {
                player.ReserveState(new Idle(), 0);
            }
        }

        private void updateBoosterSize()
        {
            if (ArmBoosterL == null || ArmBoosterR == null) return;

            float inputSize = player.inputMoveDir.x;

            ArmBoosterL.transform.localScale = Vector3.one + Vector3.one *  inputSize;
            if(ArmBoosterL.transform.localScale.magnitude <= 0.3f)
            {
                ArmBoosterL.transform.localScale = Vector3.one * 0.3f;
            }
            else if(ArmBoosterL.transform.localScale.magnitude > 1.0f)
            {
                ArmBoosterL.transform.localScale = Vector3.one;
            }

            ArmBoosterR.transform.localScale = Vector3.one + Vector3.one * -inputSize;
            if (ArmBoosterR.transform.localScale.magnitude <= 0.3f)
            {
                ArmBoosterR.transform.localScale = Vector3.one * 0.3f;
            }
            else if (ArmBoosterR.transform.localScale.magnitude > 1.0f)
            {
                ArmBoosterR.transform.localScale = Vector3.one;
            }

        }


        public override MotionPolicy GetMotionPolicy()
        {
            // 現状はスクリプト移動に戻すため、RunでもRootMotionは使わない
            // 将来再採用する場合は下のような条件に戻す
            //
            // bool useRM = player.runRootMotion.enable && player.isGroundedStable;
            // return new MotionPolicy
            // {
            //     useRootMotionPosition = useRM,
            //     useRootMotionRotation = useRM && player.runRootMotion.useRootMotionRotation,
            //     useRootMotionY = player.runRootMotion.useRootMotionY,
            //     ignoreGravity = false,
            //     rootMotionScale = player.runRootMotion.moveScale
            // };

            return MotionPolicy.Default;
        }


        public override void ExitState()
        {
            if (LrunEffect != null || RrunEffect != null)
            {
                LrunEffect.Stop();
                LrunEffect = null;
                RrunEffect.Stop();
                RrunEffect = null;
            }

            player.effectBonePlayer.StopEffect(0);
            player.effectBonePlayer.StopEffect(1);
            player.effectBonePlayer.StopEffect(2);
            player.effectBonePlayer.StopEffect(3);
        }
    }
    #endregion

    #region Jump
    private class Jump : PlayerState
    {
        public override void EnterState()
        {
            player.StartJump();
            player.effectPlayer.Play(1);

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.AddJumpCount();
            }

            if (player.audioManager != null)
            {
                player.audioManager.PlaySe("PL_Jump");
            }
        }
        public override void UpdateState()
        {
            if (player.tackleDown && player.CanTackle() && TackleGaugeManager.Instance.CanUse())
            {
                player.ConsumeBufferedNormalTackle();
                player.ReserveState(new Tackle(), 100);
                return;
            }

            // 空中で短押しを離していた場合、着地した瞬間にここで拾う
            if (player.HasBufferedNormalTackle() &&
                player.isGroundedStable &&
                player.CanTackle() &&
                TackleGaugeManager.Instance.CanUse())
            {
                player.ConsumeBufferedNormalTackle();
                player.ReserveState(new Tackle(), 100);
                return;
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.speedRate = 1f;

            if (player.isGroundedStable && player.verticalVelocity <= 0f)
            {
                if (dir.sqrMagnitude > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
        }

        public override void ExitState()
        {
            player.effectPlayer.Play(9);
        }
    }
    #endregion

    #region Charge
    private class Charge : PlayerState
    {
        EffectInstance chargeEffect = null;

        bool isCompleate = false;

        int soundHundle = -1;

        public override void EnterState()
        {
            player.isCharging = true;
            player.chargeTimer = 0f;

            // 移動止める
            player.currentVelocity = Vector3.zero;

            chargeEffect = null;

            chargeEffect = player.effectPlayer.Play(4);

            isCompleate = false;

            TackleGaugeManager.Instance.SetCharging(true);


            if (!TackleGaugeManager.Instance.CanUse())
            {
                player.ReserveState(new Idle(), 100);
                return;
            }

            if (player.audioManager != null)
            {
                soundHundle = player.audioManager.PlaySeWithHandle("PL_Charge");
            }

        }


        public override void UpdateState()
        {
            float dt = Time.deltaTime * player.agent.TimeScale;

            player.chargeTimer += dt;


            bool over = TackleGaugeManager.Instance.ConsumeCharge(dt);

            if (over)
            {
                player.StartChargeTackle(true); // ★暴発
                player.ReserveState(new ChargeTackleState(), 100);
                return;
            }




            if (!isCompleate && player.chargeTimer >= player.chargeTackle.chargeTime)
            {
                isCompleate = true;

                TackleGaugeManager.Instance.SetChargeComplete();

                player.effectPlayer.Play(5);

                if (player.audioManager != null)
                {
                    soundHundle = player.audioManager.PlaySeWithHandle("PL_ChargeCompleate");
                }

            }


            if (player.tackleUp)
            {
                if (player.chargeTimer >= player.chargeTackle.chargeTime)
                {
                    player.ReserveState(new ChargeTackleState(), 100);
                }
                else
                {
                    player.ReserveState(new Idle(), 0);
                }
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            Vector3 camForward = player.GetCameraForward();
            player.request.speedRate = 1f;

        }

        public override void ExitState()
        {
            player.isCharging = false;
            if (chargeEffect != null)
            {
                chargeEffect.StopImmediate();
                chargeEffect = null;
            }
            if (player.audioManager != null && soundHundle != -1)
            {
                player.audioManager.StopSe(soundHundle);
            }
            TackleGaugeManager.Instance.SetCharging(false);
        }
    }
    #endregion

    #region Tackle
    private class Tackle : PlayerState
    {
        public override void EnterState()
        {
            player.StartTackle();

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.AddTackleCount();
            }

            if (player.audioManager != null)
            {
                player.audioManager.PlaySe("PL_Tackle");
            }
        }

        public override void UpdateState()
        {
           
            if (player.tackleTimer <= 0f)
            {
                var dir = player.inputMoveDir.magnitude;

                if (dir > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
        }

        public override void ExitState()
        {
            player.EndTackle();
        }
    }
    #endregion

    #region ChargeTackle
    private class ChargeTackleState : PlayerState
    {
        public override void EnterState()
        {
            player.StartChargeTackle();

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.AddTackleCount();
            }

            if (player.audioManager != null)
            {
                player.audioManager.PlaySe("PL_ChargeTackle");
            }
        }

        public override void UpdateState()
        {
            if (player.tackleTimer <= 0f)
            {
                if (player.inputMoveDir.magnitude > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
        }

        public override void ExitState()
        {
            player.EndTackle();
        }
    }
    #endregion

    #region Damage
    private class DamageState : PlayerState
    {
        public override void EnterState()
        {
            player.request.Clear();

            player.animator.ResetTrigger("Damage");
            player.animator.SetTrigger("Damage");

        }

        public override void UpdateState()
        {
          
            if (player.isInKnockback)
                return;

            if (player.isInRecover)
                return;

            if (player.isInInvincible)
            {
                // 復帰後無敵中でも操作自体は戻してよいなら、ここで通常ステートへ戻す。
                // 完全に無敵終了まで待たせたい場合は return のままでよい。
                // 今回は操作復帰を優先する。
            }

            if (player.IsDead)
            {
                player.ReserveState(new DeadState(), 10000);
                return;
            }

            // ===== 通常ステートへ復帰 =====
            if (player.isGroundedStable)
            {
                if (player.inputMoveDir.magnitude > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
            else
            {
                player.ReserveState(new Jump(), 0);
            }
        }

        public override void ExitState()
        {
        }
    }

    #endregion

    #region Dead

    private class DeadState : PlayerState
    {
        public override void EnterState()
        {
            player.EnterDead();
        }

        public override void UpdateState()
        {
        }
    }

    #endregion

    #endregion

    #region ステート管理
    private void ReserveState(PlayerState s, int priority)
    {
        if (priority < reservePriority) return;
        reserveState = s;
        reservePriority = priority;
    }

    private void ApplyReserveState()
    {
        if (reserveState == null) return;
        nowState?.ExitState();
        nowState = reserveState;
        nowState.SetPlayerController(this);
        reserveState = null;
        nowState?.EnterState();
    }
    #endregion

    #region HP / 被ダメ処理

    private bool IsDamageHitbox(GameObject targetHitbox)
    {
        if (hpParam.damageHitCollider == null)
            return true;

        if (targetHitbox == null)
            return false;

        if (targetHitbox == hpParam.damageHitCollider)
            return true;

        return targetHitbox.transform.IsChildOf(hpParam.damageHitCollider.transform);
    }

    private bool IsUnblockableDamageHitbox(GameObject targetHitbox)
    {
        if (hpParam.unblockableDamageHitCollider == null)
            return false;

        if (targetHitbox == null)
            return false;

        if (targetHitbox == hpParam.unblockableDamageHitCollider)
            return true;

        return targetHitbox.transform.IsChildOf(hpParam.unblockableDamageHitCollider.transform);
    }

    private bool IsHyperArmorActive()
    {
        if (!hpParam.enableTackleHyperArmor)
            return false;

        // 通常タックル・チャージタックル中はハイパーアーマー
        return isTackling || isChargeTackle;
    }

    private bool IsJustTackleDamageGuardActive()
    {
        return justTackleDamageGuardTimer > 0f;
    }

    private void StartJustTackleDamageGuard()
    {
        justTackleDamageGuardTimer =
            Mathf.Max(hpParam.justTackleDamageGuardTime, 0f);
    }

    private void UpdateJustTackleDamageGuard(float dt)
    {
        if (justTackleDamageGuardTimer <= 0f)
            return;

        justTackleDamageGuardTimer -= dt;

        if (justTackleDamageGuardTimer < 0f)
            justTackleDamageGuardTimer = 0f;
    }

    private void UpdateUnblockableDamageCooldown(float dt)
    {
        if (unblockableDamageCooldownTimer <= 0f)
            return;

        unblockableDamageCooldownTimer -= dt;

        if (unblockableDamageCooldownTimer < 0f)
            unblockableDamageCooldownTimer = 0f;
    }

    private bool CanApplyNormalDamageNow()
    {
        if (IsDead)
            return false;

        // ノックバック中・復帰中・復帰後無敵中は通常被ダメしない
        if (isInKnockback || isInRecover || isInInvincible)
            return false;

        // ジャスト成功直後の保険。
        if (IsJustTackleDamageGuardActive())
            return false;

        return true;
    }

    private bool CanApplyUnblockableDamageNow()
    {
        if (IsDead)
            return false;

        if (unblockableDamageCooldownTimer > 0f)
            return false;

        if (!hpParam.unblockableIgnoresDamageState)
        {
            if (isInKnockback || isInRecover || isInInvincible)
                return false;
        }

        return true;
    }

    private void ApplyEnemyDamage(HitEventData data, EnemyAttackPayload payload, bool isUnblockable)
    {

        if (isUnblockable)
        {
            if (!CanApplyUnblockableDamageNow())
                return;
        }
        else
        {
            if (!CanApplyNormalDamageNow())
                return;
        }

        int damage = Mathf.Max(payload.damage, 0);
        if (damage <= 0)
            return;

        // ===== HP減少 =====
        PlayerHPManager.Instance.Damage(damage);

        if (effectPlayer)
        {
            EffectPlayParam param = EffectPlayParam.Default;

            Vector3 dir = -GetDamageKnockbackDirection(data.attackerObject);

            param.positionOffset = dir * 1.0f;


            effectPlayer.Play(10, param);
        }


        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.PlayShake(
                -transform.forward,
                isUnblockable ? 0.35f : 0.2f,
                isUnblockable ? 0.15f : 0.1f
            );
        }

        if (isUnblockable)
        {
            unblockableDamageCooldownTimer =
                Mathf.Max(hpParam.unblockableDamageCooldownTime, 0f);
        }

        // ===== ハイパーアーマー =====
        // 通常攻撃:
        //   タックル中はダメージだけ受けてノックバックしない。
        //
        // 防御不可攻撃:
        //   ハイパーアーマーを無視して必ずノックバックする。
        if (!isUnblockable && IsHyperArmorActive())
        {
            // 通常攻撃のハイパーアーマー中はノックバックしない。
            // コライダー状態はそのまま維持。
            UpdateDamageColliderEnabled();
            return;
        }

        // ===== アクション中断 =====
        // 防御不可攻撃、またはハイパーアーマーが無い通常被ダメでは中断する。
        if (isTackling)
            EndTackle();


        isNormalTacklePostHitKeeping = false;

        // ===== 既存被ダメフェーズを明示的にリセット =====
        // これを入れることで、前回の復帰/無敵状態が中途半端に残る事故を避ける。
        isInKnockback = false;
        isInRecover = false;
        isInInvincible = false;

        damageRecoverTimer = 0f;
        invincibleTimer = 0f;


        // ===== ジャンプ状態を破棄 =====

        jumpRequest = false;
        jumpBufferTimer = 0f;

        coyoteTimer = 0f;
        jumpGroundIgnoreTimer = 0f;

        canAirTackle = false;


        // ===== 上昇キャンセル =====

        if (verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }



        // ===== ノックバック開始 =====
        Vector3 knockbackDir = GetDamageKnockbackDirection(data.attackerObject);
        //Vector3 knockbackDir = GetDamageKnockbackDirection(data.attackerHitbox);

        float knockbackPower = isUnblockable && hpParam.unblockableKnockbackPower > 0f
            ? hpParam.unblockableKnockbackPower
            : hpParam.knockbackPower;

        float knockbackTime = isUnblockable && hpParam.unblockableKnockbackTime > 0f
            ? hpParam.unblockableKnockbackTime
            : hpParam.knockbackTime;

        knockbackVelocity = knockbackDir * knockbackPower;
        knockbackTimer = Mathf.Max(knockbackTime, 0f);

        isInKnockback = knockbackTimer > 0f;

        currentVelocity = knockbackVelocity;

        // 被ダメ開始時点でコライダー状態を即反映
        UpdateDamageColliderEnabled();

        // ノックバック時間が0なら即復帰時間へ
        if (!isInKnockback)
        {
            knockbackVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;

            isInRecover = true;
            damageRecoverTimer = Mathf.Max(hpParam.damageRecoverTime, 0f);

            UpdateDamageColliderEnabled();

            if (damageRecoverTimer <= 0f)
            {
                isInRecover = false;
                StartPostRecoverInvincible();
            }
        }

        // DamageState は被ダメ中のステート表示/操作抑制用。
        // 実際の復帰タイマーは UpdateDamageTimers() 側で進む。
        ReserveState(new DamageState(), 10000);
    }

    private Vector3 GetDamageKnockbackDirection(GameObject attackerObject)
    {
        if (attackerObject == null)
        {
            Debug.LogWarning("attackerObject == null");
            Vector3 fallback = transform.position;

            fallback = -transform.forward;
            fallback.y = 0f;

            return fallback.normalized;
        }

        Vector3 dir =
            transform.position -
            attackerObject.transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = -transform.forward;
        }

        return dir.normalized;
    }

    private void EnterDead()
    {
        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
        knockbackVelocity = Vector3.zero;
        knockbackTimer = 0f;
        damageRecoverTimer = 0f;
        invincibleTimer = 0f;
        justTackleDamageGuardTimer = 0f;
        unblockableDamageCooldownTimer = 0f;

        inputLook = Vector2.zero;
        smoothedLookInput = Vector2.zero;
        lookInputVelocity = Vector2.zero;
        tackleDown = false;
        tackleUp = false;
        tackleHold = false;
        tacklePressTimer = 0f;
        tackleTapBufferTimer = 0f;
        ignoreTackleUntilReleased = false;

        currentControllerYawSpeed = 0f;
        currentControllerPitchSpeed = 0f;

        isInKnockback = false;
        isInRecover = false;
        isInInvincible = false;
        isNormalTacklePostHitKeeping = false;

        if (isTackling)
            EndTackle();

        footstepTimer = 0f;
        groundedStableTimer = 0f;
        airborneTimer = 0f;


        UpdateDamageColliderEnabled();

        if (hpParam.disableInputOnDeath && playerInputMap != null)
        {
            playerInputMap.Disable();
        }
    }

    private void UpdateDamageColliderEnabled()
    {
        if (hpParam == null)
            return;


        bool isDamageStateActive =
            isInKnockback ||
            isInRecover ||
            isInInvincible;


        bool enableNormalDamage =
            !isDamageStateActive &&
            !IsDead;

        // 通常被ダメコライダー
        if (hpParam.damageHitCollider != null)
        {
            if (hpParam.damageHitCollider.activeSelf != enableNormalDamage)
                hpParam.damageHitCollider.SetActive(enableNormalDamage);
        }

        // 防御不可被ダメコライダー
        if (hpParam.unblockableDamageHitCollider != null)
        {
            bool enableUnblockableDamage;

            if (hpParam.unblockableIgnoresDamageState)
            {
                enableUnblockableDamage = !IsDead;
            }
            else
            {
                enableUnblockableDamage = enableNormalDamage;
            }

            if (hpParam.unblockableDamageHitCollider.activeSelf != enableUnblockableDamage)
                hpParam.unblockableDamageHitCollider.SetActive(enableUnblockableDamage);
        }
    }

    private void StartPostRecoverInvincible()
    {
        float time = Mathf.Max(hpParam.postRecoverInvincibleTime, 0f);

        if (time <= 0f)
        {
            isInInvincible = false;
            invincibleTimer = 0f;
            UpdateDamageColliderEnabled();
            return;
        }

        isInInvincible = true;
        invincibleTimer = time;

        UpdateDamageColliderEnabled();
    }

    private void UpdatePostRecoverInvincible(float dt)
    {
        if (!isInInvincible)
            return;

        if (dt <= 0f)
            return;

        invincibleTimer -= dt;

        if (invincibleTimer > 0f)
        {
            UpdateDamageColliderEnabled();
            return;
        }

        invincibleTimer = 0f;
        isInInvincible = false;

        // 無敵終了時に被ダメコライダーを戻す
        UpdateDamageColliderEnabled();
    }

    private void UpdateDamageTimers(float dt)
    {
        // ジャスト成功直後の通常被ダメ保険
        UpdateJustTackleDamageGuard(dt);

        // 防御不可攻撃の連続ヒット防止
        UpdateUnblockableDamageCooldown(dt);

        // ノックバック中は ApplyDamageKnockbackMovement 側で処理する
        if (isInKnockback)
        {
            UpdateDamageColliderEnabled();
            return;
        }

        // ノックバック終了後の復帰時間。
        if (isInRecover)
        {
            damageRecoverTimer -= dt;

            if (damageRecoverTimer <= 0f)
            {
                damageRecoverTimer = 0f;
                isInRecover = false;

                // 復帰後無敵へ移行
                StartPostRecoverInvincible();
            }

            UpdateDamageColliderEnabled();
            return;
        }

        // 復帰後無敵
        UpdatePostRecoverInvincible(dt);

        // 最終的に毎フレーム安全に同期する
        UpdateDamageColliderEnabled();
    }

    #endregion

    #region ワープ処理
   
    public void Warp(Vector3 worldPosition)
    {
        QueueWarp(worldPosition, transform.rotation, false, 0);
    }
    public void Warp(Vector3 worldPosition, Quaternion worldRotation)
    {
        QueueWarp(worldPosition, worldRotation, true, 0);
    }
    public void WarpWithDamage(Vector3 worldPosition, int damage)
    {
        QueueWarp(worldPosition, transform.rotation, false, damage);
    }
    public void WarpWithDamage(Vector3 worldPosition, Quaternion worldRotation, int damage)
    {
        QueueWarp(worldPosition, worldRotation, true, damage);
    }
    private void QueueWarp(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool applyRotation,
        int damage)
    {
        if (IsDead)
            return;

        pendingWarpPosition = worldPosition;
        pendingWarpRotation = worldRotation;
        pendingWarpApplyRotation = applyRotation;
        pendingWarpDamage = Mathf.Max(damage, 0);

        hasPendingWarp = true;
    }
    private bool ProcessPendingWarp()
    {
        if (!hasPendingWarp)
            return false;

        hasPendingWarp = false;

        ImmediateWarpInternal(
            pendingWarpPosition,
            pendingWarpRotation,
            pendingWarpApplyRotation,
            pendingWarpDamage
        );

        pendingWarpDamage = 0;
        pendingWarpApplyRotation = false;

        return true;
    }
    private void ImmediateWarpInternal(
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool applyRotation,
        int damage)
    {
        if (IsDead)
            return;

        // タックル、落下タックル、チャージ、候補Hitboxなどを安全に停止する
        ResetActionStateForWarp();

        // 移動・ジャンプ・ノックバック系の速度を完全に止める
        ResetMovementStateForWarp();

        // CharacterControllerを一時的に止めてから座標を書き換える
        bool controllerWasEnabled = controller != null && controller.enabled;

        if (controllerWasEnabled)
            controller.enabled = false;

        if (applyRotation)
            transform.SetPositionAndRotation(worldPosition, worldRotation);
        else
            transform.position = worldPosition;

        Physics.SyncTransforms();

        if (controllerWasEnabled)
            controller.enabled = true;

        // CharacterController内部状態を軽く同期する
        if (controller != null && controller.enabled)
        {
            controller.Move(Vector3.zero);
            Physics.SyncTransforms();
        }

        // ワープ先の接地状態をすぐ再判定する
        RefreshGroundStateAfterWarp();

        // ワープダメージ
        if (damage > 0)
        {
            ApplyWarpDamage(damage);
        }

        // 死亡していなければ通常操作状態へ戻す

        if (!IsDead)
        {
            ReserveState(new Idle(), int.MaxValue);
            ApplyReserveState();
        }


        UpdateAnimator();
        UpdateDamageColliderEnabled();
    }
    private void ResetActionStateForWarp()
    {
        if (TackleGaugeManager.Instance != null)
        {
            TackleGaugeManager.Instance.SetTackling(false);
            TackleGaugeManager.Instance.SetCharging(false);
        }

        SetNormalTackleCollider(false);
        SetJustTackleCollider(false);

        ResetJustTackleState();
        ResetTackleHitCandidates();

        if (tackleEffect != null)
        {
            tackleEffect.Stop();
            tackleEffect = null;
        }

        //if (runEffect != null)
        //{
        //    runEffect.Stop();
        //    runEffect = null;
        //}


        isTackling = false;
        isChargeTackle = false;
        isCharging = false;
        isNormalTacklePostHitKeeping = false;

        isJustTackleWindow = false;
        justTackleSucceeded = false;
        justTackleTimer = 0f;

        tackleTimer = 0f;
        tackleElapsedTimer = 0f;
        tackleMovedDistance = 0f;
        tackleStuckTimer = 0f;


        chargeTimer = 0f;
    }
    private void ResetMovementStateForWarp()
    {
        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
        knockbackVelocity = Vector3.zero;

        jumpBufferTimer = 0f;
        jumpRequest = false;
        jumpGroundIgnoreTimer = 0f;
        coyoteTimer = 0f;

        knockbackTimer = 0f;
        damageRecoverTimer = 0f;

        isInKnockback = false;
        isInRecover = false;

        inputLook = Vector2.zero;
        smoothedLookInput = Vector2.zero;
        lookInputVelocity = Vector2.zero;

        tackleDown = false;
        tackleUp = false;
        tackleHold = false;
        tacklePressTimer = 0f;
        tackleTapBufferTimer = 0f;
        ignoreTackleUntilReleased = false;

        currentControllerYawSpeed = 0f;
        currentControllerPitchSpeed = 0f;

        footstepTimer = 0f;
        groundedStableTimer = 0f;
        airborneTimer = 0f;

        if (controller != null && jumpParam.disableStepOffsetWhileAirborne)
            controller.stepOffset = defaultStepOffset;
    }
    private void RefreshGroundStateAfterWarp()
    {
        isGroundedStable = CheckGroundStable();

        if (isGroundedStable)
        {
            coyoteTimer = jumpParam.coyoteTime;
            verticalVelocity = -slopeParam.groundStickForce;
            canAirTackle = true;
        }
        else
        {
            coyoteTimer = 0f;

            verticalVelocity = -0.01f;
        }
    }

    private void ApplyWarpDamage(int damage)
    {
        if (damage <= 0)
            return;

        if (IsDead)
            return;

        PlayerHPManager.Instance.Damage(damage);

        if (cameraParam.cameraController != null)
        {
            cameraParam.cameraController.PlayShake(
                Vector3.down,
                0.25f,
                0.15f
            );
        }

        StartPostRecoverInvincible();

        UpdateDamageColliderEnabled();

        if (IsDead)
        {
            ReserveState(new DeadState(), 10000);
            ApplyReserveState();
        }
    }

    #endregion

    #region 足音制御

    private void UpdateFootstepState(float dt)
    {
        if (footstepTimer > 0f)
            footstepTimer -= dt;

        if (footstepTimer < 0f)
            footstepTimer = 0f;

        if (isGroundedStable)
        {
            groundedStableTimer += dt;
            airborneTimer = 0f;
        }
        else
        {
            airborneTimer += dt;
            groundedStableTimer = 0f;
        }
    }
    public void RequestFootstepFromAnimation()
    {
        if (!CanPlayFootstepNow())
            return;

        PlayFootstepSe();
    }

    private bool CanPlayFootstepNow()
    {
        // AudioManager未取得なら鳴らさない
        if (audioManager == null)
            return false;

        // 最低再生間隔
        if (footstepTimer > 0f)
            return false;

        // Run中以外は鳴らさない
        if (nowState is not Run)
            return false;

        // 接地していないなら鳴らさない
        if (!isGroundedStable)
            return false;

        // 接地直後のカチカチ対策
        if (groundedStableTimer < footstepGroundStableTime)
            return false;

        // 地面を離れた直後の誤発音対策
        if (airborneTimer > 0f && airborneTimer < footstepBlockTimeAfterLeaveGround)
            return false;

        // 入力が無いなら鳴らさない
        if (inputMoveDir.sqrMagnitude < 0.0001f)
            return false;

        // 上昇中は鳴らさない
        if (verticalVelocity > 0.05f)
            return false;

        // ジャンプ入力が押された瞬間は鳴らさない
        if (jumpPressed)
            return false;

        // 各種特殊状態では鳴らさない

        if (isTackling || isCharging)
            return false;


        if (isInKnockback || isInRecover || isInInvincible)
            return false;

        if (IsDead)
            return false;

        return true;
    }

    private void PlayFootstepSe()
    {
        audioManager.PlaySe(footstepSeName);
        footstepTimer = footstepInterval;
    }

    #endregion

    #region 当たり判定
    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (selfHitbox == null)
            return;

        if (other == null)
            return;

        var otherHitbox = other.GetComponent<Hitbox>();
        if (otherHitbox == null)
            return;

        if (!isTackling)
            return;

        bool isJustTackleHitbox =
            justTackle.justTackleHitCollider != null &&
            selfHitbox.gameObject == justTackle.justTackleHitCollider;

        bool isNormalTackleHitbox =
            normalTackle.tackleHitCollider != null &&
            selfHitbox.gameObject == normalTackle.tackleHitCollider;

        // タックル系Hitbox以外はここでは扱わない
        if (!isJustTackleHitbox && !isNormalTackleHitbox)
            return;

        // ===== 前方角度制限 =====
        if (!IsTackleTargetInFront(other))
            return;

        // ===== 接触点取得 =====
        Vector3 contactPoint = GetContactPoint(selfHitbox.GetComponent<Collider>(), other);

        // ジャストタックル候補
        if (isJustTackleHitbox)
        {
            if (!isJustTackleWindow)
                return;

            if (justTackleSucceeded)
                return;

            StackTackleHitCandidate(
                TackleHitKind.Just,
                selfHitbox,
                otherHitbox,
                contactPoint);

            return;
        }

        // 通常/チャージタックル候補
        if (isNormalTackleHitbox)
        {
            StackTackleHitCandidate(
                TackleHitKind.Normal,
                selfHitbox,
                otherHitbox,
                contactPoint);

            return;
        }
    }



    public void OnHit(HitEventData data)
    {
        if (data.payload is not EnemyAttackPayload enemyAttack)
            return;

        // 防御不可攻撃専用コライダー。
        if (IsUnblockableDamageHitbox(data.targetHitbox))
        {
            ApplyEnemyDamage(data, enemyAttack, true);
            return;
        }

        // 通常被ダメコライダー。
        if (IsDamageHitbox(data.targetHitbox))
        {
            ApplyEnemyDamage(data, enemyAttack, false);
            return;
        }
    }

    private Vector3 GetContactPoint(Collider attackCollider, Collider targetCollider)
    {
        if (attackCollider == null && targetCollider == null)
            return transform.position;

        if (attackCollider == null)
            return targetCollider.bounds.center;

        if (targetCollider == null)
            return attackCollider.bounds.center;

        Vector3 attackCenter = attackCollider.bounds.center;
        Vector3 targetCenter = targetCollider.bounds.center;

        Vector3 dir = targetCenter - attackCenter;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = tackleDir;

            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward;

            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        float attackExtent = attackCollider.bounds.extents.magnitude;
        float targetExtent = targetCollider.bounds.extents.magnitude;
        float centerDistance = Vector3.Distance(attackCenter, targetCenter);

        Vector3 rayStart = attackCenter - dir * (attackExtent + 0.2f);
        float rayDistance = attackExtent + centerDistance + targetExtent + 0.5f;

        Ray ray = new Ray(rayStart, dir);

        if (targetCollider.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            return hit.point;
        }

        Vector3 pointOnTarget = targetCollider.ClosestPoint(attackCenter);
        Vector3 pointOnAttack = attackCollider.ClosestPoint(targetCenter);

        Vector3 midPoint = (pointOnTarget + pointOnAttack) * 0.5f;

        if (IsValidVector3(midPoint))
            return midPoint;

        if (IsValidVector3(pointOnTarget))
            return pointOnTarget;

        return targetCenter;
    }

    private bool IsValidVector3(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    #endregion
}