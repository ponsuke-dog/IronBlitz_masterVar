using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Fragment
{
    public Transform transform;

    [HideInInspector] public Vector3 initialLocalPosition;
    [HideInInspector] public Quaternion initialLocalRotation;
    [HideInInspector] public Vector3 initialLocalScale;

    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public Vector3 angularVelocity;

    [HideInInspector] public float aliveTime;
    [HideInInspector] public float groundedTime;
    [HideInInspector] public float noGravityTimer;
    [HideInInspector] public float scaleRate;

    [HideInInspector] public bool shrinking;
    [HideInInspector] public bool disabled;
    [HideInInspector] public bool grounded;

    // Rendererは初期中心・半径の計算用に保持
    [HideInInspector] public Renderer[] renderers;

    // ★追加：初期状態で計算した「破片ローカル空間の見た目中心」
    [HideInInspector] public Vector3 localVisualCenter;

    // ★追加：破片ごとの見た目サイズから求めた推奨衝突半径
    [HideInInspector] public float visualRadius;
}

public class BlockBreakController : MonoBehaviour, IHitReceiver, IHitSource
{
    #region Inspector

    [Header("Fragments")]
    [Tooltip("破片Transform。未設定なら、このオブジェクトの子から自動収集する")]
    [SerializeField] private List<Transform> fragmentTransforms = new List<Transform>();

    [Header("Break Target")]
    [SerializeField] private GameObject breakTriggerObject;
    [SerializeField] private GameObject blockCollisionObject;
    [SerializeField] private GameObject enemyAttackTriggerObject;

    [Header("Explosion")]
    [Tooltip("爆発の基本威力")]
    [SerializeField] private float explosionPower = 10f;

    [Tooltip("ヒット位置から外側へ飛ばす強さ")]
    [SerializeField] private float outwardPowerRate = 1.0f;

    [Tooltip("上方向に加える力")]
    [SerializeField] private float upwardPower = 4.0f;

    [Tooltip("爆発方向に混ぜるランダム量")]
    [Range(0f, 1f)]
    [SerializeField] private float randomDirectionRate = 0.35f;

    [Tooltip("破壊直後に重力を無効化する時間")]
    [SerializeField] private float noGravityTime = 0.08f;

    [Header("Physics")]
    [Tooltip("重力。負の値")]
    [SerializeField] private float gravity = -30f;

    [Tooltip("空中速度減衰")]
    [SerializeField] private float airDrag = 0.08f;

    [Tooltip("地面接触時の反発係数")]
    [SerializeField] private float bounce = 0.18f;

    [Tooltip("地面接触時の摩擦")]
    [Range(0f, 1f)]
    [SerializeField] private float groundFriction = 0.55f;

    [Tooltip("壁接触時の速度減衰")]
    [Range(0f, 1f)]
    [SerializeField] private float wallFriction = 0.85f;

    [Tooltip("この速度以下なら地面接触時にほぼ停止扱い")]
    [SerializeField] private float settleSpeed = 1.0f;

    [Header("Rotation")]
    [Tooltip("初期回転の強さ")]
    [SerializeField] private float angularPower = 720f;

    [Tooltip("速度方向から回転軸を作る割合")]
    [Range(0f, 1f)]
    [SerializeField] private float velocitySpinRate = 0.65f;

    [Tooltip("空中回転減衰")]
    [SerializeField] private float angularAirDamping = 1.8f;

    [Tooltip("地面接触後の回転減衰")]
    [SerializeField] private float angularGroundDamping = 7.0f;

    [Tooltip("衝突時の回転減衰")]
    [Range(0f, 1f)]
    [SerializeField] private float angularHitDamping = 0.72f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;

    [Tooltip("破片の簡易衝突半径")]
    [SerializeField] private float fragmentRadius = 0.06f;

    [Tooltip("衝突後にめり込み防止で離す距離")]
    [SerializeField] private float collisionSkin = 0.01f;

    [Tooltip("この法線Y以上なら地面扱い")]
    [Range(0f, 1f)]
    [SerializeField] private float groundNormalY = 0.55f;

    [Tooltip("高速移動時の分割移動数上限")]
    [SerializeField] private int maxMoveSubSteps = 6;

    [Tooltip("ONなら破片の縮小に合わせて衝突半径も縮小する")]
    [SerializeField] private bool scaleCollisionRadiusWithFragment = true;

    [Tooltip("縮小中の最小衝突半径。0に近すぎると床抜けしやすいので少し残す")]
    [SerializeField] private float minFragmentRadius = 0.005f;

    [Tooltip("collisionSkinもスケールに合わせて縮小する")]
    [SerializeField] private bool scaleCollisionSkinWithFragment = true;

    [Tooltip("縮小中の最小collisionSkin")]
    [SerializeField] private float minCollisionSkin = 0.001f;

    [Header("Fragment Scale")]
    [Tooltip("破壊中の破片スケール倍率。1なら初期localScaleそのまま。旧テストコードのVector3.oneに近づけたい場合は上げる")]
    [SerializeField] private float fragmentBreakScaleMultiplier = 1f;

    [Header("Shrink")]
    [Tooltip("地面接触後、縮小開始までの待ち時間")]
    [SerializeField] private float shrinkDelayAfterGround = 0.15f;

    [Tooltip("破壊後この秒数を超えたら、地面に着いていなくても強制的に縮小開始")]
    [SerializeField] private float forceShrinkStartTime = 3.0f;

    [Tooltip("縮小速度。1なら約1秒で0になる")]
    [SerializeField] private float shrinkSpeed = 1.5f;

    [Tooltip("縮小中も少しだけ滑らせる")]
    [SerializeField] private bool moveWhileShrinking = true;

    [Tooltip("アイテム落とすか？")]
    [SerializeField] private EnemyDropOnDestroy itemdrop;

    #endregion

    #region Runtime

    private readonly List<Fragment> fragments = new List<Fragment>();

    private bool broken;
    private TimeAgent timeAgent;

    #endregion

    #region Unity

    private void Awake()
    {
        timeAgent = GetComponent<TimeAgent>();

        SetupFragments();
    }

    private void OnEnable()
    {
        if (!broken)
        {
            RestoreBlockObjects();
        }
    }

    private void Update()
    {
        if (!broken)
            return;

        float timeScale = timeAgent != null ? timeAgent.TimeScale : 1f;
        if (timeScale <= 0f)
            return;

        float dt = Time.deltaTime * timeScale;

        int disabledCount = 0;

        for (int i = 0; i < fragments.Count; i++)
        {
            Fragment f = fragments[i];

            if (f == null || f.transform == null)
            {
                disabledCount++;
                continue;
            }

            if (f.disabled)
            {
                disabledCount++;
                continue;
            }

            UpdateFragment(f, dt);

            if (f.disabled)
                disabledCount++;
        }

        if (disabledCount >= fragments.Count)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region 初期化

    private void SetupFragments()
    {
        fragments.Clear();

        // Inspector未設定なら子から自動収集。
        // breakTriggerObject / blockCollisionObject / enemyAttackTriggerObject 自身とその子は破片扱いしない。
        if (fragmentTransforms == null || fragmentTransforms.Count == 0)
        {
            fragmentTransforms = new List<Transform>();

            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];

                if (t == transform)
                    continue;

                if (IsTransformInsideObject(t, breakTriggerObject))
                    continue;

                if (IsTransformInsideObject(t, blockCollisionObject))
                    continue;

                if (IsTransformInsideObject(t, enemyAttackTriggerObject))
                    continue;

                fragmentTransforms.Add(t);
            }
        }

        for (int i = 0; i < fragmentTransforms.Count; i++)
        {
            Transform t = fragmentTransforms[i];

            if (t == null)
                continue;

            Fragment f = new Fragment
            {
                transform = t,
                initialLocalPosition = t.localPosition,
                initialLocalRotation = t.localRotation,
                initialLocalScale = t.localScale,

                velocity = Vector3.zero,
                angularVelocity = Vector3.zero,

                aliveTime = 0f,
                groundedTime = 0f,
                noGravityTimer = 0f,
                scaleRate = 1f,

                shrinking = false,
                disabled = false,
                grounded = false,

                // 破片にColliderは無い前提なのでRendererだけ保持する。
                renderers = t.GetComponentsInChildren<Renderer>(true),

                localVisualCenter = Vector3.zero,
                visualRadius = fragmentRadius,
            };

            // ★重要：Renderer.bounds.centerを毎フレーム使わず、初期状態の中心をlocalに焼く
            CacheFragmentVisualInfo(f);

            fragments.Add(f);
        }

        ResetFragments();
    }

    private bool IsTransformInsideObject(Transform target, GameObject rootObject)
    {
        if (target == null || rootObject == null)
            return false;

        if (target == rootObject.transform)
            return true;

        return target.IsChildOf(rootObject.transform);
    }

    private float GetCurrentFragmentRadius(Fragment f)
    {
        if (f == null)
            return fragmentRadius;

        // InspectorのfragmentRadiusと、見た目サイズから出した半径の大きい方を使う。
        // 小さい破片はfragmentRadius、デカい破片はvisualRadiusが効く。
        float baseRadius = Mathf.Max(fragmentRadius, f.visualRadius);

        if (!scaleCollisionRadiusWithFragment)
            return baseRadius;

        float rate = Mathf.Clamp01(f.scaleRate);

        return Mathf.Max(baseRadius * rate, minFragmentRadius);
    }

    private float GetCurrentCollisionSkin(Fragment f)
    {
        if (f == null)
            return collisionSkin;

        if (!scaleCollisionSkinWithFragment)
            return collisionSkin;

        float rate = Mathf.Clamp01(f.scaleRate);

        return Mathf.Max(collisionSkin * rate, minCollisionSkin);
    }

    #endregion

    #region 更新

    private void UpdateFragment(Fragment f, float dt)
    {
        f.aliveTime += dt;

        if (f.grounded)
            f.groundedTime += dt;
        else
            f.groundedTime = 0f;

        if (!f.shrinking)
        {
            // 地面に着いてから一定時間後に縮小開始
            if (f.grounded && f.groundedTime >= shrinkDelayAfterGround)
            {
                StartShrink(f);
            }
            // 保険：破壊から一定時間後に強制縮小
            else if (f.aliveTime >= forceShrinkStartTime)
            {
                StartShrink(f);
            }
        }

        if (f.shrinking)
        {
            UpdateShrinkingFragment(f, dt);
            return;
        }

        UpdateFlyingFragment(f, dt);
    }

    private void UpdateFlyingFragment(Fragment f, float dt)
    {
        if (f.noGravityTimer > 0f)
        {
            f.noGravityTimer -= dt;
        }
        else
        {
            f.velocity.y += gravity * dt;
        }

        // 空中速度減衰
        f.velocity = Vector3.Lerp(
            f.velocity,
            Vector3.zero,
            Mathf.Clamp01(airDrag * dt)
        );

        MoveFragment(f, dt);

        ApplyFragmentRotation(f, dt, false);
    }

    private void UpdateShrinkingFragment(Fragment f, float dt)
    {
        if (moveWhileShrinking)
        {
            // 地面上で少し滑りながら止まる
            Vector3 horizontal = new Vector3(f.velocity.x, 0f, f.velocity.z);
            horizontal = Vector3.Lerp(
                horizontal,
                Vector3.zero,
                Mathf.Clamp01(groundFriction * 6f * dt)
            );

            f.velocity.x = horizontal.x;
            f.velocity.z = horizontal.z;
            f.velocity.y = 0f;

            MoveShrinkingFragment(f, horizontal * dt);
        }
        else
        {
            f.velocity = Vector3.zero;
        }

        ApplyFragmentRotation(f, dt, true);

        f.scaleRate -= shrinkSpeed * dt;

        if (f.scaleRate <= 0f)
        {
            DisableFragment(f);
            return;
        }

        f.transform.localScale = GetFragmentBreakScale(f) * f.scaleRate;
    }

    #endregion

    #region 移動 / 衝突

    private void MoveFragment(Fragment f, float dt)
    {
        Vector3 totalMove = f.velocity * dt;
        float totalDistance = totalMove.magnitude;

        if (totalDistance <= 0.00001f)
            return;

        float currentRadius = GetCurrentFragmentRadius(f);

        int stepCount = Mathf.CeilToInt(
            totalDistance / Mathf.Max(currentRadius * 1.5f, 0.01f)
        );

        stepCount = Mathf.Clamp(stepCount, 1, Mathf.Max(1, maxMoveSubSteps));

        Vector3 stepMove = totalMove / stepCount;

        for (int i = 0; i < stepCount; i++)
        {
            if (f.disabled || f.shrinking)
                return;

            float distance = stepMove.magnitude;
            if (distance <= 0.00001f)
                return;

            Vector3 dir = stepMove / distance;

            currentRadius = GetCurrentFragmentRadius(f);
            float currentSkin = GetCurrentCollisionSkin(f);

            // Transform.positionではなく、Renderer中心を基準にSphereCastする。
            // デザイナー製モデルのpivotズレ対策。
            Vector3 center = GetFragmentCenter(f);

            if (Physics.SphereCast(
                    center,
                    currentRadius,
                    dir,
                    out RaycastHit hit,
                    distance + currentSkin,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                ResolveFragmentCollision(f, hit);
                return;
            }

            f.transform.position += stepMove;
        }
    }

    private void MoveShrinkingFragment(Fragment f, Vector3 move)
    {
        if (f == null || f.transform == null)
            return;

        if (move.sqrMagnitude <= 0.000001f)
            return;

        float distance = move.magnitude;
        Vector3 dir = move / distance;

        float currentRadius = GetCurrentFragmentRadius(f);
        float currentSkin = GetCurrentCollisionSkin(f);

        Vector3 center = GetFragmentCenter(f);

        if (Physics.SphereCast(
                center,
                currentRadius,
                dir,
                out RaycastHit hit,
                distance + currentSkin,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 normal = hit.normal.normalized;

            Vector3 targetCenter =
                hit.point + normal * (currentRadius + currentSkin);

            MoveFragmentCenterTo(f, targetCenter);

            // 縮小中は強く跳ねさせず、移動だけ止める
            f.velocity = Vector3.zero;
            return;
        }

        f.transform.position += move;
    }

    private void ResolveFragmentCollision(Fragment f, RaycastHit hit)
    {
        Vector3 normal = hit.normal.normalized;

        float currentRadius = GetCurrentFragmentRadius(f);
        float currentSkin = GetCurrentCollisionSkin(f);

        Vector3 targetCenter =
            hit.point + normal * (currentRadius + currentSkin);

        MoveFragmentCenterTo(f, targetCenter);

        bool isGround = normal.y >= groundNormalY;

        if (isGround)
        {
            ResolveGroundHit(f, normal);
        }
        else
        {
            ResolveWallHit(f, normal);
        }
    }

    private void ResolveGroundHit(Fragment f, Vector3 normal)
    {
        f.grounded = true;

        Vector3 horizontal = Vector3.ProjectOnPlane(f.velocity, normal);

        float fallingSpeed = Mathf.Max(-f.velocity.y, 0f);

        if (fallingSpeed > settleSpeed)
        {
            // 軽く跳ねる
            Vector3 reflected = Vector3.Reflect(f.velocity, normal);
            f.velocity = reflected * bounce;

            Vector3 reflectedHorizontal = new Vector3(f.velocity.x, 0f, f.velocity.z);
            reflectedHorizontal *= groundFriction;

            f.velocity.x = reflectedHorizontal.x;
            f.velocity.z = reflectedHorizontal.z;

            if (Mathf.Abs(f.velocity.y) <= settleSpeed)
                f.velocity.y = 0f;
        }
        else
        {
            // 地面に落ち着いた
            f.velocity = horizontal * groundFriction;
            f.velocity.y = 0f;
        }


        // 接地後に地面へ向かう速度成分が残っていたら消す
        float intoGround = Vector3.Dot(f.velocity, -normal);
        if (intoGround > 0f)
        {
            f.velocity += normal * intoGround;
        }

        // 接地時は回転を衝突っぽく減衰
        f.angularVelocity *= angularHitDamping;

    }

    private void ResolveWallHit(Fragment f, Vector3 normal)
    {
        f.grounded = false;

        Vector3 reflected = Vector3.Reflect(f.velocity, normal) * wallFriction;

        // 念のため、衝突面へ向かう成分を消す。
        // 角や斜面で再度めり込み続けるのを抑える。
        float intoSurface = Vector3.Dot(reflected, -normal);
        if (intoSurface > 0f)
        {
            reflected += normal * intoSurface;
        }

        f.velocity = reflected;
        f.angularVelocity = Vector3.Reflect(f.angularVelocity, normal) * angularHitDamping;
    }

    #endregion

    #region 回転

    private void ApplyFragmentRotation(Fragment f, float dt, bool groundedLike)
    {
        if (f == null || f.transform == null)
            return;

        if (f.angularVelocity.sqrMagnitude <= 0.0001f)
            return;

        // 見た目中心を回転前に保存する。
        // Transformのpivotがメッシュ中心からズレている破片でも、
        // 見た目中心を基準に回転させるため。
        Vector3 centerBefore = GetFragmentCenter(f);

        Vector3 rotateEuler = f.angularVelocity * dt;
        Quaternion deltaRotation = Quaternion.Euler(rotateEuler);

        // ワールド回転を適用
        f.transform.rotation = deltaRotation * f.transform.rotation;

        // 回転後、見た目中心がズレた分だけTransform位置を補正する。
        // これにより「親の地点を中心に回っているような動き」を抑える。
        Vector3 centerAfter = GetFragmentCenter(f);
        Vector3 centerDelta = centerBefore - centerAfter;

        f.transform.position += centerDelta;

        float damping = groundedLike ? angularGroundDamping : angularAirDamping;

        f.angularVelocity = Vector3.Lerp(
            f.angularVelocity,
            Vector3.zero,
            Mathf.Clamp01(damping * dt)
        );
    }

    private Vector3 CreateInitialAngularVelocity(Vector3 moveDir, float power)
    {
        Vector3 axisFromMove = Vector3.Cross(Vector3.up, moveDir);

        if (axisFromMove.sqrMagnitude < 0.001f)
            axisFromMove = Random.onUnitSphere;

        axisFromMove.Normalize();

        Vector3 randomAxis = Random.onUnitSphere.normalized;

        Vector3 finalAxis =
            Vector3.Slerp(randomAxis, axisFromMove, velocitySpinRate).normalized;

        return finalAxis *
               Random.Range(angularPower * 0.6f, angularPower * 1.35f) *
               Mathf.Clamp01(power / Mathf.Max(explosionPower, 0.01f));
    }

    #endregion

    #region 破壊

    public void Break()
    {
        Vector3 center = GetCenter();
        Break(center, Vector3.up, explosionPower);
    }

    public void Break(Vector3 hitPoint, Vector3 forceDir, float power)
    {
        if (broken)
            return;

        broken = true;

        if(itemdrop != null)
        {
            itemdrop.TryDrop();
        }

        if (breakTriggerObject != null)
            breakTriggerObject.SetActive(false);

        if (blockCollisionObject != null)
            blockCollisionObject.SetActive(false);

        if (enemyAttackTriggerObject != null)
            enemyAttackTriggerObject.SetActive(false);

        float safePower = Mathf.Max(power, 0.01f);

        Vector3 blockCenter = GetCenter();

        for (int i = 0; i < fragments.Count; i++)
        {
            Fragment f = fragments[i];

            if (f == null || f.transform == null)
                continue;

            f.transform.gameObject.SetActive(true);

            f.transform.localPosition = f.initialLocalPosition;
            f.transform.localRotation = f.initialLocalRotation;
            f.transform.localScale = GetFragmentBreakScale(f);

            f.aliveTime = 0f;
            f.groundedTime = 0f;
            f.noGravityTimer = noGravityTime;
            f.scaleRate = 1f;

            f.shrinking = false;
            f.disabled = false;
            f.grounded = false;

            // Transform.positionではなく、Renderer中心基準で爆発方向を作る。
            // デザイナー製分割モデルはpivotが中心に無いことが多いため。
            Vector3 fragmentCenter = GetFragmentCenter(f);

            Vector3 fromHit = fragmentCenter - hitPoint;

            if (fromHit.sqrMagnitude < 0.001f)
                fromHit = fragmentCenter - blockCenter;

            if (fromHit.sqrMagnitude < 0.001f)
                fromHit = Random.onUnitSphere;

            Vector3 outward = fromHit.normalized;

            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);

            Vector3 dir = Vector3.Slerp(
                outward,
                randomDir,
                randomDirectionRate
            ).normalized;

            Vector3 hitForceDir = forceDir;

            if (hitForceDir.sqrMagnitude < 0.001f)
                hitForceDir = outward;

            hitForceDir.Normalize();

            // ヒット方向 + 外向き + 上方向を混ぜる
            Vector3 finalDir =
                (dir * outwardPowerRate +
                 hitForceDir * 0.35f +
                 Vector3.up * 0.45f).normalized;

            float powerRandom = Random.Range(0.75f, 1.25f);

            f.velocity =
                finalDir * safePower * powerRandom +
                Vector3.up * Random.Range(upwardPower * 0.65f, upwardPower * 1.15f);

            f.angularVelocity = CreateInitialAngularVelocity(finalDir, safePower);
        }
    }

    private void StartShrink(Fragment f)
    {
        if (f == null || f.disabled)
            return;

        if (f.shrinking)
            return;

        f.shrinking = true;
        f.grounded = true;

        // 縮小開始時点で下向き速度は消す
        if (f.velocity.y < 0f)
            f.velocity.y = 0f;
    }

    private void DisableFragment(Fragment f)
    {
        if (f == null || f.transform == null)
            return;

        if (f.disabled)
            return;

        f.disabled = true;
        f.shrinking = false;

        f.velocity = Vector3.zero;
        f.angularVelocity = Vector3.zero;
        f.scaleRate = 0f;

        f.transform.localScale = Vector3.zero;
        f.transform.gameObject.SetActive(false);
    }

    #endregion

    #region Reset

    public void ResetFragments()
    {
        broken = false;

        RestoreBlockObjects();

        for (int i = 0; i < fragments.Count; i++)
        {
            Fragment f = fragments[i];

            if (f == null || f.transform == null)
                continue;

            f.transform.gameObject.SetActive(true);

            f.transform.localPosition = f.initialLocalPosition;
            f.transform.localRotation = f.initialLocalRotation;
            f.transform.localScale = f.initialLocalScale;

            f.velocity = Vector3.zero;
            f.angularVelocity = Vector3.zero;

            f.aliveTime = 0f;
            f.groundedTime = 0f;
            f.noGravityTimer = 0f;
            f.scaleRate = 1f;

            f.shrinking = false;
            f.disabled = false;
            f.grounded = false;
        }
    }

    private void RestoreBlockObjects()
    {
        if (breakTriggerObject != null)
            breakTriggerObject.SetActive(true);

        if (blockCollisionObject != null)
            blockCollisionObject.SetActive(true);

        if (enemyAttackTriggerObject != null)
            enemyAttackTriggerObject.SetActive(true);
    }

    #endregion

    #region Utility

    private Vector3 GetCenter()
    {
        if (fragments.Count == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] == null || fragments[i].transform == null)
                continue;

            sum += GetFragmentCenter(fragments[i]);
            count++;
        }

        if (count <= 0)
            return transform.position;

        return sum / count;
    }

    private Vector3 GetFragmentCenter(Fragment f)
    {
        if (f == null || f.transform == null)
            return transform.position;

        // ★Renderer.bounds.centerを毎回使わない。
        // 初期状態でキャッシュしたローカル中心を、現在のTransformへ変換する。
        return f.transform.TransformPoint(f.localVisualCenter);
    }

    private void MoveFragmentCenterTo(Fragment f, Vector3 targetCenter)
    {
        if (f == null || f.transform == null)
            return;

        Vector3 currentCenter = GetFragmentCenter(f);
        Vector3 delta = targetCenter - currentCenter;

        f.transform.position += delta;
    }

    private bool IsSameOrChild(GameObject target, GameObject root)
    {
        if (target == null || root == null)
            return false;

        if (target == root)
            return true;

        return target.transform.IsChildOf(root.transform);
    }

    private Vector3 GetFragmentBreakScale(Fragment f)
    {
        if (f == null)
            return Vector3.one;

        return f.initialLocalScale * fragmentBreakScaleMultiplier;
    }

    private void CacheFragmentVisualInfo(Fragment f)
{
    if (f == null || f.transform == null)
        return;

    if (f.renderers == null || f.renderers.Length == 0)
    {
        f.localVisualCenter = Vector3.zero;
        f.visualRadius = fragmentRadius;
        return;
    }

    bool hasBounds = false;
    Bounds worldBounds = new Bounds();

    for (int i = 0; i < f.renderers.Length; i++)
    {
        Renderer r = f.renderers[i];

        if (r == null)
            continue;

        if (!hasBounds)
        {
            worldBounds = r.bounds;
            hasBounds = true;
        }
        else
        {
            worldBounds.Encapsulate(r.bounds);
        }
    }

    if (!hasBounds)
    {
        f.localVisualCenter = Vector3.zero;
        f.visualRadius = fragmentRadius;
        return;
    }

    // ワールド中心を破片ローカルへ変換して固定する
    f.localVisualCenter = f.transform.InverseTransformPoint(worldBounds.center);

    // 破片サイズから半径を作る。
    // 完全な形状一致ではないが、fragmentRadius固定より破片サイズ差に強くなる。
    float radiusFromBounds = worldBounds.extents.magnitude;

    // ただし巨大すぎると動きが重くなるので、必要なら後で係数調整する。
    f.visualRadius = Mathf.Max(fragmentRadius, radiusFromBounds * 0.35f);
}

    #endregion

    #region Hit Interface

    public void OnHit(HitEventData data)
    {
        // break処理を呼び出すか判別するフラグ
        bool checkBreakTrigger = false;

        if (broken)
            return;


        if (breakTriggerObject != null)
        {
            if (IsSameOrChild(data.targetHitbox, breakTriggerObject))
                checkBreakTrigger = true;
        }

        if (blockCollisionObject != null)
        {
            if (IsSameOrChild(data.targetHitbox, blockCollisionObject))
                checkBreakTrigger = true;
        }

        if (enemyAttackTriggerObject != null)
        {
            // enemyAttackTriggerObject がブロック側の「敵攻撃を受ける判定」なら、
            // targetHitbox が enemyAttackTriggerObject 配下かどうかを見る。
            if (IsSameOrChild(data.targetHitbox, enemyAttackTriggerObject))
                checkBreakTrigger = true;
        }

        if (!checkBreakTrigger)
            return;

        float power = explosionPower;

        Vector3 hitPoint =
            data.attackerObject != null
                ? data.attackerObject.transform.position
                : transform.position;

        Vector3 forceDir = GetCenter() - hitPoint;

        if (forceDir.sqrMagnitude < 0.001f)
            forceDir = Vector3.up;

        Break(hitPoint, forceDir.normalized, power);

        if(MissionManager.Instance != null)
        {
            MissionManager.Instance.AddBreakCount();
        }
    }

    public void OnHitDetected(Hitbox selfHitbox, Collider other)
    {
        // このブロック側から攻撃する処理は現状なし
    }

    #endregion
}