using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TimeAgent))]
public class MoveBlock : MonoBehaviour
{
    #region Enums

    public enum MoveRouteMode
    {
        // 最後のポイントまで行ったら最初のポイントへ戻る
        Loop,

        // 最後まで行ったら逆順に戻る
        PingPong
    }

    public enum MoveStartMode
    {
        // Blockの現在位置から最初の目的地へ移動開始する
        KeepCurrentPosition,

        // 開始時にBlockを最初のポイント位置へ移動する
        SnapToFirstPoint
    }

    public enum MoveUpdateTiming
    {
        // 通常Updateで動かす
        Update,

        // FixedUpdateで動かす。Rigidbody MovePositionと相性が良い
        FixedUpdate,

        // LateUpdateで動かす。PlayerController.Update後に動かしたい場合に使う
        LateUpdate
    }

    public enum PassengerMoveMode
    {
        // CharacterControllerならMove、RigidbodyならMovePosition、それ以外はTransform移動
        Auto,

        // CharacterControllerだけ動かす
        CharacterControllerOnly,

        // Rigidbodyだけ動かす
        RigidbodyOnly,

        // Transformを直接動かす
        TransformOnly
    }

    public enum CarryVerticalMoveMode
    {
        // CharacterController.Moveで縦方向も動かす
        CharacterControllerMove,

        // 上面追従時だけ縦方向をTransformで直接動かす
        DirectTransform
    }

    public enum CrushResponseMode
    {
        // 挟まりを検知しても何もしない
        None,

        // CharacterController.Moveで横衝突したら対象の移動を戻す
        CancelTargetMove,

        // 横衝突したらログだけ出す
        LogOnly
    }
    public enum CharacterCollisionMode
    {
        // BlockのColliderがPlayer/Enemyと通常通り接触する
        Solid,

        // BlockのColliderがPlayer/Enemyを物理的に押さないようにexcludeLayersへ追加する
        PhaseThroughCharacters
    }

    #endregion

    #region Inspector Classes

    [System.Serializable]
    private class ReferenceParam
    {
        [Header("対象")]
        [Tooltip("実際に動くブロック本体です。BoxCollider必須です。")]
        public Transform block;

        [Tooltip("BlockについているBoxColliderです。未設定ならBlockから自動取得します。")]
        public BoxCollider blockCollider;

        [Tooltip("BlockについているRigidbodyです。任意です。FixedUpdate移動時はKinematic Rigidbody推奨です。")]
        public Rigidbody blockRigidbody;

        [Tooltip("移動先ポイントを子として持つ親オブジェクトです。子のHierarchy順に巡回します。")]
        public Transform pointListRoot;
    }

    [System.Serializable]
    private class MoveParam
    {
        [Header("移動設定")]
        [Tooltip("移動ルートの種類です。Loopなら最後の次は最初へ、PingPongなら往復します。")]
        public MoveRouteMode routeMode = MoveRouteMode.Loop;

        [Tooltip("開始時のBlock位置をどう扱うかです。")]
        public MoveStartMode startMode = MoveStartMode.KeepCurrentPosition;

        [Tooltip("移動更新タイミングです。PlayerControllerがUpdateで動くならLateUpdate推奨です。")]
        public MoveUpdateTiming updateTiming = MoveUpdateTiming.LateUpdate;

        [Tooltip("移動速度です。単位はUnity Unit/秒です。")]
        public float moveSpeed = 2.0f;

        [Tooltip("各ポイント到達時に停止する時間です。")]
        public float waitTimeAtPoint = 0.0f;

        [Tooltip("目的地に到達したと判定する距離です。")]
        public float arriveDistance = 0.02f;

        [Tooltip("ONならStart時にBlockをPoint_00へ移動した後、Point_01へ向かいます。")]
        public bool startNextPointAfterSnap = true;
    }

    [System.Serializable]
    private class CarryParam
    {
        [Header("上に乗っている対象の追従")]
        [Tooltip("ONならBlock上面付近にいる対象をBlockの移動量だけ動かします。")]
        public bool carryOnTop = true;

        [Tooltip("追従対象にするLayerです。PlayerやEnemyを入れます。")]
        public LayerMask passengerLayerMask = ~0;

        [Tooltip("対象をどう動かすかです。基本はAuto推奨です。")]
        public PassengerMoveMode passengerMoveMode = PassengerMoveMode.Auto;

        [Tooltip("ONならTrigger Colliderも検出します。")]
        public bool includeTriggerColliders = true;

        [Tooltip("上面判定Boxの高さです。")]
        public float topCheckHeight = 0.35f;

        [Tooltip("上面判定Boxの上方向オフセットです。")]
        public float topCheckUpOffset = 0.05f;

        [Tooltip("上面判定Boxの横方向余白です。")]
        public Vector3 topCheckPadding = new Vector3(0.08f, 0f, 0.08f);

        [Header("下降追従補助")]
        [Tooltip("ONなら、直前まで乗っていた対象を短時間保持します。下降時にTopCheckBoxから一瞬外れて置いていかれる問題への対策です。")]
        public bool useCarryMemory = true;

        [Tooltip("上面検出から外れても、この時間だけ乗客扱いを維持します。")]
        public float carryMemoryTime = 0.15f;

        [Tooltip("ONなら、Blockが下降している時だけメモリ内の対象も一緒に運びます。")]
        public bool useMemoryOnlyWhenMovingDown = true;

        [Header("CharacterController補正")]
        [Tooltip("上面追従時の縦方向移動方式です。上下移動床ではDirectTransform推奨です。")]
        public CarryVerticalMoveMode verticalMoveMode = CarryVerticalMoveMode.DirectTransform;

        [Tooltip("ONなら下降時だけ下方向へ少し追加で押します。DirectTransform使用時は通常OFF推奨です。")]
        public bool addSmallDownForceWhenMovingDown = false;

        [Tooltip("下降時にCharacterControllerへ追加する下向き量です。")]
        public float downForce = 0.03f;

        [Tooltip("ONなら上昇時は余計な下方向補正を入れません。上昇時のガタつき防止です。")]
        public bool suppressDownForceWhenMovingUp = true;
    }

    [System.Serializable]
    private class PushParam
    {
        [Header("移動方向にいる対象の押し出し")]
        [Tooltip("ONならBlockの移動経路上にいるPlayer/EnemyをBlockの水平移動量だけ動かします。")]
        public bool pushMoveDirectionTargets = true;

        [Tooltip("押し出し対象にするLayerです。PlayerやEnemyを入れます。")]
        public LayerMask pushLayerMask = ~0;

        [Tooltip("ONならTrigger Colliderも検出します。")]
        public bool includeTriggerColliders = true;

        [Tooltip("押し出し判定Boxの余白です。")]
        public Vector3 pushCheckPadding = new Vector3(0.08f, 0.05f, 0.08f);

        [Tooltip("押した結果、壁などに横衝突した場合の処理です。CancelTargetMoveなら対象移動を取り消します。")]
        public CrushResponseMode crushResponseMode = CrushResponseMode.CancelTargetMove;

        [Tooltip("ONなら挟まり検知ログを出します。")]
        public bool logCrush = true;
    }

    [System.Serializable]
    private class CollisionParam
    {
        [Header("Blockとキャラクターの物理接触")]
        [Tooltip("BlockのColliderとPlayer/Enemyの接触をどう扱うかです。")]
        public CharacterCollisionMode characterCollisionMode = CharacterCollisionMode.Solid;

        [Tooltip("PhaseThroughCharacters時にBlockのColliderが無視するLayerです。Player / Enemyを入れます。")]
        public LayerMask characterExcludeLayers;

        [Tooltip("ONならStart時にBlock以下の非Trigger Colliderを自動収集します。")]
        public bool autoCollectBlockSolidColliders = true;

        [Tooltip("ONならTrigger ColliderはexcludeLayers変更対象から除外します。")]
        public bool ignoreTriggerColliders = true;
    }

    [System.Serializable]
    private class RuntimeParam
    {
        [Header("動作")]
        [Tooltip("ONならStart時から自動で動きます。")]
        public bool playOnStart = true;

        [Tooltip("ONならポイントリストの子をStart時に自動収集します。")]
        public bool autoCollectPointsOnStart = true;

        [Tooltip("ONなら移動を一時停止します。")]
        public bool pause = false;
    }

    [System.Serializable]
    private class DebugParam
    {
        [Header("デバッグ")]
        [Tooltip("ONならSceneビューに移動ルートや判定Boxを表示します。")]
        public bool drawGizmos = true;

        [Tooltip("Gizmoのポイントサイズです。")]
        public float gizmoPointSize = 0.2f;

        [Tooltip("ONなら移動量ログを出します。")]
        public bool logMoveDelta = false;


        [Tooltip("ONなら上面追従対象数をログに出します。")]
        public bool logCarryCount = false;

        [Tooltip("ONなら移動方向押し出し対象数をログに出します。")]
        public bool logPushCount = false;

        [Tooltip("ONならexcludeLayers対象Colliderログを出します。")]
        public bool logCollisionExclusion = false;

    }

    private class ColliderExcludeRecord
    {
        public Collider collider;
        public LayerMask originalExcludeLayers;
    }

    #endregion

    #region Inspector

    [SerializeField]
    [Header("参照")]
    private ReferenceParam refs = new ReferenceParam();

    [SerializeField]
    [Header("移動")]
    private MoveParam move = new MoveParam();

    [SerializeField]
    [Header("上面追従")]
    private CarryParam carry = new CarryParam();

    [SerializeField]
    [Header("移動方向押し出し")]
    private PushParam push = new PushParam();

    [SerializeField]
    [Header("接触制御")]
    private CollisionParam collision = new CollisionParam();

    [SerializeField]
    [Header("実行")]
    private RuntimeParam runtime = new RuntimeParam();

    [SerializeField]
    [Header("デバッグ")]
    private DebugParam debug = new DebugParam();

    #endregion

    #region Runtime

    private readonly List<Transform> points = new List<Transform>();

    private readonly Collider[] overlapBuffer = new Collider[128];
    private readonly HashSet<Transform> movedTargetSet = new HashSet<Transform>();
    private readonly List<Transform> currentCarryTargets = new List<Transform>();
    private readonly List<Transform> currentPushTargets = new List<Transform>();

    private Vector3 lastPushCheckCenter;
    private Vector3 lastPushCheckHalfExtents;
    private Quaternion lastPushCheckRotation;


    // 下降中に一瞬TopCheckBoxから外れても追従を維持するための乗客メモリ
    private readonly Dictionary<Transform, float> carryMemoryTimers =
        new Dictionary<Transform, float>();

    // Dictionary更新用の一時リスト
    private readonly List<Transform> carryMemoryRemoveList =
        new List<Transform>();

    // Dictionary更新用のキー退避リスト。
    // foreach中にDictionaryを書き換えるとInvalidOperationExceptionになるため使う。
    private readonly List<Transform> carryMemoryKeyList =
        new List<Transform>();

    private readonly List<ColliderExcludeRecord> excludeRecords =
        new List<ColliderExcludeRecord>();

    private TimeAgent agent;

    private int currentPointIndex = 0;
    private int moveDirection = 1;

    private bool isPlaying = false;
    private bool isWaiting = false;
    private bool collisionExclusionApplied = false;

    private float waitTimer = 0f;

    private Vector3 lastDelta = Vector3.zero;
    private Vector3 lastTopCheckCenter;
    private Vector3 lastTopCheckHalfExtents;
    private Quaternion lastTopCheckRotation;

    #endregion

    #region Unity

    private void Awake()
    {
        agent = GetComponent<TimeAgent>();
        AutoFindReferences();
    }

    private void Start()
    {
        if (runtime.autoCollectPointsOnStart)
            CollectPointsFromRoot();

        if (collision.autoCollectBlockSolidColliders)
            CollectBlockSolidCollidersForExclusion();

        InitializeStartPosition();
        ValidateSetupOnStart();
        ApplyCharacterCollisionMode();

        isPlaying = runtime.playOnStart;
    }

    private void Update()
    {
        if (move.updateTiming != MoveUpdateTiming.Update)
            return;

        TickMove(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (move.updateTiming != MoveUpdateTiming.FixedUpdate)
            return;

        TickMove(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (move.updateTiming != MoveUpdateTiming.LateUpdate)
            return;

        TickMove(Time.deltaTime);
    }

    private void OnDisable()
    {
        ClearCarryMemory();
        RestoreColliderExcludeLayers();
    }

    private void OnDestroy()
    {
        ClearCarryMemory();
        RestoreColliderExcludeLayers();
    }


    private void OnValidate()
    {
        if (move != null)
        {
            move.moveSpeed = Mathf.Max(0f, move.moveSpeed);
            move.waitTimeAtPoint = Mathf.Max(0f, move.waitTimeAtPoint);
            move.arriveDistance = Mathf.Max(0.0001f, move.arriveDistance);
        }

        if (carry != null)
        {
            carry.topCheckHeight = Mathf.Max(0.01f, carry.topCheckHeight);
            carry.topCheckUpOffset = Mathf.Max(0f, carry.topCheckUpOffset);
            carry.downForce = Mathf.Max(0f, carry.downForce);
            carry.carryMemoryTime = Mathf.Max(0f, carry.carryMemoryTime);
        }

        if (debug != null)
        {
            debug.gizmoPointSize = Mathf.Max(0.01f, debug.gizmoPointSize);
        }


        if (push != null)
        {
            push.pushCheckPadding.x = Mathf.Max(0f, push.pushCheckPadding.x);
            push.pushCheckPadding.y = Mathf.Max(0f, push.pushCheckPadding.y);
            push.pushCheckPadding.z = Mathf.Max(0f, push.pushCheckPadding.z);
        }

    }

    #endregion

    #region 初期化

    private void AutoFindReferences()
    {
        if (refs.block == null)
        {
            Transform found = transform.Find("Block");
            if (found != null)
                refs.block = found;
        }

        if (refs.pointListRoot == null)
        {
            Transform found = transform.Find("MovePoint");
            if (found == null)
                found = transform.Find("PointList");
            if (found == null)
                found = transform.Find("ポイントリスト");

            if (found != null)
                refs.pointListRoot = found;
        }

        if (refs.block != null)
        {
            if (refs.blockCollider == null)
                refs.blockCollider = refs.block.GetComponent<BoxCollider>();

            if (refs.blockRigidbody == null)
                refs.blockRigidbody = refs.block.GetComponent<Rigidbody>();
        }
    }

    private void InitializeStartPosition()
    {
        if (refs.block == null)
            return;

        if (points.Count <= 0)
            return;

        currentPointIndex = Mathf.Clamp(currentPointIndex, 0, points.Count - 1);
        moveDirection = 1;

        if (move.startMode == MoveStartMode.SnapToFirstPoint)
        {
            SetBlockPosition(points[0].position);

            if (move.startNextPointAfterSnap && points.Count > 1)
                currentPointIndex = 1;
            else
                currentPointIndex = 0;
        }
        else
        {
            currentPointIndex = 0;
        }

        lastDelta = Vector3.zero;
    }

    [ContextMenu("Collect Points From Root")]
    public void CollectPointsFromRoot()
    {
        points.Clear();

        if (refs.pointListRoot == null)
            return;

        for (int i = 0; i < refs.pointListRoot.childCount; i++)
        {
            Transform child = refs.pointListRoot.GetChild(i);

            if (child == null)
                continue;

            points.Add(child);
        }

        currentPointIndex =
            Mathf.Clamp(currentPointIndex, 0, Mathf.Max(points.Count - 1, 0));
    }

    private void ValidateSetupOnStart()
    {
        if (refs.block == null)
        {
            Debug.LogWarning("[MoveBlock] Block が未設定です。MoveBlock直下に Block を作ってください。", this);
            return;
        }

        if (refs.blockCollider == null)
            Debug.LogWarning("[MoveBlock] Block Collider が未設定です。BlockにBoxColliderを付けてください。", this);

        if (refs.pointListRoot == null)
        {
            Debug.LogWarning("[MoveBlock] MovePoint が未設定です。MoveBlock直下に MovePoint を作ってください。", this);
            return;
        }

        if (points.Count == 0)
            Debug.LogWarning("[MoveBlock] 移動ポイントが0個です。MovePointの子にPointを置いてください。", this);

        if (move.moveSpeed <= 0f)
            Debug.LogWarning("[MoveBlock] Move Speed が0以下です。", this);

        if (runtime.pause)
            Debug.LogWarning("[MoveBlock] Pause がONです。", this);

        if (!runtime.playOnStart)
            Debug.LogWarning("[MoveBlock] Play On Start がOFFです。", this);

        if (collision.characterCollisionMode == CharacterCollisionMode.PhaseThroughCharacters &&
            collision.characterExcludeLayers.value == 0)
        {
            Debug.LogWarning("[MoveBlock] PhaseThroughCharactersですが Character Exclude Layers がNothingです。Player/Enemyを設定してください。", this);
        }

        if (points.Count > 0 && refs.block != null)
        {
            Transform target = points[currentPointIndex];

            if (target != null)
            {
                float distance = Vector3.Distance(refs.block.position, target.position);

                Debug.Log(
                    $"[MoveBlock] Setup OK / Block={refs.block.name}, Points={points.Count}, CurrentIndex={currentPointIndex}, Target={target.name}, Distance={distance:F4}, Timing={move.updateTiming}, MoveSpeed={move.moveSpeed}, CollisionMode={collision.characterCollisionMode}",
                    this
                );
            }
        }
    }

    #endregion

    #region 移動更新

    private void TickMove(float baseDt)
    {
        if (!isPlaying)
            return;

        if (runtime.pause)
            return;

        if (agent != null && agent.TimeScale <= 0f)
            return;

        float dt = baseDt;

        if (agent != null)
            dt *= agent.TimeScale;

        UpdateMove(dt);
    }

    private void UpdateMove(float dt)
    {
        if (refs.block == null)
            return;

        if (points.Count == 0)
            return;

        if (move.moveSpeed <= 0f)
            return;

        // 乗客メモリは移動待機中でも減らす
        UpdateCarryMemoryTimers(dt);

        if (isWaiting)
        {
            UpdateWait(dt);
            return;
        }

        Transform targetPoint = points[currentPointIndex];

        if (targetPoint == null)
        {
            AdvancePointIndex();
            return;
        }

        Vector3 currentPosition = refs.block.position;
        Vector3 targetPosition = targetPoint.position;

        float arriveSqrDistance = move.arriveDistance * move.arriveDistance;

        if ((currentPosition - targetPosition).sqrMagnitude <= arriveSqrDistance)
        {
            SetBlockPosition(targetPosition);
            BeginWaitOrAdvance();
            return;
        }

        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            move.moveSpeed * dt
        );

        Vector3 delta = nextPosition - currentPosition;

        if (delta.sqrMagnitude <= 0.0000001f)
            return;

        // ==================================================
        // 重要:
        // 高速移動でも取りこぼさないように、
        // 現在位置だけではなく「移動前～移動後」を覆うSwept Boxで対象を拾う。
        //
        // 1. 上面乗客をSwept Top Boxで検出
        // 2. 移動方向上の押し出し対象をSwept Block Boxで検出
        // 3. Blockを移動
        // 4. 検出済み対象をBlockのdeltaで動かす
        // ==================================================

        CollectCarryTargetsBeforeBlockMove(delta);
        CollectPushTargetsBeforeBlockMove(delta);

        MoveBlockBody(delta);

        ApplyCarryTargetsAfterBlockMove(delta);
        ApplyPushTargetsAfterBlockMove(delta);

        lastDelta = delta;

        if (debug.logMoveDelta)
            Debug.Log($"[MoveBlock] Delta={delta}", this);

        if ((refs.block.position - targetPosition).sqrMagnitude <= arriveSqrDistance)
        {
            SetBlockPosition(targetPosition);
            BeginWaitOrAdvance();
        }
    }

    private void MoveBlockBody(Vector3 delta)
    {
        if (refs.block == null)
            return;

        Vector3 nextPosition = refs.block.position + delta;

        if (refs.blockRigidbody != null &&
            refs.blockRigidbody.isKinematic &&
            move.updateTiming == MoveUpdateTiming.FixedUpdate)
        {
            refs.blockRigidbody.MovePosition(nextPosition);
        }
        else
        {
            refs.block.position = nextPosition;
        }
    }

    private void SetBlockPosition(Vector3 position)
    {
        if (refs.block == null)
            return;

        if (refs.blockRigidbody != null &&
            refs.blockRigidbody.isKinematic &&
            move.updateTiming == MoveUpdateTiming.FixedUpdate)
        {
            refs.blockRigidbody.MovePosition(position);
        }
        else
        {
            refs.block.position = position;
        }
    }

    #endregion

    #region 待機 / ルート進行

    private void UpdateWait(float dt)
    {
        waitTimer -= dt;

        if (waitTimer > 0f)
            return;

        waitTimer = 0f;
        isWaiting = false;

        AdvancePointIndex();
    }

    private void BeginWaitOrAdvance()
    {
        if (move.waitTimeAtPoint > 0f)
        {
            isWaiting = true;
            waitTimer = move.waitTimeAtPoint;
        }
        else
        {
            AdvancePointIndex();
        }
    }

    private void AdvancePointIndex()
    {
        if (points.Count <= 1)
        {
            currentPointIndex = 0;
            return;
        }

        switch (move.routeMode)
        {
            case MoveRouteMode.Loop:
                AdvanceLoop();
                break;

            case MoveRouteMode.PingPong:
                AdvancePingPong();
                break;
        }
    }

    private void AdvanceLoop()
    {
        currentPointIndex++;

        if (currentPointIndex >= points.Count)
            currentPointIndex = 0;
    }

    private void AdvancePingPong()
    {
        currentPointIndex += moveDirection;

        if (currentPointIndex >= points.Count)
        {
            currentPointIndex = points.Count - 2;
            moveDirection = -1;
        }
        else if (currentPointIndex < 0)
        {
            currentPointIndex = 1;
            moveDirection = 1;
        }

        currentPointIndex =
            Mathf.Clamp(currentPointIndex, 0, points.Count - 1);
    }

    #endregion

    #region 上面追従

    private void CollectCarryTargetsBeforeBlockMove(Vector3 delta)
    {
        currentCarryTargets.Clear();

        if (!carry.carryOnTop)
            return;

        if (refs.blockCollider == null)
            return;

        // このフレームで移動済み扱いにする対象を初期化。
        // Carry対象を先に登録し、Push側で二重移動しないように使う。
        movedTargetSet.Clear();

        Vector3 center;
        Vector3 halfExtents;
        Quaternion rotation;

        // 高速移動対応:
        // 現在位置のTopCheckBoxではなく、
        // Blockの移動前～移動後を覆うSwept Top Boxを使う。
        GetSweptTopCheckBox(delta, out center, out halfExtents, out rotation);

        lastTopCheckCenter = center;
        lastTopCheckHalfExtents = halfExtents;
        lastTopCheckRotation = rotation;

        QueryTriggerInteraction query =
            carry.includeTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapBuffer,
            rotation,
            carry.passengerLayerMask,
            query
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];

            if (col == null)
                continue;

            Transform target = ResolveMoveTarget(col);

            if (target == null)
                continue;

            if (!movedTargetSet.Add(target))
                continue;

            currentCarryTargets.Add(target);
            RefreshCarryMemory(target);
        }

        bool isMovingDown = delta.y < -0.0001f;

        if (carry.useCarryMemory)
        {
            bool canUseMemory =
                !carry.useMemoryOnlyWhenMovingDown ||
                isMovingDown;

            if (canUseMemory && carryMemoryTimers.Count > 0)
            {
                carryMemoryKeyList.Clear();

                foreach (var pair in carryMemoryTimers)
                {
                    carryMemoryKeyList.Add(pair.Key);
                }

                for (int i = 0; i < carryMemoryKeyList.Count; i++)
                {
                    Transform rememberedTarget = carryMemoryKeyList[i];

                    if (rememberedTarget == null)
                        continue;

                    if (!carryMemoryTimers.TryGetValue(rememberedTarget, out float timer))
                        continue;

                    if (timer <= 0f)
                        continue;

                    if (movedTargetSet.Contains(rememberedTarget))
                        continue;

                    movedTargetSet.Add(rememberedTarget);
                    currentCarryTargets.Add(rememberedTarget);
                }
            }
        }

        if (debug.logCarryCount)
        {
            Debug.Log(
                $"[MoveBlock] Carry Count = {currentCarryTargets.Count}, RawOverlap = {count}, Memory = {carryMemoryTimers.Count}, Down = {isMovingDown}",
                this
            );
        }
    }

    private void ApplyCarryTargetsAfterBlockMove(Vector3 delta)
    {
        if (!carry.carryOnTop)
            return;

        if (currentCarryTargets.Count == 0)
            return;

        for (int i = 0; i < currentCarryTargets.Count; i++)
        {
            Transform target = currentCarryTargets[i];

            if (target == null)
                continue;

            MoveTargetByBlockDelta(target, delta, false, true);
        }
    }

    private void GetSweptTopCheckBox(
     Vector3 delta,
     out Vector3 center,
     out Vector3 halfExtents,
     out Quaternion rotation)
    {
        GetBlockBoxWorldData(out Vector3 blockCenter, out Vector3 blockHalf, out rotation);

        Vector3 up = refs.block.up;

        // 通常の上面判定Box中心
        Vector3 baseCenter =
            blockCenter +
            up * (blockHalf.y + carry.topCheckUpOffset + carry.topCheckHeight * 0.5f);

        // 移動前～移動後の中間へずらす
        center = baseCenter + delta * 0.5f;

        // Blockローカル方向で移動量を見て、移動分だけBoxを拡張する
        Vector3 localDelta = refs.block.InverseTransformVector(delta);

        halfExtents = new Vector3(
            blockHalf.x + carry.topCheckPadding.x,
            carry.topCheckHeight * 0.5f + carry.topCheckPadding.y,
            blockHalf.z + carry.topCheckPadding.z
        );

        halfExtents.x += Mathf.Abs(localDelta.x) * 0.5f;
        halfExtents.y += Mathf.Abs(localDelta.y) * 0.5f;
        halfExtents.z += Mathf.Abs(localDelta.z) * 0.5f;
    }

    private void RefreshCarryMemory(Transform target)
    {
        if (!carry.useCarryMemory)
            return;

        if (target == null)
            return;

        carryMemoryTimers[target] = carry.carryMemoryTime;
    }

    private void UpdateCarryMemoryTimers(float dt)
    {
        if (!carry.useCarryMemory)
            return;

        if (carryMemoryTimers.Count == 0)
            return;

        carryMemoryKeyList.Clear();
        carryMemoryRemoveList.Clear();

        // 先にキーだけ退避する。
        // Dictionaryをforeachしながら値更新/削除すると例外になるため。
        foreach (var pair in carryMemoryTimers)
        {
            carryMemoryKeyList.Add(pair.Key);
        }

        for (int i = 0; i < carryMemoryKeyList.Count; i++)
        {
            Transform target = carryMemoryKeyList[i];

            if (target == null)
            {
                carryMemoryRemoveList.Add(target);
                continue;
            }

            if (!carryMemoryTimers.TryGetValue(target, out float currentTimer))
                continue;

            float nextTimer = currentTimer - dt;

            if (nextTimer <= 0f)
            {
                carryMemoryRemoveList.Add(target);
            }
            else
            {
                carryMemoryTimers[target] = nextTimer;
            }
        }

        for (int i = 0; i < carryMemoryRemoveList.Count; i++)
        {
            carryMemoryTimers.Remove(carryMemoryRemoveList[i]);
        }
    }

    private void ClearCarryMemory()
    {
        carryMemoryTimers.Clear();
        carryMemoryRemoveList.Clear();
        carryMemoryKeyList.Clear();
        currentCarryTargets.Clear();
        movedTargetSet.Clear();
    }

    #endregion

    #region 移動方向押し出し

    private void CollectPushTargetsBeforeBlockMove(Vector3 delta)
    {
        currentPushTargets.Clear();

        if (!push.pushMoveDirectionTargets)
            return;

        if (refs.blockCollider == null)
            return;

        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);

        if (horizontalDelta.sqrMagnitude <= 0.0000001f)
            return;

        Vector3 center;
        Vector3 halfExtents;
        Quaternion rotation;

        GetSweptPushCheckBox(horizontalDelta, out center, out halfExtents, out rotation);

        lastPushCheckCenter = center;
        lastPushCheckHalfExtents = halfExtents;
        lastPushCheckRotation = rotation;

        QueryTriggerInteraction query =
            push.includeTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapBuffer,
            rotation,
            push.pushLayerMask,
            query
        );

        int pushCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];

            if (col == null)
                continue;

            Transform target = ResolveMoveTarget(col);

            if (target == null)
                continue;

            // Carry側で既に動かす対象は二重移動しない
            if (movedTargetSet.Contains(target))
                continue;

            if (!movedTargetSet.Add(target))
                continue;

            currentPushTargets.Add(target);
            pushCount++;
        }

        if (debug.logPushCount)
            Debug.Log($"[MoveBlock] Push Count = {pushCount}", this);
    }

    private void ApplyPushTargetsAfterBlockMove(Vector3 delta)
    {
        if (!push.pushMoveDirectionTargets)
            return;

        if (currentPushTargets.Count == 0)
            return;

        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);

        if (horizontalDelta.sqrMagnitude <= 0.0000001f)
            return;

        for (int i = 0; i < currentPushTargets.Count; i++)
        {
            Transform target = currentPushTargets[i];

            if (target == null)
                continue;

            MoveTargetByBlockDelta(target, horizontalDelta, true, false);
        }
    }

    private void GetSweptPushCheckBox(
        Vector3 horizontalDelta,
        out Vector3 center,
        out Vector3 halfExtents,
        out Quaternion rotation)
    {
        GetBlockBoxWorldData(out Vector3 blockCenter, out Vector3 blockHalf, out rotation);

        center = blockCenter + horizontalDelta * 0.5f;

        Vector3 localDelta = refs.block.InverseTransformVector(horizontalDelta);

        halfExtents = new Vector3(
            blockHalf.x + push.pushCheckPadding.x,
            blockHalf.y + push.pushCheckPadding.y,
            blockHalf.z + push.pushCheckPadding.z
        );

        halfExtents.x += Mathf.Abs(localDelta.x) * 0.5f;
        halfExtents.z += Mathf.Abs(localDelta.z) * 0.5f;
    }

    #endregion

    #region 対象移動

    private Transform ResolveMoveTarget(Collider col)
    {
        if (col == null)
            return null;

        CharacterController cc = col.GetComponentInParent<CharacterController>();

        if (cc != null)
            return cc.transform;

        Rigidbody rb = col.attachedRigidbody;

        if (rb != null)
            return rb.transform;

        return col.transform;
    }

    private void MoveTargetByBlockDelta(
    Transform target,
    Vector3 delta,
    bool cancelOnSide = false,
    bool isCarry = false)
    {
        if (target == null)
            return;

        CharacterController cc = target.GetComponent<CharacterController>();

        if (cc != null)
        {
            MoveCharacterControllerTarget(cc, delta, cancelOnSide, isCarry);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (rb != null)
        {
            MoveRigidbodyTarget(rb, delta);
            return;
        }

        target.position += delta;
    }

    private void MoveCharacterControllerTarget(
    CharacterController cc,
    Vector3 delta,
    bool cancelOnSide,
    bool isCarry)
    {
        if (cc == null)
            return;

        Vector3 before = cc.transform.position;

        // ==========================================
        // 上面追従専用:
        // 上下移動床では、縦方向をCharacterController.Moveに任せると
        // Block自身との接触解決でガタつき/浮きが出やすい。
        //
        // そのため、上面追従時だけ
        //   横方向 = CharacterController.Move
        //   縦方向 = Transform直接加算
        // に分離できるようにする。
        // ==========================================
        if (isCarry &&
            carry.verticalMoveMode == CarryVerticalMoveMode.DirectTransform)
        {
            Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
            Vector3 verticalDelta = new Vector3(0f, delta.y, 0f);

            // 横方向はCharacterControllerの衝突解決を使う。
            if (horizontalDelta.sqrMagnitude > 0.0000001f)
            {
                cc.Move(horizontalDelta);
            }

            // 縦方向は直接動かす。
            // 上昇/下降時にCharacterControllerがBlockとの接触で押し戻されるのを避ける。
            if (Mathf.Abs(verticalDelta.y) > 0.0000001f)
            {
                cc.transform.position += verticalDelta;
            }

            return;
        }

        Vector3 moveDelta = delta;

        // DirectTransformを使わない場合だけ、従来の下方向補正を適用する。
        if (isCarry &&
            carry.addSmallDownForceWhenMovingDown &&
            delta.y < -0.0001f)
        {
            moveDelta += Vector3.down * carry.downForce;
        }

        if (isCarry &&
            carry.suppressDownForceWhenMovingUp &&
            delta.y > 0.0001f)
        {
            moveDelta = delta;
        }

        CollisionFlags flags = cc.Move(moveDelta);

        if (!cancelOnSide)
            return;

        bool hitSide = (flags & CollisionFlags.Sides) != 0;

        if (!hitSide)
            return;

        if (push.crushResponseMode == CrushResponseMode.None)
            return;

        if (push.crushResponseMode == CrushResponseMode.LogOnly)
        {
            if (push.logCrush)
                Debug.Log($"[MoveBlock] CharacterController side hit : {cc.name}", cc);

            return;
        }

        if (push.crushResponseMode == CrushResponseMode.CancelTargetMove)
        {
            Vector3 moved = cc.transform.position - before;

            if (moved.sqrMagnitude > 0.0000001f)
                cc.Move(-moved);

            if (push.logCrush)
                Debug.Log($"[MoveBlock] Cancel target move because of side collision : {cc.name}", cc);
        }
    }

    private void MoveRigidbodyTarget(Rigidbody rb, Vector3 delta)
    {
        if (rb == null)
            return;

        if (rb.isKinematic)
            rb.MovePosition(rb.position + delta);
        else
            rb.position += delta;
    }

    #endregion

    #region 接触制御 / excludeLayers

    [ContextMenu("Collect Block Solid Colliders For Exclusion")]
    public void CollectBlockSolidCollidersForExclusion()
    {
        RestoreColliderExcludeLayers();

        excludeRecords.Clear();

        if (refs.block == null)
            return;

        Collider[] colliders = refs.block.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null)
                continue;

            if (collision.ignoreTriggerColliders && col.isTrigger)
                continue;

            ColliderExcludeRecord record = new ColliderExcludeRecord
            {
                collider = col,
                originalExcludeLayers = col.excludeLayers
            };

            excludeRecords.Add(record);

            if (debug.logCollisionExclusion)
                Debug.Log($"[MoveBlock] Collect Collider : {col.name}", col);
        }
    }

    private void ApplyCharacterCollisionMode()
    {
        if (collision.characterCollisionMode == CharacterCollisionMode.Solid)
        {
            RestoreColliderExcludeLayers();
            return;
        }

        ApplyCharacterExcludeLayers();
    }

    private void ApplyCharacterExcludeLayers()
    {
        if (collisionExclusionApplied)
            return;

        for (int i = 0; i < excludeRecords.Count; i++)
        {
            ColliderExcludeRecord record = excludeRecords[i];

            if (record == null || record.collider == null)
                continue;

            LayerMask mask = record.collider.excludeLayers;
            mask.value |= collision.characterExcludeLayers.value;
            record.collider.excludeLayers = mask;

            if (debug.logCollisionExclusion)
                Debug.Log($"[MoveBlock] Apply ExcludeLayers : {record.collider.name}", record.collider);
        }

        collisionExclusionApplied = true;
    }

    private void RestoreColliderExcludeLayers()
    {
        if (!collisionExclusionApplied && excludeRecords.Count == 0)
            return;

        for (int i = 0; i < excludeRecords.Count; i++)
        {
            ColliderExcludeRecord record = excludeRecords[i];

            if (record == null || record.collider == null)
                continue;

            record.collider.excludeLayers = record.originalExcludeLayers;
        }

        collisionExclusionApplied = false;
    }

    #endregion

    #region Utility

    private void GetBlockBoxWorldData(
        out Vector3 center,
        out Vector3 halfExtents,
        out Quaternion rotation)
    {
        BoxCollider box = refs.blockCollider;

        center = box.transform.TransformPoint(box.center);

        Vector3 scale = box.transform.lossyScale;

        halfExtents = new Vector3(
            Mathf.Abs(box.size.x * scale.x) * 0.5f,
            Mathf.Abs(box.size.y * scale.y) * 0.5f,
            Mathf.Abs(box.size.z * scale.z) * 0.5f
        );

        rotation = box.transform.rotation;
    }

    #endregion

    #region 外部操作

    public void Play()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
        isWaiting = false;
        waitTimer = 0f;
    }

    public void SetPause(bool value)
    {
        runtime.pause = value;
    }

    public void Restart()
    {
        isWaiting = false;
        waitTimer = 0f;

        InitializeStartPosition();

        isPlaying = true;
    }

    public void SetMoveSpeed(float speed)
    {
        move.moveSpeed = Mathf.Max(0f, speed);
    }

    public void SetRouteMode(MoveRouteMode mode)
    {
        move.routeMode = mode;
    }

    public void MoveToPointImmediate(int index)
    {
        if (refs.block == null)
            return;

        if (points.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, points.Count - 1);

        currentPointIndex = index;
        SetBlockPosition(points[index].position);
    }

    #endregion

    #region Gizmo

    private void OnDrawGizmos()
    {
        if (debug == null || !debug.drawGizmos)
            return;

        DrawPointRouteGizmos();
        DrawRuntimeCheckGizmos();
    }

    private void DrawPointRouteGizmos()
    {
        Transform root = refs != null ? refs.pointListRoot : null;

        if (root == null)
        {
            Transform found = transform.Find("MovePoint");
            if (found == null)
                found = transform.Find("PointList");
            if (found == null)
                found = transform.Find("ポイントリスト");

            root = found;
        }

        if (root == null)
            return;

        int count = root.childCount;

        if (count <= 0)
            return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < count; i++)
        {
            Transform p = root.GetChild(i);

            if (p == null)
                continue;

            Gizmos.DrawWireSphere(p.position, debug.gizmoPointSize);
        }

        Gizmos.color = Color.yellow;

        for (int i = 0; i < count - 1; i++)
        {
            Transform a = root.GetChild(i);
            Transform b = root.GetChild(i + 1);

            if (a == null || b == null)
                continue;

            Gizmos.DrawLine(a.position, b.position);
        }

        if (move != null &&
            move.routeMode == MoveRouteMode.Loop &&
            count >= 2)
        {
            Transform first = root.GetChild(0);
            Transform last = root.GetChild(count - 1);

            if (first != null && last != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(last.position, first.position);
            }
        }
    }


    private void DrawRuntimeCheckGizmos()
    {
        if (refs == null || refs.blockCollider == null)
            return;

        DrawBoxGizmo(lastTopCheckCenter, lastTopCheckHalfExtents, lastTopCheckRotation, Color.magenta);
        DrawBoxGizmo(lastPushCheckCenter, lastPushCheckHalfExtents, lastPushCheckRotation, Color.red);
    }


    private void DrawBoxGizmo(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation,
        Color color)
    {
        if (halfExtents.sqrMagnitude <= 0.0001f)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.color = color;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = oldMatrix;
    }

    #endregion
}