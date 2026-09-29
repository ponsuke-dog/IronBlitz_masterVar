using Unity.VisualScripting;
using UnityEngine;

public class EnemyController : FieldObjectController, IHitSource, IHitReceiver
{
    private Transform player;

    [System.Serializable]
    private class EnemyParam
    {
        public float maxHP = 10f;
        public float moveSpeed = 4f;
        public float detectRange = 12f;
        public float wanderRadius = 6f;
        public float wanderInterval = 3f;
        public float wallDamageMultiplier = 1.2f;
    }

    [SerializeField]
    private EnemyParam enemyParam = new EnemyParam();

    private float hp;
    private Vector3 wanderTarget;
    private float wanderTimer;

    private State nowState;

    [SerializeField]
    [Header("タックルとの衝突コライダー")] GameObject toHitTackle = null;

    // =========================
    // ステート基底
    // =========================

    private abstract class State
    {
        protected EnemyController enemy;

        public void Set(EnemyController e)
        {
            enemy = e;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }

    // =========================
    // 徘徊
    // =========================

    private class Wander : State
    {
        public override void Enter()
        {
            enemy.wanderTimer = enemy.enemyParam.wanderInterval;
            enemy.SetRandomWanderTarget();
        }

        public override void Update()
        {
            float dt = Time.deltaTime * enemy.agent.TimeScale;

            enemy.wanderTimer -= dt;

            enemy.MoveTo(enemy.wanderTarget);

            if (Vector3.Distance(enemy.transform.position, enemy.wanderTarget) < 1f)
            {
                enemy.SetRandomWanderTarget();
            }

            if (enemy.wanderTimer <= 0f)
            {
                enemy.SetRandomWanderTarget();
                enemy.wanderTimer = enemy.enemyParam.wanderInterval;
            }

            if (enemy.IsPlayerDetected())
            {
                enemy.ChangeState(new Chase());
            }
        }
    }

    // =========================
    // 追跡
    // =========================

    private class Chase : State
    {
        public override void Update()
        {
            if (enemy.player == null)
                return;

            enemy.MoveTo(enemy.player.position);

            float dist =
                Vector3.Distance(
                    enemy.transform.position,
                    enemy.player.position);

            if (dist > enemy.enemyParam.detectRange * 1.5f)
            {
                enemy.ChangeState(new Wander());
            }
        }
    }

    // =========================
    // 初期化
    // =========================

    private void Start()
    {
        hp = enemyParam.maxHP;

        GameObject p = GameObject.FindGameObjectWithTag("Player");

        if (p)
            player = p.transform;

        ChangeState(new Wander());
    }

    // =========================
    // 更新
    // =========================

    protected override void Update()
    {
        base.Update();

        if (agent.TimeScale <= 0f)
            return;

        // 吹き飛び中はAI停止
        if (IsFlying) return;

        nowState?.Update();
    }

    // =========================
    // 移動
    // =========================

    private void MoveTo(Vector3 target)
    {
        Vector3 dir =
            target - transform.position;

        dir.y = 0;

        if (dir.sqrMagnitude < 0.1f)
            return;

        dir.Normalize();

        Vector3 move =
            dir * enemyParam.moveSpeed;

        controller.Move(move * Time.deltaTime * agent.TimeScale);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                10f * Time.deltaTime);
    }

    // =========================
    // プレイヤー検知
    // =========================

    private bool IsPlayerDetected()
    {
        if (player == null)
            return false;

        float dist =
            Vector3.Distance(
                transform.position,
                player.position);

        return dist <= enemyParam.detectRange;
    }

    // =========================
    // 徘徊ターゲット
    // =========================

    private void SetRandomWanderTarget()
    {
        Vector2 rand =
            Random.insideUnitCircle *
            enemyParam.wanderRadius;

        wanderTarget =
            transform.position +
            new Vector3(rand.x, 0, rand.y);
    }

    // =========================
    // ステート変更
    // =========================

    private void ChangeState(State next)
    {
        nowState?.Exit();

        nowState = next;

        nowState.Set(this);

        nowState.Enter();
    }

    // =========================
    // ダメージ
    // =========================

    public void TakeDamage(float damage)
    {
        hp -= damage;

        if (hp <= 0)
        {
            if (effectPlayer)
            {
                EffectPlayParam param = EffectPlayParam.Default;
                param.scale *= 2.0f;
                effectPlayer.Play(0, param);
            }
            Destroy(gameObject);
        }
    }

    // =========================
    // 壁反射ダメージ
    // =========================

    protected override void OnControllerColliderHit(ControllerColliderHit hit)
    {
        base.OnControllerColliderHit(hit);

        // 吹き飛び中以外は無視
        if (!IsFlying) return;

        if (hit.normal.y > 0.2f)
            return;


        TakeDamage(1);
    }

    public override void ApplyBlow(Vector3 dir, float power)
    {
        base.ApplyBlow(dir, power);

       

        TakeDamage(1);
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
       
    }

    public void OnHit(HitEventData data)
    {
        if(data.targetHitbox == toHitTackle)
        {
            if (data.payload is BlowPayload blow)
            {
                ApplyBlow(
                    blow.powerDirection,
                    blow.powerConstant*blow.powerRate
                    );
            }

            Debug.Log("Enemy Blow");
        }
    }
}