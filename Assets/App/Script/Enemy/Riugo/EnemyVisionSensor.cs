using UnityEngine;

public class EnemyVisionSensor : MonoBehaviour
{
    [System.Serializable]
    public class SensorParam
    {
        [Header("Normal Vision")]
        public float detectRange = 12f;
        [Range(0f, 360f)] public float viewAngle = 90f;

        [Header("Chase Vision")]
        public float chaseDetectRange = 16f;
        [Range(0f, 360f)] public float chaseViewAngle = 120f;

        [Header("Target Aim")]
        [Tooltip("プレイヤーの足元ではなく少し上を見るための補正")]
        public float targetEyeOffsetY = 1.0f;

        [Header("Scene Debug")]
        public bool drawVisionInScene = true;
        public bool debugRay = false;
    }

    [SerializeField] private Transform eyePoint;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private SensorParam sensorParam = new SensorParam();

    private bool isChasing;

    public float DetectRange => isChasing ? sensorParam.chaseDetectRange : sensorParam.detectRange;
    public float ViewAngle => isChasing ? sensorParam.chaseViewAngle : sensorParam.viewAngle;
    public Transform EyePoint => eyePoint != null ? eyePoint : transform;

    public void SetChasing(bool value)
    {
        isChasing = value;
    }

    public bool CanSeeTarget(Transform self, Transform target)
    {
        if (target == null)
            return false;

        Transform eye = EyePoint;

        Vector3 targetPoint = target.position + Vector3.up * sensorParam.targetEyeOffsetY;
        Vector3 toTarget = targetPoint - eye.position;

        float sqrDist = toTarget.sqrMagnitude;
        if (sqrDist <= 0.0001f)
            return false;

        float detectRange = DetectRange;
        if (sqrDist > detectRange * detectRange)
            return false;

        Vector3 dir = toTarget.normalized;

        // 360度未満のときだけ角度判定を行う
        if (ViewAngle < 359.9f)
        {
            float angle = Vector3.Angle(self.forward, dir);

            if (angle > ViewAngle * 0.5f)
                return false;
        }

        bool blocked = Physics.Linecast(
            eye.position,
            targetPoint,
            out RaycastHit hit,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        if (sensorParam.debugRay)
        {
            Debug.DrawLine(
                eye.position,
                targetPoint,
                blocked ? Color.red : Color.green,
                0.02f
            );

            if (blocked)
            {
                Debug.Log(
                    $"{name} Vision blocked by {hit.collider.name} " +
                    $"layer:{LayerMask.LayerToName(hit.collider.gameObject.layer)}"
                );
            }
        }

        if (blocked)
            return false;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!sensorParam.drawVisionInScene)
            return;

        Transform eye = eyePoint != null ? eyePoint : transform;

        float range = Application.isPlaying ? DetectRange : sensorParam.detectRange;
        float angle = Application.isPlaying ? ViewAngle : sensorParam.viewAngle;

        Gizmos.color = isChasing ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(eye.position, range);

        // 360度の場合は円だけで十分なので扇形線は省略
        if (angle >= 359.9f)
            return;

        Vector3 left = Quaternion.Euler(0f, -angle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, angle * 0.5f, 0f) * transform.forward;

        Gizmos.color = isChasing ? new Color(1f, 0.4f, 0.4f) : Color.cyan;
        Gizmos.DrawLine(eye.position, eye.position + left * range);
        Gizmos.DrawLine(eye.position, eye.position + right * range);
    }
}