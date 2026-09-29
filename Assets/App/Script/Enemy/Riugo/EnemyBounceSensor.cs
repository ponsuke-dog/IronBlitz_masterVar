using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyBounceSensor : MonoBehaviour
{
    [System.Serializable]
    public class SensorParam
    {
        [Header("Layers")]
        public LayerMask bounceLayer;

        [Header("Surface Detect")]
        [Range(0f, 1f)] public float floorNormalYThreshold = 0.55f;
        [Range(0f, 1f)] public float wallNormalYThreshold = 0.35f;
        [Range(0f, 1f)] public float ceilingNormalYThreshold = 0.55f;
        [Range(0f, 1f)] public float slopeFloorAssistYThreshold = 0.15f;

        [Header("Trigger Control")]
        public bool useTriggerStay = true;
        public float notifyCooldown = 0.03f;

        [Header("Debug")]
        public bool drawDebug = false;
        public bool logCollision = true;
        public bool logIgnoredReason = true;
        public bool logSurfaceDetail = true;
        public bool logMotorState = true;
    }

    [SerializeField] private EnemyNavMotor motor;
    [SerializeField] private EnemyControllerTypePatroler owner;
    [SerializeField] private SensorParam sensorParam = new SensorParam();

    private Collider selfCollider;
    private float lastNotifyTime = -999f;

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();

        if (motor == null)
            motor = GetComponentInParent<EnemyNavMotor>();

        if (owner == null)
            owner = GetComponentInParent<EnemyControllerTypePatroler>();

        if (selfCollider != null)
            selfCollider.isTrigger = true;

        if (sensorParam.logCollision)
        {
            Debug.Log(
                $"[{name}] BounceSensor Awake\n" +
                $"- motor: {(motor != null ? motor.name : "null")}\n" +
                $"- selfCollider: {(selfCollider != null ? selfCollider.GetType().Name : "null")}\n" +
                $"- isTrigger: {(selfCollider != null ? selfCollider.isTrigger.ToString() : "null")}\n" +
                $"- bounceLayerMask: {sensorParam.bounceLayer.value}\n" +
                $"- floorThreshold: {sensorParam.floorNormalYThreshold:F2}\n" +
                $"- wallThreshold: {sensorParam.wallNormalYThreshold:F2}\n" +
                $"- ceilingThreshold: {sensorParam.ceilingNormalYThreshold:F2}\n" +
                $"- slopeAssistThreshold: {sensorParam.slopeFloorAssistYThreshold:F2}\n" +
                $"- notifyCooldown: {sensorParam.notifyCooldown:F3}"
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sensorParam.logCollision)
        {
            Debug.Log(
                $"[{name}] OnTriggerEnter\n" +
                $"- target: {other.name}\n" +
                $"- targetLayer: {LayerMask.LayerToName(other.gameObject.layer)} ({other.gameObject.layer})\n" +
                $"- targetCollider: {other.GetType().Name}\n" +
                $"- selfPos: {transform.position}\n" +
                $"- otherBoundsCenter: {other.bounds.center}"
            );
        }



        TryNotifyBounce(other, "Enter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!sensorParam.useTriggerStay)
            return;

        if (sensorParam.logCollision)
        {
            Debug.Log(
                $"[{name}] OnTriggerStay\n" +
                $"- target: {other.name}\n" +
                $"- targetLayer: {LayerMask.LayerToName(other.gameObject.layer)} ({other.gameObject.layer})\n" +
                $"- selfPos: {transform.position}\n" +
                $"- otherBoundsCenter: {other.bounds.center}"
            );
        }

        TryNotifyBounce(other, "Stay");
    }

    private void TryNotifyBounce(Collider other, string phase)
    {
        if (motor == null)
        {
            if (sensorParam.logIgnoredReason)
                Debug.LogWarning($"[{name}] BounceSensor[{phase}] ignored: motor is null");
            return;
        }

        if (sensorParam.logMotorState)
        {
            Debug.Log(
                $"[{name}] BounceSensor[{phase}] MotorState\n" +
                $"- IsFlying: {motor.IsFlying}\n" +
                $"- HasLandedOnce: {motor.HasLandedOnce}\n" +
                $"- CurrentPlanarSpeed: {motor.CurrentPlanarSpeed:F3}\n" +
                $"- CurrentTotalVelocity: {motor.CurrentTotalVelocity}\n" +
                $"- LastBlowDirection: {motor.LastBlowDirection}\n" +
                $"- TimeNow: {Time.time:F3}\n" +
                $"- LastNotifyTime: {lastNotifyTime:F3}\n" +
                $"- CooldownRemain: {Mathf.Max(0f, (lastNotifyTime + sensorParam.notifyCooldown) - Time.time):F3}"
            );
        }

        if (!motor.IsFlying)
        {
            if (sensorParam.logIgnoredReason)
                Debug.Log($"[{name}] BounceSensor[{phase}] ignored: motor.IsFlying == false");
            return;
        }

        if (Time.time < lastNotifyTime + sensorParam.notifyCooldown)
        {
            if (sensorParam.logIgnoredReason)
            {
                Debug.Log(
                    $"[{name}] BounceSensor[{phase}] ignored: notifyCooldown\n" +
                    $"- now: {Time.time:F3}\n" +
                    $"- nextAllowed: {(lastNotifyTime + sensorParam.notifyCooldown):F3}"
                );
            }
            return;
        }

        int otherLayerMaskBit = 1 << other.gameObject.layer;
        bool layerMatched = (otherLayerMaskBit & sensorParam.bounceLayer.value) != 0;

        if (!layerMatched)
        {
            if (sensorParam.logIgnoredReason)
            {
                Debug.Log(
                    $"[{name}] BounceSensor[{phase}] ignored: bounceLayer mismatch\n" +
                    $"- target: {other.name}\n" +
                    $"- targetLayer: {LayerMask.LayerToName(other.gameObject.layer)} ({other.gameObject.layer})\n" +
                    $"- targetLayerBit: {otherLayerMaskBit}\n" +
                    $"- bounceLayerMask: {sensorParam.bounceLayer.value}"
                );
            }
            return;
        }

        Vector3 fallbackTravelDir = motor.CurrentTotalVelocity;
        if (fallbackTravelDir.sqrMagnitude < 0.0001f)
            fallbackTravelDir = motor.LastBlowDirection;

        if (sensorParam.logSurfaceDetail)
        {
            Debug.Log(
                $"[{name}] BounceSensor[{phase}] SurfaceInput\n" +
                $"- fallbackTravelDir: {fallbackTravelDir}\n" +
                $"- selfPosition: {transform.position}\n" +
                $"- otherClosestPointToSelf: {other.ClosestPoint(transform.position)}\n" +
                $"- otherBoundsCenter: {other.bounds.center}"
            );
        }

        bool success = EnemySurfaceUtility.TryBuildSurfaceHit(
            selfCollider,
            other,
            transform.position,
            fallbackTravelDir,
            sensorParam.floorNormalYThreshold,
            sensorParam.wallNormalYThreshold,
            sensorParam.ceilingNormalYThreshold,
            sensorParam.slopeFloorAssistYThreshold,
            out EnemySurfaceHit surfaceHit
        );

        if (!success)
        {
            if (sensorParam.logIgnoredReason)
            {
                Debug.LogWarning(
                    $"[{name}] BounceSensor[{phase}] ignored: TryBuildSurfaceHit failed\n" +
                    $"- target: {other.name}\n" +
                    $"- selfCollider: {(selfCollider != null ? selfCollider.GetType().Name : "null")}\n" +
                    $"- fallbackTravelDir: {fallbackTravelDir}"
                );
            }
            return;
        }

        float upDot = Vector3.Dot(surfaceHit.normal.normalized, Vector3.up);

        if (sensorParam.logSurfaceDetail)
        {
            Debug.Log(
                $"[{name}] BounceSensor[{phase}] SurfaceBuilt\n" +
                $"- target: {other.name}\n" +
                $"- normal: {surfaceHit.normal}\n" +
                $"- upDot: {upDot:F4}\n" +
                $"- classifiedKind: {surfaceHit.kind}\n" +
                $"- floorThreshold: {sensorParam.floorNormalYThreshold:F2}\n" +
                $"- wallThreshold: {sensorParam.wallNormalYThreshold:F2}\n" +
                $"- ceilingThreshold: {sensorParam.ceilingNormalYThreshold:F2}\n" +
                $"- slopeAssistThreshold: {sensorParam.slopeFloorAssistYThreshold:F2}"
            );
        }

        lastNotifyTime = Time.time;

        if (sensorParam.logCollision)
        {
            Debug.Log(
                $"[{name}] BounceSensor[{phase}] NotifyBounce\n" +
                $"- target: {other.name}\n" +
                $"- surface: {surfaceHit.kind}\n" +
                $"- normal: {surfaceHit.normal}\n" +
                $"- upDot: {upDot:F4}"
            );
        }

        if (owner != null)
        {
            owner.TryNotifySurfaceHitReceiver(
                selfCollider,
                other,
                surfaceHit
            );
        }

        motor.NotifyBounce(surfaceHit.normal, surfaceHit.kind);

        if (sensorParam.drawDebug)
        {
            Color c =
                surfaceHit.kind == EnemySurfaceKind.Floor ? Color.green :
                surfaceHit.kind == EnemySurfaceKind.Wall ? Color.red :
                Color.cyan;

            Debug.DrawRay(transform.position, surfaceHit.normal * 2f, c, 0.5f);
        }
    }
}