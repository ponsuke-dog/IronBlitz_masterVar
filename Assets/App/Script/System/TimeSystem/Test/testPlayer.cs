using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TimeAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public class testPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 7f;

    [Header("Gravity")]
    [SerializeField] private float gravity = 20f;

    [Header("Tackle")]
    [SerializeField] private float tackleSpeed = 18f;
    [SerializeField] private float tackleDuration = 0.25f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector3 groundCheckOffset = Vector3.zero;

    private Rigidbody rb;
    private TimeAgent agent;
    private CapsuleCollider capsule;

    private Vector3 inputDir;
    private Vector3 moveDir;
    private Vector3 lastMoveDir = Vector3.forward;

    private bool isGround;
    private bool isTackle;

    private float tackleTimer;
    private float verticalVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<TimeAgent>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Update()
    {
        // 入力取得
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        inputDir = new Vector3(h, 0, v);

        // カメラ基準移動
        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();
            moveDir = camForward * v + camRight * h;
        }
        else
        {
            moveDir = inputDir;
        }

        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.Normalize();
            lastMoveDir = moveDir;
        }

        // ジャンプ
        if (Input.GetKeyDown(KeyCode.Space))
            Jump();

        // タックル
        if (Input.GetKeyDown(KeyCode.LeftShift))
            StartTackle();
    }

    void FixedUpdate()
    {
        float scale = agent.TimeScale;
        if (scale <= 0f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        float dt = Time.fixedDeltaTime * scale;

        GroundCheck();
        ApplyGravity(dt);

        Vector3 horizontalVelocity;

        // ★タックル時間の更新
        if (isTackle)
        {
            horizontalVelocity = UpdateTackle(dt);
        }
        else
        {
            horizontalVelocity = moveDir * moveSpeed;
        }

        // 最終速度
        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;

        // Rigidbodyにセット
        rb.linearVelocity = velocity * scale;

        // 回転補正
        if (horizontalVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(horizontalVelocity);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 0.2f));
        }
    }

    void Jump()
    {
        if (!isGround) return;
        verticalVelocity = jumpForce;
    }

    void ApplyGravity(float dt)
    {
        if (isGround && verticalVelocity < 0)
        {
            verticalVelocity = 0f;
            return;
        }
        verticalVelocity -= gravity * dt;
    }

    void StartTackle()
    {
        if (isTackle) return;
        isTackle = true;
        tackleTimer = tackleDuration;
    }

    Vector3 UpdateTackle(float dt)
    {
        tackleTimer -= dt;
        if (tackleTimer <= 0f)
        {
            isTackle = false;
            return Vector3.zero;
        }
        return lastMoveDir * tackleSpeed;
    }

    void GroundCheck()
    {
        Vector3 origin = transform.position + groundCheckOffset;
        isGround = Physics.CheckSphere(origin, groundCheckRadius, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        if (!capsule) capsule = GetComponent<CapsuleCollider>();
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
    }
}