using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(TimeAgent))]
public class RugbyBoss : MonoBehaviour, IHitReceiver, IHitSource
{
    private enum BossActionType
    {
        Try,
        Haka,
        ScrumCommand,
        RocketPunch,
        AngryBeam
    }

    private enum RocketPunchPhase
    {
        Launch,
        Keep,
        Return
    }

    #region Inspector Parameter Classes

    [System.Serializable]
    private class StartParam
    {
        [Header("開始制御")]
        [Tooltip("ONなら外部からStartBoss()が呼ばれるまで、行動開始を待ちます。Idle状態には入ります。")]
        public bool waitForExternalStart = true;

        [Tooltip("単体確認用です。ONならStart時点でStartBoss()を呼びます。")]
        public bool autoStartForDebug = false;
        public CameraManager cameraManager;
    }

    [System.Serializable]
    private class StatusParam
    {
        [Header("HP")]
        [Tooltip("ボスの最大HPです。")]
        public float maxHP = 300f;

        [Tooltip("このHP以下になると怒り状態へ移行します。")]
        public float angryHPThreshold = 150f;

        [Tooltip("BlowPayloadからダメージを取れなかった場合に使う仮ダメージです。")]
        public float fallbackTackleDamage = 10f;

        [Tooltip("ONならBlowPayloadのpowerConstant × powerRateをダメージとして使います。")]
        public bool useBlowPayloadDamage = true;

        [Tooltip("BlowPayloadダメージ倍率。")]
        public float playerTackleDamageMultiplier = 2.0f;

        [Header("ダウン")]
        [Tooltip("通常時にダウンへ入るために必要なダウン値です。")]
        public float downGaugeMax = 100f;

        [Tooltip("怒り時にダウンへ入るために必要なダウン値です。")]
        public float angryDownGaugeMax = 150f;

        [Tooltip("プレイヤータックルを受けた時に加算するダウン値です。")]
        public float playerTackleDownValue = 20f;

        [Tooltip("ONならBlowPayloadのpowerRateをダウン値加算にも掛けます。")]
        public bool multiplyTackleDownByPowerRate = false;

        [Tooltip("ダウン復帰後に設定するダウン値です。基本は0です。")]
        public float downGaugeAfterRecovery = 0f;

        [Header("怒り")]
        [Tooltip("怒り状態へ入った瞬間にダウン値をリセットするかどうかです。")]
        public bool resetDownGaugeOnAngryEnter = true;

        [Tooltip("怒り状態へ入った瞬間に設定するダウン値です。基本は0です。")]
        public float downGaugeOnAngryEnter = 0f;

        [Header("チェイン被弾")]
        public float chainHitDownValue = 30f;
        public float fallbackChainDamage = 25f;

        [Header("エネミーデータ")]
        public EnemyData enemyData = null;
    }

    [System.Serializable]
    private class HitboxParam
    {
        [Header("被弾Hitbox")]
        [Tooltip("プレイヤータックルを受けるHitbox GameObject群です。")]
        public List<GameObject> playerTackleReceiveHitboxes = new List<GameObject>();

        [Tooltip("チェイン被弾を受けるHitbox GameObject群です。空ならプレイヤータックル用Hitboxを流用します。")]
        public List<GameObject> chainReceiveHitboxes = new List<GameObject>();

        [Header("物理接触Collider")]
        [Tooltip("ボスと物理的に接触させたい非IsTriggerのCollider群です。TryStateとDownState中だけCollider.enabledをONにします。")]
        public List<Collider> physicalContactColliders = new List<Collider>();

        [Tooltip("ONならAwake時に登録ColliderのisTriggerがONだった場合に警告ログを出します。物理接触用なので基本OFFのColliderを登録してください。")]
        public bool warnIfPhysicalContactColliderIsTrigger = true;

        [Tooltip("ONなら物理接触ColliderのON/OFFログを出します。")]
        public bool logPhysicalContactColliderState = false;

        [Tooltip("ONなら登録Hitboxの子オブジェクトも一致扱いにします。")]
        public bool allowChildHitboxMatch = true;
    }

    [System.Serializable]
    private class AnimatorParam
    {
        [Header("Animator共通")]
        public Animator animator;
        public bool useAnimator = true;
        public bool syncAnimatorSpeedWithTimeAgent = true;
        public int layerIndex = 0;
    }

    [System.Serializable]
    private class ActionWeightParam
    {
        [Header("Action")]
        [Tooltip("行動種別です。ScrumCommandはHaka後にだけ抽選対象になります。")]
        public BossActionType actionType = BossActionType.Try;

        [Tooltip("抽選重みです。合計100にする必要はなく、比率として扱います。0以下なら抽選対象外です。")]
        [Range(0f, 100f)]
        public float weight = 50f;
    }

    [System.Serializable]
    private class ActionSelectParam
    {
        [Header("通常時 行動抽選")]
        public ActionWeightParam[] normalActions =
        {
            new ActionWeightParam { actionType = BossActionType.Try, weight = 45f },
            new ActionWeightParam { actionType = BossActionType.Haka, weight = 25f },
            new ActionWeightParam { actionType = BossActionType.ScrumCommand, weight = 10f },
            new ActionWeightParam { actionType = BossActionType.RocketPunch, weight = 20f },
        };

        [Header("怒り時 行動抽選")]
        public ActionWeightParam[] angryActions =
        {
            new ActionWeightParam { actionType = BossActionType.Try, weight = 35f },
            new ActionWeightParam { actionType = BossActionType.Haka, weight = 25f },
            new ActionWeightParam { actionType = BossActionType.ScrumCommand, weight = 15f },
            new ActionWeightParam { actionType = BossActionType.RocketPunch, weight = 20f },
        };

        [Header("ScrumCommand Gate")]
        [Tooltip("ONならHakaを一度でも実行した後だけScrumCommandを抽選対象にします。ScrumCommandを一度実行すると再び抽選対象外になります。")]
        public bool requireHakaBeforeScrumCommand = true;

        [Tooltip("Haka状態に入った時点でScrumCommandを解禁します。OFFならHaka終了時点で解禁します。")]
        public bool unlockScrumCommandOnHakaEnter = false;

        [Tooltip("ScrumCommandを実行したら再びロックします。")]
        public bool lockScrumCommandAfterUse = true;

        [Header("Fallback")]
        public BossActionType fallbackAction = BossActionType.Try;

        [Header("Repeat Guard")]
        [Tooltip("ONなら同じ行動が連続で選ばれないようにします。候補が1つしかない場合だけ同じ行動を許可します。")]
        public bool avoidSameActionConsecutive = false;
    }

    [System.Serializable]
    private class IdleStateParam
    {
        [Header("Idle State")]
        public string animationStateName = "RB_Idle";
        public float fadeTime = 0.08f;
        public float waitDuration = 1.0f;

        [Tooltip("怒り状態中にIdleへ入ってから次行動を開始するまでの待機時間です。")]
        public float angryWaitDuration = 0.65f;
    }

    [System.Serializable]
    private class TryMotionParam
    {
        public bool enabled = true;
        public string animationStateName = "RB_Try";
        public Transform targetPoint;
    }

    [System.Serializable]
    private class TryStateParam
    {
        [Header("Try State")]
        public float fadeTime = 0.05f;
        [Range(0.1f, 1.2f)] public float finishNormalizedTime = 0.98f;
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Try候補 全比較")]
        public TryMotionParam[] motions =
        {
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_L_0" },
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_L_1" },
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_L_2" },
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_R_0" },
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_R_1" },
            new TryMotionParam { enabled = true, animationStateName = "RB_Try_R_2" },
        };

        [Header("距離判定")]
        public bool useXZDistance = true;
        public int fallbackIndex = 0;
    }

    [System.Serializable]
    private class TryAttackParam
    {
        [Header("Try Attack Hitboxes")]
        [Tooltip("左トライ攻撃のHitbox GameObject群です。AnimationEventでON/OFFします。")]
        public List<GameObject> leftAttackHitboxes = new List<GameObject>();

        [Tooltip("右トライ攻撃のHitbox GameObject群です。AnimationEventでON/OFFします。")]
        public List<GameObject> rightAttackHitboxes = new List<GameObject>();

        [Tooltip("Try攻撃Hitbox登録の子オブジェクトも一致扱いにします。")]
        public bool allowChildTryHitboxMatch = true;

        [Header("Damage")]
        [Tooltip("トライ攻撃がプレイヤーに当たった時のダメージです。")]
        public int playerDamage = 4;

        [Tooltip("同一Try中に同じReceiverへ何度もダメージを入れないようにします。")]
        public bool hitOncePerTry = true;

        [Header("Safety")]
        [Tooltip("TryStateに入った時点でTry攻撃HitboxをOFFにします。基本ON推奨です。")]
        public bool forceAttackHitboxesOffOnEnter = true;

        [Tooltip("TryState終了時にTry攻撃HitboxをOFFにします。基本ON推奨です。")]
        public bool forceAttackHitboxesOffOnExit = true;

        [Header("Try時のボールオブジェクト")]
        public GameObject RTryBall;
        public GameObject LTryBall;

        [Header("Try Ball Scale Animation")]
        [Tooltip("Tryボールを表示する時、localScaleを0から元スケールへ戻す秒数です。")]
        public float tryBallScaleInDuration = 0.12f;

        [Tooltip("Tryボールを非表示にする時、localScaleを元スケールから0へ縮小する秒数です。")]
        public float tryBallScaleOutDuration = 0.10f;
    }

    [System.Serializable]
    private class HakaStateParam
    {
        [Header("Haka State")]
        public string animationStateName = "RB_Haka";
        public float fadeTime = 0.05f;
        [Range(0.1f, 1.2f)] public float finishNormalizedTime = 0.98f;
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Summon Prefab")]
        public GameObject summonEnemyPrefabObject;
        public RugbySummonEnemy summonEnemyPrefab;

        [Header("Summon Limit")]
        public int maxAliveSummonCount = 8;

        [Header("Spawn Area")]
        public Transform spawnCenter;
        public float spawnRadius = 16f;
        public float minDistanceFromPlayer = 5f;
        public float minDistanceFromOtherSummons = 2.5f;
        public int maxSpawnSearchAttempts = 32;

        [Header("NavMesh")]
        public bool useNavMesh = true;
        public float navMeshSampleDistance = 2f;
        public bool requireNavMeshPoint = true;

        [Header("Spawn Rotation")]
        public bool facePlayerOnSpawn = true;

        [Header("EffectPoint")]
        public Transform LElbowPoint;
        public Transform RElbowPoint;

    }

    [System.Serializable]
    private class ScrumCommandStateParam
    {
        [Header("Scrum Command State")]
        public string animationStateName = "RB_ScrumCommand";
        public float fadeTime = 0.05f;
        [Range(0.1f, 1.2f)] public float finishNormalizedTime = 0.98f;
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Manager Call")]
        public bool invokeManagerOnEnter = false;
        public bool fallbackInvokeBeforeFinish = true;
        [Range(0f, 1.2f)] public float fallbackInvokeNormalizedTime = 0.5f;

        [Header("Gather Point")]
        public Transform gatherPoint;
        public bool useGatherPointTransform = true;
    }

    [System.Serializable]
    private class RocketPunchStateParam
    {
        [Header("Rocket Punch Animation")]
        [Tooltip("射出アニメーションステート名です。")]
        public string launchStateName = "RB_RocketPunch_Launch";

        [Tooltip("ロケットパンチが飛んでいる間のキープ姿勢ループステート名です。")]
        public string keepLoopStateName = "RB_RocketPunch_Keep";

        [Tooltip("ロケットパンチ着弾後、腕を戻すアニメーションステート名です。")]
        public string returnStateName = "RB_RocketPunch_Return";

        public float launchFadeTime = 0.05f;
        public float keepFadeTime = 0.05f;
        public float returnFadeTime = 0.05f;

        [Range(0.1f, 1.2f)] public float launchFinishNormalizedTime = 0.98f;
        [Range(0.1f, 1.2f)] public float returnFinishNormalizedTime = 0.98f;
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Rocket Punch Spawn")]
        [Tooltip("ロケットパンチPrefabです。RugbyRocketPunchObjectが付いたPrefabを設定します。")]
        public RugbyRocketPunchObject rocketPunchPrefab;

        [Tooltip("ロケットパンチの発射位置です。未設定ならBoss位置から発射します。")]
        public Transform firePoint;

        [Tooltip("ONなら発射時にプレイヤー方向へ向けて生成します。OFFならfirePointのforwardを使います。")]
        public bool facePlayerOnLaunch = true;

        [Tooltip("ジャストタックルで跳ね返されたロケットパンチが向かう座標です。未設定ならBoss本体へ返します。")]
        public Transform counterTargetPoint;

        [Header("Launch Timing")]
        [Tooltip("ONならRocketPunchState開始直後に生成します。OFFならAnimationEventかFallbackで生成します。")]
        public bool launchOnEnter = false;

        [Tooltip("AnimationEventを入れ忘れた場合の保険生成を行います。")]
        public bool fallbackLaunchByNormalizedTime = true;

        [Range(0f, 1.2f)]
        [Tooltip("保険生成を行うnormalizedTimeです。")]
        public float fallbackLaunchNormalizedTime = 0.25f;

        [Header("Keep Safety")]
        [Tooltip("ロケットパンチの通知が来ない場合、この秒数を超えたらReturnへ進みます。0以下なら無効です。")]
        public float maxKeepDuration = 8f;
    }

    [System.Serializable]
    private class AngryStateParam
    {
        [Header("Angry Enter State")]
        [Tooltip("怒り状態へ入る時のアニメーションステート名です。")]
        public string animationStateName = "RB_Angry";

        [Tooltip("怒りモーションへ入る時のCrossFade秒数です。")]
        public float fadeTime = 0.05f;

        [Tooltip("怒りモーションがこのnormalizedTime以上になったら終了扱いにします。")]
        [Range(0.1f, 1.2f)]
        public float finishNormalizedTime = 0.98f;

        [Tooltip("Animatorが遷移中の場合は怒り終了判定を待ちます。")]
        public bool waitTransitionCompleteBeforeFinish = true;
    }

    [System.Serializable]
    private class AngryBeamStateParam
    {
        [Header("Angry Beam State")]
        [Tooltip("怒り時ビームアニメーションステート名です。")]
        public string animationStateName = "RB_AngryBeam";

        [Tooltip("ビームへ入る時のCrossFade秒数です。")]
        public float fadeTime = 0.05f;

        [Tooltip("ビームアニメーションがこのnormalizedTime以上になったら終了扱いにします。")]
        [Range(0.1f, 1.2f)]
        public float finishNormalizedTime = 0.98f;

        [Tooltip("Animatorが遷移中の場合はビーム終了判定を待ちます。")]
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Beam Hitboxes")]
        [Tooltip("左ビームの攻撃Hitbox GameObject群です。AnimationEventでON/OFFします。")]
        public List<GameObject> leftBeamHitboxes = new List<GameObject>();

        [Tooltip("右ビームの攻撃Hitbox GameObject群です。AnimationEventでON/OFFします。")]
        public List<GameObject> rightBeamHitboxes = new List<GameObject>();

        [Tooltip("ビームHitbox登録の子オブジェクトも一致扱いにします。")]
        public bool allowChildBeamHitboxMatch = true;

        [Header("Damage")]
        [Tooltip("ビームがプレイヤーに当たった時のダメージです。")]
        public int playerDamage = 5;

        [Tooltip("同一ビーム中に同じReceiverへ何度もダメージを入れないようにします。")]
        public bool hitOncePerBeam = true;

        [Header("Defense Disable")]
        [Tooltip("ONならビーム中はプレイヤータックル被弾Hitboxとチェイン被弾Hitboxを無効化します。")]
        public bool disableReceiveHitboxesDuringBeam = true;

        [Tooltip("BeamStateに入った時点でビーム攻撃HitboxをOFFにします。基本ON推奨です。")]
        public bool forceBeamHitboxesOffOnEnter = true;

        [Tooltip("BeamState終了時にビーム攻撃HitboxをOFFにします。基本ON推奨です。")]
        public bool forceBeamHitboxesOffOnExit = true;

        [Header("ビームポイント")]
        public GameObject beamPointR;
        public GameObject beamPointL;
    }

    [System.Serializable]
    private class DownStateParam
    {
        [Header("Down Enter")]
        public string enterStateName = "RB_Down_In";
        public float enterFadeTime = 0.05f;
        [Range(0.1f, 1.2f)] public float enterFinishNormalizedTime = 0.98f;

        [Header("Down Loop")]
        public string loopStateName = "RB_Down_Loop";
        public float loopFadeTime = 0.05f;
        public float loopDuration = 3.0f;

        [Header("Down WakeUp")]
        public string wakeUpStateName = "RB_Down_Up";
        public float wakeUpFadeTime = 0.05f;
        [Range(0.1f, 1.2f)] public float wakeUpFinishNormalizedTime = 0.98f;

        [Header("Finish Check")]
        public bool waitTransitionCompleteBeforeFinish = true;
    }

    [System.Serializable]
    private class DeadStateParam
    {
        [Header("Dead Fall")]
        [Tooltip("死亡で倒れるモーションのステート名です。")]
        public string fallStateName = "RB_Dead_Fall";

        [Tooltip("死亡で倒れるモーションへのCrossFade秒数です。")]
        public float fallFadeTime = 0.1f;

        [Tooltip("死亡で倒れるモーションがこのnormalizedTime以上になったら爆散へ進みます。")]
        [Range(0.1f, 1.2f)]
        public float fallFinishNormalizedTime = 0.98f;

        [Header("Dead Explosion")]
        [Tooltip("倒れた後の爆散モーションのステート名です。")]
        public string explosionStateName = "RB_Dead_Explosion";

        [Tooltip("爆散モーションへのCrossFade秒数です。")]
        public float explosionFadeTime = 0.05f;

        [Tooltip("爆散モーションがこのnormalizedTime以上になったら死亡演出完了扱いにします。")]
        [Range(0.1f, 1.2f)]
        public float explosionFinishNormalizedTime = 0.98f;

        [Header("Finish Check")]
        public bool waitTransitionCompleteBeforeFinish = true;

        [Header("Finish")]
        [Tooltip("ONなら爆散モーション終了後にBoss GameObjectを非アクティブ化します。")]
        public bool deactivateObjectOnFinished = false;

        [Header("Effect")]
        [Tooltip("死亡時の小爆発エフェクトを発生させる候補地点です。")]
        public List<GameObject> effectPoints = new List<GameObject>();

        [Tooltip("1回のAnimationEventで発生させる小爆発の最小数です。")]
        public int miniExplosionMinCount = 1;

        [Tooltip("1回のAnimationEventで発生させる小爆発の最大数です。")]
        public int miniExplosionMaxCount = 3;

        [Tooltip("OFFなら同じAnimationEvent内で同じEffectPointを重複選択しません。候補数より生成数が多い場合は候補数で打ち止めます。")]
        public bool allowDuplicateMiniExplosionPoint = false;
    }
    [System.Serializable]
    private class DebugParam
    {
        [Header("Log")]
        public bool logInitialize = true;
        public bool logState = true;
        public bool logActionSelect = true;
        public bool logTrySelect = true;
        public bool logTryPointDistance = true;
        public bool logDamage = true;
        public bool logDownGauge = true;
        public bool logSummon = true;
        public bool logSummonSearchDetail = true;
        public bool logAnimationEvent = true;
        public bool logScrumCommand = true;
        public bool logRocketPunch = true;

        [Header("GUI / Gizmo")]
        public bool drawDebugGUI = true;
        public bool drawTryPointGizmos = true;
        public bool drawSummonAreaGizmos = true;
        public Vector3 guiOffset = new Vector3(0f, 3f, 0f);
    }

    #endregion

    #region Inspector Fields

    [Header("開始制御")]
    [SerializeField] private StartParam startParam = new StartParam();

    [Header("ステータス")]
    [SerializeField] private StatusParam statusParam = new StatusParam();

    [Header("被弾Hitbox")]
    [SerializeField] private HitboxParam hitboxParam = new HitboxParam();

    [Header("Animator")]
    [SerializeField] private AnimatorParam animatorParam = new AnimatorParam();

    [Header("行動抽選")]
    [SerializeField] private ActionSelectParam actionSelectParam = new ActionSelectParam();

    [Header("Idle")]
    [SerializeField] private IdleStateParam idleParam = new IdleStateParam();

    [Header("Try")]
    [SerializeField] private TryStateParam tryParam = new TryStateParam();

    [Header("Try Attack")]
    [SerializeField] private TryAttackParam tryAttackParam = new TryAttackParam();

    [Header("Haka")]
    [SerializeField] private HakaStateParam hakaParam = new HakaStateParam();

    [Header("Scrum Command")]
    [SerializeField] private ScrumCommandStateParam scrumCommandParam = new ScrumCommandStateParam();

    [Header("Rocket Punch")]
    [SerializeField] private RocketPunchStateParam rocketPunchParam = new RocketPunchStateParam();

    [Header("Angry")]
    [SerializeField] private AngryStateParam angryParam = new AngryStateParam();

    [Header("Angry Beam")]
    [SerializeField] private AngryBeamStateParam angryBeamParam = new AngryBeamStateParam();

    [Header("Down")]
    [SerializeField] private DownStateParam downParam = new DownStateParam();

    [Header("Dead")]
    [SerializeField] private DeadStateParam deadParam = new DeadStateParam();

    [Header("ターゲット")]
    [SerializeField] private Transform playerTarget;

    [Header("デバッグ")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    #endregion

    #region Runtime Fields

    private TimeAgent timeAgent;
    private BossStateBase currentState;
    private BossStateBase reservedState;
    private EffectPlayer effectPlayer;
    private MaterialFlashPlayer materialFlashPlayer;
    private AudioManager audioManager;
    private int reservedPriority;

    private float hp;
    private float downGauge;
    private bool isAngry;
    private bool isDead;
    private bool bossStarted;

    private int currentTryIndex = 0;
    private int lastTryMotionIndex = -1;
    private bool hasLastTryMotionIndex;
    private string currentAnimationStateName = "";
    private BossActionType currentSelectedAction = BossActionType.Try;
    private BossActionType lastSelectedAction = BossActionType.Try;
    private bool hasLastSelectedAction;

    private float currentBestTryDistance = float.MaxValue;
    private Vector3 currentSelectedTryPointPosition;
    private TryMotionParam currentTryMotion;

    private readonly List<RugbySummonEnemy> activeSummonEnemies = new List<RugbySummonEnemy>();
    private Vector3 lastSummonPosition;
    private bool hasLastSummonPosition;

    private bool scrumCommandUnlockedByHaka;
    private bool currentScrumCommandInvoked;

    private RugbyRocketPunchObject currentRocketPunch;
    private bool currentRocketPunchLaunched;
    private bool currentRocketPunchResolved;
    private bool currentRocketPunchReflectedBossHit;
    private string currentRocketPunchResolveReason;
    private bool angryEntryMotionPlayed;
    private bool angryBeamUsedOnce;
    private bool beamActive;
    private bool tryAttackActive;
    private bool playerTackleReceiveHitboxesActive;
    private bool physicalContactCollidersActive;
    private readonly List<GameObject> beamTemporarilyDisabledHitboxes = new List<GameObject>();
    private readonly List<IHitReceiver> beamHitReceiversThisBeam = new List<IHitReceiver>();
    private readonly List<IHitReceiver> tryHitReceiversThisTry = new List<IHitReceiver>();
    private Coroutine leftTryBallScaleCoroutine;
    private Coroutine rightTryBallScaleCoroutine;
    private Vector3 leftTryBallOriginalScale = Vector3.one;
    private Vector3 rightTryBallOriginalScale = Vector3.one;

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    private float DeltaTime => Time.deltaTime * TimeScale;

    private EffectInstance tryEffect = null;

    #endregion

    #region Public Properties

    public float CurrentHP => hp;
    public float MaxHP => statusParam.maxHP;
    public float CurrentDownGauge => downGauge;
    public float MaxDownGauge => GetCurrentDownGaugeMax();
    public float CurrentStunGauge => downGauge;
    public float MaxStunGauge => GetCurrentDownGaugeMax();
    public bool IsDead => isDead;
    public bool IsBossDead => isDead;
    public bool IsAngry => isAngry;
    public bool IsBossStarted => bossStarted;
    public bool IsDown => currentState is DownEnterState || currentState is DownLoopState || currentState is DownWakeUpState;
    public bool ShouldShowBossUI => bossStarted && !isDead;
    public string CurrentStateName => currentState != null ? currentState.StateName : "None";
    public string CurrentAnimationStateName => currentAnimationStateName;
    public string CurrentSelectedActionName => currentSelectedAction.ToString();
    public int CurrentTryIndex => currentTryIndex;
    public int ActiveSummonCount => activeSummonEnemies.Count;
    public bool IsScrumCommandUnlocked => scrumCommandUnlockedByHaka;

    public float HPRate
    {
        get
        {
            if (statusParam.maxHP <= 0f)
                return 0f;
            return Mathf.Clamp01(hp / statusParam.maxHP);
        }
    }

    public float DownRate
    {
        get
        {
            float max = GetCurrentDownGaugeMax();
            if (max <= 0f)
                return 0f;
            return Mathf.Clamp01(downGauge / max);
        }
    }

    public float DownDisplayRate => DownRate;
    public float StunDisplayRate => DownRate;

    #endregion

    #region Unity Events

    private void Awake()
    {
        timeAgent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();
        materialFlashPlayer = GetComponent<MaterialFlashPlayer>();

        if (animatorParam.animator == null)
            animatorParam.animator = GetComponentInChildren<Animator>();

        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        hp = Mathf.Max(1f, statusParam.maxHP);
        downGauge = 0f;
        isAngry = false;
        isDead = false;
        bossStarted = false;
        currentTryIndex = Mathf.Max(0, tryParam.fallbackIndex);
        lastTryMotionIndex = -1;
        hasLastTryMotionIndex = false;
        currentBestTryDistance = float.MaxValue;
        currentSelectedTryPointPosition = transform.position;
        currentTryMotion = null;
        currentSelectedAction = BossActionType.Try;
        lastSelectedAction = BossActionType.Try;
        hasLastSelectedAction = false;
        currentState = null;
        reservedState = null;
        reservedPriority = int.MinValue;
        activeSummonEnemies.Clear();
        hasLastSummonPosition = false;
        lastSummonPosition = transform.position;
        scrumCommandUnlockedByHaka = false;
        currentScrumCommandInvoked = false;
        currentRocketPunch = null;
        currentRocketPunchLaunched = false;
        currentRocketPunchResolved = false;
        currentRocketPunchReflectedBossHit = false;
        currentRocketPunchResolveReason = "";
        angryEntryMotionPlayed = false;
        angryBeamUsedOnce = false;
        beamActive = false;
        tryAttackActive = false;
        playerTackleReceiveHitboxesActive = false;
        physicalContactCollidersActive = false;
        ValidatePhysicalContactColliders();
        beamTemporarilyDisabledHitboxes.Clear();
        beamHitReceiversThisBeam.Clear();
        tryHitReceiversThisTry.Clear();
        InitializeTryBallScaleObjects();
        RefreshReceiveHitboxStates();

        ReserveState(new IdleState(this, true), 0);
        ApplyReservedState();

        if (debugParam.logInitialize)
        {
            Debug.Log(
                $"{name} RugbyBoss RocketPunch対応 初期化完了 " +
                $"HP:{hp:F1}/{statusParam.maxHP:F1} State:{CurrentStateName}"
            );
        }
    }

    private void Start()
    {
        bossStarted = !startParam.waitForExternalStart;
        if (startParam.autoStartForDebug)
            StartBoss();

        audioManager = AudioManager.Instance;
    }

    private void Update()
    {
        SyncAnimatorSpeed();

        float dt = DeltaTime;
        if (dt <= 0f)
            return;

        if (isDead && !(currentState is DeadState))
            return;

        reservedPriority = int.MinValue;
        CleanupSummonEnemyList();
        TryEnterAngry();
        currentState?.Update(dt);
        ApplyReservedState();
    }

    private void OnDrawGizmosSelected()
    {
        if (debugParam == null)
            return;

        if (debugParam.drawTryPointGizmos)
        {
            DrawTryMotionGizmos(tryParam.motions, Color.cyan);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(currentSelectedTryPointPosition, 0.35f);
        }

        if (debugParam.drawSummonAreaGizmos)
            DrawSummonGizmos();
    }

    #endregion

    #region State Pattern

    private abstract class BossStateBase
    {
        protected readonly RugbyBoss boss;
        protected float timer;

        protected BossStateBase(RugbyBoss boss)
        {
            this.boss = boss;
        }

        public virtual string StateName => GetType().Name;
        public virtual void Enter() { timer = 0f; }
        public virtual void Update(float dt) { timer += dt; }
        public virtual void Exit() { }
    }

    private sealed class IdleState : BossStateBase
    {
        private readonly bool immediate;

        public IdleState(RugbyBoss boss, bool immediate) : base(boss)
        {
            this.immediate = immediate;
        }

        public override string StateName => "Idle";

        public override void Enter()
        {
            base.Enter();
            boss.CrossFadeAnimation(boss.idleParam.animationStateName, immediate ? 0f : boss.idleParam.fadeTime);

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => Idle");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (boss.CanEnterDown())
            {
                boss.ReserveState(new DownEnterState(boss), 1000);
                return;
            }

            if (boss.CanEnterAngryState())
            {
                boss.ReserveState(new AngryState(boss), 900);
                return;
            }

            if (!boss.bossStarted)
                return;

            float waitDuration = boss.isAngry
                ? Mathf.Max(0f, boss.idleParam.angryWaitDuration)
                : Mathf.Max(0f, boss.idleParam.waitDuration);

            if (timer < waitDuration)
                return;

            boss.ReserveState(boss.CreateNextActionState(), 100);
        }
    }

    private sealed class TryState : BossStateBase
    {
        public TryState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "Try";

        public override void Enter()
        {
            base.Enter();
            boss.tryHitReceiversThisTry.Clear();
            boss.tryAttackActive = false;

            if (boss.tryAttackParam.forceAttackHitboxesOffOnEnter)
                boss.SetAllTryAttackHitboxesActive(false);

            boss.RefreshReceiveHitboxStates();

            boss.currentTryMotion = boss.SelectNearestTryMotionFromAll();
            string tryStateName = boss.currentTryMotion != null ? boss.currentTryMotion.animationStateName : boss.idleParam.animationStateName;
            boss.CrossFadeAnimation(tryStateName, boss.tryParam.fadeTime);

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => Try anim:{tryStateName}");
        }

        public override void Exit()
        {
            if (boss.tryAttackParam.forceAttackHitboxesOffOnExit)
                boss.SetAllTryAttackHitboxesActive(false);

            boss.ForceHideAllTryBallsImmediate();
            boss.tryAttackActive = false;
            boss.tryHitReceiversThisTry.Clear();
            base.Exit();
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (!boss.IsCurrentAnimationFinished(boss.currentAnimationStateName, boss.tryParam.finishNormalizedTime, boss.tryParam.waitTransitionCompleteBeforeFinish))
                return;

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class HakaState : BossStateBase
    {
        public HakaState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "Haka";

        public override void Enter()
        {
            base.Enter();
            boss.CrossFadeAnimation(boss.hakaParam.animationStateName, boss.hakaParam.fadeTime);

            if (boss.actionSelectParam.unlockScrumCommandOnHakaEnter)
                boss.UnlockScrumCommandByHaka();

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => Haka anim:{boss.hakaParam.animationStateName}");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (!boss.IsCurrentAnimationFinished(boss.hakaParam.animationStateName, boss.hakaParam.finishNormalizedTime, boss.hakaParam.waitTransitionCompleteBeforeFinish))
                return;

            if (!boss.actionSelectParam.unlockScrumCommandOnHakaEnter)
                boss.UnlockScrumCommandByHaka();

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class ScrumCommandState : BossStateBase
    {
        public ScrumCommandState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "ScrumCommand";

        public override void Enter()
        {
            base.Enter();
            boss.currentScrumCommandInvoked = false;
            boss.CrossFadeAnimation(boss.scrumCommandParam.animationStateName, boss.scrumCommandParam.fadeTime);

            if (boss.actionSelectParam.lockScrumCommandAfterUse)
                boss.scrumCommandUnlockedByHaka = false;

            if (boss.scrumCommandParam.invokeManagerOnEnter)
                boss.TryStartScrumCommandFromState("Enter");

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => ScrumCommand anim:{boss.scrumCommandParam.animationStateName}");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (boss.scrumCommandParam.fallbackInvokeBeforeFinish && !boss.currentScrumCommandInvoked)
            {
                if (boss.IsCurrentAnimationReachedNormalizedTime(
                        boss.scrumCommandParam.animationStateName,
                        boss.scrumCommandParam.fallbackInvokeNormalizedTime,
                        boss.scrumCommandParam.waitTransitionCompleteBeforeFinish))
                {
                    boss.TryStartScrumCommandFromState("FallbackNormalizedTime");
                }
            }

            if (!boss.IsCurrentAnimationFinished(
                    boss.scrumCommandParam.animationStateName,
                    boss.scrumCommandParam.finishNormalizedTime,
                    boss.scrumCommandParam.waitTransitionCompleteBeforeFinish))
                return;

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class RocketPunchState : BossStateBase
    {
        private RocketPunchPhase phase;
        private float keepTimer;

        public RocketPunchState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "RocketPunch";

        public override void Enter()
        {
            base.Enter();
            phase = RocketPunchPhase.Launch;
            keepTimer = 0f;
            boss.currentRocketPunch = null;
            boss.currentRocketPunchLaunched = false;
            boss.currentRocketPunchResolved = false;
            boss.currentRocketPunchReflectedBossHit = false;
            boss.currentRocketPunchResolveReason = "";
            boss.CrossFadeAnimation(boss.rocketPunchParam.launchStateName, boss.rocketPunchParam.launchFadeTime);

            if (boss.rocketPunchParam.launchOnEnter)
                boss.TryLaunchRocketPunchFromState("Enter");

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => RocketPunch Launch anim:{boss.rocketPunchParam.launchStateName}");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            switch (phase)
            {
                case RocketPunchPhase.Launch:
                    UpdateLaunchPhase();
                    break;
                case RocketPunchPhase.Keep:
                    UpdateKeepPhase(dt);
                    break;
                case RocketPunchPhase.Return:
                    UpdateReturnPhase();
                    break;
            }
        }

        private void UpdateLaunchPhase()
        {
            if (boss.rocketPunchParam.fallbackLaunchByNormalizedTime && !boss.currentRocketPunchLaunched)
            {
                if (boss.IsCurrentAnimationReachedNormalizedTime(
                        boss.rocketPunchParam.launchStateName,
                        boss.rocketPunchParam.fallbackLaunchNormalizedTime,
                        boss.rocketPunchParam.waitTransitionCompleteBeforeFinish))
                {
                    boss.TryLaunchRocketPunchFromState("FallbackNormalizedTime");
                }
            }

            if (!boss.IsCurrentAnimationFinished(
                    boss.rocketPunchParam.launchStateName,
                    boss.rocketPunchParam.launchFinishNormalizedTime,
                    boss.rocketPunchParam.waitTransitionCompleteBeforeFinish))
                return;

            phase = RocketPunchPhase.Keep;
            keepTimer = 0f;
            boss.CrossFadeAnimation(boss.rocketPunchParam.keepLoopStateName, boss.rocketPunchParam.keepFadeTime);

            if (boss.debugParam.logRocketPunch)
                Debug.Log($"{boss.name} RocketPunch phase => Keep");
        }

        private void UpdateKeepPhase(float dt)
        {
            keepTimer += dt;

            bool safetyTimeout = boss.rocketPunchParam.maxKeepDuration > 0f && keepTimer >= boss.rocketPunchParam.maxKeepDuration;
            bool projectileMissing = boss.currentRocketPunchLaunched && boss.currentRocketPunch == null && !boss.currentRocketPunchResolved;

            if (!boss.currentRocketPunchResolved && !safetyTimeout && !projectileMissing)
                return;

            phase = RocketPunchPhase.Return;
            boss.CrossFadeAnimation(boss.rocketPunchParam.returnStateName, boss.rocketPunchParam.returnFadeTime);

            if (boss.debugParam.logRocketPunch)
            {
                Debug.Log(
                    $"{boss.name} RocketPunch phase => Return " +
                    $"resolved:{boss.currentRocketPunchResolved} reason:{boss.currentRocketPunchResolveReason} timeout:{safetyTimeout} missing:{projectileMissing}"
                );
            }
        }

        private void UpdateReturnPhase()
        {
            if (!boss.IsCurrentAnimationFinished(
                    boss.rocketPunchParam.returnStateName,
                    boss.rocketPunchParam.returnFinishNormalizedTime,
                    boss.rocketPunchParam.waitTransitionCompleteBeforeFinish))
                return;

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class AngryState : BossStateBase
    {
        public AngryState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "Angry";

        public override void Enter()
        {
            base.Enter();
            boss.EnterAngryFlagFromState();
            boss.CrossFadeAnimation(boss.angryParam.animationStateName, boss.angryParam.fadeTime);

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => Angry anim:{boss.angryParam.animationStateName}");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (!boss.IsCurrentAnimationFinished(
                    boss.angryParam.animationStateName,
                    boss.angryParam.finishNormalizedTime,
                    boss.angryParam.waitTransitionCompleteBeforeFinish))
                return;

            boss.angryEntryMotionPlayed = true;

            if (!boss.angryBeamUsedOnce)
            {
                boss.ReserveState(new AngryBeamState(boss), 100);
                return;
            }

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class AngryBeamState : BossStateBase
    {
        public AngryBeamState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "AngryBeam";

        public override void Enter()
        {
            base.Enter();
            boss.angryBeamUsedOnce = true;
            boss.beamHitReceiversThisBeam.Clear();

            if (boss.angryBeamParam.forceBeamHitboxesOffOnEnter)
                boss.SetAllBeamHitboxesActive(false);

            if (boss.angryBeamParam.disableReceiveHitboxesDuringBeam)
                boss.SetReceiveHitboxesForBeam(false);

            boss.CrossFadeAnimation(boss.angryBeamParam.animationStateName, boss.angryBeamParam.fadeTime);

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => AngryBeam anim:{boss.angryBeamParam.animationStateName}");
        }

        public override void Exit()
        {
            if (boss.angryBeamParam.forceBeamHitboxesOffOnExit)
                boss.SetAllBeamHitboxesActive(false);

            boss.SetReceiveHitboxesForBeam(true);
            boss.beamActive = false;
            boss.beamHitReceiversThisBeam.Clear();
            base.Exit();
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (!boss.IsCurrentAnimationFinished(
                    boss.angryBeamParam.animationStateName,
                    boss.angryBeamParam.finishNormalizedTime,
                    boss.angryBeamParam.waitTransitionCompleteBeforeFinish))
                return;

            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class DownEnterState : BossStateBase
    {
        public DownEnterState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "DownEnter";

        public override void Enter()
        {
            base.Enter();
            boss.downGauge = boss.GetCurrentDownGaugeMax();
            boss.RefreshReceiveHitboxStates();
            boss.CrossFadeAnimation(boss.downParam.enterStateName, boss.downParam.enterFadeTime);
            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => DownEnter");
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            if (!boss.IsCurrentAnimationFinished(boss.downParam.enterStateName, boss.downParam.enterFinishNormalizedTime, boss.downParam.waitTransitionCompleteBeforeFinish))
                return;
            boss.ReserveState(new DownLoopState(boss), 100);
        }
    }

    private sealed class DownLoopState : BossStateBase
    {
        public DownLoopState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "DownLoop";

        private EffectInstance downEffect;

        public override void Enter()
        {
            base.Enter();
            boss.CrossFadeAnimation(boss.downParam.loopStateName, boss.downParam.loopFadeTime);
            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => DownLoop");

            downEffect=boss.effectPlayer.Play(13);
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            float duration = Mathf.Max(0.0001f, boss.downParam.loopDuration);
            float rate = Mathf.Clamp01(1f - timer / duration);
            boss.downGauge = boss.GetCurrentDownGaugeMax() * rate;

            if (timer < Mathf.Max(0f, boss.downParam.loopDuration))
                return;

            boss.downGauge = 0f;
            boss.ReserveState(new DownWakeUpState(boss), 100);
        }

        public override void Exit()
        {
            downEffect?.StopImmediate();
            downEffect = null;
        }
    }

    private sealed class DownWakeUpState : BossStateBase
    {
        public DownWakeUpState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "DownWakeUp";

        public override void Enter()
        {
            base.Enter();
            boss.CrossFadeAnimation(boss.downParam.wakeUpStateName, boss.downParam.wakeUpFadeTime);
            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => DownWakeUp");
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            if (!boss.IsCurrentAnimationFinished(boss.downParam.wakeUpStateName, boss.downParam.wakeUpFinishNormalizedTime, boss.downParam.waitTransitionCompleteBeforeFinish))
                return;
            boss.RecoverFromDown();
            boss.ReserveState(new IdleState(boss, false), 0);
        }
    }

    private sealed class DeadState : BossStateBase
    {
        private bool explosionStarted;
        private bool finished;

        public DeadState(RugbyBoss boss) : base(boss) { }
        public override string StateName => "Dead";

        public override void Enter()
        {
            base.Enter();
            explosionStarted = false;
            finished = false;
            boss.isDead = true;
            boss.DestroyFieldObjectsForAngryOrDead("DeadEnter");
            boss.hp = 0f;
            boss.downGauge = 0f;
            boss.SetAllTryAttackHitboxesActive(false);
            boss.SetAllBeamHitboxesActive(false);
            boss.RefreshReceiveHitboxStates();
            boss.SetReceiveHitboxesForBeam(true);

            if (!string.IsNullOrEmpty(boss.deadParam.fallStateName))
                boss.CrossFadeAnimation(boss.deadParam.fallStateName, boss.deadParam.fallFadeTime);

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} State => DeadFall anim:{boss.deadParam.fallStateName}");


            TimeUIManager.Instance.SetCountDownStart(false);

            InputSystem.actions.FindActionMap("Player").Disable();

            boss.startParam.cameraManager.BeginManualCut("Boss");
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (finished)
                return;

            if (!explosionStarted)
            {
                if (!boss.IsCurrentAnimationFinished(
                        boss.deadParam.fallStateName,
                        boss.deadParam.fallFinishNormalizedTime,
                        boss.deadParam.waitTransitionCompleteBeforeFinish))
                    return;

                explosionStarted = true;
                boss.CrossFadeAnimation(boss.deadParam.explosionStateName, boss.deadParam.explosionFadeTime);

                if (boss.debugParam.logState)
                    Debug.Log($"{boss.name} State => DeadExplosion anim:{boss.deadParam.explosionStateName}");

                return;
            }

            if (!boss.IsCurrentAnimationFinished(
                    boss.deadParam.explosionStateName,
                    boss.deadParam.explosionFinishNormalizedTime,
                    boss.deadParam.waitTransitionCompleteBeforeFinish))
                return;

            finished = true;

            if (boss.debugParam.logState)
                Debug.Log($"{boss.name} Dead finished");

            if (boss.deadParam.deactivateObjectOnFinished)
                boss.gameObject.SetActive(false);
        }
    }

    private void ReserveState(BossStateBase nextState, int priority)
    {
        if (nextState == null)
            return;
        if (priority < reservedPriority)
            return;
        reservedState = nextState;
        reservedPriority = priority;
    }

    private void ApplyReservedState()
    {
        if (reservedState == null)
            return;

        BossStateBase nextState = reservedState;
        reservedState = null;
        reservedPriority = int.MinValue;

        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();
        RefreshReceiveHitboxStates();
    }

    #endregion

    #region Action Select

    private BossStateBase CreateNextActionState()
    {
        BossActionType action = SelectNextActionType();
        currentSelectedAction = action;
        lastSelectedAction = action;
        hasLastSelectedAction = true;

        if (debugParam.logActionSelect)
        {
            Debug.Log(
                $"{name} Action Select => {action} " +
                $"Angry:{isAngry} Table:{(isAngry ? "Angry" : "Normal")} " +
                $"ScrumUnlocked:{scrumCommandUnlockedByHaka}"
            );
        }

        switch (action)
        {
            case BossActionType.Haka:
                return new HakaState(this);
            case BossActionType.ScrumCommand:
                return new ScrumCommandState(this);
            case BossActionType.RocketPunch:
                return new RocketPunchState(this);
            case BossActionType.AngryBeam:
                return new AngryBeamState(this);
            case BossActionType.Try:
            default:
                return new TryState(this);
        }
    }

    private BossActionType SelectNextActionType()
    {
        ActionWeightParam[] table = isAngry ? actionSelectParam.angryActions : actionSelectParam.normalActions;
        return SelectWeightedAction(table, actionSelectParam.fallbackAction);
    }

    private BossActionType SelectWeightedAction(ActionWeightParam[] table, BossActionType fallback)
    {
        BossActionType selected;

        if (TrySelectWeightedActionInternal(table, fallback, true, out selected))
            return selected;

        if (TrySelectWeightedActionInternal(table, fallback, false, out selected))
            return selected;

        return BossActionType.Try;
    }

    private bool TrySelectWeightedActionInternal(ActionWeightParam[] table, BossActionType fallback, bool avoidRepeat, out BossActionType selected)
    {
        selected = fallback;

        if (table == null || table.Length == 0)
        {
            if (!avoidRepeat || !IsBlockedByRepeat(fallback))
                return CanSelectAction(fallback);

            return false;
        }

        float totalWeight = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            ActionWeightParam pattern = table[i];
            if (pattern == null)
                continue;
            if (!CanSelectAction(pattern.actionType))
                continue;
            if (avoidRepeat && IsBlockedByRepeat(pattern.actionType))
                continue;

            totalWeight += Mathf.Max(0f, pattern.weight);
        }

        if (totalWeight <= 0f)
        {
            if (CanSelectAction(fallback) && (!avoidRepeat || !IsBlockedByRepeat(fallback)))
            {
                selected = fallback;
                return true;
            }

            return false;
        }

        float random = Random.Range(0f, totalWeight);
        float current = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            ActionWeightParam pattern = table[i];
            if (pattern == null)
                continue;
            if (!CanSelectAction(pattern.actionType))
                continue;
            if (avoidRepeat && IsBlockedByRepeat(pattern.actionType))
                continue;

            float weight = Mathf.Max(0f, pattern.weight);
            if (weight <= 0f)
                continue;

            current += weight;
            if (random <= current)
            {
                selected = pattern.actionType;
                return true;
            }
        }

        return false;
    }

    private bool IsBlockedByRepeat(BossActionType actionType)
    {
        // 行動抽選では同一行動の連続を許可します。
        // TryモーションだけはSelectNearestTryMotionFromAll側で連続回避します。
        return false;
    }

    private bool CanSelectAction(BossActionType actionType)
    {
        if (actionType == BossActionType.AngryBeam)
        {
            return isAngry;
        }

        if (actionType != BossActionType.ScrumCommand)
            return true;

        if (!actionSelectParam.requireHakaBeforeScrumCommand)
            return true;

        return scrumCommandUnlockedByHaka;
    }

    private void UnlockScrumCommandByHaka()
    {
        if (!actionSelectParam.requireHakaBeforeScrumCommand)
            return;
        if (scrumCommandUnlockedByHaka)
            return;

        scrumCommandUnlockedByHaka = true;
        if (debugParam.logActionSelect)
            Debug.Log($"{name} ScrumCommand unlocked by Haka");
    }

    #endregion

    #region Rocket Punch

    public void AnimEvent_RocketPunchLaunch()
    {
        if (debugParam.logAnimationEvent)
        {
            Debug.Log(
                $"{name} AnimEvent_RocketPunchLaunch called " +
                $"state:{CurrentStateName} anim:{currentAnimationStateName} frame:{Time.frameCount}"
            );
        }

        TryLaunchRocketPunchFromState("AnimationEvent");
    }

    public void RequestRocketPunchForDebug()
    {
        if (isDead)
            return;

        ReserveState(new RocketPunchState(this), 1000);
        ApplyReservedState();
    }

    private bool TryLaunchRocketPunchFromState(string reason)
    {
        if (!(currentState is RocketPunchState))
        {
            if (debugParam.logRocketPunch)
            {
                Debug.LogWarning(
                    $"{name} RocketPunch launch skipped: current state is not RocketPunch. " +
                    $"reason:{reason} state:{CurrentStateName} anim:{currentAnimationStateName}"
                );
            }
            return false;
        }

        if (currentRocketPunchLaunched)
        {
            if (debugParam.logRocketPunch)
                Debug.Log($"{name} RocketPunch launch skipped: already launched reason:{reason}");
            return false;
        }

        currentRocketPunchLaunched = true;
        return LaunchRocketPunch(reason);
    }

    private bool LaunchRocketPunch(string reason)
    {
        if (rocketPunchParam.rocketPunchPrefab == null)
        {
            Debug.LogWarning($"{name} RocketPunch launch failed: rocketPunchPrefab is null.");
            currentRocketPunchResolved = true;
            currentRocketPunchResolveReason = "PrefabNull";
            return false;
        }

        Transform firePoint = rocketPunchParam.firePoint != null ? rocketPunchParam.firePoint : transform;
        Vector3 forward = ResolveRocketPunchLaunchDirection(firePoint);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        RugbyRocketPunchObject punch = Instantiate(
            rocketPunchParam.rocketPunchPrefab,
            firePoint.position,
            rotation
        );

        // Prefab Rootが非アクティブ、またはComponentが無効のまま生成された場合の保険です。
        punch.gameObject.SetActive(true);
        punch.enabled = true;

        currentRocketPunch = punch;
        currentRocketPunchResolved = false;
        currentRocketPunchReflectedBossHit = false;
        currentRocketPunchResolveReason = "";

        Transform counterTarget = rocketPunchParam.counterTargetPoint != null
            ? rocketPunchParam.counterTargetPoint
            : transform;

        punch.Initialize(this, playerTarget, counterTarget, forward);

        if (debugParam.logRocketPunch)
        {
            Debug.Log(
                $"{name} RocketPunch launched reason:{reason} " +
                $"prefab:{rocketPunchParam.rocketPunchPrefab.name} pos:{firePoint.position} dir:{forward}"
            );
        }

        return true;
    }


    private Vector3 ResolveRocketPunchLaunchDirection(Transform firePoint)
    {
        if (rocketPunchParam.facePlayerOnLaunch && playerTarget != null)
        {
            Vector3 direction = playerTarget.position - firePoint.position;
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;
        }

        Vector3 forward = firePoint.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    public void NotifyRocketPunchImpact(RugbyRocketPunchObject punch, bool reflectedBossHit, string reason)
    {
        if (punch != null && currentRocketPunch != null && punch != currentRocketPunch)
            return;

        currentRocketPunchResolved = true;
        currentRocketPunchReflectedBossHit = reflectedBossHit;
        currentRocketPunchResolveReason = reason;

        if (currentRocketPunch == punch)
            currentRocketPunch = null;

        if (debugParam.logRocketPunch)
        {
            Debug.Log(
                $"{name} RocketPunch impact notified " +
                $"reflectedBossHit:{reflectedBossHit} reason:{reason}"
            );
        }
    }

    public void ReceiveRocketPunchCounterHit(float damage, float downValue, RugbyRocketPunchObject source)
    {
        if (isDead)
            return;

        damage = Mathf.Max(0f, damage);
        downValue = Mathf.Max(0f, downValue);

        hp = Mathf.Max(0f, hp - damage);
        downGauge = Mathf.Max(downGauge + downValue, GetCurrentDownGaugeMax());
        currentRocketPunchResolved = true;
        currentRocketPunchReflectedBossHit = true;
        currentRocketPunchResolveReason = "CounterHitBoss";

        if (currentRocketPunch == source)
            currentRocketPunch = null;

        if (debugParam.logDamage)
        {
            Debug.Log(
                $"{name} RocketPunch CounterHit damage:{damage:F1} down:{downValue:F1} " +
                $"HP:{hp:F1}/{statusParam.maxHP:F1} Down:{downGauge:F1}/{GetCurrentDownGaugeMax():F1} " +
                $"forceDown:true source:{(source != null ? source.name : "null")}"
            );
        }

        if (hp <= 0f)
        {
            EnterDead();
            return;
        }

        ForceEnterDownFromAnyState("RocketPunchCounterHit");
    }

    private void ForceEnterDownFromAnyState(string reason)
    {
        if (isDead)
            return;

        reservedState = null;
        reservedPriority = int.MinValue;

        currentState?.Exit();
        currentState = new DownEnterState(this);
        currentState.Enter();

        if (debugParam.logState)
            Debug.Log($"{name} ForceEnterDownFromAnyState reason:{reason}");
    }

    public void AnimEvent_RocketPunchEffect()
    {
        effectPlayer.Play(16);
    }

    #endregion

    #region Try Attack

    public void AnimEvent_TryAttackOn()
    {
        SetAllTryAttackHitboxesActive(true);
    }

    public void AnimEvent_TryAttackOff()
    {
        SetAllTryAttackHitboxesActive(false);
    }

    public void AnimEvent_TryAttackLeftOn()
    {
        SetTryAttackHitboxesActive(tryAttackParam.leftAttackHitboxes, true);
        ShowLeftTryBallAnimated();
        tryAttackActive = true;
        
    }

    public void AnimEvent_TryAttackLeftOff()
    {
        SetTryAttackHitboxesActive(tryAttackParam.leftAttackHitboxes, false);
        UpdateTryAttackActiveFlag();

    }

    public void AnimEvent_TryAttackRightOn()
    {
        SetTryAttackHitboxesActive(tryAttackParam.rightAttackHitboxes, true);
        ShowRightTryBallAnimated();
        tryAttackActive = true;

    }

    public void AnimEvent_TryAttackRightOff()
    {
        SetTryAttackHitboxesActive(tryAttackParam.rightAttackHitboxes, false);
        UpdateTryAttackActiveFlag();

    }
    public void AnimEvent_TryBallLeftOff()
    {
        HideLeftTryBallAnimated();
    }

    public void AnimEvent_TryBallRightOff()
    {
        HideRightTryBallAnimated();
    }

    public void AnimEvent_TryBallOff()
    {
        HideLeftTryBallAnimated();
        HideRightTryBallAnimated();
    }

    public void AnimEvent_TryBallLeftScaleOut()
    {
        HideLeftTryBallAnimated();
    }

    public void AnimEvent_TryBallRightScaleOut()
    {
        HideRightTryBallAnimated();
    }

    public void AnimEvent_TryPointEffectOnOff(int on)
    {
        if (tryEffect != null)
        {
            tryEffect.StopImmediate();
            tryEffect = null;
        }
        if (on == 1)
        {
            tryEffect = effectPlayer.PlayAt(11, tryParam.motions[currentTryIndex].targetPoint.position);
        }
    }

    private void SetAllTryAttackHitboxesActive(bool active)
    {
        SetTryAttackHitboxesActive(tryAttackParam.leftAttackHitboxes, active);
        SetTryAttackHitboxesActive(tryAttackParam.rightAttackHitboxes, active);
        tryAttackActive = active;

        if (!active)
        {
            tryHitReceiversThisTry.Clear();
        }
    }

    private void SetTryAttackHitboxesActive(List<GameObject> hitboxes, bool active)
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

    private void InitializeTryBallScaleObjects()
    {
        InitializeTryBallScaleObject(tryAttackParam.LTryBall, ref leftTryBallOriginalScale);
        InitializeTryBallScaleObject(tryAttackParam.RTryBall, ref rightTryBallOriginalScale);
    }

    private void InitializeTryBallScaleObject(GameObject target, ref Vector3 originalScale)
    {
        if (target == null)
            return;

        originalScale = target.transform.localScale;
        if (originalScale.sqrMagnitude <= 0.000001f)
            originalScale = Vector3.one;

        target.transform.localScale = Vector3.zero;
        target.SetActive(false);
    }

    private void ShowLeftTryBallAnimated()
    {
        PlayTryBallScaleAnimation(tryAttackParam.LTryBall, leftTryBallOriginalScale, ref leftTryBallScaleCoroutine, true);
    }

    private void ShowRightTryBallAnimated()
    {
        PlayTryBallScaleAnimation(tryAttackParam.RTryBall, rightTryBallOriginalScale, ref rightTryBallScaleCoroutine, true);
    }

    private void HideLeftTryBallAnimated()
    {
        PlayTryBallScaleAnimation(tryAttackParam.LTryBall, leftTryBallOriginalScale, ref leftTryBallScaleCoroutine, false);
    }

    private void HideRightTryBallAnimated()
    {
        PlayTryBallScaleAnimation(tryAttackParam.RTryBall, rightTryBallOriginalScale, ref rightTryBallScaleCoroutine, false);
    }

    private void ForceHideAllTryBallsImmediate()
    {
        ForceHideTryBallImmediate(tryAttackParam.LTryBall, ref leftTryBallScaleCoroutine);
        ForceHideTryBallImmediate(tryAttackParam.RTryBall, ref rightTryBallScaleCoroutine);
    }

    private void ForceHideTryBallImmediate(GameObject target, ref Coroutine coroutine)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }

        if (target == null)
            return;

        target.transform.localScale = Vector3.zero;
        target.SetActive(false);
    }

    private void PlayTryBallScaleAnimation(GameObject target, Vector3 originalScale, ref Coroutine coroutine, bool show)
    {
        if (target == null)
            return;

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }

        coroutine = StartCoroutine(CoScaleTryBall(target, originalScale, show));
    }

    private IEnumerator CoScaleTryBall(GameObject target, Vector3 originalScale, bool show)
    {
        if (target == null)
            yield break;

        if (originalScale.sqrMagnitude <= 0.000001f)
            originalScale = Vector3.one;

        float duration = show
            ? Mathf.Max(0f, tryAttackParam.tryBallScaleInDuration)
            : Mathf.Max(0f, tryAttackParam.tryBallScaleOutDuration);

        Vector3 startScale = show ? Vector3.zero : target.transform.localScale;
        Vector3 endScale = show ? originalScale : Vector3.zero;

        if (show)
        {
            target.SetActive(true);
            target.transform.localScale = Vector3.zero;
        }

        if (duration <= 0f)
        {
            target.transform.localScale = endScale;
            if (!show)
                target.SetActive(false);
            ClearTryBallCoroutineReference(target);
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime * TimeScale;
            float rate = Mathf.Clamp01(timer / duration);
            rate = rate * rate * (3f - 2f * rate);
            target.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, rate);
            yield return null;
        }

        target.transform.localScale = endScale;
        if (!show)
            target.SetActive(false);

        ClearTryBallCoroutineReference(target);
    }

    private void ClearTryBallCoroutineReference(GameObject target)
    {
        if (target == tryAttackParam.LTryBall)
            leftTryBallScaleCoroutine = null;
        if (target == tryAttackParam.RTryBall)
            rightTryBallScaleCoroutine = null;
    }

    private void UpdateTryAttackActiveFlag()
    {
        tryAttackActive = AnyTryAttackHitboxActive(tryAttackParam.leftAttackHitboxes) ||
                          AnyTryAttackHitboxActive(tryAttackParam.rightAttackHitboxes);

        if (!tryAttackActive)
            tryHitReceiversThisTry.Clear();
    }

    private bool AnyTryAttackHitboxActive(List<GameObject> hitboxes)
    {
        if (hitboxes == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject hitbox = hitboxes[i];
            if (hitbox != null && hitbox.activeInHierarchy)
                return true;
        }

        return false;
    }

    private bool IsTryAttackHitbox(GameObject targetHitbox)
    {
        if (ContainsTryAttackHitbox(tryAttackParam.leftAttackHitboxes, targetHitbox))
            return true;

        if (ContainsTryAttackHitbox(tryAttackParam.rightAttackHitboxes, targetHitbox))
            return true;

        return false;
    }

    private bool ContainsTryAttackHitbox(List<GameObject> hitboxes, GameObject targetHitbox)
    {
        if (hitboxes == null || targetHitbox == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject registered = hitboxes[i];
            if (registered == null)
                continue;

            if (registered == targetHitbox)
                return true;

            if (!tryAttackParam.allowChildTryHitboxMatch)
                continue;

            if (targetHitbox.transform.IsChildOf(registered.transform))
                return true;

            if (registered.transform.IsChildOf(targetHitbox.transform))
                return true;
        }

        return false;
    }

    private void TrySendTryAttackDamage(Hitbox selfHitbox, Collider other)
    {
        if (!tryAttackActive)
            return;
        if (selfHitbox == null || other == null)
            return;
        if (!IsTryAttackHitbox(selfHitbox.gameObject))
            return;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();
        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        IHitReceiver receiver = targetHitbox.receiver;
        if (tryAttackParam.hitOncePerTry && tryHitReceiversThisTry.Contains(receiver))
            return;

        MonoBehaviour receiverMono = receiver as MonoBehaviour;
        if (receiverMono == null)
            return;

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,
            targetObject = receiverMono.gameObject,
            targetHitbox = targetHitbox.gameObject,
            contactPoint = targetHitbox.transform.position,
            payload = new EnemyAttackPayload
            {
                damage = tryAttackParam.playerDamage
            }
        };

        receiver.OnHit(data);

        if (tryAttackParam.hitOncePerTry)
            tryHitReceiversThisTry.Add(receiver);

        if (debugParam.logState)
            Debug.Log($"{name} TryAttack hit target:{receiverMono.name} damage:{tryAttackParam.playerDamage}");
    }

    public void AnimEvent_TryEffect(int i)
    {
        effectPlayer.PlayAt(0, tryParam.motions[i].targetPoint.position);
    }

    #endregion

    #region Field Object Cleanup

    private void DestroyFieldObjectsForAngryOrDead(string reason)
    {
        DestroyCurrentRocketPunch(reason);
        DestroyActiveScrum(reason);
        DestroyAllSummonEnemies(reason);
    }

    private void DestroyCurrentRocketPunch(string reason)
    {
        if (currentRocketPunch == null)
            return;

        RugbyRocketPunchObject punch = currentRocketPunch;
        currentRocketPunch = null;
        currentRocketPunchResolved = true;
        currentRocketPunchResolveReason = reason;

        punch.ForceDestroyProjectile(reason);

        if (debugParam.logRocketPunch)
            Debug.Log($"{name} RocketPunch destroyed reason:{reason}");
    }

    private void DestroyActiveScrum(string reason)
    {
        if (RugbyScrumManager.Instance == null)
            return;

        RugbyScrumManager.Instance.ForceDestroyActiveScrum();

        if (debugParam.logScrumCommand)
            Debug.Log($"{name} Scrum destroyed reason:{reason}");
    }

    private void DestroyAllSummonEnemies(string reason)
    {
        CleanupSummonEnemyList();

        int destroyedCount = activeSummonEnemies.Count;

        for (int i = 0; i < activeSummonEnemies.Count; i++)
        {
            RugbySummonEnemy enemy = activeSummonEnemies[i];
            if (enemy == null)
                continue;

            enemy.ForceDestroy();
        }

        activeSummonEnemies.Clear();
        hasLastSummonPosition = false;

        if (debugParam.logSummon)
            Debug.Log($"{name} Summons destroyed reason:{reason} count:{destroyedCount}");
    }

    #endregion

    #region Angry / Beam

    private bool CanEnterAngryState()
    {
        if (isDead)
            return false;
        if (isAngry)
            return false;
        if (hp > statusParam.angryHPThreshold)
            return false;
        if (!(currentState is IdleState))
            return false;
        return true;
    }

    private void EnterAngryFlagFromState()
    {
        if (isAngry)
            return;

        isAngry = true;
        DestroyFieldObjectsForAngryOrDead("AngryEnter");

        if (statusParam.resetDownGaugeOnAngryEnter)
            downGauge = Mathf.Max(0f, statusParam.downGaugeOnAngryEnter);

        RefreshReceiveHitboxStates();

        if (debugParam.logDamage)
            Debug.Log($"{name} Angry Enter HP:{hp:F1}/{statusParam.maxHP:F1}");
    }

    public void AnimEvent_AngryEffect()
    {
        effectPlayer.Play(10);
    }

    public void AnimEvent_AngryBeamOn()
    {
        SetAllBeamHitboxesActive(true);
    }

    public void AnimEvent_AngryBeamOff()
    {
        SetAllBeamHitboxesActive(false);
    }

    public void AnimEvent_AngryBeamLeftOn()
    {
        SetBeamHitboxesActive(angryBeamParam.leftBeamHitboxes, true);
        beamActive = true;
    }

    public void AnimEvent_AngryBeamLeftOff()
    {
        SetBeamHitboxesActive(angryBeamParam.leftBeamHitboxes, false);
        UpdateBeamActiveFlag();
    }

    public void AnimEvent_AngryBeamRightOn()
    {
        SetBeamHitboxesActive(angryBeamParam.rightBeamHitboxes, true);
        beamActive = true;
    }

    public void AnimEvent_AngryBeamRightOff()
    {
        SetBeamHitboxesActive(angryBeamParam.rightBeamHitboxes, false);
        UpdateBeamActiveFlag();
    }

    private void SetAllBeamHitboxesActive(bool active)
    {
        SetBeamHitboxesActive(angryBeamParam.leftBeamHitboxes, active);
        SetBeamHitboxesActive(angryBeamParam.rightBeamHitboxes, active);
        beamActive = active;

        if (!active)
            beamHitReceiversThisBeam.Clear();
    }

    private void SetBeamHitboxesActive(List<GameObject> hitboxes, bool active)
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

    private void UpdateBeamActiveFlag()
    {
        beamActive = AnyBeamHitboxActive(angryBeamParam.leftBeamHitboxes) ||
                     AnyBeamHitboxActive(angryBeamParam.rightBeamHitboxes);

        if (!beamActive)
            beamHitReceiversThisBeam.Clear();
    }

    private bool AnyBeamHitboxActive(List<GameObject> hitboxes)
    {
        if (hitboxes == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject hitbox = hitboxes[i];
            if (hitbox != null && hitbox.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void SetReceiveHitboxesForBeam(bool active)
    {
        // 被弾コライダーのON/OFFはここで個別復元せず、状態ベースで一元管理する。
        // PlayerTackleReceiveHitboxes: DownState / TryState のみON。
        // ChainReceiveHitboxes: 通常時のみON、怒り時・死亡時はOFF。
        RefreshReceiveHitboxStates();
    }

    private void DisableReceiveHitboxListForBeam(List<GameObject> hitboxes)
    {
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject hitbox = hitboxes[i];
            if (hitbox == null)
                continue;
            if (!hitbox.activeSelf)
                continue;
            if (beamTemporarilyDisabledHitboxes.Contains(hitbox))
                continue;

            beamTemporarilyDisabledHitboxes.Add(hitbox);
            hitbox.SetActive(false);
        }
    }

    private bool IsBeamHitbox(GameObject targetHitbox)
    {
        if (ContainsBeamHitbox(angryBeamParam.leftBeamHitboxes, targetHitbox))
            return true;

        if (ContainsBeamHitbox(angryBeamParam.rightBeamHitboxes, targetHitbox))
            return true;

        return false;
    }

    private bool ContainsBeamHitbox(List<GameObject> hitboxes, GameObject targetHitbox)
    {
        if (hitboxes == null || targetHitbox == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject registered = hitboxes[i];
            if (registered == null)
                continue;

            if (registered == targetHitbox)
                return true;

            if (!angryBeamParam.allowChildBeamHitboxMatch)
                continue;

            if (targetHitbox.transform.IsChildOf(registered.transform))
                return true;

            if (registered.transform.IsChildOf(targetHitbox.transform))
                return true;
        }

        return false;
    }

    private void TrySendBeamDamage(Hitbox selfHitbox, Collider other)
    {
        if (!beamActive)
            return;
        if (selfHitbox == null || other == null)
            return;
        if (!IsBeamHitbox(selfHitbox.gameObject))
            return;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();
        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        IHitReceiver receiver = targetHitbox.receiver;
        if (angryBeamParam.hitOncePerBeam && beamHitReceiversThisBeam.Contains(receiver))
            return;

        MonoBehaviour receiverMono = receiver as MonoBehaviour;
        if (receiverMono == null)
            return;

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,
            targetObject = receiverMono.gameObject,
            targetHitbox = targetHitbox.gameObject,
            contactPoint = targetHitbox.transform.position,
            payload = new EnemyAttackPayload
            {
                damage = angryBeamParam.playerDamage
            }
        };

        receiver.OnHit(data);

        if (angryBeamParam.hitOncePerBeam)
            beamHitReceiversThisBeam.Add(receiver);

        if (debugParam.logState)
            Debug.Log($"{name} AngryBeam hit target:{receiverMono.name} damage:{angryBeamParam.playerDamage}");
    }

    public void AnimEvent_BeamChargeEffect(int LR)
    {
        var chargeEffect = effectPlayer.Play(1);
        if (LR == 0)
        {
            chargeEffect.Attach(
                           angryBeamParam.beamPointL.transform,
                           angryBeamParam.beamPointL.transform);
        }
        else if (LR == 1)
        {
            chargeEffect.Attach(
                           angryBeamParam.beamPointR.transform,
                           angryBeamParam.beamPointR.transform);
        }

    }

    public void AnimEvent_BeamEffect(int LR)
    {
        var chargeEffect = effectPlayer.Play(2);
        if (LR == 0)
        {
            chargeEffect.Attach(
                           angryBeamParam.beamPointL.transform,
                           angryBeamParam.beamPointL.transform);
        }
        else if (LR == 1)
        {
            chargeEffect.Attach(
                           angryBeamParam.beamPointR.transform,
                           angryBeamParam.beamPointR.transform);
        }

    }

    #endregion

    #region Scrum Command

    public void AnimEvent_ScrumCommand()
    {
        if (debugParam.logAnimationEvent)
        {
            Debug.Log(
                $"{name} AnimEvent_ScrumCommand called " +
                $"state:{CurrentStateName} anim:{currentAnimationStateName} frame:{Time.frameCount}"
            );
        }

        TryStartScrumCommandFromState("AnimationEvent");
    }

    public void AnimEvent_ScrumCommandStart()
    {
        AnimEvent_ScrumCommand();
    }

    public void RequestScrumCommandForDebug()
    {
        if (isDead)
            return;

        ReserveState(new ScrumCommandState(this), 1000);
        ApplyReservedState();
    }

    public void AnimEvent_ScrumCommandEffect_A()
    {
        effectPlayer.Play(14);
    }

    public void AnimEvent_ScrumCommandEffect_B()
    {
        effectPlayer.Play(15);
    }

    public bool TryStartScrumCommandForDebug()
    {
        return TryStartScrumCommand("Debug");
    }

    private bool TryStartScrumCommandFromState(string reason)
    {
        if (!(currentState is ScrumCommandState))
        {
            if (debugParam.logScrumCommand)
            {
                Debug.LogWarning(
                    $"{name} ScrumCommand skipped: Current state is not ScrumCommand. " +
                    $"reason:{reason} state:{CurrentStateName} anim:{currentAnimationStateName}"
                );
            }
            return false;
        }

        if (currentScrumCommandInvoked)
        {
            if (debugParam.logScrumCommand)
                Debug.Log($"{name} ScrumCommand skipped: already invoked reason:{reason}");
            return false;
        }

        currentScrumCommandInvoked = true;
        return TryStartScrumCommand(reason);
    }

    private bool TryStartScrumCommand(string reason)
    {
        CleanupSummonEnemyList();

        if (RugbyScrumManager.Instance == null)
        {
            Debug.LogWarning($"{name} ScrumCommand failed: RugbyScrumManager.Instance が存在しません。シーンにRugbyScrumManagerを1つ配置してください。");
            return false;
        }

        List<RugbySummonEnemy> members = new List<RugbySummonEnemy>();
        for (int i = 0; i < activeSummonEnemies.Count; i++)
        {
            RugbySummonEnemy enemy = activeSummonEnemies[i];
            if (enemy == null || !enemy.IsAlive)
                continue;
            members.Add(enemy);
        }

        bool result;
        if (scrumCommandParam.useGatherPointTransform && scrumCommandParam.gatherPoint != null)
        {
            result = RugbyScrumManager.Instance.BeginScrumCommand(
                this,
                playerTarget,
                members,
                scrumCommandParam.gatherPoint.position
            );
        }
        else
        {
            result = RugbyScrumManager.Instance.BeginScrumCommand(
                this,
                playerTarget,
                members
            );
        }

        if (debugParam.logScrumCommand)
        {
            Debug.Log(
                $"{name} ScrumCommand invoked reason:{reason} members:{members.Count} result:{result} " +
                $"manager:{RugbyScrumManager.Instance.name}"
            );
        }

        return result;
    }

    #endregion

    #region Haka Summon

    public void AnimEvent_HakaSummonEnemy()
    {
        if (debugParam.logAnimationEvent)
        {
            Debug.Log(
                $"{name} AnimEvent_HakaSummonEnemy called " +
                $"state:{CurrentStateName} anim:{currentAnimationStateName} frame:{Time.frameCount}"
            );
        }

        TrySummonHakaEnemyFromAnimationEvent();
    }

    public void AnimEvent_HakaSummon()
    {
        if (debugParam.logAnimationEvent)
        {
            Debug.Log(
                $"{name} AnimEvent_HakaSummon called " +
                $"state:{CurrentStateName} anim:{currentAnimationStateName} frame:{Time.frameCount}"
            );
        }

        TrySummonHakaEnemyFromAnimationEvent();
    }

    public bool TrySummonHakaEnemyFromAnimationEvent()
    {
        if (isDead)
        {
            if (debugParam.logSummon)
                Debug.LogWarning($"{name} Haka召喚スキップ: Bossが死亡済みです。");
            return false;
        }

        if (!(currentState is HakaState))
        {
            if (debugParam.logSummon)
            {
                Debug.LogWarning(
                    $"{name} Haka召喚スキップ: 現在Haka状態ではありません。 " +
                    $"CurrentState:{CurrentStateName} CurrentAnim:{currentAnimationStateName} Frame:{Time.frameCount}"
                );
            }
            return false;
        }

        return TrySummonHakaEnemyOne();
    }

    public bool TrySummonHakaEnemyForDebug()
    {
        if (isDead)
        {
            if (debugParam.logSummon)
                Debug.LogWarning($"{name} Debug Haka召喚失敗: Bossが死亡済みです。");
            return false;
        }

        return TrySummonHakaEnemyOne();
    }

    private bool TrySummonHakaEnemyOne()
    {
        CleanupSummonEnemyList();

        GameObject prefabObject = ResolveSummonPrefabObject();
        if (prefabObject == null)
        {
            Debug.LogWarning(
                $"{name} Haka召喚失敗: 召喚Prefabが未設定、またはPrefab内にRugbySummonEnemyが見つかりません。 " +
                $"summonEnemyPrefabObject:{GetObjectName(hakaParam.summonEnemyPrefabObject)} " +
                $"summonEnemyPrefab:{GetComponentObjectName(hakaParam.summonEnemyPrefab)}"
            );
            return false;
        }

        if (hakaParam.maxAliveSummonCount > 0 && activeSummonEnemies.Count >= hakaParam.maxAliveSummonCount)
        {
            if (debugParam.logSummon)
                Debug.Log($"{name} Haka召喚スキップ: 生存召喚数が上限です。 Active:{activeSummonEnemies.Count} Max:{hakaParam.maxAliveSummonCount}");
            return false;
        }

        Vector3 spawnPosition;
        HakaSummonSearchReport report;
        if (!TryFindHakaSummonPosition(out spawnPosition, out report))
        {
            Debug.LogWarning(
                $"{name} Haka召喚失敗: 条件を満たす召喚位置が見つかりませんでした。 " +
                $"attempts:{report.totalAttempts} navFail:{report.navMeshFailCount} " +
                $"playerDistanceFail:{report.playerDistanceFailCount} otherSummonDistanceFail:{report.otherSummonDistanceFailCount} " +
                $"center:{report.center} radius:{report.radius:F2}"
            );
            return false;
        }

        Quaternion spawnRotation = ResolveSummonRotation(spawnPosition);
        GameObject spawnedObject = Instantiate(prefabObject, spawnPosition, spawnRotation);
        RugbySummonEnemy enemy = spawnedObject.GetComponent<RugbySummonEnemy>();
        if (enemy == null)
            enemy = spawnedObject.GetComponentInChildren<RugbySummonEnemy>();

        if (enemy == null)
        {
            Debug.LogWarning($"{name} Haka召喚失敗: Instantiate後のObjectにRugbySummonEnemyが見つかりません。 spawnedObject:{spawnedObject.name}");
            Destroy(spawnedObject);
            return false;
        }

        enemy.Initialize(this, playerTarget);
        RegisterSummonEnemy(enemy);
        lastSummonPosition = spawnPosition;
        hasLastSummonPosition = true;

        if (debugParam.logSummon)
            Debug.Log($"{name} Haka召喚成功 object:{spawnedObject.name} enemy:{enemy.name} pos:{spawnPosition} active:{activeSummonEnemies.Count}");

        return true;
    }

    private GameObject ResolveSummonPrefabObject()
    {
        if (hakaParam.summonEnemyPrefabObject != null)
        {
            RugbySummonEnemy rootEnemy = hakaParam.summonEnemyPrefabObject.GetComponent<RugbySummonEnemy>();
            RugbySummonEnemy childEnemy = hakaParam.summonEnemyPrefabObject.GetComponentInChildren<RugbySummonEnemy>(true);

            if (rootEnemy != null || childEnemy != null)
                return hakaParam.summonEnemyPrefabObject;

            if (debugParam.logSummon)
                Debug.LogWarning($"{name} Haka召喚Prefab確認: summonEnemyPrefabObjectにはRugbySummonEnemyが見つかりません。 object:{hakaParam.summonEnemyPrefabObject.name}");
        }

        if (hakaParam.summonEnemyPrefab != null)
            return hakaParam.summonEnemyPrefab.gameObject;

        return null;
    }

    private struct HakaSummonSearchReport
    {
        public int totalAttempts;
        public int navMeshFailCount;
        public int playerDistanceFailCount;
        public int otherSummonDistanceFailCount;
        public int unknownFailCount;
        public Vector3 center;
        public float radius;
    }

    private bool TryFindHakaSummonPosition(out Vector3 result, out HakaSummonSearchReport report)
    {
        result = transform.position;
        report = new HakaSummonSearchReport
        {
            totalAttempts = 0,
            navMeshFailCount = 0,
            playerDistanceFailCount = 0,
            otherSummonDistanceFailCount = 0,
            unknownFailCount = 0,
            center = GetHakaSummonCenter(),
            radius = Mathf.Max(0.1f, hakaParam.spawnRadius)
        };

        int attempts = Mathf.Max(1, hakaParam.maxSpawnSearchAttempts);
        Vector3 navCenter;
        bool hasNavCenter = TryProjectToNavMesh(report.center, out navCenter);
        Vector3 searchCenter = hasNavCenter ? navCenter : report.center;

        for (int i = 0; i < attempts; i++)
        {
            report.totalAttempts++;
            Vector2 randomCircle = Random.insideUnitCircle * report.radius;
            Vector3 candidate = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 originalCandidate = candidate;

            if (!TryProjectToNavMesh(candidate, out candidate))
            {
                report.navMeshFailCount++;
                if (debugParam.logSummonSearchDetail)
                    Debug.Log($"{name} Haka召喚位置候補 rejected NavMesh attempt:{i + 1}/{attempts} candidate:{originalCandidate}");
                continue;
            }

            if (!IsFarEnoughFromPlayer(candidate))
            {
                report.playerDistanceFailCount++;
                if (debugParam.logSummonSearchDetail)
                    Debug.Log($"{name} Haka召喚位置候補 rejected PlayerDistance attempt:{i + 1}/{attempts} candidate:{candidate}");
                continue;
            }

            if (!IsFarEnoughFromOtherSummons(candidate))
            {
                report.otherSummonDistanceFailCount++;
                if (debugParam.logSummonSearchDetail)
                    Debug.Log($"{name} Haka召喚位置候補 rejected OtherSummonDistance attempt:{i + 1}/{attempts} candidate:{candidate}");
                continue;
            }

            result = candidate;
            return true;
        }

        return false;
    }

    private Vector3 GetHakaSummonCenter()
    {
        if (hakaParam.spawnCenter != null)
            return hakaParam.spawnCenter.position;
        if (playerTarget != null)
            return playerTarget.position;
        return transform.position;
    }

    private bool TryProjectToNavMesh(Vector3 candidate, out Vector3 projected)
    {
        projected = candidate;

        if (!hakaParam.useNavMesh)
            return true;

        float baseDistance = Mathf.Max(0.01f, hakaParam.navMeshSampleDistance);
        float[] sampleDistances =
        {
            baseDistance,
            baseDistance * 2f,
            baseDistance * 4f,
            Mathf.Max(baseDistance, 8f),
            Mathf.Max(baseDistance, 16f)
        };

        float[] yOffsets = { 0f, 1f, -1f, 3f, -3f, 6f, -6f, 12f, -12f };

        for (int d = 0; d < sampleDistances.Length; d++)
        {
            float distance = sampleDistances[d];
            for (int y = 0; y < yOffsets.Length; y++)
            {
                Vector3 probe = candidate + Vector3.up * yOffsets[y];
                NavMeshHit hit;
                bool found = NavMesh.SamplePosition(probe, out hit, distance, NavMesh.AllAreas);

                if (!found)
                    continue;

                projected = hit.position;
                return true;
            }
        }

        if (!hakaParam.requireNavMeshPoint)
        {
            projected = candidate;
            return true;
        }

        return false;
    }

    private bool IsFarEnoughFromPlayer(Vector3 position)
    {
        if (playerTarget == null)
            return true;
        float distance = GetXZDistance(position, playerTarget.position);
        return distance >= Mathf.Max(0f, hakaParam.minDistanceFromPlayer);
    }

    private bool IsFarEnoughFromOtherSummons(Vector3 position)
    {
        CleanupSummonEnemyList();
        float minDistance = Mathf.Max(0f, hakaParam.minDistanceFromOtherSummons);

        for (int i = 0; i < activeSummonEnemies.Count; i++)
        {
            RugbySummonEnemy enemy = activeSummonEnemies[i];
            if (enemy == null)
                continue;
            float distance = GetXZDistance(position, enemy.transform.position);
            if (distance < minDistance)
                return false;
        }

        return true;
    }

    private float GetXZDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Quaternion ResolveSummonRotation(Vector3 spawnPosition)
    {
        if (!hakaParam.facePlayerOnSpawn || playerTarget == null)
            return transform.rotation;

        Vector3 direction = playerTarget.position - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void RegisterSummonEnemy(RugbySummonEnemy enemy)
    {
        if (enemy == null)
            return;
        if (!activeSummonEnemies.Contains(enemy))
            activeSummonEnemies.Add(enemy);
    }

    private void CleanupSummonEnemyList()
    {
        activeSummonEnemies.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
    }

    private string GetComponentObjectName(Component component)
    {
        return component != null ? component.gameObject.name : "null";
    }

    public void AnimEvent_HakaEffectA()
    {
        effectPlayer.Play(7);
    }

    public void AnimEvent_HakaEffectB_LR(int LR)
    {
        if (LR == 0)
        {
            effectPlayer.PlayAt(8, hakaParam.LElbowPoint.position);
        }
        else if (LR == 1)
        {
            effectPlayer.PlayAt(8, hakaParam.RElbowPoint.position);
        }
    }

    public void AnimEvent_HakaEffectC()
    {
        effectPlayer.Play(9);
    }

    #endregion

    #region Down

    private bool CanEnterDown()
    {
        if (isDead)
            return false;
        if (IsDown)
            return false;
        return downGauge >= GetCurrentDownGaugeMax();
    }

    private void RecoverFromDown()
    {
        downGauge = Mathf.Max(0f, statusParam.downGaugeAfterRecovery);
        if (debugParam.logDownGauge)
            Debug.Log($"{name} Down復帰 Down:{downGauge:F1}/{GetCurrentDownGaugeMax():F1}");
    }

    #endregion

    #region Death

    public void AnimEvent_DeathResult()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.AddKill(statusParam.enemyData);
    }

    public void AnimEvent_DeathEffect()
    {
        effectPlayer.Play(3);
    }


    public void AnimEvent_DeathMiniExplosionEffect()
    {
        PlayDeathMiniExplosionEffects();
    }

    private void PlayDeathMiniExplosionEffects()
    {
        if (effectPlayer == null)
        {
            if (debugParam.logAnimationEvent)
                Debug.LogWarning($"{name} DeathMiniExplosion skipped: EffectPlayer is null.");
            return;
        }

        List<GameObject> validPoints = CollectValidDeathEffectPoints();
        int minCount = Mathf.Max(0, deadParam.miniExplosionMinCount);
        int maxCount = Mathf.Max(minCount, deadParam.miniExplosionMaxCount);
        int effectCount = Random.Range(minCount, maxCount + 1);

        int effectIndex = Random.Range(4, 6);

        if (!deadParam.allowDuplicateMiniExplosionPoint && validPoints.Count > 0)
            effectCount = Mathf.Min(effectCount, validPoints.Count);

        if (effectCount <= 0)
            return;

        if (validPoints.Count <= 0)
        {
            for (int i = 0; i < effectCount; i++)
            {
                effectPlayer.Play(effectIndex);
            }


            if (debugParam.logAnimationEvent)
                Debug.LogWarning($"{name} DeathMiniExplosion used boss position because effectPoints is empty. count:{effectCount}");

            return;
        }

        for (int i = 0; i < effectCount; i++)
        {
            int pointIndex = Random.Range(0, validPoints.Count);
            GameObject point = validPoints[pointIndex];

            effectPlayer.Play(effectIndex, point.transform);

            var ef = effectPlayer.Play(effectIndex, point.transform);

            //ef.Attach(
            //        point.transform,
            //       point.transform);

            if (!deadParam.allowDuplicateMiniExplosionPoint)
                validPoints.RemoveAt(pointIndex);
        }

        if (debugParam.logAnimationEvent)
            Debug.Log($"{name} DeathMiniExplosion effect:{effectIndex} count:{effectCount}");
    }

    private List<GameObject> CollectValidDeathEffectPoints()
    {
        List<GameObject> validPoints = new List<GameObject>();

        if (deadParam.effectPoints == null)
            return validPoints;

        for (int i = 0; i < deadParam.effectPoints.Count; i++)
        {
            GameObject point = deadParam.effectPoints[i];
            if (point == null)
                continue;

            validPoints.Add(point);
        }

        return validPoints;
    }
    #endregion

    #region Try Decision

    private TryMotionParam SelectNearestTryMotionFromAll()
    {
        int fallbackIndex = Mathf.Max(0, tryParam.fallbackIndex);
        currentTryIndex = fallbackIndex;
        currentBestTryDistance = float.MaxValue;
        currentSelectedTryPointPosition = transform.position;

        TryMotionParam[] motions = tryParam.motions;
        if (motions == null || motions.Length == 0)
        {
            if (debugParam.logTrySelect)
                Debug.LogWarning($"{name} Try候補が空です。Idleへ戻します。");
            return null;
        }

        TryMotionParam fallback = GetFallbackTryMotionAvoidingLast(motions, fallbackIndex);
        TryMotionParam best = null;
        TryMotionParam secondBest = null;
        int bestIndex = -1;
        int secondBestIndex = -1;
        float bestDistance = float.MaxValue;
        float secondBestDistance = float.MaxValue;

        for (int i = 0; i < motions.Length; i++)
        {
            TryMotionParam motion = motions[i];
            if (motion == null || !motion.enabled || string.IsNullOrEmpty(motion.animationStateName))
                continue;
            if (motion.targetPoint == null)
                continue;

            float distance = GetDistanceToPlayer(motion.targetPoint.position);
            if (distance < bestDistance)
            {
                secondBest = best;
                secondBestIndex = bestIndex;
                secondBestDistance = bestDistance;

                best = motion;
                bestIndex = i;
                bestDistance = distance;
            }
            else if (distance < secondBestDistance)
            {
                secondBest = motion;
                secondBestIndex = i;
                secondBestDistance = distance;
            }
        }

        TryMotionParam selected = best;
        int selectedIndex = bestIndex;
        float selectedDistance = bestDistance;
        bool avoidedRepeat = false;

        if (hasLastTryMotionIndex && bestIndex == lastTryMotionIndex && secondBest != null)
        {
            selected = secondBest;
            selectedIndex = secondBestIndex;
            selectedDistance = secondBestDistance;
            avoidedRepeat = true;
        }

        if (selected == null)
        {
            selected = fallback;
            selectedIndex = GetMotionIndex(motions, selected);
            selectedDistance = float.MaxValue;
        }

        if (selected == null)
            return null;

        currentTryIndex = Mathf.Max(0, selectedIndex);
        currentBestTryDistance = selectedDistance;
        currentSelectedTryPointPosition = selected.targetPoint != null ? selected.targetPoint.position : transform.position;
        lastTryMotionIndex = currentTryIndex;
        hasLastTryMotionIndex = true;

        if (debugParam.logTrySelect)
        {
            Debug.Log(
                $"{name} Try決定 Index:{currentTryIndex} Anim:{selected.animationStateName} " +
                $"Distance:{currentBestTryDistance:F2} AvoidRepeat:{avoidedRepeat} LastTryIndex:{lastTryMotionIndex}"
            );
        }

        return selected;
    }

    private TryMotionParam GetFallbackTryMotion(TryMotionParam[] motions, int preferredIndex)
    {
        if (motions == null || motions.Length == 0)
            return null;

        preferredIndex = Mathf.Clamp(preferredIndex, 0, motions.Length - 1);
        TryMotionParam preferred = motions[preferredIndex];

        if (IsUsableFallbackMotion(preferred))
            return preferred;

        for (int i = 0; i < motions.Length; i++)
        {
            TryMotionParam motion = motions[i];
            if (IsUsableFallbackMotion(motion))
                return motion;
        }

        return null;
    }

    private TryMotionParam GetFallbackTryMotionAvoidingLast(TryMotionParam[] motions, int preferredIndex)
    {
        if (motions == null || motions.Length == 0)
            return null;

        preferredIndex = Mathf.Clamp(preferredIndex, 0, motions.Length - 1);
        TryMotionParam preferred = motions[preferredIndex];

        if (IsUsableFallbackMotion(preferred) && (!hasLastTryMotionIndex || preferredIndex != lastTryMotionIndex))
            return preferred;

        for (int i = 0; i < motions.Length; i++)
        {
            if (hasLastTryMotionIndex && i == lastTryMotionIndex)
                continue;

            TryMotionParam motion = motions[i];
            if (IsUsableFallbackMotion(motion))
                return motion;
        }

        if (IsUsableFallbackMotion(preferred))
            return preferred;

        for (int i = 0; i < motions.Length; i++)
        {
            TryMotionParam motion = motions[i];
            if (IsUsableFallbackMotion(motion))
                return motion;
        }

        return null;
    }

    private int GetMotionIndex(TryMotionParam[] motions, TryMotionParam targetMotion)
    {
        if (motions == null || targetMotion == null)
            return -1;

        for (int i = 0; i < motions.Length; i++)
        {
            if (motions[i] == targetMotion)
                return i;
        }

        return -1;
    }

    private bool IsUsableFallbackMotion(TryMotionParam motion)
    {
        return motion != null && motion.enabled && !string.IsNullOrEmpty(motion.animationStateName);
    }

    private float GetDistanceToPlayer(Vector3 pointPosition)
    {
        if (playerTarget == null)
            return float.MaxValue;

        Vector3 playerPosition = playerTarget.position;
        if (tryParam.useXZDistance)
        {
            pointPosition.y = 0f;
            playerPosition.y = 0f;
        }

        return Vector3.Distance(pointPosition, playerPosition);
    }

    #endregion

    #region Animation

    private void CrossFadeAnimation(string stateName, float fixedTransitionDuration)
    {
        currentAnimationStateName = stateName;

        if (!animatorParam.useAnimator || animatorParam.animator == null || string.IsNullOrEmpty(stateName))
            return;

        animatorParam.animator.CrossFadeInFixedTime(
            stateName,
            Mathf.Max(0f, fixedTransitionDuration),
            animatorParam.layerIndex,
            0f
        );
    }

    private bool IsCurrentAnimationFinished(string stateName, float finishNormalizedTime, bool waitTransitionCompleteBeforeFinish)
    {
        if (!animatorParam.useAnimator || animatorParam.animator == null || string.IsNullOrEmpty(stateName))
            return true;

        if (waitTransitionCompleteBeforeFinish && animatorParam.animator.IsInTransition(animatorParam.layerIndex))
            return false;

        AnimatorStateInfo info = animatorParam.animator.GetCurrentAnimatorStateInfo(animatorParam.layerIndex);
        if (!IsAnimatorState(info, stateName))
            return false;

        return info.normalizedTime >= finishNormalizedTime;
    }

    private bool IsCurrentAnimationReachedNormalizedTime(string stateName, float normalizedTime, bool waitTransitionCompleteBeforeFinish)
    {
        if (!animatorParam.useAnimator || animatorParam.animator == null || string.IsNullOrEmpty(stateName))
            return true;

        if (waitTransitionCompleteBeforeFinish && animatorParam.animator.IsInTransition(animatorParam.layerIndex))
            return false;

        AnimatorStateInfo info = animatorParam.animator.GetCurrentAnimatorStateInfo(animatorParam.layerIndex);
        if (!IsAnimatorState(info, stateName))
            return false;

        return info.normalizedTime >= normalizedTime;
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
        if (!animatorParam.useAnimator || !animatorParam.syncAnimatorSpeedWithTimeAgent || animatorParam.animator == null)
            return;

        animatorParam.animator.speed = TimeScale;
    }

    public void AnimEvent_SE(string se)
    {
        audioManager.PlaySe(se);
    }

    public void AnimEvent_Slow(float scale)
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, scale);
    }



    #endregion

    #region External Control

    public void StartBoss()
    {
        if (isDead)
            return;

        bossStarted = true;
        if (debugParam.logState)
            Debug.Log($"{name} RugbyBoss StartBoss");
    }

    public void StopBossForDebug()
    {
        bossStarted = false;
        if (debugParam.logState)
            Debug.Log($"{name} RugbyBoss StopBossForDebug");
    }

    public void RequestTryForDebug()
    {
        if (isDead)
            return;

        ReserveState(new TryState(this), 1000);
        ApplyReservedState();
    }

    public void RequestHakaForDebug()
    {
        if (isDead)
            return;

        ReserveState(new HakaState(this), 1000);
        ApplyReservedState();
    }

    public void AddDownGaugeForDebug(float value)
    {
        if (isDead)
            return;

        downGauge += Mathf.Max(0f, value);
        if (debugParam.logDownGauge)
            Debug.Log($"{name} Debug Down加算 value:{value:F1} Down:{downGauge:F1}/{GetCurrentDownGaugeMax():F1}");
    }

    #endregion

    #region Hit Interfaces

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        TrySendTryAttackDamage(selfHitbox, other);
        TrySendBeamDamage(selfHitbox, other);
    }

    public void OnHit(HitEventData data)
    {
        if (isDead)
            return;
        if (data.targetHitbox == null)
            return;

        if (data.payload is ChainPayload chainPayload && IsChainReceiveHitbox(data.targetHitbox))
        {
            float damage = chainPayload.damage > 0f ? chainPayload.damage : statusParam.fallbackChainDamage;
            ApplyDamageAndDown(damage, statusParam.chainHitDownValue, "Chain", data);
            return;
        }

        if (data.payload is BlowPayload && IsPlayerTackleReceiveHitbox(data.targetHitbox))
        {
            float damage = ResolveTackleDamage(data);
            float downValue = ResolveTackleDownValue(data);
            ApplyDamageAndDown(damage, downValue, "PlayerTackle", data);
        }
    }

    #endregion

    #region Damage / Down / Angry

    private float ResolveTackleDamage(HitEventData data)
    {
        if (!statusParam.useBlowPayloadDamage)
            return Mathf.Max(0f, statusParam.fallbackTackleDamage);

        if (data.payload is BlowPayload blow)
        {
            float damage = blow.powerConstant * Mathf.Clamp01(blow.powerRate)* statusParam.playerTackleDamageMultiplier;
            if (damage > 0f)
                return damage;
        }

        return Mathf.Max(0f, statusParam.fallbackTackleDamage);
    }

    private float ResolveTackleDownValue(HitEventData data)
    {
        float downValue = Mathf.Max(0f, statusParam.playerTackleDownValue);
        if (statusParam.multiplyTackleDownByPowerRate && data.payload is BlowPayload blow)
            downValue *= Mathf.Clamp01(blow.powerRate);
        return downValue;
    }

    private void ApplyDamageAndDown(float damage, float downValue, string reason, HitEventData data)
    {
        if (isDead)
            return;

        damage = Mathf.Max(0f, damage);
        downValue = Mathf.Max(0f, downValue);
        hp = Mathf.Max(0f, hp - damage);
        downGauge += downValue;

        materialFlashPlayer.PlayFlash();

        if (debugParam.logDamage)
        {
            Debug.Log(
                $"{name} Damage reason:{reason} damage:{damage:F1} down:{downValue:F1} " +
                $"HP:{hp:F1}/{statusParam.maxHP:F1} Down:{downGauge:F1}/{GetCurrentDownGaugeMax():F1} " +
                $"targetHitbox:{GetObjectName(data.targetHitbox)}"
            );
        }

        if (hp <= 0f)
        {
            EnterDead();
            return;
        }

        TryEnterAngry();
    }

    private bool TryEnterAngry()
    {
        // 怒り状態への実際の遷移はIdleStateで行います。
        // Damage処理やUpdate側の既存呼び出しとの互換用に、ここでは閾値到達だけを返します。
        if (isAngry || isDead)
            return false;

        return hp <= statusParam.angryHPThreshold;
    }

    private void EnterDead()
    {
        if (isDead)
            return;

        ReserveState(new DeadState(this), int.MaxValue);
        ApplyReservedState();
    }

    private float GetCurrentDownGaugeMax()
    {
        return isAngry ? Mathf.Max(1f, statusParam.angryDownGaugeMax) : Mathf.Max(1f, statusParam.downGaugeMax);
    }


    private void RefreshReceiveHitboxStates()
    {
        // PlayerTackleReceiveHitboxes は DownState と TryState の間だけ有効。
        // Try攻撃のAnimEventではこのコライダーを触らず、Stateに入っているかどうかだけで決める。
        bool playerTackleActive = !isDead && (IsDown || currentState is TryState);

        // ChainReceiveHitboxes は基本常時有効。
        // 例外として、怒り移行モーション中・ビーム中・死亡中だけ無効化する。
        // isAngryフラグだけでは判定しない。怒り後の通常行動中はONに戻す。
        bool chainActive = !isDead &&
                           !(currentState is AngryState) &&
                           !(currentState is AngryBeamState) &&
                           !(currentState is DeadState);

        // 非IsTriggerの物理接触Colliderは、TryState / DownState中だけ有効。
        bool physicalContactActive = !isDead && (IsDown || currentState is TryState);

        playerTackleReceiveHitboxesActive = playerTackleActive;
        physicalContactCollidersActive = physicalContactActive;

        SetHitboxListActiveUnique(hitboxParam.playerTackleReceiveHitboxes, playerTackleActive);
        SetHitboxListActiveUnique(hitboxParam.chainReceiveHitboxes, chainActive);
        SetColliderListEnabledUnique(hitboxParam.physicalContactColliders, physicalContactActive);
    }

    private void SetHitboxListActiveUnique(List<GameObject> hitboxes, bool active)
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
    private void SetColliderListEnabledUnique(List<Collider> colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Count; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            if (col.enabled == enabled)
                continue;

            col.enabled = enabled;

            if (hitboxParam.logPhysicalContactColliderState)
            {
                Debug.Log(
                    $"{name} PhysicalContactCollider {(enabled ? "ON" : "OFF")} " +
                    $"collider:{col.name} state:{CurrentStateName}",
                    col
                );
            }
        }
    }

    private void ValidatePhysicalContactColliders()
    {
        if (!hitboxParam.warnIfPhysicalContactColliderIsTrigger)
            return;

        if (hitboxParam.physicalContactColliders == null)
            return;

        for (int i = 0; i < hitboxParam.physicalContactColliders.Count; i++)
        {
            Collider col = hitboxParam.physicalContactColliders[i];
            if (col == null)
                continue;

            if (!col.isTrigger)
                continue;

            Debug.LogWarning(
                $"{name} PhysicalContactCollider warning: {col.name} is Trigger. " +
                "物理接触用Colliderは非IsTriggerを登録してください。",
                col
            );
        }
    }

    public void AnimEvent_DownEffect()
    {
        effectPlayer.Play(12);
    }

    #endregion

    #region Hitbox Check

    private bool IsPlayerTackleReceiveHitbox(GameObject targetHitbox)
    {
        return ContainsHitbox(hitboxParam.playerTackleReceiveHitboxes, targetHitbox);
    }

    private bool IsChainReceiveHitbox(GameObject targetHitbox)
    {
        return ContainsHitbox(hitboxParam.chainReceiveHitboxes, targetHitbox);
    }

    private bool ContainsHitbox(List<GameObject> hitboxes, GameObject targetHitbox)
    {
        if (hitboxes == null || targetHitbox == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject registered = hitboxes[i];
            if (registered == null)
                continue;

            if (registered == targetHitbox)
                return true;

            if (!hitboxParam.allowChildHitboxMatch)
                continue;

            if (targetHitbox.transform.IsChildOf(registered.transform))
                return true;

            if (registered.transform.IsChildOf(targetHitbox.transform))
                return true;
        }

        return false;
    }

    #endregion

    #region Utility / Debug

    private string GetObjectName(GameObject obj)
    {
        return obj != null ? obj.name : "null";
    }

    private void DrawTryMotionGizmos(TryMotionParam[] motions, Color color)
    {
        if (motions == null)
            return;

        Gizmos.color = color;

        for (int i = 0; i < motions.Length; i++)
        {
            TryMotionParam motion = motions[i];
            if (motion == null || motion.targetPoint == null)
                continue;

            Gizmos.DrawWireSphere(motion.targetPoint.position, 0.28f);
            Gizmos.DrawLine(motion.targetPoint.position + Vector3.up * 0.4f, motion.targetPoint.position + Vector3.up * 1.0f);
        }
    }

    private void DrawSummonGizmos()
    {
        Vector3 center = hakaParam.spawnCenter != null ? hakaParam.spawnCenter.position : transform.position;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0.1f, hakaParam.spawnRadius));

        if (playerTarget != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(playerTarget.position, Mathf.Max(0f, hakaParam.minDistanceFromPlayer));
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < activeSummonEnemies.Count; i++)
        {
            RugbySummonEnemy enemy = activeSummonEnemies[i];
            if (enemy == null)
                continue;
            Gizmos.DrawWireSphere(enemy.transform.position, Mathf.Max(0f, hakaParam.minDistanceFromOtherSummons));
        }

        if (hasLastSummonPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastSummonPosition, 0.35f);
        }
    }

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

        Rect rect = new Rect(screen.x - 190f, Screen.height - screen.y - 24f, 460f, 270f);
        GUI.Label(
            rect,
            $"RugbyBoss Haka/Summon/Scrum/RocketPunch\n" +
            $"State:{CurrentStateName} Started:{bossStarted}\n" +
            $"Action:{currentSelectedAction}\n" +
            $"Anim:{currentAnimationStateName}\n" +
            $"HP:{hp:F0}/{statusParam.maxHP:F0} Angry:{isAngry}\n" +
            $"Down:{downGauge:F0}/{GetCurrentDownGaugeMax():F0}\n" +
            $"TryIndex:{currentTryIndex} Dist:{currentBestTryDistance:F2}\n" +
            $"Summons:{activeSummonEnemies.Count}\n" +
            $"ScrumUnlocked:{scrumCommandUnlockedByHaka} ScrumInvoked:{currentScrumCommandInvoked}\n" +
            $"RocketLaunched:{currentRocketPunchLaunched} RocketResolved:{currentRocketPunchResolved}\n" +
            $"TimeScale:{TimeScale:F2}"
        );
#endif
    }

    #endregion
}
