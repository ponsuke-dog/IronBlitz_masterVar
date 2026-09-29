using System.Collections.Generic;
using UnityEngine;

public class FieldObjectAngerExplosionTarget : MonoBehaviour
{
    private static readonly List<FieldObjectAngerExplosionTarget> targets =
        new List<FieldObjectAngerExplosionTarget>();

    public AudioManager audioManager;

    public static int AvailableTargetCount
    {
        get
        {
            int count = 0;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                FieldObjectAngerExplosionTarget target = targets[i];

                if (target == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                if (!target.isActiveAndEnabled)
                    continue;

                if (target.exploded)
                    continue;

                count++;
            }

            return count;
        }
    }

    public static bool HasAvailableTarget
    {
        get { return AvailableTargetCount > 0; }
    }

    [Header("Explosion Point")]
    [Tooltip("爆発位置。未設定ならこのTransform位置")]
    [SerializeField] private Transform explosionPoint;

    [Tooltip("Destroy対象。未設定ならこのGameObject")]
    [SerializeField] private GameObject destroyTarget;

    [Header("Debug")]
    [SerializeField] private bool logExplosion = false;

    private bool exploded;

    private void OnEnable()
    {
        if (!targets.Contains(this))
            targets.Add(this);

        audioManager = AudioManager.Instance;
    }

    private void OnDisable()
    {
        targets.Remove(this);

        audioManager = null;
    }

    public static void ExplodeAll(
        SoccerBoss boss,
        int effectIndex,
        Quaternion effectRotation,
        Vector3 effectScale,
        GameObject explosionHitboxPrefab,
        float explosionHitboxLifeTime,
        int explosionDamage,
        LayerMask explosionTargetLayer,
        bool hideRenderers,
        bool disableColliders,
        bool destroyAfterExplosion,
        float destroyDelay)
    {
        FieldObjectAngerExplosionTarget[] copy = targets.ToArray();

        for (int i = 0; i < copy.Length; i++)
        {
            if (copy[i] == null)
                continue;

            copy[i].Explode(
                boss,
                effectIndex,
                effectRotation,
                effectScale,
                explosionHitboxPrefab,
                explosionHitboxLifeTime,
                explosionDamage,
                explosionTargetLayer,
                hideRenderers,
                disableColliders,
                destroyAfterExplosion,
                destroyDelay
            );
        }
    }

    private void Explode(
        SoccerBoss boss,
        int effectIndex,
        Quaternion effectRotation,
        Vector3 effectScale,
        GameObject explosionHitboxPrefab,
        float explosionHitboxLifeTime,
        int explosionDamage,
        LayerMask explosionTargetLayer,
        bool hideRenderers,
        bool disableColliders,
        bool destroyAfterExplosion,
        float destroyDelay)
    {
        if (exploded)
            return;

        exploded = true;

        Vector3 pos = explosionPoint != null
            ? explosionPoint.position
            : transform.position;

        if (boss != null)
        {
            boss.PlayAngerFieldObjectExplosionEffect(
                effectIndex,
                pos,
                effectRotation,
                effectScale
            );
        }

        audioManager.PlaySe("FO_Explosion");

        SpawnExplosionHitbox(
            explosionHitboxPrefab,
            pos,
            effectRotation,
            explosionHitboxLifeTime,
            boss != null ? boss.gameObject : gameObject,
            explosionDamage,
            explosionTargetLayer
        );

        if (hideRenderers)
            SetRenderersEnabled(false);

        if (disableColliders)
            SetCollidersEnabled(false);

        if (logExplosion)
            Debug.Log($"{name}: FieldObject exploded by boss anger.");

        if (destroyAfterExplosion)
        {
            GameObject target = destroyTarget != null
                ? destroyTarget
                : gameObject;

            Destroy(target, Mathf.Max(0f, destroyDelay));
        }
    }

    private void SpawnExplosionHitbox(
    GameObject prefab,
    Vector3 position,
    Quaternion rotation,
    float lifeTime,
    GameObject attacker,
    int damage,
    LayerMask targetLayer)
    {
        if (prefab == null)
            return;

        GameObject hitbox = Instantiate(
            prefab,
            position,
            rotation
        );

        SoccerBallExplosionHitbox explosion =
            hitbox.GetComponent<SoccerBallExplosionHitbox>();

        if (explosion != null)
        {
            explosion.Setup(
                attacker,
                damage,
                targetLayer
            );
        }

        if (lifeTime > 0f)
            Destroy(hitbox, lifeTime);
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }


}