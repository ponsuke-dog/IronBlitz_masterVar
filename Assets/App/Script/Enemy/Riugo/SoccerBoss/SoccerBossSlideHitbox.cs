using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoccerBossSlideHitbox : MonoBehaviour
{
    [System.Serializable]
    private class HitParam
    {
        public LayerMask playerLayer;
        public float sameTargetCooldown = 0.5f;
    }

    [SerializeField] private HitParam hitParam = new HitParam();

    private SoccerBoss owner;
    private Collider selfCollider;
    private bool activeHit;
    private int damage = 10;

    private readonly Dictionary<GameObject, float> lastHitTimes =
        new Dictionary<GameObject, float>();

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();

        if (selfCollider != null)
            selfCollider.isTrigger = true;
    }

    public void Initialize(SoccerBoss boss)
    {
        owner = boss;
    }

    public void SetDamage(int value)
    {
        damage = Mathf.Max(0, value);
    }

    public void SetActiveHit(bool active)
    {
        activeHit = active;

        if (selfCollider != null)
            selfCollider.enabled = active;

        if (!active)
            lastHitTimes.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (!activeHit || other == null)
            return;

        int layerBit = 1 << other.gameObject.layer;

        if ((layerBit & hitParam.playerLayer.value) == 0)
            return;

        GameObject targetObject = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (lastHitTimes.TryGetValue(targetObject, out float lastTime))
        {
            if (Time.time < lastTime + hitParam.sameTargetCooldown)
                return;
        }

        lastHitTimes[targetObject] = Time.time;

        IHitReceiver receiver = FindReceiver(other, out GameObject receiverObject);

        if (receiver == null)
            return;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        HitEventData data = new HitEventData
        {
            attackerObject = owner != null ? owner.gameObject : gameObject,
            attackerHitbox = gameObject,
            targetObject = receiverObject,
            targetHitbox = targetHitbox != null ? targetHitbox.gameObject : other.gameObject,
            contactPoint = other.ClosestPoint(transform.position),
            payload = new EnemyAttackPayload
            {
                damage = damage
            }
        };

        receiver.OnHit(data);
    }

    private IHitReceiver FindReceiver(Collider other, out GameObject receiverObject)
    {
        receiverObject = null;

        Hitbox targetHitbox = other.GetComponent<Hitbox>();

        if (targetHitbox == null)
            targetHitbox = other.GetComponentInParent<Hitbox>();

        if (targetHitbox != null && targetHitbox.receiver != null)
        {
            receiverObject = targetHitbox.receiver is MonoBehaviour mono
                ? mono.gameObject
                : other.gameObject;

            return targetHitbox.receiver;
        }

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();

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