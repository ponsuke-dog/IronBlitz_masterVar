using System.Collections;
using UnityEngine;

#region エフェクト実体
/// <summary>
/// 実際に再生されるエフェクト
/// ・Transform制御
/// ・追従
/// ・任意World Transform再生
/// ・終了判定
/// ・停止時の残留対策
/// </summary>
public class EffectInstance : MonoBehaviour
{
    public bool IsPooled { get; set; } = true;

    private EffectManager manager;
    public EffectData data { get; private set; }

    public GameObject SourcePrefab { get; private set; }

    private Transform followTarget;
    private Transform rotationTarget;
    private EffectPlayParam playParam;

    private ParticleSystem[] particleSystems;

    private bool canCheck;
    private bool isStopping;
    private bool isReleased;

    private float timer;

    // StopEmitting で止めたあと、Particle が生き続けた場合の保険
    private float stopTimer;
    private const float DefaultStopTimeout = 3.0f;

    private Coroutine enableCheckCoroutine;

    // 任意World Transform再生用
    private bool useWorldTransform;
    private Vector3 worldPosition;
    private Quaternion worldRotation;
    private Vector3 worldScale;

    // ====================================
    // Runtime Transform Override
    // ====================================

    private bool overrideRuntimePosition;
    private bool overrideRuntimeRotation;
    private bool overrideRuntimeScale;

    private Vector3 runtimePosition;
    private Quaternion runtimeRotation;
    private bool useRuntimeRotationOffset;
    private Quaternion runtimeRotationOffset = Quaternion.identity;
    private Vector3 runtimeScale = Vector3.one;


    #region 初期化

    public void Initialize(EffectManager manager, EffectData data)
    {
        this.manager = manager;
        this.data = data;
        this.SourcePrefab = data.prefab;

        CacheParticleSystems();
    }

    private void CacheParticleSystems()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    #endregion

    #region 再生

    /// <summary>
    /// Transform基準で再生。
    /// </summary>
    public void Play(Transform target, EffectPlayParam param)
    {
        ResetState();

        CacheParticleSystems();

        this.followTarget = target;
        this.playParam = param;

        useWorldTransform = false;

        ApplyTransform();
        RestartParticles();

        if (data.forceLifeTime > 0f)
        {
            timer = data.forceLifeTime;
        }

        StartEnableCheck();
    }

    /// <summary>
    /// 任意World Transformで再生。
    /// targetに依存しないので追従しない。
    /// </summary>
    public void PlayAt(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        EffectPlayParam param)
    {
        ResetState();

        CacheParticleSystems();

        this.followTarget = null;
        this.playParam = param;

        useWorldTransform = true;
        worldPosition = position;
        worldRotation = rotation;
        worldScale = scale;

        ApplyTransform();
        RestartParticles();

        if (data.forceLifeTime > 0f)
        {
            timer = data.forceLifeTime;
        }

        StartEnableCheck();
    }

    /// <summary>
    /// 再生前に必ずParticleを全消ししてから再生する。
    /// プール再利用時の残留対策。
    /// </summary>
    private void RestartParticles()
    {
        foreach (var ps in particleSystems)
        {
            if (ps == null)
                continue;

            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(false);
            ps.Play(false);
        }
    }

    public void Attach(
    Transform followTarget,
    Transform rotationTarget = null)
    {
        useWorldTransform = false;

        this.followTarget = followTarget;
        this.rotationTarget = rotationTarget;

        if (this.followTarget != null)
        {
            ApplyTransform();
        }
    }

    public void Detach()
    {
        followTarget = null;
    }

    #endregion

    #region 更新

    private void LateUpdate()
    {
        if (!canCheck)
            return;

        if (isReleased)
            return;

        if (ShouldFollowTarget())
        {
            ApplyTransform();
        }

        // 強制寿命
        if (data.forceLifeTime > 0f)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                ReturnToPool(true);
                return;
            }
        }

        // Stop() 後の自然終了待ち
        if (isStopping)
        {
            stopTimer -= Time.deltaTime;

            if (IsAllStopped())
            {
                ReturnToPool(true);
                return;
            }

            // Local / Trail / SubEmitter / Loop などで残り続ける場合の保険
            if (stopTimer <= 0f)
            {
                ReturnToPool(true);
                return;
            }

            return;
        }

        // 通常の自動終了
        if (data.autoRelease && IsAllStopped())
        {
            ReturnToPool(true);
        }
    }

    #endregion

    #region Runtime Transform Override

    public void SetPosition(Vector3 position)
    {
        overrideRuntimePosition = true;
        runtimePosition = position;
    }

    public void ClearPositionOverride()
    {
        overrideRuntimePosition = false;
    }

    public void SetRotation(Quaternion rotation)
    {
        overrideRuntimeRotation = true;
        runtimeRotation = rotation;
    }

    public void SetEulerRotation(Vector3 euler)
    {
        SetRotation(Quaternion.Euler(euler));
    }

    public void SetDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        SetRotation(
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up));
    }

    public void SetYawDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        SetRotation(
            Quaternion.LookRotation(
                direction,
                Vector3.up));
    }

    public void SetYawDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir =
            new Vector3(
                direction.x,
                0f,
                direction.y);

        dir.Normalize();

        SetRotation(
            Quaternion.LookRotation(
                dir,
                Vector3.up));
    }

    public void ClearRotationOverride()
    {
        overrideRuntimeRotation = false;
    }

    public void SetScale(Vector3 scale)
    {
        overrideRuntimeScale = true;
        runtimeScale = scale;
    }

    public void ClearScaleOverride()
    {
        overrideRuntimeScale = false;
    }

    public void ClearAllTransformOverrides()
    {
        overrideRuntimePosition = false;
        overrideRuntimeRotation = false;
        overrideRuntimeScale = false;

        useRuntimeRotationOffset = false;
        runtimeRotationOffset = Quaternion.identity;

    }

    public void ClearRotationOffset()
    {
        useRuntimeRotationOffset = false;
        runtimeRotationOffset = Quaternion.identity;
    }

    public void SetRotationOffset(Quaternion offset)
    {
        useRuntimeRotationOffset = true;
        runtimeRotationOffset = offset;
    }

    public void SetYawOffset(float yaw)
    {
        useRuntimeRotationOffset = true;

        runtimeRotationOffset =
            Quaternion.Euler(
                0f,
                yaw,
                0f);
    }

    public void SetYawOffset(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        float yaw =
            Mathf.Atan2(
                direction.x,
                direction.y)
            * Mathf.Rad2Deg;

        SetYawOffset(yaw);
    }

    public void SetYawOffset(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        float yaw =
            Mathf.Atan2(
                direction.x,
                direction.z)
            * Mathf.Rad2Deg;

        SetYawOffset(yaw);
    }

    #endregion

    #region Transform適用

    private bool ShouldFollowTarget()
    {
        if (useWorldTransform)
            return false;

        if (followTarget == null)
            return false;

        bool follow =
            playParam.overrideFollowTarget
                ? playParam.followTarget
                : data.followTarget;

        return follow;
    }

    private void ApplyTransform()
    {
        if (useWorldTransform)
        {
            ApplyWorldTransform();
        }
        else
        {
            ApplyTargetTransform();
        }
    }

    private void ApplyTargetTransform()
    {
        if (followTarget == null)
            return;

        // =========================
        // Position
        // =========================

        Vector3 position;

        if (playParam.overridePosition)
        {
            position =
                followTarget.position +
                playParam.positionOffset;
        }
        else
        {
            position =
                followTarget.position +
                data.positionOffset +
                playParam.positionOffset;
        }

        if (overrideRuntimePosition)
        {
            position = runtimePosition;
        }

        transform.position = position;

        // =========================
        // Rotation
        // =========================

        Quaternion rotation;

        if (playParam.overrideRotation)
        {
            rotation =
                Quaternion.Euler(playParam.rotationOffset);
        }
        else
        {
            Transform rotSource =
                rotationTarget != null
                 ? rotationTarget
                 : followTarget;

            rotation =
                rotSource.rotation *
                Quaternion.Euler(
                    data.rotationOffset +
                    playParam.rotationOffset);
        }


        if (overrideRuntimeRotation)
        {
            rotation = runtimeRotation;
        }
        else if (useRuntimeRotationOffset)
        {
            rotation *= runtimeRotationOffset;
        }


        transform.rotation = rotation;

        // =========================
        // Scale
        // =========================

        Vector3 scale;

        if (playParam.overrideScale)
        {
            scale = playParam.scale;
        }
        else
        {
            scale =
                Vector3.Scale(
                    data.scale,
                    playParam.scale);
        }

        if (overrideRuntimeScale)
        {
            scale = runtimeScale;
        }

        transform.localScale = scale;
    }

    private void ApplyWorldTransform()
    {
        // =========================
        // Position
        // =========================

        Vector3 position;

        if (playParam.overridePosition)
        {
            position =
                worldPosition +
                playParam.positionOffset;
        }
        else
        {
            position =
                worldPosition +
                data.positionOffset +
                playParam.positionOffset;
        }

        if (overrideRuntimePosition)
        {
            position = runtimePosition;
        }

        transform.position = position;

        // =========================
        // Rotation
        // =========================

        Quaternion rotation;

        if (playParam.overrideRotation)
        {
            rotation =
                Quaternion.Euler(playParam.rotationOffset);
        }
        else
        {
            rotation =
                worldRotation *
                Quaternion.Euler(
                    data.rotationOffset +
                    playParam.rotationOffset);
        }

        if (overrideRuntimeRotation)
        {
            rotation = runtimeRotation;
        }

        if (overrideRuntimeRotation)
        {
            rotation = runtimeRotation;
        }
        else if (useRuntimeRotationOffset)
        {
            rotation *= runtimeRotationOffset;
        }

        transform.rotation = rotation;

        // =========================
        // Scale
        // =========================

        Vector3 scale;

        if (playParam.overrideScale)
        {
            scale = playParam.scale;
        }
        else
        {
            scale =
                Vector3.Scale(
                    Vector3.Scale(
                        data.scale,
                        worldScale),
                    playParam.scale);
        }

        if (overrideRuntimeScale)
        {
            scale = runtimeScale;
        }

        transform.localScale = scale;
    }

    #endregion

    #region Particle終了判定

    private bool IsAllStopped()
    {
        if (particleSystems == null)
            return true;

        foreach (var ps in particleSystems)
        {
            if (ps == null)
                continue;

            if (ps.IsAlive(true))
                return false;
        }

        return true;
    }

    #endregion

    #region データのリセット

    private void ResetState()
    {
        if (enableCheckCoroutine != null)
        {
            StopCoroutine(enableCheckCoroutine);
            enableCheckCoroutine = null;
        }

        canCheck = false;
        isStopping = false;
        isReleased = false;


        followTarget = null;
        rotationTarget = null;
        playParam = EffectPlayParam.Default;


        useWorldTransform = false;
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;
        worldScale = Vector3.one;

        overrideRuntimePosition = false;
        overrideRuntimeRotation = false;
        overrideRuntimeScale = false;

        runtimePosition = Vector3.zero;
        runtimeRotation = Quaternion.identity;
        runtimeScale = Vector3.one;
        useRuntimeRotationOffset = false;
        runtimeRotationOffset = Quaternion.identity;


        timer = 0f;
        stopTimer = 0f;
    }

    private void StartEnableCheck()
    {
        if (enableCheckCoroutine != null)
        {
            StopCoroutine(enableCheckCoroutine);
        }

        enableCheckCoroutine = StartCoroutine(EnableCheck());
    }

    private IEnumerator EnableCheck()
    {
        yield return null;
        yield return null;

        canCheck = true;
        enableCheckCoroutine = null;
    }

    #endregion

    #region 停止

    /// <summary>
    /// 外部から停止。
    /// ループエフェクト用。
    /// Stop後は追従解除してその場に残し、自然消滅を待つ。
    /// ただし一定時間後には強制的にClearしてプールへ返す。
    /// </summary>
    public void Stop()
    {
        if (isReleased)
            return;

        if (isStopping)
            return;

        isStopping = true;
        canCheck = true;

        followTarget = null;
        rotationTarget = null;
        useWorldTransform = true;
        worldPosition = transform.position;
        worldRotation = transform.rotation;
        worldScale = transform.localScale;

        stopTimer = DefaultStopTimeout;

        foreach (var ps in particleSystems)
        {
            if (ps == null)
                continue;

            // 余韻を残す停止
            // 既存ParticleはLifetimeが尽きるまで残る
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// 即停止。
    /// 画面上からすぐ消したい場合はこちら。
    /// </summary>
    public void StopImmediate()
    {
        if (isReleased)
            return;

        ReturnToPool(true);
    }

    #endregion

    #region 返却

    private void ReturnToPool(bool clearParticles)
    {
        if (isReleased)
            return;

        isReleased = true;
        canCheck = false;
        isStopping = false;

        if (enableCheckCoroutine != null)
        {
            StopCoroutine(enableCheckCoroutine);
            enableCheckCoroutine = null;
        }

        followTarget = null;
        rotationTarget = null;

        if (clearParticles)
        {
            ClearParticles();
        }

        if (manager == null)
        {
            gameObject.SetActive(false);
            return;
        }

        manager.Release(this);
    }

    /// <summary>
    /// Particleの残留を完全に消す。
    /// プール返却前・再生前に使う。
    /// </summary>
    private void ClearParticles()
    {
        if (particleSystems == null)
            return;

        foreach (var ps in particleSystems)
        {
            if (ps == null)
                continue;

            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(false);
        }
    }

    #endregion
}
#endregion