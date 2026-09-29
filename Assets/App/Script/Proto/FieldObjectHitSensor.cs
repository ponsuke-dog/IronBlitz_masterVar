using UnityEngine;

public class FieldObjectHitSensor : MonoBehaviour
{
    private FieldObjectController owner;

    private void Awake()
    {
        owner = GetComponentInParent<FieldObjectController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        FieldObjectController target =
            other.GetComponent<FieldObjectController>();

        if (target == null)
            return;

        if (target == owner)
            return;

        owner.OnHitObject(target);
    }
}