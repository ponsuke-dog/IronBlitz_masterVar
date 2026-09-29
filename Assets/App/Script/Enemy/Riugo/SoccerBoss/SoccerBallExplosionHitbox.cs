using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoccerBallExplosionHitbox : MonoBehaviour, IHitSource
{
    [Header("Hit")]
    [SerializeField] private LayerMask targetLayer;

    [Tooltip("爆発ダメージ。Setupで0以下が渡された場合はこの値を使う")]
    [SerializeField] private int damage = 1;

    [SerializeField] private bool hitSameTargetOnce = true;

    [Header("Owner")]
    [SerializeField] private GameObject attackerObject;

    [Header("Overlap Check")]
    [Tooltip("生成直後に範囲内のColliderを即チェックする")]
    [SerializeField] private bool checkOverlapOnSetup = true;

    [Tooltip("Start時にも範囲内チェックする。Setupを呼び忘れた時の保険")]
    [SerializeField] private bool checkOverlapOnStart = false;

    [Tooltip("TriggerStayでも判定する")]
    [SerializeField] private bool useTriggerStay = true;

    [Header("Debug")]
    [SerializeField] private bool logHit = false;
    [SerializeField] private bool logIgnoreReason = false;

    private readonly HashSet<GameObject> hitObjects =
        new HashSet<GameObject>();

    private Collider selfCollider;
    private Hitbox selfHitbox;

    private int resolvedDamage;

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();
        selfHitbox = GetComponent<Hitbox>();

        if (selfCollider != null)
            selfCollider.isTrigger = true;

        resolvedDamage = Mathf.Max(1, damage);
    }

    private void Start()
    {
        if (checkOverlapOnStart)
            CheckOverlapNow();
    }

    public void Setup(
        GameObject attacker,
        int damageValue,
        LayerMask layer)
    {
        attackerObject = attacker;

        // ここが重要：
        // damageValueが0以下なら、Prefab/Inspector側のdamageを使う
        if (damageValue > 0)
            resolvedDamage = damageValue;
        else
            resolvedDamage = Mathf.Max(1, damage);

        // layerが0ならPrefab側のtargetLayerを使う
        if (layer.value != 0)
            targetLayer = layer;

        hitObjects.Clear();

        if (selfCollider == null)
            selfCollider = GetComponent<Collider>();

        if (selfHitbox == null)
            selfHitbox = GetComponent<Hitbox>();

        if (checkOverlapOnSetup)
            CheckOverlapNow();
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        TrySendExplosionHit(selfHitbox, other, "OnHitDetected");
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySendExplosionHit(selfHitbox, other, "OnTriggerEnter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!useTriggerStay)
            return;

        TrySendExplosionHit(selfHitbox, other, "OnTriggerStay");
    }

    public void CheckOverlapNow()
    {
        if (selfCollider == null)
            selfCollider = GetComponent<Collider>();

        if (selfCollider == null)
            return;

        Collider[] hits;

        LayerMask checkLayer = targetLayer.value != 0
            ? targetLayer
            : ~0;

        if (selfCollider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);

            float maxScale = Mathf.Max(
                Mathf.Abs(sphere.transform.lossyScale.x),
                Mathf.Abs(sphere.transform.lossyScale.y),
                Mathf.Abs(sphere.transform.lossyScale.z)
            );

            float radius = sphere.radius * maxScale;

            hits = Physics.OverlapSphere(
                center,
                radius,
                checkLayer,
                QueryTriggerInteraction.Collide
            );
        }
        else
        {
            Bounds bounds = selfCollider.bounds;

            hits = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                selfCollider.transform.rotation,
                checkLayer,
                QueryTriggerInteraction.Collide
            );
        }

        if (hits == null)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider other = hits[i];

            if (other == null)
                continue;

            if (other == selfCollider)
                continue;

            if (other.transform == transform ||
                other.transform.IsChildOf(transform))
            {
                continue;
            }

            TrySendExplosionHit(selfHitbox, other, "OverlapNow");
        }
    }

    private void TrySendExplosionHit(
        Hitbox selfHitbox,
        Collider other,
        string reason)
    {
        if (other == null)
            return;

        if (!IsInTargetLayer(other.gameObject))
        {
            if (logIgnoreReason)
            {
                Debug.Log(
                    $"{name} Explosion Ignore Layer: " +
                    $"{other.name} layer:{LayerMask.LayerToName(other.gameObject.layer)} reason:{reason}"
                );
            }

            return;
        }

        IHitReceiver receiver = FindReceiver(
            other,
            out GameObject receiverObject,
            out Hitbox targetHitbox
        );

        if (receiver == null || receiverObject == null)
        {
            if (logIgnoreReason)
            {
                Debug.Log(
                    $"{name} Explosion Ignore Receiver Missing: " +
                    $"{other.name} reason:{reason}"
                );
            }

            return;
        }

        if (hitSameTargetOnce &&
            hitObjects.Contains(receiverObject))
        {
            return;
        }

        hitObjects.Add(receiverObject);

        int sendDamage = Mathf.Max(1, resolvedDamage);

        HitEventData data = new HitEventData
        {
            attackerObject = attackerObject != null
                ? attackerObject
                : gameObject,

            attackerHitbox = selfHitbox != null
                ? selfHitbox.gameObject
                : gameObject,

            targetObject = receiverObject,

            targetHitbox = targetHitbox != null
                ? targetHitbox.gameObject
                : other.gameObject,

            contactPoint = other.ClosestPoint(transform.position),

            payload = new EnemyAttackPayload
            {
                damage = sendDamage
            }
        };

        if (logHit)
        {
            Debug.Log(
                $"{name} Explosion Hit => {receiverObject.name} " +
                $"damage:{sendDamage} reason:{reason}"
            );
        }

        receiver.OnHit(data);
    }

    private bool IsInTargetLayer(GameObject obj)
    {
        if (obj == null)
            return false;

        if (targetLayer.value == 0)
            return true;

        int layerBit = 1 << obj.layer;
        return (layerBit & targetLayer.value) != 0;
    }

    private IHitReceiver FindReceiver(
        Collider other,
        out GameObject receiverObject,
        out Hitbox targetHitbox)
    {
        receiverObject = null;
        targetHitbox = null;

        targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox != null && targetHitbox.receiver != null)
        {
            receiverObject = targetHitbox.receiver is MonoBehaviour mono
                ? mono.gameObject
                : other.gameObject;

            return targetHitbox.receiver;
        }

        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IHitReceiver receiver)
            {
                receiverObject = behaviours[i].gameObject;
                return receiver;
            }
        }

        return null;
    }
}