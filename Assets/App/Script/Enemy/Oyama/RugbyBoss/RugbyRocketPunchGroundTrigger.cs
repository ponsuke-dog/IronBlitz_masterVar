using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class RugbyRocketPunchGroundTrigger : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField]
    [Tooltip("通知先のRugbyRocketPunchObjectです。未設定なら親から自動取得します。")]
    private RugbyRocketPunchObject owner;

    [Header("Trigger")]
    [SerializeField]
    [Tooltip("ONならOnTriggerStayでも通知します。高速移動や初期重なり対策です。")]
    private bool notifyOnStay = true;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("ONならSceneビューにGroundTriggerのCollider範囲を描画します。")]
    private bool drawGizmos = true;

    [SerializeField]
    [Tooltip("Gizmoの色です。")]
    private Color gizmoColor = new Color(0.2f, 0.7f, 1f, 0.65f);

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (owner == null)
            owner = GetComponentInParent<RugbyRocketPunchObject>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;
    }

    private void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
        if (owner == null)
            owner = GetComponentInParent<RugbyRocketPunchObject>();
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    public void Initialize(RugbyRocketPunchObject rocket)
    {
        owner = rocket;
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;
    }

    public void SetTriggerActive(bool active, bool controlCollider)
    {
        if (controlCollider)
        {
            if (triggerCollider == null)
                triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.enabled = active;
        }
        enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyOwner(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!notifyOnStay)
            return;
        NotifyOwner(other);
    }

    private void NotifyOwner(Collider other)
    {
        if (owner == null)
            owner = GetComponentInParent<RugbyRocketPunchObject>();
        if (owner == null)
            return;
        owner.NotifyGroundTriggerHit(other);
    }

    public void DrawDebugGizmos()
    {
        if (!drawGizmos)
            return;
        Collider col = triggerCollider != null ? triggerCollider : GetComponent<Collider>();
        if (col == null)
            return;
        Gizmos.color = gizmoColor;
        DrawColliderGizmo(col);
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugGizmos();
    }

    private void DrawColliderGizmo(Collider col)
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = col.transform.localToWorldMatrix;
        if (col is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            DrawCapsuleApprox(capsule);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        Gizmos.matrix = oldMatrix;
    }

    private void DrawCapsuleApprox(CapsuleCollider capsule)
    {
        Vector3 center = capsule.center;
        float radius = capsule.radius;
        float height = Mathf.Max(capsule.height, radius * 2f);
        Vector3 size;
        switch (capsule.direction)
        {
            case 0:
                size = new Vector3(height, radius * 2f, radius * 2f);
                break;
            case 1:
                size = new Vector3(radius * 2f, height, radius * 2f);
                break;
            default:
                size = new Vector3(radius * 2f, radius * 2f, height);
                break;
        }
        Gizmos.DrawWireCube(center, size);
    }
}
