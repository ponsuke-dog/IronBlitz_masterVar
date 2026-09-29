using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public enum EnemySpawnType
    {
        Patrol,
        Missile
    }

    [System.Serializable]
    private class CommonSpawnParam
    {
        [Header("Spawn")]
        [Tooltip("開始時に自動でスポーンする")]
        public bool spawnOnStart = true;

        [Tooltip("SpawnPointが未設定なら子にSpawnPointを自動作成する")]
        public bool createSpawnPointIfMissing = true;

        [Tooltip("初回スポーンまでの待ち時間")]
        public float initialSpawnDelay = 0f;

        [Tooltip("Destroyされたら再スポーンする")]
        public bool respawnOnDestroyed = true;

        [Tooltip("Destroy確認後、再スポーンするまでの待ち時間")]
        public float respawnDelay = 2f;

        [Tooltip("生成したEnemyをこのSpawnerの子にする")]
        public bool parentToSpawner = false;

        [Tooltip("SpawnPointの回転を使用する")]
        public bool useSpawnPointRotation = true;
    }

    [System.Serializable]
    private class PatrolSpawnParam
    {
        [Header("Prefab")]
        public EnemyControllerTypePatroler prefab;

        [Header("Route")]
        public EnemyPatrolRoute homeRoute;
        public EnemyPatrolRouteManager routeManager;

        [Header("Target")]
        public Transform playerTarget;

        [Header("Initialize")]
        [Tooltip("Spawn後にPatrol状態へ入り直す")]
        public bool restartPatrolOnSpawn = true;
    }

    [System.Serializable]
    private class MissileSpawnParam
    {
        [Header("Prefab")]
        public EnemyControllerTypeMissile prefab;

        [Header("Target")]
        public Transform playerTarget;
    }

    [Header("Enemy Type")]
    [SerializeField] private EnemySpawnType enemyType = EnemySpawnType.Patrol;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Common Settings")]
    [SerializeField] private CommonSpawnParam commonParam = new CommonSpawnParam();

    [Header("Patrol Enemy Settings")]
    [SerializeField] private PatrolSpawnParam patrolParam = new PatrolSpawnParam();

    [Header("Missile Enemy Settings")]
    [SerializeField] private MissileSpawnParam missileParam = new MissileSpawnParam();

    [Header("Debug")]
    [SerializeField] private bool logSpawn = false;

    private GameObject currentEnemyObject;
    private Coroutine spawnRoutine;
    private Coroutine watchRoutine;

    private void Reset()
    {
        EnsureSpawnPoint();
    }

    private void Awake()
    {
        EnsureSpawnPoint();
    }

    private void Start()
    {
        if (commonParam.spawnOnStart)
            RequestSpawn(commonParam.initialSpawnDelay);
    }

    private void EnsureSpawnPoint()
    {
        if (!commonParam.createSpawnPointIfMissing)
            return;

        if (spawnPoint != null)
            return;

        Transform found = transform.Find("SpawnPoint");

        if (found != null)
        {
            spawnPoint = found;
            return;
        }

        GameObject point = new GameObject("SpawnPoint");
        spawnPoint = point.transform;
        spawnPoint.SetParent(transform);
        spawnPoint.localPosition = Vector3.zero;
        spawnPoint.localRotation = Quaternion.identity;
        spawnPoint.localScale = Vector3.one;
    }

    public void RequestSpawn(float delay = 0f)
    {
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnRoutine(delay));
    }

    private IEnumerator SpawnRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SpawnEnemy();

        spawnRoutine = null;
    }

    private void SpawnEnemy()
    {
        if (currentEnemyObject != null)
        {
            if (logSpawn)
                Debug.Log($"{name} EnemySpawner: enemy already exists.");

            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;

        Vector3 position = point.position;
        Quaternion rotation = commonParam.useSpawnPointRotation
            ? point.rotation
            : Quaternion.identity;

        Transform parent = commonParam.parentToSpawner ? transform : null;

        switch (enemyType)
        {
            case EnemySpawnType.Patrol:
                SpawnPatrolEnemy(position, rotation, parent);
                break;

            case EnemySpawnType.Missile:
                SpawnMissileEnemy(position, rotation, parent);
                break;
        }

        if (currentEnemyObject != null && commonParam.respawnOnDestroyed)
        {
            if (watchRoutine != null)
                StopCoroutine(watchRoutine);

            watchRoutine = StartCoroutine(WatchDestroyedRoutine());
        }
    }

    private void SpawnPatrolEnemy(Vector3 position, Quaternion rotation, Transform parent)
    {
        if (patrolParam.prefab == null)
        {
            Debug.LogWarning($"{name} EnemySpawner: Patrol prefab is null.");
            return;
        }

        EnemyControllerTypePatroler enemy = Instantiate(
            patrolParam.prefab,
            position,
            rotation,
            parent
        );

        enemy.InitializeFromSpawner(
            patrolParam.homeRoute,
            patrolParam.routeManager,
            patrolParam.playerTarget,
            patrolParam.restartPatrolOnSpawn
        );

        currentEnemyObject = enemy.gameObject;

        if (logSpawn)
            Debug.Log($"{name} Spawn PatrolEnemy: {enemy.name}");
    }

    private void SpawnMissileEnemy(Vector3 position, Quaternion rotation, Transform parent)
    {
        if (missileParam.prefab == null)
        {
            Debug.LogWarning($"{name} EnemySpawner: Missile prefab is null.");
            return;
        }

        EnemyControllerTypeMissile enemy = Instantiate(
            missileParam.prefab,
            position,
            rotation,
            parent
        );

        enemy.InitializeFromSpawner(missileParam.playerTarget);

        currentEnemyObject = enemy.gameObject;

        if (logSpawn)
            Debug.Log($"{name} Spawn MissileEnemy: {enemy.name}");
    }

    private IEnumerator WatchDestroyedRoutine()
    {
        while (currentEnemyObject != null)
            yield return null;

        currentEnemyObject = null;
        watchRoutine = null;

        if (commonParam.respawnOnDestroyed)
            RequestSpawn(commonParam.respawnDelay);
    }

    public void ForceRespawn()
    {
        if (currentEnemyObject != null)
            Destroy(currentEnemyObject);

        currentEnemyObject = null;

        if (watchRoutine != null)
        {
            StopCoroutine(watchRoutine);
            watchRoutine = null;
        }

        RequestSpawn(0f);
    }

    public void DestroyCurrentEnemy()
    {
        if (currentEnemyObject != null)
            Destroy(currentEnemyObject);

        currentEnemyObject = null;
    }

    public bool HasAliveEnemy()
    {
        return currentEnemyObject != null;
    }
}