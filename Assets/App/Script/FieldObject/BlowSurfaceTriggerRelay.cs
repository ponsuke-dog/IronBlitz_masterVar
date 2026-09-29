using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BlowSurfaceTriggerRelay : MonoBehaviour
{
    #region 参照

    [Header("親の吹き飛び制御")]
    [SerializeField] private BlowObjectController owner;

    #endregion

    #region コンポーネント

    private Collider triggerCollider;

    #endregion

    #region 初期化

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        if (owner == null)
            owner = GetComponentInParent<BlowObjectController>();
    }

    #endregion

    #region Trigger 通知

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null || triggerCollider == null) return;
        owner.NotifySurfaceTrigger(triggerCollider, other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (owner == null || triggerCollider == null) return;
        owner.NotifySurfaceTrigger(triggerCollider, other);
    }

    #endregion
}
