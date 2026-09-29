using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class AutoFitBoxCollider : MonoBehaviour
{
    public Vector3 sizeOffset;
    public Vector3 centerOffset;

    public MeshFilter meshFilter;

#if UNITY_EDITOR
    private void OnValidate()
    {
        Sync();
    }
#endif

    void Sync()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        var mesh = meshFilter.sharedMesh;
        var col = GetComponent<BoxCollider>();

        // ÉTÉCÉYÇÕÇªÇÃÇ‹Ç‹
        Vector3 scaleSize = Vector3.Scale(mesh.bounds.size, meshFilter.transform.lossyScale);
        col.size = scaleSize + sizeOffset;

        Vector3 worldCenter = meshFilter.transform.TransformPoint(mesh.bounds.center);
        Vector3 localCenter = transform.parent.InverseTransformPoint(worldCenter);

        transform.localPosition = localCenter + centerOffset;
    }

}
