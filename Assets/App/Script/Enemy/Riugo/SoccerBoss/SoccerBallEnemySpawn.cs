using UnityEngine;

public class SoccerBallEnemySpawn : MonoBehaviour
{
    [System.Serializable]
    private class SpawnParam
    {
        [Header("Spawn ON / OFF")]
        [Tooltip("着弾後にオブジェクトを生成する")]
        public bool enableSpawn = true;

        [Header("Spawn Object")]
        [Tooltip("着弾後に生成するPrefab")]
        public GameObject spawnPrefab;

        [Tooltip("生成数")]
        public int spawnCount = 1;

        [Tooltip("生成位置のランダム半径")]
        public float spawnRadius = 1.5f;

        [Tooltip("生成位置のY補正")]
        public float yOffset = 0.05f;

        [Tooltip("生成時の向きをランダムにする")]
        public bool randomRotationY = true;

        [Header("Spawn Limit")]
        [Tooltip("Managerによる生成数制限を使う")]
        public bool useSpawnLimitManager = true;

        [Tooltip("ONなら、上限までの残り数だけ生成する。OFFなら上限に届きそうな場合は一切生成しない")]
        public bool spawnOnlyRemainingCount = true;

        [Tooltip("Managerが見つからない時は生成しない。ON推奨")]
        public bool blockSpawnIfManagerNotFound = true;

        [Header("Ground Snap")]
        [Tooltip("生成位置を地面に合わせる")]
        public bool snapToGround = true;

        public LayerMask groundLayer;
        public float groundRayHeight = 5f;
        public float groundRayDistance = 12f;

        [Header("Debug")]
        public bool logSpawn = true;
    }

    [SerializeField] private SpawnParam spawnParam = new SpawnParam();

    public void Spawn(Vector3 center)
    {
        if (!spawnParam.enableSpawn)
            return;

        if (spawnParam.spawnPrefab == null)
            return;

        int requestCount = Mathf.Max(1, spawnParam.spawnCount);
        int allowedCount = requestCount;

        if (spawnParam.useSpawnLimitManager)
        {
            SoccerBallSpawnLimitManager manager = SoccerBallSpawnLimitManager.Instance;

            if (manager == null)
            {
                if (spawnParam.logSpawn)
                    Debug.LogWarning($"{name}: SoccerSpawnLimitManager が見つかりません。");

                if (spawnParam.blockSpawnIfManagerNotFound)
                    return;
            }
            else
            {
                bool canSpawn = manager.TryReserveSpawnCount(
                    requestCount,
                    out allowedCount,
                    spawnParam.spawnOnlyRemainingCount
                );

                if (!canSpawn || allowedCount <= 0)
                {
                    if (spawnParam.logSpawn)
                    {
                        Debug.Log(
                            $"{name}: Spawn skipped. " +
                            $"current:{manager.CurrentCount}/{manager.MaxCount}"
                        );
                    }

                    return;
                }
            }
        }

        int spawned = 0;

        for (int i = 0; i < allowedCount; i++)
        {
            Vector3 pos = BuildSpawnPosition(center);

            Quaternion rot = Quaternion.identity;

            if (spawnParam.randomRotationY)
                rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject obj = Instantiate(spawnParam.spawnPrefab, pos, rot);

            // Prefabに登録コンポーネントが付いていない場合の保険
            EnsureCountTarget(obj);

            spawned++;
        }

        if (spawnParam.logSpawn && spawned > 0)
            Debug.Log($"{name}: Spawned {spawned} object(s).");
    }

    private Vector3 BuildSpawnPosition(Vector3 center)
    {
        Vector2 random = Random.insideUnitCircle * spawnParam.spawnRadius;

        Vector3 pos = center + new Vector3(
            random.x,
            spawnParam.yOffset,
            random.y
        );

        if (spawnParam.snapToGround)
            pos = SnapToGround(pos);

        return pos;
    }

    private void EnsureCountTarget(GameObject obj)
    {
        if (obj == null)
            return;

        if (!spawnParam.useSpawnLimitManager)
            return;

        SoccerBallSpawnLimitManager manager = SoccerBallSpawnLimitManager.Instance;

        if (manager == null)
            return;

        SoccerBallSpawnCountTarget target =
            obj.GetComponent<SoccerBallSpawnCountTarget>();

        if (target == null)
            target = obj.AddComponent<SoccerBallSpawnCountTarget>();

        target.RegisterToManager(manager);
    }

    private Vector3 SnapToGround(Vector3 pos)
    {
        Vector3 rayStart = pos + Vector3.up * spawnParam.groundRayHeight;

        if (Physics.Raycast(
            rayStart,
            Vector3.down,
            out RaycastHit hit,
            spawnParam.groundRayDistance,
            spawnParam.groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            pos.y = hit.point.y + spawnParam.yOffset;
        }

        return pos;
    }
}