using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class EnemyVisionDebugRenderer : MonoBehaviour
{
    [System.Serializable]
    public class DebugParam
    {
        [Header("Visibility")]
        public bool drawInGame = true;

        [Header("Mesh Shape")]
        [Min(6)] public int meshSegments = 24;
        public float heightOffset = 0.02f;

        [Header("Colors")]
        public Color normalColor = new Color(1f, 1f, 0f, 0.18f);
        public Color chaseColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    }

    [SerializeField] private DebugParam debugParam = new DebugParam();

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh runtimeMesh;

    private Transform owner;
    private Transform eyePoint;

    private float currentRange;
    private float currentAngle;
    private bool currentIsChasing;
    private bool isInitialized;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        runtimeMesh = new Mesh();
        runtimeMesh.name = "EnemyVisionRuntimeMesh";
        meshFilter.mesh = runtimeMesh;

#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        if (meshRenderer != null)
            meshRenderer.enabled = false;
#endif
    }

    public void Initialize(Transform ownerTransform, Transform eyeTransform)
    {
        owner = ownerTransform;
        eyePoint = eyeTransform != null ? eyeTransform : ownerTransform;
        isInitialized = owner != null;
    }

    public void SetVision(float range, float angle, bool isChasing)
    {
        currentRange = range;
        currentAngle = angle;
        currentIsChasing = isChasing;
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isInitialized)
        {
            if (transform.parent != null)
            {
                owner = transform.parent;
                eyePoint = owner;
                isInitialized = true;
            }
            else
            {
                return;
            }
        }

        if (meshRenderer == null || runtimeMesh == null)
            return;

        meshRenderer.enabled = debugParam.drawInGame;
        if (!debugParam.drawInGame)
            return;

        if (currentRange <= 0.01f || currentAngle <= 0.01f)
            return;

        UpdateMaterialColor();
        UpdateMesh();
#endif
    }

    private void UpdateMaterialColor()
    {
        if (meshRenderer.sharedMaterial == null)
            return;

        meshRenderer.sharedMaterial.color = currentIsChasing
            ? debugParam.chaseColor
            : debugParam.normalColor;
    }

    private void UpdateMesh()
    {
        int segments = Mathf.Max(6, debugParam.meshSegments);

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        Transform eye = eyePoint != null ? eyePoint : owner;
        if (eye == null || owner == null)
            return;

        Vector3 centerWorld =
            eye.position + Vector3.up * debugParam.heightOffset;

        vertices[0] = transform.InverseTransformPoint(centerWorld);

        float startAngle = -currentAngle * 0.5f;
        float step = currentAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + step * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * owner.forward;
            Vector3 pointWorld = centerWorld + dir * currentRange;

            vertices[i + 1] = transform.InverseTransformPoint(pointWorld);
        }

        for (int i = 0; i < segments; i++)
        {
            int tri = i * 3;
            triangles[tri] = 0;
            triangles[tri + 1] = i + 1;
            triangles[tri + 2] = i + 2;
        }

        runtimeMesh.Clear();
        runtimeMesh.vertices = vertices;
        runtimeMesh.triangles = triangles;
        runtimeMesh.RecalculateNormals();
        runtimeMesh.RecalculateBounds();
    }
}