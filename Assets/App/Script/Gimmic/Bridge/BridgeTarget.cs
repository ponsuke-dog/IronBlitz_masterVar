using UnityEngine;

public class BridgeTarget : MonoBehaviour
{
    public MeshFilter BridgeMesh;

    public Color GizmoColor = Color.lightGreen;
    
    private void OnDrawGizmos()
    {
        if (BridgeMesh == null)
        {
            return;
        }

        Gizmos.color = GizmoColor;

        Gizmos.DrawWireMesh(
            BridgeMesh.sharedMesh,
            transform.position,
            transform.rotation,
            BridgeMesh.transform.lossyScale
            );
    }
}
