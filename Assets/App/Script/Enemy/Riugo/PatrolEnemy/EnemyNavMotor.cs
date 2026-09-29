using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(TimeAgent))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNavMotor : MonoBehaviour
{
    [System.Serializable]
    public class MoveParam
    {
        [Header("Move")]
        public float rotateSpeed = 10f;

        [Header("NavMesh")]
        public float repathInterval = 0.15f;
        public float sampleDistance = 1.0f;
        public float reachDistance = 0.8f;

        [Header("Move Stability")]
        public float keepMovingWhilePathPendingTime = 0.4f;
        public float steeringFallbackDistance = 0.25f;

        [Header("NavMesh Agent Reset")]
        [Tooltip("吹っ飛び終了時に現在位置からNavMeshへ復帰させる検索距離")]
        public float restoreNavMeshSearchRadius = 3.0f;

        [Tooltip("吹っ飛び中はNavMeshAgentを無効化する")]
        public bool disableAgentWhileFlying = true;
    }

    [System.Serializable]
    public class PhysicsParam
    {
        [Header("Basic Physics")]
        public float gravity = 28f;
        public float groundStickForce = 1f;
        public float mass = 1f;

        [Header("Horizontal Damping")]
        [Range(0f, 1f)] public float airHorizontalDampingPerFrame = 0.992f;
        [Range(0f, 1f)] public float groundedSlideDamping = 0.985f;

        [Header("Floor Bounce Horizontal Brake")]
        [Tooltip("床バウンドした瞬間に水平速度へ掛ける倍率。小さいほど一気に止まりやすい")]
        [Range(0f, 1f)] public float floorBounceHorizontalDamping = 0.55f;

        [Tooltip("初回の床バウンドだけ強めに減速する")]
        public bool applyFloorBounceDampingOnlyFirstBounce = false;

        [Header("Blow Common")]
        public float blowPowerMultiplier = 5f;
        [Range(0.1f, 1f)] public float massInfluence = 0.5f;

        [Header("By Tackle Type")]
        public TackleBlowSet tackleBlowSet = new TackleBlowSet();

        [Header("Bounce Common")]
        public bool enableBounce = true;
        public float minBounceSpeed = 1.0f;
        public int maxBounceCount = 6;

        [Header("Wall Bounce")]
        [Range(0f, 2f)] public float wallBouncePower = 0.9f;
        [Range(0f, 1f)] public float wallVerticalKeepRatio = 0.85f;
        public float minWallBounceSpeed = 0.5f;

        [Header("Floor Bounce")]
        [Range(0f, 2f)] public float floorBouncePower = 0.35f;
        public float minFloorUpwardSpeed = 2.5f;
        public float firstFloorBouncePowerMultiplier = 1.0f;

        [Header("Chain Upward Power")]
        [Tooltip("吹き飛び時に最低限与える上向き速度")]
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

        [Header("End Flying")]
        public float landingSpeedThreshold = 0.12f;
    }

    [Header("References")]
    [SerializeField] private GameObject bounceHitColliderRoot;

    [Header("Layer Names")]
    [SerializeField] private string rootIdleLayerName = "FieldObject";
    [SerializeField] private string rootFlyLayerName = "FieldObjectFly";
    [SerializeField] private string flyingHitLayerName = "FlyObject";
    [SerializeField] private string landedHitLayerName = "GroundObject";

    [Header("CharacterController Contact Masks")]
    [SerializeField] private LayerMask normalExcludeLayers;
    [SerializeField] private LayerMask launchKnockdownExcludeLayers;

    [Header("Move Settings")]
    [SerializeField] private MoveParam moveParam = new MoveParam();

    [Header("Physics Settings")]
    [SerializeField] private PhysicsParam physicsParam = new PhysicsParam();

    [Header("Debug")]
    [SerializeField] private bool debugBounceLog = false;


    private CharacterController controller;
    private TimeAgent agent;
    private EffectPlayer effectPlayer;
    private AudioManager audioManager;
    private NavMeshAgent navAgent;

    private Vector3 velocity;
    private float verticalVelocity;
    private Vector3 lastBlowDirection;
    private Vector3 lastValidMoveDir = Vector3.zero;
    private Vector3 lastRequestedTarget = Vector3.zero;

    private float repathTimer;
    private float pathPendingTimer;
    private float lastHitTime;

    private bool justBlownFrame;
    private bool isFlying;
    private bool hasLandedOnce;
    private bool chainCollisionEnabled = true;
    private bool floorContactNotifiedThisFlight;
    private bool bounceStrengthOverMin;

    private float flyingElapsedTime;
    private int flyingFrameCount;
    private bool navAgentDisabledByFlying;

    private int bounceCount;
    private float lastBounceTime = -999f;
    private int floorBounceCount;

    private int rootIdleLayer;
    private int rootFlyLayer;
    private int flyingHitLayer;
    private int landedHitLayer;

    public bool IsFlying => isFlying;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public bool HasLandedOnce => hasLandedOnce;

    public float TimeScale => agent != null ? agent.TimeScale : 1f;
    public bool IsOnNavMesh => navAgent != null && navAgent.isOnNavMesh;

    public Vector3 LastBlowDirection => lastBlowDirection;
    public float CurrentPlanarSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;

    public Vector3 CurrentPlanarVelocity
    {
        get
        {
            Vector3 v = velocity;
            v.y = 0f;
            return v;
        }
    }

    public Vector3 CurrentTotalVelocity
    {
        get
        {
            Vector3 v = velocity;
            v.y = verticalVelocity;
            return v;
        }
    }

    public Vector3 CurrentMoveDirection
    {
        get
        {
            Vector3 v = CurrentPlanarVelocity;
            if (v.sqrMagnitude > 0.0001f)
                return v.normalized;

            if (lastValidMoveDir.sqrMagnitude > 0.0001f)
                return lastValidMoveDir.normalized;

            return Vector3.zero;
        }
    }

    public float LastHitTime
    {
        get => lastHitTime;
        set => lastHitTime = value;
    }

    public event Action OnWallBounceWhileFlying;
    public event Action OnFirstGroundedAfterLaunch;
    public event Action OnFlyingEnded;
    public event Action<float, EnemySurfaceKind> OnReflectionSelfDamage;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();
        navAgent = GetComponent<NavMeshAgent>();

        rootIdleLayer = LayerMask.NameToLayer(rootIdleLayerName);
        rootFlyLayer = LayerMask.NameToLayer(rootFlyLayerName);
        flyingHitLayer = LayerMask.NameToLayer(flyingHitLayerName);
        landedHitLayer = LayerMask.NameToLayer(landedHitLayerName);

        navAgent.updatePosition = false;
        navAgent.updateRotation = false;
        navAgent.autoBraking = false;
        navAgent.nextPosition = transform.position;

        bounceStrengthOverMin = false;

        if (rootIdleLayer >= 0)
            gameObject.layer = rootIdleLayer;

        audioManager = AudioManager.Instance;

        ApplyNormalContactMode();
    }

    public void ApplyNormalContactMode()
    {
        if (controller == null)
            return;

        controller.excludeLayers = normalExcludeLayers;
    }

    public void ApplyLaunchKnockdownContactMode()
    {
        if (controller == null)
            return;

        controller.excludeLayers = launchKnockdownExcludeLayers;
    }

    public void SetCharacterControllerExcludeLayers(LayerMask mask)
    {
        if (controller == null)
            return;

        controller.excludeLayers = mask;
    }

    public void TickBegin(float dt)
    {
        repathTimer -= dt;
        navAgent.nextPosition = transform.position;
    }

    public void TickEnd(float dt)
    {
        ApplyGravity(dt);
        MoveObject(dt);

        if (isFlying)
        {
            flyingElapsedTime += dt;
            flyingFrameCount++;
            UpdateFlying(dt);
        }

        justBlownFrame = false;
    }

    public void RequestMove(Vector3 target, float moveSpeed)
    {
        if (isFlying)
            return;

        if (navAgent == null || !navAgent.enabled || !IsOnNavMesh)
        {
            StopPlanarVelocity();
            return;
        }

        lastRequestedTarget = target;
        Vector3 safeTarget = GetSafeNavMeshPosition(target);

        if (repathTimer <= 0f)
        {
            navAgent.SetDestination(safeTarget);
            repathTimer = moveParam.repathInterval;
        }

        Vector3 moveDir = GetStableMoveDirection();

        if (moveDir.sqrMagnitude < 0.0001f)
        {
            if (lastValidMoveDir.sqrMagnitude > 0.0001f)
                moveDir = lastValidMoveDir;
            else
            {
                StopPlanarVelocity();
                return;
            }
        }

        moveDir.Normalize();
        lastValidMoveDir = moveDir;

        SetPlanarVelocity(moveDir * moveSpeed);
        FaceTo(transform.position + moveDir);
    }

    public void StopPlanarVelocity()
    {
        if (isFlying)
            return;

        velocity.x = 0f;
        velocity.z = 0f;
    }

    public void AddExternalPlanarDisplacement(Vector3 displacement)
    {
        if (isFlying)
            return;

        displacement.y = 0f;
        controller.Move(displacement);
    }

    public void ResetPath()
    {
        navAgent.ResetPath();
    }

    public Vector3 GetSafeNavMeshPosition(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, moveParam.sampleDistance, NavMesh.AllAreas))
            return hit.position;

        return pos;
    }

    public bool IsReached(Vector3 target)
    {
        Vector3 a = transform.position;
        Vector3 b = target;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= moveParam.reachDistance;
    }

    public bool TryCalculatePath(Vector3 target, out NavMeshPath path)
    {
        path = new NavMeshPath();

        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
            return false;

        Vector3 safeTarget = GetSafeNavMeshPosition(target);

        bool success = navAgent.CalculatePath(safeTarget, path);

        return success && path.corners != null && path.corners.Length > 0;
    }

    public bool CanReach(Vector3 target)
    {
        if (!TryCalculatePath(target, out NavMeshPath path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    public bool HasReachablePath(Vector3 target)
    {
        if (!TryCalculatePath(target, out NavMeshPath path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete ||
               path.status == NavMeshPathStatus.PathPartial;
    }

    public float GetPathLength(Vector3 target)
    {
        if (!TryCalculatePath(target, out NavMeshPath path))
            return float.MaxValue;

        if (path.status != NavMeshPathStatus.PathComplete)
            return float.MaxValue;

        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);

        return total;
    }

    public bool TrySnapToNavMesh(float searchRadius = 2.0f)
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            navAgent.Warp(hit.position);
            return true;
        }

        return false;
    }

    private TackleBlowProfile GetTackleBlowProfile(TackleType tackleType)
    {
        return physicsParam.tackleBlowSet.GetProfile(tackleType);
    }

    public void ApplyBlow(Vector3 dir, float powerConst, float powerRate, TackleType tackleType)
    {
        TackleBlowProfile tackleProfile = GetTackleBlowProfile(tackleType);

        RestoreFlyingHitLayerIfNeeded();
        ApplyLaunchKnockdownContactMode();

        Vector3 horizontalDir = new Vector3(dir.x, 0f, dir.z);

        if (horizontalDir.sqrMagnitude < 0.0001f)
            horizontalDir = transform.forward;

        horizontalDir.Normalize();
        lastBlowDirection = horizontalDir;

        powerRate = Mathf.Clamp01(powerRate);

        float effectivePowerRate = Mathf.Lerp(
            tackleProfile.minPowerRatio,
            1f,
            powerRate
        );

        float effectivePower = powerConst * effectivePowerRate;
        float rawLaunchPower = effectivePower * physicsParam.blowPowerMultiplier;

        float safeMass = Mathf.Max(physicsParam.mass, 1f);
        float adjustedMass = Mathf.Pow(safeMass, physicsParam.massInfluence);

        float launchSpeed = rawLaunchPower / adjustedMass;
        launchSpeed = Mathf.Max(launchSpeed, tackleProfile.minLaunchSpeed);

        float launchAngle = Mathf.Lerp(
            tackleProfile.minLaunchAngle,
            tackleProfile.maxLaunchAngle,
            powerRate
        );

        float rad = launchAngle * Mathf.Deg2Rad;

        float horizontalSpeed = Mathf.Cos(rad) * launchSpeed * tackleProfile.horizontalLaunchBoost;
        float upwardSpeed = Mathf.Sin(rad) * launchSpeed;
        upwardSpeed = Mathf.Max(upwardSpeed, tackleProfile.minUpwardPower);

        velocity = horizontalDir * horizontalSpeed;
        verticalVelocity = upwardSpeed;

        justBlownFrame = true;

        BeginFlying();

        if (debugBounceLog)
        {
            Debug.Log(
                $"{name} ApplyBlow " +
                $"type:{tackleType} " +
                $"powerConst:{powerConst:F2} powerRate:{powerRate:F2} " +
                $"horizontalSpeed:{horizontalSpeed:F2} upwardSpeed:{upwardSpeed:F2}"
            );
        }
    }

    private bool IsInInitialFloorIgnoreWindow()
    {
        if (!isFlying)
            return false;

        if (flyingElapsedTime < physicsParam.initialFloorIgnoreTime)
            return true;

        if (flyingFrameCount < physicsParam.initialFloorIgnoreFrames)
            return true;

        return false;
    }

    public void NotifyBounce(Vector3 hitNormal, EnemySurfaceKind kind)
    {
        if (!isFlying)
            return;

        if (!physicsParam.enableBounce)
            return;

        if (hitNormal.sqrMagnitude < 0.0001f)
            return;

        hitNormal.Normalize();

        switch (kind)
        {
            case EnemySurfaceKind.Floor:
                {
                    if (IsInInitialFloorIgnoreWindow())
                    {
                        if (debugBounceLog)
                            Debug.Log($"{name} Floor ignored at early flight window");
                        return;
                    }

                    if (!bounceStrengthOverMin)
                        return;

                    float downSpeed = Mathf.Max(0f, -verticalVelocity);
                    if (downSpeed < physicsParam.minBounceSpeed)
                        downSpeed = physicsParam.minBounceSpeed;

                    float bouncePower = physicsParam.floorBouncePower;
                    if (floorBounceCount == 0)
                        bouncePower *= physicsParam.firstFloorBouncePowerMultiplier;

                    verticalVelocity = Mathf.Max(
                        downSpeed * bouncePower,
                        physicsParam.minFloorUpwardSpeed
                    );

                    if(verticalVelocity <= physicsParam.minFloorUpwardSpeed)
                    {
                        bounceStrengthOverMin = false;
                    }

                    bool shouldApplyFloorBrake = !physicsParam.applyFloorBounceDampingOnlyFirstBounce ||floorBounceCount == 0;

                    if (shouldApplyFloorBrake)
                    {
                        velocity.x *= physicsParam.floorBounceHorizontalDamping;
                        velocity.z *= physicsParam.floorBounceHorizontalDamping;
                    }

                    bounceCount++;
                    floorBounceCount++;
                    lastBounceTime = Time.time;

                    if (effectPlayer != null)
                        effectPlayer.Play(2);

                    if (!floorContactNotifiedThisFlight)
                    {
                        floorContactNotifiedThisFlight = true;
                        hasLandedOnce = true;
                        DisableChainCollision();
                        SetLandedHitLayer();
                        OnFirstGroundedAfterLaunch?.Invoke();
                    }

                    if (debugBounceLog)
                    {
                        Debug.Log(
                            $"{name} FloorBounce " +
                            $"normal:{hitNormal} verticalVelocity:{verticalVelocity:F2}"
                        );
                    }
                    break;
                }

            case EnemySurfaceKind.Wall:
                {
                    Vector3 currentVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);
                    float speed = currentVelocity.magnitude;

                    if (speed < physicsParam.minBounceSpeed)
                        return;

                    Vector3 reflected = Vector3.Reflect(currentVelocity, hitNormal);



                    velocity.x = reflected.x * physicsParam.wallBouncePower;
                    velocity.z = reflected.z * physicsParam.wallBouncePower;
                    verticalVelocity = reflected.y * physicsParam.wallVerticalKeepRatio;

                    bounceCount++;
                    lastBounceTime = Time.time;

                    if (effectPlayer != null)
                    {
                        Vector3 normal = hitNormal;

                        normal.Normalize();

                        Vector3 position = GetSurfaceEffectPosition(normal);
                        Quaternion rotation = CreateSurfaceEffectRotation(normal, reflected);

                        EffectPlayParam param = EffectPlayParam.Default;
                        param.rotationOffset = Vector3.zero;
                        param.scale = new Vector3(2.0f, 2.0f, 2.0f);

                        audioManager.PlaySe("E_WallHit", position);

                        effectPlayer.PlayAt
                            (
                                0,
                                position,
                                rotation,
                                Vector3.one,
                                param
                            );
                    }
                        

                    if (debugBounceLog)
                    {
                        Debug.Log(
                            $"{name} WallBounce " +
                            $"normal:{hitNormal} reflected:{reflected}"
                        );
                    }

                    RaiseReflectionSelfDamage(EnemySurfaceKind.Wall);

                    OnWallBounceWhileFlying?.Invoke();
                    break;
                }

            case EnemySurfaceKind.Ceiling:
                {
                    Vector3 currentVelocity = new Vector3(velocity.x, verticalVelocity, velocity.z);
                    float speed = currentVelocity.magnitude;

                    if (speed < physicsParam.minBounceSpeed)
                        return;

                    Vector3 reflected = Vector3.Reflect(currentVelocity, hitNormal);

                    velocity.x = reflected.x * physicsParam.wallBouncePower;
                    velocity.z = reflected.z * physicsParam.wallBouncePower;
                    verticalVelocity = Mathf.Min(reflected.y, -physicsParam.minFloorUpwardSpeed);

                    bounceCount++;
                    lastBounceTime = Time.time;

                    if (effectPlayer != null)
                    {
                        Vector3 normal = hitNormal;

                        normal.Normalize();

                        Vector3 position = GetSurfaceEffectPosition(normal);
                        Quaternion rotation = CreateSurfaceEffectRotation(normal, reflected);

                        EffectPlayParam param = EffectPlayParam.Default;
                        param.rotationOffset = Vector3.zero;
                        param.scale = new Vector3(2.0f, 2.0f, 2.0f);

                        audioManager.PlaySe("E_WallHit", position);

                        effectPlayer.PlayAt
                            (
                                0,
                                position,
                                rotation,
                                Vector3.one,
                                param
                            );
                    }

                    RaiseReflectionSelfDamage(EnemySurfaceKind.Ceiling);

                    if (debugBounceLog)
                    {
                        Debug.Log(
                            $"{name} CeilingBounce " +
                            $"normal:{hitNormal} reflected:{reflected}"
                        );
                    }
                    break;
                }
        }
    }

    public Vector3 GetChainDirectionTo(GameObject targetObject)
    {
        Vector3 dir = targetObject.transform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
        {
            Vector3 totalVelocity = new Vector3(velocity.x , verticalVelocity, velocity.z);
            dir = new Vector3(totalVelocity.x, 0f, totalVelocity.z);

            if (dir.sqrMagnitude < 0.001f)
                dir = transform.forward;
        }

        return dir.normalized;
    }
    public void ApplyChainReaction(Vector3 horizontalDir ,float horizontalPower, float verticalPower)
    {
        RestoreFlyingHitLayerIfNeeded();
        ApplyLaunchKnockdownContactMode();

        Vector3 horizontal = new Vector3(horizontalDir.x, 0f, horizontalDir.z);

        if (horizontal.sqrMagnitude < 0.001f)
            horizontal = transform.forward;

        horizontal.Normalize();

        velocity = horizontal * horizontalPower;
        verticalVelocity = Mathf.Max(verticalPower, physicsParam.minUpwardPower);

        if (effectPlayer != null)
            effectPlayer.Play(1);

        BeginFlying();
    }

    private void RaiseReflectionSelfDamage(EnemySurfaceKind kind)
    {
        if (!physicsParam.enableReflectionSelfDamage)
            return;

        float damage = 0f;

        switch (kind)
        {
            case EnemySurfaceKind.Wall:
                damage = physicsParam.wallReflectionDamage;
                break;

            case EnemySurfaceKind.Ceiling:
                damage = physicsParam.ceilingReflectionDamage;
                break;

            case EnemySurfaceKind.Floor:
            default:
                return;
        }

        if (damage <= 0f)
            return;

        OnReflectionSelfDamage?.Invoke(damage, kind);

        if (debugBounceLog)
        {
            Debug.Log(
                $"{name} ReflectionSelfDamage " +
                $"kind:{kind} damage:{damage:F2}"
            );
        }
    }
    public void DisableChainCollision()
    {
        chainCollisionEnabled = false;
    }

    public bool CanChainHit()
    {
        return chainCollisionEnabled;
    }

    private void RestoreFlyingHitLayerIfNeeded()
    {
        if (bounceHitColliderRoot == null)
            return;

        if (flyingHitLayer < 0)
            return;

        if (bounceHitColliderRoot.layer != flyingHitLayer)
            SetLayerRecursive(bounceHitColliderRoot, flyingHitLayer);
    }

    private void SetLandedHitLayer()
    {
        if (bounceHitColliderRoot == null)
            return;

        if (landedHitLayer < 0)
            return;

        SetLayerRecursive(bounceHitColliderRoot, landedHitLayer);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;

        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
    }

    private Vector3 GetStableMoveDirection()
    {
        if (navAgent.pathPending)
        {
            pathPendingTimer += Time.deltaTime * TimeScale;

            if (pathPendingTimer <= moveParam.keepMovingWhilePathPendingTime &&
                lastValidMoveDir.sqrMagnitude > 0.0001f)
            {
                return lastValidMoveDir;
            }

            return Vector3.zero;
        }

        pathPendingTimer = 0f;

        if (!navAgent.hasPath)
            return lastValidMoveDir.sqrMagnitude > 0.0001f ? lastValidMoveDir : Vector3.zero;

        if (navAgent.pathStatus != NavMeshPathStatus.PathComplete &&
            navAgent.pathStatus != NavMeshPathStatus.PathPartial)
        {
            return Vector3.zero;
        }

        Vector3 dir = GetDirectionToNextPathCorner();

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        if (lastValidMoveDir.sqrMagnitude > 0.0001f)
            return lastValidMoveDir.normalized;

        return Vector3.zero;
    }

    private void BeginFlying()
    {
        isFlying = true;
        hasLandedOnce = false;
        chainCollisionEnabled = true;
        floorContactNotifiedThisFlight = false;
        bounceStrengthOverMin = true;

        flyingElapsedTime = 0f;
        flyingFrameCount = 0;

        bounceCount = 0;
        floorBounceCount = 0;
        lastBounceTime = -999f;

        if (rootFlyLayer >= 0)
            gameObject.layer = rootFlyLayer;

        RestoreFlyingHitLayerIfNeeded();

        DisableNavAgentForFlying();

        if (debugBounceLog)
            Debug.Log($"{name} BeginFlying / Bounce Layer -> FlyObject");
    }

    private void DisableNavAgentForFlying()
    {
        if (!moveParam.disableAgentWhileFlying)
            return;

        if (navAgent == null)
            return;

        if (navAgent.enabled)
        {
            if (navAgent.isOnNavMesh)
                navAgent.ResetPath();

            navAgent.enabled = false;
            navAgentDisabledByFlying = true;
        }
    }
    private void EndFlying()
    {
        isFlying = false;

        if (rootIdleLayer >= 0)
            gameObject.layer = rootIdleLayer;

        ApplyNormalContactMode();

        StopPlanarVelocity();
        lastValidMoveDir = Vector3.zero;

        RestoreNavAgentAfterFlying();

        if (debugBounceLog)
            Debug.Log($"{name} EndFlying");

        OnFlyingEnded?.Invoke();
    }

    public void ForceEndFlying()
    {
        hasLandedOnce = true;
        floorContactNotifiedThisFlight = true;

        EndFlying();
    }

    private void UpdateFlying(float dt)
    {
        if (controller.isGrounded)
            velocity *= physicsParam.groundedSlideDamping;
        else
            velocity *= physicsParam.airHorizontalDampingPerFrame;

        if (hasLandedOnce && CurrentPlanarSpeed < physicsParam.landingSpeedThreshold)
            velocity = Vector3.zero;

        if (hasLandedOnce &&
            CurrentPlanarSpeed <= physicsParam.landingSpeedThreshold &&
            controller.isGrounded)
        {
            EndFlying();
        }
    }

    private void ApplyGravity(float dt)
    {
        if (controller.isGrounded && !justBlownFrame)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -physicsParam.groundStickForce;
        }
        else
        {
            verticalVelocity -= physicsParam.gravity * dt;
        }
    }

    private void MoveObject(float dt)
    {
        Vector3 totalVelocity = velocity;
        totalVelocity.y = verticalVelocity;

        controller.Move(totalVelocity * dt);

        if (navAgent != null && navAgent.isOnNavMesh)
            navAgent.nextPosition = transform.position;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -physicsParam.groundStickForce;
    }

    private void SetPlanarVelocity(Vector3 planarVelocity)
    {
        planarVelocity.y = 0f;
        velocity.x = planarVelocity.x;
        velocity.z = planarVelocity.z;
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
            moveParam.rotateSpeed * Time.deltaTime);
    }

    public bool IsEnemyFlying()
    {
        return IsFlying;
    }

    private Vector3 GetDirectionToNextPathCorner()
    {
        NavMeshPath path = navAgent.path;

        if (path == null || path.corners == null || path.corners.Length == 0)
            return Vector3.zero;

        Vector3 current = transform.position;
        current.y = 0f;

        float skipDistance = Mathf.Max(0.05f, moveParam.steeringFallbackDistance);

        for (int i = 0; i < path.corners.Length; i++)
        {
            Vector3 corner = path.corners[i];
            corner.y = 0f;

            Vector3 toCorner = corner - current;

            if (toCorner.sqrMagnitude >= skipDistance * skipDistance)
                return toCorner;
        }

        Vector3 targetDir = lastRequestedTarget - transform.position;
        targetDir.y = 0f;

        return targetDir;
    }

    private void RestoreNavAgentAfterFlying()
    {
        if (!moveParam.disableAgentWhileFlying)
            return;

        if (navAgent == null)
            return;

        if (!navAgentDisabledByFlying)
            return;

        navAgent.enabled = true;
        navAgentDisabledByFlying = false;

        Vector3 currentPosition = transform.position;

        if (NavMesh.SamplePosition(
            currentPosition,
            out NavMeshHit hit,
            moveParam.restoreNavMeshSearchRadius,
            NavMesh.AllAreas))
        {
            transform.position = hit.position;
            navAgent.Warp(hit.position);
            navAgent.nextPosition = hit.position;
            navAgent.ResetPath();

            if (debugBounceLog)
                Debug.Log($"{name} RestoreNavAgentAfterFlying snap:{hit.position}");
        }
        else
        {
            // NavMeshが近くにない場合は、Agentだけ復帰させるが経路は持たせない
            navAgent.Warp(currentPosition);
            navAgent.nextPosition = currentPosition;
            navAgent.ResetPath();

            if (debugBounceLog)
                Debug.LogWarning($"{name} RestoreNavAgentAfterFlying failed: no NavMesh near {currentPosition}");
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