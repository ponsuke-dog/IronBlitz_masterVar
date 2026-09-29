using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(TimeAgent))]
public class FieldObjectController : MonoBehaviour
{
    // 共通コンポーネント（子クラスからも使うためprotected）
    protected CharacterController controller;
    protected TimeAgent agent;
    protected EffectPlayer effectPlayer;

    [SerializeField]
    private GameObject hitCollider;

    // 通常レイヤー
    private int layerIdle;

    // 吹き飛びレイヤー
    private int layerFly;

    // 最後の吹き飛び方向
    private Vector3 lastBlowDirection;

    // 連鎖ヒット暴走防止
    private float lastHitTime;

    // 吹き飛び直後フラグ
    private bool justBlownFrame;

    // 同一フレーム多重反射防止
    private int lastBounceFrame = -1;

    [System.Serializable]
    private class PhysicsParam
    {
        public float gravity = 28f;
        public float groundStickForce = 1f;
        public float drag = 3f;
        public float blowPowerMultiplier = 5f;
        public float mass = 1f;
        public float rotateSpeed = 720f;
        public float wallBouncePower = 0.6f;
        public float floorBouncePower = 0.12f;
        public float minStopSpeed = 0.6f;
        public float chainPowerRate = 0.55f;
        public float chainCooldown = 0.08f;
        public float minUpwardPower = 4f;
    }

    [SerializeField]
    private PhysicsParam param = new PhysicsParam();

    // 水平速度
    private Vector3 velocity;

    // 垂直速度
    private float verticalVelocity;

    // 回転速度
    private Vector3 angularVelocity;

    private State nowState;
    private State reserveState;
    private int reservePriority;

    public bool IsFlying => nowState is HitFly;

    // =========================
    // ステート基底
    // =========================

    private abstract class State
    {
        protected FieldObjectController obj;

        public void Set(FieldObjectController o)
        {
            obj = o;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }

    // =========================
    // Idle状態
    // =========================

    class Idle : State
    {
        public override void Enter()
        {
            obj.velocity = Vector3.zero;
            obj.angularVelocity = Vector3.zero;

            obj.gameObject.layer = obj.layerIdle;

            if (obj.hitCollider)
                obj.hitCollider.SetActive(false);
        }
    }

    // =========================
    // 吹き飛び状態
    // =========================

    class HitFly : State
    {
        public override void Enter()
        {
            //obj.gameObject.layer = obj.layerFly;

            if (obj.hitCollider)
                obj.hitCollider.SetActive(true);
        }

        public override void Update()
        {
            float dt = Time.deltaTime * obj.agent.TimeScale;

            // 空気抵抗
            float speed = obj.velocity.magnitude;

            speed -= obj.param.drag * dt;
            speed = Mathf.Max(speed, 0);

            if (speed > 0)
                obj.velocity = obj.velocity.normalized * speed;

            // 地面摩擦
            if (obj.controller.isGrounded)
                obj.velocity *= 0.94f;

            // 停止判定
            if (obj.velocity.magnitude < obj.param.minStopSpeed)
                obj.velocity = Vector3.zero;

            if (obj.controller.isGrounded &&
                obj.velocity == Vector3.zero &&
                Mathf.Abs(obj.verticalVelocity) < 0.2f)
            {
                obj.Reserve(new Idle(), 0);
            }
        }

        public override void Exit()
        {
            obj.gameObject.layer = obj.layerIdle;

            if (obj.hitCollider)
                obj.hitCollider.SetActive(false);
        }
    }

    // =========================
    // 初期化
    // =========================

    private void Awake()
    {
        // 必須コンポーネント取得
        controller = GetComponent<CharacterController>();
        agent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();

        layerIdle = LayerMask.NameToLayer("FieldObject");
        layerFly = LayerMask.NameToLayer("FieldObjectFly");

        // 初期状態
        Reserve(new Idle(), 0);
        ApplyReserve();
    }

    // =========================
    // 更新
    // =========================

    protected virtual void Update()
    {
        if (agent.TimeScale <= 0f) return;

        float dt = Time.deltaTime * agent.TimeScale;

        reservePriority = int.MinValue;

        nowState?.Update();
        ApplyReserve();

        ApplyGravity(dt);
        MoveObject(dt);
        RotateObject(dt);

        justBlownFrame = false;
    }

    // =========================
    // 重力
    // =========================

    private void ApplyGravity(float dt)
    {
        if (controller.isGrounded && !justBlownFrame)
        {
            if (verticalVelocity < 0)
                verticalVelocity = -param.groundStickForce;
        }
        else
        {
            verticalVelocity -= param.gravity * dt;
        }
    }

    // =========================
    // 移動
    // =========================

    private void MoveObject(float dt)
    {
        Vector3 totalVelocity = velocity;
        totalVelocity.y = verticalVelocity;

        controller.Move(totalVelocity * dt);

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity =
                -verticalVelocity * param.floorBouncePower;
        }

        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -param.groundStickForce;
        }
    }

    // =========================
    // 壁衝突処理
    // =========================

    protected virtual void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!(nowState is HitFly))
            return;

        if (hit.normal.y > 0.2f)
            return;

        if (lastBounceFrame == Time.frameCount)
            return;

        lastBounceFrame = Time.frameCount;

        Vector3 reflected =
            Vector3.Reflect(velocity, hit.normal);

        velocity =
            reflected * param.wallBouncePower;

        if (effectPlayer)
            effectPlayer.Play(0);
    }

    // =========================
    // 回転
    // =========================

    private void RotateObject(float dt)
    {
        if (angularVelocity.sqrMagnitude > 0.01f)
        {
            transform.Rotate(
                angularVelocity * dt,
                Space.World);
        }
    }

    // =========================
    // 吹き飛び
    // =========================

    public virtual void ApplyBlow(Vector3 dir, float power)
    {
        Vector3 horizontal = new Vector3(dir.x, 0, dir.z);

        if (horizontal.sqrMagnitude < 0.01f)
            horizontal = transform.forward;

        horizontal.Normalize();

        lastBlowDirection = horizontal;

        float finalPower =
            power *
            param.blowPowerMultiplier /
            Mathf.Max(param.mass, 1f);

        velocity += horizontal * finalPower;

        verticalVelocity =
            Mathf.Max(
                finalPower * 0.25f,
                param.minUpwardPower);

        justBlownFrame = true;

        angularVelocity =
            Random.insideUnitSphere *
            param.rotateSpeed;

        if (effectPlayer)
            effectPlayer.Play(0);

        Reserve(new HitFly(), 100);
    }

    // =========================
    // 連鎖衝突
    // =========================

    public void OnHitObject(FieldObjectController other)
    {
        if (!(nowState is HitFly))
            return;

        if (Time.time - lastHitTime < param.chainCooldown)
            return;

        lastHitTime = Time.time;

        Vector3 dir = lastBlowDirection;

        float power =
            velocity.magnitude *
            param.chainPowerRate;

        other.ApplyBlow(dir, power);
    }

    // =========================
    // ステート予約
    // =========================

    private void Reserve(State s, int p)
    {
        if (p < reservePriority) return;

        reserveState = s;
        reservePriority = p;
    }

    // =========================
    // ステート適用
    // =========================

    private void ApplyReserve()
    {
        if (reserveState == null) return;

        nowState?.Exit();

        nowState = reserveState;
        nowState.Set(this);

        reserveState = null;

        nowState.Enter();
    }
}