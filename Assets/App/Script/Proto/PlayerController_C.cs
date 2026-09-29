using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(TimeAgent))]
public class PlayerController_C : MonoBehaviour
{
    #region コンポーネント
    private CharacterController controller; // キャラクターコントローラー
    private TimeAgent agent; // 時間制御用
    private Animator animator;
    #endregion

    #region パラメータクラス（Inspector用）
    [System.Serializable]
    private class MoveParam
    {
        public float moveSpeed = 10f;
        public float deadZone = 0.1f;
        public float accel = 20f;
        public float decel = 25f;
        public float rotateSpeed = 10f;
    }
    [SerializeField] private MoveParam moveParam = new MoveParam();

    [System.Serializable]
    private class RotateParam
    {
        public float moveStartAngle = 30f;
    }
    [SerializeField] private RotateParam rotateParam = new RotateParam();

    [System.Serializable]
    private class SlopeParam
    {
        public float slopeAccel = 10f;
        public float slopeDecel = 15f;
        public float slopeUpFactor = 0.6f;
        public float slopeDownFactor = 1.3f;
        public float groundStickForce = 5f;
    }
    [SerializeField] private SlopeParam slopeParam = new SlopeParam();

    [System.Serializable]
    private class JumpParam
    {
        public float jumpPower = 8f;
        public float airControl = 0.3f;
        public float jumpBufferTime = 0.15f;
        public float gravityUp = 15f;
        public float gravityDown = 30f;
        public float coyoteTime = 0.1f;
    }
    [SerializeField] private JumpParam jumpParam = new JumpParam();

    [System.Serializable]
    private class TackleParam
    {
        public float tackleDistance = 5f;
        public float tackleDuration = 0.5f;
        public float tackleSpeed = 15f;
        public float tackleCooldown = 0.3f;

        [Header("入力バッファ")]
        public float tackleBufferTime = 0.15f;

        public bool allowAirTackle = false;

        [Header("タックル威力")]
        public float basePower = 10f;

        [Header("威力カーブ（0→1）")]
        public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 1f);
    }
    [SerializeField] private TackleParam tackleParam = new TackleParam();

    [System.Serializable]
    private class CameraParam
    {
        public Transform cameraTransform;
    }
    [SerializeField] private CameraParam cameraParam = new CameraParam();
    #endregion

    #region 攻撃用設定
    private CameraController cameraController;  // カメラ参照（シェイク用）

    [System.Serializable]
    private class TackleHitParam
    {
        public GameObject tackleHitCollider;// タックル攻撃用コライダー
        [Header("Hit Stop")]
        public float hitStopTime = 0.06f;   // プレイヤーのヒットストップ時間

        [Header("Camera Shake")]
        public float shakePower = 0.3f;     // 揺れ強さ
        public float shakeDuration = 0.2f;  // 揺れ時間

    }
    [SerializeField] private TackleHitParam tackleHitParam = new TackleHitParam();

    #endregion

    #region 入力
    private InputAction moveInput;
    private InputAction jumpInput;
    private InputAction tackleInput;
    private Vector2 inputMoveDir;
    private bool jumpPressed;
    private bool tacklePressed;
    #endregion

    #region ジャンプ管理
    private float jumpBufferTimer;
    private bool jumpRequest;
    #endregion

    #region 接地安定化

    // 安定した接地判定
    private bool isGroundedStable;

    // コヨーテタイム
    private float coyoteTimer;
   

    #endregion

    #region 移動
    private Vector3 currentVelocity;
    private float verticalVelocity;
    #endregion

    #region リクエスト
    private struct MoveRequest
    {
        public Vector3 moveDir;
        public Vector3 faceDir;
        public float speedRate;
        public void Clear()
        {
            moveDir = Vector3.zero;
            faceDir = Vector3.zero;
            speedRate = 1f;
        }
    }
    private MoveRequest request;
    #endregion

    #region タックル管理
    private bool isTackling = false;
    private float tackleTimer = 0f;
    private Vector3 tackleDir;
    private float tackleCooldownTimer = 0f;
    private bool canAirTackle = false;
    public bool IsTackling => isTackling;
    // タックル入力バッファ
    private float tackleBufferTimer = 0f;
    #endregion

    #region ステート
    private PlayerState nowState;
    private PlayerState reserveState;
    private int reservePriority;
    #endregion

    #region エフェクト

    private EffectPlayer effectPlayer;
    private EffectInstance tackleEffect = null;
    private EffectInstance runEffect = null;

    #endregion


    #region 初期化
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        agent = GetComponent<TimeAgent>();
        effectPlayer = GetComponent<EffectPlayer>();
        request.Clear();
        ReserveState(new Idle(), 0);
        ApplyReserveState();
    }

    private void Start()
    {
        var map = InputSystem.actions.FindActionMap("Player");
        map.Enable();
        moveInput = map.FindAction("Move");
        jumpInput = map.FindAction("Jump");
        tackleInput = map.FindAction("Tackle");

        if (Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }
    }
    #endregion

    #region メイン
    private void Update()
    {
        if (agent.TimeScale <= 0f) return;
        request.Clear();
        reservePriority = int.MinValue;

        if (tackleCooldownTimer > 0f) tackleCooldownTimer -= Time.deltaTime * agent.TimeScale;

        isGroundedStable = CheckGroundStable();

        if (isGroundedStable)
            coyoteTimer = jumpParam.coyoteTime;
        else
            coyoteTimer -= Time.deltaTime * agent.TimeScale;

        UpdateInput();
        UpdateInputBuffer();
        nowState?.UpdateState();
        ApplyReserveState();
        UpdateAnimator();
        ApplyMovement();
    }
    #endregion

    #region 入力処理
    private void UpdateInput()
    {
        inputMoveDir = moveInput.ReadValue<Vector2>();

        if (inputMoveDir.magnitude < moveParam.deadZone)
            inputMoveDir = Vector2.zero;
        else
            inputMoveDir.Normalize();

        // ジャンプ
        jumpPressed = jumpInput.WasPressedThisFrame();
        if (jumpPressed)
            jumpBufferTimer = jumpParam.jumpBufferTime;

        // タックル
        if (tackleInput.WasPressedThisFrame())
        {
            tackleBufferTimer = tackleParam.tackleBufferTime;
        }
    }
    private void UpdateInputBuffer()
    {
        // ジャンプバッファ
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime * agent.TimeScale;
            jumpRequest = true;
        }
        else
            jumpRequest = false;

        // タックル入力バッファ更新
        if (tackleBufferTimer > 0f)
        {
            tackleBufferTimer -= Time.deltaTime * agent.TimeScale;
        }
    }
    #endregion

    #region ジャンプ
    public void ExecuteJump() { verticalVelocity = jumpParam.jumpPower; jumpBufferTimer = 0f; jumpRequest = false; }
    #endregion

    #region 地面法線
    private Vector3 GetGroundNormal()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        float radius = controller.radius * 0.8f;
        Vector3[] offsets = { Vector3.zero, new Vector3(radius, 0, 0), new Vector3(-radius, 0, 0), new Vector3(0, 0, radius), new Vector3(0, 0, -radius) };
        foreach (var o in offsets) { if (Physics.Raycast(transform.position + o, Vector3.down, out RaycastHit hit, 2f)) { sum += hit.normal; count++; } }
        if (count == 0) return Vector3.up;
        return (sum / count).normalized;
    }

    private bool CheckGroundStable()
    {
        float radius = controller.radius * 0.95f;

        float castDistance =
            (controller.height * 0.5f) + 0.3f;

        Vector3 origin =
            transform.position +
            Vector3.up * 0.1f;

        return Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            castDistance
        );
    }

    #endregion

    #region 移動適用
    private void ApplyMovement()
    {
        float dt = Time.deltaTime * agent.TimeScale;

        if (isTackling)
        {
            tackleTimer -= dt;
            Vector3 move = tackleDir * tackleParam.tackleSpeed;

            if ((controller.collisionFlags & CollisionFlags.Sides) != 0)
            {
                EndTackle();
                verticalVelocity = 0f;
                return;
            }

            move.y = 0f;
            controller.Move(move * dt);

            
            return;
        }

        float control = isGroundedStable ? 1f : jumpParam.airControl;
        Vector3 groundNormal = GetGroundNormal();
        Vector3 slopeDir = Vector3.ProjectOnPlane(request.moveDir, groundNormal).normalized;
        float slopeDot = Vector3.Dot(slopeDir, Vector3.up);
        float slopeTarget = 1f;

        if (isGroundedStable)
        {
            if (slopeDot > 0.1f) slopeTarget = slopeParam.slopeUpFactor;
            else if (slopeDot < -0.1f) slopeTarget = slopeParam.slopeDownFactor;
        }

        Vector3 targetVelocity =slopeDir * moveParam.moveSpeed * request.speedRate * slopeTarget * control;

        float angle = 0f;
        if (request.faceDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(request.faceDir);
            angle = Quaternion.Angle(transform.rotation, targetRot);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, moveParam.rotateSpeed * dt);
        }

        float moveFactor = 1f;

        moveFactor = angle > rotateParam.moveStartAngle ? 0f : 1f;
        currentVelocity = (slopeDir.sqrMagnitude > 0.001f) ?
            Vector3.Lerp(currentVelocity, targetVelocity * moveFactor, moveParam.accel * dt) :
            Vector3.Lerp(currentVelocity, Vector3.zero, moveParam.decel * dt);

        if (isGroundedStable)
        {
            if (verticalVelocity < 0f) verticalVelocity = -slopeParam.groundStickForce;
            if (jumpRequest && coyoteTimer > 0f)
            {
                ExecuteJump();
            }
            canAirTackle = true;
        }
        else verticalVelocity -= (verticalVelocity > 0 ? jumpParam.gravityUp : jumpParam.gravityDown) * dt;

        Vector3 moveTotal = currentVelocity;
        moveTotal.y = verticalVelocity;
        controller.Move(moveTotal * dt);
    }
    #endregion

    #region カメラ
    private Vector3 GetCameraMoveDir()
    {
        Vector3 f = cameraParam.cameraTransform.forward;
        Vector3 r = cameraParam.cameraTransform.right;
        f.y = 0f; r.y = 0f; f.Normalize(); r.Normalize();
        return f * inputMoveDir.y + r * inputMoveDir.x;
    }
    #endregion

    #region タックル操作
    public void StartTackle(float duration = -1f)
    {
        // 既存移動を完全停止
        currentVelocity = Vector3.zero;

        // タックル時間
        tackleTimer = (duration > 0f) ? duration : tackleParam.tackleDuration;

        // =========================
        // タックル方向決定
        // 入力優先にする
        // =========================
        Vector3 inputDir = GetCameraMoveDir();

        if (inputDir.sqrMagnitude > 0.001f)
        {
            tackleDir = inputDir.normalized;
            transform.rotation = Quaternion.LookRotation(tackleDir);
        }
        else
        {
            tackleDir = transform.forward;
        }

        // =========================
        // タックル開始
        // =========================
        isTackling = true;

       

        // =========================
        // エフェクト
        // =========================
        if (tackleEffect == null)
        {
            EffectPlayParam param = EffectPlayParam.Default;
            param.positionOffset = transform.forward * 3.0f;

            tackleEffect = effectPlayer.Play(0, param);
        }

        // =========================
        // 攻撃コライダーON
        // =========================
        if (tackleHitParam.tackleHitCollider != null)
        {
            tackleHitParam.tackleHitCollider.SetActive(true);
        }

        // =========================
        // 空中制御
        // =========================
        if (!isGroundedStable)
            canAirTackle = false;

        // =========================
        // 軽いカメラ揺れ
        // =========================
        if (cameraController != null)
        {
            cameraController.PlayShake(
                Vector3.forward,
                tackleHitParam.shakePower * 0.5f,
                tackleHitParam.shakeDuration * 0.5f
            );
        }
    }

    public void EndTackle()
    {
        isTackling = false;

      

        tackleTimer = 0f;
        currentVelocity = Vector3.zero;
        tackleCooldownTimer = tackleParam.tackleCooldown;

        verticalVelocity = Mathf.Min(verticalVelocity, 0f);

        if (tackleEffect != null)
        {
            tackleEffect.Stop();
            tackleEffect = null;
        }

        if (tackleHitParam.tackleHitCollider != null)
        {
            tackleHitParam.tackleHitCollider.SetActive(false);
        }
    }

    public bool CanTackle()
    {
        return isGroundedStable ||
               (!isGroundedStable && tackleParam.allowAirTackle && canAirTackle && tackleCooldownTimer <= 0f);
    }


    // タックルがEnemyにヒットした時の処理
    public void OnTackleHit(Collider enemy)
    {
        Debug.Log("Tackle Hit");

        GameTimeManager.Instance.HitStopLayer(TimeLayerType.Gameplay, tackleHitParam.hitStopTime);

        if (cameraController != null)
        {
            cameraController.PlayShake(
                Vector3.forward,
                tackleHitParam.shakePower,
                tackleHitParam.shakeDuration
            );
        }

        var field = enemy.GetComponentInParent<FieldObjectController>();

        if (field != null)
        {
            float power = GetTacklePower();
            Vector3 dir = transform.forward;

            Debug.Log("Apply Blow");

            field.ApplyBlow(dir, power);
        }

        EndTackle();
    }

    public float GetTackleProgress()
    {
        if (!isTackling || tackleParam.tackleDuration <= 0f) return 0f;

        float progress = 1f - (tackleTimer / tackleParam.tackleDuration);
        return Mathf.Clamp01(progress);
    }

    public float GetTacklePower()
    {
        if (!isTackling) return 0f;

        float progress = GetTackleProgress();
        float curveValue = tackleParam.powerCurve.Evaluate(progress);

        // ★速度依存追加
        float speedFactor = currentVelocity.magnitude / tackleParam.tackleSpeed;

        return tackleParam.basePower * curveValue;
    }

    #endregion

    #region アニメーション

    private void UpdateAnimator()
    {
        float speed = currentVelocity.magnitude / moveParam.moveSpeed;

        if (speed < 0.1f) speed = 0f;

        if (isTackling) speed = 0f;

        animator.SetFloat("Speed", speed);
        animator.SetBool("IsTackling", isTackling);
        animator.SetBool("IsGrounded", isGroundedStable);

        float animVertical = isGroundedStable ? 0f : verticalVelocity;

        animator.SetFloat("VerticalVelocity", animVertical);
    }

    #endregion

    #region ステート
    public abstract class PlayerState
    {
        protected PlayerController_C player;
        public void SetPlayerController(PlayerController_C p) => player = p;
        public virtual void EnterState() { }
        public virtual void UpdateState() { }
        public virtual void ExitState() { }
    }

    private class Idle : PlayerState
    {
        public override void UpdateState()
        {
            // 最優先
            if (player.tackleBufferTimer > 0f && player.CanTackle() && player.tackleCooldownTimer <= 0f)
            {
                player.tackleBufferTimer = 0f;
                player.ReserveState(new Tackle(), 100);
                return;
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.faceDir = dir;
            player.request.speedRate = 1f;

            if (player.jumpRequest)
            {
                player.ReserveState(new Jump(), 10);
                return;
            }

            if (dir.sqrMagnitude > 0.001f)
            {
                player.ReserveState(new Run(), 1);
            }
        }
    }

    private class Run : PlayerState
    {
        public override void EnterState()
        {
            base.EnterState();

            // =========================
            // エフェクト
            // =========================
            if (player.runEffect == null)
            {
                EffectPlayParam param = EffectPlayParam.Default;
                //param.positionOffset = -player.transform.forward * 3.0f;
                //param.positionOffset.y = -1.0f;

                param.positionOffset.y = -1.0f;

                player.runEffect = player.effectPlayer.Play(1, param);
            }
        }
        public override void UpdateState()
        {
            // 最優先
            if (player.tackleBufferTimer > 0f && player.CanTackle() && player.tackleCooldownTimer <= 0f)
            {
                player.tackleBufferTimer = 0f;
                player.ReserveState(new Tackle(), 100);
                return;
            }

            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.faceDir = dir;
            player.request.speedRate = 1f;

            if (player.jumpRequest)
            {
                player.ReserveState(new Jump(), 10);
                return;
            }

            if (dir.sqrMagnitude < 0.001f)
            {
                player.ReserveState(new Idle(), 0);
            }
        }

        public override void ExitState()
        {
            if (player.runEffect != null)
            {
                player.runEffect.Stop();
                player.runEffect = null;
            }
        }


    }

    private class Jump : PlayerState
    {
        public override void UpdateState()
        {
            var dir = player.GetCameraMoveDir();

            player.request.moveDir = dir;
            player.request.faceDir = dir;
            player.request.speedRate = 1f;

            if (player.tackleBufferTimer > 0f && player.CanTackle() && player.tackleCooldownTimer <= 0f)
            {
                player.tackleBufferTimer = 0f;
                player.ReserveState(new Tackle(), 100);
                return;
            }

            if (player.isGroundedStable && player.verticalVelocity <= 0f)
            {
                if (dir.sqrMagnitude > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
        }
    }

    private class Tackle : PlayerState
    {
        public override void EnterState()
        {
            player.StartTackle();
        }

        public override void UpdateState()
        {
            // タックル終了チェック
            if (player.tackleTimer <= 0f)
            {
                var dir = player.inputMoveDir.magnitude;

                if (dir > 0.001f)
                    player.ReserveState(new Run(), 1);
                else
                    player.ReserveState(new Idle(), 0);
            }
        }

        public override void ExitState()
        {
            player.EndTackle();
        }
    }
    #endregion

    #region ステート管理
    private void ReserveState(PlayerState s, int priority)
    {
        if (priority < reservePriority) return;
        reserveState = s;
        reservePriority = priority;
    }

    private void ApplyReserveState()
    {
        if (reserveState == null) return;
        nowState?.ExitState();
        nowState = reserveState;
        nowState.SetPlayerController(this);
        reserveState = null;
        nowState?.EnterState();
    }
    #endregion
}