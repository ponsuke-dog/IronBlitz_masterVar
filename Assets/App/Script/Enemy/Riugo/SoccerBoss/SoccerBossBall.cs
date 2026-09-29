using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SoccerBossBall : MonoBehaviour, IHitSource, IHitReceiver
{
    #region Enums

    public enum BallEndReason
    {
        LifeEnd,
        HitPlayer,
        HitBoss,
        HitField,
        LandedTimeout,
        Manual,
        Exploded
    }

    private enum BallMode
    {
        Arc,
        Straight,
        Reflected,
        Landed,
        Exploding
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

    [System.Serializable]
    private class ReflectParam
    {
        [Header("Reflect")]
        public float reflectedSpeed = 24f;

        [Tooltip("0なら完全直線。大きいほど狙い方向へ曲がる")]
        public float homingPower = 1.5f;

        public float homingDelay = 0.15f;
        public float bossHitDistance = 1.4f;
        public float reflectedLifeTime = 4f;

        [Header("Reflect Target")]
        public Transform reflectAimPoint;
        public Vector3 bossAimOffset = new Vector3(0f, 1.8f, 0f);
        public float aimRandomRadius = 0f;
        public bool lockAimPointOnReflect = true;
        public float reflectedUpBias = 0.0f;

        [Header("Reflect Condition")]
        public bool reflectAnyHitEvent = true;
        public LayerMask reflectLayer;
        public bool allowReReflect = false;

        [Header("Reflect Aim Selection")]
        public float reflectAimSelectAngle = 70f;
        public bool requireAimPointInsideAngle = false;
        public bool applyRandomRadiusToAimPoint = false;
        public bool selectAimPointPlanar = true;

        [Header("Power Height Aim")]
        public bool usePowerBasedHeightAim = true;
        public float minPowerRateForHeight = 0f;
        public float maxPowerRateForHeight = 1f;
        public float heightScoreWeight = 0.65f;
        public float minHeightRange = 0.5f;
        public float extraHeightByPower = 0.6f;
        public bool logSelectedAimPoint = true;

        [Header("Reflect Speed From Power")]
        public bool usePowerBasedReflectSpeed = true;
        public float powerConstantSpeedMultiplier = 0.05f;
        public float powerRateSpeedBonus = 6f;
        public float minReflectedSpeed = 16f;
        public float maxReflectedSpeed = 42f;
        public bool logResolvedReflectSpeed = true;

        [Header("Reflected Boss Hit")]
        public bool useReflectedBossCollider = true;

        [Tooltip("反射Hitbox使用中は距離判定でBossに当てない")]
        public bool disableDistanceBossHitWhenUsingCollider = true;
    }

    [System.Serializable]
    private class ColliderParam
    {
        [Header("Normal Collision Object")]
        [Tooltip("通常時に使う判定子オブジェクト。Playerダメージ、反射、Field着弾に使う")]
        public GameObject[] normalCollisionObjects;

        [Header("Reflected Boss Collision Object")]
        [Tooltip("反射中にBossへ当てるための判定子オブジェクト")]
        public GameObject[] reflectedBossCollisionObjects;

        [Tooltip("反射時にNormal Collision ObjectをOFFにする")]
        public bool disableNormalCollisionObjectsOnReflect = true;

        [Header("Explosion Collision Object")]
        [Tooltip("爆発中だけONにする判定子オブジェクト")]
        public GameObject[] explosionCollisionObjects;

        [Tooltip("爆発判定をONにした瞬間、範囲内をOverlapで即チェックする")]
        public bool checkExplosionOverlapOnEnable = true;
    }

    [System.Serializable]
    private class HitParam
    {
        [Header("Layers")]
        public LayerMask playerLayer;
        public LayerMask bossLayer;
        public LayerMask fieldLayer;

        [Header("Hit")]
        public float hitCooldown = 0.05f;
        public bool destroyOnFieldBeforeReflect = false;
        public bool destroyOnFieldAfterReflect = true;
        public bool damagePlayerBeforeReflect = true;
    }

    [System.Serializable]
    private class LandingParam
    {
        [Header("Landed Reflect Window")]
        public float reflectableLandedDuration = 1.0f;
        public float landedYOffset = 0.15f;
        public bool damagePlayerWhileLanded = true;
    }

    [System.Serializable]
    private class ExplosionParam
    {
        [Header("Explosion")]
        public bool explodeOnImpact = true;
        public float duration = 0.8f;

        [Tooltip("ExplosionCollider未設定時の保険判定半径")]
        public float radius = 2.0f;

        [Tooltip("爆発ダメージ。0以下ならballのplayerDamageを使う")]
        public int damage = 0;

        public LayerMask playerLayer;
        public bool damageSameTargetOnce = true;

        [Header("Visual")]
        public bool hideModelOnExplosion = true;
        public Transform modelRoot;
        public GameObject explosionVfxPrefab;
        public float explosionVfxLifeTime = 2f;
    }

    [System.Serializable]
    private class FlyEffectParam
    {
        [Header("Fly Effect")]
        public int effectIndex = 0;
        public float forwardOffset = 3.0f;
        public Vector3 worldOffset = Vector3.zero;
        public bool faceMoveDirection = true;

        [Header("Axis Correction")]
        public EffectForwardAxis prefabForwardAxis = EffectForwardAxis.ZPlus;
        public Vector3 rotationOffsetEuler = Vector3.zero;
        public bool useWorldUp = true;

        [Header("Stop")]
        public bool stopOnReflect = true;
        public bool stopOnExplosion = true;
    }

    [System.Serializable]
    private class SweepParam
    {
        [Header("Sweep Hit")]
        public bool useSweepHit = true;
        public float sweepRadius = 0.8f;
        public LayerMask sweepLayer;
        public bool ignoreSelf = true;
    }

    [System.Serializable]
    private class DebugParam
    {
        public bool logOnHit = true;
        public bool logTrigger = false;
        public bool logDestroyReason = true;
        public bool logColliderSwitch = true;
        public bool logReflectedBossHit = true;

        [Header("Gizmos")]
        public bool drawGizmos = true;
        public bool drawAlways = true;
        public bool drawVelocity = true;
        public bool drawSweep = true;
        public bool drawBossAim = true;
        public bool drawLandingTarget = true;
        public bool drawBezierArc = true;

        [Range(4, 64)]
        public int bezierPreviewSegments = 24;

        public float velocityLineScale = 0.2f;
    }

    [System.Serializable]
    private class VisualSpinParam
    {
        [Header("Ball Visual Spin")]
        public Transform modelRoot;
        public float spinRate = 45f;
        public float minSpinSpeed = 180f;
        public float maxSpinSpeed = 1440f;
        public bool spinWhileReflected = true;
    }

    #endregion

    #region Inspector Fields

    [Header("Reflect")]
    [SerializeField] private ReflectParam reflectParam = new ReflectParam();

    [Header("Colliders")]
    [SerializeField] private ColliderParam colliderParam = new ColliderParam();

    [Header("Hit")]
    [SerializeField] private HitParam hitParam = new HitParam();

    [Header("Landing")]
    [SerializeField] private LandingParam landingParam = new LandingParam();

    [Header("Sweep")]
    [SerializeField] private SweepParam sweepParam = new SweepParam();

    [Header("Time")]
    [SerializeField] private TimeAgent timeAgent;

    [Header("Debug")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    [Header("Explosion")]
    [SerializeField] private ExplosionParam explosionParam = new ExplosionParam();

    [Header("Fly Effect")]
    [SerializeField] private FlyEffectParam flyEffectParam = new FlyEffectParam();

    [Header("Visual Spin")]
    [SerializeField] private VisualSpinParam visualSpinParam = new VisualSpinParam();

    #endregion

    #region Runtime Fields

    public event Action<SoccerBossBall, BallEndReason> OnBallFinished;

    private SoccerBoss boss;
    private Transform bossTarget;

    private Collider rootCollider;
    private Rigidbody rb;

    private EffectPlayer effectPlayer;
    private EffectBonePlayer effectBonePlayer;
    private EffectInstance flyeffect;
    private EffectInstance aimEffect;
    private AudioManager audioManager;

    private Transform flyEffectPoint;
    private Vector3 lastEffectDirection = Vector3.forward;

    private BallMode mode;
    private Vector3 velocity;
    private float gravity;

    private float lifeTimer;
    private float lifeLimit = 8f;
    private float localTime;

    private int playerDamage;
    private float reflectedDamageToBoss;
    private float reflectedStunValue;

    private Vector3 landingPosition;

    private bool reflected;
    private float reflectedTimer;
    private float lastHitTime = -999f;
    private float landedTimer;
    private bool finished;

    private Vector3 reflectedAimPosition;
    private bool hasReflectedAimPosition;

    private Vector3 previousPosition;
    private Vector3 lastSweepStart;
    private Vector3 lastSweepEnd;
    private bool hasSweepDebug;

    private bool useBezierArc;
    private Vector3 arcStart;
    private Vector3 arcControl;
    private Vector3 arcEnd;
    private float arcTimer;
    private float arcDuration;

    private bool IsOverHead;

    private SoccerBallEnemySpawn enemySpawner;

    private float explosionTimer;
    private readonly HashSet<GameObject> explosionHitObjects =
        new HashSet<GameObject>();

    private Renderer[] cachedRenderers;

    private Transform[] reflectAimTargets;
    private Transform lastSelectedReflectAimTarget;

    private float lastReflectPowerRate = 1f;
    private float lastReflectPowerConstant = 0f;
    private float currentReflectedSpeed = 0f;

    public bool IsReflected => reflected;

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;

    #endregion

    #region Unity Events

    private void Awake()
    {
        rootCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (timeAgent == null)
            timeAgent = GetComponent<TimeAgent>();

        if (rootCollider != null)
            rootCollider.isTrigger = true;

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        enemySpawner = GetComponent<SoccerBallEnemySpawn>();

        cachedRenderers = explosionParam.modelRoot != null
            ? explosionParam.modelRoot.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        effectBonePlayer = GetComponentInChildren<EffectBonePlayer>(true);
        effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        ResolveCollisionObjectReferences();
        InitializeCollisionObjects();
        SetCollisionModeNormal();

        CreateFlyEffectPoint();
        flyeffect = null;
        aimEffect = null;
        audioManager = AudioManager.Instance;
        IsOverHead = false;
    }

    private void Update()
    {
        if (finished)
            return;

        float dt = Time.deltaTime * TimeScale;

        localTime += dt;
        lifeTimer += dt;

        if (lifeTimer >= lifeLimit)
        {
            Finish(BallEndReason.LifeEnd);
            return;
        }

        switch (mode)
        {
            case BallMode.Arc:
                UpdateArc(dt);
                break;

            case BallMode.Straight:
                UpdateStraight(dt);
                break;

            case BallMode.Reflected:
                UpdateReflected(dt);
                break;

            case BallMode.Landed:
                UpdateLanded(dt);
                break;

            case BallMode.Exploding:
                UpdateExploding(dt);
                break;
        }

        UpdateVisualSpin(dt);
        UpdateFlyEffectPoint();
        DrawRuntimeDebugLines();
    }

    private void OnDestroy()
    {
        DestroyFlyEffectPoint();
    }

    #endregion

    #region Launch

    public void LaunchArc(
        SoccerBoss boss,
        Transform bossTarget,
        Vector3 start,
        Vector3 landingTarget,
        float flightTime,
        float gravity,
        float arcHeight,
        int playerDamage,
        float reflectedDamageToBoss,
        float reflectedStunValue
        )
    {
        this.boss = boss;
        this.bossTarget = bossTarget;
        this.gravity = Mathf.Max(0.01f, gravity);
        this.playerDamage = playerDamage;
        this.reflectedDamageToBoss = reflectedDamageToBoss;
        this.reflectedStunValue = reflectedStunValue;
        
        this.landingPosition = landingTarget;

        mode = BallMode.Arc;
        reflected = false;
        reflectedTimer = 0f;
        landedTimer = 0f;
        lifeTimer = 0f;
        localTime = 0f;
        finished = false;

        transform.position = start;
        previousPosition = transform.position;

        arcStart = start;
        arcEnd = landingTarget;

        aimEffect = effectPlayer.PlayAt(2, landingTarget);

        Vector3 mid = (arcStart + arcEnd) * 0.5f;
        mid.y = Mathf.Max(arcStart.y, arcEnd.y) + Mathf.Max(0.1f, arcHeight);

        arcControl = mid;
        arcTimer = 0f;
        arcDuration = Mathf.Max(0.1f, flightTime);
        useBezierArc = true;

        lifeLimit = Mathf.Max(
            1f,
            arcDuration + landingParam.reflectableLandedDuration + 0.75f
        );

        velocity = Vector3.zero;

        Vector3 startTangent = EvaluateQuadraticBezierTangent(
            arcStart,
            arcControl,
            arcEnd,
            0f
        );

        if (startTangent.sqrMagnitude > 0.0001f)
            lastEffectDirection = startTangent.normalized;

        explosionTimer = 0f;
        explosionHitObjects.Clear();

        SetCollisionModeNormal();
        PlayBallEffect();
    }

    public void LaunchStraightToTarget(
        SoccerBoss boss,
        Transform bossTarget,
        Vector3 start,
        Vector3 target,
        float speed,
        float lifeTime,
        int playerDamage,
        float reflectedDamageToBoss,
        float reflectedStunValue)
    {
        this.boss = boss;
        this.bossTarget = bossTarget;
        this.playerDamage = playerDamage;
        this.reflectedDamageToBoss = reflectedDamageToBoss;
        this.reflectedStunValue = reflectedStunValue;
        this.landingPosition = target;

        mode = BallMode.Straight;
        reflected = false;
        reflectedTimer = 0f;
        landedTimer = 0f;
        lifeTimer = 0f;
        localTime = 0f;
        finished = false;
        useBezierArc = false;

        aimEffect = effectPlayer.PlayAt(2, target);

        explosionTimer = 0f;
        explosionHitObjects.Clear();

        lifeLimit = Mathf.Max(0.5f, lifeTime);

        transform.position = start;
        previousPosition = transform.position;

        Vector3 dir = target - start;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        velocity = dir * speed;
        lastEffectDirection = dir;

        FaceVelocity();

        SetCollisionModeNormal();
        PlayBallEffectO();
    }

    #endregion

    #region State Updates

    private void UpdateArc(float dt)
    {
        if (useBezierArc)
        {
            arcTimer += dt;

            float t = Mathf.Clamp01(arcTimer / arcDuration);

            Vector3 current = transform.position;
            Vector3 nextPosition = EvaluateQuadraticBezier(
                arcStart,
                arcControl,
                arcEnd,
                t
            );

            velocity = dt > 0f
                ? (nextPosition - current) / dt
                : Vector3.zero;

            if (MoveWithSweep(nextPosition))
                return;

            FaceVelocity();

            if (t >= 1f)
                EnterLanded();

            return;
        }

        velocity.y -= gravity * dt;

        Vector3 fallbackNextPosition = transform.position + velocity * dt;

        if (MoveWithSweep(fallbackNextPosition))
            return;

        FaceVelocity();

        if (transform.position.y <= landingPosition.y + landingParam.landedYOffset)
            EnterLanded();
    }

    private void UpdateStraight(float dt)
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = transform.position + velocity * dt;

        if (!reflected && TryExplodeOnLandingPlane(currentPosition, nextPosition))
            return;

        if (MoveWithSweep(nextPosition))
            return;

        FaceVelocity();
    }

    private void UpdateReflected(float dt)
    {
        reflectedTimer += dt;

        Vector3 aim = GetCurrentReflectAimPosition();

        bool useDistanceBossHit =
            !reflectParam.useReflectedBossCollider ||
            !reflectParam.disableDistanceBossHitWhenUsingCollider;

        if (useDistanceBossHit)
        {
            float dist = Vector3.Distance(transform.position, aim);

            if (dist <= reflectParam.bossHitDistance)
            {
                HitBoss();
                return;
            }
        }

        Vector3 desiredDir = aim - transform.position;

        if (desiredDir.sqrMagnitude > 0.0001f)
        {
            desiredDir.Normalize();

            Vector3 currentDir = velocity.sqrMagnitude > 0.0001f
                ? velocity.normalized
                : desiredDir;

            if (reflectedTimer >= reflectParam.homingDelay &&
                reflectParam.homingPower > 0f)
            {
                currentDir = Vector3.Slerp(
                    currentDir,
                    desiredDir,
                    Mathf.Clamp01(reflectParam.homingPower * dt)
                );
            }

            float speed = currentReflectedSpeed > 0f
                ? currentReflectedSpeed
                : reflectParam.reflectedSpeed;

            velocity = currentDir.normalized * speed;
        }

        Vector3 nextPosition = transform.position + velocity * dt;

        if (MoveWithSweep(nextPosition))
            return;

        FaceVelocity();
    }

    private void UpdateLanded(float dt)
    {
        landedTimer += dt;

        if (landedTimer >= landingParam.reflectableLandedDuration)
            Finish(BallEndReason.LandedTimeout);
    }

    private void UpdateExploding(float dt)
    {
        explosionTimer += dt;

        if (GetExplosionCollisionObjectCount() <= 0)
            ApplyExplosionDamage();

        if (explosionTimer >= explosionParam.duration)
        {
            SetCollisionModeOff();
            Finish(BallEndReason.Exploded);
        }
    }

    #endregion

    #region Collision Entry Point

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        Debug.Log(
        $"{name} OnHitDetected called " +
        $"self:{(selfHitbox != null ? selfHitbox.gameObject.name : "null")} " +
        $"other:{(other != null ? other.name : "null")}"
        );

        if (finished || selfHitbox == null || other == null)
            return;

        if (IsHitboxInObjectGroup(
            selfHitbox,
            colliderParam.normalCollisionObjects))
        {
            HandleNormalCollision(other, "OnHitDetected Normal");
            return;
        }

        if (IsHitboxInObjectGroup(
            selfHitbox,
            colliderParam.reflectedBossCollisionObjects))
        {
            TryHitBossByReflectedCollider(
                other,
                "OnHitDetected ReflectedBoss"
            );
            return;
        }

        if (IsHitboxInObjectGroup(
            selfHitbox,
            colliderParam.explosionCollisionObjects))
        {
            TryApplyExplosionHit(
                other,
                "OnHitDetected Explosion"
            );
            return;
        }

        if (debugParam.logTrigger)
        {
            Debug.Log(
                $"{name} OnHitDetected ignored unknown selfHitbox:{selfHitbox.name} other:{other.name}"
            );
        }
    }

    public void OnHit(HitEventData data)
    {
        if (!CanReflectNow())
            return;

        bool canReflect =
            data.payload is BlowPayload ||
            reflectParam.reflectAnyHitEvent ||
            data.payload == null;

        if (!canReflect)
            return;

        Transform attacker = null;

        if (data.attackerObject != null)
            attacker = data.attackerObject.transform;
        else if (data.attackerHitbox != null)
            attacker = data.attackerHitbox.transform;

        float powerRate = 0f;
        float powerConstant = 0f;

        if (data.payload is BlowPayload blow)
        {
            powerRate = Mathf.Clamp01(blow.powerRate);
            powerConstant = Mathf.Max(0f, blow.powerConstant);
        }

        ReflectByPlayer(attacker, "OnHit", powerRate, powerConstant);

        if (debugParam.logOnHit)
        {
            Debug.Log(
                $"{name} reflected by HitEvent " +
                $"payload:{(data.payload != null ? data.payload.GetType().Name : "null")} " +
                $"targetHitbox:{(data.targetHitbox != null ? data.targetHitbox.name : "null")}"
            );
        }
    }

    #endregion

    #region Collision Handling

    private void HandleNormalCollision(Collider other, string reason)
    {
        if (localTime < lastHitTime + hitParam.hitCooldown)
            return;

        int layerBit = 1 << other.gameObject.layer;

        if (debugParam.logTrigger)
        {
            Debug.Log(
                $"{name} NormalCollision other:{other.name} " +
                $"layer:{LayerMask.LayerToName(other.gameObject.layer)} " +
                $"mode:{mode} reflected:{reflected} reason:{reason}"
            );
        }

        if (CanReflectNow() &&
            (layerBit & reflectParam.reflectLayer.value) != 0)
        {
            lastHitTime = localTime;
            ReflectByPlayer(other.transform, $"NormalCollision ReflectLayer {reason}");
            return;
        }

        bool canDamagePlayer =
            !reflected &&
            hitParam.damagePlayerBeforeReflect &&
            (
                mode == BallMode.Arc ||
                mode == BallMode.Straight ||
                (mode == BallMode.Landed && landingParam.damagePlayerWhileLanded)
            );

        if (canDamagePlayer &&
            (layerBit & hitParam.playerLayer.value) != 0)
        {
            lastHitTime = localTime;
            HitPlayer(other);
            return;
        }

        if ((layerBit & hitParam.fieldLayer.value) != 0)
        {
            if (!reflected)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                BeginExplosion(hitPoint, $"NormalCollision FieldBeforeReflect {reason}");
                return;
            }

            if (reflected && hitParam.destroyOnFieldAfterReflect)
            {
                Finish(BallEndReason.HitField);
                return;
            }
        }
    }

    private void TryHitBossByReflectedCollider(Collider other, string reason)
    {
        if (!reflected)
            return;

        if (mode != BallMode.Reflected)
            return;

        if (other == null)
            return;

        // 追加：Boss本体またはBossの子Collider以外は無視
        if (!IsColliderBelongsToBoss(other))
        {
            if (debugParam.logReflectedBossHit)
            {
                Debug.Log(
                    $"{name} ReflectedBossCollider ignored non-boss " +
                    $"other:{other.name} root:{other.transform.root.name} reason:{reason}"
                );
            }

            return;
        }

        if (!IsLayerAllowed(hitParam.bossLayer, other.gameObject, true))
        {
            if (debugParam.logReflectedBossHit)
            {
                Debug.Log(
                    $"{name} ReflectedBossCollider ignored layer " +
                    $"other:{other.name} layer:{LayerMask.LayerToName(other.gameObject.layer)} reason:{reason}"
                );
            }

            return;
        }

        if (debugParam.logReflectedBossHit)
        {
            Debug.Log(
                $"{name} ReflectedBossCollider HIT Boss " +
                $"other:{other.name} reason:{reason}"
            );
        }

        HitBoss();
    }

    private void TryApplyExplosionHit(Collider other, string reason)
    {
        if (other == null)
            return;

        LayerMask targetLayer = explosionParam.playerLayer.value != 0
            ? explosionParam.playerLayer
            : hitParam.playerLayer;

        if (!IsLayerAllowed(targetLayer, other.gameObject, true))
        {
            if (debugParam.logTrigger)
            {
                Debug.Log(
                    $"{name} Explosion ignored layer " +
                    $"other:{other.name} layer:{LayerMask.LayerToName(other.gameObject.layer)} reason:{reason}"
                );
            }

            return;
        }

        IHitReceiver receiver = FindReceiver(other, out GameObject receiverObject);

        if (receiver == null || receiverObject == null)
            return;

        if (explosionParam.damageSameTargetOnce &&
            explosionHitObjects.Contains(receiverObject))
        {
            return;
        }

        explosionHitObjects.Add(receiverObject);

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        int damage = explosionParam.damage > 0
            ? explosionParam.damage
            : playerDamage;

        HitEventData data = new HitEventData
        {
            attackerObject = gameObject,
            attackerHitbox = gameObject,
            targetObject = receiverObject,
            targetHitbox = targetHitbox != null ? targetHitbox.gameObject : other.gameObject,
            contactPoint = other.ClosestPoint(transform.position),
            payload = new EnemyAttackPayload
            {
                damage = damage
            }
        };

        if (debugParam.logTrigger)
        {
            Debug.Log(
                $"{name} ExplosionHit other:{receiverObject.name} damage:{damage} reason:{reason}"
            );
        }

        receiver.OnHit(data);
    }

    private void HitPlayer(Collider other)
    {
        EndBallEffect();

        IHitReceiver receiver = FindReceiver(other, out GameObject receiverObject);

        if (receiver == null)
            return;

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

        receiver.OnHit(data);

        Finish(BallEndReason.HitPlayer);
    }

    private void HitBoss()
    {
        SetCollisionModeOff();

        if (boss != null)
        {
            boss.ApplyReflectedBallDamage(
                reflectedDamageToBoss,
                reflectedStunValue,
                this.transform.position
            );
        }

        Finish(BallEndReason.HitBoss);
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

    private bool IsColliderBelongsToBoss(Collider other)
    {
        if (other == null)
            return false;

        Transform otherTransform = other.transform;

        // boss参照がある場合：Boss本体またはBossの子ならOK
        if (boss != null)
        {
            if (otherTransform == boss.transform ||
                otherTransform.IsChildOf(boss.transform))
            {
                return true;
            }
        }

        // bossTarget参照がある場合：BossTarget本体または子ならOK
        if (bossTarget != null)
        {
            if (otherTransform == bossTarget ||
                otherTransform.IsChildOf(bossTarget))
            {
                return true;
            }
        }

        // 念のため、当たったCollider側からSoccerBossを探す
        SoccerBoss hitBoss = other.GetComponentInParent<SoccerBoss>();

        if (hitBoss != null)
        {
            if (boss == null)
                return true;

            return hitBoss == boss;
        }

        return false;
    }

    #endregion

    #region Reflect

    private void ReflectByPlayer(
        Transform attacker,
        string reason,
        float powerRate = 0f,
        float powerConstant = 0f)
    {
        reflected = true;
        mode = BallMode.Reflected;

        reflectedTimer = 0f;
        lifeTimer = 0f;
        lifeLimit = reflectParam.reflectedLifeTime;

        SetCollisionModeReflected();

        lastReflectPowerRate = Mathf.Clamp01(powerRate);
        lastReflectPowerConstant = Mathf.Max(0f, powerConstant);

        currentReflectedSpeed = ResolveReflectedSpeed(
            lastReflectPowerRate,
            lastReflectPowerConstant
        );

        if (flyEffectParam.stopOnReflect)
            EndBallEffect();

        reflectedAimPosition = BuildReflectAimPosition(
            attacker,
            lastReflectPowerRate
        );

        hasReflectedAimPosition = true;

        Vector3 dir = reflectedAimPosition - transform.position;
        dir.y += reflectParam.reflectedUpBias;

        if (dir.sqrMagnitude < 0.0001f && attacker != null)
            dir = transform.position - attacker.position;

        if (dir.sqrMagnitude < 0.0001f)
            dir = -velocity;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        velocity = dir * currentReflectedSpeed;

        if (debugParam.logOnHit)
        {
            Debug.Log(
                $"{name} reflected reason:{reason} " +
                $"powerRate:{lastReflectPowerRate:F2} " +
                $"speed:{currentReflectedSpeed:F1} " +
                $"aim:{reflectedAimPosition} " +
                $"selectedAim:{(lastSelectedReflectAimTarget != null ? lastSelectedReflectAimTarget.name : "None")}"
            );
        }
    }

    private float ResolveReflectedSpeed(float powerRate, float powerConstant)
    {
        float speed = reflectParam.reflectedSpeed;

        if (reflectParam.usePowerBasedReflectSpeed)
        {
            speed +=
                powerConstant *
                powerRate *
                reflectParam.powerConstantSpeedMultiplier;

            speed +=
                powerRate *
                reflectParam.powerRateSpeedBonus;
        }

        speed = Mathf.Clamp(
            speed,
            reflectParam.minReflectedSpeed,
            reflectParam.maxReflectedSpeed
        );

        return speed;
    }

    private bool CanReflectNow()
    {
        if (!reflectParam.allowReReflect && reflected)
            return false;

        return mode == BallMode.Arc ||
               mode == BallMode.Straight ||
               mode == BallMode.Landed ||
               (mode == BallMode.Reflected && reflectParam.allowReReflect);
    }

    public void SetReflectAimTargets(Transform[] targets)
    {
        reflectAimTargets = targets;
    }

    private Vector3 BuildReflectAimPosition(
        Transform attacker = null,
        float powerRate = 1f)
    {
        if (TryGetBestReflectAimPoint(attacker, powerRate, out Vector3 selectedAim))
        {
            if (reflectParam.applyRandomRadiusToAimPoint &&
                reflectParam.aimRandomRadius > 0f)
            {
                selectedAim += UnityEngine.Random.insideUnitSphere *
                               reflectParam.aimRandomRadius;
            }

            return selectedAim;
        }

        lastSelectedReflectAimTarget = null;

        Vector3 aim;

        if (reflectParam.reflectAimPoint != null)
            aim = reflectParam.reflectAimPoint.position;
        else if (bossTarget != null)
            aim = bossTarget.position + reflectParam.bossAimOffset;
        else
            aim = transform.position - velocity;

        if (reflectParam.aimRandomRadius > 0f)
            aim += UnityEngine.Random.insideUnitSphere * reflectParam.aimRandomRadius;

        return aim;
    }

    private bool TryGetBestReflectAimPoint(
        Transform attacker,
        float powerRate,
        out Vector3 aim)
    {
        aim = Vector3.zero;
        lastSelectedReflectAimTarget = null;

        if (reflectAimTargets == null || reflectAimTargets.Length == 0)
            return false;

        Vector3 centerDir = GetReflectCenterDirection(attacker);

        if (reflectParam.selectAimPointPlanar)
            centerDir.y = 0f;

        if (centerDir.sqrMagnitude < 0.0001f)
            return false;

        centerDir.Normalize();

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < reflectAimTargets.Length; i++)
        {
            Transform p = reflectAimTargets[i];

            if (p == null)
                continue;

            minY = Mathf.Min(minY, p.position.y);
            maxY = Mathf.Max(maxY, p.position.y);
        }

        if (minY == float.MaxValue)
            return false;

        float heightRange = Mathf.Max(
            reflectParam.minHeightRange,
            maxY - minY
        );

        float heightPowerT = Mathf.InverseLerp(
            reflectParam.minPowerRateForHeight,
            reflectParam.maxPowerRateForHeight,
            Mathf.Clamp01(powerRate)
        );

        Transform bestTarget = null;
        float bestScore = float.MaxValue;
        float bestAngle = float.MaxValue;
        float bestHeightDiff = 0f;

        for (int i = 0; i < reflectAimTargets.Length; i++)
        {
            Transform point = reflectAimTargets[i];

            if (point == null)
                continue;

            Vector3 toPoint = point.position - transform.position;

            if (reflectParam.selectAimPointPlanar)
                toPoint.y = 0f;

            if (toPoint.sqrMagnitude < 0.0001f)
                continue;

            toPoint.Normalize();

            float angle = Vector3.Angle(centerDir, toPoint);
            float angleScore = angle / Mathf.Max(1f, reflectParam.reflectAimSelectAngle);

            float pointHeightT = Mathf.InverseLerp(
                minY,
                minY + heightRange,
                point.position.y
            );

            float heightDiff = Mathf.Abs(pointHeightT - heightPowerT);

            float score = angleScore;

            if (reflectParam.usePowerBasedHeightAim)
                score += heightDiff * reflectParam.heightScoreWeight;

            if (score < bestScore)
            {
                bestScore = score;
                bestAngle = angle;
                bestHeightDiff = heightDiff;
                bestTarget = point;
            }
        }

        if (bestTarget == null)
            return false;

        if (reflectParam.requireAimPointInsideAngle &&
            bestAngle > reflectParam.reflectAimSelectAngle)
        {
            if (reflectParam.logSelectedAimPoint)
            {
                Debug.Log(
                    $"{name} AimPoint rejected. " +
                    $"best:{bestTarget.name} angle:{bestAngle:F1} " +
                    $"limit:{reflectParam.reflectAimSelectAngle:F1}"
                );
            }

            return false;
        }

        aim = bestTarget.position;

        if (reflectParam.usePowerBasedHeightAim &&
            reflectParam.extraHeightByPower > 0f)
        {
            aim.y += heightPowerT * reflectParam.extraHeightByPower;
        }

        lastSelectedReflectAimTarget = bestTarget;

        if (reflectParam.logSelectedAimPoint)
        {
            Debug.Log(
                $"{name} AimPoint selected:{bestTarget.name} " +
                $"angle:{bestAngle:F1} " +
                $"heightDiff:{bestHeightDiff:F2} " +
                $"power:{powerRate:F2} " +
                $"aim:{aim}"
            );
        }

        return true;
    }

    private Vector3 GetReflectCenterDirection(Transform attacker)
    {
        if (attacker != null && attacker.forward.sqrMagnitude > 0.0001f)
            return attacker.forward;

        if (velocity.sqrMagnitude > 0.0001f)
            return -velocity.normalized;

        if (bossTarget != null)
        {
            Vector3 toBoss = bossTarget.position - transform.position;

            if (toBoss.sqrMagnitude > 0.0001f)
                return toBoss.normalized;
        }

        return Vector3.forward;
    }

    private Vector3 GetCurrentReflectAimPosition()
    {
        if (reflectParam.lockAimPointOnReflect && hasReflectedAimPosition)
            return reflectedAimPosition;

        return BuildReflectAimPosition();
    }

    #endregion

    #region Explosion

    private void EnterLanded()
    {
        Vector3 pos = transform.position;
        pos.y = landingPosition.y + landingParam.landedYOffset;
        audioManager.PlaySe("SB_BallLand", pos);

        BeginExplosion(pos, "ArcLanded");
    }

    private void BeginExplosion(Vector3 position, string reason)
    {
        if (!explosionParam.explodeOnImpact)
        {
            EndBallEffect();
            Finish(BallEndReason.LandedTimeout);
            return;
        }

        if (flyEffectParam.stopOnExplosion)
            EndBallEffect();

        mode = BallMode.Exploding;
        velocity = Vector3.zero;
        useBezierArc = false;
        explosionTimer = 0f;
        explosionHitObjects.Clear();

        transform.position = position;

        if (explosionParam.hideModelOnExplosion)
            SetModelVisible(false);

        if (effectPlayer != null)
        {
            if(IsOverHead)
            {
                effectPlayer.PlayAt(4, position);
            }
            else
            {
                effectPlayer.PlayAt(1, position);
            }
            IsOverHead = false;
        }
            

        SetCollisionModeExplosion();

        if (explosionParam.explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(
                explosionParam.explosionVfxPrefab,
                transform.position,
                Quaternion.identity
            );

            if (explosionParam.explosionVfxLifeTime > 0f)
                Destroy(vfx, explosionParam.explosionVfxLifeTime);
        }

        if (enemySpawner != null && reason != "AngerEnter")
            enemySpawner.Spawn(transform.position);

        if (debugParam.logDestroyReason)
            Debug.Log($"{name} BeginExplosion reason:{reason}");
    }

    public void ForceExplodeFromAnger()
    {
        if (finished)
            return;

        if (mode == BallMode.Exploding)
            return;

        BeginExplosion(transform.position, "AngerEnter");
    }

    private void ApplyExplosionDamage()
    {
        LayerMask targetLayer = explosionParam.playerLayer.value != 0
            ? explosionParam.playerLayer
            : hitParam.playerLayer;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionParam.radius,
            targetLayer,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            TryApplyExplosionHit(hits[i], "OverlapSphereFallback");
        }
    }

    private void CheckExplosionOverlapNow()
    {
        GameObject[] objects = colliderParam.explosionCollisionObjects;

        if (objects == null || objects.Length == 0)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];

            if (obj == null)
                continue;

            Collider[] colliders =
                obj.GetComponentsInChildren<Collider>(true);

            for (int c = 0; c < colliders.Length; c++)
            {
                Collider col = colliders[c];

                if (col == null || !col.enabled)
                    continue;

                Collider[] hits = GetOverlapsForCollider(col);

                if (hits == null)
                    continue;

                for (int h = 0; h < hits.Length; h++)
                {
                    Collider other = hits[h];

                    if (other == null)
                        continue;

                    if (IsOwnCollider(other))
                        continue;

                    TryApplyExplosionHit(
                        other,
                        "ExplosionOverlapNow"
                    );
                }
            }
        }
    }

    #endregion

    #region Collider Control

    private void InitializeCollisionObjects()
    {
        SetupCollisionObjectGroup(colliderParam.normalCollisionObjects);
        SetupCollisionObjectGroup(colliderParam.reflectedBossCollisionObjects);
        SetupCollisionObjectGroup(colliderParam.explosionCollisionObjects);
    }

    private void SetupCollisionObjectGroup(GameObject[] objects)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];

            if (obj == null)
                continue;

            Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null)
                    colliders[c].isTrigger = true;
            }
        }
    }

    private void SetCollisionModeNormal()
    {
        SetCollisionObjectGroupActive(
            colliderParam.normalCollisionObjects,
            true
        );

        SetCollisionObjectGroupActive(
            colliderParam.reflectedBossCollisionObjects,
            false
        );

        SetCollisionObjectGroupActive(
            colliderParam.explosionCollisionObjects,
            false
        );

        if (debugParam.logColliderSwitch)
            Debug.Log($"{name} CollisionMode => Normal");
    }

    private void SetCollisionModeReflected()
    {
        bool normalActive =
            !colliderParam.disableNormalCollisionObjectsOnReflect;

        SetCollisionObjectGroupActive(
            colliderParam.normalCollisionObjects,
            normalActive
        );

        SetCollisionObjectGroupActive(
            colliderParam.reflectedBossCollisionObjects,
            reflectParam.useReflectedBossCollider
        );

        SetCollisionObjectGroupActive(
            colliderParam.explosionCollisionObjects,
            false
        );

        if (debugParam.logColliderSwitch)
            Debug.Log($"{name} CollisionMode => Reflected");
    }

    private void SetCollisionModeExplosion()
    {
        SetCollisionObjectGroupActive(
            colliderParam.normalCollisionObjects,
            false
        );

        SetCollisionObjectGroupActive(
            colliderParam.reflectedBossCollisionObjects,
            false
        );

        SetCollisionObjectGroupActive(
            colliderParam.explosionCollisionObjects,
            true
        );

        if (colliderParam.checkExplosionOverlapOnEnable)
            CheckExplosionOverlapNow();

        if (debugParam.logColliderSwitch)
            Debug.Log($"{name} CollisionMode => Explosion");
    }

    private void SetCollisionModeOff()
    {
        SetCollisionObjectGroupActive(
            colliderParam.normalCollisionObjects,
            false
        );

        SetCollisionObjectGroupActive(
            colliderParam.reflectedBossCollisionObjects,
            false
        );

        SetCollisionObjectGroupActive(
            colliderParam.explosionCollisionObjects,
            false
        );
    }

    private void SetCollisionObjectGroupActive(
        GameObject[] objects,
        bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];

            if (obj == null)
                continue;

            // RootのSoccerBall自身を登録してしまうとBall本体ごと消えるので保険
            if (obj == gameObject)
            {
                SetCollidersInObjectActive(obj, active);
                continue;
            }

            obj.SetActive(active);
        }
    }

    private void SetCollidersInObjectActive(
        GameObject obj,
        bool active)
    {
        if (obj == null)
            return;

        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = active;
        }
    }

    private void SetColliderGroupActive(Collider[] colliders, bool active)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = active;
        }
    }

    private int GetExplosionCollisionObjectCount()
    {
        if (colliderParam.explosionCollisionObjects == null)
            return 0;

        int count = 0;

        for (int i = 0; i < colliderParam.explosionCollisionObjects.Length; i++)
        {
            if (colliderParam.explosionCollisionObjects[i] != null)
                count++;
        }

        return count;
    }

    private bool IsOwnCollider(Collider col)
    {
        if (col == null)
            return false;

        return col.transform == transform ||
               col.transform.IsChildOf(transform);
    }

    private bool IsHitboxInObjectGroup(
     Hitbox selfHitbox,
     GameObject[] objects)
    {
        if (selfHitbox == null || objects == null)
            return false;

        GameObject selfObject = selfHitbox.gameObject;
        Transform selfTransform = selfHitbox.transform;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];

            if (obj == null)
                continue;

            Transform objTransform = obj.transform;

            bool sameObject = selfObject == obj;

            bool selfIsChildOfObj =
                selfTransform == objTransform ||
                selfTransform.IsChildOf(objTransform);

            bool objIsChildOfSelf =
                objTransform.IsChildOf(selfTransform);

            if (debugParam.logTrigger)
            {
                Debug.Log(
                    $"{name} HitboxGroupCheck " +
                    $"obj:{obj.name} id:{obj.GetInstanceID()} path:{GetTransformPath(obj.transform)} / " +
                    $"self:{selfObject.name} id:{selfObject.GetInstanceID()} path:{GetTransformPath(selfTransform)} / " +
                    $"same:{sameObject} selfChild:{selfIsChildOfObj} objChild:{objIsChildOfSelf}"
                );
            }

            if (sameObject || selfIsChildOfObj || objIsChildOfSelf)
                return true;
        }

        return false;
    }
    private Collider[] GetOverlapsForCollider(Collider col)
    {
        LayerMask targetLayer = explosionParam.playerLayer.value != 0
            ? explosionParam.playerLayer
            : hitParam.playerLayer;

        if (col is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);

            float maxScale = Mathf.Max(
                Mathf.Abs(sphere.transform.lossyScale.x),
                Mathf.Abs(sphere.transform.lossyScale.y),
                Mathf.Abs(sphere.transform.lossyScale.z)
            );

            float radius = sphere.radius * maxScale;

            return Physics.OverlapSphere(
                center,
                radius,
                targetLayer,
                QueryTriggerInteraction.Collide
            );
        }

        Bounds bounds = col.bounds;

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            col.transform.rotation,
            targetLayer,
            QueryTriggerInteraction.Collide
        );
    }

    private void ResolveCollisionObjectReferences()
    {
        ResolveObjectArrayByName(ref colliderParam.normalCollisionObjects);
        ResolveObjectArrayByName(ref colliderParam.reflectedBossCollisionObjects);
        ResolveObjectArrayByName(ref colliderParam.explosionCollisionObjects);
    }

    private void ResolveObjectArrayByName(ref GameObject[] objects)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject registered = objects[i];

            if (registered == null)
                continue;

            // すでにこのSoccerBall(Clone)の子ならそのままでOK
            if (registered.transform == transform ||
                registered.transform.IsChildOf(transform))
            {
                continue;
            }

            // Prefabや別インスタンスを参照している可能性があるので、
            // 自分の子階層から同じ名前のObjectを探す
            Transform found = FindChildByName(transform, registered.name);

            if (found != null)
            {
                if (debugParam.logTrigger)
                {
                    Debug.Log(
                        $"{name} CollisionObject resolved " +
                        $"{registered.name} -> {GetTransformPath(found)}"
                    );
                }

                objects[i] = found.gameObject;
            }
            else
            {
                Debug.LogWarning(
                    $"{name} CollisionObject resolve failed. " +
                    $"name:{registered.name}"
                );
            }
        }
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            Transform found = FindChildByName(child, targetName);

            if (found != null)
                return found;
        }

        return null;
    }

    private string GetTransformPath(Transform target)
    {
        if (target == null)
            return "null";

        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private bool IsLayerAllowed(
    LayerMask mask,
    GameObject obj,
    bool allowWhenMaskEmpty = true)
    {
        if (obj == null)
            return false;

        if (mask.value == 0)
            return allowWhenMaskEmpty;

        int layerBit = 1 << obj.layer;
        return (layerBit & mask.value) != 0;
    }

    #endregion

    #region Movement And Sweep

    private bool MoveWithSweep(Vector3 nextPosition)
    {
        Vector3 current = transform.position;
        Vector3 move = nextPosition - current;
        float distance = move.magnitude;

        lastSweepStart = current;
        lastSweepEnd = nextPosition;
        hasSweepDebug = true;

        if (!sweepParam.useSweepHit || distance <= 0.0001f)
        {
            transform.position = nextPosition;
            previousPosition = transform.position;
            return false;
        }

        Vector3 dir = move / distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            current,
            sweepParam.sweepRadius,
            dir,
            distance,
            sweepParam.sweepLayer,
            QueryTriggerInteraction.Collide
        );

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;

                if (hitCollider == null)
                    continue;

                if (sweepParam.ignoreSelf && IsOwnCollider(hitCollider))
                    continue;

                bool consumed = TryHandleSweepHit(hitCollider, hits[i].point);

                if (consumed)
                {
                    previousPosition = transform.position;
                    return true;
                }
            }
        }

        transform.position = nextPosition;
        previousPosition = transform.position;
        return false;
    }

    private bool TryHandleSweepHit(Collider other, Vector3 hitPoint)
    {
        if (other == null)
            return false;

        if (localTime < lastHitTime + hitParam.hitCooldown)
            return false;

        int layerBit = 1 << other.gameObject.layer;

        if (CanReflectNow() &&
            (layerBit & reflectParam.reflectLayer.value) != 0)
        {
            lastHitTime = localTime;
            transform.position = hitPoint;
            ReflectByPlayer(other.transform, "Sweep ReflectLayer");
            return true;
        }

        bool canDamagePlayer =
            !reflected &&
            hitParam.damagePlayerBeforeReflect &&
            (
                mode == BallMode.Arc ||
                mode == BallMode.Straight ||
                (mode == BallMode.Landed && landingParam.damagePlayerWhileLanded)
            );

        if (canDamagePlayer &&
            (layerBit & hitParam.playerLayer.value) != 0)
        {
            lastHitTime = localTime;
            transform.position = hitPoint;
            HitPlayer(other);
            return true;
        }

        if (reflected && !reflectParam.useReflectedBossCollider &&
            (layerBit & hitParam.bossLayer.value) != 0)
        {
            lastHitTime = localTime;
            transform.position = hitPoint;
            HitBoss();
            return true;
        }

        if ((layerBit & hitParam.fieldLayer.value) != 0)
        {
            if (!reflected)
            {
                transform.position = hitPoint;
                BeginExplosion(hitPoint, "SweepFieldBeforeReflect");
                return true;
            }

            if (reflected && hitParam.destroyOnFieldAfterReflect)
            {
                transform.position = hitPoint;
                Finish(BallEndReason.HitField);
                return true;
            }
        }

        return false;
    }

    private bool TryExplodeOnLandingPlane(
        Vector3 currentPosition,
        Vector3 nextPosition)
    {
        float landingY = landingPosition.y + landingParam.landedYOffset;

        bool crossedLandingPlane =
            currentPosition.y > landingY &&
            nextPosition.y <= landingY;

        if (!crossedLandingPlane)
            return false;

        float denom = currentPosition.y - nextPosition.y;

        if (Mathf.Abs(denom) < 0.0001f)
            return false;

        float t = (currentPosition.y - landingY) / denom;
        t = Mathf.Clamp01(t);

        Vector3 impactPoint = Vector3.Lerp(currentPosition, nextPosition, t);
        impactPoint.y = landingY;

        BeginExplosion(impactPoint, "StraightLandingPlane");
        return true;
    }

    #endregion

    #region Visual And Effect

    private void SetModelVisible(bool visible)
    {
        if (cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = visible;
        }
    }

    private void PlayBallEffect()
    {
        if (flyeffect != null)
            return;

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        if (effectPlayer == null)
            return;

        if (!TryGetFlyEffectPose(out Vector3 effectPos, out Quaternion effectRot))
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

        IsOverHead = false;

        flyeffect = effectPlayer.PlayAt(
            flyEffectParam.effectIndex,
            effectPos,
            effectRot,
            Vector3.one,
            param
        );
    }
    
    private void PlayBallEffectO()
    {
        if (flyeffect != null)
            return;

        if (effectPlayer == null)
            effectPlayer = GetComponentInChildren<EffectPlayer>(true);

        if (effectPlayer == null)
            return;

        if (!TryGetFlyEffectPose(out Vector3 effectPos, out Quaternion effectRot))
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

        IsOverHead = true;

        flyeffect = effectPlayer.PlayAt(
            3,
            effectPos,
            effectRot,
            Vector3.one,
            param
        );
    }

    private void EndBallEffect()
    {
        if (flyeffect != null)
        {
            flyeffect.StopImmediate();
            flyeffect = null;
        }

        if(aimEffect != null)
        {
            aimEffect.StopImmediate();
            aimEffect = null;
        }
    }

    private void CreateFlyEffectPoint()
    {
        if (flyEffectPoint != null)
            return;

        GameObject point = new GameObject($"{name}_FlyEffectPoint");
        flyEffectPoint = point.transform;
        flyEffectPoint.SetParent(null);
        flyEffectPoint.position = transform.position;
        flyEffectPoint.rotation = transform.rotation;
    }

    private void DestroyFlyEffectPoint()
    {
        if (flyEffectPoint == null)
            return;

        Destroy(flyEffectPoint.gameObject);
        flyEffectPoint = null;
    }

    private void UpdateFlyEffectPoint()
    {
        if (!TryGetFlyEffectPose(out Vector3 effectPos, out Quaternion effectRot))
            return;

        if (flyEffectPoint != null)
            flyEffectPoint.SetPositionAndRotation(effectPos, effectRot);

        if (flyeffect != null)
            flyeffect.transform.SetPositionAndRotation(effectPos, effectRot);
    }

    private bool TryGetFlyEffectPose(
        out Vector3 effectPos,
        out Quaternion effectRot)
    {
        effectPos = transform.position;
        effectRot = transform.rotation;

        Vector3 dir = GetCurrentFlyDirection();

        if (dir.sqrMagnitude < 0.0001f)
            return false;

        dir.Normalize();
        lastEffectDirection = dir;

        effectPos =
            transform.position +
            dir * flyEffectParam.forwardOffset +
            flyEffectParam.worldOffset;

        if (!flyEffectParam.faceMoveDirection)
        {
            effectRot = transform.rotation;
            return true;
        }

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion axisCorrection = GetPrefabForwardAxisCorrection();
        Quaternion extraOffset = Quaternion.Euler(flyEffectParam.rotationOffsetEuler);

        effectRot = look * axisCorrection * extraOffset;

        return true;
    }

    private void UpdateVisualSpin(float dt)
    {
        if (visualSpinParam.modelRoot == null)
            return;

        if (mode == BallMode.Exploding || mode == BallMode.Landed)
            return;

        if (mode == BallMode.Reflected && !visualSpinParam.spinWhileReflected)
            return;

        if (velocity.sqrMagnitude < 0.0001f)
            return;

        Vector3 moveDir = velocity.normalized;
        Vector3 spinAxis = Vector3.Cross(Vector3.up, moveDir);

        if (spinAxis.sqrMagnitude < 0.0001f)
            spinAxis = transform.right;

        spinAxis.Normalize();

        float speed = velocity.magnitude;

        float spinSpeed = Mathf.Clamp(
            speed * visualSpinParam.spinRate,
            visualSpinParam.minSpinSpeed,
            visualSpinParam.maxSpinSpeed
        );

        visualSpinParam.modelRoot.Rotate(
            spinAxis,
            spinSpeed * dt,
            Space.World
        );
    }

    private void FaceVelocity()
    {
        if (velocity.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(velocity.normalized);
    }

    private Vector3 GetCurrentFlyDirection()
    {
        Vector3 dir = velocity;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        if (mode == BallMode.Arc && useBezierArc)
        {
            float currentT = arcDuration <= 0f
                ? 1f
                : Mathf.Clamp01(arcTimer / arcDuration);

            dir = EvaluateQuadraticBezierTangent(
                arcStart,
                arcControl,
                arcEnd,
                currentT
            );

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        if (lastEffectDirection.sqrMagnitude > 0.0001f)
            return lastEffectDirection.normalized;

        return transform.forward;
    }

    private Quaternion GetPrefabForwardAxisCorrection()
    {
        Vector3 localForwardAxis = Vector3.forward;

        switch (flyEffectParam.prefabForwardAxis)
        {
            case EffectForwardAxis.ZPlus:
                localForwardAxis = Vector3.forward;
                break;

            case EffectForwardAxis.ZMinus:
                localForwardAxis = Vector3.back;
                break;

            case EffectForwardAxis.XPlus:
                localForwardAxis = Vector3.right;
                break;

            case EffectForwardAxis.XMinus:
                localForwardAxis = Vector3.left;
                break;

            case EffectForwardAxis.YPlus:
                localForwardAxis = Vector3.up;
                break;

            case EffectForwardAxis.YMinus:
                localForwardAxis = Vector3.down;
                break;
        }

        return Quaternion.FromToRotation(localForwardAxis, Vector3.forward);
    }

    #endregion

    #region Math

    private Vector3 EvaluateQuadraticBezier(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        float t)
    {
        float u = 1f - t;

        return
            u * u * p0 +
            2f * u * t * p1 +
            t * t * p2;
    }

    private Vector3 EvaluateQuadraticBezierTangent(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        float t)
    {
        return
            2f * (1f - t) * (p1 - p0) +
            2f * t * (p2 - p1);
    }

    #endregion

    #region Finish

    private void Finish(BallEndReason reason)
    {
        if (finished)
            return;

        finished = true;

        SetCollisionModeOff();

        if (debugParam.logDestroyReason)
            Debug.Log($"{name} Finish reason:{reason}");


        OnBallFinished?.Invoke(this, reason);

        EndBallEffect();
        DestroyFlyEffectPoint();

        Destroy(gameObject);
    }

    public void ForceFinish()
    {
        Finish(BallEndReason.Manual);
    }

    #endregion

    #region Debug Draw

    private void DrawRuntimeDebugLines()
    {
        if (debugParam == null || !debugParam.drawGizmos)
            return;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Debug.DrawLine(
                transform.position,
                transform.position + velocity.normalized * 2f,
                Color.cyan
            );
        }

        if (hasSweepDebug)
        {
            Debug.DrawLine(
                lastSweepStart,
                lastSweepEnd,
                Color.magenta
            );
        }

        if (mode == BallMode.Reflected && hasReflectedAimPosition)
        {
            Debug.DrawLine(
                transform.position,
                reflectedAimPosition,
                Color.red
            );
        }

        Debug.DrawLine(
            transform.position,
            landingPosition,
            Color.green
        );

        if (debugParam.drawBezierArc && useBezierArc)
            DrawBezierDebugLine();

        DrawFlyEffectDebugLines();
    }

    private void DrawBezierDebugLine()
    {
        int segments = Mathf.Max(4, debugParam.bezierPreviewSegments);

        Vector3 prev = arcStart;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 next = EvaluateQuadraticBezier(arcStart, arcControl, arcEnd, t);

            Debug.DrawLine(prev, next, Color.yellow);
            prev = next;
        }
    }

    private void DrawFlyEffectDebugLines()
    {
        if (flyEffectPoint == null)
            return;

        if (flyeffect != null)
        {
            Vector3 e = flyeffect.transform.position;

            Debug.DrawLine(
                e,
                e + flyeffect.transform.forward * 4.0f,
                Color.magenta
            );
        }

        Vector3 p = flyEffectPoint.position;

        Debug.DrawLine(p, p + flyEffectPoint.forward * 4.0f, Color.blue);
        Debug.DrawLine(p, p + flyEffectPoint.right * 2.0f, Color.red);
        Debug.DrawLine(p, p + flyEffectPoint.up * 2.0f, Color.green);

        Vector3 dir = GetCurrentFlyDirection();

        if (dir.sqrMagnitude > 0.0001f)
        {
            Debug.DrawLine(
                transform.position,
                transform.position + dir.normalized * 4.0f,
                Color.yellow
            );
        }
    }

    private void OnDrawGizmos()
    {
        if (debugParam == null || !debugParam.drawGizmos || !debugParam.drawAlways)
            return;

        DrawBallDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (debugParam == null || !debugParam.drawGizmos)
            return;

        DrawBallDebugGizmos();
    }

    private void DrawBallDebugGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (debugParam.drawVelocity && velocity.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                transform.position,
                transform.position + velocity * debugParam.velocityLineScale
            );
        }

        if (debugParam.drawSweep && hasSweepDebug)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(lastSweepStart, lastSweepEnd);
            Gizmos.DrawWireSphere(lastSweepStart, sweepParam.sweepRadius);
            Gizmos.DrawWireSphere(lastSweepEnd, sweepParam.sweepRadius);
        }

        if (debugParam.drawBossAim)
        {
            Gizmos.color = Color.red;

            Vector3 aim = hasReflectedAimPosition
                ? reflectedAimPosition
                : BuildReflectAimPosition();

            Gizmos.DrawLine(transform.position, aim);
            Gizmos.DrawWireSphere(aim, reflectParam.bossHitDistance);
        }

        if (debugParam.drawLandingTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(landingPosition, 0.35f);
            Gizmos.DrawLine(transform.position, landingPosition);
        }

        if (mode == BallMode.Exploding)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionParam.radius);
        }
    }

    #endregion
}