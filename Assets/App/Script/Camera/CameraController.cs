using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cinemachine;

[RequireComponent(typeof(TimeAgent))]
public class CameraController : MonoBehaviour
{
    [System.Serializable]
    private class CameraParam
    {
        [Header("Default InGame Camera")]
        [Tooltip("CameraManagerを使わない場合の初期InGameカメラです。CameraManager使用時は自動で差し替えられます。")]
        public CinemachineVirtualCamera defaultVirtualCamera;

        [Header("FOV")]
        [Tooltip("ONなら各InGameカメラの初期FOVを通常FOVとして使います。複数InGameカメラを使うならON推奨です。")]
        public bool useCameraInitialFOVAsNormal = true;

        [Tooltip("useCameraInitialFOVAsNormalがOFFの時に使う通常FOVです。")]
        public float normalFOV = 60f;

        [Tooltip("タックル中のFOVです。")]
        public float tackleFOV = 70f;

        [Header("FOV補間")]
        [Tooltip("FOV変更の滑らかさです。小さいほど速く、大きいほどゆっくり変化します。")]
        public float fovSmoothTime = 0.15f;
    }

    [SerializeField] private CameraParam param;

    private TimeAgent agent;
    private CinemachineVirtualCamera currentVirtualCamera;

    private float baseFOV;
    private float currentFOV;
    private float targetFOV;
    private float fovVelocity;

    private bool isTackleFOVActive = false;

    private CinemachineImpulseSource impulse;

    [Header("Post Process")]
    [SerializeField] private Volume postProcessVolume;

    private ChromaticAberration chroma;
    private float chromaBase;
    private float chromaTimer;
    private float chromaDuration;

    private void Awake()
    {
        agent = GetComponent<TimeAgent>();
        impulse = GetComponent<CinemachineImpulseSource>();

        currentVirtualCamera = param.defaultVirtualCamera;
    }

    private void Start()
    {
        InitializeFOVFromCurrentCamera();

        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out chroma);

            if (chroma != null)
            {
                chroma.active = true;                      // ★ 念のため有効化
                chroma.intensity.overrideState = true;     // ★ ここがOFFだと値を変えても効かない
                chromaBase = chroma.intensity.value;
            }
            else
            {
                Debug.LogWarning("[CameraController] ChromaticAberration が VolumeProfile にありません。");
            }
        }
        else
        {
            Debug.LogWarning("[CameraController] postProcessVolume / profile が未設定です。");
        }
    }

    private void Update()
    {
        if (agent != null && agent.TimeScale <= 0f)
            return;

        float dt = Time.deltaTime;

        if (agent != null)
            dt *= agent.TimeScale;

        UpdateFOV(dt);
        UpdateChromatic(dt);
    }

    /// <summary>
    /// CameraManagerから現在のInGameカメラを通知する。
    /// InGameカメラが切り替わった時も、タックル中ならタックルFOVを維持する。
    /// </summary>
    public void SetActiveInGameCamera(CinemachineVirtualCamera camera)
    {
        if (camera == null)
            return;

        currentVirtualCamera = camera;

        baseFOV = param.useCameraInitialFOVAsNormal
            ? currentVirtualCamera.m_Lens.FieldOfView
            : param.normalFOV;

        currentFOV = currentVirtualCamera.m_Lens.FieldOfView;
        targetFOV = isTackleFOVActive ? param.tackleFOV : baseFOV;
        fovVelocity = 0f;
    }

    private void InitializeFOVFromCurrentCamera()
    {
        if (currentVirtualCamera != null)
        {
            baseFOV = param.useCameraInitialFOVAsNormal
                ? currentVirtualCamera.m_Lens.FieldOfView
                : param.normalFOV;

            currentFOV = currentVirtualCamera.m_Lens.FieldOfView;
            targetFOV = baseFOV;
        }
        else
        {
            baseFOV = param.normalFOV;
            currentFOV = param.normalFOV;
            targetFOV = param.normalFOV;
        }
    }

    private void UpdateFOV(float dt)
    {
        if (currentVirtualCamera == null)
            return;

        currentFOV = Mathf.SmoothDamp(
            currentFOV,
            targetFOV,
            ref fovVelocity,
            Mathf.Max(param.fovSmoothTime, 0.001f),
            Mathf.Infinity,
            dt
        );

        currentVirtualCamera.m_Lens.FieldOfView = currentFOV;
    }

    private void UpdateChromatic(float dt)
    {
        if (chroma == null)
            return;

        if (chromaTimer <= 0f)
            return;

        chromaTimer -= dt;

        float t = (chromaDuration > 0f) ? (chromaTimer / chromaDuration) : 0f;
        t = Mathf.Clamp01(t);

        // t=1で最大、0で基準値へ戻る
        chroma.intensity.value = Mathf.Lerp(chromaBase, 1f, t);

        if (chromaTimer <= 0f)
        {
            chromaTimer = 0f;
            chroma.intensity.value = chromaBase;
        }
    }

    public void PlayShake(Vector3 dir, float power, float duration)
    {
        if (impulse != null)
        {
            impulse.GenerateImpulse(dir * power);
        }

        if (chroma != null)
        {
            chromaDuration = Mathf.Max(duration, 0.01f);
            chromaTimer = chromaDuration;
            chroma.intensity.value = 1f;
        }
    }

    public void OnTackleStart()
    {
        isTackleFOVActive = true;
        targetFOV = param.tackleFOV;
    }

    public void OnTackleEnd()
    {
        isTackleFOVActive = false;
        targetFOV = baseFOV;
    }

    public void SetNormalFOVImmediate()
    {
        isTackleFOVActive = false;

        targetFOV = baseFOV;
        currentFOV = baseFOV;
        fovVelocity = 0f;

        if (currentVirtualCamera != null)
            currentVirtualCamera.m_Lens.FieldOfView = currentFOV;
    }


    public void SetActiveCamera(CinemachineVirtualCamera camera)
    {
        SetActiveInGameCamera(camera);
    }

}