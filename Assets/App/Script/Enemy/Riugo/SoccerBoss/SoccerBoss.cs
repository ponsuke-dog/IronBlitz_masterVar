using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class SoccerBoss : MonoBehaviour, IHitReceiver, IHitSource
{
    #region Enums

    private enum BossState
    {
        Idle,
        KickArc,
        FreeKick,
        Slide,
        OverheadKick,
        ReflectedHit,
        Anger,
        Roar,
        Stunned,
        Dead
    }
    private enum BossAttackType
    {
        Punt,
        Slide,
        Overhead,
        FreeKick,
        Roar
    }
    private enum BossAnchorSide
    {
        North,
        East,
        South,
        West
    }

    private enum AnimationFinishMode
    {
        Timer,
        AnimationEnd,
        AnimationEvent
    }

    private enum SlidePhase
    {
        None,

        // 構え
        ReadyAnimation,

        // 溜めループ。Timerで抜ける
        ChargeLoop,

        // スライディング開始前の飛び上がり。スクリプトで開始地点へ移動
        JumpInAnimation,

        // スライディングループ。到達で抜ける
        LoopMove,

        // スライディング終了後の飛び上がり。スクリプトで場外Anchorへ移動
        JumpOutAnimation,

        // 着地
        LandingAnimation
    }

    private enum SlideEntryDirection
    {
        Center,
        Right,
        Left
    }
    private enum StunPhase
    {
        None,

        // 倒れるモーション
        DownAnimation,

        // 気絶ループ
        StunLoop,

        // 起き上がりモーション
        GetUpAnimation
    }

    private enum DeadPhase
    {
        None,

        // 死亡時の体勢変化モーション
        FallAnimation,

        // DeadFall / DownDeath 後に再生する爆散モーション
        BurstAnimation,

        // 爆散終了後。死亡処理完了状態
        Finished
    }

    private enum EffectIdParity
    {
        Even,
        Odd
    }

    #endregion

    #region Serializable Params

    [System.Serializable]
    private class StateAnimationFinishParam
    {
        [Header("Finish")]
        public AnimationFinishMode finishMode = AnimationFinishMode.AnimationEnd;

        [Tooltip("Timer終了用")]
        public float duration = 1.0f;

        [Tooltip("AnimationEnd時、この再生割合で終了扱い")]
        [Range(0f, 1.2f)]
        public float endNormalizedTime = 0.95f;
    }

    [System.Serializable]
    private class StatusParam
    {
        [Header("HP")]
        public float maxHP = 300f;
        public float angryHPThreshold = 150f;

        [Header("Stun")]
        [Tooltip("通常時のStun突入値")]
        public float stunGaugeMax = 100f;

        [Tooltip("怒り時のStun突入値")]
        public float angryStunGaugeMax = 150f;

        public float reflectedBallStunValue = 30f;

        public float FieldObjectStunValue = 10f;

        [Tooltip("通常時のStun時間")]
        public float stunnedDuration = 5.0f;

        [Tooltip("怒り時のStun時間")]
        public float angryStunnedDuration = 3.0f;

        [Tooltip("Stun復帰後のStun値")]
        public float stunGaugeAfterRecovery = 0f;

        [Tooltip("怒り突入時にStun値をリセットする")]
        public bool resetStunGaugeOnAngryEnter = true;

        [Tooltip("怒り突入時に設定するStun値。基本0")]
        public float stunGaugeOnAngryEnter = 0f;

        [Header("Damage")]
        public float reflectedBallDamage = 25f;

        [Header("Anger Effect Point")]
        public Transform angerEffectPoint;

        [Header("DeathCameraPoint")]
        public CameraManager cameraManager;

        [Header("Group")]
        public EnemyData enemyData;
    }

    [System.Serializable]
    private class AttackPatternParam
    {
        [Tooltip("行動種類")]
        public BossAttackType attackType = BossAttackType.Punt;

        [Tooltip("抽選確率。通常/怒りそれぞれの合計が100になるように設定")]
        [Range(0f, 100f)]
        public float chancePercent = 25f;

        [Header("Consecutive Limit")]
        [Tooltip("同じ行動の連続制限を使う")]
        public bool useConsecutiveLimit = true;

        [Tooltip("同じ行動を最大何回まで連続可能にするか")]
        public int maxConsecutiveCount = 1;
    }

    [System.Serializable]
    private class AttackSelectParam
    {
        [Header("Normal Attack Table")]
        [Tooltip("通常時の行動抽選。Punt / Slide / FreeKickを想定")]
        public AttackPatternParam[] normalAttacks =
        {
            new AttackPatternParam
            {
                attackType = BossAttackType.Punt,
                chancePercent = 50f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 2
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Slide,
                chancePercent = 25f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.FreeKick,
                chancePercent = 25f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            }
        };

        [Header("Angry Attack Table")]
        [Tooltip("怒り時の行動抽選。Punt / Slide / Overhead / FreeKickを想定")]
        public AttackPatternParam[] angryAttacks =
        {
            new AttackPatternParam
            {
                attackType = BossAttackType.Punt,
                chancePercent = 10f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 2
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Slide,
                chancePercent = 25f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Overhead,
                chancePercent = 50f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.FreeKick,
                chancePercent = 10f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Roar,
                chancePercent = 5f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            }
        };

        [Header("Special Rules")]
        [Tooltip("通常時/怒り時それぞれの最初の行動をKick系にする")]
        public bool forceFirstAttackOfEachPhaseToKick = true;

        [Tooltip("Roar後の次行動を必ずKick系にする")]
        public bool forceAttackAfterRoarToKick = true;

        [Tooltip("FieldObjectが残っていない時はRoarを抽選候補から外す")]
        public bool disableRoarWhenNoFieldObject = true;

        [Tooltip("特殊ルールで候補から外した時のログ")]
        public bool logAttackFilter = true;

        [Header("Debug")]
        public bool logAttackSelect = true;
        public bool warnIfTotalChanceIsNot100 = true;
    }

    [System.Serializable]
    private class TimingParam
    {
        [Header("Interval")]
        public float idleBeforeAttack = 0.8f;
        public float angryIdleBeforeAttack = 0.45f;
    }

    [System.Serializable]
    private class StageParam
    {
        [Header("Stage Auto Detect")]
        [Tooltip("ステージ本体。ColliderかRendererが付いているオブジェクト")]
        public GameObject stageObject;

        public bool preferColliderBounds = true;

        [Header("Fallback")]
        public Transform fallbackStageCenter;
        public float fallbackStageSize = 24f;

        [Header("Fixed Anchor")]
        [Tooltip("開始時のBoss位置から、ステージ辺に対するOffsetを自動取得する")]
        public bool autoCaptureAnchorOffsetFromInitialTransform = true;

        [Tooltip("自動取得しない場合に使う、ステージ端から外側への距離")]
        public float anchorOutsideDistance = 5f;

        [Tooltip("開始時に一番近い定位置へ補正する")]
        public bool snapToNearestAnchorOnStart = true;

        [Tooltip("キック時に定位置へ補正する。基本OFF推奨")]
        public bool snapToAnchorBeforeKick = false;

        [Header("Target Clamp")]
        [Tooltip("着弾地点をステージ内に収める余白")]
        public float targetClampMargin = 1.5f;

        [Header("Legacy")]
        [Tooltip("旧方式のY補正。自動Offset方式では基本0推奨")]
        public float bossYOffset = 0f;
    }

    [System.Serializable]
    private class KickParam
    {
        [Header("Arc Kick")]
        public Transform ballSpawnPoint;
        public SoccerBossBall ballPrefab;

        [Tooltip("大きいほどふんわり遅い")]
        public float arcFlightTime = 2.0f;

        [Tooltip("Bezier未使用時の予備。Bezier軌道では基本使わない")]
        public float gravity = 14f;

        [Tooltip("Bezier軌道の山の高さ。大きいほど高くふんわり")]
        public float arcHeight = 7.0f;

        public float targetLeadTime = 0.15f;
        public float landingY = 0.05f;
        public int playerDamage = 1;

        [Header("Aim")]
        [Tooltip("プレイヤー狙い弾のランダムずらし半径")]
        public float playerAimRandomRadius = 1.2f;

        [Tooltip("ランダム着弾地点のステージ端からの余白")]
        public float randomTargetMargin = 2.0f;

        [Header("Multi Ball")]
        public int ballCount = 1;
    }

    [System.Serializable]
    private class OverheadKickParam
    {
        [Header("Overhead Straight Kick")]
        public Transform ballSpawnPoint;
        public SoccerBossBall ballPrefab;

        [Tooltip("直線弾の速度")]
        public float speed = 28f;

        public float lifeTime = 5f;
        public float targetLeadTime = 0.2f;
        public int playerDamage = 1;

        [Header("Aim")]
        public float targetHeightFromStage = 0.6f;
        public float playerAimRandomRadius = 0.8f;
        public float randomTargetMargin = 2.0f;

        [Header("Multi Ball")]
        public int ballCount = 1;
    }

    [System.Serializable]
    private class SlideParam
    {
        [Header("Slide Hit")]
        public SoccerBossSlideHitbox slideHitbox;
        public int damage = 1;

        [Header("Charge Loop")]
        [Tooltip("溜めループを何秒続けるか")]
        public float chargeDuration = 1.0f;

        [Header("Jump In")]
        [Tooltip("飛び上がり中にスライド開始点へ移動する時間。0以下ならアニメ終了までで補間")]
        public float jumpInMoveDuration = 0.45f;

        [Tooltip("飛び上がり中の移動高さ")]
        public float jumpInHeight = 3.0f;

        [Header("Slide Move")]
        [Tooltip("スライディング中の移動速度")]
        public float speed = 18f;

        [Tooltip("到達判定距離")]
        public float reachDistance = 0.2f;

        [Tooltip("スライディング経路をステージ辺中心にする")]
        public bool useRawStageEdgeCenterForSlidePath = true;

        [Header("Slide Root Offset")]
        [Tooltip("スライド開始/終了点に対するRoot位置の辺方向オフセット")]
        public float pathTangentOffset = 0f;

        [Tooltip("スライド開始/終了点に対するRoot位置の外側方向オフセット")]
        public float pathOutsideOffset = 0f;

        [Tooltip("スライド開始/終了点に対するRoot位置の高さオフセット")]
        public float pathYOffset = 0f;

        [Header("Jump Out")]
        [Tooltip("終了時の飛び上がりで場外Anchorへ移動する時間")]
        public float jumpOutDuration = 0.45f;

        [Tooltip("終了時の飛び上がり高さ")]
        public float jumpOutHeight = 3.0f;

        [Header("Facing")]
        public bool faceSlideDirectionOnStartAndLoop = true;
        public bool faceJumpOutDirection = true;
        public bool faceStageCenterOnLanding = true;

        [Header("Slide Foot Reference")]
        [Tooltip("スライディング中に開始点→終了点へ動かしたい足ボーン。基本は一番前に出る右足")]
        public Transform slideReferenceFootBone;
        public Transform slideChargeReferenceToeBone;

        [Tooltip("スライディング中、右足をスライド経路に合わせる")]
        public bool useFootReferenceForSlideMove = true;

        [Tooltip("右足位置合わせ時にY座標も合わせる。基本OFF推奨")]
        public bool alignFootY = false;

        [Tooltip("開始直前に右足を開始地点へ合わせる最大補正距離。大きすぎるとワープする")]
        public float maxFootSnapOnSlideStart = 3.0f;

        [Header("Slide Entry Animation Select")]
        [Tooltip("ONならスライド方向に応じてStanby/Charge/JumpUpモーションを中央/右/左で切り替える")]
        public bool selectEntryAnimationByDirection = true;

        [Tooltip("この値より横成分が大きければ右/左扱い")]
        [Range(0f, 1f)]
        public float sideEntryThreshold = 0.35f;

        [Tooltip("右/左の判定が逆に感じる場合ON")]
        public bool invertEntryRightLeft = false;

        [Tooltip("方向判定ログを出す")]
        public bool logEntryDirection = true;

        [Header("Directional Entry Apply")]
        [Tooltip("Stanby/Readyを方向別に切り替える")]
        public bool useDirectionalReady = true;

        [Tooltip("Chargeを方向別に切り替える")]
        public bool useDirectionalCharge = true;

        [Tooltip("JumpUpを方向別に切り替える")]
        public bool useDirectionalJumpUp = true;

        [Header("Entry Motion Control")]
        [Tooltip("Stanby/Charge/JumpIn中にスクリプトでスライド方向へ回転する。方向別モーションを使うならOFF推奨")]
        public bool faceEntryAnimationsToSlideDirection = false;

        [Tooltip("JumpIn中にスクリプトでXZ位置を開始地点へ補間する。方向別JumpInで足位置を合わせるならOFF推奨")]
        public bool moveJumpInXZToStartByScript = false;

        [Tooltip("SlidingLoop開始時に右足を開始地点へスナップする。モーション側で足が合っているならOFF推奨")]
        public bool snapFootToStartOnLoopEnter = false;

        [Tooltip("SlidingLoop中、右足を毎フレーム目標位置に合わせる。足ボーンが揺れるならOFF推奨")]
        public bool followFootEveryFrameDuringLoop = false;

        [Header("Slide Foot Loop Control")]
        [Tooltip("SlidingD開始時、回転とモーション切り替え後に右足を開始地点へ合わせる")]
        public bool correctFootAfterLoopRotation = true;

        [Tooltip("SlidingD開始時にAnimatorを0秒更新して、初期姿勢を即反映してから足補正する")]
        public bool updateAnimatorBeforeLoopFootCorrection = true;

        [Tooltip("足補正がこの距離を超えたら警告を出す。0以下なら無効")]
        public float warnLoopEnterFootCorrectionDistance = 2.0f;

        [Tooltip("Slidingループ中も右足を基準にして移動する")]
        public bool useFootReferenceDuringLoop = true;

        [Tooltip("Snapしない場合、現在の右足位置を開始点として扱う。ワープ防止向け")]
        public bool useCurrentFootAsLoopStartWhenNoSnap = true;

        [Header("Slide Effect Reference")]
        [Tooltip("スライディング中にエフェクトを出す開始地点")]
        public Transform slideEffectReferenceFootBone;

        [Header("Slide Y Offset Interpolation")]
        [Tooltip("JumpIn中にY補正を補間で適用する")]
        public bool useJumpInYOffsetInterpolation = true;

        [Tooltip("JumpIn完了時に到達するY補正値。下げたい場合はマイナス")]
        public float jumpInYOffset = -0.5f;

        [Tooltip("Y補正に使う時間。0以下ならSlideJumpInのAnimationParam.durationを使う")]
        public float jumpInYOffsetDuration = 0f;

        [Tooltip("Y補正カーブ。0→1で評価される")]
        public AnimationCurve jumpInYOffsetCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("JumpOut終了時にY補正を一気に戻す")]
        public bool restoreYOffsetOnJumpOutEnd = true;
    }

    [System.Serializable]
    private class StunPoseParam
    {
        [Tooltip("復帰時に現在定位置へ戻す")]
        public bool snapBackToAnchorOnRecovery = true;

        [Tooltip("気絶中だけ弱点Hitboxを有効化する")]
        public bool enableStunnedHitbox = true;

        [Header("Effect PlayPoint")]
        public Transform BossHeadPoint;
    }

    [System.Serializable]
    private class HitboxParam
    {
        [Header("FieldObject / Chain Receive")]
        [Tooltip("FieldObjectやChain系の接触を受ける通常ボディHitbox群")]
        public GameObject[] fieldObjectReceiveHitboxes;

        [Tooltip("FieldObject用Colliderを無効化するStateでOFFにする")]
        public bool disableFieldObjectHitboxesDuringSlide = true;

        [Tooltip("FieldObject用ColliderをDeath中にOFFにする")]
        public bool disableFieldObjectHitboxesWhenDead = true;

        [Header("Stunned Weak Point")]
        [Tooltip("気絶中にプレイヤー直接攻撃を受ける弱点Hitbox群")]
        public GameObject[] stunnedHitBoxes;

        [Tooltip("気絶中以外は弱点Hitbox群をOFFにする")]
        public bool disableStunnedHitBoxesWhenNotStunned = true;

        public float stunnedHitCooldown = 0.15f;

        [Header("Slide Attack Hitbox")]
        [Tooltip("スライディング中にプレイヤー/FieldObjectへ攻撃を出すHitbox")]
        public GameObject slideAttackHitbox;

        [Tooltip("SlideのJumpIn開始からJumpOut終了までSlideAttackHitboxをONにする")]
        public bool enableSlideAttackFromJumpInToJumpOut = true;

        [Tooltip("1回のスライディング中、同じ対象に1回だけ当てる")]
        public bool hitSameTargetOncePerSlide = true;

        [Tooltip("スライディングでPlayerへ与えるダメージ")]
        public int slidePlayerDamage = 1;

        [Header("Layer Check")]
        [Tooltip("FieldObject/Chainとして扱うLayer")]
        public LayerMask fieldObjectLayer;

        [Tooltip("Playerとして扱うLayer")]
        public LayerMask playerLayer;

        [Tooltip("スライディングでFieldObjectにも通知する")]
        public bool notifyFieldObjectOnSlideHit = true;

        [Tooltip("判定切り替えログ")]
        public bool logHitboxSwitch = true;
    }

    [System.Serializable]
    private class AnimationEventParam
    {
        [Header("Kick Animation Event")]
        [Tooltip("ONならKickArcのボール生成をAnimation Eventで行う")]
        public bool spawnKickArcByAnimationEvent = true;

        [Tooltip("ONならFreeKickのボール生成をAnimation Eventで行う")]
        public bool spawnFreeKickByAnimationEvent = true;

        [Tooltip("ONならOverheadKickのボール生成をAnimation Eventで行う")]
        public bool spawnOverheadByAnimationEvent = true;
    }

    [System.Serializable]
    private class AnimationParam
    {
        [Header("Main Attack Finish")]
        public StateAnimationFinishParam kickArc = new StateAnimationFinishParam();
        public StateAnimationFinishParam freeKick = new StateAnimationFinishParam();
        public StateAnimationFinishParam overheadKick = new StateAnimationFinishParam();

        [Header("Reflect Hit Finish")]
        public StateAnimationFinishParam reflectedHit = new StateAnimationFinishParam();

        [Header("Anger Finish")]
        public StateAnimationFinishParam anger = new StateAnimationFinishParam();
        public StateAnimationFinishParam stunnedAnger = new StateAnimationFinishParam();

        [Header("Roar Finish")]
        [Tooltip("怒り状態中に行動として使う咆哮モーションの終了判定")]
        public StateAnimationFinishParam roar = new StateAnimationFinishParam();

        [Header("Slide Phase Finish")]
        public StateAnimationFinishParam slideReady = new StateAnimationFinishParam();
        public StateAnimationFinishParam slideJumpIn = new StateAnimationFinishParam();
        public StateAnimationFinishParam slideJumpOut = new StateAnimationFinishParam();
        public StateAnimationFinishParam slideLanding = new StateAnimationFinishParam();

        [Header("Stun Phase Finish")]
        public StateAnimationFinishParam stunDown = new StateAnimationFinishParam();
        public StateAnimationFinishParam stunGetUp = new StateAnimationFinishParam();

        [Header("Dead Phase Finish")]
        public StateAnimationFinishParam deadFall = new StateAnimationFinishParam();

        [Tooltip("Stunned / Down中から死亡へ移行する専用死亡モーションの終了判定")]
        public StateAnimationFinishParam downDeath = new StateAnimationFinishParam();

        [Tooltip("DeadFall / DownDeath 後に再生する爆散モーションの終了判定")]
        public StateAnimationFinishParam deathBurst = new StateAnimationFinishParam();
    }

    [System.Serializable]
    private class BossAnimationParam
    {
        [Header("Animator State Names")]
        public string idle = "Boss_Idle";

        [Header("PuntKick")]
        public string kickArc = "Boss_KickArc";
        [Header("Free Kick")]
        public string freeKick = "SBoss_FreeKick";
        [Header("OverHeadKick")]
        public string overheadKick = "Boss_OverheadKick";


        [Header("Slide")]
        public string slideReady = "SBoss_SlidingA";
        public string slideReadyRight = "SBoss_SlidingA_R";
        public string slideReadyLeft = "SBoss_SlidingA_L";

        public string slideCharge = "SBoss_SlidingB";
        public string slideChargeRight = "SBoss_SlidingB_R";
        public string slideChargeLeft = "SBoss_SlidingB_L";

        public string slideJumpIn = "SBoss_SlidingC";
        public string slideJumpInRight = "SBoss_SlidingC_R";
        public string slideJumpInLeft = "SBoss_SlidingC_L";

        public string slideLoop = "SBoss_SlidingD";
        public string slideJumpOut = "SBoss_SlidingE";
        public string slideLanding = "SBoss_Landing";


        [Header("Stun")]
        public string stunDown = "Boss_StunDown";
        public string stunLoop = "Boss_StunLoop";
        public string stunGetUp = "Boss_StunGetUp";

        [Header("Reflect Hit")]
        public string reflectedHit = "Boss_ReflectedHit";

        [Header("Anger")]
        public string anger = "SBoss_Anger";

        [Tooltip("Stunned中に怒りへ入った時の専用怒りモーション")]
        public string stunnedAnger = "SBoss_StunnedAnger";

        [Header("Roar")]
        [Tooltip("怒り状態中に通常行動として使う咆哮モーション。怒りモーション流用なら SBoss_Anger")]
        public string roar = "SBoss_Anger";

        [Header("Dead")]
        public string deadFall = "Boss_DeadFall";
        public string downDeath = "Boss_DownDeath";
        public string deadLoop = "Boss_DeadLoop";

        [Header("Animator Params")]
        public string angryBool = "IsAngry";

        [Header("Blend Time")]
        public float defaultBlendTime = 0.05f;
        public float idleBlendTime = 0.08f;
        public float kickBlendTime = 0.05f;
        public float freeKickBlendTime = 0.05f;
        public float overheadBlendTime = 0.05f;

        public float slideReadyBlendTime = 0.05f;
        public float slideChargeBlendTime = 0.05f;
        public float slideJumpInBlendTime = 0.05f;
        public float slideLoopBlendTime = 0.03f;
        public float slideJumpOutBlendTime = 0.05f;
        public float slideLandingBlendTime = 0.05f;

        public float stunDownBlendTime = 0.05f;
        public float stunLoopBlendTime = 0.05f;
        public float stunGetUpBlendTime = 0.05f;

        public float reflectedHitBlendTime = 0.05f;

        public float angerBlendTime = 0.05f;
        public float stunnedAngerBlendTime = 0.05f;
        public float roarBlendTime = 0.05f;

        public float deadFallBlendTime = 0.05f;
        public float downDeathBlendTime = 0.05f;
        public float deadLoopBlendTime = 0.05f;

        [Header("Animator Layer")]
        public int animatorLayerIndex = 0;
        public string animatorLayerName = "Base Layer";
        public bool tryFullPathStateName = true;
        public bool logAnimationPlayError = true;

        [Header("Restart")]
        public bool forceRestartKickAnimations = true;
        public bool forceRestartFreeKickAnimations = true;
        public bool forceRestartSlideAnimations = true;
        public bool forceRestartStunAnimations = true;
        public bool forceRestartIdleOnEnter = true;
        public bool updateAnimatorImmediatelyOnRestart = true;
        public bool forceRestartReflectedHitAnimation = true;
        public bool forceRestartAngerAnimation = true;
        public bool forceRestartStunnedAngerAnimation = true;
        public bool forceRestartRoarAnimation = true;
        public bool forceRestartDeadAnimations = true;
        public bool forceRestartDownDeathAnimation = true;

        [Header("Idle Keep Alive")]
        public bool keepIdleStateAlive = true;
        public bool restartIdleWhenFinished = false;
    }

    [System.Serializable]
    private class ReflectTargetParam
    {
        [Header("Reflect Aim Points")]
        [Tooltip("反射ボールが狙えるBoss側のボーン/位置。4か所程度登録想定")]
        public Transform[] aimPoints = new Transform[4];
    }

    [System.Serializable]
    private class AngerExplosionParam
    {
        [Header("SoccerBall Explosion")]
        public bool explodeAllBallsOnAngerEnter = true;
        public bool removeBallsFromActiveListImmediately = true;

        [Header("FieldObject Explosion")]
        public bool explodeFieldObjectsByAnimationEvent = true;

        [Tooltip("BossのEffectPlayerに登録した爆発Effectの番号")]
        public int fieldObjectExplosionEffectIndex = 1;

        public Vector3 fieldObjectExplosionRotationEuler = Vector3.zero;
        public Vector3 fieldObjectExplosionScale = Vector3.one;

        [Tooltip("FieldObject爆発の当たり判定Prefab")]
        public GameObject fieldObjectExplosionHitboxPrefab;

        public float fieldObjectExplosionHitboxLifeTime = 0.25f;

        [Header("FieldObject Explosion Damage")]
        public int fieldObjectExplosionDamage = 1;
        public LayerMask fieldObjectExplosionTargetLayer;

        public bool explodeFieldObjectsOnlyOncePerAnger = true;

        public bool destroyFieldObjectAfterExplosion = true;
        public float destroyFieldObjectDelay = 0f;

        public bool hideFieldObjectRenderers = true;
        public bool disableFieldObjectColliders = true;

        [Header("Debug")]
        public bool logAngerExplosion = true;
    }

    [System.Serializable]
    private class DyingEffectParam
    {
        [Header("Dying Effect")]
        [Tooltip("死亡演出用EffectをAnimationEventで再生する")]
        public bool enable = true;

        [Tooltip("基準にするEffect番号。Evenなら 0,2,4... / Oddなら 1,3,5... から抽選")]
        public EffectIdParity baseParity = EffectIdParity.Even;

        [Header("Effect ID Range")]
        [Tooltip("Small用Effect ID範囲")]
        public int smallMinId = 0;
        public int smallMaxId = 29;

        [Tooltip("Medium用Effect ID範囲")]
        public int mediumMinId = 30;
        public int mediumMaxId = 59;

        [Tooltip("Large用Effect ID範囲")]
        public int largeMinId = 60;
        public int largeMaxId = 89;

        [Header("Grow Count")]
        [Tooltip("Growモードで最初に再生する基準Effect数")]
        public int growStartPlayCount = 1;

        [Tooltip("AnimEvent_DyingEffectが呼ばれるたびに増える基準Effect数")]
        public int growAddCountPerCall = 1;

        [Tooltip("一度に再生する最大基準Effect数")]
        public int growMaxPlayCount = 6;

        [Header("Reset")]
        [Tooltip("死亡ステート突入時にGrowの進行状態をリセットする")]
        public bool resetGrowOnEnterDead = true;

        [Header("Debug")]
        public bool logSelectedEffects = true;
    }

    #endregion

    #region Inspector Fields

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private TimeAgent timeAgent;

    [Header("Start Control")]
    [SerializeField]
    private bool waitForExternalStart = true;

    [SerializeField]
    private bool playIdleWhileWaitingStart = true;

    [Header("Animator")]
    [Tooltip("Boss本体に付いているAnimatorを登録。未設定ならGetComponent<Animator>()で取得")]
    [SerializeField] private Animator animator;

    [Header("Anchor")]
    [SerializeField] private BossAnchorSide currentAnchorSide = BossAnchorSide.West;

    [Header("Status")]
    [SerializeField] private StatusParam statusParam = new StatusParam();

    [Header("Attack Select")]
    [SerializeField] private AttackSelectParam attackSelectParam = new AttackSelectParam();

    [Header("Timing")]
    [SerializeField] private TimingParam timingParam = new TimingParam();

    [Header("Stage")]
    [SerializeField] private StageParam stageParam = new StageParam();

    [Header("PuntKick")]
    [SerializeField] private KickParam kickParam = new KickParam();

    [Header("Free Kick")]
    [SerializeField] private KickParam freeKickParam = new KickParam();

    [Header("Overhead Kick")]
    [SerializeField] private OverheadKickParam overheadKickParam = new OverheadKickParam();

    [Header("Slide")]
    [SerializeField] private SlideParam slideParam = new SlideParam();

    [Header("Stun Pose")]
    [SerializeField] private StunPoseParam stunPoseParam = new StunPoseParam();

    [Header("Hitbox")]
    [SerializeField] private HitboxParam hitboxParam = new HitboxParam();

    [Header("Animation Event")]
    [SerializeField] private AnimationEventParam animationEventParam = new AnimationEventParam();

    [Header("Animation Finish")]
    [SerializeField] private AnimationParam animationParam = new AnimationParam();

    [Header("Animation Names")]
    [SerializeField] private BossAnimationParam bossAnimationParam = new BossAnimationParam();

    [Header("Reflect Target")]
    [SerializeField] private ReflectTargetParam reflectTargetParam = new ReflectTargetParam();

    [Header("Anger Explosion")]
    [SerializeField]private AngerExplosionParam angerExplosionParam = new AngerExplosionParam();

    [Header("Material Flash")]
    [SerializeField] private MaterialFlashPlayer hitMaterialFlashPlayer;

    [Header("Dying Effect")]
    [SerializeField] private DyingEffectParam dyingEffectParam = new DyingEffectParam();

    [Header("Debug")]
    [SerializeField] private bool logState = true;
    [SerializeField] private bool logHit = true;
    [SerializeField] private bool drawStateLabel = true;
    [SerializeField] private Vector3 stateLabelOffset = new Vector3(0f, 3f, 0f);

    #endregion

    #region Runtime Fields

    private readonly List<SoccerBossBall> activeBalls = new List<SoccerBossBall>();

    private float hp;
    private float stunGauge;
    private bool isAngry;

    private BossState state;
    private float stateTimer;
    private bool animationEventFinished;

    private Vector3 previousPlayerPosition;
    private Vector3 estimatedPlayerVelocity;

    private float localTime;
    private float lastStunnedHitTime = -999f;

    private bool spawnedBallsThisState;

    private bool bossStarted;
    private bool initialized;

    //連続攻撃確認用
    private BossAttackType lastAttackType;
    private int consecutiveAttackCount;
    private bool hasLastAttackType;

    private bool hasSelectedNormalAttack;
    private bool hasSelectedAngryAttack;
    private bool forceNextAttackKickOnly;

    // Anchor Offset
    private bool hasCapturedAnchorOffset;
    private Vector3 capturedAnchorLocalOffset;
    private BossAnchorSide capturedAnchorBaseSide;

    // Slide
    private SlidePhase slidePhase = SlidePhase.None;
    private float slidePhaseTimer;

    private Vector3 slideStart;
    private Vector3 slideEnd;
    private BossAnchorSide slideStartSide;
    private BossAnchorSide slideEndSide;

    private Vector3 slideJumpInStart;
    private Vector3 slideJumpInEnd;
    private float slideJumpInTimer;

    private Vector3 slideJumpOutStart;
    private Vector3 slideJumpOutEnd;
    private float slideJumpOutTimer;

    private Vector3 slideDirection;
    private Quaternion slideMoveRotation;

    private Vector3 slideFootLoopStart;
    private Vector3 slideFootLoopEnd;
    private Vector3 slideFootCurrent;

    private SlideEntryDirection currentSlideEntryDirection = SlideEntryDirection.Center;

    private Quaternion slideEntryRotation;

    private Vector3 slideLoopRootStart;
    private Vector3 slideLoopRootEnd;
    private EffectInstance slidingEffect;
    private EffectInstance slidingwarningEffect;

    private bool slideYOffsetActive;
    private float slideCurrentYOffset;
    private float slideYOffsetTimer;
    private float slideYOffsetDuration;

    // Stun
    private StunPhase stunPhase = StunPhase.None;
    private float stunPhaseTimer;
    private EffectInstance stuneffect;

    //Anger
    private bool fieldObjectsExplodedThisAnger;

    // Roar
    private bool fieldObjectsExplodedThisRoar;

    // Dead
    private DeadPhase deadPhase = DeadPhase.None;
    private float deadPhaseTimer;

    // Dying Effect
    private const int dyingEffectSizeOffset = 30;
    private const int dyingEffectLocalSlotCount = 30;

    private readonly int[] dyingEffectActiveLevelByLocalBase =
        new int[dyingEffectLocalSlotCount];

    private bool dyingEffectRuntimeInitialized;
    private int dyingEffectGrowCallCount;

    //HitBox
    private readonly HashSet<GameObject> slideHitTargets = new HashSet<GameObject>();

    //UI
    public float CurrentHP => hp;
    public float MaxHP => statusParam.maxHP;

    public float CurrentStunGauge => stunGauge;
    public float MaxStunGauge => GetCurrentStunGaugeMax();

    public bool IsAngry => isAngry;
    public bool IsBossDead => state == BossState.Dead;
    public float HPRate
    {
        get
        {
            if (statusParam.maxHP <= 0f)
                return 0f;

            return Mathf.Clamp01(hp / statusParam.maxHP);
        }
    }
    public float StunRate
    {
        get
        {
            float max = GetCurrentStunGaugeMax();

            if (max <= 0f)
                return 0f;

            return Mathf.Clamp01(stunGauge / max);
        }
    }

    public bool WaitForExternalStart => waitForExternalStart;
    public bool HasBossStarted => bossStarted;

    public bool ShouldShowBossUI
    {
        get
        {
            // 外部開始待ちを使わないなら最初から表示
            if (!waitForExternalStart)
                return true;

            // 外部開始待ちを使うなら StartBoss() 後だけ表示
            return bossStarted;
        }
    }

    private bool fieldObjectHitboxesActive;
    private bool stunnedHitboxesActive;
    private bool slideAttackHitboxActive;

    private int angryBoolHash;
    private bool angerEnteredFromStunned;
    private bool deathEnteredFromStunned;
    //private bool deathRegistered;

    // Stun中の安全移行予約
    private bool pendingAngerAfterStunSafePoint;
    private bool pendingDeathAfterStunSafePoint;

    private EffectPlayer effectPlayer;
    private EffectBonePlayer effectBonePlayer;
    private AudioManager audioManager;
    public bool IsStunned => state == BossState.Stunned;
    public bool IsDead => state == BossState.Dead;
    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    private float ScaledDeltaTime => Time.deltaTime * TimeScale;


    #endregion

    #region Unity Events

    private void Awake()
    {
        hp = statusParam.maxHP;

        if (timeAgent == null)
            timeAgent = GetComponent<TimeAgent>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (hitMaterialFlashPlayer == null)
            hitMaterialFlashPlayer = GetComponentInChildren<MaterialFlashPlayer>();

        if (playerTarget == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerTarget = p.transform;
        }

        angryBoolHash = Animator.StringToHash(bossAnimationParam.angryBool);

        if (slideParam.slideHitbox != null)
        {
            slideParam.slideHitbox.Initialize(this);
            slideParam.slideHitbox.SetDamage(slideParam.damage);
            slideParam.slideHitbox.SetActiveHit(false);
        }

        if (playerTarget != null)
            previousPlayerPosition = playerTarget.position;

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>();

        if (effectBonePlayer == null)
            effectBonePlayer = GetComponentInChildren<EffectBonePlayer>();

        stuneffect = null;

        slidingEffect = null;

        slidingwarningEffect = null;

        SetFieldObjectHitboxesActive(true);
        SetStunnedHitboxesActive(false);
        SetSlideAttackHitboxActive(false);

    }

    private void Start()
    {
        InitializeBossPosition();

        initialized = true;

        bossStarted = !waitForExternalStart;

        audioManager = AudioManager.Instance;

        if (bossStarted)
        {
            ResumeAnimatorFromExternalWait();
            ChangeState(BossState.Idle);
            return;
        }

        if (playIdleWhileWaitingStart)
        {
            ResumeAnimatorFromExternalWait();
            ChangeState(BossState.Idle);
        }
        else
        {
            // Animator ControllerのデフォルトIdleが勝手に流れるのを止める
            PauseAnimatorForExternalWait();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < activeBalls.Count; i++)
        {
            if (activeBalls[i] != null)
                activeBalls[i].OnBallFinished -= HandleBallFinished;
        }

        activeBalls.Clear();
    }

    private void Update()
    {
        float dt = ScaledDeltaTime;

        localTime += dt;

        if (state == BossState.Dead)
        {
            UpdateDead(dt);
            return;
        }

        // 開始待ち中は、攻撃・回転・状態更新を進めない
        if (!bossStarted)
        {
            if (playIdleWhileWaitingStart)
            {
                ResumeAnimatorFromExternalWait();
                EnsureIdleAnimationPlaying();
            }
            else
            {
                PauseAnimatorForExternalWait();
            }

            return;
        }

        UpdatePlayerVelocity(dt);

        stateTimer += dt;

        switch (state)
        {
            case BossState.Idle:
                UpdateIdle();
                break;

            case BossState.KickArc:
                UpdateKickArc();
                break;

            case BossState.FreeKick:
                UpdateFreeKick();
                break;

            case BossState.OverheadKick:
                UpdateOverheadKick();
                break;

            case BossState.Slide:
                UpdateSlide(dt);
                break;

            case BossState.ReflectedHit:
                UpdateReflectedHit();
                break;

            case BossState.Anger:
                UpdateAngerState();
                break;

            case BossState.Roar:
                UpdateRoar();
                break;

            case BossState.Stunned:
                UpdateStunned();
                break;
        }
    }

    #endregion

    #region State Machine

    private void ChangeState(BossState next)
    {
        ExitState(state);

        state = next;
        stateTimer = 0f;
        animationEventFinished = false;

        if (logState)
            Debug.Log($"{name} SoccerBoss State => {next}");

        EnterState(next);
    }

    private void EnterState(BossState next)
    {
        switch (next)
        {
            case BossState.Idle:
                EnterIdle();
                break;

            case BossState.KickArc:
                EnterKickArc();
                break;

            case BossState.FreeKick:
                EnterFreeKick();
                break;

            case BossState.OverheadKick:
                EnterOverheadKick();
                break;

            case BossState.Slide:
                EnterSlide();
                break;

            case BossState.ReflectedHit:
                EnterReflectedHit();
                break;

            case BossState.Anger:
                EnterAnger();
                break;

            case BossState.Roar:
                EnterRoar();
                break;

            case BossState.Stunned:
                EnterStunned();
                break;

            case BossState.Dead:
                EnterDead();
                break;
        }
    }

    private void ExitState(BossState oldState)
    {
        switch (oldState)
        {
            case BossState.Slide:
                slidePhase = SlidePhase.None;

                if (slideParam.slideHitbox != null)
                    slideParam.slideHitbox.SetActiveHit(false);

                SetSlideAttackHitboxActive(false);

                if (state != BossState.Dead)
                    SetFieldObjectHitboxesActive(true);

                RestoreSlideYOffsetImmediate();

                if (slidingEffect != null)
                {
                    slidingEffect.StopImmediate();
                    slidingEffect = null;
                }

                break;

            case BossState.Stunned:
                stunPhase = StunPhase.None;
                SetStunnedHitboxesActive(false);
                break;

            case BossState.ReflectedHit:
                SetStunnedHitboxesActive(false);
                break;

            case BossState.Roar:
                SetSlideAttackHitboxActive(false);
                SetStunnedHitboxesActive(false);
                break;

            case BossState.Dead:
                deadPhase = DeadPhase.None;
                break;
        }
    }

    private void EnterIdle()
    {
        ApplyNormalHitboxMode();

        FacePlayer();

        PlayIdle();
    }

    private void EnterKickArc()
    {
        ApplyNormalHitboxMode();

        spawnedBallsThisState = false;

        if (stageParam.snapToAnchorBeforeKick)
            SnapToCurrentAnchor();

        FacePlayer();
        PlayKickArc();

        if (!animationEventParam.spawnKickArcByAnimationEvent)
            AnimEvent_SpawnKickArcBalls();
    }

    private void EnterFreeKick()
    {
        ApplyNormalHitboxMode();

        spawnedBallsThisState = false;

        if (stageParam.snapToAnchorBeforeKick)
            SnapToCurrentAnchor();

        FacePlayer();

        PlayFreeKick();

        if (!animationEventParam.spawnFreeKickByAnimationEvent)
            AnimEvent_SpawnFreeKickBalls();
    }

    private void EnterOverheadKick()
    {
        ApplyNormalHitboxMode();

        spawnedBallsThisState = false;

        if (stageParam.snapToAnchorBeforeKick)
            SnapToCurrentAnchor();

        FacePlayer();
        PlayOverheadKick();

        if (!animationEventParam.spawnOverheadByAnimationEvent)
            AnimEvent_SpawnOverheadBalls();
    }

    private void EnterSlide()
    {
        SetStunnedHitboxesActive(false);
        spawnedBallsThisState = false;

        PrepareSlidePath();

        slidePhase = SlidePhase.ReadyAnimation;
        slidePhaseTimer = 0f;

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        // Entryモーションは開始辺基準の向きにする
        ApplySlideEntryRotation();

        PlaySlideReady();
    }

    private void EnterReflectedHit()
    {
        ApplyNormalHitboxMode();

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        animationEventFinished = false;

        PlayReflectedHit();
    }

    private void EnterAnger()
    {
        ApplyNormalHitboxMode();

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        SetFieldObjectHitboxesActive(false);
        SetSlideAttackHitboxActive(false);
        SetStunnedHitboxesActive(false);

        FacePlayer();

        audioManager.PlaySe("SB_Angry", statusParam.angerEffectPoint.position);

        if (stuneffect != null)
        {
            stuneffect.StopImmediate();
            stuneffect = null;
        }

        fieldObjectsExplodedThisAnger = false;

        if (angerExplosionParam.explodeAllBallsOnAngerEnter)
            ExplodeAllActiveBallsForAnger();

        PlayAnger();
    }

    private void EnterRoar()
    {
        ApplyNormalHitboxMode();

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        SetFieldObjectHitboxesActive(false);
        SetSlideAttackHitboxActive(false);
        SetStunnedHitboxesActive(false);

        FacePlayer();

        audioManager.PlaySe("SB_Angry", statusParam.angerEffectPoint.position);

        if (stuneffect != null)
        {
            stuneffect.StopImmediate();
            stuneffect = null;
        }

        animationEventFinished = false;
        fieldObjectsExplodedThisRoar = false;

        // 怒り咆哮でも画面内のボールを消したい場合は流用
        if (angerExplosionParam.explodeAllBallsOnAngerEnter)
            ExplodeAllActiveBallsForAnger();

        PlayRoar();
    }
    private void EnterDead()
    {
        SetFieldObjectHitboxesActive(false);
        SetStunnedHitboxesActive(false);
        SetSlideAttackHitboxActive(false);

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        ForceFinishAllBalls();

        hitMaterialFlashPlayer?.StopFlash(true);

        if (stuneffect != null)
        {
            stuneffect.StopImmediate();
            stuneffect = null;
        }

        TimeUIManager.Instance.SetCountDownStart(false);

        //GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 0.4f);

        InputSystem.actions.FindActionMap("Player").Disable();

        statusParam.cameraManager.BeginManualCut("Boss");

        stunPhase = StunPhase.None;

        //deathRegistered = false;

        deadPhase = DeadPhase.FallAnimation;
        deadPhaseTimer = 0f;
        animationEventFinished = false;

        if (dyingEffectParam.resetGrowOnEnterDead)
        {
            ResetDyingEffectRuntime();
        }

        PlayDeadFall();
    }
    private void InitializeBossPosition()
    {
        if (stageParam.autoCaptureAnchorOffsetFromInitialTransform)
        {
            CaptureAnchorOffsetFromCurrentTransform();
        }

        if (stageParam.snapToNearestAnchorOnStart)
        {
            currentAnchorSide = FindNearestAnchorSide(transform.position);
            SnapToCurrentAnchor();
        }
    }

    public void StartBoss()
    {
        if (!initialized)
        {
            InitializeBossPosition();
            initialized = true;
        }

        if (bossStarted)
            return;

        bossStarted = true;

        ResumeAnimatorFromExternalWait();

        stateTimer = 0f;
        animationEventFinished = false;

        ChangeState(BossState.Idle);

        if (logState)
            Debug.Log($"{name} Boss Started");
    }

    #endregion

    #region State Updates

    private void UpdateIdle()
    {
        FacePlayer();

        EnsureIdleAnimationPlaying();

        if (HasActiveBalls())
        {
            stateTimer = 0f;
            return;
        }

        // StunGetUpから戻ってきた場合など、予約された死亡/怒りをここで処理する
        if (pendingDeathAfterStunSafePoint || hp <= 0f)
        {
            pendingDeathAfterStunSafePoint = false;
            pendingAngerAfterStunSafePoint = false;

            EnterDeathByDamage();
            return;
        }

        if (pendingAngerAfterStunSafePoint)
        {
            pendingAngerAfterStunSafePoint = false;

            TryEnterAngry();
            return;
        }

        if (TryEnterAngry())
            return;

        if (stunGauge >= GetCurrentStunGaugeMax() &&
            state != BossState.Stunned)
        {
            ChangeState(BossState.Stunned);
            return;
        }

        float wait = isAngry
            ? timingParam.angryIdleBeforeAttack
            : timingParam.idleBeforeAttack;

        if (stateTimer < wait)
            return;

        SelectNextAttack();
    }

    private void UpdateKickArc()
    {
        FacePlayer();

        bool animFinished = IsStateAnimationFinished(
            animationParam.kickArc,
            bossAnimationParam.kickArc
        );

        if (!animFinished)
            return;

        // ボールが残っていても、キックモーション自体が終わったらIdleへ戻る
        ChangeState(BossState.Idle);
    }

    private void UpdateFreeKick()
    {
        FacePlayer();

        bool animFinished = IsStateAnimationFinished(
            animationParam.freeKick,
            bossAnimationParam.freeKick
        );

        if (!animFinished)
            return;

        ChangeState(BossState.Idle);
    }
    private void UpdateOverheadKick()
    {
        FacePlayer();

        bool animFinished = IsStateAnimationFinished(
            animationParam.overheadKick,
            bossAnimationParam.overheadKick
        );

        if (!animFinished)
            return;

        // ボールが残っていても、キックモーション自体が終わったらIdleへ戻る
        ChangeState(BossState.Idle);
    }

    private void UpdateReflectedHit()
    {
        FacePlayer();

        bool finished = IsStateAnimationFinished(
            animationParam.reflectedHit,
            bossAnimationParam.reflectedHit
        );

        if (!finished)
            return;

        ChangeState(BossState.Idle);
    }

    private void UpdateAngerState()
    {
        FacePlayer();

        StateAnimationFinishParam finishParam = angerEnteredFromStunned
            ? animationParam.stunnedAnger
            : animationParam.anger;

        string stateName = angerEnteredFromStunned
            ? bossAnimationParam.stunnedAnger
            : bossAnimationParam.anger;

        bool finished = IsStateAnimationFinished(
            finishParam,
            stateName
        );

        if (!finished)
            return;

        angerEnteredFromStunned = false;

        ChangeState(BossState.Idle);
    }

    private void UpdateRoar()
    {
        FacePlayer();

        bool finished = IsStateAnimationFinished(
            animationParam.roar,
            bossAnimationParam.roar
        );

        if (!finished)
            return;

        if (attackSelectParam.forceAttackAfterRoarToKick)
            forceNextAttackKickOnly = true;

        ChangeState(BossState.Idle);
    }
    private void UpdateDead(float dt)
    {
        deadPhaseTimer += dt;

        switch (deadPhase)
        {
            case DeadPhase.FallAnimation:
                UpdateDeadFall();
                break;

            case DeadPhase.BurstAnimation:
                UpdateDeathBurst();
                break;

            case DeadPhase.Finished:
                // 死亡処理完了後は何もしない
                break;
        }
    }

    private void UpdateDeadFall()
    {
        StateAnimationFinishParam finishParam = deathEnteredFromStunned
            ? animationParam.downDeath
            : animationParam.deadFall;

        string stateName = deathEnteredFromStunned
            ? bossAnimationParam.downDeath
            : bossAnimationParam.deadFall;

        bool finished = IsStateAnimationFinished(
            finishParam,
            deadPhaseTimer,
            animationEventFinished,
            stateName
        );

        if (!finished)
            return;

        deathEnteredFromStunned = false;

        TimeUIManager.Instance.SetCountDownStart(false);

        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 1.0f);

        BeginDeathBurst();
    }

    private void BeginDeathBurst()
    {
        animationEventFinished = false;
        deadPhaseTimer = 0f;
        deadPhase = DeadPhase.BurstAnimation;

        //effectPlayer.PlayAt(8 , kickParam.ballSpawnPoint.position);

        PlayDeathBurst();
    }

    private void UpdateDeathBurst()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.deathBurst,
            deadPhaseTimer,
            animationEventFinished,
            bossAnimationParam.deadLoop
        );

        if (!finished)
            return;

        FinishDeath();
    }

    private void FinishDeath()
    {
        //if (!deathRegistered)
        //{
        //    deathRegistered = true;

        //    if (MissionManager.Instance != null)
        //        MissionManager.Instance.AddKill(statusParam.enemyData);
        //}

        deadPhase = DeadPhase.Finished;
        deadPhaseTimer = 0f;
        animationEventFinished = false;
    }

    #endregion

    #region AttackSelction

    private void SelectNextAttack()
    {
        AttackPatternParam[] table = isAngry
            ? attackSelectParam.angryAttacks
            : attackSelectParam.normalAttacks;

        bool isFirstAttackInCurrentPhase = isAngry
            ? !hasSelectedAngryAttack
            : !hasSelectedNormalAttack;

        bool kickOnly =
            forceNextAttackKickOnly ||
            (
                attackSelectParam.forceFirstAttackOfEachPhaseToKick &&
                isFirstAttackInCurrentPhase
            );

        if (!TrySelectAttack(table, out BossAttackType selected, kickOnly))
        {
            // 保険。特殊ルールや設定ミスで候補が消えた場合はKick系へ
            selected = GetFallbackKickAttack(table);
        }

        RegisterSelectedAttack(selected);

        if (isAngry)
            hasSelectedAngryAttack = true;
        else
            hasSelectedNormalAttack = true;

        forceNextAttackKickOnly = false;

        ChangeState(ToBossState(selected));
    }

    private bool TrySelectAttack(
    AttackPatternParam[] table,
    out BossAttackType selected,
    bool kickOnly = false)
{
    selected = BossAttackType.Punt;

    if (table == null || table.Length == 0)
        return false;

    float totalChance = GetTotalChance(
        table,
        applyConsecutiveFilter: true,
        kickOnly: kickOnly
    );

    // 連続制限で候補が全部消えた場合は、連続制限だけ無視して再抽選
    bool ignoreConsecutiveLimit = false;

    if (totalChance <= 0f)
    {
        totalChance = GetTotalChance(
            table,
            applyConsecutiveFilter: false,
            kickOnly: kickOnly
        );

        ignoreConsecutiveLimit = true;
    }

    if (totalChance <= 0f)
        return false;

    if (attackSelectParam.warnIfTotalChanceIsNot100)
    {
        float rawTotal = GetRawTotalChance(table);

        if (Mathf.Abs(rawTotal - 100f) > 0.01f)
        {
            Debug.LogWarning(
                $"{name}: Attack chance total is {rawTotal:F1}. " +
                "Inspector上では合計100になるように設定してください。"
            );
        }
    }

    float r = Random.Range(0f, totalChance);
    float current = 0f;

    for (int i = 0; i < table.Length; i++)
    {
        AttackPatternParam param = table[i];

        if (param == null)
            continue;

        if (param.chancePercent <= 0f)
            continue;

        if (!IsAttackSelectable(param.attackType, kickOnly))
            continue;

        if (!ignoreConsecutiveLimit &&
            IsBlockedByConsecutiveLimit(param))
        {
            continue;
        }

        current += param.chancePercent;

        if (r <= current)
        {
            selected = param.attackType;

            if (attackSelectParam.logAttackSelect)
            {
                Debug.Log(
                    $"{name} SelectAttack: {selected} " +
                    $"angry:{isAngry} total:{totalChance:F1} kickOnly:{kickOnly}"
                );
            }

            return true;
        }
    }

    return false;
}

    private float GetRawTotalChance(AttackPatternParam[] table)
    {
        if (table == null)
            return 0f;

        float total = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            if (table[i] == null)
                continue;

            total += Mathf.Max(0f, table[i].chancePercent);
        }

        return total;
    }

    private float GetTotalChance(
    AttackPatternParam[] table,
    bool applyConsecutiveFilter,
    bool kickOnly = false)
    {
        if (table == null)
            return 0f;

        float total = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            AttackPatternParam param = table[i];

            if (param == null)
                continue;

            if (param.chancePercent <= 0f)
                continue;

            if (!IsAttackSelectable(param.attackType, kickOnly))
                continue;

            if (applyConsecutiveFilter &&
                IsBlockedByConsecutiveLimit(param))
            {
                continue;
            }

            total += param.chancePercent;
        }

        return total;
    }

    private bool IsBlockedByConsecutiveLimit(AttackPatternParam param)
    {
        if (param == null)
            return true;

        if (!param.useConsecutiveLimit)
            return false;

        if (!hasLastAttackType)
            return false;

        if (lastAttackType != param.attackType)
            return false;

        int maxCount = Mathf.Max(1, param.maxConsecutiveCount);

        return consecutiveAttackCount >= maxCount;
    }

    private bool IsAttackSelectable(BossAttackType attackType, bool kickOnly)
    {
        if (kickOnly && !IsKickAttack(attackType))
        {
            if (attackSelectParam.logAttackFilter)
            {
                Debug.Log(
                    $"{name}: Attack filtered by kickOnly. type:{attackType}"
                );
            }

            return false;
        }

        if (attackType == BossAttackType.Roar &&
            attackSelectParam.disableRoarWhenNoFieldObject &&
            !FieldObjectAngerExplosionTarget.HasAvailableTarget)
        {
            if (attackSelectParam.logAttackFilter)
            {
                Debug.Log(
                    $"{name}: Roar filtered because FieldObject count is 0."
                );
            }

            return false;
        }

        return true;
    }

    private bool IsKickAttack(BossAttackType attackType)
    {
        switch (attackType)
        {
            case BossAttackType.Punt:
            case BossAttackType.FreeKick:
            case BossAttackType.Overhead:
                return true;
        }

        return false;
    }

    private BossAttackType GetFallbackKickAttack(AttackPatternParam[] table)
    {
        if (TrySelectFallbackKickFromTable(table, out BossAttackType selected))
            return selected;

        return BossAttackType.Punt;
    }

    private bool TrySelectFallbackKickFromTable(
        AttackPatternParam[] table,
        out BossAttackType selected)
    {
        selected = BossAttackType.Punt;

        if (table == null || table.Length == 0)
            return false;

        float total = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            AttackPatternParam param = table[i];

            if (param == null)
                continue;

            if (param.chancePercent <= 0f)
                continue;

            if (!IsKickAttack(param.attackType))
                continue;

            total += param.chancePercent;
        }

        if (total <= 0f)
            return false;

        float r = Random.Range(0f, total);
        float current = 0f;

        for (int i = 0; i < table.Length; i++)
        {
            AttackPatternParam param = table[i];

            if (param == null)
                continue;

            if (param.chancePercent <= 0f)
                continue;

            if (!IsKickAttack(param.attackType))
                continue;

            current += param.chancePercent;

            if (r <= current)
            {
                selected = param.attackType;
                return true;
            }
        }

        return false;
    }
    private void RegisterSelectedAttack(BossAttackType selected)
    {
        if (hasLastAttackType && lastAttackType == selected)
        {
            consecutiveAttackCount++;
        }
        else
        {
            lastAttackType = selected;
            consecutiveAttackCount = 1;
            hasLastAttackType = true;
        }
    }
    private BossState ToBossState(BossAttackType attackType)
    {
        switch (attackType)
        {
            case BossAttackType.Punt:
                return BossState.KickArc;

            case BossAttackType.Slide:
                return BossState.Slide;

            case BossAttackType.Overhead:
                return BossState.OverheadKick;

            case BossAttackType.FreeKick:
                return BossState.FreeKick;

            case BossAttackType.Roar:
                return BossState.Roar;
        }

        return BossState.KickArc;
    }

    #endregion

    #region Animation Control

    private void PlayBossAnimation(
    string stateName,
    float blendTime,
    float speedRate = 1f,
    bool forceRestart = false)
    {
        if (animator == null)
            return;

        if (string.IsNullOrEmpty(stateName))
            return;

        int layer = bossAnimationParam.animatorLayerIndex;

        string resolvedStateName = ResolveAnimatorStateName(stateName, layer);

        if (string.IsNullOrEmpty(resolvedStateName))
        {
            if (bossAnimationParam.logAnimationPlayError)
            {
                Debug.LogWarning(
                    $"{name}: Animator State が見つかりません。" +
                    $" input:{stateName}, " +
                    $"fullPath:{bossAnimationParam.animatorLayerName}.{stateName}, " +
                    $"layer:{layer}"
                );
            }

            return;
        }

        if (forceRestart)
        {
            // 文字列版を使う。
            // int Hash版だとFullPathHashが必要なことがあり、State not foundになりやすい。
            animator.Play(resolvedStateName, layer, 0f);

            if (bossAnimationParam.updateAnimatorImmediatelyOnRestart)
                animator.Update(0f);

            return;
        }

        animator.CrossFadeInFixedTime(
            resolvedStateName,
            Mathf.Max(0f, blendTime),
            layer,
            0f
        );
    }

    private string ResolveAnimatorStateName(string stateName, int layer)
    {
        // まずは入力された名前そのままを確認
        int shortHash = Animator.StringToHash(stateName);

        if (animator.HasState(layer, shortHash))
            return stateName;

        if (!bossAnimationParam.tryFullPathStateName)
            return null;

        // 次に Base Layer.StateName を確認
        string fullPath = $"{bossAnimationParam.animatorLayerName}.{stateName}";
        int fullPathHash = Animator.StringToHash(fullPath);

        if (animator.HasState(layer, fullPathHash))
            return fullPath;

        return null;
    }
    private bool IsAnimatorStateName(AnimatorStateInfo info, string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
            return false;

        if (info.IsName(stateName))
            return true;

        string fullPath = $"{bossAnimationParam.animatorLayerName}.{stateName}";

        if (info.IsName(fullPath))
            return true;

        return false;
    }

    private bool IsBossAnimationFinished(
    string stateName,
    float endNormalizedTime = 0.95f)
    {
        if (animator == null)
            return true;

        if (string.IsNullOrEmpty(stateName))
            return true;

        int layer = bossAnimationParam.animatorLayerIndex;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);

        if (!IsAnimatorStateName(info, stateName))
        {
            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);

            if (!IsAnimatorStateName(nextInfo, stateName))
                return false;

            info = nextInfo;
        }

        return info.normalizedTime >= endNormalizedTime;
    }

    private bool IsStateAnimationFinished(
        StateAnimationFinishParam param,
        string stateName)
    {
        return IsStateAnimationFinished(
            param,
            stateTimer,
            animationEventFinished,
            stateName
        );
    }

    private bool IsStateAnimationFinished(
        StateAnimationFinishParam param,
        float timer,
        bool eventFinished,
        string stateName)
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
                return IsBossAnimationFinished(
                    stateName,
                    param.endNormalizedTime
                );
        }

        return false;
    }

    private void PlayIdle()
    {
        PlayBossAnimation(
            bossAnimationParam.idle,
            bossAnimationParam.idleBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartIdleOnEnter
        );
    }

    private void PlayKickArc()
    {
        PlayBossAnimation(
            bossAnimationParam.kickArc,
            bossAnimationParam.kickBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartKickAnimations
        );
    }

    private void PlayFreeKick()
    {
        PlayBossAnimation(
            bossAnimationParam.freeKick,
            bossAnimationParam.freeKickBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartFreeKickAnimations
        );
    }
    private void PlayOverheadKick()
    {
        PlayBossAnimation(
            bossAnimationParam.overheadKick,
            bossAnimationParam.overheadBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartKickAnimations
        );
    }

    private void PlaySlideReady()
    {
        string stateName = SelectDirectionalSlideAnimation(
            bossAnimationParam.slideReady,
            bossAnimationParam.slideReadyRight,
            bossAnimationParam.slideReadyLeft,
            slideParam.useDirectionalReady
        );

        PlayBossAnimation(
            stateName,
            bossAnimationParam.slideReadyBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartSlideAnimations
        );
    }

    private void PlaySlideCharge()
    {
        string stateName = SelectDirectionalSlideAnimation(
            bossAnimationParam.slideCharge,
            bossAnimationParam.slideChargeRight,
            bossAnimationParam.slideChargeLeft,
            slideParam.useDirectionalCharge
        );

        PlayBossAnimation(
            stateName,
            bossAnimationParam.slideChargeBlendTime,
            1f,
            forceRestart: false
        );
    }

    private void PlaySlideJumpIn()
    {
        string stateName = SelectDirectionalSlideAnimation(
            bossAnimationParam.slideJumpIn,
            bossAnimationParam.slideJumpInRight,
            bossAnimationParam.slideJumpInLeft,
            slideParam.useDirectionalJumpUp
        );

        PlayBossAnimation(
            stateName,
            bossAnimationParam.slideJumpInBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartSlideAnimations
        );
    }

    private void PlaySlideLoop()
    {
        PlayBossAnimation(
            bossAnimationParam.slideLoop,
            bossAnimationParam.slideLoopBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartSlideAnimations
        );
    }

    private void PlaySlideJumpOut()
    {
        PlayBossAnimation(
            bossAnimationParam.slideJumpOut,
            bossAnimationParam.slideJumpOutBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartSlideAnimations
        );
    }

    private void PlaySlideLanding()
    {
        PlayBossAnimation(
            bossAnimationParam.slideLanding,
            bossAnimationParam.slideLandingBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartSlideAnimations
        );
    }

    private void PlayStunDown()
    {
        PlayBossAnimation(
            bossAnimationParam.stunDown,
            bossAnimationParam.stunDownBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartStunAnimations
        );
    }

    private void PlayStunLoop()
    {
        PlayBossAnimation(
            bossAnimationParam.stunLoop,
            bossAnimationParam.stunLoopBlendTime,
            1f,
            forceRestart: false
        );
    }

    private void PlayStunGetUp()
    {
        PlayBossAnimation(
            bossAnimationParam.stunGetUp,
            bossAnimationParam.stunGetUpBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartStunAnimations
        );
    }

    private void PlayReflectedHit()
    {
        PlayBossAnimation(
            bossAnimationParam.reflectedHit,
            bossAnimationParam.reflectedHitBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartReflectedHitAnimation
        );
    }

    private void PlayAnger()
    {
        string stateName = angerEnteredFromStunned
            ? bossAnimationParam.stunnedAnger
            : bossAnimationParam.anger;

        float blendTime = angerEnteredFromStunned
            ? bossAnimationParam.stunnedAngerBlendTime
            : bossAnimationParam.angerBlendTime;

        bool forceRestart = angerEnteredFromStunned
            ? bossAnimationParam.forceRestartStunnedAngerAnimation
            : bossAnimationParam.forceRestartAngerAnimation;

        PlayBossAnimation(
            stateName,
            blendTime,
            1f,
            forceRestart: forceRestart
        );
    }

    private void PlayRoar()
    {
        PlayBossAnimation(
            bossAnimationParam.roar,
            bossAnimationParam.roarBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartRoarAnimation
        );
    }
    private void PlayDeadFall()
    {
        string stateName = deathEnteredFromStunned
            ? bossAnimationParam.downDeath
            : bossAnimationParam.deadFall;

        float blendTime = deathEnteredFromStunned
            ? bossAnimationParam.downDeathBlendTime
            : bossAnimationParam.deadFallBlendTime;

        bool forceRestart = deathEnteredFromStunned
            ? bossAnimationParam.forceRestartDownDeathAnimation
            : bossAnimationParam.forceRestartDeadAnimations;

        PlayBossAnimation(
            stateName,
            blendTime,
            1f,
            forceRestart: forceRestart
        );
    }

    private void PlayDeathBurst()
    {
        PlayBossAnimation(
            bossAnimationParam.deadLoop,
            bossAnimationParam.deadLoopBlendTime,
            1f,
            forceRestart: bossAnimationParam.forceRestartDeadAnimations
        );
    }

    private void EnsureIdleAnimationPlaying()
    {
        if (!bossAnimationParam.keepIdleStateAlive)
            return;

        if (animator == null)
            return;

        if (string.IsNullOrEmpty(bossAnimationParam.idle))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        bool isIdle = IsAnimatorStateName(info, bossAnimationParam.idle);

        if (!isIdle && !animator.IsInTransition(0))
        {
            PlayIdle();
            return;
        }

        if (isIdle &&
            bossAnimationParam.restartIdleWhenFinished &&
            info.normalizedTime >= 0.98f)
        {
            PlayIdle();
        }
    }

    private void PauseAnimatorForExternalWait()
    {
        if (animator == null)
            return;

        animator.speed = 0f;
    }

    private void ResumeAnimatorFromExternalWait()
    {
        if (animator == null)
            return;

        animator.speed = 1f;
    }

    private string SelectDirectionalSlideAnimation(
    string center,
    string right,
    string left,
    bool useDirectional)
    {
        if (!useDirectional)
            return center;

        switch (currentSlideEntryDirection)
        {
            case SlideEntryDirection.Right:
                if (!string.IsNullOrEmpty(right))
                    return right;
                break;

            case SlideEntryDirection.Left:
                if (!string.IsNullOrEmpty(left))
                    return left;
                break;
        }

        return center;
    }

    #endregion

    #region Animation Events

    /// <summary>
    /// KickArcアニメーションの「ボールを蹴る瞬間」にAnimation Eventで呼ぶ。
    /// </summary>
    public void AnimEvent_SpawnKickArcBalls()
    {
        if (state != BossState.KickArc)
            return;

        if (spawnedBallsThisState)
            return;

        audioManager.PlaySe("SB_Kick");

        spawnedBallsThisState = true;
        SpawnArcBalls();
    }

    public void AnimEvent_SpawnFreeKickBalls()
    {
        if (state != BossState.FreeKick)
            return;

        if (spawnedBallsThisState)
            return;

        audioManager.PlaySe("SB_Kick");

        spawnedBallsThisState = true;
        SpawnArcBalls(freeKickParam);
    }

    /// <summary>
    /// OverheadKickアニメーションの「ボールを蹴る瞬間」にAnimation Eventで呼ぶ。
    /// </summary>
    public void AnimEvent_SpawnOverheadBalls()
    {
        if (state != BossState.OverheadKick)
            return;

        if (spawnedBallsThisState)
            return;

        audioManager.PlaySe("SB_Kick");

        spawnedBallsThisState = true;
        SpawnStraightBalls();
    }

    //BossEffects 
    public void AnimEvent_SmokeEffectPlay()
    {
        if(effectPlayer ==null)
            return;

        if (state != BossState.Stunned)
            return;

        audioManager.PlaySe("RB_Down");

        effectPlayer.PlayAt(0, stunPoseParam.BossHeadPoint.position);
    }

    public void AnimEvent_PuntChargeEffectPlay()
    {
        if (effectPlayer == null)
            return;

        if (state != BossState.OverheadKick)
        {
            effectPlayer.PlayAt(2, kickParam.ballSpawnPoint.position);
        }
        else
        {
            effectPlayer.PlayAt(12, kickParam.ballSpawnPoint.position);
        }
        
        audioManager.PlaySe("SB_KickCharge");
    }

    public void AnimEvent_AngerEffectPlay()
    {
        if (effectPlayer == null)
            return;

        //Vector3 dir = GetStageCenterRaw() - transform.position;
        Vector3 dir = playerTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized);

        effectPlayer.PlayAt(10 , statusParam.angerEffectPoint.position, rot);
    }

    public void AnimEvent_ExplodeFieldObjectsForAnger()
    {
        if (state != BossState.Anger &&
            state != BossState.Roar)
        {
            return;
        }

        if (!angerExplosionParam.explodeFieldObjectsByAnimationEvent)
            return;

        if (angerExplosionParam.explodeFieldObjectsOnlyOncePerAnger)
        {
            if (state == BossState.Anger && fieldObjectsExplodedThisAnger)
                return;

            if (state == BossState.Roar && fieldObjectsExplodedThisRoar)
                return;
        }

        if (state == BossState.Anger)
            fieldObjectsExplodedThisAnger = true;
        else if (state == BossState.Roar)
            fieldObjectsExplodedThisRoar = true;

        Quaternion rot = Quaternion.Euler(
            angerExplosionParam.fieldObjectExplosionRotationEuler
        );

        FieldObjectAngerExplosionTarget.ExplodeAll(
            boss: this,
            effectIndex: angerExplosionParam.fieldObjectExplosionEffectIndex,
            effectRotation: rot,
            effectScale: angerExplosionParam.fieldObjectExplosionScale,
            explosionHitboxPrefab: angerExplosionParam.fieldObjectExplosionHitboxPrefab,
            explosionHitboxLifeTime: angerExplosionParam.fieldObjectExplosionHitboxLifeTime,
            explosionDamage: angerExplosionParam.fieldObjectExplosionDamage,
            explosionTargetLayer: angerExplosionParam.fieldObjectExplosionTargetLayer,
            hideRenderers: angerExplosionParam.hideFieldObjectRenderers,
            disableColliders: angerExplosionParam.disableFieldObjectColliders,
            destroyAfterExplosion: angerExplosionParam.destroyFieldObjectAfterExplosion,
            destroyDelay: angerExplosionParam.destroyFieldObjectDelay
        );

        if (angerExplosionParam.logAngerExplosion)
        {
            Debug.Log($"{name}: {state} animation event exploded FieldObjects.");
        }
    }

    public void AnimEvent_DyingEffect()
    {
        PlayDyingEffectGrow();
    }
    public void AnimEvent_BossExplosion()
    {
        effectBonePlayer.StopAll();

        audioManager.StopSe("SB_MiniExplosion");

        audioManager.PlaySe("SB_Explosion");

        effectPlayer.PlayAt(8, kickParam.ballSpawnPoint.position);
    }

    public void AnimEvent_Confetti()
    {
        effectPlayer.PlayAt(11 , kickParam.ballSpawnPoint.position);
    }
    public void AnimEvent_SlowForFallDown()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 0.5f);
    }
    public void AnimEvent_SlowForDeathBoom()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 0.6f);
    }

    public void AnimEvent_SlowReset()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 1.5f);
    }

    public void AnimEvent_WhereBossDies()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.AddKill(statusParam.enemyData);
    }

    /// <summary>
    /// AnimationFinishMode.AnimationEvent用。
    /// 任意のアニメーション終了地点で呼ぶ。
    /// </summary>
    public void AnimEvent_FinishState()
    {
        NotifyAnimationEventFinished();
    }

    public void NotifyAnimationEventFinished()
    {
        animationEventFinished = true;
    }

    #endregion

    #region Kick / Ball Spawn

    private void SpawnArcBalls()
    {
        SpawnArcBalls(kickParam);
    }

    private void SpawnArcBalls(KickParam param)
    {
        if (param == null)
            return;

        if (param.ballPrefab == null)
            return;

        Transform spawn = param.ballSpawnPoint != null
            ? param.ballSpawnPoint
            : transform;

        int count = Mathf.Max(1, param.ballCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 target;

            if (i == 0)
                target = GetPlayerNearLandingTarget(param);
            else
                target = GetRandomStageLandingTarget(param.randomTargetMargin);

            target.y = GetStageTopY() + param.landingY;

            SoccerBossBall ball = Instantiate(
                param.ballPrefab,
                spawn.position,
                Quaternion.identity
            );

            RegisterActiveBall(ball);
            ApplyReflectTargetsToBall(ball);

            ball.LaunchArc(
                boss: this,
                bossTarget: transform,
                start: spawn.position,
                landingTarget: target,
                flightTime: param.arcFlightTime,
                gravity: param.gravity,
                arcHeight: param.arcHeight,
                playerDamage: param.playerDamage,
                reflectedDamageToBoss: statusParam.reflectedBallDamage,
                reflectedStunValue: statusParam.reflectedBallStunValue
            );
        }
    }

    private void SpawnStraightBalls()
    {
        if (overheadKickParam.ballPrefab == null)
            return;

        Transform spawn = overheadKickParam.ballSpawnPoint != null
            ? overheadKickParam.ballSpawnPoint
            : transform;

        int count = Mathf.Max(1, overheadKickParam.ballCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 target;

            if (i == 0)
            {
                target = GetPredictedPlayerPosition(overheadKickParam.targetLeadTime);

                Vector2 random =
                    Random.insideUnitCircle * overheadKickParam.playerAimRandomRadius;

                target.x += random.x;
                target.z += random.y;
                target = ClampToStage(target);
            }
            else
            {
                target = GetRandomStageLandingTarget(
                    overheadKickParam.randomTargetMargin
                );
            }

            target.y = GetStageTopY() + overheadKickParam.targetHeightFromStage;

            Vector3 markerPos = target;
            markerPos.y = GetStageTopY() + 0.03f;

            SoccerBossBall ball = Instantiate(
                overheadKickParam.ballPrefab,
                spawn.position,
                Quaternion.identity
            );

            RegisterActiveBall(ball);
            ApplyReflectTargetsToBall(ball);

            ball.LaunchStraightToTarget(
                boss: this,
                bossTarget: transform,
                start: spawn.position,
                target: target,
                speed: overheadKickParam.speed,
                lifeTime: overheadKickParam.lifeTime,
                playerDamage: overheadKickParam.playerDamage,
                reflectedDamageToBoss: statusParam.reflectedBallDamage,
                reflectedStunValue: statusParam.reflectedBallStunValue
            );
        }
    }

    private void ApplyReflectTargetsToBall(SoccerBossBall ball)
    {
        if (ball == null)
            return;

        // SoccerBossBall側に SetReflectAimTargets(Transform[] targets) を追加しておく
        ball.SetReflectAimTargets(reflectTargetParam.aimPoints);
    }

    private void RegisterActiveBall(SoccerBossBall ball)
    {
        if (ball == null)
            return;

        if (!activeBalls.Contains(ball))
            activeBalls.Add(ball);

        ball.OnBallFinished += HandleBallFinished;
    }

    private void HandleBallFinished(
        SoccerBossBall ball,
        SoccerBossBall.BallEndReason reason)
    {
        if (ball != null)
            ball.OnBallFinished -= HandleBallFinished;

        activeBalls.Remove(ball);
    }

    private bool HasActiveBalls()
    {
        activeBalls.RemoveAll(b => b == null);
        return activeBalls.Count > 0;
    }

    private void ForceFinishAllBalls()
    {
        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            if (activeBalls[i] != null)
            {
                activeBalls[i].OnBallFinished -= HandleBallFinished;
                activeBalls[i].ForceFinish();
            }
        }

        activeBalls.Clear();
    }

    #endregion

    #region Slide

    private void PrepareSlidePath()
    {
        slideStartSide = FindNearestStageEdgeSide(transform.position);
        slideEndSide = SelectRandomOtherSide(slideStartSide);

        slideStart = GetSlidePathPoint(slideStartSide);
        slideEnd = GetSlidePathPoint(slideEndSide);

        slideDirection = slideEnd - slideStart;
        slideDirection.y = 0f;

        if (slideDirection.sqrMagnitude < 0.0001f)
            slideDirection = GetStageCenterRaw() - transform.position;

        slideDirection.y = 0f;

        if (slideDirection.sqrMagnitude < 0.0001f)
            slideDirection = transform.forward;

        slideDirection.Normalize();
        slideMoveRotation = Quaternion.LookRotation(slideDirection, Vector3.up);

        // Ready / Charge / JumpIn は、開始辺からステージ内側を向く
        GetSideBasis(
            slideStartSide,
            out Vector3 startOutward,
            out _
        );

        Vector3 entryForward = -startOutward;
        entryForward.y = 0f;

        if (entryForward.sqrMagnitude < 0.0001f)
            entryForward = transform.forward;

        slideEntryRotation = Quaternion.LookRotation(entryForward.normalized, Vector3.up);

        ResolveSlideEntryDirection();
    }

    private void ResolveSlideEntryDirection()
    {
        currentSlideEntryDirection = SlideEntryDirection.Center;

        if (!slideParam.selectEntryAnimationByDirection)
            return;

        GetSideBasis(
            slideStartSide,
            out Vector3 outward,
            out _
        );

        // 開始辺から見たステージ内側方向を「正面」とする
        Vector3 forwardToStage = -outward;
        forwardToStage.y = 0f;

        if (forwardToStage.sqrMagnitude < 0.0001f)
            return;

        forwardToStage.Normalize();

        // 開始辺から見た右方向
        Vector3 right = Vector3.Cross(Vector3.up, forwardToStage).normalized;

        Vector3 moveDir = slideEnd - slideStart;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude < 0.0001f)
            return;

        moveDir.Normalize();

        float sideDot = Vector3.Dot(moveDir, right);

        if (slideParam.invertEntryRightLeft)
            sideDot *= -1f;

        if (sideDot > slideParam.sideEntryThreshold)
        {
            currentSlideEntryDirection = SlideEntryDirection.Right;
        }
        else if (sideDot < -slideParam.sideEntryThreshold)
        {
            currentSlideEntryDirection = SlideEntryDirection.Left;
        }
        else
        {
            currentSlideEntryDirection = SlideEntryDirection.Center;
        }

        if (slideParam.logEntryDirection)
        {
            Debug.Log(
                $"{name} SlideEntryDirection:{currentSlideEntryDirection} " +
                $"startSide:{slideStartSide} endSide:{slideEndSide} " +
                $"sideDot:{sideDot:F2}"
            );
        }
    }
    private Vector3 GetSlidePathPoint(BossAnchorSide side)
    {
        Vector3 edgeCenter = GetStageEdgeCenterRaw(side);

        GetSideBasis(
            side,
            out Vector3 outward,
            out Vector3 tangent
        );

        Vector3 p =
            edgeCenter +
            tangent * slideParam.pathTangentOffset +
            outward * slideParam.pathOutsideOffset +
            Vector3.up * slideParam.pathYOffset;

        return p;
    }

    private void ApplySlideMoveRotation()
    {
        transform.rotation = slideMoveRotation;
    }

    private void ApplySlideEntryRotation()
    {
        transform.rotation = slideEntryRotation;

        if (slidingwarningEffect == null)
        {
            if (currentSlideEntryDirection == SlideEntryDirection.Center)
            {
                Vector3 size = Vector3.one;
                size.x = 1.48125f;
                size.y = 1.48125f;
                size.z = 1.48125f;

                slidingwarningEffect = effectPlayer.PlayAt(9, GetStageCenter(), slideMoveRotation, size);
            }
            else
            {
                Vector3 sliding = (slideStart  + slideEnd) * 0.5f;

                sliding.y = slideStart.y;

                slidingwarningEffect = effectPlayer.PlayAt(9, sliding, slideMoveRotation);
            }
        }
            
    }

    private void UpdateSlide(float dt)
    {
        slidePhaseTimer += dt;

        switch (slidePhase)
        {
            case SlidePhase.ReadyAnimation:
                UpdateSlideReady();
                break;

            case SlidePhase.ChargeLoop:
                UpdateSlideCharge();
                break;

            case SlidePhase.JumpInAnimation:
                UpdateSlideJumpIn(dt);
                break;

            case SlidePhase.LoopMove:
                UpdateSlideLoopMove(dt);
                break;

            case SlidePhase.JumpOutAnimation:
                UpdateSlideJumpOut(dt);
                break;

            case SlidePhase.LandingAnimation:
                UpdateSlideLandingAnimation();
                break;
        }
    }

    private void UpdateSlideReady()
    {
        string currentReadyStateName = SelectDirectionalSlideAnimation(
            bossAnimationParam.slideReady,
            bossAnimationParam.slideReadyRight,
            bossAnimationParam.slideReadyLeft,
            slideParam.useDirectionalReady
        );

        bool finished = IsStateAnimationFinished(
            animationParam.slideReady,
            slidePhaseTimer,
            animationEventFinished,
            currentReadyStateName
        );

        if (!finished)
            return;

        BeginSlideCharge();
    }

    private void BeginSlideCharge()
    {
        animationEventFinished = false;
        slidePhaseTimer = 0f;
        slidePhase = SlidePhase.ChargeLoop;

        audioManager.PlaySe("SB_SlidingCharge" , slideParam.slideChargeReferenceToeBone.position);

        ApplySlideEntryRotation();

        effectPlayer.PlayAt(4, slideParam.slideChargeReferenceToeBone.position);

        PlaySlideCharge();
    }

    private void UpdateSlideCharge()
    {
        ApplySlideEntryRotation();

        if (slidePhaseTimer < slideParam.chargeDuration)
            return;

        BeginSlideJumpIn();
    }

    private void BeginSlideJumpIn()
    {
        animationEventFinished = false;
        slidePhaseTimer = 0f;
        slideJumpInTimer = 0f;
        slidePhase = SlidePhase.JumpInAnimation;

        slideJumpInStart = transform.position;
        slideJumpInEnd = slideStart;

        BeginSlideYOffsetInterpolation();

        // JumpIn開始から通常受けを切り、スライド攻撃判定をON
        BeginSlideHitboxMode();

        ApplySlideEntryRotation();

        audioManager.PlaySe("SB_Sliding");

        if (effectPlayer != null)
        {
            EffectPlayParam param = EffectPlayParam.Default;

            // PlayAtで再生し、EffectInstance側のfollow処理には任せない
            param.overrideFollowTarget = true;
            param.followTarget = false;

            // EffectData側の位置・回転補正をできるだけ使わない
            param.overridePosition = true;
            param.positionOffset = Vector3.zero;

            param.overrideRotation = false;
            param.rotationOffset = Vector3.zero;

            // ScaleはEffectData側を使いたいので基本overrideしない
            param.overrideScale = false;

            effectPlayer.PlayAt(5, slideParam.slideEffectReferenceFootBone.position, slideParam.slideEffectReferenceFootBone.rotation);

            slidingEffect = effectPlayer.PlayAt(
                3,
                slideParam.slideEffectReferenceFootBone.position,
                slideParam.slideEffectReferenceFootBone.rotation,
                Vector3.one,
                param
                );
        }

        PlaySlideJumpIn();
    }

    private void UpdateSlideJumpIn(float dt)
    {
        slideJumpInTimer += dt;

        ApplySlideEntryRotation();

        UpdateSlideYOffsetInterpolation(dt);

        if (slideParam.moveJumpInXZToStartByScript)
        {
            UpdateJumpInXZCorrection(dt);
        }

        if (slidingEffect != null)
        {
            slidingEffect.transform.SetPositionAndRotation(slideParam.slideEffectReferenceFootBone.position, slideParam.slideEffectReferenceFootBone.rotation);
        }

        string currentJumpInStateName = SelectDirectionalSlideAnimation(
            bossAnimationParam.slideJumpIn,
            bossAnimationParam.slideJumpInRight,
            bossAnimationParam.slideJumpInLeft,
            slideParam.useDirectionalJumpUp
        );

        bool animationFinished = IsStateAnimationFinished(
            animationParam.slideJumpIn,
            slidePhaseTimer,
            animationEventFinished,
            currentJumpInStateName
        );

        if (!animationFinished)
            return;

        // 念のため、JumpIn終了時点で補正値を完全に目標値にする
        if (slideParam.useJumpInYOffsetInterpolation)
            ApplySlideYOffsetDelta(slideParam.jumpInYOffset);

        BeginSlideLoopMove();
    }

    private void BeginSlideLoopMove()
    {
        animationEventFinished = false;
        slidePhaseTimer = 0f;
        slidePhase = SlidePhase.LoopMove;

        // ここからはSliding本体なので進行方向を向く
        if (slideParam.faceSlideDirectionOnStartAndLoop)
            ApplySlideMoveRotation();

        if (slideParam.slideHitbox != null)
        {
            slideParam.slideHitbox.SetDamage(slideParam.damage);
            slideParam.slideHitbox.SetActiveHit(true);
        }

        // SlidingDを開始
        PlaySlideLoop();

        // SlidingDの0F姿勢を反映してから足補正する
        if (slideParam.updateAnimatorBeforeLoopFootCorrection &&
            animator != null)
        {
            animator.Update(0f);
        }

        // 回転 + SlidingD初期姿勢反映後に、右足を開始地点へ戻す
        CorrectSlideFootToStartAfterLoopEnter();

        Vector3 slideDelta = slideEnd - slideStart;

        if (!slideParam.alignFootY)
            slideDelta.y = 0f;

        if (slideParam.useFootReferenceDuringLoop &&
            slideParam.slideReferenceFootBone != null)
        {
            // ここからは右足をslideStart -> slideEndへ動かす
            slideFootLoopStart = slideStart;
            slideFootLoopEnd = slideEnd;
            slideFootCurrent = slideFootLoopStart;
        }
        else
        {
            // 足ボーンを使わない場合はRoot基準
            slideLoopRootStart = transform.position;
            slideLoopRootEnd = transform.position + slideDelta;
        }

        
    }

    private void UpdateSlideLoopMove(float dt)
    {
        if (slideParam.faceSlideDirectionOnStartAndLoop)
            ApplySlideMoveRotation();

        if(slidingEffect != null)
        {
            slidingEffect.transform.SetPositionAndRotation(slideParam.slideEffectReferenceFootBone.position, slideParam.slideEffectReferenceFootBone.rotation);
        }

        if (slideParam.useFootReferenceDuringLoop &&
            slideParam.slideReferenceFootBone != null)
        {
            slideFootCurrent = Vector3.MoveTowards(
                slideFootCurrent,
                slideFootLoopEnd,
                slideParam.speed * dt
            );

            MoveRootSoSlideReferenceMatches(slideFootCurrent);

            float footDistance = GetPlanarDistance(
                slideFootCurrent,
                slideFootLoopEnd
            );

            if (footDistance > slideParam.reachDistance)
                return;
        }
        else
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                slideLoopRootEnd,
                slideParam.speed * dt
            );

            float rootDistance = GetPlanarDistance(
                transform.position,
                slideLoopRootEnd
            );

            if (rootDistance > slideParam.reachDistance)
                return;
        }

        BeginSlideJumpOut();
    }

    private void BeginSlideJumpOut()
    {
        animationEventFinished = false;
        slidePhaseTimer = 0f;
        slideJumpOutTimer = 0f;
        slidePhase = SlidePhase.JumpOutAnimation;

        audioManager.StopSe("SB_Sliding");

        slideJumpOutStart = transform.position;
        slideJumpOutEnd = GetAnchorPosition(slideEndSide);

        if (slideYOffsetActive)
        {
            slideJumpOutEnd += Vector3.up * slideCurrentYOffset;
        }

        if (slidingwarningEffect != null)
        {
            slidingwarningEffect.StopImmediate();
            slidingwarningEffect = null;
        }

        currentAnchorSide = slideEndSide;

        Vector3 jumpDir = slideJumpOutEnd - slideJumpOutStart;
        jumpDir.y = 0f;

        if (slideParam.faceJumpOutDirection && jumpDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(jumpDir.normalized, Vector3.up);

        PlaySlideJumpOut();
    }

    private void UpdateSlideJumpOut(float dt)
    {
        slideJumpOutTimer += dt;

        float moveT = slideParam.jumpOutDuration <= 0f
            ? 1f
            : Mathf.Clamp01(slideJumpOutTimer / slideParam.jumpOutDuration);

        transform.position = EvaluateJumpPosition(
            slideJumpOutStart,
            slideJumpOutEnd,
            slideParam.jumpOutHeight,
            moveT
        );

        if (slidingEffect != null)
        {
            slidingEffect.transform.SetPositionAndRotation(slideParam.slideEffectReferenceFootBone.position, slideParam.slideEffectReferenceFootBone.rotation);
        }

        bool animationFinished = IsStateAnimationFinished(
            animationParam.slideJumpOut,
            slidePhaseTimer,
            animationEventFinished,
            bossAnimationParam.slideJumpOut
        );

        bool moveFinished = moveT >= 1f;

        if (!animationFinished || !moveFinished)
            return;

        transform.position = slideJumpOutEnd;

        RestoreSlideYOffsetImmediate();
        EndSlideHitboxMode();

        BeginSlideLandingAnimation();
    }

    private void BeginSlideLandingAnimation()
    {
        animationEventFinished = false;
        slidePhaseTimer = 0f;
        slidePhase = SlidePhase.LandingAnimation;

        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        if (slidingEffect != null)
        {
            slidingEffect.StopImmediate();
            slidingEffect = null;
        }

        if (slideParam.faceStageCenterOnLanding)
            FaceTo(GetStageCenterRaw());

        PlaySlideLanding();
    }

    private void UpdateSlideLandingAnimation()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.slideLanding,
            slidePhaseTimer,
            animationEventFinished,
            bossAnimationParam.slideLanding
        );

        if (!finished)
            return;

        slidePhase = SlidePhase.None;

        transform.position = GetAnchorPosition(currentAnchorSide);

        if (slideParam.faceStageCenterOnLanding)
            FaceTo(GetStageCenterRaw());

        ChangeState(BossState.Idle);
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 GetSlideReferencePosition()
    {
        if (slideParam.useFootReferenceForSlideMove &&
            slideParam.slideReferenceFootBone != null)
        {
            return slideParam.slideReferenceFootBone.position;
        }

        return transform.position;
    }

    private void MoveRootSoSlideReferenceMatches(Vector3 targetReferencePosition)
    {
        Vector3 currentReference = GetSlideReferencePosition();

        Vector3 delta = targetReferencePosition - currentReference;

        if (!slideParam.alignFootY)
            delta.y = 0f;

        transform.position += delta;
    }

    private void CorrectSlideFootToStartAfterLoopEnter()
    {
        if (!slideParam.correctFootAfterLoopRotation)
            return;

        if (!slideParam.useFootReferenceDuringLoop)
            return;

        if (slideParam.slideReferenceFootBone == null)
            return;

        Vector3 beforeFoot = GetSlideReferencePosition();

        Vector3 targetFoot = slideStart;

        if (!slideParam.alignFootY)
            targetFoot.y = beforeFoot.y;

        Vector3 delta = targetFoot - beforeFoot;

        if (!slideParam.alignFootY)
            delta.y = 0f;

        float correctionDistance = delta.magnitude;

        transform.position += delta;

        if (slideParam.warnLoopEnterFootCorrectionDistance > 0f &&
            correctionDistance > slideParam.warnLoopEnterFootCorrectionDistance)
        {
            Debug.LogWarning(
                $"{name} SlidingD開始時の足補正が大きいです。 " +
                $"distance:{correctionDistance:F2} " +
                $"startSide:{slideStartSide} endSide:{slideEndSide} " +
                $"entryDir:{currentSlideEntryDirection}"
            );
        }
    }

    private void UpdateJumpInXZCorrection(float dt)
    {
        if (slideParam.slideReferenceFootBone == null)
            return;

        Vector3 currentFoot = GetSlideReferencePosition();
        Vector3 targetFoot = slideStart;

        Vector3 delta = targetFoot - currentFoot;
        delta.y = 0f;

        Vector3 correction = Vector3.ClampMagnitude(
            delta,
            slideParam.maxFootSnapOnSlideStart * dt
        );

        transform.position += correction;
    }

    private void BeginSlideYOffsetInterpolation()
    {
        if (!slideParam.useJumpInYOffsetInterpolation)
            return;

        slideYOffsetActive = true;
        slideCurrentYOffset = 0f;
        slideYOffsetTimer = 0f;

        slideYOffsetDuration = slideParam.jumpInYOffsetDuration;

        if (slideYOffsetDuration <= 0f)
            slideYOffsetDuration = Mathf.Max(0.01f, animationParam.slideJumpIn.duration);
    }

    private void UpdateSlideYOffsetInterpolation(float dt)
    {
        if (!slideYOffsetActive)
            return;

        if (!slideParam.useJumpInYOffsetInterpolation)
            return;

        // JumpIn中だけ補間して、完了後はそのY補正を維持する
        if (slidePhase != SlidePhase.JumpInAnimation)
            return;

        slideYOffsetTimer += dt;

        float t = slideYOffsetDuration <= 0f
            ? 1f
            : Mathf.Clamp01(slideYOffsetTimer / slideYOffsetDuration);

        float curveT = slideParam.jumpInYOffsetCurve != null
            ? slideParam.jumpInYOffsetCurve.Evaluate(t)
            : t;

        float desiredYOffset = Mathf.Lerp(
            0f,
            slideParam.jumpInYOffset,
            curveT
        );

        ApplySlideYOffsetDelta(desiredYOffset);
    }

    private void ApplySlideYOffsetDelta(float desiredYOffset)
    {
        float deltaY = desiredYOffset - slideCurrentYOffset;

        if (Mathf.Abs(deltaY) < 0.0001f)
            return;

        transform.position += Vector3.up * deltaY;
        slideCurrentYOffset = desiredYOffset;
    }

    private void RestoreSlideYOffsetImmediate()
    {
        if (!slideYOffsetActive)
            return;

        if (!slideParam.restoreYOffsetOnJumpOutEnd)
            return;

        if (Mathf.Abs(slideCurrentYOffset) > 0.0001f)
        {
            transform.position -= Vector3.up * slideCurrentYOffset;
        }

        slideCurrentYOffset = 0f;
        slideYOffsetTimer = 0f;
        slideYOffsetActive = false;
    }

    #endregion

    #region Stun / Damage / HitBox

    private void EnterStunned()
    {
        if (slideParam.slideHitbox != null)
            slideParam.slideHitbox.SetActiveHit(false);

        SetSlideAttackHitboxActive(false);

        // FieldObject判定はDeath以外なら常時ON方針なのでON
        SetFieldObjectHitboxesActive(true);

        // 倒れ切るまでは弱点OFF
        SetStunnedHitboxesActive(false);

        ForceFinishAllBalls();

        stunPhase = StunPhase.DownAnimation;
        stunPhaseTimer = 0f;

        PlayStunDown();
    }

    private void UpdateStunned()
    {
        float dt = ScaledDeltaTime;

        stunPhaseTimer += dt;

        // StunLoopに入ってからだけ、怒り/死亡への特殊移行を許可する
        if (IsStunLoopReadyForSpecialTransition())
        {
            if (ProcessStunSpecialTransition())
                return;
        }

        switch (stunPhase)
        {
            case StunPhase.DownAnimation:
                UpdateStunDown();
                break;

            case StunPhase.StunLoop:
                UpdateStunLoop();
                break;

            case StunPhase.GetUpAnimation:
                UpdateStunGetUp();
                break;
        }
    }

    private bool ProcessStunSpecialTransition()
    {
        // 死亡を最優先
        if (pendingDeathAfterStunSafePoint || hp <= 0f)
        {
            pendingDeathAfterStunSafePoint = false;
            pendingAngerAfterStunSafePoint = false;

            EnterDeathByDamage();
            return true;
        }

        // 次に怒り
        if (pendingAngerAfterStunSafePoint || ShouldEnterAngryByHP())
        {
            pendingAngerAfterStunSafePoint = false;

            TryEnterAngry();
            return true;
        }

        return false;
    }

    private void UpdateStunDown()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.stunDown,
            stunPhaseTimer,
            animationEventFinished,
            bossAnimationParam.stunDown
        );

        if (!finished)
            return;

        BeginStunLoop();
    }

    private void BeginStunLoop()
    {
        animationEventFinished = false;
        stunPhaseTimer = 0f;
        stunPhase = StunPhase.StunLoop;

        SetStunnedHitboxesActive(true);

        if (effectPlayer != null)
        {
            stuneffect = effectPlayer.PlayAt(
                1,
                stunPoseParam.BossHeadPoint.position
            );
        }

        PlayStunLoop();
    }

    private void UpdateStunLoop()
    {
        if (stunPhaseTimer < GetCurrentStunnedDuration())
            return;

        BeginStunGetUp();
    }

    private void BeginStunGetUp()
    {
        animationEventFinished = false;
        stunPhaseTimer = 0f;
        stunPhase = StunPhase.GetUpAnimation;

        SetStunnedHitboxesActive(false);

        if (stuneffect != null)
        {
            stuneffect.StopImmediate();
            stuneffect = null;
        }

        PlayStunGetUp();
    }

    private void UpdateStunGetUp()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.stunGetUp,
            stunPhaseTimer,
            animationEventFinished,
            bossAnimationParam.stunGetUp
        );

        if (!finished)
            return;

        stunGauge = statusParam.stunGaugeAfterRecovery;

        if (stunPoseParam.snapBackToAnchorOnRecovery)
            SnapToCurrentAnchor();

        // GetUp中にHP0/怒り条件を満たしていた場合は、
        // ここではState変更せず、Idleに戻ってから処理する
        if (hp <= 0f)
        {
            hp = 0f;
            pendingDeathAfterStunSafePoint = true;
        }
        else if (ShouldEnterAngryByHP())
        {
            pendingAngerAfterStunSafePoint = true;
        }

        stunPhase = StunPhase.None;

        ChangeState(BossState.Idle);
    }


    public void ApplyReflectedBallDamage(float damage, float stunValue , Vector3 pos)
    {
        if (state == BossState.Dead)
            return;

        hp -= damage;
        stunGauge += stunValue;

        audioManager.PlaySe("FO_Explosion", pos);

        GetSideBasis(
            currentAnchorSide,
            out Vector3 outward,
            out _
            );

        // 開始辺から見たステージ内側方向を「正面」とする
        Quaternion rotation = Quaternion.LookRotation(-outward, Vector3.up);

        effectPlayer.PlayAt(7 , pos , rotation);

        //光
        hitMaterialFlashPlayer?.PlayFlash();

        if (logHit)
        {
            Debug.Log(
                $"{name} ReflectedBall Hit damage:{damage:F1} " +
                $"hp:{hp:F1} stun:{stunGauge:F1}/{statusParam.stunGaugeMax:F1}"
            );
        }

        if (hp <= 0f)
        {
            EnterDeathByDamage();
            return;
        }

        if (stunGauge >= GetCurrentStunGaugeMax() &&
            state != BossState.Stunned)
        {
            ChangeState(BossState.Stunned);
            return;
        }

        // 死亡でも気絶でもない場合は、反射ボール被弾モーションへ
        if (state != BossState.ReflectedHit)
        {
            ChangeState(BossState.ReflectedHit);
        }
        else
        {
            // 連続命中時はモーションを再スタートしたい場合
            stateTimer = 0f;
            animationEventFinished = false;
            PlayReflectedHit();
        }
    }

    private void SetFieldObjectHitboxesActive(bool active)
    {
        SetHitboxObjectsActive(hitboxParam.fieldObjectReceiveHitboxes, active);
        fieldObjectHitboxesActive = active;

        if (hitboxParam.logHitboxSwitch)
            Debug.Log($"{name} FieldObject Hitboxes => {active}");
    }

    private void SetStunnedHitboxesActive(bool active)
    {
        if (!active && !hitboxParam.disableStunnedHitBoxesWhenNotStunned)
            return;

        SetHitboxObjectsActive(hitboxParam.stunnedHitBoxes, active);
        stunnedHitboxesActive = active;

        if (hitboxParam.logHitboxSwitch)
            Debug.Log($"{name} Stunned Hitboxes => {active}");
    }

    private void SetSlideAttackHitboxActive(bool active)
    {
        if (hitboxParam.slideAttackHitbox != null)
            hitboxParam.slideAttackHitbox.SetActive(active);

        slideAttackHitboxActive = active;

        if (!active)
            slideHitTargets.Clear();

        if (hitboxParam.logHitboxSwitch)
            Debug.Log($"{name} Slide Attack Hitbox => {active}");
    }

    private void SetHitboxObjectsActive(GameObject[] hitboxes, bool active)
    {
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] == null)
                continue;

            hitboxes[i].SetActive(active);
        }
    }

    private void ApplyNormalHitboxMode()
    {
        if (state != BossState.Dead)
            SetFieldObjectHitboxesActive(true);

        SetStunnedHitboxesActive(false);
        SetSlideAttackHitboxActive(false);
    }
    private void BeginSlideHitboxMode()
    {
        if (hitboxParam.disableFieldObjectHitboxesDuringSlide)
            SetFieldObjectHitboxesActive(false);

        SetStunnedHitboxesActive(false);

        if (hitboxParam.enableSlideAttackFromJumpInToJumpOut)
            SetSlideAttackHitboxActive(true);

        slideHitTargets.Clear();
    }

    private void EndSlideHitboxMode()
    {
        SetSlideAttackHitboxActive(false);

        if (state != BossState.Dead)
            SetFieldObjectHitboxesActive(true);
    }
    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (state == BossState.Dead)
            return;

        if (selfHitbox == null || other == null)
            return;

        if (hitboxParam.slideAttackHitbox == null)
            return;

        // Slide攻撃Hitbox以外はBoss側では処理しない
        if (selfHitbox.gameObject != hitboxParam.slideAttackHitbox)
            return;

        if (!IsSlideAttackPeriod())
            return;

        HandleSlideAttackHit(selfHitbox, other);
    }
    public void OnHit(HitEventData data)
    {
        if (state == BossState.Dead)
            return;

        // 1. FieldObject / Chain用ボディHitbox
        if (IsFieldObjectReceiveHit(data))
        {
            HandleFieldObjectHit(data);
            return;
        }

        // 2. Stunned中のプレイヤー直接攻撃用Hitbox
        if (IsStunnedDirectAttackHit(data))
        {
            HandleStunnedDirectAttackHit(data);
            return;
        }
    }

    private void TakeDirectDamage(float damage)
    {
        if (damage <= 0f)
            return;

        hp -= damage;

        //MaterialFlash
        hitMaterialFlashPlayer?.PlayFlash();

        if (logHit)
            Debug.Log($"{name} Stunned Damage:{damage:F1} HP:{hp:F1}");

        if (hp <= 0f)
        {
            EnterDeathByDamage();
            return;
        }
    }

    private bool IsFieldObjectReceiveHit(HitEventData data)
    {
        if (state == BossState.Dead)
            return false;

        // Slide中のJumpIn〜JumpOutでは通常受けを無視
        if (IsSlideAttackPeriod())
        {
            Debug.Log($"Slide was Reason");

            return false;

        }

        GameObject target = data.targetHitbox;
        if (target == null)
        {
            Debug.Log($"target was null");

            //target = data.targetObject.gameObject;
        }

        if (!ContainsHitbox(hitboxParam.fieldObjectReceiveHitboxes, target))
            return false;

        if(data.payload is ChainPayload chain)
        {
            Debug.Log("Was Chain");

            return true;
        }

        return false;
    }

    private void HandleFieldObjectHit(HitEventData data)
    {
        if(data.payload is ChainPayload chain)
        {
            stunGauge += statusParam.FieldObjectStunValue;

            Vector3 scale = Vector3.one;

            scale.x = 0.5f;
            scale.y = 0.5f;
            scale.z = 0.5f;

            Vector3 rot = chain.direction;
            rot.y = 0.0f;

            rot.Normalize();

            Quaternion rotation = Quaternion.LookRotation(rot, Vector3.up);

            audioManager.PlaySe("FO_Explosion", data.attackerObject.transform.position);

            effectPlayer.PlayAt(7, data.attackerObject.transform.position,rotation, scale);

            TakeDirectDamage(chain.damage);
        }

        if (logHit)
        {
            Debug.Log(
                $"{name} FieldObject/Chain Hit " +
                $"target:{GetObjectName(data.targetHitbox)} " +
                $"attacker:{GetObjectName(data.attackerObject)} " +
                $"payload:{(data.payload != null ? data.payload.GetType().Name : "null")}"
            );
        }

        // ここにFieldObjectが当たった時の処理を追加
        // 例: スタン値を増やす、エフェクト、ノックバックなど
        // 現時点では判定確認だけならログのみでOK
    }

    private bool IsStunnedDirectAttackHit(HitEventData data)
    {
        if (state != BossState.Stunned)
            return false;

        if (stunPhase != StunPhase.StunLoop)
            return false;

        if (!ContainsHitbox(hitboxParam.stunnedHitBoxes, data.targetHitbox))
            return false;

        bool validPayload =
            data.payload is BlowPayload ||
            data.payload == null;

        if (!validPayload)
            return false;

        return true;
    }

    private bool IsStunLoopReadyForSpecialTransition()
    {
        return state == BossState.Stunned &&
               stunPhase == StunPhase.StunLoop &&
               stunPhaseTimer > 0f;
    }

    private bool ShouldDelaySpecialTransitionFromStun()
    {
        if (state != BossState.Stunned)
            return false;

        // StunLoopに入って1フレーム以上経っていれば移行OK
        if (IsStunLoopReadyForSpecialTransition())
            return false;

        // DownAnimation中、またはGetUpAnimation中は予約だけにする
        return true;
    }

    private bool ShouldEnterAngryByHP()
    {
        if (isAngry)
            return false;

        if (state == BossState.Dead)
            return false;

        return hp <= statusParam.angryHPThreshold;
    }

    private bool HasPendingStunSpecialTransition()
    {
        return pendingDeathAfterStunSafePoint ||
               pendingAngerAfterStunSafePoint ||
               hp <= 0f ||
               ShouldEnterAngryByHP();
    }

    private void HandleStunnedDirectAttackHit(HitEventData data)
    {
        if (localTime < lastStunnedHitTime + hitboxParam.stunnedHitCooldown)
            return;

        lastStunnedHitTime = localTime;
        
        if(data.payload is BlowPayload blow)
        {
            float finalDamage = ResolveDamageFromBlow(blow);

            TakeDirectDamage(finalDamage);
        }
    }

    private bool ContainsHitbox(GameObject[] hitboxes, GameObject targetHitbox)
    {
        if (hitboxes == null || targetHitbox == null)
        {
            //Debug.Log($"It was me");

            return false;
        }

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] == targetHitbox)
            {
                Debug.Log($"HitBox was OK");

                return true;
            }
                
        }

        Debug.Log($"HitBox wasn't Ok");

        return false;
    }

    private bool IsSlideAttackPeriod()
    {
        if (state != BossState.Slide)
            return false;

        return slidePhase == SlidePhase.JumpInAnimation ||
               slidePhase == SlidePhase.LoopMove ||
               slidePhase == SlidePhase.JumpOutAnimation;
    }

    private string GetObjectName(GameObject obj)
    {
        return obj != null ? obj.name : "null";
    }

    private void HandleSlideAttackHit(Hitbox selfHitbox, Collider other)
    {
        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        IHitReceiver receiver = null;
        GameObject receiverObject = null;

        if (targetHitbox != null && targetHitbox.receiver != null)
        {
            receiver = targetHitbox.receiver;

            if (receiver is MonoBehaviour mono)
                receiverObject = mono.gameObject;
            else
                receiverObject = other.gameObject;
        }
        else
        {
            receiver = other.GetComponentInParent<IHitReceiver>();

            if (receiver is MonoBehaviour mono)
                receiverObject = mono.gameObject;
        }

        if (receiver == null || receiverObject == null)
            return;

        if (receiverObject == gameObject)
            return;

        if (hitboxParam.hitSameTargetOncePerSlide &&
            slideHitTargets.Contains(receiverObject))
        {
            return;
        }

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,

            targetObject = receiverObject,
            targetHitbox = targetHitbox != null
                ? targetHitbox.gameObject
                : other.gameObject,

            contactPoint = other.ClosestPoint(selfHitbox.transform.position),

            payload = 
                new EnemyAttackPayload
                {
                    damage = hitboxParam.slidePlayerDamage
                }
               
        };

        if (logHit)
        {
            Debug.Log(
                $"{name} Slide Hit " +
                $"target:{receiverObject.name} " +
                $"layer:{LayerMask.LayerToName(other.gameObject.layer)} " +
                $"payload:{data.payload.GetType().Name}"
            );
        }

        receiver.OnHit(data);
    }

    private float ResolveDamageFromBlow(BlowPayload blow)
    {
        float rate = Mathf.Clamp01(blow.powerRate);
        return Mathf.Max(0f, blow.powerConstant * rate);
    }

    private void EnterDeathByDamage()
    {
        hp = 0f;

        if (ShouldDelaySpecialTransitionFromStun())
        {
            pendingDeathAfterStunSafePoint = true;
            pendingAngerAfterStunSafePoint = false;
            return;
        }

        deathEnteredFromStunned =
            state == BossState.Stunned ||
            stunPhase == StunPhase.DownAnimation ||
            stunPhase == StunPhase.StunLoop ||
            stunPhase == StunPhase.GetUpAnimation;

        ChangeState(BossState.Dead);
    }

    #endregion

    #region Stage / Anchor

    private BossAnchorSide SelectRandomOtherSide(BossAnchorSide baseSide)
    {
        BossAnchorSide selected = baseSide;

        int safety = 0;

        while (selected == baseSide && safety < 20)
        {
            selected = (BossAnchorSide)Random.Range(0, 4);
            safety++;
        }

        return selected;
    }

    private BossAnchorSide FindNearestAnchorSide(Vector3 position)
    {
        BossAnchorSide best = BossAnchorSide.North;
        float bestDist = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            BossAnchorSide side = (BossAnchorSide)i;
            Vector3 anchor = GetAnchorPosition(side);
            float dist = Vector3.SqrMagnitude(position - anchor);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = side;
            }
        }

        return best;
    }

    private BossAnchorSide FindNearestStageEdgeSide(Vector3 position)
    {
        BossAnchorSide best = BossAnchorSide.North;
        float bestDist = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            BossAnchorSide side = (BossAnchorSide)i;
            Vector3 edgeCenter = GetStageEdgeCenter(side);
            float dist = Vector3.SqrMagnitude(position - edgeCenter);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = side;
            }
        }

        return best;
    }

    private Vector3 GetAnchorPosition(BossAnchorSide side)
    {
        if (hasCapturedAnchorOffset)
        {
            return GetAnchorPositionFromCapturedOffset(side);
        }

        return GetOutsidePositionFromStageSide(
            side,
            stageParam.anchorOutsideDistance
        );
    }

    private Vector3 GetAnchorPositionFromCapturedOffset(BossAnchorSide side)
    {
        Vector3 edgeCenter = GetStageEdgeCenterRaw(side);

        GetSideBasis(
            side,
            out Vector3 outward,
            out Vector3 tangent
        );

        return
            edgeCenter +
            tangent * capturedAnchorLocalOffset.x +
            Vector3.up * capturedAnchorLocalOffset.y +
            outward * capturedAnchorLocalOffset.z;
    }

    private Vector3 GetStageEdgeCenter(BossAnchorSide side)
    {
        return GetStageEdgeCenterRaw(side);
    }

    private Vector3 GetOutsidePositionFromStageSide(
    BossAnchorSide side,
    float outsideOffset)
    {
        Vector3 edgeCenter = GetStageEdgeCenterRaw(side);

        GetSideBasis(
            side,
            out Vector3 outward,
            out _
        );

        Vector3 pos = edgeCenter + outward * outsideOffset;

        // 旧方式ではbossYOffsetを使う
        pos.y += stageParam.bossYOffset;

        return pos;
    }

    private Vector3 EvaluateJumpPosition(
        Vector3 start,
        Vector3 end,
        float height,
        float t)
    {
        Vector3 pos = Vector3.Lerp(start, end, t);

        float arc = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, height);
        pos.y += arc;

        return pos;
    }

    private void SnapToCurrentAnchor()
    {
        transform.position = GetAnchorPosition(currentAnchorSide);
        FacePlayer();
    }

    private bool TryGetStageBounds(out Bounds bounds)
    {
        bounds = new Bounds(
            Vector3.zero,
            Vector3.one * stageParam.fallbackStageSize
        );

        if (stageParam.stageObject == null)
            return false;

        if (stageParam.preferColliderBounds)
        {
            Collider col = stageParam.stageObject.GetComponent<Collider>();

            if (col == null)
                col = stageParam.stageObject.GetComponentInChildren<Collider>();

            if (col != null)
            {
                bounds = col.bounds;
                return true;
            }
        }

        Renderer renderer = stageParam.stageObject.GetComponent<Renderer>();

        if (renderer == null)
            renderer = stageParam.stageObject.GetComponentInChildren<Renderer>();

        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        return false;
    }

    private Vector3 GetStageCenter()
    {
        if (TryGetStageBounds(out Bounds bounds))
        {
            Vector3 center = bounds.center;
            center.y += stageParam.bossYOffset;
            return center;
        }

        if (stageParam.fallbackStageCenter != null)
        {
            Vector3 center = stageParam.fallbackStageCenter.position;
            center.y += stageParam.bossYOffset;
            return center;
        }

        return Vector3.zero;
    }

    private float GetStageTopY()
    {
        if (TryGetStageBounds(out Bounds bounds))
            return bounds.max.y;

        if (stageParam.fallbackStageCenter != null)
            return stageParam.fallbackStageCenter.position.y;

        return transform.position.y;
    }

    private Vector2 GetStageHalfSizeXZ()
    {
        if (TryGetStageBounds(out Bounds bounds))
        {
            return new Vector2(
                bounds.extents.x,
                bounds.extents.z
            );
        }

        float half = stageParam.fallbackStageSize * 0.5f;
        return new Vector2(half, half);
    }

    private void CaptureAnchorOffsetFromCurrentTransform()
    {
        capturedAnchorBaseSide = FindNearestStageEdgeSide(transform.position);

        Vector3 edgeCenter = GetStageEdgeCenterRaw(capturedAnchorBaseSide);

        GetSideBasis(
            capturedAnchorBaseSide,
            out Vector3 outward,
            out Vector3 tangent
        );

        Vector3 delta = transform.position - edgeCenter;

        capturedAnchorLocalOffset = new Vector3(
            Vector3.Dot(delta, tangent),
            delta.y,
            Vector3.Dot(delta, outward)
        );

        hasCapturedAnchorOffset = true;

        if (logState)
        {
            Debug.Log(
                $"{name} Captured Anchor Offset. " +
                $"baseSide:{capturedAnchorBaseSide} " +
                $"offsetLocal:{capturedAnchorLocalOffset}"
            );
        }
    }

    private Vector3 GetStageCenterRaw()
    {
        if (TryGetStageBounds(out Bounds bounds))
            return bounds.center;

        if (stageParam.fallbackStageCenter != null)
            return stageParam.fallbackStageCenter.position;

        return Vector3.zero;
    }

    private Vector3 GetStageEdgeCenterRaw(BossAnchorSide side)
    {
        Vector3 center = GetStageCenterRaw();
        Vector2 half = GetStageHalfSizeXZ();

        Vector3 pos = center;

        switch (side)
        {
            case BossAnchorSide.North:
                pos.z = center.z + half.y;
                pos.x = center.x;
                break;

            case BossAnchorSide.East:
                pos.x = center.x + half.x;
                pos.z = center.z;
                break;

            case BossAnchorSide.South:
                pos.z = center.z - half.y;
                pos.x = center.x;
                break;

            case BossAnchorSide.West:
                pos.x = center.x - half.x;
                pos.z = center.z;
                break;
        }

        pos.y = center.y;
        return pos;
    }

    private void GetSideBasis(
    BossAnchorSide side,
    out Vector3 outward,
    out Vector3 tangent)
    {
        switch (side)
        {
            case BossAnchorSide.North:
                outward = Vector3.forward;
                break;

            case BossAnchorSide.East:
                outward = Vector3.right;
                break;

            case BossAnchorSide.South:
                outward = Vector3.back;
                break;

            case BossAnchorSide.West:
                outward = Vector3.left;
                break;

            default:
                outward = Vector3.forward;
                break;
        }

        // 辺に沿う方向。
        // North: +X
        // East : -Z
        // South: -X
        // West : +Z
        tangent = Vector3.Cross(Vector3.up, outward).normalized;
    }
    private Vector3 ClampToStage(Vector3 pos)
    {
        Vector3 center = GetStageCenter();
        Vector2 half = GetStageHalfSizeXZ();

        float margin = stageParam.targetClampMargin;

        pos.x = Mathf.Clamp(
            pos.x,
            center.x - half.x + margin,
            center.x + half.x - margin
        );

        pos.z = Mathf.Clamp(
            pos.z,
            center.z - half.y + margin,
            center.z + half.y - margin
        );

        return pos;
    }

    #endregion

    #region Target / Aim

    private void UpdatePlayerVelocity(float dt)
    {
        if (playerTarget == null)
            return;

        dt = Mathf.Max(dt, 0.0001f);
        estimatedPlayerVelocity =
            (playerTarget.position - previousPlayerPosition) / dt;

        previousPlayerPosition = playerTarget.position;
    }

    private Vector3 GetPredictedPlayerPosition(float leadTime)
    {
        if (playerTarget == null)
            return GetStageCenter();

        return playerTarget.position + estimatedPlayerVelocity * leadTime;
    }

    private Vector3 GetPlayerNearLandingTarget(KickParam param)
    {
        Vector3 target;

        if (playerTarget != null)
        {
            target = GetPredictedPlayerPosition(param.targetLeadTime);

            Vector2 random =
                Random.insideUnitCircle * param.playerAimRandomRadius;

            target.x += random.x;
            target.z += random.y;

            target = ClampToStage(target);
        }
        else
        {
            target = GetRandomStageLandingTarget(param.randomTargetMargin);
        }

        return target;
    }

    private Vector3 GetRandomStageLandingTarget(float margin)
    {
        Vector3 center = GetStageCenter();
        Vector2 half = GetStageHalfSizeXZ();

        float safeHalfX = Mathf.Max(0.1f, half.x - margin);
        float safeHalfZ = Mathf.Max(0.1f, half.y - margin);

        Vector3 target = center;

        target.x = Random.Range(center.x - safeHalfX, center.x + safeHalfX);
        target.z = Random.Range(center.z - safeHalfZ, center.z + safeHalfZ);

        return ClampToStage(target);
    }

    private void FacePlayer()
    {
        if (playerTarget == null)
            return;

        FaceTo(playerTarget.position);
    }

    private void FaceTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    #endregion

    #region Status / Angry

    private void UpdateAngry()
    {
        TryEnterAngry();
    }

    private bool TryEnterAngry()
    {
        if (isAngry)
            return false;

        if (state == BossState.Dead)
            return false;

        if (hp > statusParam.angryHPThreshold)
            return false;

        if (ShouldDelaySpecialTransitionFromStun())
        {
            pendingAngerAfterStunSafePoint = true;
            return false;
        }

        // 怒り移行を許可する状態
        bool canEnterAngry =
            state == BossState.Idle ||
            IsStunLoopReadyForSpecialTransition();

        if (!canEnterAngry)
            return false;

        angerEnteredFromStunned =
            state == BossState.Stunned ||
            stunPhase == StunPhase.StunLoop;

        isAngry = true;

        if (statusParam.resetStunGaugeOnAngryEnter)
            stunGauge = statusParam.stunGaugeOnAngryEnter;

        if (animator != null && !string.IsNullOrEmpty(bossAnimationParam.angryBool))
            animator.SetBool(angryBoolHash, true);

        pendingAngerAfterStunSafePoint = false;

        ChangeState(BossState.Anger);

        return true;
    }

    public void PlayAngerFieldObjectExplosionEffect(
    int effectIndex,
    Vector3 position,
    Quaternion rotation,
    Vector3 scale)
    {
        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>();

        if (effectPlayer == null)
            return;

        EffectPlayParam param = EffectPlayParam.Default;

        param.overrideFollowTarget = true;
        param.followTarget = false;

        param.overridePosition = true;
        param.positionOffset = Vector3.zero;

        param.overrideRotation = false;
        param.rotationOffset = Vector3.zero;

        effectPlayer.PlayAt(
            effectIndex,
            position,
            rotation,
            scale,
            param
        );
    }
    private void ExplodeAllActiveBallsForAnger()
    {
        if (activeBalls == null || activeBalls.Count == 0)
            return;

        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            SoccerBossBall ball = activeBalls[i];

            if (ball == null)
            {
                activeBalls.RemoveAt(i);
                continue;
            }

            if (angerExplosionParam.removeBallsFromActiveListImmediately)
            {
                ball.OnBallFinished -= HandleBallFinished;
                activeBalls.RemoveAt(i);
            }

            ball.ForceExplodeFromAnger();
        }

        if (angerExplosionParam.logAngerExplosion)
        {
            Debug.Log($"{name}: Anger enter exploded all active SoccerBalls.");
        }
    }

    private float GetCurrentStunGaugeMax()
    {
        return isAngry
            ? statusParam.angryStunGaugeMax
            : statusParam.stunGaugeMax;
    }

    private float GetCurrentStunnedDuration()
    {
        return isAngry
            ? statusParam.angryStunnedDuration
            : statusParam.stunnedDuration;
    }
    #endregion

    #region UI

    public float StunDisplayRate
    {
        get
        {
            float max = GetCurrentStunGaugeMax();

            if (max <= 0f)
                return 0f;

            if (state == BossState.Stunned)
            {
                switch (stunPhase)
                {
                    case StunPhase.DownAnimation:
                        return 1f;

                    case StunPhase.StunLoop:
                        float duration = GetCurrentStunnedDuration();

                        if (duration <= 0f)
                            return 0f;

                        return Mathf.Clamp01(1f - stunPhaseTimer / duration);

                    case StunPhase.GetUpAnimation:
                        return 0f;
                }
            }

            return Mathf.Clamp01(stunGauge / max);
        }
    }

    #endregion

    #region Dying Effect

    private void PlayDyingEffectGrow()
    {
        if (dyingEffectParam == null || !dyingEffectParam.enable)
            return;

        if (effectBonePlayer == null)
            effectBonePlayer = GetComponentInChildren<EffectBonePlayer>();

        if (effectBonePlayer == null)
            return;

        EnsureDyingEffectRuntime();

        List<int> candidates = CreateGrowDyingEffectCandidates();

        if (candidates.Count == 0)
        {
            if (dyingEffectParam.logSelectedEffects)
            {
                Debug.LogWarning(
                    $"{name}: DyingEffect Grow candidates is empty. " +
                    "All slots may already be Large."
                );
            }

            return;
        }

        int playCount = GetGrowDyingEffectPlayCount(candidates.Count);

        ShuffleList(candidates);

        List<int> selectedBaseIds = new List<int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (selectedBaseIds.Count >= playCount)
                break;

            int localBaseId = candidates[i];

            audioManager.PlaySe("SB_MiniExplosion", effectBonePlayer.GetEffect(localBaseId).transform.position);

            if (!TryPlayGrowDyingEffectAtLocalBase(localBaseId, out int playedBaseId))
                continue;

            selectedBaseIds.Add(playedBaseId);
        }

        dyingEffectGrowCallCount++;

        if (dyingEffectParam.logSelectedEffects)
        {
            Debug.Log(
                $"{name}: AnimEvent_DyingEffect Grow call:{dyingEffectGrowCallCount} " +
                $"selected base IDs: {string.Join(", ", selectedBaseIds)}"
            );
        }
    }

    private List<int> CreateGrowDyingEffectCandidates()
    {
        List<int> candidates = new List<int>();

        if (effectBonePlayer == null)
            return candidates;

        int min = Mathf.Max(0, Mathf.Min(
            dyingEffectParam.smallMinId,
            dyingEffectParam.smallMaxId
        ));

        int max = Mathf.Max(min, Mathf.Max(
            dyingEffectParam.smallMinId,
            dyingEffectParam.smallMaxId
        ));

        for (int id = min; id <= max; id++)
        {
            if (!IsDyingEffectBaseParity(id))
                continue;

            int pairId = GetDyingEffectPairId(id);

            if (pairId < min || pairId > max)
                continue;

            int localBaseId = GetDyingEffectLocalBaseId(id);

            if (!IsValidDyingEffectLocalBase(localBaseId))
                continue;

            RefreshDyingEffectSlot(localBaseId);

            int activeLevel = dyingEffectActiveLevelByLocalBase[localBaseId];

            // すでにLargeならこれ以上成長できないので候補から外す
            if (activeLevel >= 2)
                continue;

            int nextLevel = activeLevel + 1;

            int nextBaseId = GetDyingEffectIdByLocalBaseAndLevel(
                localBaseId,
                nextLevel
            );

            int nextPairId = GetDyingEffectPairId(nextBaseId);

            if (!IsDyingEffectIdInLevelRange(nextBaseId, nextLevel))
                continue;

            if (!IsDyingEffectIdInLevelRange(nextPairId, nextLevel))
                continue;

            if (!effectBonePlayer.HasEffect(nextBaseId))
                continue;

            if (!effectBonePlayer.HasEffect(nextPairId))
                continue;

            candidates.Add(localBaseId);
        }

        return candidates;
    }

    private bool TryPlayGrowDyingEffectAtLocalBase(
        int localBaseId,
        out int playedBaseId)
    {
        playedBaseId = -1;

        if (!IsValidDyingEffectLocalBase(localBaseId))
            return false;

        RefreshDyingEffectSlot(localBaseId);

        int activeLevel = dyingEffectActiveLevelByLocalBase[localBaseId];

        if (activeLevel >= 2)
            return false;

        int nextLevel = activeLevel + 1;

        int baseId = GetDyingEffectIdByLocalBaseAndLevel(
            localBaseId,
            nextLevel
        );

        if (!TryPlayDyingEffectPair(baseId))
            return false;

        playedBaseId = baseId;
        return true;
    }

    private bool TryPlayDyingEffectPair(int baseId)
    {
        int pairId = GetDyingEffectPairId(baseId);

        if (!effectBonePlayer.HasEffect(baseId))
            return false;

        if (!effectBonePlayer.HasEffect(pairId))
            return false;

        int level = GetDyingEffectLevel(baseId);
        int localBaseId = GetDyingEffectLocalBaseId(baseId);

        if (!IsValidDyingEffectLocalBase(localBaseId))
            return false;

        RefreshDyingEffectSlot(localBaseId);

        int activeLevel = dyingEffectActiveLevelByLocalBase[localBaseId];

        // 同じ位置に下位サイズが出ているなら、上位を出す前に止める
        if (activeLevel >= 0 && activeLevel < level)
        {
            StopDyingEffectSameSlotLowerThan(localBaseId, level);
        }

        effectBonePlayer.PlayEffect(baseId);
        effectBonePlayer.PlayEffect(pairId);

        dyingEffectActiveLevelByLocalBase[localBaseId] =
            Mathf.Max(activeLevel, level);

        if (dyingEffectParam.logSelectedEffects)
        {
            Debug.Log(
                $"{name}: PlayDyingEffectPair base:{baseId} pair:{pairId} " +
                $"local:{localBaseId} level:{level}"
            );
        }

        return true;
    }

    private void StopDyingEffectSameSlotLowerThan(
        int localBaseId,
        int targetLevel)
    {
        for (int level = 0; level < targetLevel; level++)
        {
            int baseId = GetDyingEffectIdByLocalBaseAndLevel(
                localBaseId,
                level
            );

            StopDyingEffectPair(baseId);
        }
    }

    private void StopDyingEffectPair(int baseId)
    {
        int pairId = GetDyingEffectPairId(baseId);

        if (effectBonePlayer.HasEffect(baseId))
            effectBonePlayer.StopEffect(baseId);

        if (effectBonePlayer.HasEffect(pairId))
            effectBonePlayer.StopEffect(pairId);
    }

    private void RefreshDyingEffectSlot(int localBaseId)
    {
        if (!IsValidDyingEffectLocalBase(localBaseId))
            return;

        int activeLevel = dyingEffectActiveLevelByLocalBase[localBaseId];

        if (activeLevel < 0)
            return;

        int baseId = GetDyingEffectIdByLocalBaseAndLevel(
            localBaseId,
            activeLevel
        );

        if (IsDyingEffectPairPlaying(baseId))
            return;

        dyingEffectActiveLevelByLocalBase[localBaseId] = -1;
    }

    private bool IsDyingEffectPairPlaying(int baseId)
    {
        int pairId = GetDyingEffectPairId(baseId);

        bool basePlaying =
            effectBonePlayer.HasEffect(baseId) &&
            effectBonePlayer.IsPlaying(baseId);

        bool pairPlaying =
            effectBonePlayer.HasEffect(pairId) &&
            effectBonePlayer.IsPlaying(pairId);

        return basePlaying || pairPlaying;
    }

    private bool IsDyingEffectBaseParity(int id)
    {
        bool isEven = id % 2 == 0;

        switch (dyingEffectParam.baseParity)
        {
            case EffectIdParity.Even:
                return isEven;

            case EffectIdParity.Odd:
                return !isEven;
        }

        return false;
    }

    private int GetDyingEffectPairId(int baseId)
    {
        switch (dyingEffectParam.baseParity)
        {
            case EffectIdParity.Even:
                return baseId + 1;

            case EffectIdParity.Odd:
                return baseId - 1;
        }

        return baseId;
    }

    private int GetGrowDyingEffectPlayCount(int candidateCount)
    {
        int start = Mathf.Max(0, dyingEffectParam.growStartPlayCount);
        int addPerCall = Mathf.Max(0, dyingEffectParam.growAddCountPerCall);
        int max = Mathf.Max(start, dyingEffectParam.growMaxPlayCount);

        int count = start + dyingEffectGrowCallCount * addPerCall;

        count = Mathf.Min(count, max);

        return Mathf.Clamp(count, 0, candidateCount);
    }

    private int GetDyingEffectLevel(int id)
    {
        if (id >= dyingEffectParam.largeMinId &&
            id <= dyingEffectParam.largeMaxId)
        {
            return 2;
        }

        if (id >= dyingEffectParam.mediumMinId &&
            id <= dyingEffectParam.mediumMaxId)
        {
            return 1;
        }

        return 0;
    }

    private int GetDyingEffectLocalBaseId(int id)
    {
        int local = id % dyingEffectSizeOffset;

        if (local < 0)
            local += dyingEffectSizeOffset;

        return local;
    }

    private int GetDyingEffectIdByLocalBaseAndLevel(
        int localBaseId,
        int level)
    {
        return localBaseId + dyingEffectSizeOffset * level;
    }

    private bool IsDyingEffectIdInLevelRange(int id, int level)
    {
        switch (level)
        {
            case 0:
                return id >= dyingEffectParam.smallMinId &&
                       id <= dyingEffectParam.smallMaxId;

            case 1:
                return id >= dyingEffectParam.mediumMinId &&
                       id <= dyingEffectParam.mediumMaxId;

            case 2:
                return id >= dyingEffectParam.largeMinId &&
                       id <= dyingEffectParam.largeMaxId;
        }

        return false;
    }

    private bool IsValidDyingEffectLocalBase(int localBaseId)
    {
        return localBaseId >= 0 &&
               localBaseId < dyingEffectLocalSlotCount;
    }

    private void EnsureDyingEffectRuntime()
    {
        if (dyingEffectRuntimeInitialized)
            return;

        ResetDyingEffectRuntime();

        dyingEffectRuntimeInitialized = true;
    }

    private void ResetDyingEffectRuntime()
    {
        for (int i = 0; i < dyingEffectActiveLevelByLocalBase.Length; i++)
        {
            dyingEffectActiveLevelByLocalBase[i] = -1;
        }

        dyingEffectGrowCallCount = 0;
        dyingEffectRuntimeInitialized = true;
    }

    private void ShuffleList(List<int> list)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            int temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    #endregion

    #region Debug GUI

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!drawStateLabel)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 world = transform.position + stateLabelOffset;
        Vector3 screen = cam.WorldToScreenPoint(world);

        if (screen.z <= 0f)
            return;

        Rect rect = new Rect(
            screen.x - 130f,
            Screen.height - screen.y - 20f,
            260f,
            60f
        );

        GUI.Label(
            rect,
            $"{state} / {slidePhase}\nHP:{hp:F0} Stun:{stunGauge:F0}/{GetCurrentStunGaugeMax():F0} Angry:{isAngry} Side:{currentAnchorSide}"
        );
#endif
    }

    #endregion
}