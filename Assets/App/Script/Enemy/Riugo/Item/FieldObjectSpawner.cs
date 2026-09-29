using System.Collections;
using UnityEngine;

/// <summary>
/// FieldObjectをSpawnPointから生成し、
/// 自分が生成したFieldObjectがDestroyされたら一定時間後に再生成するスポナー。
/// </summary>
public class FieldObjectSpawner : MonoBehaviour
{
    #region Inspector

    [Header("Spawn Target")]
    [SerializeField]
    [Tooltip("生成するFieldObjectのPrefab")]
    private GameObject fieldObjectPrefab;

    [SerializeField]
    [Tooltip("生成位置の基準点。未設定なら子オブジェクトのSpawnPointを探します")]
    private Transform spawnPoint;

    [SerializeField]
    [Tooltip("生成したFieldObjectの親。未設定ならScene直下に生成します")]
    private Transform spawnedParent;

    [Header("Spawn Timing")]
    [SerializeField]
    [Tooltip("開始時に自動で生成するか")]
    private bool spawnOnStart = true;

    [SerializeField]
    [Tooltip("開始時の生成Delay")]
    private float initialSpawnDelay = 0f;

    [SerializeField]
    [Tooltip("FieldObjectがDestroyされた後、再生成するまでのDelay")]
    private float respawnDelay = 3f;

    [SerializeField]
    [Tooltip("Time.timeScaleの影響を受けずにDelayを進めるか")]
    private bool useUnscaledTime = false;

    [Header("Spawn Transform")]
    [SerializeField]
    [Tooltip("SpawnPointの回転を使うか")]
    private bool useSpawnPointRotation = true;

    [SerializeField]
    [Tooltip("SpawnPointからのローカル位置オフセット")]
    private Vector3 localPositionOffset = Vector3.zero;

    [SerializeField]
    [Tooltip("SpawnPointの回転に追加する回転オフセット")]
    private Vector3 localRotationOffset = Vector3.zero;

    [Header("Respawn Option")]
    [SerializeField]
    [Tooltip("DestroyではなくSetActive(false)になった場合も消えた扱いにするか")]
    private bool respawnWhenInactive = false;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("生成・破壊検知ログを出すか")]
    private bool logDebug = false;

    #endregion

    #region Runtime

    private GameObject currentFieldObject;
    private Coroutine spawnCoroutine;
    private bool spawnerActive;

    #endregion

    #region Unity Events

    private void Reset()
    {
        CreateSpawnPointIfNeeded();
    }

    private void Awake()
    {
        ResolveSpawnPoint();
    }

    private void Start()
    {
        spawnerActive = spawnOnStart;

        if (spawnerActive)
        {
            ScheduleSpawn(initialSpawnDelay);
        }
    }

    private void Update()
    {
        if (!spawnerActive)
            return;

        if (spawnCoroutine != null)
            return;

        if (!IsCurrentFieldObjectLost())
            return;

        if (logDebug)
            Debug.Log($"{name} : FieldObject destroyed. Respawn scheduled.");

        currentFieldObject = null;
        ScheduleSpawn(respawnDelay);
    }

    private void OnDrawGizmosSelected()
    {
        Transform point = spawnPoint != null ? spawnPoint : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(point.position, 0.35f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(point.position, point.position + point.forward * 1.0f);
    }

    #endregion

    #region Spawn

    private void ScheduleSpawn(float delay)
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        spawnCoroutine = StartCoroutine(SpawnRoutine(delay));
    }

    private IEnumerator SpawnRoutine(float delay)
    {
        float timer = Mathf.Max(delay, 0f);

        while (timer > 0f)
        {
            timer -= useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            yield return null;
        }

        spawnCoroutine = null;

        if (!spawnerActive)
            yield break;

        if (!IsCurrentFieldObjectLost())
            yield break;

        SpawnNow();
    }

    public void SpawnNow()
    {
        if (fieldObjectPrefab == null)
        {
            Debug.LogWarning($"{name} : FieldObject Prefabが設定されていません。");
            return;
        }

        ResolveSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogWarning($"{name} : SpawnPointがありません。");
            return;
        }

        if (!IsCurrentFieldObjectLost())
            return;

        Vector3 spawnPosition =
            spawnPoint.position +
            spawnPoint.TransformDirection(localPositionOffset);

        Quaternion baseRotation = useSpawnPointRotation
            ? spawnPoint.rotation
            : fieldObjectPrefab.transform.rotation;

        Quaternion spawnRotation =
            baseRotation * Quaternion.Euler(localRotationOffset);

        currentFieldObject = Instantiate(
            fieldObjectPrefab,
            spawnPosition,
            spawnRotation,
            spawnedParent
        );

        if (logDebug)
            Debug.Log($"{name} : Spawned {currentFieldObject.name}");
    }

    private bool IsCurrentFieldObjectLost()
    {
        if (currentFieldObject == null)
            return true;

        if (respawnWhenInactive && !currentFieldObject.activeInHierarchy)
            return true;

        return false;
    }

    #endregion

    #region Public Control

    public void StartSpawner()
    {
        if (spawnerActive)
            return;

        spawnerActive = true;

        if (IsCurrentFieldObjectLost())
            ScheduleSpawn(initialSpawnDelay);
    }

    public void StopSpawner(bool destroyCurrentObject = false)
    {
        spawnerActive = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (destroyCurrentObject && currentFieldObject != null)
        {
            Destroy(currentFieldObject);
            currentFieldObject = null;
        }
    }

    public void ForceRespawn()
    {
        if (currentFieldObject != null)
        {
            Destroy(currentFieldObject);
            currentFieldObject = null;
        }

        if (spawnerActive)
            ScheduleSpawn(respawnDelay);
    }

    public GameObject CurrentFieldObject => currentFieldObject;

    #endregion

    #region SpawnPoint

    private void ResolveSpawnPoint()
    {
        if (spawnPoint != null)
            return;

        Transform found = transform.Find("SpawnPoint");

        if (found != null)
        {
            spawnPoint = found;
            return;
        }

        // 万が一SpawnPointが無い場合はSpawner自身を基準点にする
        spawnPoint = transform;
    }

    private void CreateSpawnPointIfNeeded()
    {
        Transform found = transform.Find("SpawnPoint");

        if (found != null)
        {
            spawnPoint = found;
            return;
        }

        GameObject point = new GameObject("SpawnPoint");
        point.transform.SetParent(transform);
        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;

        spawnPoint = point.transform;
    }

    #endregion

    #region Validate

    private void OnValidate()
    {
        initialSpawnDelay = Mathf.Max(0f, initialSpawnDelay);
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }

    #endregion
}