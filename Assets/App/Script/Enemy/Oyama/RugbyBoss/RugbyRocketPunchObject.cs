using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RugbyRocketPunchObject : MonoBehaviour, IHitReceiver, IHitSource
{
    private enum PunchState
    {
        FlyingToPlayer,
        ReflectedToBoss,
        Impacted
    }

    #region Inspector Parameter Classes

    [System.Serializable]
    private class MovementParam
    {
        [Header("Time Scale")]
        [Tooltip("ONならRugbyBoss側のTimeAgent.TimeScaleを使って移動します。")]
        public bool useOwnerTimeAgent = true;

        [Header("Homing Missile Speed")]
        [Tooltip("プレイヤーへ向かう時の速度です。")]
        public float launchSpeed = 18f;

        [Tooltip("プレイヤー追尾中の旋回速度です。度/秒。大きいほど強く追尾します。")]
        public float homingTurnSpeed = 760f;

        [Tooltip("この距離以下になったら追尾をやめ、その時点の進行方向で直進します。")]
        public float lockDistanceToPlayer = 8f;
        [Header("Homing Missile Shape")]
        [Tooltip("発射直後の上昇フェーズの終わりです。0.2なら全体距離の20%程度まで上昇寄りです。")]
        [Range(0.05f, 0.6f)]
        public float initialRiseEndProgress = 0.22f;

        [Tooltip("射出直後に上へ向ける強さです。")]
        public float initialRiseUpWeight = 3.2f;

        [Tooltip("射出直後に前方向へ残す強さです。0に近いほど真上、1に近いほど前に出ます。")]
        public float initialRiseForwardWeight = 0.35f;

        [Tooltip("中盤で狙う、プレイヤーの手前側オフセット距離です。大きいほどプレイヤー真上ではなく斜めから入ります。")]
        public float approachOffsetDistance = 7.0f;

        [Tooltip("中盤で狙う、プレイヤー手前側の高さです。")]
        public float approachHeight = 3.0f;

        [Tooltip("終端誘導を開始する進行率です。ここからプレイヤー本体方向へ寄せていきます。")]
        [Range(0.3f, 0.95f)]
        public float terminalStartProgress = 0.72f;

        [Tooltip("終端側で下向きに入る補正です。")]
        public float terminalDownBias = 1.2f;

        [Tooltip("プレイヤーのどの高さを狙うかです。胸元や中心を狙うならYを1前後にします。")]
        public Vector3 targetOffset = new Vector3(0f, 1.0f, 0f);

        [Tooltip("プレイヤーとの初期水平距離が短すぎる時に使う最低距離です。")]
        public float minInitialDistanceForProgress = 6f;

        [Header("Reflected")]
        [Tooltip("ジャストタックルで跳ね返った後、反射ターゲットへ向かう速度です。")]
        public float reflectedSpeed = 22f;

        [Tooltip("反射ターゲットへ向かう時の旋回速度です。度/秒。")]
        public float reflectedTurnSpeed = 900f;

        [Header("Life")]
        [Tooltip("最大生存時間です。0以下なら無制限です。独自TimeScaleの影響を受けます。")]
        public float lifeTime = 10f;

        [Tooltip("発射地点からこの距離以上進んだら消滅します。0以下なら無制限です。")]
        public float maxTravelDistance = 120f;
    }

    [System.Serializable]
    private class HitboxParam
    {
        [Header("Player Attack Hitboxes")]
        [Tooltip("プレイヤーに当たる攻撃Hitboxです。通常飛行中だけONにします。Rootではなく子Hitboxを登録してください。")]
        public List<GameObject> playerAttackHitboxes = new List<GameObject>();

        [Tooltip("ONなら攻撃HitboxのTrigger通知に加えて、毎フレームOverlap確認も行います。高速移動や角度差でTriggerが抜けるケースの保険です。")]
        public bool useManualPlayerAttackOverlapCheck = true;

        [Tooltip("手動Overlap確認時に攻撃Collider boundsへ追加する余白です。")]
        public float manualPlayerAttackOverlapPadding = 0.08f;

        [Header("Reflect Receive Hitboxes")]
        [Tooltip("ジャストタックルを受けるHitboxです。通常飛行中だけONにします。Rootではなく子Hitboxを登録してください。")]
        public List<GameObject> reflectReceiveHitboxes = new List<GameObject>();

        [Tooltip("Hitbox登録の子オブジェクトも一致扱いにします。")]
        public bool allowChildHitboxMatch = true;

        [Tooltip("ONならHitboxリストに自分自身のRoot GameObjectが入っていてもSetActive(false)しません。射出直後Disable対策です。")]
        public bool preventRootDisableByHitboxList = true;

        [Header("Just Tackle Priority")]
        [Tooltip("ONならプレイヤー命中を少しだけ遅延し、その間にJustTackleのOnHitが来たら反射を優先します。")]
        public bool deferPlayerHitForJustTackle = true;

        [Tooltip("プレイヤー命中を保留する時間です。生成ロケットパンチでJust判定ログが後から来る場合の対策です。")]
        public float playerHitDeferDuration = 0.10f;

        [Header("Damage")]
        [Tooltip("プレイヤーに当たった時のダメージです。")]
        public int playerDamage = 3;

        [Tooltip("跳ね返ってボスに命中した時の大ダメージです。")]
        public float bossCounterDamage = 80f;

        [Tooltip("跳ね返ってボスに命中した時に加えるダウン値です。Boss側で最大値以上に補正されます。")]
        public float bossCounterDownValue = 9999f;
    }

    [System.Serializable]
    private class CounterParam
    {
        [Header("Counter Target")]
        [Tooltip("反射時に向かうターゲットです。Boss側から指定されます。未指定ならBoss本体Transformを使います。")]
        public Transform counterTarget;

        [Tooltip("反射ターゲット座標に足すオフセットです。")]
        public Vector3 counterTargetOffset = Vector3.zero;

        [Header("Distance Hit")]
        [Tooltip("反射後、この距離以内に入ったらボスへ命中した扱いにします。Colliderではなく距離判定です。")]
        public float bossCounterHitDistance = 2.0f;

        [Tooltip("反射直後、この秒数だけ距離命中を無視します。")]
        public float ignoreCounterHitSecondsAfterReflect = 0.05f;

        [Tooltip("ONなら反射ターゲットの距離判定Gizmoを描画します。")]
        public bool drawCounterHitGizmo = true;
    }

    [System.Serializable]
    private class GroundParam
    {
        [Header("Ground Contact")]
        [Tooltip("ONなら専用子Triggerからの地面接触でロケットパンチを破壊します。")]
        public bool enableGroundContactDestroy = true;

        [Tooltip("地面専用の子Triggerです。HitboxではなくRugbyRocketPunchGroundTriggerを付けた子オブジェクトを登録してください。")]
        public RugbyRocketPunchGroundTrigger groundTrigger;

        [Tooltip("GroundTriggerのColliderを飛行中だけ有効化します。")]
        public bool controlGroundTriggerCollider = true;

        [Tooltip("地面接触として扱うLayerです。Ground専用Layerに絞ってください。")]
        public LayerMask groundLayerMask = ~0;

        [Tooltip("発射直後、この秒数は地面接触を無視します。独自TimeScaleの影響を受けます。")]
        public float ignoreGroundSecondsAfterLaunch = 0.15f;

        [Tooltip("自分自身やBoss配下のColliderを地面接触判定から除外します。")]
        public bool ignoreSelfAndOwnerColliders = true;

        [Tooltip("ONならプレイヤーヒット保留中は地面接触破壊を行いません。")]
        public bool skipGroundDestroyWhilePlayerHitPending = true;

        [Tooltip("ONなら地面接触時のログを出します。")]
        public bool logGroundContact = true;
    }

    [System.Serializable]
    private class VisualParam
    {
        [Header("Visual")]
        [Tooltip("ONなら常に進行方向へ向けます。")]
        public bool faceMoveDirection = true;

        [Tooltip("進行方向へ向ける時の上方向です。基本はVector3.upです。")]
        public Vector3 upAxis = Vector3.up;
    }

    [System.Serializable]
    private class DebugParam
    {
        [Header("Log")]
        [Tooltip("ONなら状態変化ログを出します。")]
        public bool logState = true;

        [Tooltip("ONならヒット処理ログを出します。")]
        public bool logHit = true;

        [Tooltip("ONならジャストタックル反射ログを出します。")]
        public bool logReflect = true;

        [Tooltip("ONならOnHitが拒否された理由をログに出します。")]
        public bool logHitRejectReason = true;

        [Tooltip("ONならHitbox設定に問題がありそうな時に警告ログを出します。")]
        public bool logHitboxWarning = true;

        [Tooltip("ONならTimeAgent由来の独自TimeScaleログを出します。")]
        public bool logTimeScale = false;

        [Tooltip("ONならホーミング中の距離、進行率、方向などを詳細ログに出します。")]
        public bool logHomingDetail = false;

        [Tooltip("ONなら反射後、カウンターターゲットとの距離判定ログを出します。")]
        public bool logCounterDistance = true;

        [Header("Trajectory Debug")]
        [Tooltip("ONならSceneビューに予測軌道を描画します。")]
        public bool drawTrajectoryGizmos = true;

        [Tooltip("予測軌道の分割数です。")]
        [Range(4, 96)]
        public int trajectorySegments = 32;

        [Tooltip("予測軌道の1ステップ秒数です。独自TimeScale適用前のローカルシミュレーション値です。")]
        public float predictionStep = 0.08f;

        [Tooltip("ロック後の直進方向を描画する長さです。")]
        public float lockedDirectionDrawLength = 6f;
    }

    #endregion

    #region Inspector Fields

    [Header("Movement")]
    [SerializeField] private MovementParam movementParam = new MovementParam();

    [Header("Hitbox")]
    [SerializeField] private HitboxParam hitboxParam = new HitboxParam();

    [Header("Counter")]
    [SerializeField] private CounterParam counterParam = new CounterParam();

    [Header("Ground")]
    [SerializeField] private GroundParam groundParam = new GroundParam();

    [Header("Visual")]
    [SerializeField] private VisualParam visualParam = new VisualParam();

    [Header("Debug")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    #endregion

    #region Runtime Fields

    private RugbyBoss ownerBoss;
    private Transform playerTarget;
    private Transform counterTargetFromBoss;
    private Transform ownerBossTransform;
    private TimeAgent ownerTimeAgent;
    private EffectPlayer effectPlayer;

    private PunchState state = PunchState.Impacted;
    private Vector3 moveDirection;
    private Vector3 startPosition;
    private Vector3 previousPosition;
    private Vector3 initialHorizontalDirection;
    private float initialHorizontalDistance;
    private float lastHomingProgress;
    private float lifeTimer;
    private float reflectedTimer;
    private bool playerTrackingLocked;
    private bool initialized;
    private Collider[] selfColliders;
    private Collider[] ownerColliders;

    private bool pendingPlayerHit;
    private float pendingPlayerHitTimer;
    private Hitbox pendingSelfHitbox;
    private Collider pendingOtherCollider;

    #endregion

    #region Public Properties

    public bool IsActive => initialized && state != PunchState.Impacted;
    public bool IsReflected => state == PunchState.ReflectedToBoss;

    private float TimeScale
    {
        get
        {
            if (!movementParam.useOwnerTimeAgent)
                return 1f;

            return ownerTimeAgent != null ? ownerTimeAgent.TimeScale : 1f;
        }
    }

    private float ScaledDeltaTime => Time.deltaTime * TimeScale;

    #endregion

    #region Initialize / Update

    public void Initialize(RugbyBoss owner, Transform player, Transform counterTarget, Vector3 initialDirection)
    {
        gameObject.SetActive(true);
        enabled = true;

        ownerBoss = owner;
        playerTarget = player;
        counterTargetFromBoss = counterTarget;
        ownerBossTransform = ownerBoss != null ? ownerBoss.transform : null;
        counterParam.counterTarget = counterTargetFromBoss != null ? counterTargetFromBoss : ownerBossTransform;
        ownerTimeAgent = ownerBoss != null ? ownerBoss.GetComponent<TimeAgent>() : null;
        effectPlayer = GetComponent<EffectPlayer>();

        startPosition = transform.position;
        previousPosition = transform.position;
        lifeTimer = 0f;
        reflectedTimer = 0f;
        lastHomingProgress = 0f;
        playerTrackingLocked = false;
        pendingPlayerHit = false;
        pendingPlayerHitTimer = 0f;
        pendingSelfHitbox = null;
        pendingOtherCollider = null;
        initialized = true;
        state = PunchState.FlyingToPlayer;

        selfColliders = GetComponentsInChildren<Collider>(true);
        ownerColliders = ownerBoss != null ? ownerBoss.GetComponentsInChildren<Collider>(true) : null;

        if (initialDirection.sqrMagnitude < 0.0001f)
            initialDirection = transform.forward;
        if (initialDirection.sqrMagnitude < 0.0001f)
            initialDirection = Vector3.forward;

        moveDirection = initialDirection.normalized;

        initialHorizontalDirection = moveDirection;
        initialHorizontalDirection.y = 0f;
        if (initialHorizontalDirection.sqrMagnitude < 0.0001f)
        {
            initialHorizontalDirection = GetCurrentTargetPosition() - transform.position;
            initialHorizontalDirection.y = 0f;
        }
        if (initialHorizontalDirection.sqrMagnitude < 0.0001f)
            initialHorizontalDirection = Vector3.forward;
        initialHorizontalDirection.Normalize();

        initialHorizontalDistance = Mathf.Max(
            movementParam.minInitialDistanceForProgress,
            GetXZDistanceBetween(transform.position, GetCurrentTargetPosition())
        );

        InitializeGroundTrigger();
        ApplyFlyingHitboxMode();
        FaceDirection(moveDirection);

        if (debugParam.logState)
        {
            Debug.Log(
                $"{name} RocketPunch Initialize " +
                $"dir:{moveDirection} start:{startPosition} " +
                $"initialDistance:{initialHorizontalDistance:F2} timeScale:{TimeScale:F2} " +
                $"target:{GetCurrentTargetPosition()} counterTarget:{GetCounterTargetPosition()}",
                this
            );
        }
    }

    public void SetCounterTarget(Transform target)
    {
        counterTargetFromBoss = target;
        counterParam.counterTarget = target != null ? target : ownerBossTransform;
    }

    private void Update()
    {
        if (!initialized || state == PunchState.Impacted)
            return;

        float dt = ScaledDeltaTime;
        if (dt <= 0f)
            return;

        lifeTimer += dt;

        if (state == PunchState.ReflectedToBoss)
            reflectedTimer += dt;

        if (debugParam.logTimeScale)
            Debug.Log($"{name} RocketPunch TimeScale:{TimeScale:F2} dt:{dt:F4}", this);

        if (movementParam.lifeTime > 0f && lifeTimer >= movementParam.lifeTime)
        {
            Impact(false, "LifeTime");
            return;
        }

        if (movementParam.maxTravelDistance > 0f && Vector3.Distance(startPosition, transform.position) >= movementParam.maxTravelDistance)
        {
            Impact(false, "MaxTravelDistance");
            return;
        }

        switch (state)
        {
            case PunchState.FlyingToPlayer:
                UpdateFlyingToPlayer(dt);
                break;

            case PunchState.ReflectedToBoss:
                UpdateReflectedToBoss(dt);
                break;
        }

        UpdatePendingPlayerHit(dt);
    }

    #endregion

    #region Movement

    private void UpdateFlyingToPlayer(float dt)
    {
        if (!playerTrackingLocked)
        {
            float distance = GetXZDistanceBetween(transform.position, GetCurrentTargetPosition());
            bool lockByDistance = distance <= Mathf.Max(0f, movementParam.lockDistanceToPlayer);

            if (lockByDistance)
            {
                playerTrackingLocked = true;

                if (debugParam.logState)
                {
                    Debug.Log(
                        $"{name} RocketPunch homing locked by distance " +
                        $"distance:{distance:F2} " +
                        $"lockDistance:{movementParam.lockDistanceToPlayer:F2} " +
                        $"progress:{lastHomingProgress:F2} " +
                        $"dir:{moveDirection}",
                        this
                    );
                }
            }
            else
            {
                Vector3 desired = GetHomingMissileDesiredDirection(out lastHomingProgress);

                moveDirection = Vector3.RotateTowards(
                    moveDirection,
                    desired,
                    Mathf.Max(0f, movementParam.homingTurnSpeed) * Mathf.Deg2Rad * dt,
                    0f
                );

                if (moveDirection.sqrMagnitude > 0.0001f)
                    moveDirection.Normalize();

                if (debugParam.logHomingDetail)
                {
                    Debug.Log(
                        $"{name} RocketPunch homing " +
                        $"distance:{distance:F2} " +
                        $"lockDistance:{movementParam.lockDistanceToPlayer:F2} " +
                        $"progress:{lastHomingProgress:F2} " +
                        $"desired:{desired} " +
                        $"dir:{moveDirection} " +
                        $"target:{GetCurrentTargetPosition()}",
                        this
                    );
                }
            }
        }

        MoveForward(movementParam.launchSpeed, dt);
        TryManualPlayerAttackOverlap();
    }

    private void UpdateReflectedToBoss(float dt)
    {
        Vector3 desired = GetDirectionToCounterTarget();

        moveDirection = Vector3.RotateTowards(
            moveDirection,
            desired,
            Mathf.Max(0f, movementParam.reflectedTurnSpeed) * Mathf.Deg2Rad * dt,
            0f
        );

        if (moveDirection.sqrMagnitude > 0.0001f)
            moveDirection.Normalize();

        MoveForward(movementParam.reflectedSpeed, dt);
        TryApplyCounterHitByDistance();
    }

    private Vector3 GetHomingMissileDesiredDirection(out float progress)
    {
        Vector3 target = GetCurrentTargetPosition();
        Vector3 toTarget = target - transform.position;
        Vector3 horizontal = toTarget;
        horizontal.y = 0f;

        float remainDistance = horizontal.magnitude;
        progress = Mathf.Clamp01(1f - remainDistance / Mathf.Max(0.01f, initialHorizontalDistance));

        if (horizontal.sqrMagnitude < 0.0001f)
            horizontal = initialHorizontalDirection;

        if (horizontal.sqrMagnitude < 0.0001f)
            horizontal = Vector3.forward;

        horizontal.Normalize();

        Vector3 desired;
        float riseEnd = Mathf.Clamp(movementParam.initialRiseEndProgress, 0.05f, 0.6f);
        float terminalStart = Mathf.Clamp(movementParam.terminalStartProgress, riseEnd + 0.05f, 0.95f);

        if (progress < riseEnd)
        {
            float t = progress / riseEnd;
            Vector3 riseDirection = initialHorizontalDirection * movementParam.initialRiseForwardWeight + Vector3.up * movementParam.initialRiseUpWeight;
            Vector3 approachPointEarly = target - horizontal * movementParam.approachOffsetDistance + Vector3.up * movementParam.approachHeight;
            Vector3 approachDirection = approachPointEarly - transform.position;

            if (approachDirection.sqrMagnitude < 0.0001f)
                approachDirection = horizontal;

            desired = Vector3.Slerp(riseDirection.normalized, approachDirection.normalized, t * 0.55f);
        }
        else
        {
            float terminalT = Mathf.InverseLerp(terminalStart, 1f, progress);
            float approachDistance = Mathf.Lerp(movementParam.approachOffsetDistance, 0f, terminalT);
            float approachHeight = Mathf.Lerp(movementParam.approachHeight, 0f, terminalT);

            Vector3 approachPoint = target - horizontal * approachDistance + Vector3.up * approachHeight;
            desired = approachPoint - transform.position;

            if (terminalT > 0f)
                desired += Vector3.down * movementParam.terminalDownBias * terminalT;
        }

        if (desired.sqrMagnitude < 0.0001f)
            desired = moveDirection;

        return desired.normalized;
    }

    private Vector3 GetCurrentTargetPosition()
    {
        if (playerTarget != null)
            return playerTarget.position + movementParam.targetOffset;

        return transform.position + moveDirection * 10f;
    }

    private Vector3 GetCounterTargetPosition()
    {
        if (counterParam.counterTarget != null)
            return counterParam.counterTarget.position + counterParam.counterTargetOffset;

        if (ownerBossTransform != null)
            return ownerBossTransform.position + counterParam.counterTargetOffset;

        return transform.position - moveDirection * 10f;
    }

    private Vector3 GetDirectionToCounterTarget()
    {
        Vector3 target = GetCounterTargetPosition();
        Vector3 direction = target - transform.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = -transform.forward;

        return direction.normalized;
    }

    private void MoveForward(float speed, float dt)
    {
        Vector3 delta = moveDirection * Mathf.Max(0f, speed) * dt;
        MoveByDelta(delta);
    }

    private void MoveByDelta(Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.000001f)
            return;

        previousPosition = transform.position;
        transform.position += delta;

        Vector3 newDirection = transform.position - previousPosition;
        if (newDirection.sqrMagnitude > 0.000001f)
            moveDirection = newDirection.normalized;

        FaceDirection(moveDirection);
    }

    #endregion

    #region Hit / Reflect / Counter
    private void TryManualPlayerAttackOverlap()
    {
        if (!hitboxParam.useManualPlayerAttackOverlapCheck)
            return;

        if (state != PunchState.FlyingToPlayer)
            return;

        if (pendingPlayerHit)
            return;

        if (hitboxParam.playerAttackHitboxes == null)
            return;

        float padding = Mathf.Max(0f, hitboxParam.manualPlayerAttackOverlapPadding);

        for (int i = 0; i < hitboxParam.playerAttackHitboxes.Count; i++)
        {
            GameObject hitboxObject = hitboxParam.playerAttackHitboxes[i];
            if (hitboxObject == null || !hitboxObject.activeInHierarchy)
                continue;

            Hitbox selfHitbox = hitboxObject.GetComponent<Hitbox>();
            if (selfHitbox == null)
                selfHitbox = hitboxObject.GetComponentInChildren<Hitbox>(true);
            if (selfHitbox == null)
                selfHitbox = hitboxObject.GetComponentInParent<Hitbox>();

            if (selfHitbox == null)
                continue;

            Collider[] attackColliders = hitboxObject.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < attackColliders.Length; c++)
            {
                Collider attackCollider = attackColliders[c];
                if (attackCollider == null || !attackCollider.enabled)
                    continue;

                Bounds bounds = attackCollider.bounds;
                float radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) + padding;
                Vector3 center = bounds.center;

                Collider[] overlaps = Physics.OverlapSphere(center, Mathf.Max(0.01f, radius), ~0, QueryTriggerInteraction.Collide);
                for (int h = 0; h < overlaps.Length; h++)
                {
                    Collider other = overlaps[h];
                    if (other == null)
                        continue;

                    if (IsSelfOrOwnerCollider(other))
                        continue;

                    Hitbox targetHitbox = other.GetComponent<Hitbox>();
                    if (targetHitbox == null)
                        targetHitbox = other.GetComponentInParent<Hitbox>();

                    if (targetHitbox == null || targetHitbox.receiver == null)
                        continue;

                    if (hitboxParam.deferPlayerHitForJustTackle)
                        BeginPendingPlayerHit(selfHitbox, other);
                    else
                        TrySendPlayerDamage(selfHitbox, other);

                    if (debugParam.logHit)
                        Debug.Log($"{name} RocketPunch manual attack overlap target:{targetHitbox.name} collider:{other.name}", this);

                    return;
                }
            }
        }
    }

    private bool IsSelfOrOwnerCollider(Collider target)
    {
        if (target == null)
            return false;

        if (selfColliders != null)
        {
            for (int i = 0; i < selfColliders.Length; i++)
            {
                if (selfColliders[i] == target)
                    return true;
            }
        }

        if (ownerColliders != null)
        {
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                if (ownerColliders[i] == target)
                    return true;
            }
        }

        return false;
    }
    private void InitializeGroundTrigger()
    {
        if (groundParam.groundTrigger == null)
            groundParam.groundTrigger = GetComponentInChildren<RugbyRocketPunchGroundTrigger>(true);

        if (groundParam.groundTrigger == null)
            return;

        groundParam.groundTrigger.Initialize(this);
        SetGroundTriggerActive(state == PunchState.FlyingToPlayer);
    }

    private void SetGroundTriggerActive(bool active)
    {
        if (groundParam.groundTrigger == null)
            return;

        bool finalActive = active && groundParam.enableGroundContactDestroy && state == PunchState.FlyingToPlayer;
        groundParam.groundTrigger.SetTriggerActive(finalActive, groundParam.controlGroundTriggerCollider);
    }

    public void NotifyGroundTriggerHit(Collider other)
    {
        if (!initialized || state == PunchState.Impacted)
            return;
        if (!groundParam.enableGroundContactDestroy)
            return;
        if (state != PunchState.FlyingToPlayer)
            return;
        if (other == null)
            return;
        if (lifeTimer < Mathf.Max(0f, groundParam.ignoreGroundSecondsAfterLaunch))
            return;
        if (!IsLayerIncluded(groundParam.groundLayerMask, other.gameObject.layer))
            return;
        if (groundParam.ignoreSelfAndOwnerColliders && IsSelfOrOwnerCollider(other))
            return;
        if (groundParam.skipGroundDestroyWhilePlayerHitPending && pendingPlayerHit)
            return;

        if (groundParam.logGroundContact || debugParam.logState)
            Debug.Log($"{name} RocketPunch GroundContact Impact collider:{other.name} layer:{LayerMask.LayerToName(other.gameObject.layer)}", this);

        Impact(false, "GroundContact");
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void TryApplyCounterHitByDistance()
    {
        if (state != PunchState.ReflectedToBoss)
            return;

        if (ownerBoss == null)
            return;

        if (reflectedTimer < Mathf.Max(0f, counterParam.ignoreCounterHitSecondsAfterReflect))
            return;

        float distance = Vector3.Distance(transform.position, GetCounterTargetPosition());

        if (debugParam.logCounterDistance)
        {
            Debug.Log(
                $"{name} RocketPunch counter distance:{distance:F2} required:{counterParam.bossCounterHitDistance:F2} target:{GetCounterTargetPosition()}",
                this
            );
        }

        if (distance > Mathf.Max(0.01f, counterParam.bossCounterHitDistance))
            return;

        ownerBoss.ReceiveRocketPunchCounterHit(
            hitboxParam.bossCounterDamage,
            hitboxParam.bossCounterDownValue,
            this
        );

        Impact(true, "BossCounterDistanceHit");
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (!initialized || state == PunchState.Impacted)
            return;
        if (selfHitbox == null || other == null)
            return;

        GameObject selfHitboxObject = selfHitbox.gameObject;

        if (state == PunchState.FlyingToPlayer && ContainsHitbox(hitboxParam.playerAttackHitboxes, selfHitboxObject))
        {
            if (hitboxParam.deferPlayerHitForJustTackle)
            {
                BeginPendingPlayerHit(selfHitbox, other);
            }
            else
            {
                TrySendPlayerDamage(selfHitbox, other);
            }
        }
    }

    private void BeginPendingPlayerHit(Hitbox selfHitbox, Collider other)
    {
        if (pendingPlayerHit)
            return;

        pendingPlayerHit = true;
        pendingPlayerHitTimer = 0f;
        pendingSelfHitbox = selfHitbox;
        pendingOtherCollider = other;

        if (debugParam.logHit)
            Debug.Log($"{name} RocketPunch player hit pending for JustTackle window:{hitboxParam.playerHitDeferDuration:F3}", this);
    }

    private void UpdatePendingPlayerHit(float dt)
    {
        if (!pendingPlayerHit)
            return;

        if (state != PunchState.FlyingToPlayer)
        {
            ClearPendingPlayerHit();
            return;
        }

        pendingPlayerHitTimer += dt;

        if (pendingPlayerHitTimer < Mathf.Max(0f, hitboxParam.playerHitDeferDuration))
            return;

        Hitbox hitbox = pendingSelfHitbox;
        Collider other = pendingOtherCollider;
        ClearPendingPlayerHit();

        if (hitbox == null || other == null)
            return;

        TrySendPlayerDamage(hitbox, other);
    }

    private void ClearPendingPlayerHit()
    {
        pendingPlayerHit = false;
        pendingPlayerHitTimer = 0f;
        pendingSelfHitbox = null;
        pendingOtherCollider = null;
    }

    private void TrySendPlayerDamage(Hitbox selfHitbox, Collider other)
    {
        if (state != PunchState.FlyingToPlayer)
            return;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();
        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();
        if (targetHitbox == null || targetHitbox.receiver == null)
            return;

        IHitReceiver receiver = targetHitbox.receiver;
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
                damage = hitboxParam.playerDamage
            }
        };

        if (debugParam.logHit)
            Debug.Log($"{name} RocketPunch hit player target:{receiverMono.name} damage:{hitboxParam.playerDamage}", this);

        receiver.OnHit(data);
        Impact(false, "PlayerHit");
    }

    public void OnHit(HitEventData data)
    {
        if (!initialized || state == PunchState.Impacted)
        {
            LogHitReject("not initialized or already impacted", data);
            return;
        }

        if (!(data.payload is BlowPayload blow))
        {
            LogHitReject("payload is not BlowPayload", data);
            return;
        }

        if (!IsJustTackle(blow))
        {
            LogHitReject("BlowPayload is not JustTackle", data);
            return;
        }

        if (!ContainsHitbox(hitboxParam.reflectReceiveHitboxes, data.targetHitbox))
        {
            LogHitReject("targetHitbox is not registered reflectReceiveHitbox", data);
            return;
        }

        ClearPendingPlayerHit();
        ReflectToCounterTarget(GetDirectionToCounterTarget());
    }

    private void LogHitReject(string reason, HitEventData data)
    {
        if (!debugParam.logHitRejectReason)
            return;

        string attackerObjectName = data.attackerObject != null ? data.attackerObject.name : "null";
        string attackerHitboxName = data.attackerHitbox != null ? data.attackerHitbox.name : "null";
        string targetObjectName = data.targetObject != null ? data.targetObject.name : "null";
        string targetHitboxName = data.targetHitbox != null ? data.targetHitbox.name : "null";
        string payloadName = data.payload != null ? data.payload.GetType().Name : "null";

        Debug.LogWarning(
            $"{name} RocketPunch OnHit rejected " +
            $"reason:{reason} state:{state} " +
            $"attackerObject:{attackerObjectName} attackerHitbox:{attackerHitboxName} " +
            $"targetObject:{targetObjectName} targetHitbox:{targetHitboxName} payload:{payloadName}",
            this
        );
    }

    private void ReflectToCounterTarget(Vector3 reflectedDirection)
    {
        if (state == PunchState.Impacted)
            return;

        if (reflectedDirection.sqrMagnitude < 0.0001f)
            reflectedDirection = GetDirectionToCounterTarget();
        if (reflectedDirection.sqrMagnitude < 0.0001f)
            reflectedDirection = -moveDirection;

        moveDirection = reflectedDirection.normalized;
        state = PunchState.ReflectedToBoss;
        reflectedTimer = 0f;
        playerTrackingLocked = true;
        ClearPendingPlayerHit();
        ApplyReflectedHitboxMode();
        FaceDirection(moveDirection);

        if (debugParam.logReflect)
            Debug.Log($"{name} RocketPunch reflected to counterTarget dir:{moveDirection} target:{GetCounterTargetPosition()}", this);
    }

    private bool IsJustTackle(BlowPayload blow)
    {
        return blow.tackleType == TackleType.JustNormal ||
               blow.tackleType == TackleType.JustCharge ||
               blow.tackleType.ToString().Contains("Just");
    }

    public void ForceDestroyProjectile(string reason = "ForceDestroy")
    {
        if (state == PunchState.Impacted)
            return;

        Impact(false, reason);
    }

    private void Impact(bool reflectedBossHit, string reason)
    {
        if (state == PunchState.Impacted)
            return;

        state = PunchState.Impacted;
        ClearPendingPlayerHit();
        ApplyAllHitboxesOff();

        if (ownerBoss != null)
            ownerBoss.NotifyRocketPunchImpact(this, reflectedBossHit, reason);

        if (debugParam.logState)
            Debug.Log($"{name} RocketPunch Impact reason:{reason} reflectedBossHit:{reflectedBossHit}", this);

        effectPlayer.PlayAt(0, transform.position + transform.forward * 10.0f);

        AudioManager.Instance.PlaySe("RB_RPExplosion");

        Destroy(gameObject);
    }

    #endregion

    #region Hitbox Utility

    private void ApplyFlyingHitboxMode()
    {
        SetHitboxListActive(hitboxParam.playerAttackHitboxes, true);
        SetHitboxListActive(hitboxParam.reflectReceiveHitboxes, true);
        SetGroundTriggerActive(true);
    }

    private void ApplyReflectedHitboxMode()
    {
        SetHitboxListActive(hitboxParam.playerAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.reflectReceiveHitboxes, false);
        SetGroundTriggerActive(false);
    }

    private void ApplyAllHitboxesOff()
    {
        SetHitboxListActive(hitboxParam.playerAttackHitboxes, false);
        SetHitboxListActive(hitboxParam.reflectReceiveHitboxes, false);
        SetGroundTriggerActive(false);
    }

    private void SetHitboxListActive(List<GameObject> hitboxes, bool active)
    {
        if (hitboxes == null)
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject hitbox = hitboxes[i];
            if (hitbox == null)
                continue;

            if (hitboxParam.preventRootDisableByHitboxList && hitbox == gameObject && !active)
            {
                if (debugParam.logHitboxWarning)
                    Debug.LogWarning($"{name} Hitbox list contains projectile root. Root SetActive(false) skipped. 子Hitboxを登録してください。", this);
                continue;
            }

            if (hitbox.activeSelf == active)
                continue;

            hitbox.SetActive(active);
        }
    }

    private bool ContainsHitbox(List<GameObject> hitboxes, GameObject target)
    {
        if (hitboxes == null || target == null)
            return false;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            GameObject root = hitboxes[i];
            if (root == null)
                continue;
            if (target == root)
                return true;
            if (!hitboxParam.allowChildHitboxMatch)
                continue;
            if (target.transform.IsChildOf(root.transform))
                return true;
            if (root.transform.IsChildOf(target.transform))
                return true;
        }

        return false;
    }

    #endregion

    #region Utility / Gizmos

    private float GetXZDistanceBetween(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void FaceDirection(Vector3 direction)
    {
        if (!visualParam.faceMoveDirection)
            return;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = visualParam.upAxis.sqrMagnitude > 0.0001f ? visualParam.upAxis.normalized : Vector3.up;
        transform.rotation = Quaternion.LookRotation(direction.normalized, up);
    }

    private void OnDrawGizmosSelected()
    {
        if (debugParam == null)
            return;

        if (debugParam.drawTrajectoryGizmos)
            DrawTrajectoryPredictionGizmo();

        if (counterParam.drawCounterHitGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(GetCounterTargetPosition(), Mathf.Max(0.01f, counterParam.bossCounterHitDistance));
        }

        if (groundParam.groundTrigger != null)
            groundParam.groundTrigger.DrawDebugGizmos();
    }

    private void DrawTrajectoryPredictionGizmo()
    {
        int segments = Mathf.Max(4, debugParam.trajectorySegments);
        float step = Mathf.Max(0.01f, debugParam.predictionStep);

        Vector3 simPosition = transform.position;
        Vector3 simDirection = Application.isPlaying && initialized ? moveDirection : transform.forward;
        if (simDirection.sqrMagnitude < 0.0001f)
            simDirection = Vector3.forward;
        simDirection.Normalize();

        Vector3 simInitialHorizontal = simDirection;
        simInitialHorizontal.y = 0f;
        if (simInitialHorizontal.sqrMagnitude < 0.0001f)
            simInitialHorizontal = Vector3.forward;
        simInitialHorizontal.Normalize();

        Vector3 simTarget = playerTarget != null ? playerTarget.position + movementParam.targetOffset : simPosition + simDirection * 10f;
        float simInitialDistance = Mathf.Max(movementParam.minInitialDistanceForProgress, GetXZDistanceBetween(simPosition, simTarget));
        float simLife = Application.isPlaying && initialized ? lifeTimer : 0f;
        bool simLocked = Application.isPlaying && initialized && playerTrackingLocked;
        float simProgress = Application.isPlaying && initialized ? lastHomingProgress : 0f;

        Gizmos.color = simLocked ? Color.yellow : Color.cyan;
        Vector3 prev = simPosition;

        for (int i = 0; i < segments; i++)
        {
            if (!simLocked)
            {
                simTarget = playerTarget != null ? playerTarget.position + movementParam.targetOffset : simPosition + simDirection * 10f;
                float remain = GetXZDistanceBetween(simPosition, simTarget);
                simProgress = Mathf.Clamp01(1f - remain / Mathf.Max(0.01f, simInitialDistance));

                Vector3 horizontal = simTarget - simPosition;
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < 0.0001f)
                    horizontal = simInitialHorizontal;
                if (horizontal.sqrMagnitude < 0.0001f)
                    horizontal = Vector3.forward;
                horizontal.Normalize();

                float riseEnd = Mathf.Clamp(movementParam.initialRiseEndProgress, 0.05f, 0.6f);
                float terminalStart = Mathf.Clamp(movementParam.terminalStartProgress, riseEnd + 0.05f, 0.95f);
                Vector3 desired;

                if (simProgress < riseEnd)
                {
                    float t = simProgress / riseEnd;
                    Vector3 riseDirection = simInitialHorizontal * movementParam.initialRiseForwardWeight + Vector3.up * movementParam.initialRiseUpWeight;
                    Vector3 approachPointEarly = simTarget - horizontal * movementParam.approachOffsetDistance + Vector3.up * movementParam.approachHeight;
                    Vector3 approachDirection = approachPointEarly - simPosition;
                    if (approachDirection.sqrMagnitude < 0.0001f)
                        approachDirection = horizontal;
                    desired = Vector3.Slerp(riseDirection.normalized, approachDirection.normalized, t * 0.55f);
                }
                else
                {
                    float terminalT = Mathf.InverseLerp(terminalStart, 1f, simProgress);
                    float approachDistance = Mathf.Lerp(movementParam.approachOffsetDistance, 0f, terminalT);
                    float approachHeight = Mathf.Lerp(movementParam.approachHeight, 0f, terminalT);
                    Vector3 approachPoint = simTarget - horizontal * approachDistance + Vector3.up * approachHeight;
                    desired = approachPoint - simPosition;
                    if (terminalT > 0f)
                        desired += Vector3.down * movementParam.terminalDownBias * terminalT;
                }

                if (desired.sqrMagnitude > 0.0001f)
                {
                    simDirection = Vector3.RotateTowards(
                        simDirection,
                        desired.normalized,
                        Mathf.Max(0f, movementParam.homingTurnSpeed) * Mathf.Deg2Rad * step,
                        0f
                    ).normalized;
                }

                bool lockByDistance = remain <= Mathf.Max(0f, movementParam.lockDistanceToPlayer);
                simLocked = lockByDistance;
            }

            simPosition += simDirection * Mathf.Max(0f, movementParam.launchSpeed) * step;
            Gizmos.DrawLine(prev, simPosition);
            prev = simPosition;
            simLife += step;
        }

        Gizmos.color = Color.red;
        if (playerTarget != null)
            Gizmos.DrawWireSphere(playerTarget.position + movementParam.targetOffset, 0.35f);

        if (Application.isPlaying && initialized && playerTrackingLocked)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + moveDirection.normalized * Mathf.Max(0f, debugParam.lockedDirectionDrawLength));
        }
    }

    #endregion
}
