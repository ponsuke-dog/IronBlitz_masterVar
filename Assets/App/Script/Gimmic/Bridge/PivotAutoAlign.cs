using UnityEngine;

public class PivotAutoAlign : MonoBehaviour
{
    public Transform pivot;        // Pivot
    public Transform modelRoot;    // bridge 1
    public bool autoAlignOnStart = true;

    void Start()
    {
        if (autoAlignOnStart)
        {
            AlignPivotToModelRoot();
        }
    }

    public void AlignPivotToModelRoot()
    {
        if (pivot == null || modelRoot == null)
        {
            Debug.LogWarning("pivot または modelRoot が設定されていません");
            return;
        }

        // MeshFilter を探す
        var mf = modelRoot.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("modelRoot に MeshFilter がありません");
            return;
        }

        var mesh = mf.sharedMesh;
        var bounds = mesh.bounds;

        // Mesh の中心（ワールド座標）
        Vector3 worldCenter = mf.transform.TransformPoint(bounds.center);

        // forward 方向（ワールド）
        Vector3 forward = modelRoot.forward;

        // 根本の位置 = 中心 - forward * extents.z
        Vector3 rootPos = worldCenter - forward * bounds.extents.z;

        // pivot を根本に移動
        pivot.position = rootPos;

        // pivot の向きを modelRoot に合わせる（必要なら）
        pivot.rotation = modelRoot.rotation;

        Debug.Log("Pivot を modelRoot の根本に合わせました");
    }
}
