using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class BridgeAutoSetUp : MonoBehaviour
{
    public Transform bridgeUnit;
    public Transform bridgeRoot;
    public float desiredLength = 10f;

    private float lastLength = -1f;
    private bool pending = false;

    public Transform meshTransform;
    public MeshFilter meshFilter;
    public Transform pivot;

#if UNITY_EDITOR

    private void OnValidate()
    {
        // Prefabモードでは動かさない
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;

        if (!Mathf.Approximately(lastLength, desiredLength))
        {
            lastLength = desiredLength;
            ScheduleRebuild();
        }
    }

    void ScheduleRebuild()
    {
        if (pending) return;
        pending = true;

        EditorApplication.delayCall += () =>
        {
            pending = false;

            // Prefabモードなら絶対に実行しない
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;

            if (this == null) return;

            RebuildBridge();
            AdjustPivotOnly();
        };
    }

    void RebuildBridge()
    {
        if (bridgeUnit == null || bridgeRoot == null)
        {
            Debug.LogWarning("bridgeUnit または bridgeRoot が null");
            return;
        }

        var mf = bridgeUnit.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("MeshFilter が見つからない");
            return;
        }

        float unitLength = mf.sharedMesh.bounds.size.z;

        while (bridgeRoot.childCount > 0)
        {
            DestroyImmediate(bridgeRoot.GetChild(0).gameObject);
        }

        int count = Mathf.CeilToInt(desiredLength / unitLength);
        Vector3 dir = bridgeUnit.forward;

        for (int i = 0; i < count; i++)
        {
            // InstantiatePrefab は使わない（Prefabモードで壊れる）
            var b = Instantiate(bridgeUnit, bridgeRoot);

            if (b == null)
            {
                Debug.LogError("Instantiate が null を返しました（Prefabモードの可能性）");
                return;
            }

            b.position = bridgeRoot.TransformPoint(Vector3.forward * (unitLength * i));
            b.rotation = bridgeUnit.rotation;


        }
    }

    void AdjustPivotOnly()
    {
        if (meshFilter == null) return;
        if (pivot == null) return;

        var mesh = meshFilter.sharedMesh;
        var bounds = mesh.bounds;

        // スケール後の中心と extents を計算
        Vector3 scaledCenter = Vector3.Scale(bounds.center, meshTransform.localScale);
        Vector3 scaledExtent = Vector3.Scale(bounds.extents, meshTransform.localScale);

        // pivot をメッシュの根本に移動（中心 - 下方向）
        Vector3 localPivot = scaledCenter - Vector3.forward * scaledExtent.z;

        pivot.localPosition = localPivot;
        meshTransform.localPosition = -localPivot;
    }

#endif
}