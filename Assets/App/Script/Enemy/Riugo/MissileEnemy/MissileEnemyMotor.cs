using System;
using UnityEngine;


[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(TimeAgent))]
public class MissileEnemyMotor : MonoBehaviour
{
    [System.Serializable]
    public class FlightParam
    {
        [Header("General")]
        public float turnSpeed = 18f;

        [Tooltip("Ray Hoverを使わない場合や、Chargeなどの通常上昇で使う上下移動速度")]
        public float verticalMoveSpeed = 6f;

        [Header("Ray Hover")]
        public bool useRayHover = true;

        [Tooltip("下方向Ray / SphereCastを飛ばす原点。未設定なら自身のTransformを使う")]
        public Transform hoverRayOrigin;

        [Tooltip("浮遊の基準として見る地面Layer")]
        public LayerMask hoverGroundMask;

        [Tooltip("地面から保ちたい距離")]
        public float hoverDistance = 4.0f;

        [Tooltip("Ray / SphereCastの最大距離。hoverDistanceより長めにする")]
        public float hoverRayLength = 8.0f;

        [Tooltip("地面を見失った時に使う保険高度")]
        public float fallbackHoverDistance = 4.0f;

        [Tooltip("上下補正の最大速度")]
        public float hoverAdjustSpeed = 8.0f;

        [Header("Hover Probe")]
        [Tooltip("RayではなくSphereCastで地面を探す。段差や坂に強くなる")]
        public bool useSphereCast = true;

        [Tooltip("SphereCastの半径")]
        public float hoverProbeRadius = 0.25f;

        [Header("Hover Bob")]
        [Tooltip("浮遊時の小さな上下揺れ")]
        public float bobAmplitude = 0.12f;

        [Tooltip("浮遊時の揺れの速さ")]
        public float bobFrequency = 1.6f;

        [Header("Debug")]
        public bool drawHoverRay = false;
    }

    [System.Serializable]
    public class AvoidanceParam
    {
        [Header("Probe")]
        public LayerMask obstacleMask;
        public float probeRadius = 0.45f;
        public float forwardProbeDistance = 2.2f;
        public float sideProbeDistance = 1.5f;
        public float sideProbeAngle = 35f;

        [Header("Steering")]
        public float avoidStrength = 1.6f;

        [Header("Debug")]
        public bool drawDebug = false;
    }

    [System.Serializable]
    public class DashParam
    {
        [Header("Dash")]
        public float dashSpeed = 18f;
        public float dashFailSafeDistanceMultiplier = 1.5f;
        public float minDashDistance = 4.0f;

        [Header("Impact")]
        public float impactPauseDuration = 0.22f;
    }

    [System.Serializable]
    public class BlowParam
    {
        [Header("Basic Physics")]
        public float gravity = 28f;
        public float mass = 1f;

        [Header("Horizontal Damping")]
        [Range(0f, 1f)] public float airHorizontalDampingPerFrame = 0.992f;
        [Range(0f, 1f)] public float groundedSlideDamping = 0.985f;

        [Header("Blow Common")]
        public float blowPowerMultiplier = 5f;
        [Range(0.1f, 1f)] public float massInfluence = 0.5f;

        [Header("By Tackle Type")]
        public TackleBlowSet tackleBlowSet = new TackleBlowSet();

        [Header("Bounce Common")]
        public bool enableBounce = true;
        public float minBounceSpeed = 1.0f;
        public int maxBounceCount = 6;
        public float bounceCooldown = 0.03f;

        [Header("Wall Bounce")]
        [Range(0f, 2f)] public float wallBouncePower = 0.9f;
        [Range(0f, 1f)] public float wallVerticalKeepRatio = 0.85f;
        public float minWallBounceSpeed = 0.5f;
        public float wallBouncePlanarBoost = 1.15f;
        public float postBounceMinPlanarSpeed = 2.0f;

        [Header("Floor Bounce")]
        [Range(0f, 2f)] public float floorBouncePower = 0.35f;
        public float minFloorUpwardSpeed = 2.5f;
        public float firstFloorBouncePowerMultiplier = 1.0f;

        [Header("Floor Bounce Stop")]
        [Tooltip("trueの場合、計算された床Bounce上昇速度がminFloorUpwardSpeed以下なら、これ以上床Bounceしない")]
        public bool stopFloorBounceWhenUnderMinUpward = true;

        [Header("Chain Upward Power")]
        [Tooltip("連鎖吹き飛び時に最低限与える上向き速度")]
        public float minUpwardPower = 4f;

        [Header("Reflection Self Damage")]
        public bool enableReflectionSelfDamage = true;
        public float wallReflectionDamage = 1f;
        public float ceilingReflectionDamage = 1f;

        [Header("Surface Detect")]
        [Range(0f, 1f)] public float floorNormalYThreshold = 0.55f;
        [Range(0f, 1f)] public float wallNormalYThreshold = 0.35f;
        [Range(0f, 1f)] public float ceilingNormalYThreshold = 0.55f;

        [Header("Slope Assist")]
        [Range(0f, 1f)] public float slopeFloorAssistYThreshold = 0.15f;

        [Header("Initial Floor Ignore")]
        public float initialFloorIgnoreTime = 0.06f;
        public int initialFloorIgnoreFrames = 2;

        [Header("Knockdown / End")]
        public float layDownReadySpeedThreshold = 0.22f;
        public float landingSpeedThreshold = 0.12f;

        [Header("Controller Ground Fallback")]
        [Tooltip("BounceSensorが床を拾えなかった場合、CharacterControllerのBelow接触を床接触として扱う")]
        public bool useControllerGroundFallback = true;

        [Tooltip("CharacterControllerの床接触保険で使う床法線")]
        public Vector3 controllerGroundFallbackNormal = Vector3.up;

        [Header("Debug")]
        public bool logBounce = false;
        public bool logControllerGroundFallback = false;
    }

    [System.Serializable]
    public class SurfaceFeedbackParam
    {
        [Header("Wall / Ceiling Bounce Feedback")]
        public bool enable = true;

        [Tooltip("壁反射時に再生するEffectPlayerのID")]
        public int wallEffectIndex = 0;

        [Tooltip("天井反射時に再生するEffectPlayerのID")]
        public int ceilingEffectIndex = 0;

        [Tooltip("壁反射時のSE名")]
        public string wallSeName = "E_WallHit";

        [Tooltip("天井反射時のSE名")]
        public string ceilingSeName = "E_WallHit";

        [Tooltip("反射EffectのScale")]
        public Vector3 effectScale = new Vector3(2f, 2f, 2f);

        [Header("Debug")]
        public bool warnIfMissingReference = true;
    }

    [Header("Settings")]
    [SerializeField] private FlightParam flightParam = new FlightParam();
    [SerializeField] private AvoidanceParam avoidanceParam = new AvoidanceParam();
    [SerializeField] private DashParam dashParam = new DashParam();
    [SerializeField] private BlowParam blowParam = new BlowParam();
    [SerializeField] private SurfaceFeedbackParam surfaceFeedbackParam = new SurfaceFeedbackParam();

    private CharacterController controller;
    private EffectPlayer effectPlayer;
    private TimeAgent timeAgent;
    private AudioManager audioManager;

    private Vector3 homePosition;

    private bool dashHitEnvironmentThisFrame;
    private Vector3 lastDashImpactNormal = Vector3.forward;
    private Vector3 lastDashDirection = Vector3.forward;

    private bool isDashing;
    private Vector3 lockedDashDirection;
    private float dashTravelledDistance;
    private float dashMaxDistance;

    private bool isImpactPause;
    private float impactPauseTimer;

    private Vector3 velocity;
    private float verticalVelocity;

    private Vector3 preMovePlanarVelocity;
    private float preMoveVerticalVelocity;

    private bool isFlying;
    private bool hasLandedOnce;
    private bool justBlownFrame;
    private bool hasTouchedFloorDuringBlow;
    private bool knockdownGroundNotified;
    private bool floorBounceAllowed;

    private float flyingElapsedTime;
    private int flyingFrameCount;

    private int bounceCount;
    private float lastBounceTime = -999f;

    private CollisionFlags lastMoveFlags;

    public float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;
    public Vector3 HomePosition => homePosition;

    public bool DashHitEnvironmentThisFrame => dashHitEnvironmentThisFrame;
    public Vector3 LastDashImpactNormal => lastDashImpactNormal;
    public bool IsDashing => isDashing;
    public bool IsImpactPause => isImpactPause;

    public bool IsBlown => isFlying;
    public bool HasTouchedGroundOnce => hasLandedOnce;
    public bool IsGrounded => controller != null && controller.isGrounded;

    public float CurrentSpeed { get; private set; }
    public float CurrentPlanarSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;

    public Vector3 CurrentTotalVelocity
    {
        get
        {
            Vector3 v = velocity;
            v.y = verticalVelocity;
            return v;
        }
    }

    public bool IsKnockdownGroundNotified => knockdownGroundNotified;

    public bool CanEnterKnockdownRecovery
    {
        get
        {
            if (!knockdownGroundNotified)
                return false;

            if (!controller.isGrounded)
                return false;

            return CurrentPlanarSpeed <= blowParam.layDownReadySpeedThreshold &&
                   Mathf.Abs(verticalVelocity) <= blowParam.layDownReadySpeedThreshold;
        }
    }

    public event Action OnDashImpactEnvironment;
    public event Action OnDashFinishedByDistance;
    public event Action OnFirstGroundContactDuringBlow;
    public event Action<float, EnemySurfaceKind> OnReflectionSelfDamage;

    private bool warnedMissingEffectPlayer;
    private bool warnedMissingAudioManager;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        timeAgent = GetComponent<TimeAgent>();

        // Rootに無ければ子も探す
        effectPlayer = GetComponent<EffectPlayer>();
        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        homePosition = transform.position;

        // Awake時点で取れない可能性があるので、ここでは一応取得するだけ
        audioManager = AudioManager.Instance;
    }

    public void SetCharacterControllerExcludeLayers(LayerMask mask)
    {
        if (controller == null)
            return;

        controller.excludeLayers = mask;
    }

    public void TickBegin(float dt)
    {
        dashHitEnvironmentThisFrame = false;
        CurrentSpeed = 0f;
        lastMoveFlags = CollisionFlags.None;
    }

    public void HoverAtHome(float dt)
    {
        if (IsSpecialBusy())
            return;

        float targetY = GetHoverTargetY(homePosition.y);

        Vector3 target = new Vector3(
            homePosition.x,
            targetY,
            homePosition.z
        );

        MoveTowardWorldPointInternal(target, 0f, false, dt, false, true);
    }

    public void MoveTowardPoint(
        Vector3 worldPoint,
        float moveSpeed,
        float heightOffset,
        bool useAvoidance,
        float dt)
    {
        if (IsSpecialBusy())
            return;

        float targetY;

        if (flightParam.useRayHover)
        {
            // Ray Hover使用時は、地面からの距離だけで高さを決める。
            // heightOffsetを足すと「Ray浮遊距離 + chaseHeightOffset」で二重に浮くため足さない。
            targetY = GetHoverTargetY(worldPoint.y);
        }
        else
        {
            // Ray Hoverを使わない場合だけ、従来のheightOffsetを使う。
            targetY = worldPoint.y + heightOffset + GetHoverBob();
        }

        Vector3 target = new Vector3(
            worldPoint.x,
            targetY,
            worldPoint.z
        );

        MoveTowardWorldPointInternal(target, moveSpeed, useAvoidance, dt, true, flightParam.useRayHover);
    }

    public void HoldAndRise(
        Vector3 anchorWorldPoint,
        float riseHeight,
        float riseSpeed,
        float dt)
    {
        if (IsSpecialBusy())
            return;

        Vector3 target = new Vector3(
            anchorWorldPoint.x,
            anchorWorldPoint.y + riseHeight,
            anchorWorldPoint.z
        );

        MoveTowardWorldPointInternal(target, riseSpeed, false, dt, false, false);
    }

    public void FaceToPointSmooth(Vector3 worldPoint, float dt, float speedMultiplier = 1f)
    {
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        FaceToDirectionSmooth(dir, dt, speedMultiplier);
    }

    public void FaceToDirectionSmooth(Vector3 dir, float dt, float speedMultiplier = 1f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            flightParam.turnSpeed * speedMultiplier * dt
        );
    }

    public void FaceToDirectionInstant(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public void BeginDash(Vector3 lockedTargetPosition)
    {
        isDashing = true;
        dashTravelledDistance = 0f;

        Vector3 dir = lockedTargetPosition - transform.position;
        float startDistance = dir.magnitude;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        lockedDashDirection = dir.normalized;
        lastDashDirection = lockedDashDirection;

        dashMaxDistance = Mathf.Max(
            startDistance * dashParam.dashFailSafeDistanceMultiplier,
            dashParam.minDashDistance
        );

        FaceToDirectionInstant(lockedDashDirection);
    }

    public void UpdateDash(float dt)
    {
        if (!isDashing)
            return;

        Vector3 delta = lockedDashDirection * dashParam.dashSpeed * dt;
        float moveDist = delta.magnitude;

        dashTravelledDistance += moveDist;
        CurrentSpeed = dashParam.dashSpeed;

        lastMoveFlags = controller.Move(delta);

        if (dashTravelledDistance >= dashMaxDistance)
        {
            isDashing = false;
            OnDashFinishedByDistance?.Invoke();
        }
    }

    public void BeginImpactPause()
    {
        isDashing = false;
        isImpactPause = true;
        impactPauseTimer = dashParam.impactPauseDuration;
        CurrentSpeed = 0f;
    }

    public bool UpdateImpactPause(float dt)
    {
        if (!isImpactPause)
            return true;

        impactPauseTimer -= dt;
        if (impactPauseTimer <= 0f)
        {
            isImpactPause = false;
            return true;
        }

        return false;
    }

    public void StopSpecialMotion()
    {
        isDashing = false;
        isImpactPause = false;
    }

    public void ForceEndBlow()
    {
        isFlying = false;
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        preMovePlanarVelocity = Vector3.zero;
        preMoveVerticalVelocity = 0f;
        CurrentSpeed = 0f;
    }

    private TackleBlowProfile GetTackleBlowProfile(TackleType tackleType)
    {
        return blowParam.tackleBlowSet.GetProfile(tackleType);
    }

    public void ApplyBlow(Vector3 dir, float powerConst, float powerRate, TackleType tackleType)
    {
        StopSpecialMotion();

        TackleBlowProfile tackleProfile = GetTackleBlowProfile(tackleType);

        Vector3 horizontalDir = new Vector3(dir.x, 0f, dir.z);
        if (horizontalDir.sqrMagnitude < 0.0001f)
            horizontalDir = transform.forward;

        horizontalDir.Normalize();

        powerRate = Mathf.Clamp01(powerRate);

        float effectivePowerRate = Mathf.Lerp(
            tackleProfile.minPowerRatio,
            1f,
            powerRate
        );

        float effectivePower = powerConst * effectivePowerRate;
        float rawLaunchPower = effectivePower * blowParam.blowPowerMultiplier;

        float safeMass = Mathf.Max(blowParam.mass, 1f);
        float adjustedMass = Mathf.Pow(safeMass, blowParam.massInfluence);

        float launchSpeed = rawLaunchPower / adjustedMass;
        launchSpeed = Mathf.Max(launchSpeed, tackleProfile.minLaunchSpeed);

        float launchAngle = Mathf.Lerp(
            tackleProfile.minLaunchAngle,
            tackleProfile.maxLaunchAngle,
            powerRate
        );

        float rad = launchAngle * Mathf.Deg2Rad;

        float horizontalSpeed =
            Mathf.Cos(rad) *
            launchSpeed *
            tackleProfile.horizontalLaunchBoost;

        float upwardSpeed = Mathf.Sin(rad) * launchSpeed;
        upwardSpeed = Mathf.Max(upwardSpeed, tackleProfile.minUpwardPower);

        velocity = horizontalDir * horizontalSpeed;
        verticalVelocity = upwardSpeed;

        justBlownFrame = true;
        BeginFlying();

        if (blowParam.logBounce)
        {
            Debug.Log(
                $"{name} ApplyBlow " +
                $"type:{tackleType} " +
                $"horizontal:{horizontalSpeed:F2} upward:{upwardSpeed:F2} angle:{launchAngle:F2}"
            );
        }
    }

    public void ApplyChainReaction(Vector3 horizontalDir, float horizontalPower, float verticalPower)
    {
        Vector3 horizontal = new Vector3(horizontalDir.x, 0f, horizontalDir.z);

        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = transform.forward;

        horizontal.Normalize();

        velocity = horizontal * horizontalPower;
        verticalVelocity = Mathf.Max(verticalPower, blowParam.minUpwardPower);

        if (effectPlayer != null)
            effectPlayer.Play(1);

        BeginFlying();
    }

    private void BeginFlying()
    {
        isFlying = true;

        hasLandedOnce = false;
        hasTouchedFloorDuringBlow = false;
        knockdownGroundNotified = false;

        // 最初は床Bounce可能
        floorBounceAllowed = true;

        flyingElapsedTime = 0f;
        flyingFrameCount = 0;

        bounceCount = 0;
        lastBounceTime = -999f;
        preMovePlanarVelocity = Vector3.zero;
        preMoveVerticalVelocity = 0f;
    }

    public void UpdateBlow(float dt)
    {
        if (!isFlying)
            return;

        preMovePlanarVelocity = velocity;
        preMoveVerticalVelocity = verticalVelocity;

        ApplyGravity(dt);
        MoveFlying(dt);

        flyingElapsedTime += dt;
        flyingFrameCount++;

        UpdateFlying(dt);

        justBlownFrame = false;
    }

    private void ApplyGravity(float dt)
    {
        verticalVelocity -= blowParam.gravity * dt;
    }

    private void MoveFlying(float dt)
    {
        Vector3 total = velocity;
        total.y = verticalVelocity;

        CurrentSpeed = total.magnitude;

        lastMoveFlags = controller.Move(total * dt);

        TryNotifyControllerGroundFallback();
    }

    private void TryNotifyControllerGroundFallback()
    {
        if (!isFlying)
            return;

        if (!blowParam.useControllerGroundFallback)
            return;

        if ((lastMoveFlags & CollisionFlags.Below) == 0)
            return;

        if (IsInInitialFloorIgnoreWindow())
            return;

        // すでにKnockdown移行用の接地通知を送っているなら不要
        if (knockdownGroundNotified)
            return;

        // 上向きに跳ねている最中のBelow判定は無視する
        // CharacterControllerは接地が少し残ることがあるため
        if (preMoveVerticalVelocity > 0.05f || verticalVelocity > 0.05f)
            return;

        Vector3 fallbackNormal = blowParam.controllerGroundFallbackNormal;
        if (fallbackNormal.sqrMagnitude < 0.0001f)
            fallbackNormal = Vector3.up;

        if (blowParam.logControllerGroundFallback)
        {
            Debug.Log(
                $"{name} ControllerGroundFallback " +
                $"flags:{lastMoveFlags} normal:{fallbackNormal}"
            );
        }

        NotifyBlowSurfaceHit(fallbackNormal.normalized, EnemySurfaceKind.Floor);
    }

    private void UpdateFlying(float dt)
    {
        if (!isFlying)
            return;

        if (controller.isGrounded)
            velocity *= blowParam.groundedSlideDamping;
        else
            velocity *= blowParam.airHorizontalDampingPerFrame;

        if (hasLandedOnce && CurrentPlanarSpeed < blowParam.landingSpeedThreshold)
            velocity = Vector3.zero;

        // Knockdown移行用の最終接地通知が出た後だけ終了可能にする
        if (knockdownGroundNotified &&
            controller.isGrounded &&
            CurrentPlanarSpeed <= blowParam.landingSpeedThreshold &&
            Mathf.Abs(verticalVelocity) <= blowParam.landingSpeedThreshold)
        {
            EndFlying();
        }
    }

    private void EndFlying()
    {
        isFlying = false;
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        preMovePlanarVelocity = Vector3.zero;
        preMoveVerticalVelocity = 0f;
        CurrentSpeed = 0f;
    }

    private bool IsInInitialFloorIgnoreWindow()
    {
        if (!isFlying)
            return false;

        if (flyingElapsedTime < blowParam.initialFloorIgnoreTime)
            return true;

        if (flyingFrameCount < blowParam.initialFloorIgnoreFrames)
            return true;

        return false;
    }

    public void NotifyBlowSurfaceHit(Vector3 normal, EnemySurfaceKind kind)
    {
        if (!isFlying)
            return;

        if (!blowParam.enableBounce)
            return;

        if (Time.time < lastBounceTime + blowParam.bounceCooldown)
            return;

        if (normal.sqrMagnitude < 0.0001f)
            return;

        normal.Normalize();

        switch (kind)
        {
            case EnemySurfaceKind.Floor:
                {
                    if (IsInInitialFloorIgnoreWindow())
                    {
                        if (blowParam.logBounce)
                            Debug.Log($"{name} Floor ignored at early flight window");
                        return;
                    }

                    // 上向きに跳ねている最中の床Stay通知は無視する
                    // TriggerStayやCharacterControllerの接地残りによる二重床判定対策
                    if (preMoveVerticalVelocity > 0.05f || verticalVelocity > 0.05f)
                        return;

                    hasTouchedFloorDuringBlow = true;
                    hasLandedOnce = true;

                    float downSpeed = Mathf.Max(0f, -preMoveVerticalVelocity);

                    float bouncePower = blowParam.floorBouncePower;
                    if (bounceCount == 0)
                        bouncePower *= blowParam.firstFloorBouncePowerMultiplier;

                    float calculatedUpSpeed = downSpeed * bouncePower;

                    bool canBounce =
                        floorBounceAllowed &&
                        bounceCount < blowParam.maxBounceCount &&
                        downSpeed >= blowParam.minBounceSpeed;

                    if (canBounce)
                    {
                        // 計算値がMin以下でも、今回だけはMinで跳ねる
                        verticalVelocity = Mathf.Max(
                            calculatedUpSpeed,
                            blowParam.minFloorUpwardSpeed
                        );

                        bounceCount++;
                        lastBounceTime = Time.time;

                        if (effectPlayer != null)
                            effectPlayer.Play(2);

                        // ただし、計算値がMin以下だった場合は、
                        // このBounce以降の床Bounceを禁止する
                        if (blowParam.stopFloorBounceWhenUnderMinUpward &&
                            calculatedUpSpeed <= blowParam.minFloorUpwardSpeed)
                        {
                            floorBounceAllowed = false;
                        }

                        if (blowParam.logBounce)
                        {
                            Debug.Log(
                                $"{name} FloorBounce " +
                                $"downSpeed:{downSpeed:F2} " +
                                $"calculatedUp:{calculatedUpSpeed:F2} " +
                                $"vertical:{verticalVelocity:F2} " +
                                $"allowNext:{floorBounceAllowed}"
                            );
                        }

                        // 重要：
                        // Bounceした接触ではKnockdown移行通知を出さない。
                        // 次に地面へ落ちてきた接触で通知する。
                        break;
                    }

                    // ここに来たら「もうBounceしない床接触」
                    // つまりKnockdownへ移行してよい接地
                    floorBounceAllowed = false;

                    if (verticalVelocity < 0f)
                        verticalVelocity = 0f;

                    lastBounceTime = Time.time;

                    if (!knockdownGroundNotified)
                    {
                        knockdownGroundNotified = true;
                        OnFirstGroundContactDuringBlow?.Invoke();
                    }

                    if (blowParam.logBounce)
                    {
                        Debug.Log(
                            $"{name} FloorTouch FinalGround " +
                            $"downSpeed:{downSpeed:F2} " +
                            $"calculatedUp:{calculatedUpSpeed:F2}"
                        );
                    }

                    break;
                }

            case EnemySurfaceKind.Wall:
                {
                    Vector3 currentVelocity = new Vector3(
                        velocity.x,
                        verticalVelocity,
                        velocity.z
                    );

                    float speed = currentVelocity.magnitude;
                    if (speed < blowParam.minBounceSpeed)
                        return;

                    Vector3 reflected = Vector3.Reflect(currentVelocity, normal);

                    velocity.x = reflected.x * blowParam.wallBouncePower;
                    velocity.z = reflected.z * blowParam.wallBouncePower;
                    verticalVelocity = reflected.y * blowParam.wallVerticalKeepRatio;

                    PlaySurfaceBounceFeedback(
                        EnemySurfaceKind.Wall,
                        normal,
                        reflected
                    );

                    bounceCount++;
                    lastBounceTime = Time.time;

                    RaiseReflectionSelfDamage(EnemySurfaceKind.Wall);
                    break;
                }

            case EnemySurfaceKind.Ceiling:
                {
                    Vector3 currentVelocity = new Vector3(
                        velocity.x,
                        verticalVelocity,
                        velocity.z
                    );

                    float speed = currentVelocity.magnitude;
                    if (speed < blowParam.minWallBounceSpeed)
                        return;

                    Vector3 reflected = Vector3.Reflect(currentVelocity, normal);

                    velocity.x = reflected.x * blowParam.wallBouncePower;
                    velocity.z = reflected.z * blowParam.wallBouncePower;
                    verticalVelocity = Mathf.Min(
                        reflected.y,
                        -blowParam.minFloorUpwardSpeed
                    );

                    PlaySurfaceBounceFeedback(
                        EnemySurfaceKind.Ceiling,
                        normal,
                        reflected
                    );

                    bounceCount++;
                    lastBounceTime = Time.time;

                    RaiseReflectionSelfDamage(EnemySurfaceKind.Ceiling);
                    break;
                }
        }

        if (blowParam.logBounce)
        {
            Debug.Log(
                $"{name} SurfaceBounce " +
                $"kind:{kind} normal:{normal} " +
                $"vertical:{verticalVelocity:F2} planar:{CurrentPlanarSpeed:F2}"
            );
        }
    }

    public Vector3 GetChainDirectionTo(GameObject targetObject)
    {
        Vector3 dir = targetObject.transform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
        {
            Vector3 totalVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);
            dir = new Vector3(totalVelocity.x, 0f, totalVelocity.z);

            if (dir.sqrMagnitude < 0.001f)
                dir = transform.forward;
        }

        return dir.normalized;
    }

    private void RaiseReflectionSelfDamage(EnemySurfaceKind kind)
    {
        if (!blowParam.enableReflectionSelfDamage)
            return;

        float damage = 0f;

        switch (kind)
        {
            case EnemySurfaceKind.Wall:
                damage = blowParam.wallReflectionDamage;
                break;

            case EnemySurfaceKind.Ceiling:
                damage = blowParam.ceilingReflectionDamage;
                break;

            case EnemySurfaceKind.Floor:
            default:
                return;
        }

        if (damage <= 0f)
            return;

        OnReflectionSelfDamage?.Invoke(damage, kind);

        if (blowParam.logBounce)
        {
            Debug.Log(
                $"{name} ReflectionSelfDamage " +
                $"kind:{kind} damage:{damage:F2}"
            );
        }
    }

    public bool IsLayDownReady()
    {
        if (!knockdownGroundNotified)
            return false;

        return CurrentPlanarSpeed <= blowParam.layDownReadySpeedThreshold &&
               Mathf.Abs(verticalVelocity) <= blowParam.layDownReadySpeedThreshold;
    }

    private bool IsSpecialBusy()
    {
        return isDashing || isImpactPause || isFlying;
    }

    private void MoveTowardWorldPointInternal(
        Vector3 target,
        float moveSpeed,
        bool useAvoidance,
        float dt,
        bool faceMoveDirection,
        bool useHoverAdjustSpeed)
    {
        Vector3 current = transform.position;

        Vector3 horizontalToTarget = new Vector3(
            target.x - current.x,
            0f,
            target.z - current.z
        );

        Vector3 horizontalDir = Vector3.zero;
        float horizontalDist = horizontalToTarget.magnitude;

        if (horizontalDist > 0.0001f)
        {
            horizontalDir = horizontalToTarget / horizontalDist;

            if (useAvoidance)
                horizontalDir = GetAvoidedDirection(horizontalDir);

            if (faceMoveDirection)
                FaceToDirectionSmooth(horizontalDir, dt, 1f);
        }

        float moveDist = moveSpeed * dt;
        if (moveDist > horizontalDist)
            moveDist = horizontalDist;

        float yMoveSpeed = useHoverAdjustSpeed
            ? flightParam.hoverAdjustSpeed
            : flightParam.verticalMoveSpeed;

        float nextY = Mathf.MoveTowards(
            current.y,
            target.y,
            yMoveSpeed * dt
        );

        Vector3 delta =
            horizontalDir * moveDist +
            Vector3.up * (nextY - current.y);

        CurrentSpeed = horizontalDist > 0.0001f ? moveSpeed : 0f;
        lastMoveFlags = controller.Move(delta);
    }

    private Vector3 GetAvoidedDirection(Vector3 desiredDir)
    {
        desiredDir.y = 0f;
        if (desiredDir.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        desiredDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 0.15f;
        Vector3 steer = desiredDir;

        CheckProbe(origin, desiredDir, avoidanceParam.forwardProbeDistance, ref steer);
        CheckProbe(
            origin,
            Quaternion.Euler(0f, avoidanceParam.sideProbeAngle, 0f) * desiredDir,
            avoidanceParam.sideProbeDistance,
            ref steer
        );
        CheckProbe(
            origin,
            Quaternion.Euler(0f, -avoidanceParam.sideProbeAngle, 0f) * desiredDir,
            avoidanceParam.sideProbeDistance,
            ref steer
        );

        steer.y = 0f;
        if (steer.sqrMagnitude < 0.0001f)
            return desiredDir;

        return steer.normalized;
    }

    private void CheckProbe(
        Vector3 origin,
        Vector3 dir,
        float distance,
        ref Vector3 steer)
    {
        if (!Physics.SphereCast(
            origin,
            avoidanceParam.probeRadius,
            dir,
            out RaycastHit hit,
            distance,
            avoidanceParam.obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            if (avoidanceParam.drawDebug)
                Debug.DrawRay(origin, dir.normalized * distance, Color.green);

            return;
        }

        Vector3 avoid = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
        steer += avoid * avoidanceParam.avoidStrength;

        if (avoidanceParam.drawDebug)
        {
            Debug.DrawRay(origin, dir.normalized * distance, Color.red);
            Debug.DrawRay(hit.point, hit.normal, Color.yellow);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isDashing)
            return;

        if (((1 << hit.gameObject.layer) & avoidanceParam.obstacleMask.value) == 0)
            return;

        dashHitEnvironmentThisFrame = true;
        lastDashImpactNormal = hit.normal;
        isDashing = false;

        OnDashImpactEnvironment?.Invoke();
    }

    public bool IsEnemyFlying()
    {
        return isFlying;
    }

    public float GetCurrentHoverTargetY()
    {
        return GetHoverTargetY(transform.position.y);
    }

    public bool IsNearHoverHeight(float tolerance)
    {
        float targetY = GetCurrentHoverTargetY();
        return Mathf.Abs(transform.position.y - targetY) <= tolerance;
    }

    public bool IsNearHoverHeightPlus(float extraHeight, float tolerance)
    {
        float targetY = GetCurrentHoverTargetY() + extraHeight;
        return Mathf.Abs(transform.position.y - targetY) <= tolerance;
    }

    public void RecoverHoverHeight(float dt)
    {
        if (IsSpecialBusy())
            return;

        float targetY = GetCurrentHoverTargetY();
        MoveVerticalOnly(targetY, flightParam.hoverAdjustSpeed, dt);
    }

    public void HoldHoverHeightPlus(float extraHeight, float moveSpeed, float dt)
    {
        if (IsSpecialBusy())
            return;

        float targetY = GetCurrentHoverTargetY() + extraHeight;
        MoveVerticalOnly(targetY, moveSpeed, dt);
    }

    private void MoveVerticalOnly(float targetY, float moveSpeed, float dt)
    {
        if (controller == null)
            return;

        Vector3 current = transform.position;

        float nextY = Mathf.MoveTowards(
            current.y,
            targetY,
            moveSpeed * dt
        );

        Vector3 delta = Vector3.up * (nextY - current.y);
        controller.Move(delta);

        CurrentSpeed = Mathf.Abs(nextY - current.y) / Mathf.Max(dt, 0.0001f);
    }
    private float GetHoverTargetY(float baseFallbackY)
    {
        float bob = GetHoverBob();

        if (!flightParam.useRayHover)
            return baseFallbackY + flightParam.hoverDistance + bob;

        Transform originTransform = flightParam.hoverRayOrigin != null
            ? flightParam.hoverRayOrigin
            : transform;

        Vector3 origin = originTransform.position;

        bool hitGround;

        if (flightParam.useSphereCast)
        {
            hitGround = Physics.SphereCast(
                origin,
                flightParam.hoverProbeRadius,
                Vector3.down,
                out RaycastHit hit,
                flightParam.hoverRayLength,
                flightParam.hoverGroundMask,
                QueryTriggerInteraction.Ignore
            );

            if (flightParam.drawHoverRay)
            {
                Color rayColor = hitGround ? Color.green : Color.red;
                Debug.DrawRay(origin, Vector3.down * flightParam.hoverRayLength, rayColor);
            }

            if (hitGround)
                return hit.point.y + flightParam.hoverDistance + bob;
        }
        else
        {
            hitGround = Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                flightParam.hoverRayLength,
                flightParam.hoverGroundMask,
                QueryTriggerInteraction.Ignore
            );

            if (flightParam.drawHoverRay)
            {
                Color rayColor = hitGround ? Color.green : Color.red;
                Debug.DrawRay(origin, Vector3.down * flightParam.hoverRayLength, rayColor);
            }

            if (hitGround)
                return hit.point.y + flightParam.hoverDistance + bob;
        }

        return baseFallbackY + flightParam.fallbackHoverDistance + bob;
    }

    private float GetHoverBob()
    {
        return Mathf.Sin(Time.time * flightParam.bobFrequency) *
               flightParam.bobAmplitude;
    }

    private EffectPlayer GetEffectPlayerSafe()
    {
        if (effectPlayer != null)
            return effectPlayer;

        effectPlayer = GetComponent<EffectPlayer>();

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        return effectPlayer;
    }

    private AudioManager GetAudioManagerSafe()
    {
        if (audioManager != null)
            return audioManager;

        audioManager = AudioManager.Instance;
        return audioManager;
    }

    private void PlaySurfaceBounceFeedback(
        EnemySurfaceKind kind,
        Vector3 normal,
        Vector3 reflectedVelocity)
    {
        if (!surfaceFeedbackParam.enable)
            return;

        if (normal.sqrMagnitude < 0.0001f)
            return;

        Vector3 hitNormal = normal.normalized;

        Vector3 position = GetSurfaceEffectPosition(hitNormal);
        Quaternion rotation = CreateSurfaceEffectRotation(hitNormal, reflectedVelocity);

        string seName = kind == EnemySurfaceKind.Ceiling
            ? surfaceFeedbackParam.ceilingSeName
            : surfaceFeedbackParam.wallSeName;

        int effectIndex = kind == EnemySurfaceKind.Ceiling
            ? surfaceFeedbackParam.ceilingEffectIndex
            : surfaceFeedbackParam.wallEffectIndex;

        // 音はEffectPlayerの有無に関係なく再生する
        AudioManager audio = GetAudioManagerSafe();
        if (audio != null && !string.IsNullOrEmpty(seName))
        {
            audio.PlaySe(seName, position);
        }
        else if (surfaceFeedbackParam.warnIfMissingReference && !warnedMissingAudioManager)
        {
            warnedMissingAudioManager = true;
            Debug.LogWarning($"{name} : AudioManagerが取得できないため、反射SEを再生できませんでした。");
        }

        // EffectもAudioManagerの有無に関係なく再生する
        EffectPlayer player = GetEffectPlayerSafe();
        if (player != null)
        {
            EffectPlayParam param = EffectPlayParam.Default;
            param.rotationOffset = Vector3.zero;
            param.scale = surfaceFeedbackParam.effectScale;

            player.PlayAt(
                effectIndex,
                position,
                rotation,
                Vector3.one,
                param
            );
        }
        else if (surfaceFeedbackParam.warnIfMissingReference && !warnedMissingEffectPlayer)
        {
            warnedMissingEffectPlayer = true;
            Debug.LogWarning($"{name} : EffectPlayerが取得できないため、反射Effectを再生できませんでした。");
        }
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
        return center - normal.normalized * radius + normal.normalized * 0.03f;
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
}