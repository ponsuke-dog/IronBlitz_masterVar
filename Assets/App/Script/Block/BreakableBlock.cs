using UnityEngine;

public class BreakableBlock : MonoBehaviour
{
    [Header("Objects")]
    public GameObject intactObject;      // ”j‰ó‘Oƒ‚ƒfƒ‹
    public GameObject fracturedObject;   // ”j•Ğ‚Ìe

    [Header("Explosion Settings")]
    public float explosionForce = 300f;
    public float explosionRadius = 2f;
    public float upwardModifier = 0.5f;

    [Header("Destroy Settings")]
    public float destroyAfterSeconds = 5f;

    void Start()
    {

        // ”j•Ğ‚ÍÅ‰”ñ•\¦
        fracturedObject.SetActive(false);

        // 3•bŒã‚É©“®”j‰ó
        Invoke(nameof(Break), 3f);

        // ”j•Ğ‚ÉˆêŠ‡‚Å Rigidbody + Collider ‚ğİ’è
        foreach (Transform piece in fracturedObject.transform)
        {
            // Rigidbody
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb == null)
                rb = piece.gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;   // Å‰‚Í~‚ß‚é
            rb.useGravity = false;
            rb.mass = 0.5f;

            // Collider
            Collider col = piece.GetComponent<Collider>();
            if (col == null)
               col = piece.gameObject.AddComponent<BoxCollider>();

            col.isTrigger = true; // Õ“Ë‚ÍƒgƒŠƒK[‚Åˆ—
        }


    }

    public void Break()
    {
        intactObject.SetActive(false);
        fracturedObject.SetActive(true);


        foreach (Transform piece in fracturedObject.transform)
        {
            BreakFragment frag = piece.GetComponent<BreakFragment>();

            if (frag == null)
                frag = piece.gameObject.AddComponent<BreakFragment>();

            frag.Init(transform.position, explosionForce, GetComponent<TimeAgent>().TimeScale);
        }

        // ”j•Ğ‚ğ‚«”ò‚Î‚·
        //foreach (Transform piece in fracturedObject.transform)
        //{
        //    Rigidbody rb = piece.GetComponent<Rigidbody>();

        //    if (rb != null)
        //    {
        //        rb.isKinematic = false;
        //        rb.useGravity = true;

        //        // ”š”­—Í
        //        rb.AddExplosionForce(
        //            explosionForce,
        //            transform.position,
        //            explosionRadius,
        //            upwardModifier,
        //            ForceMode.Impulse
        //        );

        //        // ƒ‰ƒ“ƒ_ƒ€‰ñ“]iƒŠƒAƒ‹Š´ƒAƒbƒvj
        //        rb.AddTorque(Random.insideUnitSphere * 50f, ForceMode.Impulse);

        //        // ˆê’èŠÔŒã‚ÉíœiŒy—Ê‰»j
        //        Destroy(piece.gameObject, destroyAfterSeconds);
        //    }
        //}

    }


}
