using UnityEngine;

/// <summary>
/// 回復アイテムなどを一定間隔で自動生成するスポナー。
/// 生成したアイテムがまだ残っている間は再生成しない。
/// 複数Prefabを登録した場合はランダムに1つ選んで生成する。
/// 生成間隔も複数登録でき、その中からランダムに選ばれる。
/// </summary>
public sealed class AutoItemSpawner : MonoBehaviour
{
    [Header("Spawn Prefabs")]
    [Tooltip("自動生成するPrefab候補です。複数登録した場合、この中からランダムで1つ選ばれます。回復アイテム、弾薬、バフアイテムなどを設定できます。")]
    [SerializeField] private GameObject[] spawnPrefabs;

    [Header("Spawn Point")]
    [Tooltip("生成位置として使うTransformです。未設定の場合、このスポナー自身のTransform位置に生成します。")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("trueにするとSpawn Pointの回転を使って生成します。falseの場合はQuaternion.identityで生成します。")]
    [SerializeField] private bool useSpawnPointRotation = true;

    [Tooltip("生成位置に加算するワールド座標オフセットです。地面から少し浮かせたい場合などに使います。")]
    [SerializeField] private Vector3 worldPositionOffset = Vector3.zero;

    [Header("Spawn Timing")]
    [Tooltip("生成間隔の候補です。複数設定した場合、この中からランダムで1つ選ばれます。初回生成にもこの時間が反映されるため、起動直後に即生成されません。")]
    [SerializeField] private float[] spawnIntervals = new float[] { 5.0f };

    [Tooltip("trueにすると、同じ生成間隔が連続で選ばれにくくなります。Spawn Intervalsが2つ以上ある場合のみ有効です。")]
    [SerializeField] private bool avoidSameIntervalConsecutive = false;

    [Tooltip("trueにすると、生成済みアイテムが消えた瞬間から次のランダム生成間隔を数え直します。基本はtrue推奨です。")]
    [SerializeField] private bool resetTimerWhenCurrentItemDestroyed = true;

    [Header("Random Prefab")]
    [Tooltip("trueにすると、同じPrefabが連続で選ばれにくくなります。Prefab候補が2つ以上ある場合のみ有効です。")]
    [SerializeField] private bool avoidSamePrefabConsecutive = false;

    [Header("Time")]
    [Tooltip("trueにするとTime.timeScaleの影響を受けないTime.unscaledDeltaTimeでタイマーを進めます。ポーズ中でも生成時間を進めたい場合に使います。")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("trueにすると、このGameObjectに付いているTimeAgentのTimeScaleを使用します。ボスやプレイヤーと同じ独自タイムスケール管理をしたい場合に有効です。")]
    [SerializeField] private bool useTimeAgent = true;

    [Tooltip("trueにすると、TimeAgentが見つからない場合に親階層からもTimeAgentを探します。スポナーを子オブジェクトに置く場合に便利です。")]
    [SerializeField] private bool searchTimeAgentInParent = true;

    [Header("Current Spawned Object")]
    [Tooltip("現在生成されているアイテムです。基本的には自動設定されます。デバッグ確認用です。")]
    [SerializeField] private GameObject currentSpawnedObject;

    [Header("Debug")]
    [Tooltip("trueにすると、生成失敗、Prefab未設定、生成間隔選択などをログ出力します。調整中はtrue、本番ではfalse推奨です。")]
    [SerializeField] private bool enableDebugLog = false;

    [Tooltip("trueにすると、Sceneビューに生成位置のGizmoを表示します。")]
    [SerializeField] private bool drawGizmos = true;

    [Tooltip("Gizmo表示の半径です。")]
    [SerializeField, Min(0.01f)] private float gizmoRadius = 0.3f;

    private TimeAgent timeAgent;

    private float spawnTimer;
    private int lastSpawnPrefabIndex = -1;
    private int lastSpawnIntervalIndex = -1;
    private bool hadSpawnedObjectLastFrame;

    private void Awake()
    {
        ResolveTimeAgent();

        /*
         * 初回生成もランダム生成間隔を反映する。
         * そのため起動時に即生成せず、ここで選ばれた時間だけ待ってから生成する。
         */
        ResetSpawnTimerByRandomInterval();

        hadSpawnedObjectLastFrame = currentSpawnedObject != null;
    }

    private void OnEnable()
    {
        ResolveTimeAgent();
    }

    private void Update()
    {
        RefreshCurrentObjectState();

        if (currentSpawnedObject != null)
        {
            return;
        }

        float deltaTime = GetDeltaTime();

        if (deltaTime <= 0.0f)
        {
            return;
        }

        spawnTimer -= deltaTime;

        if (spawnTimer > 0.0f)
        {
            return;
        }

        TrySpawn();

        /*
         * Prefab未設定などで生成に失敗した場合も、
         * 毎フレーム生成試行し続けないように次のランダム間隔を設定する。
         */
        if (currentSpawnedObject == null)
        {
            ResetSpawnTimerByRandomInterval();
        }
    }

    /// <summary>
    /// 外部から即座に生成を試みる。
    /// 既に生成済みアイテムが残っている場合は何もしない。
    /// この関数は即時生成用なので、通常の自動生成タイマーとは別扱い。
    /// </summary>
    public void TrySpawn()
    {
        if (currentSpawnedObject != null)
        {
            return;
        }

        GameObject prefab = GetRandomPrefab();

        if (prefab == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[AutoItemSpawner] Spawn prefab is not set.", this);
            }

            return;
        }

        Vector3 position = GetSpawnPosition();
        Quaternion rotation = GetSpawnRotation();

        currentSpawnedObject = Instantiate(prefab, position, rotation);
        hadSpawnedObjectLastFrame = currentSpawnedObject != null;

        /*
         * 生成したアイテムが残っている間はタイマーを使わない。
         * ただし、後で何らかの理由で参照が外れた場合に備えて次の待ち時間は設定しておく。
         */
        ResetSpawnTimerByRandomInterval();

        if (enableDebugLog)
        {
            Debug.Log(
                $"[AutoItemSpawner] Spawned item. prefab={prefab.name}, position={position}",
                this
            );
        }
    }

    /// <summary>
    /// 現在生成されているアイテムを破棄する。
    /// 破棄後、次のランダム生成間隔を数え直す。
    /// </summary>
    public void DestroyCurrentSpawnedObject()
    {
        if (currentSpawnedObject == null)
        {
            return;
        }

        Destroy(currentSpawnedObject);
        currentSpawnedObject = null;
        hadSpawnedObjectLastFrame = false;

        ResetSpawnTimerByRandomInterval();
    }

    /// <summary>
    /// 生成タイマーをランダム生成間隔でリセットする。
    /// すでにアイテムが存在する場合、そのアイテムは消さない。
    /// </summary>
    public void ResetSpawnTimer()
    {
        ResetSpawnTimerByRandomInterval();
    }

    /// <summary>
    /// 次のUpdateで即生成可能な状態にする。
    /// すでにアイテムが存在する場合は生成されない。
    /// デバッグやイベント報酬用。
    /// </summary>
    public void ForceReadyToSpawn()
    {
        spawnTimer = 0.0f;
    }

    /// <summary>
    /// 現在生成済みのアイテム参照を外部から設定する。
    /// すでにシーン上に置いてある回復アイテムを初期状態として扱いたい場合に使える。
    /// </summary>
    public void SetCurrentSpawnedObject(GameObject spawnedObject)
    {
        currentSpawnedObject = spawnedObject;
        hadSpawnedObjectLastFrame = currentSpawnedObject != null;

        if (currentSpawnedObject == null)
        {
            ResetSpawnTimerByRandomInterval();
        }
    }

    private void RefreshCurrentObjectState()
    {
        bool hasCurrentObject = currentSpawnedObject != null;

        if (hadSpawnedObjectLastFrame && !hasCurrentObject)
        {
            if (resetTimerWhenCurrentItemDestroyed)
            {
                ResetSpawnTimerByRandomInterval();
            }

            if (enableDebugLog)
            {
                Debug.Log("[AutoItemSpawner] Current spawned item was destroyed.", this);
            }
        }

        hadSpawnedObjectLastFrame = hasCurrentObject;
    }

    private void ResetSpawnTimerByRandomInterval()
    {
        float interval = GetRandomSpawnInterval();
        spawnTimer = Mathf.Max(0.01f, interval);

        if (enableDebugLog)
        {
            Debug.Log($"[AutoItemSpawner] Next spawn interval = {spawnTimer:0.00}", this);
        }
    }

    private float GetRandomSpawnInterval()
    {
        if (spawnIntervals == null || spawnIntervals.Length == 0)
        {
            return 5.0f;
        }

        int validCount = 0;

        for (int i = 0; i < spawnIntervals.Length; i++)
        {
            if (spawnIntervals[i] > 0.0f && float.IsFinite(spawnIntervals[i]))
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return 5.0f;
        }

        if (validCount == 1)
        {
            for (int i = 0; i < spawnIntervals.Length; i++)
            {
                if (spawnIntervals[i] > 0.0f && float.IsFinite(spawnIntervals[i]))
                {
                    lastSpawnIntervalIndex = i;
                    return spawnIntervals[i];
                }
            }

            return 5.0f;
        }

        int selectedIndex = -1;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            int index = Random.Range(0, spawnIntervals.Length);

            if (spawnIntervals[index] <= 0.0f || !float.IsFinite(spawnIntervals[index]))
            {
                continue;
            }

            if (avoidSameIntervalConsecutive && index == lastSpawnIntervalIndex)
            {
                continue;
            }

            selectedIndex = index;
            break;
        }

        if (selectedIndex < 0)
        {
            for (int i = 0; i < spawnIntervals.Length; i++)
            {
                if (spawnIntervals[i] <= 0.0f || !float.IsFinite(spawnIntervals[i]))
                {
                    continue;
                }

                if (avoidSameIntervalConsecutive && i == lastSpawnIntervalIndex)
                {
                    continue;
                }

                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            for (int i = 0; i < spawnIntervals.Length; i++)
            {
                if (spawnIntervals[i] > 0.0f && float.IsFinite(spawnIntervals[i]))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        if (selectedIndex < 0)
        {
            return 5.0f;
        }

        lastSpawnIntervalIndex = selectedIndex;
        return spawnIntervals[selectedIndex];
    }

    private GameObject GetRandomPrefab()
    {
        if (spawnPrefabs == null || spawnPrefabs.Length == 0)
        {
            return null;
        }

        int validCount = 0;

        for (int i = 0; i < spawnPrefabs.Length; i++)
        {
            if (spawnPrefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        if (validCount == 1)
        {
            for (int i = 0; i < spawnPrefabs.Length; i++)
            {
                if (spawnPrefabs[i] != null)
                {
                    lastSpawnPrefabIndex = i;
                    return spawnPrefabs[i];
                }
            }

            return null;
        }

        int selectedIndex = -1;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            int index = Random.Range(0, spawnPrefabs.Length);

            if (spawnPrefabs[index] == null)
            {
                continue;
            }

            if (avoidSamePrefabConsecutive && index == lastSpawnPrefabIndex)
            {
                continue;
            }

            selectedIndex = index;
            break;
        }

        if (selectedIndex < 0)
        {
            for (int i = 0; i < spawnPrefabs.Length; i++)
            {
                if (spawnPrefabs[i] == null)
                {
                    continue;
                }

                if (avoidSamePrefabConsecutive && i == lastSpawnPrefabIndex)
                {
                    continue;
                }

                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            for (int i = 0; i < spawnPrefabs.Length; i++)
            {
                if (spawnPrefabs[i] != null)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        if (selectedIndex < 0)
        {
            return null;
        }

        lastSpawnPrefabIndex = selectedIndex;
        return spawnPrefabs[selectedIndex];
    }

    private Vector3 GetSpawnPosition()
    {
        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        return targetSpawnPoint.position + worldPositionOffset;
    }

    private Quaternion GetSpawnRotation()
    {
        if (!useSpawnPointRotation)
        {
            return Quaternion.identity;
        }

        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        return targetSpawnPoint.rotation;
    }

    private float GetDeltaTime()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (useTimeAgent)
        {
            if (timeAgent == null)
            {
                ResolveTimeAgent();
            }

            if (timeAgent != null)
            {
                deltaTime *= timeAgent.TimeScale;
            }
        }

        if (!float.IsFinite(deltaTime))
        {
            return 0.0f;
        }

        return Mathf.Max(0.0f, deltaTime);
    }

    private void ResolveTimeAgent()
    {
        if (!useTimeAgent)
        {
            timeAgent = null;
            return;
        }

        timeAgent = GetComponent<TimeAgent>();

        if (timeAgent == null && searchTimeAgentInParent)
        {
            timeAgent = GetComponentInParent<TimeAgent>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (spawnIntervals == null || spawnIntervals.Length == 0)
        {
            spawnIntervals = new float[] { 5.0f };
        }

        for (int i = 0; i < spawnIntervals.Length; i++)
        {
            if (!float.IsFinite(spawnIntervals[i]) || spawnIntervals[i] < 0.01f)
            {
                spawnIntervals[i] = 0.01f;
            }
        }

        if (gizmoRadius < 0.01f)
        {
            gizmoRadius = 0.01f;
        }
    }
#endif

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = currentSpawnedObject != null ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(GetSpawnPosition(), gizmoRadius);

        Gizmos.color = Color.white;
        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        Gizmos.DrawLine(targetSpawnPoint.position, GetSpawnPosition());
    }
}