using System;
using System.Collections.Generic;
using UnityEngine;

public class ArcherBossArrow : MonoBehaviour, IHitSource
{
    #region Enums

    public enum ArrowEndReason
    {
        LifeEnd,
        HitPlayer,
        HitField,
        Manual,
        DistanceEnd
    }

    private enum ArrowMode
    {
        None,
        Straight,
        ArcDrop,
        LockOnDrop,
        LaneApproachStraight
    }
    private enum LockOnDropPhase
    {
        None,
        // Boss側から上空へ飛ぶ
        Rise,
        // ロック位置の上で待つ
        Wait,
        // ロック位置へ落下
        Fall
    }

    private enum SpinAxis
    {
        X_Axis,
        Y_Axis,
        Z_Axis,
        Custom
    }

    private enum EffectForwardAxis
    {
        ZPlus,
        ZMinus,
        XPlus,
        XMinus,
        YPlus,
        YMinus
    }

    #endregion

    #region Serializable Params

    [Serializable]
    private class ColliderParam
    {
        [Header("Collision")]
        [Tooltip("未設定なら自分のColliderを使う")]
        public Collider rootCollider;

        [Tooltip("Triggerにする")]
        public bool forceTrigger = true;

        [Tooltip("Launch時にColliderサイズを矢サイズへ合わせる")]
        public bool resizeColliderOnLaunch = true;
    }

    [Serializable]
    private class SweepParam
    {
        [Header("Sweep")]
        public bool useSweep = true;

        [Tooltip("矢サイズから自動計算する場合の最低Sweep半径")]
        public float minRadius = 0.35f;

        [Tooltip("矢サイズに対するSweep半径倍率")]
        public float radiusScale = 0.45f;

        public bool ignoreSelf = true;
    }

    [Serializable]
    private class VisualParam
    {
        [Header("Temporary Model")]
        [Tooltip("モデル未作成時、子に仮Cubeを作る")]
        public bool createDebugArrowIfNoRenderer = true;

        [Tooltip("モデル未作成時に作る仮Cube")]
        public Vector3 debugArrowBaseScale = Vector3.one;

        [Tooltip("見た目のRoot。未設定ならこのTransform全体をScale")]
        public Transform visualRoot;

        [Header("Facing")]
        public bool faceMoveDirection = true;
    }
    [Serializable]
    private class ModelSpinParam
    {
        [Header("Spinning Model")]
        [Tooltip("回転させるモデル。未設定ならVisualRootを使う")]
        public Transform modelRoot;

        [Header("Flag Spin Model")]
        [Tooltip("回転させるか")]
        public bool spinEnable = false;

        [Header("Spin Axis")]
        [Tooltip("回転軸")]
        public SpinAxis spinAxis = SpinAxis.Y_Axis;

        [Tooltip("Custom選択時に使う回転軸")]
        public Vector3 customAxis = Vector3.up;

        [Header("Spin Speed")]
        [Tooltip("回転速度。マイナスにすると逆回転")]
        public float speed = 180f;

        [Header("Space")]
        [Tooltip("ONならローカル軸で回転。OFFならワールド軸で回転")]
        public bool useLocalSpace = true;
    }

    [Serializable]
    private class ArrowEffectParam
    {
        [Header("Trajectory Effect")]
        [Tooltip("矢の軌道Effectを使う")]
        public bool useTrajectoryEffect = true;

        [Tooltip("矢の軌道EffectのEffectPlayer番号")]
        public int trajectoryEffectIndex = 0;

        [Tooltip("矢の進行方向へどれくらい前にEffectを出すか")]
        public float trajectoryForwardOffset = 0.0f;

        [Tooltip("軌道Effectのワールド座標オフセット")]
        public Vector3 trajectoryWorldOffset = Vector3.zero;

        [Tooltip("ONなら軌道Effectを矢の移動方向へ向ける")]
        public bool faceMoveDirection = true;

        [Header("Trajectory Axis Correction")]
        [Tooltip("Effectプレハブの前方向")]
        public EffectForwardAxis prefabForwardAxis = EffectForwardAxis.ZPlus;

        [Tooltip("追加の回転補正")]
        public Vector3 rotationOffsetEuler = Vector3.zero;

        [Header("Warning Effect")]
        [Tooltip("ArcDrop系の着弾地点Warningを使う")]
        public bool useArcDropWarning = true;

        [Tooltip("LockOnDropの着弾地点Warningを使う")]
        public bool useLockOnDropWarning = true;

        [Tooltip("WarningEffectのEffectPlayer番号")]
        public int warningEffectIndex = 1;

        [Tooltip("WarningEffectの位置オフセット")]
        public Vector3 warningWorldOffset = Vector3.zero;

        [Tooltip("LockOnDropのRise中、ターゲット更新に合わせてWarningも動かす")]
        public bool updateLockOnWarningWhileTracking = true;

        [Header("Stop")]
        [Tooltip("矢が終了したら軌道Effectを止める")]
        public bool stopTrajectoryOnFinish = true;

        [Tooltip("矢が終了したらWarningEffectを止める")]
        public bool stopWarningOnFinish = true;
    }

    [Serializable]
    private class DebugParam
    {
        public bool logHit = true;
        public bool logFinish = false;

        [Header("Gizmos")]
        public bool drawGizmos = true;
        public bool drawVelocity = true;
        public bool drawArc = true;

        [Range(4, 64)]
        public int arcPreviewSegments = 24;
    }

    #endregion

    #region Inspector Fields

    [Header("Collider")]
    [SerializeField] private ColliderParam colliderParam = new ColliderParam();

    [Header("Sweep")]
    [SerializeField] private SweepParam sweepParam = new SweepParam();

    [Header("Visual")]
    [SerializeField] private VisualParam visualParam = new VisualParam();

    [Header("Model Spin")]
    [SerializeField] private ModelSpinParam modelSpinParam = new ModelSpinParam();

    [Header("Effect")]
    [SerializeField] private ArrowEffectParam arrowEffectParam = new ArrowEffectParam();

    [Header("Debug")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    #endregion

    #region Runtime Fields

    public event Action<ArcherBossArrow> OnArrowFinished;

    private ArcherBoss boss;
    private TimeAgent timeAgent;

    EffectPlayer effectPlayer;

    private ArrowMode mode = ArrowMode.None;

    private Vector3 velocity;
    private Vector3 launchPosition;
    private Vector3 arrowSize = Vector3.one;

    private float lifeTimer;
    private float lifeLimit = 5f;

    private float traveledDistance;
    private float maxTravelDistance = 30f;

    private bool finished;

    private int playerDamage;
    private LayerMask playerLayer;

    private Vector3 laneApproachTarget;
    private Vector3 laneApproachFinalDirection;
    private float laneApproachSpeed;
    private float laneApproachStraightSpeed;
    private float laneApproachReachDistance;
    private bool laneApproachFaceFinalDirection;
    private Vector3 laneApproachStart;
    private float laneApproachTimer;
    private float laneApproachDuration;
    private bool laneApproachUseDuration;

    private Vector3 arcStart;
    private Vector3 arcControl;
    private Vector3 arcEnd;
    private float arcTimer;
    private float arcDuration;

    private bool arcUseHorizontalSpeed;
    private float arcHorizontalSpeed;
    private float arcTraveledXZ;
    private float arcTotalXZDistance;
    private float arcHeightValue;

    private LockOnDropPhase lockOnDropPhase = LockOnDropPhase.None;

    private Vector3 lockOnStart;
    private Vector3 lockOnControl;
    private Vector3 lockOnDropStart;
    private Vector3 lockOnTarget;

    private float lockOnRiseTimer;
    private float lockOnRiseDuration;

    private float lockOnWaitTimer;
    private float lockOnWaitDuration;

    private float lockOnFallSpeed;
    private float lockOnHitDistance;

    private Func<Vector3> lockOnRetargetProvider;

    private float lockOnDropHeight;
    private float lockOnRiseArcHeight;
    private float lockOnFallOrderDelay;

    private bool lockOnTrackTargetDuringRise;
    private bool lockOnLockedTargetAfterRise;

    private EffectInstance trajectoryEffect;
    private EffectInstance warningEffect;

    private Transform trajectoryEffectPoint;
    private Vector3 lastEffectDirection = Vector3.forward;

    private readonly HashSet<GameObject> hitObjects =
        new HashSet<GameObject>();

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    private float ScaledDeltaTime => Time.deltaTime * TimeScale;

    #endregion

    #region Unity Events

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (finished)
            return;

        float dt = ScaledDeltaTime;

        lifeTimer += dt;

        if (lifeTimer >= lifeLimit)
        {
            Finish(ArrowEndReason.LifeEnd);
            return;
        }

        switch (mode)
        {
            case ArrowMode.Straight:
                UpdateStraight(dt);
                break;

            case ArrowMode.ArcDrop:
                UpdateArcDrop(dt);
                break;

            case ArrowMode.LockOnDrop:
                UpdateLockOnDrop(dt);
                break;

            case ArrowMode.LaneApproachStraight:
                UpdateLaneApproachStraight(dt);
                break;
        }

        FaceVelocity();

        UpdateTrajectoryEffectPoint();

        UpdateModelSpin(dt);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other, "OnTriggerEnter");
    }

    private void OnDestroy()
    {
        EndArrowEffects();
        DestroyTrajectoryEffectPoint();
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos();
    }

    #endregion

    #region Initialize

    private void Initialize()
    {
        if (colliderParam.rootCollider == null)
            colliderParam.rootCollider = GetComponent<Collider>();

        if (colliderParam.rootCollider == null)
            colliderParam.rootCollider = gameObject.AddComponent<BoxCollider>();

        if (colliderParam.forceTrigger)
            colliderParam.rootCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        CreateDebugArrowIfNeeded();
        CreateTrajectoryEffectPoint();
    }

    private void CreateDebugArrowIfNeeded()
    {
        if (!visualParam.createDebugArrowIfNoRenderer)
            return;

        Renderer renderer = GetComponentInChildren<Renderer>();

        if (renderer != null)
            return;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Debug_Arrow_Model";
        cube.transform.SetParent(transform);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = visualParam.debugArrowBaseScale;

        Collider col = cube.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        if (visualParam.visualRoot == null)
            visualParam.visualRoot = cube.transform;
    }

    #endregion

    #region Launch

    public void LaunchStraight(
    ArcherBoss boss,
    Vector3 start,
    Vector3 direction,
    float speed,
    float lifeTime,
    float maxDistance,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            maxDistance,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.Straight;

        Vector3 dir = direction;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        velocity = dir * Mathf.Max(0.01f, speed);

        FaceVelocity();
    }

    public void LaunchLaneApproachStraight(
    ArcherBoss boss,
    Vector3 start,
    Vector3 approachTarget,
    Vector3 finalDirection,
    float approachSpeed,
    float straightSpeed,
    float lifeTime,
    float maxDistance,
    float reachDistance,
    bool faceFinalDirectionDuringApproach,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            maxDistance,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.LaneApproachStraight;

        laneApproachUseDuration = false;
        laneApproachTimer = 0f;
        laneApproachDuration = 0f;
        laneApproachStart = start;

        laneApproachTarget = approachTarget;

        finalDirection.y = 0f;

        if (finalDirection.sqrMagnitude < 0.0001f)
            finalDirection = transform.forward;

        finalDirection.y = 0f;

        if (finalDirection.sqrMagnitude < 0.0001f)
            finalDirection = Vector3.forward;

        laneApproachFinalDirection = finalDirection.normalized;
        laneApproachSpeed = Mathf.Max(0.01f, approachSpeed);
        laneApproachStraightSpeed = Mathf.Max(0.01f, straightSpeed);
        laneApproachReachDistance = Mathf.Max(0.01f, reachDistance);
        laneApproachFaceFinalDirection = faceFinalDirectionDuringApproach;

        Vector3 firstDir = laneApproachTarget - start;

        if (firstDir.sqrMagnitude < 0.0001f)
            firstDir = laneApproachFinalDirection;

        velocity = firstDir.normalized * laneApproachSpeed;

        FaceVelocity();
    }


    public void LaunchLaneApproachStraightByDuration(
    ArcherBoss boss,
    Vector3 start,
    Vector3 approachTarget,
    Vector3 finalDirection,
    float approachDuration,
    float straightSpeed,
    float lifeTime,
    float maxDistance,
    bool faceFinalDirectionDuringApproach,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            maxDistance,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.LaneApproachStraight;

        laneApproachStart = start;
        laneApproachTarget = approachTarget;

        finalDirection.y = 0f;

        if (finalDirection.sqrMagnitude < 0.0001f)
            finalDirection = transform.forward;

        finalDirection.y = 0f;

        if (finalDirection.sqrMagnitude < 0.0001f)
            finalDirection = Vector3.forward;

        laneApproachFinalDirection = finalDirection.normalized;
        laneApproachStraightSpeed = Mathf.Max(0.01f, straightSpeed);

        laneApproachTimer = 0f;
        laneApproachDuration = Mathf.Max(0.01f, approachDuration);
        laneApproachUseDuration = true;

        laneApproachFaceFinalDirection = faceFinalDirectionDuringApproach;

        Vector3 firstDir = laneApproachTarget - start;

        if (firstDir.sqrMagnitude < 0.0001f)
            firstDir = laneApproachFinalDirection;

        velocity = firstDir.normalized * laneApproachStraightSpeed;

        FaceVelocity();
    }


    public void LaunchArcDrop(
    ArcherBoss boss,
    Vector3 start,
    Vector3 target,
    float flightTime,
    float arcHeight,
    float lifeTime,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            9999f,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.ArcDrop;

        arcStart = start;
        arcEnd = target;

        PlayWarningEffect(arcEnd);

        Vector3 mid = (arcStart + arcEnd) * 0.5f;
        mid.y = Mathf.Max(arcStart.y, arcEnd.y) + Mathf.Max(0.1f, arcHeight);

        arcControl = mid;
        arcTimer = 0f;
        arcDuration = Mathf.Max(0.1f, flightTime);

        velocity = EvaluateQuadraticBezierTangent(
            arcStart,
            arcControl,
            arcEnd,
            0f
        );

        FaceVelocity();
    }

    public void LaunchArcDropBySpeed(
    ArcherBoss boss,
    Vector3 start,
    Vector3 target,
    float horizontalSpeed,
    float arcHeight,
    float lifeTime,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            9999f,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.ArcDrop;

        arcStart = start;
        arcEnd = target;

        PlayWarningEffect(arcEnd);

        arcControl = (arcStart + arcEnd) * 0.5f;
        arcControl.y = Mathf.Max(arcStart.y, arcEnd.y) + Mathf.Max(0.1f, arcHeight);

        arcTimer = 0f;
        arcDuration = 9999f;

        arcUseHorizontalSpeed = true;
        arcHorizontalSpeed = Mathf.Max(0.01f, horizontalSpeed);
        arcTraveledXZ = 0f;
        arcHeightValue = Mathf.Max(0.1f, arcHeight);

        Vector2 startXZ = new Vector2(arcStart.x, arcStart.z);
        Vector2 endXZ = new Vector2(arcEnd.x, arcEnd.z);

        arcTotalXZDistance = Vector2.Distance(startXZ, endXZ);

        if (arcTotalXZDistance <= 0.001f)
            arcTotalXZDistance = 0.001f;

        Vector3 firstDir = GetArcPositionByHorizontalRate(0.02f) - arcStart;

        velocity = firstDir.sqrMagnitude > 0.0001f
            ? firstDir.normalized * arcHorizontalSpeed
            : Vector3.forward * arcHorizontalSpeed;

        FaceVelocity();
    }

    public void LaunchLockOnDrop(
   ArcherBoss boss,
   Vector3 start,
   Func<Vector3> retargetProvider,
   float riseDuration,
   float riseArcHeight,
   float dropHeight,
   float lockOnDelay,
   float fallOrderDelay,
   bool trackTargetDuringRise,
   float fallSpeed,
   float hitDistance,
   float lifeTime,
   Vector3 size,
   bool applyLaunchSize,
   int playerDamage,
   LayerMask playerLayer,
   TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            9999f,
            size,
            applyLaunchSize,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.LockOnDrop;
        lockOnDropPhase = LockOnDropPhase.Rise;

        lockOnRetargetProvider = retargetProvider;

        lockOnStart = start;
        lockOnRiseTimer = 0f;
        lockOnRiseDuration = Mathf.Max(0.05f, riseDuration);

        lockOnDropHeight = Mathf.Max(0.1f, dropHeight);
        lockOnRiseArcHeight = Mathf.Max(0.1f, riseArcHeight);

        lockOnWaitTimer = 0f;
        lockOnWaitDuration = Mathf.Max(0f, lockOnDelay);
        lockOnFallOrderDelay = Mathf.Max(0f, fallOrderDelay);

        lockOnTrackTargetDuringRise = trackTargetDuringRise;
        lockOnLockedTargetAfterRise = false;

        lockOnFallSpeed = Mathf.Max(0.01f, fallSpeed);
        lockOnHitDistance = Mathf.Max(0.01f, hitDistance);

        RefreshLockOnDropPoint();

        PlayWarningEffect(lockOnTarget);

        Vector3 tangent = EvaluateQuadraticBezierTangent(
            lockOnStart,
            lockOnControl,
            lockOnDropStart,
            0f
        );

        velocity = tangent.sqrMagnitude > 0.0001f
            ? tangent.normalized
            : Vector3.up;

        FaceVelocity();
    }

    private void InitializeLaunchCommon(
    ArcherBoss boss,
    Vector3 start,
    float lifeTime,
    float maxDistance,
    Vector3 size,
    bool applyLaunchSize,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        this.boss = boss;
        this.timeAgent = timeAgent;
        this.playerDamage = playerDamage;
        this.playerLayer = playerLayer;

        transform.position = start;

        launchPosition = start;
        traveledDistance = 0f;
        maxTravelDistance = Mathf.Max(0.1f, maxDistance);

        lifeTimer = 0f;
        lifeLimit = Mathf.Max(0.1f, lifeTime);
        finished = false;

        hitObjects.Clear();

        if (applyLaunchSize)
        {
            ApplyArrowSize(size);
        }
        else
        {
            // Prefabに設定されたScaleをそのまま使う。
            // Sweep判定用のarrowSizeだけは現在Scaleを参考にしておく。
            arrowSize = GetCurrentWorldSizeForSweep();
        }

        PlayTrajectoryEffect();
    }

    public void LaunchLockOnDrop(
    ArcherBoss boss,
    Vector3 start,
    Func<Vector3> retargetProvider,
    float riseDuration,
    float riseArcHeight,
    float dropHeight,
    float lockOnDelay,
    float fallOrderDelay,
    bool trackTargetDuringRise,
    float fallSpeed,
    float hitDistance,
    float lifeTime,
    Vector3 size,
    int playerDamage,
    LayerMask playerLayer,
    TimeAgent timeAgent)
    {
        InitializeLaunchCommon(
            boss,
            start,
            lifeTime,
            9999f,
            size,
            true,
            playerDamage,
            playerLayer,
            timeAgent
        );

        mode = ArrowMode.LockOnDrop;
        lockOnDropPhase = LockOnDropPhase.Rise;

        lockOnRetargetProvider = retargetProvider;

        lockOnStart = start;
        lockOnRiseTimer = 0f;
        lockOnRiseDuration = Mathf.Max(0.05f, riseDuration);

        lockOnDropHeight = Mathf.Max(0.1f, dropHeight);
        lockOnRiseArcHeight = Mathf.Max(0.1f, riseArcHeight);

        lockOnWaitTimer = 0f;
        lockOnWaitDuration = Mathf.Max(0f, lockOnDelay);
        lockOnFallOrderDelay = Mathf.Max(0f, fallOrderDelay);

        lockOnTrackTargetDuringRise = trackTargetDuringRise;
        lockOnLockedTargetAfterRise = false;

        lockOnFallSpeed = Mathf.Max(0.01f, fallSpeed);
        lockOnHitDistance = Mathf.Max(0.01f, hitDistance);

        RefreshLockOnDropPoint();

        Vector3 tangent = EvaluateQuadraticBezierTangent(
            lockOnStart,
            lockOnControl,
            lockOnDropStart,
            0f
        );

        velocity = tangent.sqrMagnitude > 0.0001f
            ? tangent.normalized
            : Vector3.up;

        FaceVelocity();
    }

    private Vector3 GetCurrentWorldSizeForSweep()
    {
        if (visualParam.visualRoot != null)
            return AbsVector3(visualParam.visualRoot.lossyScale);

        return AbsVector3(transform.lossyScale);
    }

    private Vector3 AbsVector3(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }

    #endregion

    #region Size

    private void ApplyArrowSize(Vector3 size)
    {
        arrowSize = new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z)
        );

        if (visualParam.visualRoot != null)
        {
            visualParam.visualRoot.localScale = arrowSize;
        }
        else
        {
            transform.localScale = arrowSize;
        }

        if (!colliderParam.resizeColliderOnLaunch)
            return;

        if (colliderParam.rootCollider is BoxCollider box)
        {
            box.size = Vector3.one;
            box.center = Vector3.zero;
        }
        else if (colliderParam.rootCollider is SphereCollider sphere)
        {
            sphere.radius = 0.5f;
            sphere.center = Vector3.zero;
        }
        else if (colliderParam.rootCollider is CapsuleCollider capsule)
        {
            capsule.radius = 0.5f;
            capsule.height = 1f;
            capsule.center = Vector3.zero;
        }
    }

    private float GetSweepRadius()
    {
        float max = Mathf.Max(arrowSize.x, arrowSize.y, arrowSize.z);
        return Mathf.Max(sweepParam.minRadius, max * sweepParam.radiusScale);
    }

    #endregion

    #region Update Move

    private void UpdateStraight(float dt)
    {
        Vector3 current = transform.position;
        Vector3 next = current + velocity * dt;

        traveledDistance += Vector3.Distance(current, next);

        if (traveledDistance >= maxTravelDistance)
        {
            Finish(ArrowEndReason.DistanceEnd);
            return;
        }

        if (MoveWithSweep(next))
            return;

        transform.position = next;
    }

    private void UpdateLaneApproachStraight(float dt)
    {
        if (laneApproachUseDuration)
        {
            UpdateLaneApproachStraightByDuration(dt);
            return;
        }

        Vector3 current = transform.position;

        Vector3 toTarget = laneApproachTarget - current;

        if (toTarget.sqrMagnitude <= laneApproachReachDistance * laneApproachReachDistance)
        {
            mode = ArrowMode.Straight;
            velocity = laneApproachFinalDirection * laneApproachStraightSpeed;
            FaceVelocity();
            return;
        }

        Vector3 next = Vector3.MoveTowards(
            current,
            laneApproachTarget,
            laneApproachSpeed * dt
        );

        traveledDistance += Vector3.Distance(current, next);

        if (traveledDistance >= maxTravelDistance)
        {
            Finish(ArrowEndReason.DistanceEnd);
            return;
        }

        Vector3 moveVelocity = dt > 0f
            ? (next - current) / dt
            : Vector3.zero;

        velocity = laneApproachFaceFinalDirection
            ? laneApproachFinalDirection * laneApproachStraightSpeed
            : moveVelocity;

        if (MoveWithSweep(next))
            return;

        transform.position = next;
    }

    private void UpdateLaneApproachStraightByDuration(float dt)
    {
        laneApproachTimer += dt;

        float t = Mathf.Clamp01(laneApproachTimer / laneApproachDuration);

        Vector3 current = transform.position;
        Vector3 next = Vector3.Lerp(
            laneApproachStart,
            laneApproachTarget,
            t
        );

        traveledDistance += Vector3.Distance(current, next);

        if (traveledDistance >= maxTravelDistance)
        {
            Finish(ArrowEndReason.DistanceEnd);
            return;
        }

        Vector3 moveVelocity = dt > 0f
            ? (next - current) / dt
            : Vector3.zero;

        velocity = laneApproachFaceFinalDirection
            ? laneApproachFinalDirection * laneApproachStraightSpeed
            : moveVelocity;

        if (MoveWithSweep(next))
            return;

        transform.position = next;

        if (t >= 1f)
        {
            mode = ArrowMode.Straight;
            velocity = laneApproachFinalDirection * laneApproachStraightSpeed;
            FaceVelocity();
        }
    }

    private void UpdateArcDrop(float dt)
    {
        if (arcUseHorizontalSpeed)
        {
            UpdateArcDropByHorizontalSpeed(dt);
            return;
        }

        arcTimer += dt;

        float t = Mathf.Clamp01(arcTimer / arcDuration);

        Vector3 current = transform.position;
        Vector3 next = EvaluateQuadraticBezier(
            arcStart,
            arcControl,
            arcEnd,
            t
        );

        velocity = dt > 0f
            ? (next - current) / dt
            : Vector3.zero;

        if (MoveWithSweep(next))
            return;

        transform.position = next;

        if (t >= 1f)
            Finish(ArrowEndReason.HitField);
    }

    private bool MoveWithSweep(Vector3 nextPosition)
    {
        if (!sweepParam.useSweep)
            return false;

        Vector3 start = transform.position;
        Vector3 end = nextPosition;
        Vector3 dir = end - start;
        float dist = dir.magnitude;

        if (dist <= 0.0001f)
            return false;

        RaycastHit[] hits = Physics.SphereCastAll(
            start,
            GetSweepRadius(),
            dir.normalized,
            dist,
            playerLayer,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider other = hits[i].collider;

            if (other == null)
                continue;

            if (sweepParam.ignoreSelf && other.transform.IsChildOf(transform))
                continue;

            if (HandleCollision(other, "Sweep"))
                return true;
        }

        return false;
    }

    private void UpdateLockOnDrop(float dt)
    {
        switch (lockOnDropPhase)
        {
            case LockOnDropPhase.Rise:
                UpdateLockOnRise(dt);
                break;

            case LockOnDropPhase.Wait:
                UpdateLockOnWait(dt);
                break;

            case LockOnDropPhase.Fall:
                UpdateLockOnFall(dt);
                break;
        }
    }

    private void UpdateLockOnRise(float dt)
    {
        lockOnRiseTimer += dt;

        if (lockOnTrackTargetDuringRise && !lockOnLockedTargetAfterRise)
        {
            RefreshLockOnDropPoint();

            if (arrowEffectParam.updateLockOnWarningWhileTracking)
                UpdateWarningEffectPosition(lockOnTarget);
        }

        float t = Mathf.Clamp01(lockOnRiseTimer / lockOnRiseDuration);

        Vector3 current = transform.position;
        Vector3 next = EvaluateQuadraticBezier(
            lockOnStart,
            lockOnControl,
            lockOnDropStart,
            t
        );

        velocity = dt > 0f
            ? (next - current) / dt
            : Vector3.zero;

        if (MoveWithSweep(next))
            return;

        transform.position = next;

        if (t >= 1f)
        {
            // ここが今回の重要ポイント。
            // 頭上到達時に、もう一度プレイヤー位置を取得して落下目標を確定する。
            RefreshLockOnDropPoint();
            UpdateWarningEffectPosition(lockOnTarget);

            transform.position = lockOnDropStart;

            lockOnLockedTargetAfterRise = true;
            lockOnDropPhase = LockOnDropPhase.Wait;

            velocity = Vector3.down;
            FaceVelocity();
        }
    }

    private void UpdateLockOnWait(float dt)
    {
        lockOnWaitTimer += dt;

        velocity = Vector3.down;
        FaceVelocity();

        float wait = lockOnWaitDuration + lockOnFallOrderDelay;

        if (lockOnWaitTimer >= wait)
            lockOnDropPhase = LockOnDropPhase.Fall;
    }

    private void UpdateLockOnFall(float dt)
    {
        Vector3 current = transform.position;
        Vector3 dir = lockOnTarget - current;

        if (dir.sqrMagnitude <= lockOnHitDistance * lockOnHitDistance)
        {
            Finish(ArrowEndReason.HitField);
            return;
        }

        Vector3 next = Vector3.MoveTowards(
            current,
            lockOnTarget,
            lockOnFallSpeed * dt
        );

        velocity = dt > 0f
            ? (next - current) / dt
            : Vector3.down;

        if (MoveWithSweep(next))
            return;

        transform.position = next;
    }

    private void UpdateArcDropByHorizontalSpeed(float dt)
    {
        arcTraveledXZ += arcHorizontalSpeed * dt;

        float t = Mathf.Clamp01(arcTraveledXZ / arcTotalXZDistance);

        Vector3 current = transform.position;
        Vector3 next = GetArcPositionByHorizontalRate(t);

        velocity = dt > 0f
            ? (next - current) / dt
            : Vector3.zero;

        if (MoveWithSweep(next))
            return;

        transform.position = next;

        if (t >= 1f)
            Finish(ArrowEndReason.HitField);
    }

    private Vector3 GetArcPositionByHorizontalRate(float t)
    {
        t = Mathf.Clamp01(t);

        Vector3 pos = Vector3.Lerp(arcStart, arcEnd, t);

        float baseY = Mathf.Lerp(arcStart.y, arcEnd.y, t);
        float arcY = Mathf.Sin(t * Mathf.PI) * arcHeightValue;

        pos.y = baseY + arcY;

        return pos;
    }

    #endregion

    #region Collision

    private bool HandleCollision(Collider other, string reason)
    {
        if (finished || other == null)
            return false;

        int bit = 1 << other.gameObject.layer;

        if ((bit & playerLayer.value) == 0)
            return false;

        IHitReceiver receiver = FindReceiver(other, out GameObject receiverObject);

        if (receiver == null || receiverObject == null)
            return false;

        if (hitObjects.Contains(receiverObject))
            return false;

        hitObjects.Add(receiverObject);

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = gameObject,
            targetObject = receiverObject,
            targetHitbox = targetHitbox != null ? targetHitbox.gameObject : other.gameObject,
            contactPoint = other.ClosestPoint(transform.position),
            payload = new EnemyAttackPayload
            {
                damage = playerDamage
            }
        };

        if (debugParam.logHit)
        {
            Debug.Log(
                $"{name} Arrow Hit Player:{receiverObject.name} " +
                $"Damage:{playerDamage} Reason:{reason}"
            );
        }

        receiver.OnHit(data);

        Finish(ArrowEndReason.HitPlayer);
        return true;
    }

    private IHitReceiver FindReceiver(Collider other, out GameObject receiverObject)
    {
        receiverObject = null;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox != null && targetHitbox.receiver != null)
        {
            receiverObject = targetHitbox.receiver is MonoBehaviour mono
                ? mono.gameObject
                : other.gameObject;

            return targetHitbox.receiver;
        }

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IHitReceiver receiver)
            {
                receiverObject = behaviours[i].gameObject;
                return receiver;
            }
        }

        return null;
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
    }

    #endregion

    #region Finish

    public void FinishManually()
    {
        Finish(ArrowEndReason.Manual);
    }

    private void Finish(ArrowEndReason reason)
    {
        if (finished)
            return;

        finished = true;

        EndArrowEffects();

        if (debugParam.logFinish)
            Debug.Log($"{name} Arrow Finish => {reason}");

        OnArrowFinished?.Invoke(this);

        Destroy(gameObject);
    }

    #endregion

    #region Utility

    private void FaceVelocity()
    {
        if (!visualParam.faceMoveDirection)
            return;

        if (velocity.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(
            velocity.normalized,
            Vector3.up
        );
    }

    private Vector3 EvaluateQuadraticBezier(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float t)
    {
        float u = 1f - t;

        return u * u * a +
               2f * u * t * b +
               t * t * c;
    }

    private Vector3 EvaluateQuadraticBezierTangent(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float t)
    {
        return 2f * (1f - t) * (b - a) +
               2f * t * (c - b);
    }

    private void RefreshLockOnDropPoint()
    {
        Vector3 target = lockOnRetargetProvider != null
            ? lockOnRetargetProvider()
            : lockOnTarget;

        lockOnTarget = target;
        lockOnDropStart = lockOnTarget + Vector3.up * lockOnDropHeight;

        Vector3 mid = (lockOnStart + lockOnDropStart) * 0.5f;
        mid.y = Mathf.Max(lockOnStart.y, lockOnDropStart.y) + lockOnRiseArcHeight;

        lockOnControl = mid;
    }

    private void UpdateModelSpin(float dt)
    {
        if (!modelSpinParam.spinEnable)
            return;

        Transform target = modelSpinParam.modelRoot;

        if (target == null && visualParam.visualRoot != null)
            target = visualParam.visualRoot;

        if (target == null)
            return;

        Vector3 spinAxis = GetSpinAxis();

        if (spinAxis.sqrMagnitude < 0.0001f)
            return;

        spinAxis.Normalize();

        Space space = modelSpinParam.useLocalSpace
            ? Space.Self
            : Space.World;

        target.Rotate(
            spinAxis,
            modelSpinParam.speed * dt,
            space
        );
    }

    private Vector3 GetSpinAxis()
    {
        switch (modelSpinParam.spinAxis)
        {
            case SpinAxis.X_Axis:
                return Vector3.right;

            case SpinAxis.Y_Axis:
                return Vector3.up;

            case SpinAxis.Z_Axis:
                return Vector3.forward;

            case SpinAxis.Custom:
                return modelSpinParam.customAxis;
        }

        return Vector3.up;
    }

    #endregion

    #region Visual And Effect

    private void PlayTrajectoryEffect()
    {
        if (!arrowEffectParam.useTrajectoryEffect)
            return;

        if (trajectoryEffect != null)
            return;

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        if (effectPlayer == null)
            return;

        if (!TryGetTrajectoryEffectPose(
                out Vector3 effectPos,
                out Quaternion effectRot))
        {
            effectPos = transform.position;
            effectRot = transform.rotation;
        }

        EffectPlayParam param = EffectPlayParam.Default;

        param.overrideFollowTarget = true;
        param.followTarget = false;

        param.overridePosition = true;
        param.positionOffset = Vector3.zero;

        param.overrideRotation = false;
        param.rotationOffset = Vector3.zero;

        param.overrideScale = false;

        trajectoryEffect = effectPlayer.PlayAt(
            arrowEffectParam.trajectoryEffectIndex,
            effectPos,
            effectRot,
            Vector3.one,
            param
        );
    }

    private void PlayWarningEffect(Vector3 position)
    {
        bool canUseWarning =
            mode == ArrowMode.ArcDrop && arrowEffectParam.useArcDropWarning ||
            mode == ArrowMode.LockOnDrop && arrowEffectParam.useLockOnDropWarning;

        if (!canUseWarning)
            return;

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        if (effectPlayer == null)
            return;

        EndWarningEffect();

        Vector3 warningPosition =
            position +
            arrowEffectParam.warningWorldOffset;

        warningEffect = effectPlayer.PlayAt(
            arrowEffectParam.warningEffectIndex,
            warningPosition
        );
    }

    private void UpdateWarningEffectPosition(Vector3 position)
    {
        if (warningEffect == null)
            return;

        Vector3 warningPosition =
            position +
            arrowEffectParam.warningWorldOffset;

        warningEffect.transform.position = warningPosition;
    }

    private void EndArrowEffects()
    {
        if (arrowEffectParam.stopTrajectoryOnFinish)
            EndTrajectoryEffect();

        if (arrowEffectParam.stopWarningOnFinish)
            EndWarningEffect();
    }

    private void EndTrajectoryEffect()
    {
        if (trajectoryEffect == null)
            return;

        trajectoryEffect.StopImmediate();
        trajectoryEffect = null;
    }

    private void EndWarningEffect()
    {
        if (warningEffect == null)
            return;

        warningEffect.StopImmediate();
        warningEffect = null;
    }

    private void CreateTrajectoryEffectPoint()
    {
        if (trajectoryEffectPoint != null)
            return;

        GameObject point = new GameObject($"{name}_TrajectoryEffectPoint");
        trajectoryEffectPoint = point.transform;
        trajectoryEffectPoint.SetParent(null);
        trajectoryEffectPoint.position = transform.position;
        trajectoryEffectPoint.rotation = transform.rotation;
    }

    private void DestroyTrajectoryEffectPoint()
    {
        if (trajectoryEffectPoint == null)
            return;

        Destroy(trajectoryEffectPoint.gameObject);
        trajectoryEffectPoint = null;
    }

    private void UpdateTrajectoryEffectPoint()
    {
        if (!TryGetTrajectoryEffectPose(
                out Vector3 effectPos,
                out Quaternion effectRot))
        {
            return;
        }

        if (trajectoryEffectPoint != null)
            trajectoryEffectPoint.SetPositionAndRotation(effectPos, effectRot);

        if (trajectoryEffect != null)
            trajectoryEffect.transform.SetPositionAndRotation(effectPos, effectRot);
    }

    private bool TryGetTrajectoryEffectPose(
        out Vector3 effectPos,
        out Quaternion effectRot)
    {
        effectPos = transform.position;
        effectRot = transform.rotation;

        Vector3 dir = GetCurrentEffectDirection();

        if (dir.sqrMagnitude < 0.0001f)
            return false;

        dir.Normalize();
        lastEffectDirection = dir;

        effectPos =
            transform.position +
            dir * arrowEffectParam.trajectoryForwardOffset +
            arrowEffectParam.trajectoryWorldOffset;

        if (!arrowEffectParam.faceMoveDirection)
        {
            effectRot = transform.rotation;
            return true;
        }

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion axisCorrection = GetPrefabForwardAxisCorrection();
        Quaternion extraOffset = Quaternion.Euler(
            arrowEffectParam.rotationOffsetEuler
        );

        effectRot = look * axisCorrection * extraOffset;

        return true;
    }

    private Vector3 GetCurrentEffectDirection()
    {
        Vector3 dir = velocity;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        if (mode == ArrowMode.ArcDrop)
        {
            if (arcUseHorizontalSpeed)
            {
                float rate = arcTotalXZDistance <= 0f
                    ? 1f
                    : Mathf.Clamp01(arcTraveledXZ / arcTotalXZDistance);

                Vector3 tangent =
                    GetArcPositionByHorizontalRate(Mathf.Clamp01(rate + 0.02f)) -
                    GetArcPositionByHorizontalRate(rate);

                if (tangent.sqrMagnitude > 0.0001f)
                    return tangent.normalized;
            }
            else
            {
                float currentT = arcDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(arcTimer / arcDuration);

                Vector3 tangent = EvaluateQuadraticBezierTangent(
                    arcStart,
                    arcControl,
                    arcEnd,
                    currentT
                );

                if (tangent.sqrMagnitude > 0.0001f)
                    return tangent.normalized;
            }
        }

        if (mode == ArrowMode.LockOnDrop)
        {
            switch (lockOnDropPhase)
            {
                case LockOnDropPhase.Rise:
                    {
                        float t = lockOnRiseDuration <= 0f
                            ? 1f
                            : Mathf.Clamp01(lockOnRiseTimer / lockOnRiseDuration);

                        Vector3 tangent = EvaluateQuadraticBezierTangent(
                            lockOnStart,
                            lockOnControl,
                            lockOnDropStart,
                            t
                        );

                        if (tangent.sqrMagnitude > 0.0001f)
                            return tangent.normalized;

                        break;
                    }

                case LockOnDropPhase.Wait:
                    return Vector3.down;

                case LockOnDropPhase.Fall:
                    {
                        Vector3 fallDir = lockOnTarget - transform.position;

                        if (fallDir.sqrMagnitude > 0.0001f)
                            return fallDir.normalized;

                        return Vector3.down;
                    }
            }
        }

        if (lastEffectDirection.sqrMagnitude > 0.0001f)
            return lastEffectDirection.normalized;

        return transform.forward;
    }

    private Quaternion GetPrefabForwardAxisCorrection()
    {
        switch (arrowEffectParam.prefabForwardAxis)
        {
            case EffectForwardAxis.ZPlus:
                return Quaternion.identity;

            case EffectForwardAxis.ZMinus:
                return Quaternion.Euler(0f, 180f, 0f);

            case EffectForwardAxis.XPlus:
                return Quaternion.Euler(0f, -90f, 0f);

            case EffectForwardAxis.XMinus:
                return Quaternion.Euler(0f, 90f, 0f);

            case EffectForwardAxis.YPlus:
                return Quaternion.Euler(90f, 0f, 0f);

            case EffectForwardAxis.YMinus:
                return Quaternion.Euler(-90f, 0f, 0f);
        }

        return Quaternion.identity;
    }

    #endregion

    #region Gizmos

    private void DrawGizmos()
    {
        if (!debugParam.drawGizmos)
            return;

        if (debugParam.drawVelocity && velocity.sqrMagnitude > 0.0001f)
        {
            Gizmos.DrawLine(
                transform.position,
                transform.position + velocity.normalized * 2f
            );
        }

        if (debugParam.drawArc && mode == ArrowMode.ArcDrop)
        {
            Vector3 prev = arcStart;

            int segments = Mathf.Max(4, debugParam.arcPreviewSegments);

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = EvaluateQuadraticBezier(
                    arcStart,
                    arcControl,
                    arcEnd,
                    t
                );

                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }

    #endregion
}