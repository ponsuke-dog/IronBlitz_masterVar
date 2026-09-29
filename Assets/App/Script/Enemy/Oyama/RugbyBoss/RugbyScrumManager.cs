using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RugbyScrumManager : MonoBehaviour
{
    public static RugbyScrumManager Instance { get; private set; }

    private enum ScrumState
    {
        None,
        Gathering,
        FormIdle,
        Rush,
        Broken,
        Finished
    }

    [System.Serializable]
    private class FormationParam
    {
        [Header("Gather Point")]
        [Tooltip("Boss位置から見た集合地点オフセットです。BeginScrumCommandで明示位置を渡した場合はそちらが優先されます。")]
        public Vector3 gatherOffsetFromBoss = new Vector3(0f, 0f, 4f);

        [Tooltip("集合地点に向かう召喚エネミーの速度です。")]
        public float memberGatherSpeed = 6f;

        [Tooltip("集合地点に到着したとみなす距離です。")]
        public float memberArriveDistance = 0.25f;

        [Tooltip("全員集合を待つ最大時間です。超えた場合は未到着メンバーも強制隊列化します。")]
        public float gatherTimeout = 4f;

        [Header("Formation")]
        [Tooltip("隊列確定後、突撃開始まで待機する時間です。")]
        public float formIdleDuration = 0.5f;

        [Tooltip("横1列あたりの人数です。")]
        public int membersPerRow = 3;

        [Tooltip("隊列の横方向間隔です。")]
        public float memberSpacingX = 0.85f;

        [Tooltip("隊列の奥行き方向間隔です。")]
        public float memberSpacingZ = 0.75f;

        [Tooltip("隊列全体のローカルオフセットです。")]
        public Vector3 localOffset = Vector3.zero;

        [Tooltip("隊列化時にメンバーをスロット位置へ強制補正します。")]
        public bool snapMembersOnForm = true;
    }

    [System.Serializable]
    private class RushParam
    {
        [Header("Rush")]
        [Tooltip("スクラム全体の突撃速度です。")]
        public float rushSpeed = 9f;

        [Tooltip("プレイヤーがこの距離より遠い場合は追跡します。")]
        public float trackingDistance = 5f;

        [Tooltip("プレイヤーがこの距離以下になったら、その時点の方向へ固定して直進します。")]
        public float lockStraightDistance = 4f;

        [Tooltip("追跡中の旋回速度です。度/秒。")]
        public float turnSpeed = 360f;

        [Tooltip("突撃開始地点からこの距離を超えたらスクラムを終了します。")]
        public float maxRushDistance = 30f;

        [Tooltip("スクラム全体の寿命です。0以下なら無効です。")]
        public float lifeTime = 8f;

        [Header("Stage Bounds")]
        [Tooltip("ONならステージ矩形外へ出たらスクラムを終了します。")]
        public bool destroyOutsideStageBounds = false;

        [Tooltip("ステージ範囲の中心です。XZだけ判定します。")]
        public Vector3 stageBoundsCenter = Vector3.zero;

        [Tooltip("ステージ範囲のサイズです。XZだけ判定します。")]
        public Vector3 stageBoundsSize = new Vector3(60f, 10f, 60f);
    }

    [System.Serializable]
    private class HitParam
    {
        [Header("Player Hit")]
        [Tooltip("スクラム突撃がプレイヤーへ当たった時のダメージです。")]
        public int playerDamage = 3;

        [Tooltip("プレイヤーに当たったらスクラムを終了します。")]
        public bool finishOnPlayerHit = true;

        [Tooltip("ONならスクラム突撃攻撃を仮想的に1つの攻撃判定として扱い、複数メンバーのHitboxが同時に当たっても1回だけ処理します。")]
        public bool treatRushAttackAsSingleVirtualCollider = true;

        [Tooltip("ONならスクラム攻撃Hitを1フレーム保留し、その間にジャストタックル通知が来た場合はジャストタックルを優先します。")]
        public bool delayPlayerHitOneFrameForJustTackle = true;

        [Header("Just Tackle Break")]
        [Tooltip("ジャストタックルで分解された召喚エネミーの水平吹き飛び速度です。")]
        public float breakHorizontalPower = 24f;

        [Tooltip("ジャストタックルで分解された召喚エネミーの上方向速度です。")]
        public float breakVerticalPower = 9f;

        [Tooltip("分解時に各召喚エネミーへ加えるランダム角度です。")]
        public float scatterAngle = 28f;
    }

    [System.Serializable]
    private class CommandParam
    {
        [Header("Invalid Member")]
        [Tooltip("集合命令時点でSpawnMotion/Chase以外の召喚エネミーを破壊します。")]
        public bool destroyInvalidMembersOnCommand = true;

        [Tooltip("同時にスクラムできる最低人数です。満たない場合でも開始するなら1以下にします。")]
        public int minMemberCount = 1;
    }

    [System.Serializable]
    private class RuntimeRootParam
    {
        [Header("Runtime Root")]
        [Tooltip("スクラムごとに生成される隊列親オブジェクト名です。Manager自身やGatherPointは移動させません。")]
        public string runtimeRootName = "RugbyScrum_RuntimeRoot";

        [Tooltip("ONならランタイム親オブジェクトをManagerの子として作ります。OFFならHierarchy直下に作ります。")]
        public bool parentRuntimeRootToManager = true;

        [Tooltip("スクラム終了時にランタイム親オブジェクトを削除します。基本ON推奨です。")]
        public bool destroyRuntimeRootOnFinish = true;
    }

    [System.Serializable]
    private class DebugParam
    {
        [Header("Log")]
        public bool logState = true;
        public bool logMember = true;
        public bool logHit = true;
        public bool logRuntimeRoot = true;

        [Header("Gizmo")]
        public bool drawFormationGizmos = true;
        public bool drawStageBoundsGizmos = true;
    }

    [Header("Formation")]
    [SerializeField] private FormationParam formationParam = new FormationParam();

    [Header("Rush")]
    [SerializeField] private RushParam rushParam = new RushParam();

    [Header("Hit")]
    [SerializeField] private HitParam hitParam = new HitParam();

    [Header("Command")]
    [SerializeField] private CommandParam commandParam = new CommandParam();

    [Header("Runtime Root")]
    [SerializeField] private RuntimeRootParam runtimeRootParam = new RuntimeRootParam();

    [Header("Debug")]
    [SerializeField] private DebugParam debugParam = new DebugParam();

    private ScrumState state = ScrumState.None;
    private RugbyBoss ownerBoss;
    private Transform playerTarget;
    private Transform activeRoot;
    private GameObject activeRootObject;
    private Vector3 fixedGatherPoint;
    private Vector3 moveDirection;
    private Vector3 rushStartPosition;
    private bool rushDirectionLocked;
    private float stateTimer;
    private float lifeTimer;

    private readonly List<RugbySummonEnemy> members = new List<RugbySummonEnemy>();
    private readonly List<Vector3> localSlots = new List<Vector3>();
    private readonly List<Quaternion> localRotations = new List<Quaternion>();
    private readonly HashSet<GameObject> rushHitTargets = new HashSet<GameObject>();
    private bool rushAttackResolvedThisRush;
    private bool pendingScrumAttack;
    private int pendingScrumAttackFrame = -1;
    private RugbySummonEnemy pendingScrumAttackSourceEnemy;
    private IHitReceiver pendingScrumAttackReceiver;
    private MonoBehaviour pendingScrumAttackReceiverMono;
    private HitEventData pendingScrumAttackData;

    public bool IsScrumActive =>
        state == ScrumState.Gathering ||
        state == ScrumState.FormIdle ||
        state == ScrumState.Rush;

    public int CurrentMemberCount => members.Count;
    public Transform ActiveRoot => activeRoot;
    public Vector3 FixedGatherPoint => fixedGatherPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsScrumActive)
            return;

        float dt = Time.deltaTime;
        stateTimer += dt;
        lifeTimer += dt;

        if (rushParam.lifeTime > 0f && lifeTimer >= rushParam.lifeTime)
        {
            FinishScrum();
            return;
        }

        switch (state)
        {
            case ScrumState.Gathering:
                UpdateGathering();
                break;
            case ScrumState.FormIdle:
                UpdateFormIdle();
                break;
            case ScrumState.Rush:
                UpdateRush(dt);
                break;
        }

        ProcessPendingScrumAttack();
    }

    public bool BeginScrumCommand(RugbyBoss boss, Transform target, List<RugbySummonEnemy> candidates)
    {
        Vector3 gatherPoint = ResolveDefaultGatherPoint(boss, target);
        return BeginScrumCommand(boss, target, candidates, gatherPoint);
    }

    public bool BeginScrumCommand(RugbyBoss boss, Transform target, List<RugbySummonEnemy> candidates, Vector3 worldGatherPoint)
    {
        FinishScrumWithoutLaunchingMembers();
        DestroyRuntimeRoot();

        ownerBoss = boss;
        playerTarget = target;
        fixedGatherPoint = worldGatherPoint;

        members.Clear();
        localSlots.Clear();
        localRotations.Clear();
        FilterMembers(candidates);

        if (members.Count < Mathf.Max(1, commandParam.minMemberCount))
        {
            if (debugParam.logState)
                Debug.LogWarning($"{name} RugbyScrumManager Begin failed member:{members.Count}");

            state = ScrumState.None;
            return false;
        }

        CreateRuntimeRoot(fixedGatherPoint, GetDirectionFromPointToPlayerOrForward(fixedGatherPoint));
        BuildFormationSlots();
        BeginGathering();
        return true;
    }

    private Vector3 ResolveDefaultGatherPoint(RugbyBoss boss, Transform target)
    {
        if (boss != null)
            return boss.transform.TransformPoint(formationParam.gatherOffsetFromBoss);

        if (target != null)
            return target.position;

        return transform.position;
    }

    private void CreateRuntimeRoot(Vector3 worldPosition, Vector3 forwardDirection)
    {
        activeRootObject = new GameObject(runtimeRootParam.runtimeRootName);
        activeRoot = activeRootObject.transform;

        if (runtimeRootParam.parentRuntimeRootToManager)
            activeRoot.SetParent(transform, worldPositionStays: true);

        activeRoot.position = worldPosition;

        forwardDirection.y = 0f;
        if (forwardDirection.sqrMagnitude < 0.0001f)
            forwardDirection = Vector3.forward;

        activeRoot.rotation = Quaternion.LookRotation(forwardDirection.normalized, Vector3.up);

        if (debugParam.logRuntimeRoot)
        {
            Debug.Log(
                $"{name} Scrum runtime root created " +
                $"root:{activeRootObject.name} pos:{activeRoot.position} rot:{activeRoot.rotation.eulerAngles}"
            );
        }
    }

    private void DestroyRuntimeRoot()
    {
        if (activeRootObject == null)
        {
            activeRoot = null;
            return;
        }

        if (debugParam.logRuntimeRoot)
            Debug.Log($"{name} Scrum runtime root destroyed root:{activeRootObject.name}");

        if (runtimeRootParam.destroyRuntimeRootOnFinish)
            Destroy(activeRootObject);

        activeRootObject = null;
        activeRoot = null;
    }

    private void FilterMembers(List<RugbySummonEnemy> candidates)
    {
        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            RugbySummonEnemy enemy = candidates[i];
            if (enemy == null)
                continue;
            if (!enemy.IsAlive)
                continue;

            if (!enemy.CanJoinScrumCommand)
            {
                if (commandParam.destroyInvalidMembersOnCommand)
                {
                    if (debugParam.logMember)
                        Debug.Log($"{name} Scrum command destroy invalid enemy:{enemy.name} state:{enemy.CurrentStateName}");

                    enemy.ForceDestroy();
                }

                continue;
            }

            if (!members.Contains(enemy))
                members.Add(enemy);
        }
    }

    private void BuildFormationSlots()
    {
        int perRow = Mathf.Max(1, formationParam.membersPerRow);

        for (int i = 0; i < members.Count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int countInRow = Mathf.Min(perRow, members.Count - row * perRow);

            float x = (col - (countInRow - 1) * 0.5f) * formationParam.memberSpacingX;
            float z = -row * formationParam.memberSpacingZ;

            localSlots.Add(formationParam.localOffset + new Vector3(x, 0f, z));
            localRotations.Add(Quaternion.identity);
        }
    }

    private void BeginGathering()
    {
        if (activeRoot == null)
        {
            Debug.LogWarning($"{name} Scrum BeginGathering failed: activeRoot is null.");
            state = ScrumState.None;
            return;
        }

        state = ScrumState.Gathering;
        stateTimer = 0f;
        lifeTimer = 0f;
        moveDirection = GetDirectionToPlayerOrForward();
        rushDirectionLocked = false;

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null)
                continue;

            Vector3 slot = i < localSlots.Count ? localSlots[i] : Vector3.zero;
            Quaternion rot = i < localRotations.Count ? localRotations[i] : Quaternion.identity;

            enemy.BeginMoveToScrumPoint(
                activeRoot,
                slot,
                rot,
                formationParam.memberGatherSpeed,
                formationParam.memberArriveDistance
            );
        }

        if (debugParam.logState)
        {
            Debug.Log(
                $"{name} Scrum State => Gathering " +
                $"member:{members.Count} gatherPoint:{fixedGatherPoint} root:{GetRootName()}"
            );
        }
    }

    private void UpdateGathering()
    {
        FaceTargetDuringPreparation();
        CleanupDeadMembers();

        if (members.Count <= 0)
        {
            FinishScrum();
            return;
        }

        if (AreAllMembersReadyForFormation())
        {
            BeginFormIdle(forceCaptureRemaining: false);
            return;
        }

        if (stateTimer >= Mathf.Max(0f, formationParam.gatherTimeout))
        {
            BeginFormIdle(forceCaptureRemaining: true);
        }
    }

    private bool AreAllMembersReadyForFormation()
    {
        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null)
                continue;
            if (!enemy.IsAlive)
                continue;
            if (!enemy.IsReadyForScrumFormation)
                return false;
        }

        return true;
    }

    private void BeginFormIdle(bool forceCaptureRemaining)
    {
        if (activeRoot == null)
        {
            FinishScrum();
            return;
        }

        state = ScrumState.FormIdle;
        stateTimer = 0f;
        moveDirection = GetDirectionToPlayerOrForward();
        activeRoot.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            Vector3 slot = i < localSlots.Count ? localSlots[i] : Vector3.zero;
            Quaternion rot = i < localRotations.Count ? localRotations[i] : Quaternion.identity;

            if (forceCaptureRemaining || formationParam.snapMembersOnForm)
                enemy.CaptureIntoScrumIdle(activeRoot, slot, rot);
            else
                enemy.UpdateScrumSlot(slot, rot);
        }

        if (debugParam.logState)
            Debug.Log($"{name} Scrum State => FormIdle force:{forceCaptureRemaining} root:{GetRootName()}");
    }

    private void UpdateFormIdle()
    {
        FaceTargetDuringPreparation();
        CleanupDeadMembers();

        if (members.Count <= 0)
        {
            FinishScrum();
            return;
        }

        if (stateTimer < Mathf.Max(0f, formationParam.formIdleDuration))
            return;

        BeginRush();
    }

    private void BeginRush()
    {
        if (activeRoot == null)
        {
            FinishScrum();
            return;
        }

        state = ScrumState.Rush;
        stateTimer = 0f;
        ResetRushAttackRuntime();
        rushStartPosition = activeRoot.position;
        rushDirectionLocked = false;
        moveDirection = GetDirectionToPlayerOrForward();
        activeRoot.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null || !enemy.IsAlive)
                continue;
            enemy.BeginScrumRush();
        }

        if (debugParam.logState)
            Debug.Log($"{name} Scrum State => Rush member:{members.Count} root:{GetRootName()} start:{rushStartPosition}");
    }

    private void UpdateRush(float dt)
    {
        if (activeRoot == null)
        {
            FinishScrum();
            return;
        }

        CleanupDeadMembers();

        if (members.Count <= 0)
        {
            FinishScrum();
            return;
        }

        if (!rushDirectionLocked)
        {
            Vector3 targetDir = GetDirectionToPlayerOrForward();
            float distance = GetDistanceToPlayer();

            if (distance <= Mathf.Max(0f, rushParam.lockStraightDistance))
            {
                rushDirectionLocked = true;
                moveDirection = targetDir;

                if (debugParam.logState)
                    Debug.Log($"{name} Scrum direction locked distance:{distance:F2} dir:{moveDirection}");
            }
            else if (distance > Mathf.Max(rushParam.lockStraightDistance, rushParam.trackingDistance))
            {
                moveDirection = Vector3.RotateTowards(
                    moveDirection,
                    targetDir,
                    Mathf.Max(0f, rushParam.turnSpeed) * Mathf.Deg2Rad * dt,
                    0f
                );
                moveDirection.y = 0f;
                if (moveDirection.sqrMagnitude > 0.0001f)
                    moveDirection.Normalize();
            }
            else
            {
                moveDirection = targetDir;
            }
        }

        activeRoot.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        activeRoot.position += moveDirection * Mathf.Max(0f, rushParam.rushSpeed) * dt;

        if (Vector3.Distance(rushStartPosition, activeRoot.position) >= Mathf.Max(0.1f, rushParam.maxRushDistance))
        {
            FinishScrum();
            return;
        }

        if (rushParam.destroyOutsideStageBounds && IsOutsideStageBounds(activeRoot.position))
        {
            FinishScrum();
        }
    }

    public void NotifyScrumAttackHit(RugbySummonEnemy sourceEnemy, Hitbox selfHitbox, Collider other)
    {
        if (state != ScrumState.Rush)
            return;

        if (sourceEnemy == null || selfHitbox == null || other == null)
            return;

        if (!members.Contains(sourceEnemy))
            return;

        if (hitParam.treatRushAttackAsSingleVirtualCollider && rushAttackResolvedThisRush)
            return;

        if (hitParam.treatRushAttackAsSingleVirtualCollider && pendingScrumAttack)
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

        if (hitParam.treatRushAttackAsSingleVirtualCollider && rushHitTargets.Contains(receiverMono.gameObject))
            return;

        HitEventData data = new HitEventData
        {
            attackerObject = sourceEnemy.gameObject,
            attackerHitbox = selfHitbox.gameObject,
            targetObject = receiverMono.gameObject,
            targetHitbox = targetHitbox.gameObject,
            contactPoint = targetHitbox.transform.position,
            payload = new EnemyAttackPayload
            {
                damage = hitParam.playerDamage
            }
        };

        pendingScrumAttack = true;
        pendingScrumAttackFrame = Time.frameCount;
        pendingScrumAttackSourceEnemy = sourceEnemy;
        pendingScrumAttackReceiver = receiver;
        pendingScrumAttackReceiverMono = receiverMono;
        pendingScrumAttackData = data;

        if (debugParam.logHit)
        {
            Debug.Log(
                $"{name} Scrum attack pending source:{sourceEnemy.name} " +
                $"target:{receiverMono.name} damage:{hitParam.playerDamage} frame:{pendingScrumAttackFrame}"
            );
        }

        if (!hitParam.delayPlayerHitOneFrameForJustTackle)
            ProcessPendingScrumAttack();
    }

    private void ProcessPendingScrumAttack()
    {
        if (!pendingScrumAttack)
            return;

        if (state != ScrumState.Rush)
        {
            ClearPendingScrumAttack();
            return;
        }

        if (hitParam.treatRushAttackAsSingleVirtualCollider && rushAttackResolvedThisRush)
        {
            ClearPendingScrumAttack();
            return;
        }

        if (hitParam.delayPlayerHitOneFrameForJustTackle && Time.frameCount <= pendingScrumAttackFrame)
            return;

        if (pendingScrumAttackReceiver == null || pendingScrumAttackReceiverMono == null)
        {
            ClearPendingScrumAttack();
            return;
        }

        if (hitParam.treatRushAttackAsSingleVirtualCollider)
        {
            rushAttackResolvedThisRush = true;
            rushHitTargets.Add(pendingScrumAttackReceiverMono.gameObject);
        }

        if (debugParam.logHit)
        {
            string sourceName = pendingScrumAttackSourceEnemy != null ? pendingScrumAttackSourceEnemy.name : "null";
            Debug.Log(
                $"{name} Scrum attack resolved source:{sourceName} " +
                $"target:{pendingScrumAttackReceiverMono.name} damage:{hitParam.playerDamage}"
            );
        }

        IHitReceiver receiver = pendingScrumAttackReceiver;
        HitEventData data = pendingScrumAttackData;
        ClearPendingScrumAttack();

        receiver.OnHit(data);

        if (hitParam.finishOnPlayerHit)
            FinishScrum();
    }

    private void CancelPendingScrumAttack(string reason)
    {
        if (!pendingScrumAttack)
            return;

        if (debugParam.logHit)
            Debug.Log($"{name} Scrum attack canceled reason:{reason}");

        ClearPendingScrumAttack();
    }

    private void ClearPendingScrumAttack()
    {
        pendingScrumAttack = false;
        pendingScrumAttackFrame = -1;
        pendingScrumAttackSourceEnemy = null;
        pendingScrumAttackReceiver = null;
        pendingScrumAttackReceiverMono = null;
        pendingScrumAttackData = default;
    }

    private void ResetRushAttackRuntime()
    {
        rushAttackResolvedThisRush = false;
        rushHitTargets.Clear();
        ClearPendingScrumAttack();
    }

    public void NotifyScrumJustTackle(RugbySummonEnemy sourceEnemy, HitEventData data, BlowPayload blow)
    {
        if (!IsScrumActive)
            return;
        if (sourceEnemy == null)
            return;
        if (!members.Contains(sourceEnemy))
            return;
        if (!IsJustTackle(blow))
            return;

        Vector3 breakDirection = blow.powerDirection;
        breakDirection.y = 0f;

        if (breakDirection.sqrMagnitude < 0.0001f)
            breakDirection = -moveDirection;

        if (breakDirection.sqrMagnitude < 0.0001f)
            breakDirection = activeRoot != null ? -activeRoot.forward : -transform.forward;

        breakDirection.Normalize();
        CancelPendingScrumAttack("JustTackle");
        rushAttackResolvedThisRush = true;
        BreakScrum(breakDirection);
    }

    private void BreakScrum(Vector3 baseDirection)
    {
        CancelPendingScrumAttack("BreakScrum");
        rushAttackResolvedThisRush = true;

        if (debugParam.logHit)
            Debug.Log($"{name} Scrum Break member:{members.Count} dir:{baseDirection}");

        state = ScrumState.Broken;

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            float randomYaw = Random.Range(-Mathf.Abs(hitParam.scatterAngle), Mathf.Abs(hitParam.scatterAngle));
            Vector3 direction = Quaternion.Euler(0f, randomYaw, 0f) * baseDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = baseDirection;
            direction.Normalize();

            enemy.SetIgnoreChainForCurrentLaunch(true);

            enemy.ReleaseFromScrumAsLaunch(
                direction,
                hitParam.breakHorizontalPower,
                hitParam.breakVerticalPower
            );
        }

        members.Clear();
        state = ScrumState.Finished;
        DestroyRuntimeRoot();
    }

    public void ForceDestroyActiveScrum()
    {
        CancelPendingScrumAttack("ForceDestroyActiveScrum");
        rushAttackResolvedThisRush = true;

        if (debugParam.logState)
            Debug.Log($"{name} Scrum ForceDestroyActiveScrum member:{members.Count}");

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null)
                continue;

            enemy.ForceDestroy();
        }

        members.Clear();
        localSlots.Clear();
        localRotations.Clear();
        state = ScrumState.Finished;
        DestroyRuntimeRoot();
        ownerBoss = null;
        playerTarget = null;
        fixedGatherPoint = Vector3.zero;
        moveDirection = Vector3.zero;
        rushStartPosition = Vector3.zero;
        rushDirectionLocked = false;
        stateTimer = 0f;
        lifeTimer = 0f;
    }

    private void FinishScrum()
    {
        CancelPendingScrumAttack("FinishScrum");
        rushAttackResolvedThisRush = true;

        if (!IsScrumActive)
            return;

        if (debugParam.logState)
            Debug.Log($"{name} Scrum Finished member:{members.Count}");

        for (int i = 0; i < members.Count; i++)
        {
            RugbySummonEnemy enemy = members[i];
            if (enemy == null || !enemy.IsAlive)
                continue;

            enemy.ReleaseFromScrumAsLaunch(
                moveDirection,
                0f,
                0f
            );
        }

        members.Clear();
        state = ScrumState.Finished;
        DestroyRuntimeRoot();
    }

    private void FinishScrumWithoutLaunchingMembers()
    {
        CancelPendingScrumAttack("FinishScrumWithoutLaunchingMembers");
        rushAttackResolvedThisRush = true;

        if (!IsScrumActive)
        {
            members.Clear();
            DestroyRuntimeRoot();
            return;
        }

        members.Clear();
        state = ScrumState.Finished;
        DestroyRuntimeRoot();
    }

    private void CleanupDeadMembers()
    {
        members.RemoveAll(enemy => enemy == null || !enemy.IsAlive);
    }

    private void FaceTargetDuringPreparation()
    {
        if (activeRoot == null)
            return;

        moveDirection = GetDirectionToPlayerOrForward();
        activeRoot.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
    }

    private Vector3 GetDirectionToPlayerOrForward()
    {
        Vector3 origin = activeRoot != null ? activeRoot.position : fixedGatherPoint;
        return GetDirectionFromPointToPlayerOrForward(origin);
    }

    private Vector3 GetDirectionFromPointToPlayerOrForward(Vector3 origin)
    {
        if (playerTarget != null)
        {
            Vector3 direction = playerTarget.position - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                return direction.normalized;
        }

        Vector3 forward = activeRoot != null ? activeRoot.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        return forward.normalized;
    }

    private float GetDistanceToPlayer()
    {
        if (playerTarget == null)
            return float.MaxValue;

        Vector3 a = activeRoot != null ? activeRoot.position : fixedGatherPoint;
        Vector3 b = playerTarget.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool IsJustTackle(BlowPayload blow)
    {
        return blow.tackleType == TackleType.JustNormal ||
               blow.tackleType == TackleType.JustCharge ||
               blow.tackleType.ToString().Contains("Just");
    }

    private bool IsOutsideStageBounds(Vector3 position)
    {
        Vector3 half = rushParam.stageBoundsSize * 0.5f;
        Vector3 min = rushParam.stageBoundsCenter - half;
        Vector3 max = rushParam.stageBoundsCenter + half;

        return position.x < min.x || position.x > max.x ||
               position.z < min.z || position.z > max.z;
    }

    private string GetRootName()
    {
        return activeRootObject != null ? activeRootObject.name : "null";
    }

    private void OnDrawGizmosSelected()
    {
        if (debugParam == null)
            return;

        Transform gizmoRoot = activeRoot != null ? activeRoot : transform;

        if (debugParam.drawFormationGizmos)
        {
            int perRow = Mathf.Max(1, formationParam.membersPerRow);
            int count = Application.isPlaying ? Mathf.Max(members.Count, perRow) : Mathf.Max(perRow * 2, 1);

            Gizmos.color = Color.cyan;
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;
                int countInRow = Mathf.Min(perRow, count - row * perRow);
                float x = (col - (countInRow - 1) * 0.5f) * formationParam.memberSpacingX;
                float z = -row * formationParam.memberSpacingZ;
                Vector3 local = formationParam.localOffset + new Vector3(x, 0f, z);
                Gizmos.DrawWireSphere(gizmoRoot.TransformPoint(local), 0.2f);
            }
        }

        if (debugParam.drawStageBoundsGizmos && rushParam.destroyOutsideStageBounds)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(rushParam.stageBoundsCenter, rushParam.stageBoundsSize);
        }
    }
}
