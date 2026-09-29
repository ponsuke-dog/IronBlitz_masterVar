using UnityEngine;

public class EnemyDropOnDestroy : MonoBehaviour
{
    #region Serializable Classes

    [System.Serializable]
    private class DropLotteryEntry
    {
        [Header("Drop")]
        [Tooltip("ドロップするPrefab")]
        public GameObject prefab;

        [Tooltip("抽選確率。0～100で指定。合計が100未満なら残りはハズレ")]
        [Range(0f, 100f)]
        public float probability = 0f;

        [Header("Spawn Offset")]
        [Tooltip("敵の位置からどれだけずらして生成するか")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("生成時の回転。ONならPrefabの回転を使う")]
        public bool usePrefabRotation = true;

        [Tooltip("usePrefabRotationがOFFの時に使う回転")]
        public Vector3 rotationEuler = Vector3.zero;
    }

    #endregion

    #region Inspector Fields

    [Header("Drop Lottery")]
    [SerializeField]
    private DropLotteryEntry[] dropEntries;

    [Header("Spawn Position")]
    [Tooltip("ここを設定すると、このTransformの位置を基準にドロップする。未設定ならEnemy自身の位置")]
    [SerializeField]
    private Transform dropPoint;

    [Tooltip("生成位置のYを固定する")]
    [SerializeField]
    private bool overrideSpawnY = false;

    [SerializeField]
    private float spawnY = 0f;

    [Header("Destroy Control")]
    [Tooltip("シーン終了や親の破棄など、アプリ終了時にはドロップさせない")]
    [SerializeField]
    private bool ignoreWhenApplicationQuitting = true;

    [Tooltip("同じEnemyから2回以上ドロップしないようにする")]
    [SerializeField]
    private bool dropOnlyOnce = true;

    [Header("Debug")]
    [SerializeField]
    private bool logDropResult = false;

    #endregion

    #region Runtime Fields

    private bool hasDropped = false;
    private static bool isApplicationQuitting = false;

    #endregion

    #region Unity Events

    private void OnDestroy()
    {
        if (ignoreWhenApplicationQuitting && isApplicationQuitting)
            return;

        TryDrop();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    #endregion

    #region Drop

    public void TryDrop()
    {
        if (dropOnlyOnce && hasDropped)
            return;

        hasDropped = true;

        if (dropEntries == null || dropEntries.Length == 0)
        {
            if (logDropResult)
                Debug.Log($"{name} Drop failed: dropEntries is empty.");

            return;
        }

        float totalProbability = GetTotalProbability();

        if (totalProbability <= 0f)
        {
            if (logDropResult)
                Debug.Log($"{name} Drop failed: totalProbability is 0.");

            return;
        }

        float clampedTotal = Mathf.Min(totalProbability, 100f);

        float roll = Random.Range(0f, 100f);

        if (roll >= clampedTotal)
        {
            if (logDropResult)
            {
                Debug.Log(
                    $"{name} Drop miss. roll:{roll:F2} total:{clampedTotal:F2}"
                );
            }

            return;
        }

        DropLotteryEntry selectedEntry = SelectEntry(roll);

        if (selectedEntry == null || selectedEntry.prefab == null)
        {
            if (logDropResult)
                Debug.Log($"{name} Drop selected null entry.");

            return;
        }

        SpawnDrop(selectedEntry);

        if (logDropResult)
        {
            Debug.Log(
                $"{name} Drop success: {selectedEntry.prefab.name} roll:{roll:F2}"
            );
        }
    }

    private float GetTotalProbability()
    {
        float total = 0f;

        for (int i = 0; i < dropEntries.Length; i++)
        {
            DropLotteryEntry entry = dropEntries[i];

            if (entry == null)
                continue;

            if (entry.prefab == null)
                continue;

            total += Mathf.Max(0f, entry.probability);
        }

        return total;
    }

    private DropLotteryEntry SelectEntry(float roll)
    {
        float current = 0f;

        for (int i = 0; i < dropEntries.Length; i++)
        {
            DropLotteryEntry entry = dropEntries[i];

            if (entry == null)
                continue;

            if (entry.prefab == null)
                continue;

            float probability = Mathf.Max(0f, entry.probability);

            if (probability <= 0f)
                continue;

            current += probability;

            if (roll < current)
                return entry;
        }

        return null;
    }

    private void SpawnDrop(DropLotteryEntry entry)
    {
        Vector3 basePosition = dropPoint != null
            ? dropPoint.position
            : transform.position;

        Vector3 spawnPosition = basePosition + entry.positionOffset;

        if (overrideSpawnY)
            spawnPosition.y = spawnY;

        Quaternion spawnRotation = entry.usePrefabRotation
            ? entry.prefab.transform.rotation
            : Quaternion.Euler(entry.rotationEuler);

        Instantiate(
            entry.prefab,
            spawnPosition,
            spawnRotation
        );
    }

    #endregion
}