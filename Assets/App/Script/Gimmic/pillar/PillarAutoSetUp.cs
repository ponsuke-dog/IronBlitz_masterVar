using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

[ExecuteAlways]
public class PillarAutoSetUp : MonoBehaviour
{
    [Header("橋の大きさ")]
    public Vector3 meshScale = new Vector3(1f, 1f, 1f);

    public Transform meshTransform;

    public MeshFilter meshFilter;
    public Transform pivot;
 //   public Transform target;

    [Header("微調整用")]
    public Vector3 targetOffset;

    // 変更検知用
    private Vector3 lastScale;



#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lastScale == meshScale)
        {
            return;
        }
        lastScale = meshScale;
        Setup();
    }
#endif

    void Setup()
    {

        if (meshFilter == null)
        {
            meshFilter = GetComponentInChildren<MeshFilter>();
        }

        var mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            return;
        }

        if (meshTransform == null)
        {
            meshTransform = meshFilter.transform;
        }

        var bounds = mesh.bounds;

        Vector3 baseSize = bounds.size;

        // メッシュの大きさ調整
        Vector3 scale = new Vector3(
            meshScale.x / baseSize.x,
            meshScale.y / baseSize.y,
            meshScale.z / baseSize.z
            );

        meshTransform.localScale = scale;

        Vector3 scaledCenter = Vector3.Scale(bounds.center, meshTransform.localScale);
        Vector3 scaledExtent = Vector3.Scale(bounds.extents, meshTransform.localScale);

        Vector3 axis = Vector3.forward;
        float halfLength = scaledExtent.z;



        if (pivot == null)
        {
            pivot = transform.Find("Pivot");
        }

        if (meshFilter == null || pivot == null)
        {
            return;
        }


        // ローカルで端を計算              
        Vector3 localPivot = scaledCenter;
        //Vector3 localTarget = localCenter + axis * halfLength;

        // 中心から下面に移動
        localPivot -= Vector3.up * scaledExtent.y;
        Debug.Log("pivotの位置変更");
        pivot.localPosition = localPivot;

        meshTransform.localPosition = -localPivot;

    }

}
