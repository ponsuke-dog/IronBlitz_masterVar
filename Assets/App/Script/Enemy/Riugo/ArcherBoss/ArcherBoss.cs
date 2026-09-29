using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ArcherBoss : MonoBehaviour, IHitReceiver, IHitSource
{
    #region Enums

    private enum BossState
    {
        Idle,
        StraightShot,
        ArcShot,
        LaneArcBurst,
        LockOnDropShot,
        AngryBarrage,
        MeleeAttack,
        JumpRetreat,
        Anger,
        Stunned,
        Dead
    }

    private enum BossAttackType
    {
        Straight,
        Arc,
        LaneArcBurst,
        LockOnDrop,
        AngryBarrage
    }

    private enum AttackPhase
    {
        None,
        MoveToShootPosition,
        PlayAttackAnimation,
        Spawn,
        WaitEnd
    }

    private enum StunPhase
    {
        None,

        // Downへ入るモーション
        DownAnimation,

        // Down中のループ
        DownLoop,

        // Idleへ戻るモーション
        GetUpAnimation
    }

    private enum JumpPhase
    {
        None,

        // Jumpに入るモーション
        JumpStartAnimation,

        // 空中移動中のループ
        JumpLoopMove,

        // 着地モーション
        LandingAnimation
    }

    private enum AnimationFinishMode
    {
        Timer,
        AnimationEnd,
        AnimationEvent
    }

    private enum StageSide
    {
        PlayerSide,
        BossSide
    }

    private enum ArcTargetHorizontalMode
    {
        PlayerCenter,
        PlayerLaneCenter
    }

    private enum ArrowSpawnHeightMode
    {
        LaneSizedCenter,
        PrefabStraightHeight,
        CellRowCenter
    }

    private enum CloseRangeArcAction
    {
        UseCloseRangeMoveSettings,
        ChangeToAlternativeAttack
    }

    private enum DeadPhase
    {
        None,

        // AB_Death_A中。指定ポイントへ移動する
        DeathA_MoveToPoint,

        // Death_Aの残りをその場で再生する
        DeathA_Wait,

        // AB_Death_B中。爆散アニメーション
        DeathB_Burst,

        // 死亡演出完了
        Finished
    }
    private enum DeathMoveMode
    {
        // AB_Death_Aの終了時間に合わせて目的地へ到達する
        MatchDeathAAnimationTime,

        // 指定速度で移動し、Animation終了またはAnimEventでその場停止
        MoveBySpeedUntilStop
    }

    #endregion

    #region Serializable Params

    [Serializable]
    private class StatusParam
    {
        [Header("HP")]
        public float maxHP = 300f;
        public float angryHPThreshold = 150f;

        [Header("Direct Tackle Damage")]
        public float defaultTackleDamage = 10f;

        [Header("Mission / Kill")]
        public EnemyData enemyData;

        [Header("Death Camera")]
        public CameraManager Camera;
    }

    [Serializable]
    private class AttackPatternParam
    {
        public BossAttackType attackType = BossAttackType.Straight;

        [Range(0f, 100f)]
        public float chancePercent = 50f;

        [Header("Consecutive Limit")]
        public bool useConsecutiveLimit = true;
        public int maxConsecutiveCount = 2;
    }

    [Serializable]
    private class AttackSelectParam
    {
        [Header("Normal Attack Table")]
        public AttackPatternParam[] normalAttacks =
        {
            new AttackPatternParam
            {
                attackType = BossAttackType.Straight,
                chancePercent = 55f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 2
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Arc,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.LaneArcBurst,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.LockOnDrop,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            }

        };

        [Header("Angry Attack Table")]
        public AttackPatternParam[] angryAttacks =
        {
            new AttackPatternParam
            {
                attackType = BossAttackType.Straight,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.Arc,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.LaneArcBurst,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.LockOnDrop,
                chancePercent = 15f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            },
            new AttackPatternParam
            {
                attackType = BossAttackType.AngryBarrage,
                chancePercent = 40f,
                useConsecutiveLimit = true,
                maxConsecutiveCount = 1
            }
        };

        [Header("Special Rules")]
        public bool forceFirstAttackToStraight = true;
        public bool forceAttackAfterJumpToStraight = true;

        [Header("Debug")]
        public bool logAttackSelect = true;
    }

    [Serializable]
    private class TimingParam
    {
        [Header("Idle")]
        public float idleBeforeAttack = 0.8f;
        public float angryIdleBeforeAttack = 0.45f;

        [Header("Attack End")]
        public float straightShotDuration = 0.65f;
        public float arcShotDuration = 0.8f;
        public float angryBarrageDuration = 1.2f;

        [Header("Anger")]
        public float angerDuration = 1.2f;
    }

    [Serializable]
    private class StageLaneParam
    {
        [Header("Stage End Circle Centers")]
        [Tooltip("プレイヤー側の円形部分の中心")]
        public Transform playerSideCircleCenter;

        [Tooltip("ボス側の円形部分の中心")]
        public Transform bossSideCircleCenter;

        [Header("Lane Size")]
        [Tooltip("ステージ横幅")]
        public float stageWidth = 50f;

        [Tooltip("グリッド判定に使う縦方向の最小Y")]
        public float gridMinY = 0.5f;

        [Tooltip("グリッド判定に使う縦方向の最大Y")]
        public float gridMaxY = 8f;

        [Tooltip("Bossの地面Y")]
        public float bossGroundY = 0f;

        [Header("Ground Height")]
        [Tooltip("ONならBoss側円形中心のYを基準の地面Yとして使う")]
        public bool autoGroundYFromBossSideCircleCenter = true;

        [Tooltip("autoGroundYFromBossSideCircleCenterがONの時に加算するY補正")]
        public float groundYOffset = 0f;

        [Header("Boss Body Height")]
        [Tooltip("ONならゲーム開始時のBossのY座標を維持して移動する")]
        public bool keepInitialBossY = true;

        [Tooltip("keepInitialBossYがOFFの時、地面Yからどれくらい上にBoss Rootを置くか")]
        public float bossRootHeightFromGround = 0f;

        [Header("Arrow Direction")]
        [Tooltip("矢の開始位置をBoss側中心よりさらに奥へずらす距離")]
        public float arrowStartBehindBoss = 3f;

        [Tooltip("矢が進む最大距離。0以下ならステージ長さ + extraTravelDistance")]
        public float straightMaxTravelDistance = 0f;

        [Tooltip("ステージ長さに追加する矢の移動距離")]
        public float extraTravelDistance = 12f;

        [Tooltip("ステージ横幅から少し内側へ収める余白")]
        public float sideMargin = 0.5f;

        [Header("Boss Shoot Position")]
        [Tooltip("Bossが射撃位置へ横移動するときの速度")]
        public float bossSideMoveSpeed = 16f;

        [Tooltip("射撃位置へ到達した扱いにする距離")]
        public float bossShootPositionReachDistance = 0.05f;

        [Header("Per Attack Boss Move")]
        [Tooltip("StraightShotはレーンへ横移動してから撃つ")]
        public bool moveBeforeStraightShot = true;

        [Tooltip("ArcShotはその場で撃つ")]
        public bool moveBeforeArcShot = false;

        [Tooltip("LaneArcBurstはその場で撃つ")]
        public bool moveBeforeLaneArcBurst = false;

        [Tooltip("LockOnDropはその場で撃つ")]
        public bool moveBeforeLockOnDrop = false;

        [Tooltip("AngryBarrageは中央へ戻ってから撃つ")]
        public bool moveBeforeAngryBarrage = true;

        [Tooltip("ONならAngryBarrageは選択レーンではなく中央へ移動する")]
        public bool angryBarrageMoveToCenter = true;

        [Header("Debug")]
        public bool drawStageGizmos = true;
        public bool drawGridGizmos = true;
        public int debugGridColumns = 5;
        public int debugGridRows = 2;
    }

    [Serializable]
    private class ArrowSpawnParam
    {
        [Header("References")]
        public ArcherBossArrow arrowPrefab;

        [Header("Layer")]
        public LayerMask playerLayer;

        [Header("Common")]
        public int playerDamage = 1;

        [Header("Size Padding")]
        [Tooltip("1マスを完全に埋めるための横倍率")]
        public float cellWidthScale = 1.0f;

        [Tooltip("1マスを完全に埋めるための縦倍率")]
        public float cellHeightScale = 1.0f;

        [Tooltip("矢の進行方向の厚み")]
        public float arrowDepth = 1.5f;
    }

    [Serializable]
    private class StraightShotParam
    {
        [Header("Prefab")]
        [Tooltip("未設定ならArrowSpawnParamのarrowPrefabを使う")]
        public ArcherBossArrow arrowPrefabOverride;

        [Header("Prefab Size")]
        [Tooltip("ONならStraight専用Prefab登録時、そのPrefabのScaleを尊重してBoss側でサイズ変更しない")]
        public bool preserveOverridePrefabSize = true;

        [Tooltip("専用Prefabが未設定でArrowSpawn共通Prefabを使う時だけ、レーンサイズに合わせて拡縮する")]
        public bool resizeDefaultPrefabToLane = true;

        [Header("Missing Prefab")]
        [Tooltip("Straight用Prefabも共通Prefabも無い場合、別攻撃に切り替える")]
        public bool changeAttackIfPrefabMissing = true;

        [Header("Prefab Position")]
        [Tooltip("専用Prefabのサイズを尊重する時の地面からの発射高さ")]
        public float prefabSpawnHeightFromGround = 2.0f;

        [Header("Normal Straight Arrow")]
        [Tooltip("通常時の横分割数。3推奨")]
        [Range(1, 12)]
        public int laneDivisions = 3;

        [Tooltip("通常時の縦分割数。通常は1")]
        [Range(1, 6)]
        public int rowDivisions = 1;

        [Tooltip("通常時に撃つ矢の数")]
        [Range(1, 12)]
        public int arrowCount = 1;

        [Tooltip("通常時はプレイヤーがいる横Laneを優先する")]
        public bool aimPlayerLane = true;

        [Tooltip("通常時は中央の高さRowを使う")]
        public bool useCenterRow = true;

        [Header("Normal Arrow Height")]
        [Tooltip("ONならNormal矢の高さをPlayerの現在Yまで広げる")]
        public bool expandHeightToPlayerY = true;

        [Tooltip("PlayerのY座標よりどれくらい上まで矢を伸ばすか")]
        public float playerHeightMargin = 1.5f;

        [Tooltip("Normal矢の最低縦サイズ")]
        public float minNormalArrowHeight = 3.0f;

        public float speed = 24f;
        public float lifeTime = 5f;
        public float spawnInterval = 0.05f;
    }

    [Serializable]
    private class ArcShotParam
    {
        [Header("Prefab")]
        [Tooltip("未設定ならArrowSpawnParamのarrowPrefabを使う")]
        public ArcherBossArrow arrowPrefabOverride;

        [Header("Prefab Size")]
        [Tooltip("ONならArc専用Prefab登録時、そのPrefabのScaleを尊重してBoss側でサイズ変更しない")]
        public bool preserveOverridePrefabSize = true;

        [Tooltip("専用Prefabが未設定でArrowSpawn共通Prefabを使う時だけ、レーンサイズに合わせて拡縮する")]
        public bool resizeDefaultPrefabToLane = false;

        [Header("Missing Prefab")]
        [Tooltip("Arc用Prefabも共通Prefabも無い場合、別攻撃に切り替える")]
        public bool changeAttackIfPrefabMissing = true;

        [Header("Arc Arrow")]
        public int arrowCount = 1;

        [Range(1, 12)]
        public int laneDivisions = 3;

        [Range(1, 6)]
        public int rowDivisions = 1;

        [Tooltip("プレイヤーよりBoss側へどれくらい前に落とすか")]
        public float targetForwardOffsetFromPlayer = 20f;

        [Tooltip("発射開始位置の地面からの高さ")]
        public float startHeightFromGround = 2.0f;

        [Tooltip("着弾位置の地面からの高さ")]
        public float targetHeightFromGround = 0.5f;

        [Tooltip("SoccerBallより高めにしたい場合は大きくする")]
        public float arcHeight = 14f;

        [Tooltip("大きいほどゆっくり飛ぶ")]
        public float flightTime = 1.8f;

        public float lifeTime = 5f;

        [Header("Random Count")]
        [Tooltip("ONならArcShotの本数をMin〜Maxからランダムに決める")]
        public bool useRandomArrowCount = true;

        [Tooltip("ArcShotで撃つ最小本数")]
        [Range(1, 3)]
        public int minArrowCount = 1;

        [Tooltip("ArcShotで撃つ最大本数")]
        [Range(1, 3)]
        public int maxArrowCount = 3;

        [Tooltip("横方向ランダムずらし。基本0推奨")]
        public float targetRandomRightRadius = 0f;

        public float spawnInterval = 0.08f;

        [Header("Arc Move")]
        [Tooltip("ONならFlightTimeではなくArcSpeedで飛行時間を自動計算する")]
        public bool useArcSpeed = true;

        [Tooltip("Arc矢の移動速度。useArcSpeedがONの時に使う")]
        public float arcSpeed = 22f;

        [Tooltip("ベジェ曲線長を推定する分割数")]
        [Range(4, 64)]
        public int arcLengthSamples = 16;

        [Header("Close Range Arc")]
        [Tooltip("ONならPlayerがBoss側に近い時、ArcShotの移動設定を変更する")]
        public bool useCloseRangeArcSettings = true;

        [Tooltip("0=Bossと反対側 / 1=Boss側。Playerがこの値以上Boss側に来たら近距離扱い")]
        [Range(0f, 1f)]
        public float closeRangeStartProgress = 0.7f;

        [Tooltip("近距離時の処理")]
        public CloseRangeArcAction closeRangeAction =
            CloseRangeArcAction.UseCloseRangeMoveSettings;

        [Tooltip("近距離時も速度指定で飛ばす")]
        public bool closeUseArcSpeed = false;

        [Tooltip("近距離時のArc速度。closeUseArcSpeedがONの時に使う")]
        public float closeArcSpeed = 12f;

        [Tooltip("近距離時の飛行時間。closeUseArcSpeedがOFFの時に使う")]
        public float closeFlightTime = 1.4f;

        [Tooltip("近距離時の山なり高さ")]
        public float closeArcHeight = 12f;

        [Header("Target Horizontal")]
        [Tooltip("PlayerCenter: プレイヤーの横位置そのもの / PlayerLaneCenter: プレイヤーがいるレーン中央")]
        public ArcTargetHorizontalMode targetHorizontalMode = ArcTargetHorizontalMode.PlayerLaneCenter;

    }

    [Serializable]
    private class LaneArcBurstParam
    {
        [Header("Prefab")]
        [Tooltip("未設定ならArcShotのPrefab、さらに未設定ならArrowSpawnParamのarrowPrefabを使う")]
        public ArcherBossArrow arrowPrefabOverride;

        [Header("Count")]
        [Tooltip("ONならMin〜Maxからランダム本数を同時発射する")]
        public bool useRandomArrowCount = true;

        [Tooltip("固定本数。useRandomArrowCountがOFFの時に使う")]
        [Range(1, 8)]
        public int arrowCount = 3;

        [Tooltip("最小本数")]
        [Range(1, 8)]
        public int minArrowCount = 2;

        [Tooltip("最大本数")]
        [Range(1, 8)]
        public int maxArrowCount = 5;

        [Header("Lane")]
        [Range(1, 12)]
        public int laneDivisions = 5;

        [Range(1, 6)]
        public int rowDivisions = 1;

        [Tooltip("ONなら同じレーンを重複して選ばない")]
        public bool uniqueLane = true;

        [Tooltip("ONなら中央レーンから撃つ。OFFなら各ターゲットレーンの位置から撃つ")]
        public bool fixedStartLane = true;

        [Tooltip("fixedStartLaneがONの時に使う開始レーン。-1なら中央")]
        public int startLane = -1;

        [Header("Target")]
        [Tooltip("プレイヤーよりBoss側へどれくらい前に落とすか")]
        public float targetForwardOffsetFromPlayer = 20f;

        [Tooltip("着弾位置の地面からの高さ")]
        public float targetHeightFromGround = 0.5f;

        [Tooltip("発射開始位置の地面からの高さ")]
        public float startHeightFromGround = 2.0f;

        [Tooltip("横方向ランダムずらし")]
        public float targetRandomRightRadius = 0f;

        [Header("Arc Move")]
        public bool useArcSpeed = true;
        public float arcSpeed = 22f;
        public float arcHeight = 14f;
        public float flightTime = 1.8f;
        public float lifeTime = 5f;

        [Header("Close Range Arc")]
        [Tooltip("ONならPlayerがBoss側に近い時、LaneArcBurstの移動設定を変更する")]
        public bool useCloseRangeArcSettings = true;

        [Tooltip("0=Bossと反対側 / 1=Boss側。Playerがこの値以上Boss側に来たら近距離扱い")]
        [Range(0f, 1f)]
        public float closeRangeStartProgress = 0.7f;

        [Tooltip("近距離時の処理")]
        public CloseRangeArcAction closeRangeAction =
            CloseRangeArcAction.UseCloseRangeMoveSettings;

        [Tooltip("近距離時も速度指定で飛ばす")]
        public bool closeUseArcSpeed = false;

        [Tooltip("近距離時のArc速度。closeUseArcSpeedがONの時に使う")]
        public float closeArcSpeed = 12f;

        [Tooltip("近距離時の飛行時間。closeUseArcSpeedがOFFの時に使う")]
        public float closeFlightTime = 1.4f;

        [Tooltip("近距離時の山なり高さ")]
        public float closeArcHeight = 12f;

        [Header("Prefab Size")]
        public bool preserveOverridePrefabSize = true;
        public bool resizeDefaultPrefabToLane = false;

        [Header("Missing Prefab")]
        public bool changeAttackIfPrefabMissing = true;
    }

    [Serializable]
    private class LockOnDropParam
    {
        [Header("Prefab")]
        [Tooltip("未設定ならArrowSpawnParamのarrowPrefabを使う")]
        public ArcherBossArrow arrowPrefabOverride;

        [Header("Prefab Size")]
        [Tooltip("ONならLockOnDrop専用Prefab登録時、そのPrefabのScaleを尊重してBoss側でサイズ変更しない")]
        public bool preserveOverridePrefabSize = true;

        [Tooltip("専用Prefabが未設定でArrowSpawn共通Prefabを使う時だけ、Arrow Sizeで拡縮する")]
        public bool resizeDefaultPrefabByArrowSize = true;

        [Header("Missing Prefab")]
        [Tooltip("LockOnDrop用Prefabも共通Prefabも無い場合、別攻撃に切り替える")]
        public bool changeAttackIfPrefabMissing = true;

        [Header("Count")]
        [Tooltip("一回の攻撃で落とす矢の数")]
        public int arrowCount = 3;

        [Tooltip("矢を落とす間隔")]
        public float dropInterval = 0.25f;

        [Tooltip("矢を発射する間隔。0なら全弾ほぼ同時に上へ飛ぶ")]
        public float launchInterval = 0f;

        [Tooltip("Rise中もプレイヤー頭上を追う。ON推奨")]
        public bool trackPlayerDuringRise = true;

        [Header("Animation Event")]
        [Tooltip("AnimationEventで1本ずつ撃つ時、Eventの呼び出し順にdropInterval分だけ落下開始をずらす")]
        public bool useEventIndexAsFallOrderDelay = false;

        [Header("Lock On")]
        [Tooltip("ロックオン時、Player位置からBoss側へずらす距離")]
        public float targetForwardOffsetFromPlayer = 0f;

        [Tooltip("ロック位置の横方向ランダム")]
        public float targetRandomRightRadius = 1.0f;

        [Tooltip("ロック位置の地面からの高さ")]
        public float targetHeightFromGround = 0.5f;

        [Header("Rise")]
        [Tooltip("Bossから上空へ飛ぶ時間")]
        public float riseDuration = 0.45f;

        [Tooltip("Bossから上空へ飛ぶ時の山の高さ")]
        public float riseArcHeight = 10f;

        [Tooltip("落下開始位置の地面からの高さ")]
        public float dropHeightFromGround = 18f;

        [Header("Fall")]
        [Tooltip("ロックオンしてから落下開始までの待ち時間")]
        public float lockOnDelay = 0.6f;

        [Tooltip("落下速度")]
        public float fallSpeed = 28f;

        [Tooltip("地面到達扱いにする距離")]
        public float hitDistance = 0.25f;

        public float lifeTime = 5f;

        [Header("Size")]
        public Vector3 arrowSize = new Vector3(1.5f, 1.5f, 1.5f);

        [Header("Debug")]
        public bool logLockOnTarget = true;
    }

    [Serializable]
    private class AngryBarrageParam
    {
        [Header("Prefab")]
        [Tooltip("未設定ならArrowSpawnParamのarrowPrefabを使う")]
        public ArcherBossArrow arrowPrefabOverride;

        [Header("Prefab Size")]
        [Tooltip("ONならAngryBarrage専用Prefab登録時、そのPrefabのScaleを尊重してBoss側でサイズ変更しない")]
        public bool preserveOverridePrefabSize = true;

        [Tooltip("専用Prefabが未設定でArrowSpawn共通Prefabを使う時だけ、マスサイズに合わせて拡縮する")]
        public bool resizeDefaultPrefabToCell = true;

        [Header("Missing Prefab")]
        [Tooltip("AngryBarrage用Prefabも共通Prefabも無い場合、別攻撃に切り替える")]
        public bool changeAttackIfPrefabMissing = true;

        [Header("Grid")]
        [Tooltip("怒り時の横分割。5推奨")]
        [Range(1, 12)]
        public int laneDivisions = 5;

        [Tooltip("怒り時の縦分割。2推奨")]
        [Range(1, 6)]
        public int rowDivisions = 2;

        [Header("Arrow Count")]
        public int minArrowCount = 5;
        public int maxArrowCount = 8;

        [Header("Move")]
        public float speed = 28f;
        public float lifeTime = 5f;
        public float spawnInterval = 0.035f;

        [Header("Size")]
        [Tooltip("怒り弾幕矢の横幅倍率")]
        public float cellWidthScale = 0.92f;

        [Tooltip("怒り弾幕矢の高さ倍率。上段をくぐれるように0.6〜0.75推奨")]
        public float cellHeightScale = 0.65f;

        [Tooltip("怒り弾幕矢の奥行き")]
        public float arrowDepth = 1.5f;

        [Header("Lane Approach")]
        [Tooltip("ONならAngryBarrageの矢が一度自分のレーン中心へ向かい、到達後まっすぐ進む")]
        public bool useLaneApproach = true;

        [Header("Muzzle Approach")]
        [Tooltip("ONならAngryBarrageの開始位置にMuzzleを使う")]
        public bool useMuzzleStart = true;

        [Tooltip("AngryBarrage Muzzleが未設定の場合、Straight Muzzleを使う")]
        public bool fallbackToStraightMuzzle = true;

        [Tooltip("ONならAngryBarrageのY座標はMuzzleの高さを維持する")]
        public bool keepMuzzleHeight = true;

        [Tooltip("ONならLaneApproach中に選択されたRowの高さへ補正する")]
        public bool approachToLaneHeight = true;

        [Tooltip("ONならLaneApproachの補正先をステージ中心ではなくMuzzle前方基準にする")]
        public bool approachFromMuzzleForward = true;

        [Tooltip("ONなら距離ではなく時間でレーン補正を終える。全矢のStraight開始タイミングを揃える")]
        public bool syncLaneApproachByDuration = true;

        [Tooltip("レーン補正にかける時間。全矢がこの秒数後にStraightへ移行する")]
        public float laneApproachDuration = 0.25f;

        [Tooltip("自分のレーンへ向かう速度")]
        public float laneApproachSpeed = 36f;

        [Tooltip("レーン到達扱いにする距離")]
        public float laneApproachReachDistance = 0.2f;

        [Tooltip("自分のレーンへ向かう時、レーン中心よりどれくらい前方へ進ませるか")]
        public float laneApproachForwardLeadDistance = 5f;

        [Tooltip("レーンへ寄っている最中も、矢の見た目を最終進行方向へ向ける")]
        public bool faceFinalDirectionDuringApproach = true;

        [Header("Boss Move")]
        [Tooltip("怒り時は選択された最初のマスの横レーンへ移動してから撃つ")]
        public bool moveBossToFirstSelectedLane = true;
    }

    [Serializable]
    private class MeleeParam
    {
        [Header("Trigger")]
        [Tooltip("ONならPlayerがBoss側の円形エリアに入った時に近接攻撃を行う")]
        public bool enableMeleeWhenPlayerEnterBossCircle = true;

        [Tooltip("円形エリアの半径。0以下ならStageWidthの半分を使う")]
        public float bossCircleRadius = 8f;

        [Tooltip("近接攻撃の再発動までのクールタイム")]
        public float cooldown = 2.0f;

        [Header("Temporary Spin Attack")]
        [Tooltip("仮近接攻撃としてその場で一回転する時間")]
        public float spinDuration = 0.6f;

        [Tooltip("一回転の角度。360で一回転")]
        public float spinAngle = 360f;

        [Tooltip("回転中にプレイヤーの方を向き直さない")]
        public bool lockRotationDuringSpin = true;

        [Header("After Attack")]
        [Tooltip("近接攻撃後にStunnedへ入る")]
        public bool enterStunAfterMelee = true;

        [Tooltip("ONならMelee完了後はStunDownを再生せず、直接StunLoopへ入る")]
        public bool enterStunLoopDirectlyAfterMeleeComplete = true;

        [Tooltip("近接攻撃後のStun終了後、反対側へジャンプする")]
        public bool jumpOppositeAfterMeleeStun = true;

        [Header("Interrupt")]
        [Tooltip("ONならIdle以外の攻撃中でも、PlayerがBoss円内に入った時に近接攻撃へ移行する")]
        public bool interruptAttackWithMelee = true;

        [Tooltip("近接攻撃に入る時、残っている矢を消す")]
        public bool clearActiveArrowsOnMelee = false;

        [Header("Debug Check")]
        [Tooltip("近接判定の距離ログを出す")]
        public bool logMeleeCheck = false;

        [Header("Debug")]
        public bool logMelee = true;
    }

    [Serializable]
    private class JumpParam
    {
        [Header("Jump Retreat")]
        [Tooltip("Bossがジャンプで移動する円形中心候補。両端の円形中心などを登録")]
        public Transform[] jumpCircleCenters;

        public float jumpDuration = 0.8f;
        public float jumpHeight = 5f;

        public bool facePlayerAfterJump = true;
        public bool disableHitboxesWhileJumping = true;

        [Header("Landing Facing")]
        [Tooltip("ONなら着地モーション終盤でBossのRoot方向を道路正面へ補正する")]
        public bool alignFacingDuringLanding = true;

        [Tooltip("着地モーションの何割まで進んだら向き補正するか")]
        [Range(0f, 1f)]
        public float landingAlignNormalizedTime = 0.85f;

        [Tooltip("ONなら着地終了後も保険として向きを再補正する")]
        public bool alignFacingOnLandingEnd = true;

        [Header("Landing Facing Lock")]
        [Tooltip("ONなら着地終盤からIdle開始直後までRoot回転を固定する")]
        public bool lockFacingAroundLanding = true;

        [Tooltip("Idleへ入った後、何フレームだけ向きを固定するか")]
        [Range(0, 10)]
        public int landingFacingLockFramesAfterIdle = 2;

        [Tooltip("着地後のIdleをCrossFadeではなく即時再生する")]
        public bool playIdleImmediatelyAfterLanding = true;
    }

    [Serializable]
    private class StunParam
    {
        [Header("Stun Effect Point")]
        [Tooltip("スタンエフェクト位置")]
        public Transform stunEffectPoint;

        [Header("Duration")]
        [Tooltip("通常状態でのStun時間")]
        public float normalDuration = 3.0f;

        [Tooltip("怒り状態でのStun時間")]
        public float angryDuration = 2.0f;

        [Tooltip("Stun中もタックル被弾HitboxをONにする")]
        public bool keepDamageHitboxActive = true;

        [Tooltip("Stunに入った瞬間、残っている矢を消す")]
        public bool clearActiveArrowsOnEnter = false;

        [Tooltip("Stun終了後、Jumpへ入る直前に残っている矢を消す")]
        public bool clearActiveArrowsBeforeJump = true;

        [Tooltip("Stun終了後、ステージ反対側へジャンプする")]
        public bool jumpToOppositeSideOnEnd = true;

        [Tooltip("Angry移行予約中はStun中の追加ダメージを受けないようにする")]
        public bool disableDamageHitboxOnAngerTransition = true;

        [Header("Debug")]
        public bool logStun = true;
    }

    [Serializable]
    private class HitboxParam
    {
        [Header("Direct Tackle Receive")]
        public GameObject[] directTackleReceiveHitboxes;

        public bool disableHitboxesWhenDead = true;
        public float directHitCooldown = 0.12f;

        [Header("Melee Attack")]
        [Tooltip("Melee攻撃中にON/OFFする攻撃判定")]
        public GameObject[] meleeAttackHitboxes;

        [Tooltip("Melee攻撃でPlayerへ与えるダメージ")]
        public int meleePlayerDamage = 1;

        [Tooltip("1回のMelee中、同じ対象に1回だけ当てる")]
        public bool hitSameTargetOncePerMelee = true;

        [Header("Layer Check")]
        public LayerMask playerAttackLayer;

        [Tooltip("Melee攻撃が当たる対象Layer。0ならLayer判定なし")]
        public LayerMask meleeTargetLayer;

        [Header("Debug")]
        public bool logHitboxSwitch = true;
    }

    [Serializable]
    private class AnimationEventParam
    {
        [Header("Spawn By Animation Event")]
        public bool spawnStraightByAnimationEvent = false;
        public bool spawnArcByAnimationEvent = false;
        public bool spawnAngryBarrageByAnimationEvent = false;
        public bool spawnLockOnDropByAnimationEvent = false;
    }

    [Serializable]
    private class StateAnimationFinishParam
    {
        public AnimationFinishMode finishMode = AnimationFinishMode.Timer;
        public float duration = 1.0f;

        [Range(0f, 1.2f)]
        public float endNormalizedTime = 0.95f;
    }

    [Serializable]
    private class AnimationParam
    {
        [Header("Attack Finish")]
        public StateAnimationFinishParam straightShot = new StateAnimationFinishParam();
        public StateAnimationFinishParam arcShot = new StateAnimationFinishParam();
        public StateAnimationFinishParam angryBarrage = new StateAnimationFinishParam();

        [Header("Melee Finish")]
        public StateAnimationFinishParam melee = new StateAnimationFinishParam();

        [Header("Stun Phase Finish")]
        public StateAnimationFinishParam stunDown = new StateAnimationFinishParam();
        public StateAnimationFinishParam stunGetUp = new StateAnimationFinishParam();

        [Header("Jump Phase Finish")]
        public StateAnimationFinishParam jumpStart = new StateAnimationFinishParam();
        public StateAnimationFinishParam jumpLanding = new StateAnimationFinishParam();

        [Header("Anger Finish")]
        public StateAnimationFinishParam anger = new StateAnimationFinishParam();

        [Header("Dead Finish")]
        public StateAnimationFinishParam deathA = new StateAnimationFinishParam();
        public StateAnimationFinishParam deathB = new StateAnimationFinishParam();
    }

    [Serializable]
    private class BossAnimationParam
    {
        [Header("Animator State Names")]
        public string idle = "AB_Idle";

        [Header("Shot")]
        public string straightShot = "AB_StraightShot";
        public string arcShot1 = "AB_ArcShot_1";
        public string arcShot2 = "AB_ArcShot_2";
        public string arcShot3 = "AB_ArcShot_3";
        public string dropShot = "AB_DropShot";

        [Header("Reuse")]
        [Tooltip("AngryBarrageで使うアニメーション。基本はAB_StraightShot")]
        public string angryBarrage = "AB_StraightShot";

        [Tooltip("LaneArcBurstで使うアニメーション。基本はAB_ArcShot_1")]
        public string laneArcBurst = "AB_ArcShot_1";

        [Header("Melee")]
        public string melee = "AB_Melee";

        [Header("Jump")]
        public string jumpStart = "AB_Jump_A";
        public string jumpLoop = "AB_Jump_B";
        public string jumpLanding = "AB_Jump_C";

        [Header("Stun")]
        public string stunDown = "AB_Down_A";
        public string stunLoop = "AB_Down_B";
        public string stunGetUp = "AB_Down_C";

        [Header("Anger")]
        public string anger = "AB_Angry";

        [Header("Dead")]
        public string deathA = "AB_Death_A";
        public string deathB = "AB_Death_B";

        [Header("Animator Params")]
        public string angryBool = "IsAngry";

        [Header("Blend")]
        public float defaultBlendTime = 0.05f;
        public float idleBlendTime = 0.08f;

        public float shotBlendTime = 0.05f;
        public float meleeBlendTime = 0.05f;

        public float jumpStartBlendTime = 0.05f;
        public float jumpLoopBlendTime = 0.05f;
        public float jumpLandingBlendTime = 0.05f;

        public float stunDownBlendTime = 0.05f;
        public float stunLoopBlendTime = 0.05f;
        public float stunGetUpBlendTime = 0.05f;

        public float angerBlendTime = 0.05f;
        public float deadBlendTime = 0.05f;

        [Header("Restart")]
        public bool forceRestartAnimations = true;

        [Header("Layer")]
        public int animatorLayerIndex = 0;
        public string animatorLayerName = "Base Layer";
        public bool tryFullPathStateName = true;
    }

    [Serializable]
    private class ArrowMuzzleParam
    {
        [Header("Common Muzzle")]
        [Tooltip("全攻撃共通の発射基準点。未設定なら従来のステージ中心基準を使う")]
        public Transform defaultMuzzle;

        [Header("Attack Muzzles")]
        [Tooltip("Straight用の発射基準点。未設定ならDefault Muzzleを使う")]
        public Transform straightMuzzle;

        [Tooltip("Arc用の発射基準点。未設定ならDefault Muzzleを使う")]
        public Transform arcMuzzle;

        [Tooltip("LockOnDrop用の発射基準点。未設定ならDefault Muzzleを使う")]
        public Transform lockOnDropMuzzle;

        [Tooltip("AngryBarrage用の発射基準点。未設定ならDefault Muzzleを使う")]
        public Transform angryBarrageMuzzle;

        [Header("Effect Point")]
        [Tooltip("チャージエフェクトの発生位置")]
        public Transform effectPoint;

        [Header("Offset")]
        [Tooltip("Muzzle使用時にローカル座標で加える補正")]
        public Vector3 localOffset = Vector3.zero;

        [Header("Fallback")]
        [Tooltip("Muzzle未設定時は従来通りステージ側の円中心から撃つ")]
        public bool useStageCenterIfMuzzleMissing = true;
    }

    [Serializable]
    private class DeathParam
    {
        [Header("Death Target Points")]
        [Tooltip("Player側ステージで死亡した時に向かう点")]
        public Transform playerSideDeathPoint;

        [Tooltip("Boss側ステージで死亡した時に向かう点")]
        public Transform bossSideDeathPoint;

        [Tooltip("上記が未設定だった時の予備")]
        public Transform fallbackDeathPoint;

        [Header("Death A Move")]
        [Tooltip("Death_A中の移動方式")]
        public DeathMoveMode moveMode = DeathMoveMode.MatchDeathAAnimationTime;

        [Tooltip("Death_Aの全体時間。MatchDeathAAnimationTime時はこの秒数で目的地へ移動する")]
        public float deathAMoveDuration = 2.0f;

        [Tooltip("MoveBySpeedUntilStop時の移動速度")]
        public float deathAMoveSpeed = 5.0f;

        [Tooltip("目的地に到達した扱いにする距離")]
        public float reachDistance = 0.05f;

        [Tooltip("移動停止後はその場で残りDeath_Aを再生する")]
        public bool stayAtCurrentPositionAfterMoveStop = true;

        [Tooltip("Death_A中の向きを固定する")]
        public bool lockRotationDuringDeathA = true;

        [Tooltip("Death_A中の向き。ONなら死亡開始時の向きを維持する")]
        public bool keepStartRotation = true;

        [Header("Death B / Emission")]
        [Tooltip("Death_Bで使用する白Emission Material")]
        public Material whiteEmissionMaterial;

        [Tooltip("未設定なら子Rendererを自動取得する")]
        public bool autoCollectRenderers = true;

        [Tooltip("手動で対象Rendererを指定したい場合に使用")]
        public Renderer[] renderers;

        [Tooltip("ONならRenderer内の全Material Slotを白Emission Materialへ差し替える")]
        public bool replaceAllMaterialSlots = true;

        [Tooltip("replaceAllMaterialSlotsがOFFの時、差し替えるMaterial Index")]
        public int targetMaterialIndex = 0;

        [Tooltip("白Emissionへ変化する秒数。AnimEvent_StartDeathWhiteEmissionからこの時間で白くなる")]
        public float whiteEmissionFadeDuration = 0.8f;

        [ColorUsage(true, true)]
        [Tooltip("最終的な白Emission色。Bloomを使うならHDRで強め")]
        public Color finalWhiteColor = new Color(6f, 6f, 6f, 1f);

        [Tooltip("白Emission開始時のIntensity")]
        public float startIntensity = 0f;

        [Tooltip("白Emission完了時のIntensity")]
        public float endIntensity = 8f;

        [Range(0f, 1f)]
        [Tooltip("白Emission開始時のAlpha")]
        public float startAlpha = 0f;

        [Range(0f, 1f)]
        [Tooltip("白Emission完了時のAlpha")]
        public float endAlpha = 1f;

        [Tooltip("FullEmission Shader側のEmission色プロパティ名")]
        public string emissionColorProperty = "_EmissionColor";

        [Tooltip("FullEmission Shader側のIntensityプロパティ名")]
        public string intensityProperty = "_Intensity";

        [Tooltip("FullEmission Shader側のAlphaプロパティ名")]
        public string alphaProperty = "_Alpha";

        [Tooltip("白化カーブ")]
        public AnimationCurve whiteEmissionCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Finish")]
        [Tooltip("Death_B終了後にRendererを非表示にする")]
        public bool hideRenderersOnFinish = true;

        [Tooltip("Death_B終了後にGameObjectをDestroyする")]
        public bool destroyOnFinish = false;

        [Tooltip("Destroyする場合の遅延")]
        public float destroyDelay = 0.5f;

        [Header("Target Filter")]
        [Tooltip("名前にこの文字列を含むRendererは除外する")]
        public string[] excludeNameContains =
        {
        "Arrow",
        "Hitbox",
        "Collision",
        "Muzzle"
    };

        [Header("Debug")]
        public bool logDeath = true;
    }

    [Serializable]
    private class DebugVisualParam
    {
        [Header("Temporary Model")]
        public bool createDebugCapsuleIfNoRenderer = true;

        public Vector3 debugCapsuleScale = new Vector3(1.6f, 2.4f, 1.6f);
        public Vector3 debugCapsuleLocalPosition = new Vector3(0f, 1.2f, 0f);
    }

    private struct GridCell
    {
        public int lane;
        public int row;

        public GridCell(int lane, int row)
        {
            this.lane = lane;
            this.row = row;
        }
    }

    #endregion

    #region Inspector Fields

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private TimeAgent timeAgent;

    [Header("Start Control")]
    [SerializeField] private bool waitForExternalStart = true;
    [SerializeField] private bool playIdleWhileWaitingStart = true;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Status")]
    [SerializeField] private StatusParam statusParam = new StatusParam();

    [Header("Attack Select")]
    [SerializeField] private AttackSelectParam attackSelectParam = new AttackSelectParam();

    [Header("Timing")]
    [SerializeField] private TimingParam timingParam = new TimingParam();

    [Header("Stage Lane")]
    [SerializeField] private StageLaneParam stageLaneParam = new StageLaneParam();

    [Header("Arrow Spawn")]
    [SerializeField] private ArrowSpawnParam arrowSpawnParam = new ArrowSpawnParam();

    [Header("Arrow Muzzle")]
    [SerializeField] private ArrowMuzzleParam arrowMuzzleParam = new ArrowMuzzleParam();

    [Header("Straight Shot")]
    [SerializeField] private StraightShotParam straightShotParam = new StraightShotParam();

    [Header("Arc Shot")]
    [SerializeField] private ArcShotParam arcShotParam = new ArcShotParam();

    [Header("Lane Arc Burst")]
    [SerializeField] private LaneArcBurstParam laneArcBurstParam = new LaneArcBurstParam();

    [Header("Lock On Drop Shot")]
    [SerializeField] private LockOnDropParam lockOnDropParam = new LockOnDropParam();

    [Header("Angry Barrage")]
    [SerializeField] private AngryBarrageParam angryBarrageParam = new AngryBarrageParam();

    [Header("Jump")]
    [SerializeField] private JumpParam jumpParam = new JumpParam();

    [Header("Melee")]
    [SerializeField] private MeleeParam meleeParam = new MeleeParam();

    [Header("Stun")]
    [SerializeField] private StunParam stunParam = new StunParam();

    [Header("Hitbox")]
    [SerializeField] private HitboxParam hitboxParam = new HitboxParam();

    [Header("Death")]
    [SerializeField] private DeathParam deathParam = new DeathParam();

    [Header("Animation Event")]
    [SerializeField] private AnimationEventParam animationEventParam = new AnimationEventParam();

    [Header("Animation Finish")]
    [SerializeField] private AnimationParam animationParam = new AnimationParam();

    [Header("Animation Names")]
    [SerializeField] private BossAnimationParam bossAnimationParam = new BossAnimationParam();

    [Header("Material Flash")]
    [Tooltip("被弾のFlash")]
    [SerializeField] private MaterialFlashPlayer hitMaterialFlashPlayer;

    [Header("Debug Visual")]
    [SerializeField] private DebugVisualParam debugVisualParam = new DebugVisualParam();

    [Header("Debug")]
    [SerializeField] private bool logState = true;
    [SerializeField] private bool logHit = true;

    #endregion

    #region Runtime Fields

    private readonly List<ArcherBossArrow> activeArrows =
        new List<ArcherBossArrow>();

    private readonly List<GridCell> pendingCells =
        new List<GridCell>();

    private BossState state;
    private AttackPhase attackPhase;

    private float stateTimer;
    private bool animationEventFinished;

    private float hp;
    private bool isAngry;
    private bool bossStarted;
    private bool initialized;

    private BossAttackType lastAttackType;
    private int consecutiveAttackCount;
    private bool hasLastAttackType;

    private bool hasSelectedNormalAttack;
    private bool hasSelectedAngryAttack;
    private bool forceNextAttackStraight;

    private bool spawnedArrowsThisState;

    private bool jumpToOppositeSideAfterStun;

    private int currentArcStartLane;

    private int lockOnDropEventSpawnIndex;

    private float initialBossY;

    private float lastDirectHitTime = -999f;

    private Vector3 shootMoveTargetPosition;

    private Vector3 jumpStartPosition;
    private Vector3 jumpEndPosition;
    private float jumpTimer;
    private bool landingFacingAligned;
    private bool landingFacingLockActive;
    private Quaternion landingLockedRotation;
    private int landingFacingLockFramesRemaining;
    private bool enterIdleFromLanding;
    private bool pendingAngerAfterJumpLanding;

    private float lastMeleeTime = -999f;
    private Quaternion meleeStartRotation;
    private Quaternion meleeEndRotation;

    private StageSide currentStageSide = StageSide.BossSide;
    private StageSide jumpTargetStageSide = StageSide.BossSide;

    private StunPhase stunPhase = StunPhase.None;
    private float stunPhaseTimer;
    private EffectInstance stunEffectInstance;

    private JumpPhase jumpPhase = JumpPhase.None;
    private float jumpPhaseTimer;

    private int currentArcArrowCount = 1;

    private readonly HashSet<GameObject> meleeHitTargets =
        new HashSet<GameObject>();

    private bool enterStunLoopDirectlyFromMeleeComplete;

    private float localTime;

    public float CurrentHP => hp;
    public float MaxHP => statusParam.maxHP;

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

    public bool WaitForExternalStart => waitForExternalStart;
    public bool HasBossStarted => bossStarted;

    public bool ShouldShowBossUI
    {
        get
        {
            if (!waitForExternalStart)
                return true;

            return bossStarted;
        }
    }

    public bool IsStunnedForUI
    {
        get
        {
            if (state != BossState.Stunned)
                return false;

            return stunPhase == StunPhase.DownAnimation ||
                   stunPhase == StunPhase.DownLoop;
        }
    }

    public float StunDisplayRate
    {
        get
        {
            if (state != BossState.Stunned)
                return 0f;

            switch (stunPhase)
            {
                case StunPhase.DownAnimation:
                    // Stunに入った直後は満タン表示
                    return 1f;

                case StunPhase.DownLoop:
                    {
                        float duration = GetCurrentStunLoopDuration();

                        if (duration <= 0f)
                            return 0f;

                        // 残り時間なので 1 → 0 に減る
                        return Mathf.Clamp01(1f - stunPhaseTimer / duration);
                    }

                case StunPhase.GetUpAnimation:
                    // 起き上がり中はStun残りなし扱い
                    return 0f;
            }

            return 0f;
        }
    }

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    private float ScaledDeltaTime => Time.deltaTime * TimeScale;

    private DeadPhase deadPhase = DeadPhase.None;
    private float deadPhaseTimer;

    private Vector3 deathMoveStartPosition;
    private Vector3 deathMoveTargetPosition;
    private Quaternion deathStartRotation;

    private bool deathMoveStopped;
    private bool deathWhiteEmissionActive;
    private float deathWhiteEmissionTimer;

    private class DeathEmissionCache
    {
        public Renderer renderer;
        public Material[] originalSharedMaterials;
        public Material[] runtimeEmissionMaterials;
    }

    private readonly List<DeathEmissionCache> deathEmissionCaches =
        new List<DeathEmissionCache>();

    private bool deathEmissionInitialized;


    private EffectPlayer effectPlayer;
    private EffectBonePlayer effectBonePlayer;
    private AudioManager audioManager;

    #endregion

    #region Unity Events

    private void Awake()
    {
        Initialize();

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>();

        if (effectBonePlayer == null)
            effectBonePlayer = GetComponentInChildren<EffectBonePlayer>();

        stunEffectInstance = null;
    }

    private void Start()
    {
        if (!waitForExternalStart)
        {
            StartBoss();
            return;
        }

        bossStarted = false;

        audioManager = AudioManager.Instance;

        if (playIdleWhileWaitingStart)
        {
            ResumeAnimatorFromExternalWait();
            PlayBossAnimation(
                bossAnimationParam.idle,
                bossAnimationParam.idleBlendTime,
                false
            );
            effectBonePlayer.PlayEffect(0);
            effectBonePlayer.PlayEffect(1);
            effectBonePlayer.PlayEffect(2);
            effectBonePlayer.PlayEffect(3);
            effectBonePlayer.PlayEffect(4);
        }
        else
        {
            PauseAnimatorForExternalWait();
        }
    }

    private void OnDestroy()
    {
        ClearArrowEvents();
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

        stateTimer += dt;

        if (TryStartMeleeAttack())
            return;

        switch (state)
        {
            case BossState.Idle:
                UpdateIdle(dt);
                break;

            case BossState.StraightShot:
                UpdateStraightShot(dt);
                break;

            case BossState.ArcShot:
                UpdateArcShot(dt);
                break;

            case BossState.LaneArcBurst:
                UpdateLaneArcBurst(dt);
                break;

            case BossState.LockOnDropShot:
                UpdateLockOnDropShot(dt);
                break;

            case BossState.AngryBarrage:
                UpdateAngryBarrage(dt);
                break;

            case BossState.JumpRetreat:
                UpdateJumpRetreat(dt);
                break;

            case BossState.MeleeAttack:
                UpdateMeleeAttack(dt);
                break;

            case BossState.Stunned:
                UpdateStunned(dt);
                break;

            case BossState.Anger:
                UpdateAnger(dt);
                break;
        }
    }

    private void LateUpdate()
    {
        UpdateLandingFacingLock();
    }

    private void OnDrawGizmosSelected()
    {
        DrawStageGizmos();
    }

    #endregion

    #region Initialize

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        initialBossY = transform.position.y;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (timeAgent == null)
            timeAgent = GetComponent<TimeAgent>();

        if (hitMaterialFlashPlayer == null)
            hitMaterialFlashPlayer = GetComponentInChildren<MaterialFlashPlayer>();

        hp = statusParam.maxHP;

        CreateDebugCapsuleIfNeeded();

        ApplyNormalHitboxMode();
        SetMeleeAttackHitboxesActive(false);

        state = BossState.Idle;
        attackPhase = AttackPhase.None;
        stateTimer = 0f;

        InitializeCurrentStageSideFromPosition();
    }

    private void CreateDebugCapsuleIfNeeded()
    {
        if (!debugVisualParam.createDebugCapsuleIfNoRenderer)
            return;

        Renderer renderer = GetComponentInChildren<Renderer>();

        if (renderer != null)
            return;

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Debug_ArcherBoss_Capsule";
        capsule.transform.SetParent(transform);
        capsule.transform.localPosition = debugVisualParam.debugCapsuleLocalPosition;
        capsule.transform.localRotation = Quaternion.identity;
        capsule.transform.localScale = debugVisualParam.debugCapsuleScale;

        Collider col = capsule.GetComponent<Collider>();

        if (col != null)
            Destroy(col);
    }

    #endregion

    #region Public API

    public void StartBoss()
    {
        Initialize();

        bossStarted = true;
        ResumeAnimatorFromExternalWait();

        ChangeState(BossState.Idle);

        if (logState)
            Debug.Log($"{name} ArcherBoss Started");
    }

    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    #endregion

    #region State Machine

    private void ChangeState(BossState next)
    {
        ExitState(state);

        state = next;
        stateTimer = 0f;
        animationEventFinished = false;
        spawnedArrowsThisState = false;
        attackPhase = AttackPhase.None;
        pendingCells.Clear();

        if (logState)
            Debug.Log($"{name} ArcherBoss State => {next}");

        EnterState(next);
    }

    private void EnterState(BossState next)
    {
        switch (next)
        {
            case BossState.Idle:
                EnterIdle();
                break;

            case BossState.StraightShot:
                EnterStraightShot();
                break;

            case BossState.ArcShot:
                EnterArcShot();
                break;

            case BossState.LaneArcBurst:
                EnterLaneArcBurst();
                break;

            case BossState.LockOnDropShot:
                EnterLockOnDropShot();
                break;

            case BossState.AngryBarrage:
                EnterAngryBarrage();
                break;

            case BossState.JumpRetreat:
                EnterJumpRetreat();
                break;

            case BossState.MeleeAttack:
                EnterMeleeAttack();
                break;

            case BossState.Stunned:
                EnterStunned();
                break;

            case BossState.Anger:
                EnterAnger();
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
            case BossState.MeleeAttack:
                StopMeleeAttackHitboxes();
                break;

            case BossState.JumpRetreat:
                jumpPhase = JumpPhase.None;
                ApplyNormalHitboxMode();
                break;

            case BossState.Stunned:
                stunPhase = StunPhase.None;

                if (stunEffectInstance != null)
                {
                    stunEffectInstance.StopImmediate();
                    stunEffectInstance = null;
                }

                if (!stunParam.keepDamageHitboxActive)
                    ApplyNormalHitboxMode();

                break;
        }
    }

    #endregion

    #region Idle

    private void EnterIdle()
    {
        ApplyNormalHitboxMode();

        if (enterIdleFromLanding && jumpParam.playIdleImmediatelyAfterLanding)
        {
            PlayBossAnimation(
                bossAnimationParam.idle,
                0f,
                true
            );

            enterIdleFromLanding = false;
            return;
        }

        effectBonePlayer.PlayEffect(0);
        effectBonePlayer.PlayEffect(1);
        effectBonePlayer.PlayEffect(2);
        effectBonePlayer.PlayEffect(3);
        effectBonePlayer.PlayEffect(4);

        enterIdleFromLanding = false;

        PlayBossAnimation(
            bossAnimationParam.idle,
            bossAnimationParam.idleBlendTime,
            false
        );
    }

    private void UpdateIdle(float dt)
    {
        float wait = isAngry
            ? timingParam.angryIdleBeforeAttack
            : timingParam.idleBeforeAttack;

        if (stateTimer < wait)
            return;

        SelectNextAttack();
    }

    #endregion

    #region Straight Shot

    private void EnterStraightShot()
    {
        if (!TryResolveStraightArrowPrefab(out _, out _))
        {
            if (straightShotParam.changeAttackIfPrefabMissing)
            {
                ChangeToAlternativeAttack(BossAttackType.Straight);
                return;
            }

            ChangeState(BossState.Idle);
            return;
        }

        ApplyNormalHitboxMode();

        PrepareNormalStraightCells();

        int lane = pendingCells.Count > 0 ? pendingCells[0].lane : 0;

        shootMoveTargetPosition = GetBossShootPositionForLane(
            lane,
            straightShotParam.laneDivisions,
            currentStageSide
        );

        BeginAttackPhase(stageLaneParam.moveBeforeStraightShot);
    }

    private void UpdateStraightShot(float dt)
    {
        UpdateAttackPhase(
            dt,
            SpawnStraightShot,
            animationParam.straightShot,
            timingParam.straightShotDuration
        );
    }

    private void PrepareNormalStraightCells()
    {
        pendingCells.Clear();

        int divisions = Mathf.Max(1, straightShotParam.laneDivisions);
        int rows = Mathf.Max(1, straightShotParam.rowDivisions);
        int count = Mathf.Max(1, straightShotParam.arrowCount);

        if (straightShotParam.aimPlayerLane && playerTarget != null)
        {
            int playerLane = GetLaneIndexFromPosition(
                playerTarget.position,
                divisions
            );

            int row = straightShotParam.useCenterRow
                ? rows / 2
                : 0;

            pendingCells.Add(new GridCell(playerLane, row));
        }

        while (pendingCells.Count < count)
        {
            int lane = UnityEngine.Random.Range(0, divisions);
            int row = straightShotParam.useCenterRow
                ? rows / 2
                : UnityEngine.Random.Range(0, rows);

            GridCell cell = new GridCell(lane, row);

            if (!ContainsCell(pendingCells, cell))
                pendingCells.Add(cell);
        }
    }

    private void SpawnStraightShot()
    {
        if (spawnedArrowsThisState)
            return;

        spawnedArrowsThisState = true;

        int divisions = Mathf.Max(1, straightShotParam.laneDivisions);
        int rows = Mathf.Max(1, straightShotParam.rowDivisions);

        for (int i = 0; i < pendingCells.Count; i++)
        {
            SpawnStraightCellArrowDelayed(
                pendingCells[i],
                divisions,
                rows,
                straightShotParam.speed,
                straightShotParam.lifeTime,
                i * straightShotParam.spawnInterval,
                true
            );
        }
    }

    #endregion

    #region Arc Shot

    private void EnterArcShot()
    {
        if (!TryResolveArcArrowPrefab(out _, out _))
        {
            if (arcShotParam.changeAttackIfPrefabMissing)
            {
                ChangeToAlternativeAttack(BossAttackType.Arc);
                return;
            }

            ChangeState(BossState.Idle);
            return;
        }

        ApplyNormalHitboxMode();

        if (arcShotParam.useCloseRangeArcSettings &&
             arcShotParam.closeRangeAction == CloseRangeArcAction.ChangeToAlternativeAttack &&
             IsPlayerCloseToCurrentBossSide(arcShotParam.closeRangeStartProgress))
        {
            ChangeToAlternativeAttack(BossAttackType.Arc);
            return;
        }

        PrepareArcCells();

        int lane = pendingCells.Count > 0 ? pendingCells[0].lane : 0;

        shootMoveTargetPosition = GetBossShootPositionForLane(
            lane,
            arcShotParam.laneDivisions,
            currentStageSide
        );

        BeginAttackPhase(stageLaneParam.moveBeforeArcShot);
    }

    private void UpdateArcShot(float dt)
    {
        UpdateAttackPhase(
            dt,
            SpawnArcShot,
            animationParam.arcShot,
            timingParam.arcShotDuration
        );
    }

    private void PrepareArcCells()
    {
        pendingCells.Clear();

        int divisions = Mathf.Max(1, arcShotParam.laneDivisions);
        int rows = Mathf.Max(1, arcShotParam.rowDivisions);
        int count = ResolveArcArrowCount();

        currentArcArrowCount = count;

        int baseLane = playerTarget != null
            ? GetLaneIndexFromPosition(playerTarget.position, divisions)
            : divisions / 2;

        currentArcStartLane = baseLane;

        for (int i = 0; i < count; i++)
        {
            int lane = Mathf.Clamp(
                baseLane + UnityEngine.Random.Range(-1, 2),
                0,
                divisions - 1
            );

            int row = rows / 2;

            GridCell cell = new GridCell(lane, row);

            if (!ContainsCell(pendingCells, cell))
                pendingCells.Add(cell);
            else
                pendingCells.Add(new GridCell(baseLane, row));
        }
    }

    private void SpawnArcShot()
    {
        if (spawnedArrowsThisState)
            return;

        spawnedArrowsThisState = true;

        int divisions = Mathf.Max(1, arcShotParam.laneDivisions);
        int rows = Mathf.Max(1, arcShotParam.rowDivisions);

        for (int i = 0; i < pendingCells.Count; i++)
        {
            SpawnArcDropArrowDelayed(
                pendingCells[i],
                divisions,
                rows,
                i * arcShotParam.spawnInterval
            );
        }
    }

    private int ResolveArcArrowCount()
    {
        if (!arcShotParam.useRandomArrowCount)
            return Mathf.Max(1, arcShotParam.arrowCount);

        int min = Mathf.Clamp(arcShotParam.minArrowCount, 1, 3);
        int max = Mathf.Clamp(arcShotParam.maxArrowCount, min, 3);

        return UnityEngine.Random.Range(min, max + 1);
    }

    private Vector3 GetArcArrowStartForLane(int lane, int divisions)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();
        Vector3 shootDir = GetCurrentShootDirection();

        Vector3 pos =
            sideCenter +
            right * GetLaneOffset(lane, divisions) -
            shootDir * stageLaneParam.arrowStartBehindBoss;

        pos.y = GetStageGroundY() + arcShotParam.startHeightFromGround;

        return pos;
    }

    private void LaunchArcShotArrowByCurrentDistance(
    ArcherBossArrow arrow,
    Vector3 start,
    Vector3 target,
    Vector3 size,
    bool applyLaunchSize)
    {
        bool closeRange =
            arcShotParam.useCloseRangeArcSettings &&
            IsPlayerCloseToCurrentBossSide(arcShotParam.closeRangeStartProgress);

        bool useArcSpeed = closeRange
            ? arcShotParam.closeUseArcSpeed
            : arcShotParam.useArcSpeed;

        float arcSpeed = closeRange
            ? arcShotParam.closeArcSpeed
            : arcShotParam.arcSpeed;

        float flightTime = closeRange
            ? arcShotParam.closeFlightTime
            : arcShotParam.flightTime;

        float arcHeight = closeRange
            ? arcShotParam.closeArcHeight
            : arcShotParam.arcHeight;

        if (useArcSpeed)
        {
            audioManager.PlaySe("AB_ArrowShot");

            arrow.LaunchArcDropBySpeed(
                this,
                start,
                target,
                arcSpeed,
                arcHeight,
                arcShotParam.lifeTime,
                size,
                applyLaunchSize,
                arrowSpawnParam.playerDamage,
                arrowSpawnParam.playerLayer,
                timeAgent
            );

            return;
        }

        audioManager.PlaySe("AB_ArrowShot");

        arrow.LaunchArcDrop(
            this,
            start,
            target,
            flightTime,
            arcHeight,
            arcShotParam.lifeTime,
            size,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );
    }

    #endregion

    #region Lane Arc Burst

    private void EnterLaneArcBurst()
    {
        if (!TryResolveLaneArcBurstArrowPrefab(out _, out _))
        {
            if (laneArcBurstParam.changeAttackIfPrefabMissing)
            {
                ChangeToAlternativeAttack(BossAttackType.LaneArcBurst);
                return;
            }

            ChangeState(BossState.Idle);
            return;
        }

        ApplyNormalHitboxMode();

        PrepareLaneArcBurstCells();

        int startLane = ResolveLaneArcBurstStartLane(
            laneArcBurstParam.laneDivisions
        );

        shootMoveTargetPosition = GetBossShootPositionForLane(
            startLane,
            laneArcBurstParam.laneDivisions,
            currentStageSide
        );

        BeginAttackPhase(stageLaneParam.moveBeforeLaneArcBurst);
    }

    private void UpdateLaneArcBurst(float dt)
    {
        UpdateAttackPhase(
            dt,
            SpawnLaneArcBurst,
            animationParam.arcShot,
            timingParam.arcShotDuration
        );
    }

    private int ResolveLaneArcBurstArrowCount()
    {
        if (!laneArcBurstParam.useRandomArrowCount)
            return Mathf.Max(1, laneArcBurstParam.arrowCount);

        int min = Mathf.Max(1, laneArcBurstParam.minArrowCount);
        int max = Mathf.Max(min, laneArcBurstParam.maxArrowCount);

        return UnityEngine.Random.Range(min, max + 1);
    }

    private void PrepareLaneArcBurstCells()
    {
        pendingCells.Clear();

        int divisions = Mathf.Max(1, laneArcBurstParam.laneDivisions);
        int rows = Mathf.Max(1, laneArcBurstParam.rowDivisions);

        int count = Mathf.Min(
            ResolveLaneArcBurstArrowCount(),
            laneArcBurstParam.uniqueLane ? divisions : 999
        );

        while (pendingCells.Count < count)
        {
            int lane = UnityEngine.Random.Range(0, divisions);
            int row = rows / 2;

            GridCell cell = new GridCell(lane, row);

            if (!laneArcBurstParam.uniqueLane || !ContainsCell(pendingCells, cell))
                pendingCells.Add(cell);
        }
    }

    private int ResolveLaneArcBurstStartLane(int divisions)
    {
        divisions = Mathf.Max(1, divisions);

        if (!laneArcBurstParam.fixedStartLane)
        {
            if (pendingCells.Count > 0)
                return pendingCells[0].lane;

            return divisions / 2;
        }

        if (laneArcBurstParam.startLane < 0)
            return divisions / 2;

        return Mathf.Clamp(
            laneArcBurstParam.startLane,
            0,
            divisions - 1
        );
    }

    private Vector3 GetLaneArcBurstStart(
    GridCell cell,
    int divisions)
    {
        int lane = laneArcBurstParam.fixedStartLane
            ? ResolveLaneArcBurstStartLane(divisions)
            : cell.lane;

        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();
        Vector3 shootDir = GetCurrentShootDirection();

        Vector3 pos =
            sideCenter +
            right * GetLaneOffset(lane, divisions) -
            shootDir * stageLaneParam.arrowStartBehindBoss;

        pos.y = GetStageGroundY() + laneArcBurstParam.startHeightFromGround;

        return pos;
    }

    private Vector3 GetLaneArcBurstTarget(
    GridCell cell,
    int divisions,
    int rows)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 forward = GetStageForward();
        Vector3 right = GetStageRight();

        Vector3 basePosition;

        if (playerTarget != null)
        {
            Vector3 towardBossSide = GetDirectionTowardCurrentBossSide();

            // 通常ArcShotと同じ考え方。
            // プレイヤーの現在位置からBoss側へ少し前にずらす。
            basePosition =
                playerTarget.position +
                towardBossSide * laneArcBurstParam.targetForwardOffsetFromPlayer;
        }
        else
        {
            // Playerが無い場合だけ保険としてステージ中央寄りへ落とす
            Vector3 shootDir = GetCurrentShootDirection();
            basePosition =
                sideCenter +
                shootDir * (GetStageLength() * 0.5f);
        }

        Vector3 diff = basePosition - sideCenter;
        float localForward = Vector3.Dot(diff, forward);

        float randomX = UnityEngine.Random.Range(
            -laneArcBurstParam.targetRandomRightRadius,
            laneArcBurstParam.targetRandomRightRadius
        );

        Vector3 target =
            sideCenter +
            forward * localForward +
            right * (GetLaneOffset(cell.lane, divisions) + randomX);

        target = ClampToStageLength(target);
        target.y = GetStageGroundY() + laneArcBurstParam.targetHeightFromGround;

        return target;
    }

    private void LaunchLaneArcBurstArrowByCurrentDistance(
    ArcherBossArrow arrow,
    Vector3 start,
    Vector3 target,
    Vector3 size,
    bool applyLaunchSize)
    {
        bool closeRange =
            laneArcBurstParam.useCloseRangeArcSettings &&
            IsPlayerCloseToCurrentBossSide(laneArcBurstParam.closeRangeStartProgress);

        bool useArcSpeed = closeRange
            ? laneArcBurstParam.closeUseArcSpeed
            : laneArcBurstParam.useArcSpeed;

        float arcSpeed = closeRange
            ? laneArcBurstParam.closeArcSpeed
            : laneArcBurstParam.arcSpeed;

        float flightTime = closeRange
            ? laneArcBurstParam.closeFlightTime
            : laneArcBurstParam.flightTime;

        float arcHeight = closeRange
            ? laneArcBurstParam.closeArcHeight
            : laneArcBurstParam.arcHeight;

        if (useArcSpeed)
        {
            audioManager.PlaySe("AB_ArrowShot");

            arrow.LaunchArcDropBySpeed(
                this,
                start,
                target,
                arcSpeed,
                arcHeight,
                laneArcBurstParam.lifeTime,
                size,
                applyLaunchSize,
                arrowSpawnParam.playerDamage,
                arrowSpawnParam.playerLayer,
                timeAgent
            );

            return;
        }

        audioManager.PlaySe("AB_ArrowShot");

        arrow.LaunchArcDrop(
            this,
            start,
            target,
            flightTime,
            arcHeight,
            laneArcBurstParam.lifeTime,
            size,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );
    }

    #endregion

    #region Lock On Drop Shot

    private void EnterLockOnDropShot()
    {
        if (!TryResolveLockOnDropArrowPrefab(out _, out _))
        {
            if (lockOnDropParam.changeAttackIfPrefabMissing)
            {
                ChangeToAlternativeAttack(BossAttackType.LockOnDrop);
                return;
            }

            ChangeState(BossState.Idle);
            return;
        }

        ApplyNormalHitboxMode();

        lockOnDropEventSpawnIndex = 0;

        shootMoveTargetPosition = transform.position;

        BeginAttackPhase(stageLaneParam.moveBeforeLockOnDrop);
    }

    private void UpdateLockOnDropShot(float dt)
    {
        UpdateAttackPhase(
            dt,
            SpawnLockOnDropShot,
            animationParam.arcShot,
            timingParam.arcShotDuration
        );
    }

    private void SpawnLockOnDropShot()
    {
        if (spawnedArrowsThisState)
            return;

        spawnedArrowsThisState = true;

        if (animationEventParam.spawnLockOnDropByAnimationEvent)
            return;

        int count = Mathf.Max(1, lockOnDropParam.arrowCount);

        for (int i = 0; i < count; i++)
        {
            float launchDelay = i * Mathf.Max(0f, lockOnDropParam.launchInterval);
            float fallOrderDelay = i * Mathf.Max(0f, lockOnDropParam.dropInterval);

            SpawnLockOnDropArrowDelayed(
                launchDelay,
                fallOrderDelay
            );
        }
    }

    #endregion

    #region Melee Attack

    private void EnterMeleeAttack()
    {
        lastMeleeTime = localTime;

        attackPhase = AttackPhase.None;
        pendingCells.Clear();
        spawnedArrowsThisState = false;
        animationEventFinished = false;

        enterStunLoopDirectlyFromMeleeComplete = false;

        StopMeleeAttackHitboxes();

        if (meleeParam.clearActiveArrowsOnMelee)
            ForceFinishAllArrows();

        if (meleeParam.logMelee)
            Debug.Log($"{name} ArcherBoss MeleeAttack Start");

        PlayBossAnimation(
            bossAnimationParam.melee,
            bossAnimationParam.meleeBlendTime,
            bossAnimationParam.forceRestartAnimations
        );
    }

    private void UpdateMeleeAttack(float dt)
    {
        bool finished = IsStateAnimationFinished(
            animationParam.melee,
            stateTimer,
            animationEventFinished,
            bossAnimationParam.melee
        );

        if (!finished)
            return;

        StopMeleeAttackHitboxes();

        if (meleeParam.logMelee)
            Debug.Log($"{name} ArcherBoss MeleeAttack End");

        if (meleeParam.jumpOppositeAfterMeleeStun)
            jumpToOppositeSideAfterStun = true;

        if (meleeParam.enterStunAfterMelee)
        {
            enterStunLoopDirectlyFromMeleeComplete =
                meleeParam.enterStunLoopDirectlyAfterMeleeComplete;

            ChangeState(BossState.Stunned);
            return;
        }

        enterStunLoopDirectlyFromMeleeComplete = false;
        ChangeState(BossState.Idle);
    }

    private bool TryStartMeleeAttack()
    {
        if (!meleeParam.enableMeleeWhenPlayerEnterBossCircle)
            return false;

        if (playerTarget == null)
            return false;

        if (!CanStartMeleeFromCurrentState())
            return false;

        if (localTime < lastMeleeTime + meleeParam.cooldown)
            return false;

        if (!IsPlayerInsideCurrentBossCircle())
            return false;

        ChangeState(BossState.MeleeAttack);
        return true;
    }

    private bool IsPlayerInsideCurrentBossCircle()
    {
        Vector3 center = GetStageSideCenter(currentStageSide);
        Vector3 player = playerTarget.position;

        center.y = 0f;
        player.y = 0f;

        float radius = meleeParam.bossCircleRadius;

        if (radius <= 0f)
            radius = Mathf.Max(0.1f, stageLaneParam.stageWidth * 0.5f);

        float distance = Vector3.Distance(center, player);
        bool inside = distance <= radius;

        if (meleeParam.logMeleeCheck)
        {
            Debug.Log(
                $"{name} MeleeCheck " +
                $"state:{state} " +
                $"side:{currentStageSide} " +
                $"distance:{distance:F2} " +
                $"radius:{radius:F2} " +
                $"inside:{inside}"
            );
        }

        return inside;
    }

    private bool CanStartMeleeFromCurrentState()
    {
        if (state == BossState.MeleeAttack ||
            state == BossState.Stunned ||
            state == BossState.JumpRetreat ||
            state == BossState.Dead ||
            state == BossState.Anger)
        {
            return false;
        }

        if (state == BossState.Idle)
            return true;

        return meleeParam.interruptAttackWithMelee;
    }

    private void StopMeleeAttackHitboxes()
    {
        SetMeleeAttackHitboxesActive(false);
        meleeHitTargets.Clear();
    }

    #endregion

    #region Angry Barrage

    private void EnterAngryBarrage()
    {
        if (!TryResolveAngryBarrageArrowPrefab(out _, out _))
        {
            if (angryBarrageParam.changeAttackIfPrefabMissing)
            {
                ChangeToAlternativeAttack(BossAttackType.AngryBarrage);
                return;
            }

            ChangeState(BossState.Idle);
            return;
        }

        ApplyNormalHitboxMode();

        PrepareAngryBarrageCells();

        int divisions = Mathf.Max(1, angryBarrageParam.laneDivisions);

        int lane = stageLaneParam.angryBarrageMoveToCenter
            ? divisions / 2
            : pendingCells.Count > 0
                ? pendingCells[0].lane
                : divisions / 2;

        shootMoveTargetPosition = GetBossShootPositionForLane(
            lane,
            divisions,
            currentStageSide
        );

        BeginAttackPhase(stageLaneParam.moveBeforeAngryBarrage);
    }

    private void UpdateAngryBarrage(float dt)
    {
        UpdateAttackPhase(
            dt,
            SpawnAngryBarrage,
            animationParam.angryBarrage,
            timingParam.angryBarrageDuration
        );
    }

    private void PrepareAngryBarrageCells()
    {
        pendingCells.Clear();

        int divisions = Mathf.Max(1, angryBarrageParam.laneDivisions);
        int rows = Mathf.Max(1, angryBarrageParam.rowDivisions);

        int minCount = Mathf.Max(1, angryBarrageParam.minArrowCount);
        int maxCount = Mathf.Max(minCount, angryBarrageParam.maxArrowCount);

        int cellCount = divisions * rows;
        int count = Mathf.Min(
            UnityEngine.Random.Range(minCount, maxCount + 1),
            cellCount
        );

        while (pendingCells.Count < count)
        {
            int lane = UnityEngine.Random.Range(0, divisions);
            int row = UnityEngine.Random.Range(0, rows);

            GridCell cell = new GridCell(lane, row);

            if (!ContainsCell(pendingCells, cell))
                pendingCells.Add(cell);
        }
    }

    private void SpawnAngryBarrage()
    {
        if (spawnedArrowsThisState)
            return;

        spawnedArrowsThisState = true;

        int divisions = Mathf.Max(1, angryBarrageParam.laneDivisions);
        int rows = Mathf.Max(1, angryBarrageParam.rowDivisions);

        for (int i = 0; i < pendingCells.Count; i++)
        {
            SpawnAngryBarrageCellArrow(
                pendingCells[i],
                divisions,
                rows
            );
        }
    }

    private Vector3 GetAngryBarrageApproachTarget(
    Vector3 start,
    Vector3 lanePoint,
    Vector3 finalDirection)
    {
        if (!angryBarrageParam.approachFromMuzzleForward)
        {
            Vector3 legacyTarget =
                lanePoint +
                finalDirection.normalized *
                angryBarrageParam.laneApproachForwardLeadDistance;

            if (!angryBarrageParam.approachToLaneHeight)
                legacyTarget.y = start.y;

            return legacyTarget;
        }

        Vector3 right = GetStageRight();
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);

        Vector3 startFlat = start;
        Vector3 laneFlat = lanePoint;
        Vector3 centerFlat = sideCenter;

        startFlat.y = 0f;
        laneFlat.y = 0f;
        centerFlat.y = 0f;

        float startRightOffset = Vector3.Dot(
            startFlat - centerFlat,
            right
        );

        float laneRightOffset = Vector3.Dot(
            laneFlat - centerFlat,
            right
        );

        float rightDelta = laneRightOffset - startRightOffset;

        Vector3 approachTarget =
            start +
            finalDirection.normalized *
            angryBarrageParam.laneApproachForwardLeadDistance +
            right * rightDelta;

        // ここが今回の重要修正。
        // 横だけでなく、選択されたRowの高さにも補正する。
        if (angryBarrageParam.approachToLaneHeight)
        {
            approachTarget.y = lanePoint.y;
        }
        else
        {
            approachTarget.y = start.y;
        }

        return approachTarget;
    }

    #endregion

    #region Attack Phase

    private void BeginAttackPhase(bool moveBeforeShot)
    {
        attackPhase = moveBeforeShot
            ? AttackPhase.MoveToShootPosition
            : AttackPhase.PlayAttackAnimation;
    }

    private void UpdateAttackPhase(
    float dt,
    Action spawnAction,
    StateAnimationFinishParam finishParam,
    float fallbackDuration)
    {
        switch (attackPhase)
        {
            case AttackPhase.MoveToShootPosition:
                if (MoveBossToShootPosition(dt))
                    attackPhase = AttackPhase.PlayAttackAnimation;
                break;

            case AttackPhase.PlayAttackAnimation:
                PlayCurrentAttackAnimation();
                attackPhase = AttackPhase.Spawn;
                break;

            case AttackPhase.Spawn:
                if (ShouldSpawnByAnimationEvent())
                {
                    attackPhase = AttackPhase.WaitEnd;
                    return;
                }

                spawnAction?.Invoke();
                attackPhase = AttackPhase.WaitEnd;
                break;

            case AttackPhase.WaitEnd:
                if (IsStateFinished(finishParam, fallbackDuration))
                    ChangeState(BossState.Idle);
                break;
        }
    }

    private bool MoveBossToShootPosition(float dt)
    {
        Vector3 current = transform.position;
        current.y = GetBossMoveY();

        Vector3 target = shootMoveTargetPosition;
        target.y = GetBossMoveY();

        Vector3 next = Vector3.MoveTowards(
            current,
            target,
            stageLaneParam.bossSideMoveSpeed * dt
        );

        next.y = GetBossMoveY();
        transform.position = next;

        FacePlayerPlanar();

        return Vector3.Distance(next, target) <=
               stageLaneParam.bossShootPositionReachDistance;
    }

    private void PlayCurrentAttackAnimation()
    {
        switch (state)
        {
            case BossState.StraightShot:
                PlayBossAnimation(
                    bossAnimationParam.straightShot,
                    bossAnimationParam.shotBlendTime,
                    bossAnimationParam.forceRestartAnimations
                );
                break;

            case BossState.ArcShot:
                PlayBossAnimation(
                    GetArcShotAnimationName(),
                    bossAnimationParam.shotBlendTime,
                    bossAnimationParam.forceRestartAnimations
                );
                break;

            case BossState.LaneArcBurst:
                PlayBossAnimation(
                    bossAnimationParam.laneArcBurst,
                    bossAnimationParam.shotBlendTime,
                    bossAnimationParam.forceRestartAnimations
                );
                break;

            case BossState.LockOnDropShot:
                PlayBossAnimation(
                    bossAnimationParam.dropShot,
                    bossAnimationParam.shotBlendTime,
                    bossAnimationParam.forceRestartAnimations
                );
                break;

            case BossState.AngryBarrage:
                PlayBossAnimation(
                    bossAnimationParam.angryBarrage,
                    bossAnimationParam.shotBlendTime,
                    bossAnimationParam.forceRestartAnimations
                );
                break;
        }
    }

    #endregion

    #region Arrow Spawn Utility

    private ArcherBossArrow ResolveArrowPrefab(ArcherBossArrow overridePrefab)
    {
        return overridePrefab != null
            ? overridePrefab
            : arrowSpawnParam.arrowPrefab;
    }

    private void SpawnStraightCellArrowDelayed(
    GridCell cell,
    int divisions,
    int rows,
    float speed,
    float lifeTime,
    float delay,
    bool expandHeightToPlayerY)
    {
        if (delay <= 0f)
        {
            SpawnStraightCellArrow(
                cell,
                divisions,
                rows,
                speed,
                lifeTime,
                expandHeightToPlayerY
            );
            return;
        }

        StartCoroutine(
            SpawnStraightCellArrowCoroutine(
                cell,
                divisions,
                rows,
                speed,
                lifeTime,
                delay,
                expandHeightToPlayerY
            )
        );
    }

    private System.Collections.IEnumerator SpawnStraightCellArrowCoroutine(
    GridCell cell,
    int divisions,
    int rows,
    float speed,
    float lifeTime,
    float delay,
    bool expandHeightToPlayerY)
    {
        float timer = 0f;

        while (timer < delay)
        {
            timer += ScaledDeltaTime;
            yield return null;
        }

        SpawnStraightCellArrow(
            cell,
            divisions,
            rows,
            speed,
            lifeTime,
            expandHeightToPlayerY
        );
    }

    private void SpawnStraightCellArrow(
        GridCell cell,
        int divisions,
        int rows,
        float speed,
        float lifeTime,
        bool expandHeightToPlayerY)
    {
        if (!TryResolveStraightArrowPrefab(
                out ArcherBossArrow prefab,
                out bool applyLaunchSize))
        {
            if (straightShotParam.changeAttackIfPrefabMissing)
                ChangeToAlternativeAttack(BossAttackType.Straight);
            else
                ChangeState(BossState.Idle);

            return;
        }

        ArrowSpawnHeightMode heightMode = applyLaunchSize
            ? ArrowSpawnHeightMode.LaneSizedCenter
            : ArrowSpawnHeightMode.PrefabStraightHeight;

        Vector3 referencePoint = GetStraightLaneReferencePoint(
            cell,
            divisions,
            rows,
            expandHeightToPlayerY,
            heightMode
        );

        Vector3 start = GetStraightLikeStart(
            BossAttackType.Straight,
            referencePoint
        );

        Vector3 direction = GetStraightForwardDirection();

        Vector3 size = applyLaunchSize
            ? GetCellArrowSize(cell, divisions, rows, expandHeightToPlayerY)
            : Vector3.one;

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        audioManager.PlaySe("AB_ArrowShot");

        arrow.LaunchStraight(
            this,
            start,
            direction,
            speed,
            lifeTime,
            GetStraightTravelDistanceFromStart(start, referencePoint),
            size,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );
    }

    private void SpawnStraightCellArrowWithPrefab(
    ArcherBossArrow prefab,
    GridCell cell,
    int divisions,
    int rows,
    float speed,
    float lifeTime,
    bool expandHeightToPlayerY,
    Vector3 arrowSize,
    bool applyLaunchSize,
    ArrowSpawnHeightMode heightMode,
    BossAttackType attackType)
    {
        if (prefab == null)
            return;

        Vector3 referencePoint = GetStraightLaneReferencePoint(
            cell,
            divisions,
            rows,
            expandHeightToPlayerY,
            heightMode
        );

        Vector3 start = GetStraightLikeStart(
            attackType,
            referencePoint
        );

        Vector3 direction = GetStraightLikeDirection(
            start,
            referencePoint
        );

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        arrow.LaunchStraight(
            this,
            start,
            direction,
            speed,
            lifeTime,
            GetStraightTravelDistanceFromStart(start, referencePoint),
            arrowSize,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );
    }

    private void SpawnArcDropArrowDelayed(
        GridCell cell,
        int divisions,
        int rows,
        float delay)
    {
        if (delay <= 0f)
        {
            SpawnArcDropArrow(cell, divisions, rows);
            return;
        }

        StartCoroutine(
            SpawnArcDropArrowCoroutine(
                cell,
                divisions,
                rows,
                delay
            )
        );
    }

    private System.Collections.IEnumerator SpawnArcDropArrowCoroutine(
        GridCell cell,
        int divisions,
        int rows,
        float delay)
    {
        float timer = 0f;

        while (timer < delay)
        {
            timer += ScaledDeltaTime;
            yield return null;
        }

        SpawnArcDropArrow(cell, divisions, rows);
    }

    private void SpawnArcDropArrow(GridCell cell, int divisions, int rows)
    {
        if (!TryResolveArcArrowPrefab(out ArcherBossArrow prefab, out bool applyLaunchSize))
        {
            if (arcShotParam.changeAttackIfPrefabMissing)
                ChangeToAlternativeAttack(BossAttackType.Arc);
            else
                ChangeState(BossState.Idle);

            return;
        }

        Vector3 start = GetArcArrowStartFromMuzzleOrLane(currentArcStartLane, divisions);
        Vector3 target = GetArcArrowTarget(cell, divisions, rows);

        Vector3 size = applyLaunchSize
            ? GetCellArrowSize(cell, divisions, rows, false)
            : Vector3.one;

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        LaunchArcShotArrowByCurrentDistance(
            arrow,
            start,
            target,
            size,
            applyLaunchSize
        );
    }

    private void SpawnLaneArcBurst()
    {
        if (spawnedArrowsThisState)
            return;

        spawnedArrowsThisState = true;

        int divisions = Mathf.Max(1, laneArcBurstParam.laneDivisions);
        int rows = Mathf.Max(1, laneArcBurstParam.rowDivisions);

        for (int i = 0; i < pendingCells.Count; i++)
        {
            SpawnLaneArcBurstArrow(
                pendingCells[i],
                divisions,
                rows
            );
        }
    }

    private void SpawnLaneArcBurstArrow(
    GridCell cell,
    int divisions,
    int rows)
    {
        if (!TryResolveLaneArcBurstArrowPrefab(
                out ArcherBossArrow prefab,
                out bool applyLaunchSize))
        {
            if (laneArcBurstParam.changeAttackIfPrefabMissing)
                ChangeToAlternativeAttack(BossAttackType.LaneArcBurst);
            else
                ChangeState(BossState.Idle);

            return;
        }

        Vector3 start = GetLaneArcBurstStartFromMuzzleOrLane(cell, divisions);
        Vector3 target = GetLaneArcBurstTarget(cell, divisions, rows);

        Vector3 size = applyLaunchSize
            ? GetCellArrowSize(cell, divisions, rows, false)
            : Vector3.one;

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        LaunchLaneArcBurstArrowByCurrentDistance(
            arrow,
            start,
            target,
            size,
            applyLaunchSize
        );
    }

    private Vector3 GetArcArrowStart(GridCell cell, int divisions)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();
        Vector3 shootDir = GetCurrentShootDirection();

        Vector3 pos =
            sideCenter +
            right * GetLaneOffset(cell.lane, divisions) -
            shootDir * stageLaneParam.arrowStartBehindBoss;

        pos.y = GetStageGroundY() + arcShotParam.startHeightFromGround;

        return pos;
    }

    private Vector3 GetArcArrowTarget(
    GridCell cell,
    int divisions,
    int rows)
    {
        Vector3 towardBossSide = GetDirectionTowardCurrentBossSide();

        Vector3 basePosition;

        if (playerTarget != null)
        {
            basePosition =
                playerTarget.position +
                towardBossSide * arcShotParam.targetForwardOffsetFromPlayer;
        }
        else
        {
            basePosition = GetStageSideCenter(GetOppositeSide(currentStageSide));
        }

        Vector3 target = ResolveArcTargetHorizontalPosition(
            basePosition,
            cell,
            divisions
        );

        target = ClampToStageLength(target);
        target.y = GetStageGroundY() + arcShotParam.targetHeightFromGround;

        return target;
    }

    private Vector3 ResolveArcTargetHorizontalPosition(
    Vector3 basePosition,
    GridCell cell,
    int divisions)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 forward = GetStageForward();
        Vector3 right = GetStageRight();

        Vector3 diff = basePosition - sideCenter;
        float localForward = Vector3.Dot(diff, forward);

        float localRight;

        if (playerTarget != null &&
            arcShotParam.targetHorizontalMode == ArcTargetHorizontalMode.PlayerCenter)
        {
            // プレイヤーの現在の横位置そのものを使う
            localRight = Vector3.Dot(playerTarget.position - sideCenter, right);
        }
        else if (playerTarget != null &&
                 arcShotParam.targetHorizontalMode == ArcTargetHorizontalMode.PlayerLaneCenter)
        {
            // プレイヤーがいるレーン番号を取り、そのレーン中央を使う
            int playerLane = GetLaneIndexFromPosition(
                playerTarget.position,
                divisions
            );

            localRight = GetLaneOffset(playerLane, divisions);
        }
        else
        {
            // Playerが無い場合や保険として、選ばれたcellのレーン中央
            localRight = GetLaneOffset(cell.lane, divisions);
        }

        float randomX = UnityEngine.Random.Range(
            -arcShotParam.targetRandomRightRadius,
            arcShotParam.targetRandomRightRadius
        );

        return sideCenter +
               forward * localForward +
               right * (localRight + randomX);
    }

    private void SpawnLockOnDropArrowDelayed(
    float launchDelay,
    float fallOrderDelay)
    {
        if (launchDelay <= 0f)
        {
            SpawnLockOnDropArrow(fallOrderDelay);
            return;
        }

        StartCoroutine(
            SpawnLockOnDropArrowCoroutine(
                launchDelay,
                fallOrderDelay
            )
        );
    }

    private System.Collections.IEnumerator SpawnLockOnDropArrowCoroutine(
    float launchDelay,
    float fallOrderDelay)
    {
        float timer = 0f;

        while (timer < launchDelay)
        {
            timer += ScaledDeltaTime;
            yield return null;
        }

        SpawnLockOnDropArrow(fallOrderDelay);
    }

    private void SpawnLockOnDropArrow(float fallOrderDelay)
    {
        if (!TryResolveLockOnDropArrowPrefab(
                out ArcherBossArrow prefab,
                out bool applyLaunchSize))
        {
            if (lockOnDropParam.changeAttackIfPrefabMissing)
                ChangeToAlternativeAttack(BossAttackType.LockOnDrop);
            else
                ChangeState(BossState.Idle);

            return;
        }

        Vector3 start = GetLockOnDropArrowStart();

        float fixedRightOffset = UnityEngine.Random.Range(
            -lockOnDropParam.targetRandomRightRadius,
            lockOnDropParam.targetRandomRightRadius
        );

        Func<Vector3> retargetProvider = () =>
            GetLockOnDropTarget(fixedRightOffset);

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        Vector3 size = applyLaunchSize
            ? lockOnDropParam.arrowSize
            : Vector3.one;

        audioManager.PlaySe("AB_ArrowShot");

        arrow.LaunchLockOnDrop(
            this,
            start,
            retargetProvider,
            lockOnDropParam.riseDuration,
            lockOnDropParam.riseArcHeight,
            lockOnDropParam.dropHeightFromGround,
            lockOnDropParam.lockOnDelay,
            fallOrderDelay,
            lockOnDropParam.trackPlayerDuringRise,
            lockOnDropParam.fallSpeed,
            lockOnDropParam.hitDistance,
            lockOnDropParam.lifeTime,
            size,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );

        if (lockOnDropParam.logLockOnTarget)
        {
            Debug.Log(
                $"{name} LockOnDrop launched. " +
                $"fallOrderDelay:{fallOrderDelay:F2} " +
                $"lockOnDelay:{lockOnDropParam.lockOnDelay:F2} " +
                $"fallSpeed:{lockOnDropParam.fallSpeed:F1} " +
                $"applyLaunchSize:{applyLaunchSize}"
            );
        }
    }

    private Vector3 GetLockOnDropArrowStart()
    {
        if (TryGetMuzzlePosition(BossAttackType.LockOnDrop, out Vector3 muzzlePosition))
            return muzzlePosition;

        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        sideCenter.y = GetStageGroundY() + lockOnDropParam.dropHeightFromGround;
        return sideCenter;
    }

    private Vector3 GetLockOnDropTarget(float fixedRightOffset)
    {
        Vector3 target;

        if (playerTarget != null)
        {
            Vector3 towardBossSide = GetDirectionTowardCurrentBossSide();

            target =
                playerTarget.position +
                towardBossSide * lockOnDropParam.targetForwardOffsetFromPlayer;
        }
        else
        {
            target = GetStageSideCenter(GetOppositeSide(currentStageSide));
        }

        Vector3 right = GetStageRight();

        target += right * fixedRightOffset;

        target = ClampToStageLength(target);
        target.y = GetStageGroundY() + lockOnDropParam.targetHeightFromGround;

        return target;
    }

    private void SpawnAngryBarrageCellArrowDelayed(
    GridCell cell,
    int divisions,
    int rows,
    float delay)
    {
        if (delay <= 0f)
        {
            SpawnAngryBarrageCellArrow(cell, divisions, rows);
            return;
        }

        StartCoroutine(
            SpawnAngryBarrageCellArrowCoroutine(
                cell,
                divisions,
                rows,
                delay
            )
        );
    }

    private System.Collections.IEnumerator SpawnAngryBarrageCellArrowCoroutine(
        GridCell cell,
        int divisions,
        int rows,
        float delay)
    {
        float timer = 0f;

        while (timer < delay)
        {
            timer += ScaledDeltaTime;
            yield return null;
        }

        SpawnAngryBarrageCellArrow(cell, divisions, rows);
    }

    private void SpawnAngryBarrageCellArrow(
    GridCell cell,
    int divisions,
    int rows)
    {
        if (!TryResolveAngryBarrageArrowPrefab(
                out ArcherBossArrow prefab,
                out bool applyLaunchSize))
        {
            if (angryBarrageParam.changeAttackIfPrefabMissing)
                ChangeToAlternativeAttack(BossAttackType.AngryBarrage);
            else
                ChangeState(BossState.Idle);

            return;
        }

        // AngryBarrageはPrefabサイズを尊重する場合でも、
        // 補正先の位置は必ず選択されたRow中心にする。
        Vector3 lanePoint = GetStraightLaneReferencePoint(
            cell,
            divisions,
            rows,
            false,
            ArrowSpawnHeightMode.CellRowCenter
        );

        Vector3 start = GetAngryBarrageStart(lanePoint);

        Vector3 size = applyLaunchSize
            ? GetCellArrowSizeWithScale(
                cell,
                divisions,
                rows,
                false,
                angryBarrageParam.cellWidthScale,
                angryBarrageParam.cellHeightScale,
                angryBarrageParam.arrowDepth
            )
            : Vector3.one;

        ArcherBossArrow arrow = Instantiate(
            prefab,
            start,
            Quaternion.identity
        );

        RegisterActiveArrow(arrow);

        Vector3 finalDirection = GetStraightForwardDirection();

        if (angryBarrageParam.useLaneApproach)
        {
            Vector3 approachTarget = GetAngryBarrageApproachTarget(
                start,
                lanePoint,
                finalDirection
            );

            float maxDistance =
                GetStraightTravelDistanceFromStart(start, approachTarget);

            if (angryBarrageParam.syncLaneApproachByDuration)
            {
                audioManager.PlaySe("AB_ArrowShot");

                arrow.LaunchLaneApproachStraightByDuration(
                    this,
                    start,
                    approachTarget,
                    finalDirection,
                    angryBarrageParam.laneApproachDuration,
                    angryBarrageParam.speed,
                    angryBarrageParam.lifeTime,
                    maxDistance,
                    angryBarrageParam.faceFinalDirectionDuringApproach,
                    size,
                    applyLaunchSize,
                    arrowSpawnParam.playerDamage,
                    arrowSpawnParam.playerLayer,
                    timeAgent
                );
            }
            else
            {
                audioManager.PlaySe("AB_ArrowShot");

                arrow.LaunchLaneApproachStraight(
                    this,
                    start,
                    approachTarget,
                    finalDirection,
                    angryBarrageParam.laneApproachSpeed,
                    angryBarrageParam.speed,
                    angryBarrageParam.lifeTime,
                    maxDistance,
                    angryBarrageParam.laneApproachReachDistance,
                    angryBarrageParam.faceFinalDirectionDuringApproach,
                    size,
                    applyLaunchSize,
                    arrowSpawnParam.playerDamage,
                    arrowSpawnParam.playerLayer,
                    timeAgent
                );
            }

            return;
        }

        audioManager.PlaySe("AB_ArrowShot");

        arrow.LaunchStraight(
            this,
            start,
            finalDirection,
            angryBarrageParam.speed,
            angryBarrageParam.lifeTime,
            GetStraightTravelDistanceFromStart(start, lanePoint),
            size,
            applyLaunchSize,
            arrowSpawnParam.playerDamage,
            arrowSpawnParam.playerLayer,
            timeAgent
        );
    }

    private bool TryResolveStraightArrowPrefab(
    out ArcherBossArrow prefab,
    out bool applyLaunchSize)
    {
        if (straightShotParam.arrowPrefabOverride != null)
        {
            prefab = straightShotParam.arrowPrefabOverride;
            applyLaunchSize = !straightShotParam.preserveOverridePrefabSize;
            return true;
        }

        if (arrowSpawnParam.arrowPrefab != null)
        {
            prefab = arrowSpawnParam.arrowPrefab;
            applyLaunchSize = straightShotParam.resizeDefaultPrefabToLane;
            return true;
        }

        prefab = null;
        applyLaunchSize = false;
        return false;
    }

    private bool TryResolveArcArrowPrefab(
    out ArcherBossArrow prefab,
    out bool applyLaunchSize)
    {
        if (arcShotParam.arrowPrefabOverride != null)
        {
            prefab = arcShotParam.arrowPrefabOverride;
            applyLaunchSize = !arcShotParam.preserveOverridePrefabSize;
            return true;
        }

        if (arrowSpawnParam.arrowPrefab != null)
        {
            prefab = arrowSpawnParam.arrowPrefab;
            applyLaunchSize = arcShotParam.resizeDefaultPrefabToLane;
            return true;
        }

        prefab = null;
        applyLaunchSize = false;
        return false;
    }

    private bool TryResolveLaneArcBurstArrowPrefab(
    out ArcherBossArrow prefab,
    out bool applyLaunchSize)
    {
        if (laneArcBurstParam.arrowPrefabOverride != null)
        {
            prefab = laneArcBurstParam.arrowPrefabOverride;
            applyLaunchSize = !laneArcBurstParam.preserveOverridePrefabSize;
            return true;
        }

        if (arcShotParam.arrowPrefabOverride != null)
        {
            prefab = arcShotParam.arrowPrefabOverride;
            applyLaunchSize = !laneArcBurstParam.preserveOverridePrefabSize;
            return true;
        }

        if (arrowSpawnParam.arrowPrefab != null)
        {
            prefab = arrowSpawnParam.arrowPrefab;
            applyLaunchSize = laneArcBurstParam.resizeDefaultPrefabToLane;
            return true;
        }

        prefab = null;
        applyLaunchSize = false;
        return false;
    }

    private bool TryResolveAngryBarrageArrowPrefab(
    out ArcherBossArrow prefab,
    out bool applyLaunchSize)
    {
        if (angryBarrageParam.arrowPrefabOverride != null)
        {
            prefab = angryBarrageParam.arrowPrefabOverride;
            applyLaunchSize = !angryBarrageParam.preserveOverridePrefabSize;
            return true;
        }

        if (arrowSpawnParam.arrowPrefab != null)
        {
            prefab = arrowSpawnParam.arrowPrefab;
            applyLaunchSize = angryBarrageParam.resizeDefaultPrefabToCell;
            return true;
        }

        prefab = null;
        applyLaunchSize = false;
        return false;
    }

    private bool TryResolveLockOnDropArrowPrefab(
        out ArcherBossArrow prefab,
        out bool applyLaunchSize)
    {
        if (lockOnDropParam.arrowPrefabOverride != null)
        {
            prefab = lockOnDropParam.arrowPrefabOverride;
            applyLaunchSize = !lockOnDropParam.preserveOverridePrefabSize;
            return true;
        }

        if (arrowSpawnParam.arrowPrefab != null)
        {
            prefab = arrowSpawnParam.arrowPrefab;
            applyLaunchSize = lockOnDropParam.resizeDefaultPrefabByArrowSize;
            return true;
        }

        prefab = null;
        applyLaunchSize = false;
        return false;
    }

    private float ResolveArcFlightTime(Vector3 start, Vector3 target)
    {
        if (!arcShotParam.useArcSpeed)
            return Mathf.Max(0.1f, arcShotParam.flightTime);

        float length = EstimateArcPathLength(
            start,
            target,
            arcShotParam.arcHeight,
            arcShotParam.arcLengthSamples
        );

        float speed = Mathf.Max(0.01f, arcShotParam.arcSpeed);

        return Mathf.Max(0.1f, length / speed);
    }

    private float EstimateArcPathLength(
        Vector3 start,
        Vector3 target,
        float arcHeight,
        int samples)
    {
        samples = Mathf.Max(4, samples);

        Vector3 control = GetArcControlPoint(
            start,
            target,
            arcHeight
        );

        float length = 0f;
        Vector3 prev = start;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;

            Vector3 p = EvaluateQuadraticBezier(
                start,
                control,
                target,
                t
            );

            length += Vector3.Distance(prev, p);
            prev = p;
        }

        return length;
    }

    private Vector3 GetArcControlPoint(
        Vector3 start,
        Vector3 target,
        float arcHeight)
    {
        Vector3 mid = (start + target) * 0.5f;
        mid.y = Mathf.Max(start.y, target.y) + Mathf.Max(0.1f, arcHeight);
        return mid;
    }

    private Vector3 EvaluateQuadraticBezier(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float t)
    {
        float u = 1f - t;

        return
            u * u * a +
            2f * u * t * b +
            t * t * c;
    }

    private Vector3 GetStraightLaneReferencePoint(
    GridCell cell,
    int divisions,
    int rows,
    bool expandHeightToPlayerY,
    ArrowSpawnHeightMode heightMode)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();
        Vector3 shootDir = GetCurrentShootDirection();

        Vector3 pos =
            sideCenter +
            right * GetLaneOffset(cell.lane, divisions) -
            shootDir * stageLaneParam.arrowStartBehindBoss;

        switch (heightMode)
        {
            case ArrowSpawnHeightMode.PrefabStraightHeight:
                pos.y = GetStageGroundY() + GetPrefabStraightSpawnHeightOffset();
                return pos;

            case ArrowSpawnHeightMode.CellRowCenter:
                pos.y = GetRowCenterY(cell.row, rows);
                return pos;

            case ArrowSpawnHeightMode.LaneSizedCenter:
            default:
                break;
        }

        float bottomY;
        float topY;

        if (expandHeightToPlayerY)
        {
            GetNormalStraightVerticalRange(
                out bottomY,
                out topY
            );
        }
        else
        {
            GetCellVerticalRange(
                cell,
                rows,
                out bottomY,
                out topY
            );
        }

        pos.y = (bottomY + topY) * 0.5f;

        return pos;
    }

    private Vector3 GetAngryBarrageStart(Vector3 lanePoint)
    {
        if (angryBarrageParam.useMuzzleStart &&
            TryGetAngryBarrageMuzzlePosition(out Vector3 muzzlePosition))
        {
            if (!angryBarrageParam.keepMuzzleHeight)
                muzzlePosition.y = lanePoint.y;

            return muzzlePosition;
        }

        return GetStraightLikeStart(
            BossAttackType.AngryBarrage,
            lanePoint
        );
    }

    private Vector3 GetArrowSpawnStartOrReference(
    BossAttackType attackType,
    Vector3 referencePoint)
    {
        if (TryGetMuzzlePosition(attackType, out Vector3 muzzlePosition))
            return muzzlePosition;

        return referencePoint;
    }

    private Vector3 GetStraightLikeDirection(
     Vector3 start,
     Vector3 referencePoint)
    {
        Vector3 dir = referencePoint - start;

        // Straight系は上下方向に飛ばさない。
        // 高さはstart時点で合わせているので、方向はXZだけ見る。
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = GetCurrentShootDirection();

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        return dir.normalized;
    }

    private float GetStraightTravelDistanceFromStart(
    Vector3 start,
    Vector3 referencePoint)
    {
        Vector3 a = start;
        Vector3 b = referencePoint;

        a.y = 0f;
        b.y = 0f;

        return GetStraightArrowTravelDistance() +
               Vector3.Distance(a, b);
    }

    private Vector3 GetStraightLikeStart(
    BossAttackType attackType,
    Vector3 referencePoint)
    {
        if (!TryGetMuzzlePosition(attackType, out Vector3 muzzlePosition))
            return referencePoint;

        Vector3 start = referencePoint;

        // XZだけMuzzleを使う。
        // YはreferencePoint側、つまり前のレーン/段の高さを使う。
        start.x = muzzlePosition.x;
        start.z = muzzlePosition.z;

        return start;
    }

    private Vector3 GetStraightForwardDirection()
    {
        Vector3 dir = GetCurrentShootDirection();
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        return dir.normalized;
    }

    #endregion

    #region Active Arrow Management

    private void RegisterActiveArrow(ArcherBossArrow arrow)
    {
        if (arrow == null)
            return;

        if (!activeArrows.Contains(arrow))
            activeArrows.Add(arrow);

        arrow.OnArrowFinished += HandleArrowFinished;
    }

    private void HandleArrowFinished(ArcherBossArrow arrow)
    {
        if (arrow != null)
            arrow.OnArrowFinished -= HandleArrowFinished;

        activeArrows.Remove(arrow);
    }

    private bool HasActiveArrows()
    {
        for (int i = activeArrows.Count - 1; i >= 0; i--)
        {
            if (activeArrows[i] == null)
            {
                activeArrows.RemoveAt(i);
                continue;
            }

            return true;
        }

        return false;
    }

    private void ClearArrowEvents()
    {
        for (int i = 0; i < activeArrows.Count; i++)
        {
            if (activeArrows[i] != null)
                activeArrows[i].OnArrowFinished -= HandleArrowFinished;
        }

        activeArrows.Clear();
    }

    private void ForceFinishAllArrows()
    {
        ArcherBossArrow[] copy = activeArrows.ToArray();

        for (int i = 0; i < copy.Length; i++)
        {
            if (copy[i] != null)
                copy[i].FinishManually();
        }

        activeArrows.Clear();
    }

    #endregion

    #region Jump Retreat

    private void EnterJumpRetreat()
    {
        if (jumpParam.disableHitboxesWhileJumping)
            SetDirectTackleHitboxesActive(false);

        effectBonePlayer.PlayEffect(5);
        effectBonePlayer.PlayEffect(6);

        SetMeleeAttackHitboxesActive(false);

        jumpStartPosition = transform.position;

        if (jumpToOppositeSideAfterStun)
        {
            jumpTargetStageSide = GetOppositeSide(currentStageSide);
            jumpEndPosition = GetJumpPositionForSide(jumpTargetStageSide);
            jumpToOppositeSideAfterStun = false;
        }
        else
        {
            jumpTargetStageSide = currentStageSide;
            jumpEndPosition = SelectJumpEndPosition();
        }

        jumpTimer = 0f;
        jumpPhaseTimer = 0f;
        jumpPhase = JumpPhase.JumpStartAnimation;
        animationEventFinished = false;

        PlayBossAnimation(
            bossAnimationParam.jumpStart,
            bossAnimationParam.jumpStartBlendTime,
            bossAnimationParam.forceRestartAnimations
        );
    }

    private void UpdateJumpRetreat(float dt)
    {
        jumpPhaseTimer += dt;

        switch (jumpPhase)
        {
            case JumpPhase.JumpStartAnimation:
                UpdateJumpStart();
                break;

            case JumpPhase.JumpLoopMove:
                UpdateJumpLoopMove(dt);
                break;

            case JumpPhase.LandingAnimation:
                UpdateJumpLanding();
                break;
        }
    }

    private void UpdateJumpStart()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.jumpStart,
            jumpPhaseTimer,
            animationEventFinished,
            bossAnimationParam.jumpStart
        );

        if (!finished)
            return;

        BeginJumpLoopMove();
    }

    private void BeginJumpLoopMove()
    {
        animationEventFinished = false;
        jumpPhaseTimer = 0f;
        jumpTimer = 0f;
        jumpPhase = JumpPhase.JumpLoopMove;

        audioManager.PlaySe("AB_Jump");

        PlayBossAnimation(
            bossAnimationParam.jumpLoop,
            bossAnimationParam.jumpLoopBlendTime,
            bossAnimationParam.forceRestartAnimations
        );
    }

    private void UpdateJumpLoopMove(float dt)
    {
        jumpTimer += dt;

        float duration = Mathf.Max(0.01f, jumpParam.jumpDuration);
        float t = Mathf.Clamp01(jumpTimer / duration);

        Vector3 pos = Vector3.Lerp(
            jumpStartPosition,
            jumpEndPosition,
            t
        );

        pos.y += Mathf.Sin(t * Mathf.PI) * jumpParam.jumpHeight;

        transform.position = pos;

        if (t < 1f)
            return;

        transform.position = jumpEndPosition;
        currentStageSide = jumpTargetStageSide;

        BeginJumpLanding();
    }

    private void BeginJumpLanding()
    {
        animationEventFinished = false;
        jumpPhaseTimer = 0f;
        jumpPhase = JumpPhase.LandingAnimation;
        landingFacingAligned = false;

        landingFacingLockActive = false;
        landingFacingLockFramesRemaining = 0;
        enterIdleFromLanding = false;

        PlayBossAnimation(
            bossAnimationParam.jumpLanding,
            bossAnimationParam.jumpLandingBlendTime,
            bossAnimationParam.forceRestartAnimations
        );
    }

    private void UpdateJumpLanding()
    {
        TryAlignFacingDuringLanding();

        bool finished = IsStateAnimationFinished(
            animationParam.jumpLanding,
            jumpPhaseTimer,
            animationEventFinished,
            bossAnimationParam.jumpLanding
        );

        if (!finished)
            return;

        if (jumpParam.alignFacingOnLandingEnd)
            StartLandingFacingLock();

        landingFacingLockFramesRemaining =
            Mathf.Max(0, jumpParam.landingFacingLockFramesAfterIdle);

        jumpPhase = JumpPhase.None;

        ApplyNormalHitboxMode();

        effectBonePlayer.StopEffect(5);
        effectBonePlayer.StopEffect(6);

        if (pendingAngerAfterJumpLanding)
        {
            pendingAngerAfterJumpLanding = false;
            jumpToOppositeSideAfterStun = false;
            forceNextAttackStraight = false;

            ChangeState(BossState.Anger);
            return;
        }

        enterIdleFromLanding = true;
        forceNextAttackStraight = true;

        ChangeState(BossState.Idle);
    }

    private Vector3 SelectJumpEndPosition()
    {
        if (jumpParam.jumpCircleCenters != null &&
            jumpParam.jumpCircleCenters.Length > 0)
        {
            List<Transform> valid = new List<Transform>();

            for (int i = 0; i < jumpParam.jumpCircleCenters.Length; i++)
            {
                if (jumpParam.jumpCircleCenters[i] != null)
                    valid.Add(jumpParam.jumpCircleCenters[i]);
            }

            if (valid.Count > 0)
            {
                Vector3 p = valid[UnityEngine.Random.Range(0, valid.Count)].position;
                p.y = GetBossMoveY();
                return p;
            }
        }

        Vector3 fallback = GetBossSideCenter();
        fallback.y = GetBossMoveY();
        return fallback;
    }

    private Vector3 GetJumpPositionForSide(StageSide side)
    {
        Vector3 pos = GetStageSideCenter(side);
        pos.y = GetBossMoveY();
        return pos;
    }

    private void FaceShootDirection()
    {
        Vector3 dir = GetCurrentShootDirection();
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void TryAlignFacingDuringLanding()
    {
        if (!jumpParam.alignFacingDuringLanding)
            return;

        if (landingFacingAligned)
            return;

        if (!IsCurrentAnimatorStatePastNormalizedTime(
                bossAnimationParam.jumpLanding,
                jumpParam.landingAlignNormalizedTime))
        {
            return;
        }

        StartLandingFacingLock();
        landingFacingAligned = true;
    }

    private void AlignBossToStageRoadForward()
    {
        Vector3 dir = GetCurrentShootDirection();
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            if (playerTarget != null)
            {
                dir = playerTarget.position - transform.position;
                dir.y = 0f;
            }
        }

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up
        );
    }

    private void StartLandingFacingLock()
    {
        if (!jumpParam.lockFacingAroundLanding)
            return;

        Vector3 dir = GetCurrentShootDirection();
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            if (playerTarget != null)
            {
                dir = playerTarget.position - transform.position;
                dir.y = 0f;
            }
        }

        if (dir.sqrMagnitude < 0.0001f)
            return;

        landingLockedRotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up
        );

        landingFacingLockActive = true;
        transform.rotation = landingLockedRotation;
    }

    private void UpdateLandingFacingLock()
    {
        if (!landingFacingLockActive)
            return;

        transform.rotation = landingLockedRotation;

        if (landingFacingLockFramesRemaining > 0)
        {
            landingFacingLockFramesRemaining--;
            return;
        }

        // JumpLanding中はロックを維持する
        if (state == BossState.JumpRetreat &&
            jumpPhase == JumpPhase.LandingAnimation)
        {
            return;
        }

        landingFacingLockActive = false;
    }

    #endregion

    #region Hurt / Anger / Dead

    private void EnterAnger()
    {
        isAngry = true;

        SetAnimatorBoolIfExists(
            bossAnimationParam.angryBool,
            true
        );

        audioManager.PlaySe("SB_Angry", this.gameObject.transform.position);

        PlayBossAnimation(
            bossAnimationParam.anger,
            bossAnimationParam.angerBlendTime,
            bossAnimationParam.forceRestartAnimations
        );
    }

    private void UpdateAnger(float dt)
    {
        bool finished = IsStateAnimationFinished(
            animationParam.anger,
            stateTimer,
            animationEventFinished,
            bossAnimationParam.anger
        );

        if (!finished)
        {
            // 保険としてTimerも使いたい場合だけ残す
            if (stateTimer < timingParam.angerDuration)
                return;
        }

        hasSelectedAngryAttack = false;
        forceNextAttackStraight = true;
        ChangeState(BossState.Idle);
    }

    private bool TryRequestAngerByHPThreshold()
    {
        if (isAngry)
            return false;

        if (pendingAngerAfterJumpLanding)
            return false;

        if (hp <= 0f)
            return false;

        if (hp > statusParam.angryHPThreshold)
            return false;

        pendingAngerAfterJumpLanding = true;
        jumpToOppositeSideAfterStun = true;

        ApplyAngerTransitionProtection();

        if (logState)
            Debug.Log($"{name} ArcherBoss Anger reserved after Jump Landing.");

        return true;
    }
    private void ForceBeginStunGetUpForAnger()
    {
        if (state != BossState.Stunned)
            return;

        if (stunPhase == StunPhase.GetUpAnimation)
            return;

        ApplyAngerTransitionProtection();

        BeginStunGetUp();

        if (stunParam.logStun)
            Debug.Log($"{name} ArcherBoss Stun interrupted for Anger transition.");
    }

    private bool IsAngerTransitionReserved()
    {
        return pendingAngerAfterJumpLanding && !isAngry;
    }

    private void ApplyAngerTransitionProtection()
    {
        if (!IsAngerTransitionReserved())
            return;

        if (stunParam.disableDamageHitboxOnAngerTransition)
        {
            SetDirectTackleHitboxesActive(false);
        }

        SetMeleeAttackHitboxesActive(false);
    }

    private void EnterDead()
    {
        StopMeleeAttackHitboxes();
        SetDirectTackleHitboxesActive(false);
        ForceFinishAllArrows();

        hitMaterialFlashPlayer?.StopFlash(true);

        attackPhase = AttackPhase.None;
        pendingCells.Clear();
        spawnedArrowsThisState = false;
        animationEventFinished = false;

        deadPhase = DeadPhase.DeathA_MoveToPoint;
        deadPhaseTimer = 0f;

        deathMoveStopped = false;
        deathWhiteEmissionActive = false;
        deathWhiteEmissionTimer = 0f;

        InputSystem.actions.FindActionMap("Player").Disable();

        PlayDeathCameraByCurrentStageSide();

        deathMoveStartPosition = transform.position;
        deathMoveTargetPosition = ResolveDeathMoveTargetPosition();
        deathStartRotation = transform.rotation;

        InitializeDeathEmission();

        PlayBossAnimation(
            bossAnimationParam.deathA,
            bossAnimationParam.deadBlendTime,
            bossAnimationParam.forceRestartAnimations
        );

        effectBonePlayer.StopAll();

        if (deathParam.logDeath)
        {
            Debug.Log(
                $"{name} DeathA Start. " +
                $"MoveMode:{deathParam.moveMode} " +
                $"Target:{deathMoveTargetPosition}"
            );
        }
    }

    private void UpdateDead(float dt)
    {
        deadPhaseTimer += dt;

        if (deathWhiteEmissionActive)
            UpdateDeathWhiteEmission(dt);

        switch (deadPhase)
        {
            case DeadPhase.DeathA_MoveToPoint:
                UpdateDeathAMove(dt);
                UpdateDeathAFinishCheck();
                break;

            case DeadPhase.DeathA_Wait:
                UpdateDeathAFinishCheck();
                break;

            case DeadPhase.DeathB_Burst:
                UpdateDeathBFinishCheck();
                break;

            case DeadPhase.Finished:
                break;
        }
    }

    private void UpdateDeathAMove(float dt)
    {
        if (deathMoveStopped)
            return;

        switch (deathParam.moveMode)
        {
            case DeathMoveMode.MatchDeathAAnimationTime:
                UpdateDeathAMoveByAnimationTime();
                break;

            case DeathMoveMode.MoveBySpeedUntilStop:
                UpdateDeathAMoveBySpeed(dt);
                break;
        }

        if (deathParam.lockRotationDuringDeathA &&
            deathParam.keepStartRotation)
        {
            transform.rotation = deathStartRotation;
        }
    }

    private void UpdateDeathAMoveByAnimationTime()
    {
        float duration = Mathf.Max(0.01f, deathParam.deathAMoveDuration);
        float rate = Mathf.Clamp01(deadPhaseTimer / duration);

        transform.position = Vector3.Lerp(
            deathMoveStartPosition,
            deathMoveTargetPosition,
            rate
        );

        if (rate >= 1f)
        {
            transform.position = deathMoveTargetPosition;
            StopDeathAMoveOnly();
        }
    }

    private void UpdateDeathAMoveBySpeed(float dt)
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            deathMoveTargetPosition,
            deathParam.deathAMoveSpeed * dt
        );

        float distance = Vector3.Distance(
            transform.position,
            deathMoveTargetPosition
        );

        if (distance <= deathParam.reachDistance)
        {
            transform.position = deathMoveTargetPosition;
            StopDeathAMoveOnly();
        }
    }

    private void StopDeathAMoveOnly()
    {
        deathMoveStopped = true;

        if (deathParam.stayAtCurrentPositionAfterMoveStop)
        {
            deathMoveStartPosition = transform.position;
        }

        if (deadPhase == DeadPhase.DeathA_MoveToPoint)
            deadPhase = DeadPhase.DeathA_Wait;

        if (deathParam.logDeath)
            Debug.Log($"{name} DeathA Move Stop");
    }

    private void UpdateDeathAFinishCheck()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.deathA,
            deadPhaseTimer,
            animationEventFinished,
            bossAnimationParam.deathA
        );

        if (!finished)
            return;

        BeginDeathB();
    }

    private void BeginDeathB()
    {
        deadPhase = DeadPhase.DeathB_Burst;
        deadPhaseTimer = 0f;
        animationEventFinished = false;

        PlayBossAnimation(
            bossAnimationParam.deathB,
            bossAnimationParam.deadBlendTime,
            bossAnimationParam.forceRestartAnimations
        );

        if (deathParam.logDeath)
            Debug.Log($"{name} DeathB Start");
    }

    private void StartDeathWhiteEmission()
    {
        if (deathParam.whiteEmissionMaterial == null)
            return;

        if (!deathEmissionInitialized || deathEmissionCaches.Count == 0)
            InitializeDeathEmission();

        if (deathEmissionCaches.Count == 0)
            return;

        deathWhiteEmissionActive = true;
        deathWhiteEmissionTimer = 0f;

        ApplyDeathWhiteEmission(0f);

        if (deathParam.logDeath)
            Debug.Log($"{name} Death White Emission Start");
    }

    private void UpdateDeathWhiteEmission(float dt)
    {
        deathWhiteEmissionTimer += dt;

        float duration = Mathf.Max(0.01f, deathParam.whiteEmissionFadeDuration);
        float t = Mathf.Clamp01(deathWhiteEmissionTimer / duration);

        float rate = deathParam.whiteEmissionCurve != null
            ? deathParam.whiteEmissionCurve.Evaluate(t)
            : t;

        rate = Mathf.Clamp01(rate);

        ApplyDeathWhiteEmission(rate);

        if (t >= 1f)
        {
            // 完了後も白状態を維持する
            deathWhiteEmissionActive = false;
            ApplyDeathWhiteEmission(1f);
        }
    }

    private void UpdateDeathBFinishCheck()
    {
        bool finished = IsStateAnimationFinished(
            animationParam.deathB,
            deadPhaseTimer,
            animationEventFinished,
            bossAnimationParam.deathB
        );

        if (!finished)
            return;

        FinishDeath();
    }

    private void FinishDeath()
    {
        deadPhase = DeadPhase.Finished;

        RestoreDeathEmission();

        if (deathParam.hideRenderersOnFinish)
            SetDeathRenderersVisible(false);

        if (deathParam.destroyOnFinish)
            Destroy(gameObject, Mathf.Max(0f, deathParam.destroyDelay));

        if (deathParam.logDeath)
            Debug.Log($"{name} Death Finished");
    }

    private Vector3 ResolveDeathMoveTargetPosition()
    {
        Transform target = null;

        switch (currentStageSide)
        {
            case StageSide.PlayerSide:
                target = deathParam.playerSideDeathPoint;
                break;

            case StageSide.BossSide:
                target = deathParam.bossSideDeathPoint;
                break;
        }

        if (target == null)
            target = deathParam.fallbackDeathPoint;

        if (target != null)
            return target.position;

        return transform.position + Vector3.up * 6f;
    }

    private void InitializeDeathEmission()
    {
        deathEmissionCaches.Clear();

        if (deathParam.whiteEmissionMaterial == null)
            return;

        Renderer[] renderers = deathParam.renderers;

        if ((renderers == null || renderers.Length == 0) &&
            deathParam.autoCollectRenderers)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (ShouldExcludeDeathRenderer(renderer))
                continue;

            Material[] originals = renderer.sharedMaterials;

            if (originals == null || originals.Length == 0)
                continue;

            DeathEmissionCache cache = new DeathEmissionCache
            {
                renderer = renderer,
                originalSharedMaterials = originals,
                runtimeEmissionMaterials = CreateDeathEmissionMaterials(originals.Length)
            };

            deathEmissionCaches.Add(cache);
        }

        deathEmissionInitialized = true;

        if (deathParam.logDeath)
        {
            Debug.Log(
                $"{name} DeathEmission Initialized. " +
                $"RendererCount:{deathEmissionCaches.Count}"
            );
        }
    }

    private Material[] CreateDeathEmissionMaterials(int count)
    {
        count = Mathf.Max(1, count);

        Material[] materials = new Material[count];

        for (int i = 0; i < count; i++)
        {
            bool replace =
                deathParam.replaceAllMaterialSlots ||
                i == deathParam.targetMaterialIndex;

            if (!replace)
                continue;

            materials[i] = new Material(deathParam.whiteEmissionMaterial);
            SetupDeathEmissionMaterial(materials[i], 0f);
        }

        return materials;
    }

    private void SetupDeathEmissionMaterial(Material mat, float rate)
    {
        if (mat == null)
            return;

        Color color = Color.Lerp(
            Color.black,
            deathParam.finalWhiteColor,
            rate
        );

        float intensity = Mathf.Lerp(
            deathParam.startIntensity,
            deathParam.endIntensity,
            rate
        );

        float alpha = Mathf.Lerp(
            deathParam.startAlpha,
            deathParam.endAlpha,
            rate
        );

        if (mat.HasProperty(deathParam.emissionColorProperty))
            mat.SetColor(deathParam.emissionColorProperty, color);

        if (mat.HasProperty(deathParam.intensityProperty))
            mat.SetFloat(deathParam.intensityProperty, intensity);

        if (mat.HasProperty(deathParam.alphaProperty))
            mat.SetFloat(deathParam.alphaProperty, alpha);
    }

    private void ApplyDeathWhiteEmission(float rate)
    {
        if (!deathEmissionInitialized || deathEmissionCaches.Count == 0)
            InitializeDeathEmission();

        if (deathEmissionCaches.Count == 0)
            return;

        for (int i = 0; i < deathEmissionCaches.Count; i++)
        {
            DeathEmissionCache cache = deathEmissionCaches[i];

            if (cache == null || cache.renderer == null)
                continue;

            Material[] originals = cache.originalSharedMaterials;

            if (originals == null || originals.Length == 0)
                continue;

            Material[] result = new Material[originals.Length];

            for (int m = 0; m < result.Length; m++)
            {
                bool replace =
                    deathParam.replaceAllMaterialSlots ||
                    m == deathParam.targetMaterialIndex;

                if (replace)
                {
                    Material emissionMat = cache.runtimeEmissionMaterials[m];

                    if (emissionMat == null)
                    {
                        emissionMat = new Material(deathParam.whiteEmissionMaterial);
                        cache.runtimeEmissionMaterials[m] = emissionMat;
                    }

                    SetupDeathEmissionMaterial(emissionMat, rate);
                    result[m] = emissionMat;
                }
                else
                {
                    result[m] = originals[m];
                }
            }

            cache.renderer.sharedMaterials = result;
        }
    }

    private void RestoreDeathEmission()
    {
        for (int i = 0; i < deathEmissionCaches.Count; i++)
        {
            DeathEmissionCache cache = deathEmissionCaches[i];

            if (cache == null || cache.renderer == null)
                continue;

            if (cache.originalSharedMaterials == null)
                continue;

            cache.renderer.sharedMaterials = cache.originalSharedMaterials;
        }
    }

    private bool ShouldExcludeDeathRenderer(Renderer renderer)
    {
        if (renderer == null)
            return true;

        string[] filters = deathParam.excludeNameContains;

        if (filters == null)
            return false;

        string objectName = renderer.gameObject.name;

        for (int i = 0; i < filters.Length; i++)
        {
            string filter = filters[i];

            if (string.IsNullOrEmpty(filter))
                continue;

            if (objectName.Contains(filter))
                return true;
        }

        return false;
    }

    private void SetDeathRenderersVisible(bool visible)
    {
        Renderer[] renderers = deathParam.renderers;

        if ((renderers == null || renderers.Length == 0) &&
            deathParam.autoCollectRenderers)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (ShouldExcludeDeathRenderer(renderers[i]))
                continue;

            renderers[i].enabled = visible;
        }
    }

    private void PlayDeathCameraByCurrentStageSide()
    {

        StageSide deathSide = ResolveDeathStageSideByPosition();

        TimeUIManager.Instance.SetCountDownStart(false);

        if (deathSide == StageSide.PlayerSide)
        {
            statusParam.Camera.BeginManualCut("Boss_B");
        }
        else if(deathSide == StageSide.BossSide) 
        {
            statusParam.Camera.BeginManualCut("Boss_A");
        }

    }
    private StageSide ResolveDeathStageSideByPosition()
    {
        if (stageLaneParam.playerSideCircleCenter == null ||
            stageLaneParam.bossSideCircleCenter == null)
        {
            return currentStageSide;
        }

        Vector3 pos = transform.position;

        float playerSideDistance =
            (pos - stageLaneParam.playerSideCircleCenter.position).sqrMagnitude;

        float bossSideDistance =
            (pos - stageLaneParam.bossSideCircleCenter.position).sqrMagnitude;

        return playerSideDistance <= bossSideDistance
            ? StageSide.PlayerSide
            : StageSide.BossSide;
    }

    #endregion

    #region Stun

    private void EnterStunned()
    {
        attackPhase = AttackPhase.None;
        pendingCells.Clear();
        spawnedArrowsThisState = false;
        animationEventFinished = false;

        StopMeleeAttackHitboxes();

        // 今回のStunが「Melee完了によるStunLoop直行」かを先に保存する
        bool directLoopFromMeleeComplete =
            enterStunLoopDirectlyFromMeleeComplete;

        enterStunLoopDirectlyFromMeleeComplete = false;

        if (stunParam.clearActiveArrowsOnEnter)
            ForceFinishAllArrows();

        bool angerReserved = IsAngerTransitionReserved();

        if (angerReserved && stunParam.disableDamageHitboxOnAngerTransition)
        {
            SetDirectTackleHitboxesActive(false);
        }
        else
        {
            SetDirectTackleHitboxesActive(stunParam.keepDamageHitboxActive);
        }

        stunPhase = StunPhase.DownAnimation;
        stunPhaseTimer = 0f;

        // Angry移行予約がある場合は、これまで通りGetUpへ
        if (angerReserved)
        {
            if (stunParam.logStun)
                Debug.Log($"{name} ArcherBoss Stun entered with Anger reserved. Skip to GetUp.");

            BeginStunGetUp();
            return;
        }

        // Melee完了から入ったStunだけ、Downを飛ばしてLoopへ直接入る
        if (directLoopFromMeleeComplete)
        {
            if (stunParam.logStun)
                Debug.Log($"{name} ArcherBoss Melee complete. Skip StunDown and enter StunLoop.");

            BeginStunLoop();
            return;
        }

        // Player攻撃などでMelee中に被弾した場合はこちら。
        // 今まで通りStunDownから入る。
        PlayBossAnimation(
            bossAnimationParam.stunDown,
            bossAnimationParam.stunDownBlendTime,
            bossAnimationParam.forceRestartAnimations
        );

        if (stunParam.logStun)
            Debug.Log($"{name} ArcherBoss Stun Down Start");
    }

    private void UpdateStunned(float dt)
    {
        stunPhaseTimer += dt;

        switch (stunPhase)
        {
            case StunPhase.DownAnimation:
                UpdateStunDown();
                break;

            case StunPhase.DownLoop:
                UpdateStunLoop();
                break;

            case StunPhase.GetUpAnimation:
                UpdateStunGetUp();
                break;
        }
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
        stunPhase = StunPhase.DownLoop;

        stunEffectInstance = effectPlayer.PlayAt(2, stunParam.stunEffectPoint.position);

        PlayBossAnimation(
            bossAnimationParam.stunLoop,
            bossAnimationParam.stunLoopBlendTime,
            bossAnimationParam.forceRestartAnimations
        );

        if (stunParam.logStun)
            Debug.Log($"{name} ArcherBoss Stun Loop Start duration:{GetCurrentStunLoopDuration():F2}");
    }

    private void UpdateStunLoop()
    {
        if (stunPhaseTimer < GetCurrentStunLoopDuration())
            return;

        BeginStunGetUp();
    }

    private void BeginStunGetUp()
    {
        animationEventFinished = false;
        stunPhaseTimer = 0f;
        stunPhase = StunPhase.GetUpAnimation;

        PlayBossAnimation(
            bossAnimationParam.stunGetUp,
            bossAnimationParam.stunGetUpBlendTime,
            bossAnimationParam.forceRestartAnimations
        );

        if (stunParam.logStun)
            Debug.Log($"{name} ArcherBoss Stun GetUp Start");
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

        if (stunParam.logStun)
            Debug.Log($"{name} ArcherBoss Stunned End");

        stunPhase = StunPhase.None;

        if (stunParam.jumpToOppositeSideOnEnd ||
            jumpToOppositeSideAfterStun ||
            pendingAngerAfterJumpLanding)
        {
            if (stunParam.clearActiveArrowsBeforeJump)
                ForceFinishAllArrows();

            jumpToOppositeSideAfterStun = true;
            ChangeState(BossState.JumpRetreat);
            return;
        }

        ChangeState(BossState.Idle);
    }

    private float GetCurrentStunLoopDuration()
    {
        if (isAngry)
            return Mathf.Max(0f, stunParam.angryDuration);

        return Mathf.Max(0f, stunParam.normalDuration);
    }

    #endregion

    #region Attack Select

    private void SelectNextAttack()
    {
        BossAttackType selected;

        if (forceNextAttackStraight)
        {
            selected = BossAttackType.Straight;
            forceNextAttackStraight = false;
        }
        else if (attackSelectParam.forceFirstAttackToStraight &&
                 ((!isAngry && !hasSelectedNormalAttack) ||
                  (isAngry && !hasSelectedAngryAttack)))
        {
            selected = BossAttackType.Straight;
        }
        else
        {
            AttackPatternParam[] table = isAngry
                ? attackSelectParam.angryAttacks
                : attackSelectParam.normalAttacks;

            selected = SelectAttackFromTable(table);
        }

        RegisterSelectedAttack(selected);

        ChangeState(ToBossState(selected));
    }

    private BossAttackType SelectAttackFromTable(AttackPatternParam[] table)
    {
        if (table == null || table.Length == 0)
            return BossAttackType.Straight;

        List<AttackPatternParam> candidates = new List<AttackPatternParam>();

        for (int i = 0; i < table.Length; i++)
        {
            AttackPatternParam pattern = table[i];

            if (pattern == null)
                continue;

            if (pattern.chancePercent <= 0f)
                continue;

            if (IsConsecutiveLimited(pattern))
                continue;

            candidates.Add(pattern);
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && table[i].chancePercent > 0f)
                    candidates.Add(table[i]);
            }
        }

        float total = 0f;

        for (int i = 0; i < candidates.Count; i++)
            total += candidates[i].chancePercent;

        if (total <= 0f)
            return BossAttackType.Straight;

        float r = UnityEngine.Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            acc += candidates[i].chancePercent;

            if (r <= acc)
                return candidates[i].attackType;
        }

        return candidates[candidates.Count - 1].attackType;
    }

    private bool IsConsecutiveLimited(AttackPatternParam pattern)
    {
        if (!pattern.useConsecutiveLimit)
            return false;

        if (!hasLastAttackType)
            return false;

        if (lastAttackType != pattern.attackType)
            return false;

        return consecutiveAttackCount >= Mathf.Max(1, pattern.maxConsecutiveCount);
    }

    private void RegisterSelectedAttack(BossAttackType attack)
    {
        if (hasLastAttackType && lastAttackType == attack)
            consecutiveAttackCount++;
        else
            consecutiveAttackCount = 1;

        lastAttackType = attack;
        hasLastAttackType = true;

        if (isAngry)
            hasSelectedAngryAttack = true;
        else
            hasSelectedNormalAttack = true;

        if (attackSelectParam.logAttackSelect)
            Debug.Log($"{name} ArcherBoss Attack => {attack}");
    }

    private BossState ToBossState(BossAttackType attack)
    {
        switch (attack)
        {
            case BossAttackType.Straight:
                return BossState.StraightShot;

            case BossAttackType.Arc:
                return BossState.ArcShot;

            case BossAttackType.LaneArcBurst:
                return BossState.LaneArcBurst;

            case BossAttackType.LockOnDrop:
                return BossState.LockOnDropShot;

            case BossAttackType.AngryBarrage:
                return BossState.AngryBarrage;
        }

        return BossState.StraightShot;
    }

    private void ChangeToAlternativeAttack(BossAttackType excludedAttack)
    {
        AttackPatternParam[] table = isAngry
            ? attackSelectParam.angryAttacks
            : attackSelectParam.normalAttacks;

        BossAttackType alternative = SelectAttackFromTableExcluding(
            table,
            excludedAttack
        );

        if (alternative == excludedAttack)
        {
            if (logState)
                Debug.LogWarning($"{name} ArcherBoss could not find alternative attack. Back to Idle.");

            ChangeState(BossState.Idle);
            return;
        }

        if (logState)
        {
            Debug.LogWarning(
                $"{name} ArcherBoss skipped {excludedAttack} because prefab is missing. " +
                $"Alternative => {alternative}"
            );
        }

        RegisterSelectedAttack(alternative);
        ChangeState(ToBossState(alternative));
    }

    private BossAttackType SelectAttackFromTableExcluding(
    AttackPatternParam[] table,
    BossAttackType excludedAttack)
    {
        if (table == null || table.Length == 0)
            return excludedAttack;

        List<AttackPatternParam> candidates = new List<AttackPatternParam>();

        for (int i = 0; i < table.Length; i++)
        {
            AttackPatternParam pattern = table[i];

            if (pattern == null)
                continue;

            if (pattern.attackType == excludedAttack)
                continue;

            if (pattern.chancePercent <= 0f)
                continue;

            if (IsAttackPrefabMissing(pattern.attackType))
                continue;

            if (IsConsecutiveLimited(pattern))
                continue;

            candidates.Add(pattern);
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < table.Length; i++)
            {
                AttackPatternParam pattern = table[i];

                if (pattern == null)
                    continue;

                if (pattern.attackType == excludedAttack)
                    continue;

                if (pattern.chancePercent <= 0f)
                    continue;

                if (IsAttackPrefabMissing(pattern.attackType))
                    continue;

                candidates.Add(pattern);
            }
        }

        if (candidates.Count == 0)
            return excludedAttack;

        float total = 0f;

        for (int i = 0; i < candidates.Count; i++)
            total += candidates[i].chancePercent;

        if (total <= 0f)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)].attackType;

        float r = UnityEngine.Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            acc += candidates[i].chancePercent;

            if (r <= acc)
                return candidates[i].attackType;
        }

        return candidates[candidates.Count - 1].attackType;
    }

    private bool IsAttackPrefabMissing(BossAttackType attackType)
    {
        switch (attackType)
        {
            case BossAttackType.Straight:
                return !TryResolveStraightArrowPrefab(out _, out _);

            case BossAttackType.Arc:
                return !TryResolveArcArrowPrefab(out _, out _);

            case BossAttackType.LaneArcBurst:
                return !TryResolveLaneArcBurstArrowPrefab(out _, out _);

            case BossAttackType.LockOnDrop:
                return !TryResolveLockOnDropArrowPrefab(out _, out _);

            case BossAttackType.AngryBarrage:
                return !TryResolveAngryBarrageArrowPrefab(out _, out _);
        }

        return true;
    }

    #endregion

    #region Hit Receive

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (selfHitbox == null || other == null)
            return;

        if (state == BossState.MeleeAttack &&
            IsMeleeAttackHitbox(selfHitbox.gameObject))
        {
            HandleMeleeAttackHit(selfHitbox, other);
        }
    }

    public void OnHit(HitEventData data)
    {
        if (state == BossState.Dead)
            return;

        if (!bossStarted)
            return;

        if (IsAngerTransitionReserved())
            return;

        if (!IsBossDamageHit(data))
            return;

        if (localTime < lastDirectHitTime + hitboxParam.directHitCooldown)
            return;

        lastDirectHitTime = localTime;

        float damage = ResolveDamageFromHitEventData(data);

        TakeDirectDamage(damage, data.contactPoint);
    }
    private float ResolveDamageFromHitEventData(HitEventData data)
    {
        if (data.payload is EnemyAttackPayload enemyAttack)
            return Mathf.Max(0f, enemyAttack.damage);

        if (data.payload is BlowPayload blow)
            return ResolveDamageFromBlow(blow);

        if (data.payload is ChainPayload chain)
            return Mathf.Max(0f, chain.damage);

        return statusParam.defaultTackleDamage;
    }

    private float ResolveDamageFromBlow(BlowPayload blow)
    {
        float rate = Mathf.Clamp01(blow.powerRate);
        float constant = Mathf.Max(0f, blow.powerConstant);

        if (constant <= 0f)
            return statusParam.defaultTackleDamage;

        return Mathf.Max(0f, constant * rate);
    }

    private void TakeDirectDamage(float damage, Vector3 hitPoint)
    {
        if (damage <= 0f)
            return;

        // 怒り移行予約後は、追加ダメージを受けない
        if (IsAngerTransitionReserved())
            return;

        // 被弾によるStunは、Melee完了扱いではない
        enterStunLoopDirectlyFromMeleeComplete = false;

        hp -= damage;

        hitMaterialFlashPlayer?.PlayFlash();

        if (logHit)
        {
            Debug.Log(
                $"{name} ArcherBoss Damage:{damage:F1} " +
                $"HP:{hp:F1} " +
                $"State:{state}"
            );
        }

        if (hp <= 0f)
        {
            hp = 0f;
            ChangeState(BossState.Dead);
            return;
        }

        bool requestedAnger = TryRequestAngerByHPThreshold();

        if (requestedAnger && state == BossState.Stunned)
        {
            ForceBeginStunGetUpForAnger();
            return;
        }

        if (state == BossState.Stunned)
            return;

        if (state == BossState.JumpRetreat ||
            state == BossState.Anger)
        {
            return;
        }

        ChangeState(BossState.Stunned);
    }

    private bool IsBossDamageHit(HitEventData data)
    {
        if (state == BossState.JumpRetreat ||
            state == BossState.Anger ||
            state == BossState.Dead)
        {
            return false;
        }

        // Stunned中もタコ殴りできるように許可する
        if (state == BossState.Stunned)
            return true;

        // Hitboxが未登録なら、Bossがreceiverになっている時点で受ける
        if (hitboxParam.directTackleReceiveHitboxes == null ||
            hitboxParam.directTackleReceiveHitboxes.Length == 0)
        {
            return true;
        }

        GameObject target = data.targetHitbox;

        if (target == null && data.targetObject != null)
            target = data.targetObject;

        if (target == null)
            return true;

        if (!IsObjectInArray(target, hitboxParam.directTackleReceiveHitboxes))
        {
            if (logHit)
            {
                Debug.Log(
                    $"{name} ArcherBoss Hit ignored by hitbox filter. " +
                    $"target:{GetObjectName(target)} payload:{GetPayloadName(data)}"
                );
            }

            return false;
        }

        // LayerMaskが0ならLayer判定なし
        if (hitboxParam.playerAttackLayer.value != 0 &&
            data.attackerObject != null)
        {
            int bit = 1 << data.attackerObject.layer;

            if ((bit & hitboxParam.playerAttackLayer.value) == 0)
            {
                if (logHit)
                {
                    Debug.Log(
                        $"{name} ArcherBoss Hit ignored by layer. " +
                        $"attacker:{GetObjectName(data.attackerObject)} " +
                        $"layer:{LayerMask.LayerToName(data.attackerObject.layer)}"
                    );
                }

                return false;
            }
        }

        return true;
    }

    private void HandleMeleeAttackHit(Hitbox selfHitbox, Collider other)
    {
        if (hitboxParam.meleeTargetLayer.value != 0)
        {
            int bit = 1 << other.gameObject.layer;

            if ((bit & hitboxParam.meleeTargetLayer.value) == 0)
                return;
        }

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

        if (hitboxParam.hitSameTargetOncePerMelee &&
            meleeHitTargets.Contains(receiverObject))
        {
            return;
        }

        meleeHitTargets.Add(receiverObject);

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = selfHitbox.gameObject,

            targetObject = receiverObject,
            targetHitbox = targetHitbox != null
                ? targetHitbox.gameObject
                : other.gameObject,

            contactPoint = other.ClosestPoint(selfHitbox.transform.position),

            payload = new EnemyAttackPayload
            {
                damage = hitboxParam.meleePlayerDamage
            }
        };

        if (logHit)
        {
            Debug.Log(
                $"{name} Melee Hit target:{receiverObject.name} " +
                $"damage:{hitboxParam.meleePlayerDamage}"
            );
        }

        receiver.OnHit(data);
    }

    #endregion

    #region Hitbox

    private void ApplyNormalHitboxMode()
    {
        SetDirectTackleHitboxesActive(true);
    }

    private void SetDirectTackleHitboxesActive(bool active)
    {
        SetHitboxObjectsActive(hitboxParam.directTackleReceiveHitboxes, active);

        if (hitboxParam.logHitboxSwitch)
            Debug.Log($"{name} ArcherBoss DirectTackleHitboxes => {active}");
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

    private bool IsObjectInArray(GameObject obj, GameObject[] array)
    {
        if (obj == null || array == null)
            return false;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == null)
                continue;

            if (obj == array[i])
                return true;

            if (obj.transform.IsChildOf(array[i].transform))
                return true;
        }

        return false;
    }

    private void SetMeleeAttackHitboxesActive(bool active)
    {
        SetHitboxObjectsActive(hitboxParam.meleeAttackHitboxes, active);

        if (hitboxParam.logHitboxSwitch)
            Debug.Log($"{name} ArcherBoss MeleeHitboxes => {active}");
    }

    private bool IsMeleeAttackHitbox(GameObject obj)
    {
        return IsObjectInArray(obj, hitboxParam.meleeAttackHitboxes);
    }

    #endregion

    #region Stage / Grid Utility

    private void InitializeCurrentStageSideFromPosition()
    {
        Vector3 current = transform.position;
        Vector3 playerSide = GetPlayerSideCenter();
        Vector3 bossSide = GetBossSideCenter();

        current.y = 0f;
        playerSide.y = 0f;
        bossSide.y = 0f;

        float distToPlayerSide = Vector3.Distance(current, playerSide);
        float distToBossSide = Vector3.Distance(current, bossSide);

        currentStageSide =
            distToPlayerSide < distToBossSide
                ? StageSide.PlayerSide
                : StageSide.BossSide;
    }

    private Vector3 GetStageSideCenter(StageSide side)
    {
        return side == StageSide.BossSide
            ? GetBossSideCenter()
            : GetPlayerSideCenter();
    }

    private StageSide GetOppositeSide(StageSide side)
    {
        return side == StageSide.BossSide
            ? StageSide.PlayerSide
            : StageSide.BossSide;
    }

    private Vector3 GetShootDirectionFromSide(StageSide side)
    {
        Vector3 forward = GetStageForward();

        return side == StageSide.BossSide
            ? -forward
            : forward;
    }

    private Vector3 GetCurrentShootDirection()
    {
        return GetShootDirectionFromSide(currentStageSide);
    }

    private Vector3 GetDirectionTowardCurrentBossSide()
    {
        // プレイヤーの「前」に落とす方向。
        // Bossが今いる側へ向かう方向。
        Vector3 forward = GetStageForward();

        return currentStageSide == StageSide.BossSide
            ? forward
            : -forward;
    }

    private Vector3 GetPlayerSideCenter()
    {
        if (stageLaneParam.playerSideCircleCenter != null)
            return stageLaneParam.playerSideCircleCenter.position;

        return transform.position - Vector3.forward * 30f;
    }

    private Vector3 GetBossSideCenter()
    {
        if (stageLaneParam.bossSideCircleCenter != null)
            return stageLaneParam.bossSideCircleCenter.position;

        return transform.position;
    }

    private Vector3 GetStageForward()
    {
        Vector3 dir = GetBossSideCenter() - GetPlayerSideCenter();
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        return dir.normalized;
    }

    private Vector3 GetStageRight()
    {
        return Vector3.Cross(Vector3.up, GetStageForward()).normalized;
    }

    private float GetStageLength()
    {
        Vector3 a = GetPlayerSideCenter();
        Vector3 b = GetBossSideCenter();

        a.y = 0f;
        b.y = 0f;

        return Mathf.Max(0.1f, Vector3.Distance(a, b));
    }

    private float GetAvailableWidth()
    {
        return Mathf.Max(
            0.1f,
            stageLaneParam.stageWidth - stageLaneParam.sideMargin * 2f
        );
    }

    private float GetGridHeight()
    {
        return Mathf.Max(
            0.1f,
            stageLaneParam.gridMaxY - stageLaneParam.gridMinY
        );
    }

    private float GetLaneOffset(int laneIndex, int laneDivisions)
    {
        laneDivisions = Mathf.Max(1, laneDivisions);

        if (laneDivisions == 1)
            return 0f;

        laneIndex = Mathf.Clamp(laneIndex, 0, laneDivisions - 1);

        float width = GetAvailableWidth();
        float cellWidth = width / laneDivisions;

        // 各セルの中央に置く
        return -width * 0.5f +
               cellWidth * laneIndex +
               cellWidth * 0.5f;
    }

    private float GetRowCenterY(int rowIndex, int rowDivisions)
    {
        rowDivisions = Mathf.Max(1, rowDivisions);

        float groundY = GetStageGroundY();
        float cellHeight = GetGridHeight() / rowDivisions;

        return groundY +
               stageLaneParam.gridMinY +
               cellHeight * rowIndex +
               cellHeight * 0.5f;
    }

    private int GetLaneIndexFromPosition(Vector3 position, int laneDivisions)
    {
        laneDivisions = Mathf.Max(1, laneDivisions);

        if (laneDivisions == 1)
            return 0;

        Vector3 center = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();

        float width = GetAvailableWidth();
        float halfWidth = width * 0.5f;

        float localX = Vector3.Dot(position - center, right);

        // -halfWidth ～ +halfWidth を 0～1 に変換
        float t = Mathf.InverseLerp(-halfWidth, halfWidth, localX);

        // 念のため範囲外を丸める
        t = Mathf.Clamp01(t);

        // 0～1 を laneDivisions 個に分割
        int index = Mathf.FloorToInt(t * laneDivisions);

        // t == 1 の時だけ laneDivisions になってしまうのでClamp
        return Mathf.Clamp(index, 0, laneDivisions - 1);
    }

    private Vector3 GetBossShootPositionForLane(
    int laneIndex,
    int laneDivisions,
    StageSide side)
    {
        Vector3 center = GetStageSideCenter(side);
        Vector3 right = GetStageRight();

        Vector3 pos = center + right * GetLaneOffset(laneIndex, laneDivisions);

        // Boss本体は地面Yではなく、Boss用の移動高さを使う
        pos.y = GetBossMoveY();

        return pos;
    }

    private Vector3 GetStraightArrowStart(
    GridCell cell,
    int divisions,
    int rows,
    bool expandHeightToPlayerY,
    ArrowSpawnHeightMode heightMode,
    BossAttackType attackType)
    {
        Vector3 sideCenter = GetStageSideCenter(currentStageSide);
        Vector3 right = GetStageRight();
        Vector3 shootDir = GetCurrentShootDirection();

        Vector3 pos =
            sideCenter +
            right * GetLaneOffset(cell.lane, divisions) -
            shootDir * stageLaneParam.arrowStartBehindBoss;

        pos.y = ResolveStraightSpawnY(
            cell,
            rows,
            expandHeightToPlayerY,
            heightMode,
            attackType
        );

        return pos;
    }

    private float ResolveStraightSpawnY(
    GridCell cell,
    int rows,
    bool expandHeightToPlayerY,
    ArrowSpawnHeightMode heightMode,
    BossAttackType attackType)
    {
        if (TryGetMuzzlePosition(attackType, out Vector3 muzzlePosition))
            return muzzlePosition.y;

        switch (heightMode)
        {
            case ArrowSpawnHeightMode.PrefabStraightHeight:
                return GetStageGroundY() + GetPrefabStraightSpawnHeightOffset();

            case ArrowSpawnHeightMode.CellRowCenter:
                return GetRowCenterY(cell.row, rows);

            case ArrowSpawnHeightMode.LaneSizedCenter:
            default:
                break;
        }

        float bottomY;
        float topY;

        if (expandHeightToPlayerY)
        {
            GetNormalStraightVerticalRange(
                out bottomY,
                out topY
            );
        }
        else
        {
            GetCellVerticalRange(
                cell,
                rows,
                out bottomY,
                out topY
            );
        }

        return (bottomY + topY) * 0.5f;
    }

    private Vector3 ClampToStageLength(Vector3 position)
    {
        Vector3 playerCenter = GetPlayerSideCenter();
        Vector3 bossCenter = GetBossSideCenter();
        Vector3 forward = GetStageForward();
        Vector3 right = GetStageRight();

        Vector3 diff = position - playerCenter;

        float z = Vector3.Dot(diff, forward);
        float x = Vector3.Dot(position - bossCenter, right);

        z = Mathf.Clamp(z, 0f, GetStageLength());
        x = Mathf.Clamp(x, -GetAvailableWidth() * 0.5f, GetAvailableWidth() * 0.5f);

        Vector3 result =
            playerCenter +
            forward * z +
            right * x;

        result.y = position.y;

        return result;
    }

    private Vector3 GetCellArrowSize(
    GridCell cell,
    int divisions,
    int rows,
    bool expandHeightToPlayerY)
    {
        float cellWidth = GetAvailableWidth() / Mathf.Max(1, divisions);

        float bottomY;
        float topY;

        if (expandHeightToPlayerY)
        {
            GetNormalStraightVerticalRange(
                out bottomY,
                out topY
            );
        }
        else
        {
            GetCellVerticalRange(
                cell,
                rows,
                out bottomY,
                out topY
            );
        }

        float cellHeight = Mathf.Max(0.01f, topY - bottomY);

        return new Vector3(
            cellWidth * arrowSpawnParam.cellWidthScale,
            cellHeight * arrowSpawnParam.cellHeightScale,
            arrowSpawnParam.arrowDepth
        );
    }

    private Vector3 GetCellArrowSizeWithScale(
    GridCell cell,
    int divisions,
    int rows,
    bool expandHeightToPlayerY,
    float widthScale,
    float heightScale,
    float depth)
    {
        float cellWidth = GetAvailableWidth() / Mathf.Max(1, divisions);

        float bottomY;
        float topY;

        if (expandHeightToPlayerY)
        {
            GetNormalStraightVerticalRange(
                out bottomY,
                out topY
            );
        }
        else
        {
            GetCellVerticalRange(
                cell,
                rows,
                out bottomY,
                out topY
            );
        }

        float cellHeight = Mathf.Max(0.01f, topY - bottomY);

        return new Vector3(
            cellWidth * widthScale,
            cellHeight * heightScale,
            depth
        );
    }

    private float GetStraightArrowTravelDistance()
    {
        if (stageLaneParam.straightMaxTravelDistance > 0f)
            return stageLaneParam.straightMaxTravelDistance;

        return GetStageLength() +
               stageLaneParam.arrowStartBehindBoss +
               stageLaneParam.extraTravelDistance;
    }

    private bool ContainsCell(List<GridCell> cells, GridCell target)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].lane == target.lane &&
                cells[i].row == target.row)
            {
                return true;
            }
        }

        return false;
    }

    private void FacePlayerPlanar()
    {
        if (playerTarget == null)
            return;

        Vector3 dir = playerTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private float GetStageGroundY()
    {
        if (stageLaneParam.autoGroundYFromBossSideCircleCenter &&
            stageLaneParam.bossSideCircleCenter != null)
        {
            return stageLaneParam.bossSideCircleCenter.position.y +
                   stageLaneParam.groundYOffset;
        }

        return stageLaneParam.bossGroundY;
    }

    private void GetCellVerticalRange(
    GridCell cell,
    int rows,
    out float bottomY,
    out float topY)
    {
        rows = Mathf.Max(1, rows);

        float groundY = GetStageGroundY();
        float cellHeight = GetGridHeight() / rows;

        bottomY =
            groundY +
            stageLaneParam.gridMinY +
            cellHeight * cell.row;

        topY = bottomY + cellHeight;
    }

    private void GetNormalStraightVerticalRange(
    out float bottomY,
    out float topY)
    {
        float groundY = GetStageGroundY();

        bottomY = groundY + stageLaneParam.gridMinY;
        topY = groundY + stageLaneParam.gridMaxY;

        if (straightShotParam.expandHeightToPlayerY && playerTarget != null)
        {
            topY = Mathf.Max(
                topY,
                playerTarget.position.y + straightShotParam.playerHeightMargin
            );
        }

        float height = topY - bottomY;

        if (height < straightShotParam.minNormalArrowHeight)
        {
            float center = (bottomY + topY) * 0.5f;
            float half = straightShotParam.minNormalArrowHeight * 0.5f;

            bottomY = center - half;
            topY = center + half;
        }
    }

    private float GetPrefabStraightSpawnHeightOffset()
    {
        return Mathf.Max(0f, straightShotParam.prefabSpawnHeightFromGround);
    }

    private float GetBossMoveY()
    {
        if (stageLaneParam.keepInitialBossY)
            return initialBossY;

        return GetStageGroundY() + stageLaneParam.bossRootHeightFromGround;
    }

    private Transform GetMuzzleTransform(BossAttackType attackType)
    {
        switch (attackType)
        {
            case BossAttackType.Straight:
                if (arrowMuzzleParam.straightMuzzle != null)
                    return arrowMuzzleParam.straightMuzzle;
                break;

            case BossAttackType.Arc:
                if (arrowMuzzleParam.arcMuzzle != null)
                    return arrowMuzzleParam.arcMuzzle;
                break;

            case BossAttackType.LockOnDrop:
                if (arrowMuzzleParam.lockOnDropMuzzle != null)
                    return arrowMuzzleParam.lockOnDropMuzzle;
                break;

            case BossAttackType.AngryBarrage:
                if (arrowMuzzleParam.angryBarrageMuzzle != null)
                    return arrowMuzzleParam.angryBarrageMuzzle;
                break;
        }

        return arrowMuzzleParam.defaultMuzzle;
    }

    private bool TryGetMuzzlePosition(
        BossAttackType attackType,
        out Vector3 position)
    {
        Transform muzzle = GetMuzzleTransform(attackType);

        if (muzzle == null)
        {
            position = Vector3.zero;
            return false;
        }

        position = muzzle.TransformPoint(arrowMuzzleParam.localOffset);
        return true;
    }

    private bool TryGetAngryBarrageMuzzlePosition(out Vector3 position)
    {
        Transform muzzle = null;

        if (arrowMuzzleParam.angryBarrageMuzzle != null)
        {
            muzzle = arrowMuzzleParam.angryBarrageMuzzle;
        }
        else if (angryBarrageParam.fallbackToStraightMuzzle &&
                 arrowMuzzleParam.straightMuzzle != null)
        {
            muzzle = arrowMuzzleParam.straightMuzzle;
        }
        else if (arrowMuzzleParam.defaultMuzzle != null)
        {
            muzzle = arrowMuzzleParam.defaultMuzzle;
        }

        if (muzzle == null)
        {
            position = Vector3.zero;
            return false;
        }

        position = muzzle.TransformPoint(arrowMuzzleParam.localOffset);
        return true;
    }

    private Vector3 GetArcArrowStartFromMuzzleOrLane(int lane, int divisions)
    {
        Vector3 laneStart = GetArcArrowStartForLane(lane, divisions);

        if (TryGetMuzzlePosition(BossAttackType.Arc, out Vector3 muzzlePosition))
            return muzzlePosition;

        return laneStart;
    }

    private Vector3 GetLaneArcBurstStartFromMuzzleOrLane(
    GridCell cell,
    int divisions)
    {
        Vector3 laneStart = GetLaneArcBurstStart(cell, divisions);

        if (TryGetMuzzlePosition(BossAttackType.Arc, out Vector3 muzzlePosition))
            return muzzlePosition;

        return laneStart;
    }

    private float GetPlayerProgressTowardCurrentBossSide()
    {
        if (playerTarget == null)
            return 0f;

        Vector3 bossSideCenter = GetStageSideCenter(currentStageSide);
        Vector3 oppositeSideCenter = GetStageSideCenter(GetOppositeSide(currentStageSide));

        bossSideCenter.y = 0f;
        oppositeSideCenter.y = 0f;

        Vector3 player = playerTarget.position;
        player.y = 0f;

        Vector3 fromOppositeToBoss = bossSideCenter - oppositeSideCenter;
        float length = fromOppositeToBoss.magnitude;

        if (length <= 0.01f)
            return 0f;

        Vector3 dir = fromOppositeToBoss / length;
        float progress = Vector3.Dot(player - oppositeSideCenter, dir) / length;

        return Mathf.Clamp01(progress);
    }

    private bool IsPlayerCloseToCurrentBossSide(float threshold)
    {
        return GetPlayerProgressTowardCurrentBossSide() >= threshold;
    }

    #endregion

    #region Animation Event

    public void AnimEvent_ShotChargeEffect()
    {


        if (state == BossState.AngryBarrage)
        {
            effectPlayer.PlayAt(4 , arrowMuzzleParam.effectPoint.position);    
        }
        else
        {
            effectPlayer.PlayAt(3 , arrowMuzzleParam.effectPoint.position);
        }
    }
    public void AnimEvent_SpawnStraightShot()
    {
        if (state == BossState.StraightShot)
        {
            if (!animationEventParam.spawnStraightByAnimationEvent)
                return;

            SpawnStraightShot();
            attackPhase = AttackPhase.WaitEnd;
            return;
        }

        if (state == BossState.AngryBarrage)
        {
            if (!animationEventParam.spawnAngryBarrageByAnimationEvent)
                return;

            SpawnAngryBarrage();
            attackPhase = AttackPhase.WaitEnd;
            return;
        }
    }

    public void AnimEvent_SpawnArcShot()
    {
        if (!animationEventParam.spawnArcByAnimationEvent)
            return;

        if (state == BossState.ArcShot)
        {
            SpawnArcShot();

            if (attackPhase == AttackPhase.Spawn ||
                attackPhase == AttackPhase.WaitEnd)
            {
                attackPhase = AttackPhase.WaitEnd;
            }

            if (logState)
                Debug.Log($"{name} AnimEvent Spawn ArcShot");

            return;
        }

        if (state == BossState.LaneArcBurst)
        {
            SpawnLaneArcBurst();

            if (attackPhase == AttackPhase.Spawn ||
                attackPhase == AttackPhase.WaitEnd)
            {
                attackPhase = AttackPhase.WaitEnd;
            }

            if (logState)
                Debug.Log($"{name} AnimEvent Spawn LaneArcBurst");

            return;
        }
    }

    public void AnimEvent_SpawnLockOnDropArrow()
    {
        if (!animationEventParam.spawnLockOnDropByAnimationEvent)
            return;

        if (state != BossState.LockOnDropShot)
            return;

        int maxCount = Mathf.Max(1, lockOnDropParam.arrowCount);

        if (lockOnDropEventSpawnIndex >= maxCount)
            return;

        float fallOrderDelay = lockOnDropParam.useEventIndexAsFallOrderDelay
            ? lockOnDropEventSpawnIndex * Mathf.Max(0f, lockOnDropParam.dropInterval)
            : 0f;

        SpawnLockOnDropArrow(fallOrderDelay);

        lockOnDropEventSpawnIndex++;

        if (attackPhase == AttackPhase.Spawn ||
            attackPhase == AttackPhase.WaitEnd)
        {
            attackPhase = AttackPhase.WaitEnd;
        }

        if (logState)
            Debug.Log($"{name} AnimEvent Spawn LockOnDrop {lockOnDropEventSpawnIndex}/{maxCount}");
    }
    public void AnimEvent_AlignJumpLandingFacing()
    {
        if (state != BossState.JumpRetreat)
            return;

        if (jumpPhase != JumpPhase.LandingAnimation)
            return;

        StartLandingFacingLock();
        landingFacingAligned = true;
    }
    public void AnimEvent_MeleeHitboxOn()
    {
        if (state != BossState.MeleeAttack)
            return;

        meleeHitTargets.Clear();
        SetMeleeAttackHitboxesActive(true);
    }

    public void AnimEvent_MeleeHitboxOff()
    {
        SetMeleeAttackHitboxesActive(false);
    }

    public void AnimEvent_AngerEffectPlay()
    {
        if (effectPlayer == null)
            return;

        Vector3 dir = playerTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized);

        effectPlayer.PlayAt(1, this.gameObject.transform.position, rot);
    }

    public void AnimEvent_StopDeathAMove()
    {
        if (state != BossState.Dead)
            return;

        if (deadPhase != DeadPhase.DeathA_MoveToPoint)
            return;

        StopDeathAMoveOnly();
    }

    public void AnimEvent_StartDeathWhiteEmission()
    {
        if (state != BossState.Dead)
            return;

        if (deadPhase != DeadPhase.DeathB_Burst)
            return;

        StartDeathWhiteEmission();
    }

    public void AnimEvent_BossExplosion()
    {
        //effectBonePlayer.StopAll();

        audioManager.PlaySe("SB_Explosion");

        effectPlayer.Play(0);
    }

    public void AnimEvent_RestoreDeathEmission()
    {
        if (state != BossState.Dead)
            return;

        RestoreDeathEmission();

        deathWhiteEmissionActive = false;

        if (deathParam.logDeath)
            Debug.Log($"{name} Death Emission Restore");
    }

    public void AnimEvent_SlowForDeathLevatation()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 0.4f);
    }
    public void AnimEvent_SlowForDeathBoom()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 0.3f);
    }

    public void AnimEvent_SlowReset()
    {
        GameTimeManager.Instance.SlowLayer(TimeLayerType.Gameplay, 1.0f);
    }
    public void AnimEvent_WhereBossDies()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.AddKill(statusParam.enemyData);
    }

    public void AnimEvent_FinishState()
    {
        animationEventFinished = true;
    }

    #endregion

    #region Animation

    private bool IsStateFinished(StateAnimationFinishParam param, float fallbackDuration)
    {
        if (param == null)
            return stateTimer >= fallbackDuration;

        switch (param.finishMode)
        {
            case AnimationFinishMode.Timer:
                return stateTimer >= Mathf.Max(0.01f, param.duration);

            case AnimationFinishMode.AnimationEvent:
                return animationEventFinished;

            case AnimationFinishMode.AnimationEnd:
                return IsCurrentAnimationNearEnd(param.endNormalizedTime);
        }

        return stateTimer >= fallbackDuration;
    }

    private bool IsCurrentAnimationNearEnd(float normalizedEnd)
    {
        if (animator == null)
            return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(
            bossAnimationParam.animatorLayerIndex
        );

        return info.normalizedTime >= normalizedEnd;
    }

    private void PlayBossAnimation(string stateName, float blendTime, bool forceRestart)
    {
        if (animator == null)
            return;

        if (string.IsNullOrEmpty(stateName))
            return;

        string playName = stateName;

        if (bossAnimationParam.tryFullPathStateName &&
            !string.IsNullOrEmpty(bossAnimationParam.animatorLayerName))
        {
            playName = bossAnimationParam.animatorLayerName + "." + stateName;
        }

        if (forceRestart)
        {
            animator.Play(
                playName,
                bossAnimationParam.animatorLayerIndex,
                0f
            );
        }
        else
        {
            animator.CrossFadeInFixedTime(
                playName,
                Mathf.Max(0f, blendTime),
                bossAnimationParam.animatorLayerIndex
            );
        }
    }

    private bool IsStateAnimationFinished(
    StateAnimationFinishParam finishParam,
    float timer,
    bool eventFinished,
    string stateName)
    {
        if (finishParam == null)
            return timer >= 1.0f;

        switch (finishParam.finishMode)
        {
            case AnimationFinishMode.Timer:
                return timer >= Mathf.Max(0.01f, finishParam.duration);

            case AnimationFinishMode.AnimationEvent:
                return eventFinished;

            case AnimationFinishMode.AnimationEnd:
                return IsAnimatorStateFinished(
                    stateName,
                    finishParam.endNormalizedTime
                );
        }

        return timer >= Mathf.Max(0.01f, finishParam.duration);
    }

    private bool IsAnimatorStateFinished(
    string stateName,
    float endNormalizedTime)
    {
        if (animator == null)
            return true;

        if (string.IsNullOrEmpty(stateName))
            return true;

        int layer = GetAnimatorLayerIndex();

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);

        bool isTargetState =
            info.IsName(stateName) ||
            info.IsName($"{bossAnimationParam.animatorLayerName}.{stateName}");

        if (!isTargetState)
        {
            // CrossFade直後など、現在Stateではなく次State側に入っている場合の保険
            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);

                bool isNextTargetState =
                    nextInfo.IsName(stateName) ||
                    nextInfo.IsName($"{bossAnimationParam.animatorLayerName}.{stateName}");

                if (isNextTargetState)
                    return nextInfo.normalizedTime >= endNormalizedTime;
            }

            return false;
        }

        if (info.loop)
            return false;

        return info.normalizedTime >= endNormalizedTime;
    }

    private int GetAnimatorLayerIndex()
    {
        if (animator == null)
            return 0;

        if (!string.IsNullOrEmpty(bossAnimationParam.animatorLayerName))
        {
            int layer = animator.GetLayerIndex(bossAnimationParam.animatorLayerName);

            if (layer >= 0)
                return layer;
        }

        return Mathf.Clamp(
            bossAnimationParam.animatorLayerIndex,
            0,
            Mathf.Max(0, animator.layerCount - 1)
        );
    }

    private void EnsureIdleAnimationPlaying()
    {
        if (animator == null)
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(
            bossAnimationParam.animatorLayerIndex
        );

        if (!info.IsName(bossAnimationParam.idle))
        {
            PlayBossAnimation(
                bossAnimationParam.idle,
                bossAnimationParam.idleBlendTime,
                false
            );
        }
    }

    private void PauseAnimatorForExternalWait()
    {
        if (animator != null)
            animator.speed = 0f;
    }

    private void ResumeAnimatorFromExternalWait()
    {
        if (animator != null)
            animator.speed = 1f;
    }

    private string GetArcShotAnimationName()
    {
        switch (Mathf.Clamp(currentArcArrowCount, 1, 3))
        {
            case 1:
                return bossAnimationParam.arcShot1;

            case 2:
                return bossAnimationParam.arcShot2;

            case 3:
                return bossAnimationParam.arcShot3;
        }

        return bossAnimationParam.arcShot1;
    }

    private bool IsCurrentAnimatorStatePastNormalizedTime(
    string stateName,
    float normalizedTime)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrEmpty(stateName))
            return false;

        int layer = GetAnimatorLayerIndex();

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);

        bool isTargetState =
            info.IsName(stateName) ||
            info.IsName($"{bossAnimationParam.animatorLayerName}.{stateName}");

        if (!isTargetState)
        {
            if (!animator.IsInTransition(layer))
                return false;

            AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layer);

            bool isNextTargetState =
                nextInfo.IsName(stateName) ||
                nextInfo.IsName($"{bossAnimationParam.animatorLayerName}.{stateName}");

            if (!isNextTargetState)
                return false;

            return nextInfo.normalizedTime >= normalizedTime;
        }

        return info.normalizedTime >= normalizedTime;
    }

    private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrEmpty(paramName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName &&
                parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void SetAnimatorBoolIfExists(string paramName, bool value)
    {
        if (!HasAnimatorParameter(paramName, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(paramName, value);
    }

    private bool ShouldSpawnByAnimationEvent()
    {
        switch (state)
        {
            case BossState.StraightShot:
                return animationEventParam.spawnStraightByAnimationEvent;

            case BossState.ArcShot:
                return animationEventParam.spawnArcByAnimationEvent;

            case BossState.LaneArcBurst:
                // LaneArcBurst は ArcShot_1 を再利用するので Arc のEvent設定で管理
                return animationEventParam.spawnArcByAnimationEvent;

            case BossState.AngryBarrage:
                return animationEventParam.spawnAngryBarrageByAnimationEvent;

            case BossState.LockOnDropShot:
                return animationEventParam.spawnLockOnDropByAnimationEvent;
        }

        return false;
    }

    #endregion

    #region Utility

    private string GetObjectName(GameObject obj)
    {
        return obj != null ? obj.name : "null";
    }

    private string GetPayloadName(HitEventData data)
    {
        return data.payload != null ? data.payload.GetType().Name : "null";
    }

    #endregion

    #region Gizmos

    private void DrawStageGizmos()
    {
        if (!stageLaneParam.drawStageGizmos)
            return;

        Vector3 playerCenter = GetPlayerSideCenter();
        Vector3 bossCenter = GetBossSideCenter();
        Vector3 forward = GetStageForward();
        Vector3 right = GetStageRight();

        float halfW = GetAvailableWidth() * 0.5f;

        Vector3 a = playerCenter - right * halfW;
        Vector3 b = playerCenter + right * halfW;
        Vector3 c = bossCenter + right * halfW;
        Vector3 d = bossCenter - right * halfW;

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        if (!stageLaneParam.drawGridGizmos)
            return;

        int columns = Mathf.Max(1, stageLaneParam.debugGridColumns);
        int rows = Mathf.Max(1, stageLaneParam.debugGridRows);

        for (int x = 1; x < columns; x++)
        {
            float t = x / (float)columns;
            float offset = Mathf.Lerp(-halfW, halfW, t);

            Vector3 p0 = playerCenter + right * offset;
            Vector3 p1 = bossCenter + right * offset;

            Gizmos.DrawLine(p0, p1);
        }

        for (int y = 1; y < rows; y++)
        {
            float t = y / (float)rows;
            float height = Mathf.Lerp(
                stageLaneParam.gridMinY,
                stageLaneParam.gridMaxY,
                t
            );

            Vector3 p0 = playerCenter - right * halfW;
            Vector3 p1 = playerCenter + right * halfW;
            Vector3 p2 = bossCenter + right * halfW;
            Vector3 p3 = bossCenter - right * halfW;

            p0.y = height;
            p1.y = height;
            p2.y = height;
            p3.y = height;

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }
    }

    #endregion
}