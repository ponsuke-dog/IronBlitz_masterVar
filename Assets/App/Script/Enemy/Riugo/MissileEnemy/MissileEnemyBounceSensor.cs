using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MissileEnemyBounceSensor : MonoBehaviour
{
    [System.Serializable]
    public class SensorParam
    {
        [Header("Detect")]
        public LayerMask bounceMask;

        [Header("Surface Detect")]
        [Range(0f, 1f)] public float floorNormalYThreshold = 0.55f;
        [Range(0f, 1f)] public float wallNormalYThreshold = 0.35f;
        [Range(0f, 1f)] public float ceilingNormalYThreshold = 0.55f;
        [Range(0f, 1f)] public float slopeFloorAssistYThreshold = 0.15f;

        [Header("Notify")]
        public bool useTriggerStay = true;
        public float notifyCooldown = 0.03f;

        [Header("Debug")]
        public bool drawDebug = false;
        public bool logHit = false;
    }

    [SerializeField] private MissileEnemyMotor motor;
    [SerializeField] private EnemyControllerTypeMissile owner;
    [SerializeField] private SensorParam sensorParam = new SensorParam();

    private Collider selfCollider;
    private Rigidbody rb;
    private float lastNotifyTime = -999f;

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        if (motor == null)
            motor = GetComponentInParent<MissileEnemyMotor>();

        if (owner == null)
            owner = GetComponentInParent<EnemyControllerTypeMissile>();

        if (selfCollider != null)
        {
            // OnTriggerEnter / OnTriggerStay を使うので true に固定
            selfCollider.isTrigger = true;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;

            // 親のCharacterControllerに追従するだけなら補間しない方がズレにくい
            rb.interpolation = RigidbodyInterpolation.None;

            // Triggerセンサー用途ならまずはDiscreteで十分
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    private void LateUpdate()
    {
        // CharacterController.Move後のTransformを物理側へ同期
        // センサー位置ズレ確認中は入れておくと安全
        Physics.SyncTransforms();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryNotify(other, "Enter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!sensorParam.useTriggerStay)
            return;

        TryNotify(other, "Stay");
    }

    private void TryNotify(Collider other, string source)
    {
        if (motor == null)
            return;

        if (!motor.IsBlown)
            return;

        if (Time.time < lastNotifyTime + sensorParam.notifyCooldown)
            return;

        if (((1 << other.gameObject.layer) & sensorParam.bounceMask.value) == 0)
            return;

        Vector3 fallbackTravelDir = motor.CurrentTotalVelocity;

        if (fallbackTravelDir.sqrMagnitude < 0.0001f)
            fallbackTravelDir = -motor.LastDashImpactNormal;

        if (fallbackTravelDir.sqrMagnitude < 0.0001f)
            fallbackTravelDir = transform.forward;

        if (!EnemySurfaceUtility.TryBuildSurfaceHit(
                selfCollider,
                other,
                transform.position,
                fallbackTravelDir,
                sensorParam.floorNormalYThreshold,
                sensorParam.wallNormalYThreshold,
                sensorParam.ceilingNormalYThreshold,
                sensorParam.slopeFloorAssistYThreshold,
                out EnemySurfaceHit surfaceHit))
        {
            return;
        }

        lastNotifyTime = Time.time;

        if (owner != null)
        {
            owner.TryNotifySurfaceHitReceiver(
                selfCollider,
                other,
                surfaceHit
            );
        }

        motor.NotifyBlowSurfaceHit(surfaceHit.normal, surfaceHit.kind);

        if (sensorParam.logHit)
        {
            Debug.Log(
                $"{name} BounceSensor[{source}] " +
                $"target:{other.name} kind:{surfaceHit.kind} normal:{surfaceHit.normal} " +
                $"sensorPos:{transform.position} motorPos:{motor.transform.position}"
            );
        }

        if (sensorParam.drawDebug)
        {
            Color c =
                surfaceHit.kind == EnemySurfaceKind.Floor ? Color.green :
                surfaceHit.kind == EnemySurfaceKind.Wall ? Color.red :
                Color.cyan;

            Debug.DrawRay(transform.position, surfaceHit.normal * 2f, c, 0.25f);
        }
    }
}