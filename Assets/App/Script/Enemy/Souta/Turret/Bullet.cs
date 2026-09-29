using UnityEngine;

public class EnemyBullet : MonoBehaviour, IHitReceiver, IHitSource
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private int palyerDamage = 10;
    [SerializeField] private int enemyDamage = 10;

    [SerializeField] private float searchRadius = 20f;
    [SerializeField] private float lockAngle = 45f;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private Transform bulletModel;
    [SerializeField] private Vector3 rotateAxis = Vector3.forward;
    [SerializeField] private float rotateSpeed = 720f;

    [Header("Reflected Move")]
    [SerializeField] private float reflectedRiseDistance = 3f;
    [SerializeField] private float reflectedLifeTime = 8f;
    [SerializeField] private float turnSpeed = 3f;

    [SerializeField] private GameObject attackCollision;
    [SerializeField] private GameObject enemyHitCollision;
    [SerializeField] private LayerMask groundLayer;

    [Header("Time")]
    [SerializeField] private TimeAgent timeAgent;

    private Transform m_Shooter;
    private bool isReflected = false;
    private bool isJustTackleHit = false;
    private float lifeTimer = 0f;
    private float justTackleHitTimer = 0f;
    private float justTacklehitLimitTime = 0.5f;

    private Transform reflectedTarget;
    private float reflectedTargetHeight;
    private bool isRisingAfterReflect;
    public void Initialize(Transform shooter)
    {
        m_Shooter = shooter;
    }

    private void Awake()
    {
        attackCollision.SetActive(true);
        enemyHitCollision.SetActive(false);

        if (timeAgent == null)
            timeAgent = GetComponent<TimeAgent>();

    }

    private void Update()
    {
        float timeScale = timeAgent.TimeScale;

        if (isReflected && reflectedTarget != null)
        {
            if (isRisingAfterReflect)
            {
                // 指定した高さに到達したら敵へ向きを変える
                if (transform.position.y >= reflectedTargetHeight)
                {
                    isRisingAfterReflect = false;
                }
                else
                {
                    transform.forward = Vector3.up;
                }
            }

            if (!isRisingAfterReflect)
            {
                Collider targetCollider =
                    reflectedTarget.GetComponentInChildren<Collider>();

                Vector3 targetPosition;

                if (targetCollider != null)
                {
                    targetPosition = targetCollider.bounds.center;
                }
                else
                {
                    targetPosition =
                        reflectedTarget.position + Vector3.up;
                }

                Vector3 targetDirection = (targetPosition - transform.position).normalized;

                // 徐々に敵方向へ向きを変える
                transform.forward = Vector3.Slerp(
                    transform.forward,
                    targetDirection,
                    turnSpeed * Time.deltaTime
                ).normalized;
            }
        }

        float moveDistance = speed * Time.deltaTime * timeScale;

        // Groundとの衝突判定
        if (Physics.Raycast(
            transform.position,
            transform.forward,
            out RaycastHit hit,
            moveDistance,
            groundLayer))
        {
            Destroy(gameObject);
            return;
        }

        // 移動
        transform.position += transform.forward * moveDistance;

        // 弾のモデルを回転させる
        if (bulletModel != null)
        {
            bulletModel.Rotate(
                Vector3.right,
                rotateSpeed * Time.deltaTime,
                Space.Self);
        }

        //寿命の処理
        lifeTimer += Time.deltaTime;
        float currentLifeTime = isReflected ? reflectedLifeTime : lifeTime;

        if (lifeTimer >= currentLifeTime)
        {
            Destroy(gameObject);
        }

        //ジャストタックル時の無敵時間の処理
        justTackleHitTimer += Time.deltaTime;
        if(justTackleHitTimer >= justTacklehitLimitTime)
            isJustTackleHit = false;
    }

    Transform FindTarget(Transform player)
    {
        Debug.Log(enemyLayer.value);

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            searchRadius,
            enemyLayer);

        Debug.Log($"Found {hits.Length} enemies in range.");

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            TurretEnemyController enemy = hit.GetComponentInParent<TurretEnemyController>();
            if (enemy == null)
                continue;
            

            Debug.Log($"Checking enemy: {hit.transform.name}");
            Vector3 dir =
                (hit.transform.position - transform.position).normalized;

            float angle =
                Vector3.Angle(player.forward, dir);

            Debug.Log(angle);

            if (angle > lockAngle)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    hit.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = enemy.transform;
            }
        }

        return bestTarget;
    }


    #region Hit Interface

    //呼び出されたときに、呼び出したオブジェクトを登録

    public void OnHit(HitEventData data)
    {
        Transform player = data.attackerObject.transform;
        reflectedTarget = FindTarget(player);

        if (reflectedTarget != null)
        {
            Debug.Log("Target : " + reflectedTarget.name);

            // 現在位置から、指定した高さまで上昇する
            reflectedTargetHeight = transform.position.y + reflectedRiseDistance;

            isRisingAfterReflect = true;

            // 上へ飛ばす
            Vector3 dir = (player.forward + Vector3.up * 1.2f).normalized;

            transform.forward = dir;
        }
        else
        {
            Debug.Log("Targetなし");

            transform.forward = player.forward;
            isRisingAfterReflect = false;
        }

        isReflected = true;
        lifeTimer = 0f;

        attackCollision.SetActive(false);
        enemyHitCollision.SetActive(true);
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        if (isJustTackleHit)
            return;

        // 反射前
        if (!isReflected)
        {
            if(other.gameObject.layer == LayerMask.NameToLayer("JustTackle"))
            {
                isJustTackleHit = true;
                return;
            }

            if (selfHitbox.gameObject != attackCollision)
                return;


        }
        // 反射後
        else
        {
            Debug.Log("EnemyBullet: OnHitDetected called, checking for enemyHitCollision");
            if (selfHitbox.gameObject != enemyHitCollision)
                return;
        }

        IHitReceiver receiver =
            FindReceiver(other, out GameObject receiverObject);

        if (receiver == null)
        {
            return;
        }

        HitEventData data = new();

        // 攻撃側
        data.attackerObject = gameObject;
        data.attackerHitbox = selfHitbox.gameObject;

        // 被弾側
        data.targetObject =
            ((MonoBehaviour)receiver).gameObject;

        data.targetHitbox =
            other.gameObject;
        Debug.Log("EnemyBullet: OnHitDetected called, target object: " + data.targetObject.name);
        //プレイヤーかエネミーでダメージ量を変更
        int damage = isReflected ? enemyDamage : palyerDamage;

        // 接触点
        data.contactPoint =
            other.ClosestPoint(transform.position);
        data.payload = new EnemyAttackPayload
        {
            damage = damage
        };

        receiver.OnHit(data);
        Debug.Log("EnemyBullet: OnHitDetected called, damage sent: " + damage);
        Destroy(gameObject);

    }

    #endregion

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