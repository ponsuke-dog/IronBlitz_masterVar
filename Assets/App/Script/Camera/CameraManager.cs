using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UIElements;

[System.Serializable]
public class CameraShotEvent : UnityEvent<CameraShot> { }

public enum CameraManagerState
{
    None,

    // TimelineでPreviewカメラ演出を再生中
    PreviewTimeline,

    // Timeline終了後、Preview最後の状態を維持したままFadeOut中
    PreviewFadeOut,

    // 完全暗転後、PreviewカメラからInGameカメラへ切り替える瞬間
    SwitchToInGameCamera,

    // InGameカメラへ切り替えた後、FadeIn中
    InGameFadeIn,

    // 通常プレイ中
    InGame,

    // Cutカメラ割り込み中
    Cut
}

public enum TimelinePreviewEndMode
{
    // Timelineの最後のフレームで固定する
    HoldLastFrame,

    // FadeOut中もTimelineを進め続ける
    ContinueTimelineDuringFade,

    // Timeline最後の状態から指定Transformを指定方向へ引く
    PullBackAfterTimelineEnd
}

public enum PreviewPullBackDirectionSpace
{
    // previewPullBackTarget のローカル方向基準。
    // カメラ基準で「後ろ」「斜め上後ろ」「斜め右後ろ」などにしたい場合はこちら。
    TargetLocal,

    // ワールド方向基準。
    // シーン全体で固定方向へ動かしたい場合はこちら。
    World
}

public enum CameraFadeMode
{
    // FadeManager.Instance を使う
    FadeManager,

    // NotifyFadeOutCompleted / NotifyFadeInCompleted を外部から呼ぶ方式
    ExternalNotify,

    // 仮時間で自動進行
    TemporaryTimer
}

public class CameraManager : MonoBehaviour
{
    #region Inspector - 必須参照

    [Header("必須参照")]
    [Tooltip("Main CameraについているCinemachineBrainです。未設定ならCamera.mainから自動取得します。")]
    [SerializeField] private CinemachineBrain brain;

    [Tooltip("ステージ入場Previewを再生するPlayableDirectorです。TimelineでCinemachine Trackを組みます。")]
    [SerializeField] private PlayableDirector previewTimelineDirector;

    [Tooltip("Timelineで使うPreview用VirtualCamera群の親です。暗転後に非Active化します。")]
    [SerializeField] private Transform previewTimelineCameraRoot;

    [Tooltip("InGame用VirtualCameraを子に持つ親です。上からInGame_00, InGame_01...として扱います。")]
    [SerializeField] private Transform inGameCameraRoot;

    [Tooltip("Cut用VirtualCameraを子に持つ親です。上からCut_00, Cut_01...として扱います。")]
    [SerializeField] private Transform cutCameraRoot;

    [Tooltip("InGame中のFOV変更、Shake、色収差などを担当するCameraControllerです。")]
    [SerializeField] private CameraController cameraController;

    #endregion

    #region Inspector - Preview設定

    [Header("Preview設定")]
    [Tooltip("ONならStart時にPreview Timelineを再生します。OFFなら最初からInGameへ入ります。")]
    [SerializeField] private bool playPreviewOnStart = true;

    [Tooltip("Preview終了後、FadeOut中に最後のPreview状態をどう扱うか。")]
    [SerializeField] private TimelinePreviewEndMode previewEndMode = TimelinePreviewEndMode.HoldLastFrame;

    [Tooltip("PullBackAfterTimelineEndの時に動かす対象です。最後のPreviewカメラ、またはその親Rootを入れます。")]
    [SerializeField] private Transform previewPullBackTarget;

    [Tooltip("PullBackAfterTimelineEnd時の移動速度です。")]
    [SerializeField] private float previewPullBackSpeed = 1.5f;

    [Tooltip("PullBackAfterTimelineEnd時の移動方向をTargetLocal基準にするかWorld基準にするか。基本はTargetLocal推奨です。")]
    [SerializeField] private PreviewPullBackDirectionSpace previewPullBackDirectionSpace = PreviewPullBackDirectionSpace.TargetLocal;

    [Tooltip("PullBackAfterTimelineEnd時の移動方向です。TargetLocalの場合、(0,0,-1)=真後ろ、(0,0.4,-1)=斜め上後ろ、(0.4,0,-1)=斜め右後ろです。")]
    [SerializeField] private Vector3 previewPullBackDirection = new Vector3(0f, 0f, -1f);

    [Tooltip("ONならPreview/Cut/Fade待ちをTime.timeScaleの影響外で進めます。")]
    [SerializeField] private bool useUnscaledTime = true;

    #endregion

    #region Inspector - Fade設定

    [Header("Fade設定")]
    [Tooltip("CameraManagerがFadeをどう進めるか。基本はFadeManager推奨です。")]
    [SerializeField] private CameraFadeMode fadeMode = CameraFadeMode.FadeManager;

    [Tooltip("Preview終了後、暗転する時に使うFadePresetです。FadeManagerモードで使用します。")]
    [SerializeField] private FadePreset fadeOutPreset;

    [Tooltip("InGameカメラへ切り替えた後、明転する時に使うFadePresetです。FadeManagerモードで使用します。")]
    [SerializeField] private FadePreset fadeInPreset;

    [Tooltip("TemporaryTimerモードの時、FadeOut開始後にこの秒数で暗転完了扱いにします。")]
    [SerializeField] private float temporaryFadeOutWait = 0.5f;

    [Tooltip("TemporaryTimerモードの時、FadeIn開始後にこの秒数で明転完了扱いにします。")]
    [SerializeField] private float temporaryFadeInWait = 0.5f;

    #endregion

    #region Inspector - InGame設定

    [Header("InGame設定")]
    [Tooltip("Preview終了後に使うInGameカメラ番号です。InGameCameras配下の上から0です。")]
    [SerializeField] private int defaultInGameCameraIndex = 0;

    [Tooltip("有効にしたいVirtualCameraへ設定するPriorityです。")]
    [SerializeField] private int activePriority = 100;

    [Tooltip("無効扱いにするVirtualCameraへ設定するPriorityです。")]
    [SerializeField] private int inactivePriority = 0;

    #endregion


    #region Inspector - Skip設定

    [Header("Skip設定")]
    [Tooltip("ONならInputSystemのScenes/Skipを使います。OFFなら仮キーを使います。")]
    [SerializeField] private bool useInputSystemSkip = true;

    [Tooltip("Previewスキップに使うActionMap名です。")]
    [SerializeField] private string skipActionMapName = "Scenes";

    [Tooltip("Previewスキップに使うAction名です。Hold Interaction付き想定です。")]
    [SerializeField] private string skipActionName = "Skip";

    [Tooltip("InputSystemを使わない場合の仮キーです。")]
    [SerializeField] private KeyCode temporarySkipKey = KeyCode.Space;

    [Tooltip("仮キー使用時のみ、この秒数押し続けるとPreviewをスキップします。InputSystemのHold使用時はAction側の設定を使います。")]
    [SerializeField] private float skipHoldTime = 1.0f;

    [Header("Pause設定")]
    [Tooltip("ONならTime.timeScaleが0以下の間、Preview Timelineの手動更新を停止します。")]
    [SerializeField] private bool pausePreviewWhenGamePaused = true;

    #endregion


    #region Inspector - Events

    [Header("Events - Preview")]
    [Tooltip("Preview開始時。Player Input無効化、通常UI非表示、Preview用UI表示など。")]
    public UnityEvent onPreviewStarted;

    [Tooltip("Preview Timelineが最後まで再生された時。この後FadeOutへ進みます。")]
    public UnityEvent onPreviewTimelineFinished;

    [Tooltip("Previewがスキップされた時。この後FadeOutへ進みます。")]
    public UnityEvent onPreviewSkipped;

    [Header("Events - Fade")]
    [Tooltip("FadeOut開始要求。FadeManagerを使わない追加演出などを差し込めます。")]
    public UnityEvent onRequestFadeOut;

    [Tooltip("FadeOut完了扱い。完全暗転した直後に呼ばれます。")]
    public UnityEvent onFadeOutCompleted;

    [Tooltip("FadeIn開始要求。FadeManagerを使わない追加演出などを差し込めます。")]
    public UnityEvent onRequestFadeIn;

    [Tooltip("FadeIn完了扱い。完全に明るくなった直後に呼ばれます。")]
    public UnityEvent onFadeInCompleted;

    [Header("Events - InGame")]
    [Tooltip("暗転中にInGameカメラへ切り替わった直後。ゲームUI有効化などはここが向いています。")]
    public UnityEvent onInGameCameraReady;

    [Tooltip("FadeIn完了後のInGame開始時。InputSystem有効化、タイマー開始などはここが向いています。")]
    public UnityEvent onInGameStarted;

    [Tooltip("InGameカメラが切り替わった時。")]
    public CameraShotEvent onInGameCameraChanged;

    [Header("Events - Cut")]
    [Tooltip("Cutカメラ開始時。")]
    public CameraShotEvent onCutCameraStarted;

    [Tooltip("CutカメラからInGameカメラへ戻り始める時。")]
    public CameraShotEvent onCutCameraReturnStarted;

    [Tooltip("Cutカメラ終了時。")]
    public CameraShotEvent onCutCameraFinished;

    #endregion

    #region Inspector - Debug

    [Header("Debug")]
    [SerializeField] private CameraManagerState state = CameraManagerState.None;

    [Tooltip("ONならCameraManagerの状態遷移ログをConsoleに出します。")]
    [SerializeField] private bool debugLog = true;

    [Header("Debug - Camera Test Keys")]
    [Tooltip("ONならInGame/Cut中に仮キー入力でCut/Shake/FOVテストを行います。")]
    [SerializeField] private bool enableDebugCameraTestKeys = true;

    #endregion

    #region Runtime

    private readonly List<CameraShot> allShots = new List<CameraShot>();
    private readonly List<CameraShot> inGameShots = new List<CameraShot>();
    private readonly List<CameraShot> cutShots = new List<CameraShot>();
    private readonly Dictionary<string, CameraShot> shotMap = new Dictionary<string, CameraShot>();

    private CinemachineBlendDefinition defaultBrainBlend;

    private CameraShot currentShot;
    private CameraShot currentInGameShot;

    private Coroutine previewRoutine;
    private Coroutine cutRoutine;

    private CameraShot activeCutShot;

    private bool manualCutActive = false;
    private CameraShot manualCutShot;
    private CameraShot manualReturnShot;

    private bool skipRequested = false;
    private float skipTimer = 0f;

    private bool skipActionMapEnabledByCameraManager = false;
    private bool skipActionCallbackRegistered = false;


    private InputActionMap skipActionMap;
    private InputAction skipAction;
    private bool skipActionEnabledByCameraManager = false;


    private bool fadeOutCompleted = false;
    private bool fadeInCompleted = false;


    public CameraManagerState State => state;
    public CameraShot CurrentInGameShot => currentInGameShot;

    public bool IsManualCutActive => manualCutActive;
    public CameraShot CurrentManualCutShot => manualCutShot;
    public CameraShot CurrentActiveCutShot => activeCutShot;



    private InputActionMap playerActionMap;
    private bool playerInputLockedByCameraManager = false;

    private bool forceDisablePlayerInputUntilInGame = true;

    private string playerActionMapName = "Player";


    #endregion

    #region Unity

    private void Awake()
    {
        if (brain == null)
            brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;

        if (brain != null)
            defaultBrainBlend = brain.m_DefaultBlend;

        PreparePreviewTimelineDirector();

        CollectCameraShots();
        SetAllShotCamerasInactive();

        Log($"Awake完了 / InGame={inGameShots.Count}, Cut={cutShots.Count}, Brain={(brain != null)}, Timeline={(previewTimelineDirector != null)}");
    }

    private void Start()
    {
        CacheSkipInputAction();
        CachePlayerInputActionMap();

        if (playPreviewOnStart)
        {
            PlayStagePreview();
        }
        else
        {
            SwitchInGameCamera(defaultInGameCameraIndex, true);
            EnterInGameState();
        }
    }

    private void Update()
    {
        // Preview / Fade中に外部からPlayer入力がEnableされても、ここで毎フレーム止める
        ForceDisablePlayerInputIfLocked();

        // Preview中はInputSystem/仮キーどちらでも、ここで長押し時間を測る。
        if (state == CameraManagerState.PreviewTimeline)
        {
            UpdatePreviewSkip();
        }

        if (Time.timeScale <= 0.0f)
        {
            return;
        }

        UpdateDebugCameraTestKeys();
    }


    private void OnDisable()
    {
        DisableSkipInputAction();

        // CameraManagerが無効化される時はロック状態だけ解除する。
        // シーン遷移中などで勝手にPlayer入力をEnableしたくない場合があるため、
        // ここではEnablePlayerInputNow()は呼ばない。
        playerInputLockedByCameraManager = false;
    }


    #endregion

    #region Debug Test Keys

    private void UpdateDebugCameraTestKeys()
    {
        if (!enableDebugCameraTestKeys)
            return;

        if (state != CameraManagerState.InGame && state != CameraManagerState.Cut)
            return;

        // Alpha1: Cut_00を短時間表示して自動で戻る
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Log("Debug Alpha1 / Cut_00 短時間テスト");
            PlayCutCamera(0, 0.35f, true);
        }

        // Alpha2: Cut_01があれば長めに表示。無ければ何もしない
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Log("Debug Alpha2 / Cut_01 長時間テスト");
            PlayCutCamera(1, 1.20f, true);
        }

        // Alpha3: 手動Cut開始。自動では戻らない
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Log("Debug Alpha3 / ManualCut開始");
            BeginManualCut(0);
        }

        // Alpha4: 手動Cut終了。元のInGameカメラへ戻る
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Log("Debug Alpha4 / ManualCut終了");
            EndManualCut(true);
        }

        // Alpha5: Cut割り込みテスト。Cut_01があればCut_01、無ければCut_00
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            int interruptIndex = cutShots.Count > 1 ? 1 : 0;

            Log($"Debug Alpha5 / Cut割り込みテスト index={interruptIndex}");
            PlayCutCamera(interruptIndex, 0.60f, true);
        }

        // Y: Shake + Chromatic テスト
        if (Input.GetKeyDown(KeyCode.Y))
        {
            Log("Debug Y / Shake + Chromatic テスト");

            if (cameraController != null)
                cameraController.PlayShake(Vector3.right, 1.0f, 0.25f);
        }

        // T: タックルFOV開始テスト
        if (Input.GetKeyDown(KeyCode.T))
        {
            Log("Debug T / Tackle FOV Start");

            if (cameraController != null)
                cameraController.OnTackleStart();
        }

        // G: タックルFOV終了テスト
        if (Input.GetKeyDown(KeyCode.G))
        {
            Log("Debug G / Tackle FOV End");

            if (cameraController != null)
                cameraController.OnTackleEnd();
        }

        // I: InGame_00へ切り替え
        if (Input.GetKeyDown(KeyCode.I))
        {
            Log("Debug I / InGame_00 切り替え");
            SwitchInGameCamera(0, false);
        }

        // O: InGame_01へ切り替え。無ければ何もしない
        if (Input.GetKeyDown(KeyCode.O))
        {
            Log("Debug O / InGame_01 切り替え");
            SwitchInGameCamera(1, false);
        }
    }

    #endregion

    #region Camera Collection

    public void RefreshCameraList()
    {
        CollectCameraShots();
    }

    private void CollectCameraShots()
    {
        allShots.Clear();
        inGameShots.Clear();
        cutShots.Clear();
        shotMap.Clear();

        CollectFromRoot(inGameCameraRoot, CameraShotKind.InGame, inGameShots);
        CollectFromRoot(cutCameraRoot, CameraShotKind.Cut, cutShots);

        SortByHierarchyOrder(inGameShots);
        SortByHierarchyOrder(cutShots);

        AssignRuntimeIds(inGameShots, "InGame");
        AssignRuntimeIds(cutShots, "Cut");

        Log($"CameraShot収集完了 / InGame={inGameShots.Count}, Cut={cutShots.Count}");
    }

    private void CollectFromRoot(Transform root, CameraShotKind kind, List<CameraShot> targetList)
    {
        if (root == null)
            return;

        CameraShot[] shots = root.GetComponentsInChildren<CameraShot>(true);

        for (int i = 0; i < shots.Length; i++)
        {
            CameraShot shot = shots[i];

            if (shot == null || shot.VirtualCamera == null)
                continue;

            shot.kind = kind;

            if (!allShots.Contains(shot))
                allShots.Add(shot);

            if (!targetList.Contains(shot))
                targetList.Add(shot);
        }
    }

    private void SortByHierarchyOrder(List<CameraShot> list)
    {
        list.Sort((a, b) =>
            a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    private void AssignRuntimeIds(List<CameraShot> shots, string prefix)
    {
        for (int i = 0; i < shots.Count; i++)
        {
            string id = string.IsNullOrEmpty(shots[i].idOverride)
                ? $"{prefix}_{i:00}"
                : shots[i].idOverride;

            shots[i].SetRuntimeId(id, i);

            if (!shotMap.ContainsKey(id))
                shotMap.Add(id, shots[i]);
        }
    }

    #endregion

    #region Preview Flow

    public void PlayStagePreview()
    {
        if (previewRoutine != null)
            StopCoroutine(previewRoutine);

        previewRoutine = StartCoroutine(PreviewRoutine());
    }

    private IEnumerator PreviewRoutine()
    {
        state = CameraManagerState.PreviewTimeline;

        skipRequested = false;
        skipTimer = 0f;

        EnablePreviewObjects();

        OnPreviewStarted();
        onPreviewStarted?.Invoke();

        if (previewTimelineDirector == null || previewTimelineDirector.playableAsset == null)
        {
            Log("PreviewTimelineDirector または playableAsset がありません。InGameへ進みます。");
            yield return PreviewToInGameRoutine(false);
            yield break;
        }

        PreparePreviewTimelineDirector();

        double duration = previewTimelineDirector.duration;

        if (duration <= 0.0)
        {
            Log("Preview Timeline duration が0以下です。InGameへ進みます。");
            yield return PreviewToInGameRoutine(false);
            yield break;
        }

        previewTimelineDirector.time = 0.0;
        previewTimelineDirector.Evaluate();

        Log($"Preview Timeline開始 / duration={duration}");

        double timelineTime = 0.0;

        while (!skipRequested && timelineTime < duration)
        {
            if (IsPreviewPausedByGamePause())
            {
                yield return null;
                continue;
            }

            timelineTime += GetDeltaTime();

            if (timelineTime > duration)
                timelineTime = duration;

            previewTimelineDirector.time = timelineTime;
            previewTimelineDirector.Evaluate();

            yield return null;
        }

        bool skipped = skipRequested;

        if (skipped)
        {
            Log("Preview Skip成立");

            OnPreviewSkipped();
            onPreviewSkipped?.Invoke();
        }
        else
        {
            PrepareTimelineEndForFadeOut();

            Log("Preview Timeline終了");

            OnPreviewTimelineFinished();
            onPreviewTimelineFinished?.Invoke();
        }

        yield return PreviewToInGameRoutine(skipped);
    }

    private IEnumerator PreviewToInGameRoutine(bool skipped)
    {
        state = CameraManagerState.PreviewFadeOut;

        fadeOutCompleted = false;

        Log("FadeOut開始要求");

        OnRequestFadeOut();
        onRequestFadeOut?.Invoke();

        yield return RunFadeOut();

        Log("FadeOut完了");

        OnFadeOutCompleted();
        onFadeOutCompleted?.Invoke();

        state = CameraManagerState.SwitchToInGameCamera;

        ReleasePreviewTimeline();

        bool switched = SwitchInGameCamera(defaultInGameCameraIndex, true);

        if (!switched)
        {
            Debug.LogError($"[CameraManager] InGameカメラ切り替え失敗 / index={defaultInGameCameraIndex}, count={inGameShots.Count}");
        }

        ForceBrainUpdate();

        Log("InGameカメラ準備完了");

        OnPreviewCameraSwitchedToInGame();
        onInGameCameraReady?.Invoke();

        state = CameraManagerState.InGameFadeIn;

        fadeInCompleted = false;

        Log("FadeIn開始要求");

        OnRequestFadeIn();
        onRequestFadeIn?.Invoke();

        yield return RunFadeIn();

        Log("FadeIn完了");

        OnFadeInCompleted();
        onFadeInCompleted?.Invoke();

        EnterInGameState();

        previewRoutine = null;
    }

    private void EnablePreviewObjects()
    {
        if (previewTimelineCameraRoot != null)
            previewTimelineCameraRoot.gameObject.SetActive(true);

        if (previewTimelineDirector != null)
            previewTimelineDirector.enabled = true;
    }

    private void PreparePreviewTimelineDirector()
    {
        if (previewTimelineDirector == null)
            return;

        previewTimelineDirector.playOnAwake = false;
        previewTimelineDirector.timeUpdateMode = DirectorUpdateMode.Manual;
    }

    private void PrepareTimelineEndForFadeOut()
    {
        if (previewTimelineDirector == null)
            return;

        switch (previewEndMode)
        {
            case TimelinePreviewEndMode.HoldLastFrame:
                previewTimelineDirector.time = previewTimelineDirector.duration;
                previewTimelineDirector.Evaluate();
                break;

            case TimelinePreviewEndMode.ContinueTimelineDuringFade:
                break;

            case TimelinePreviewEndMode.PullBackAfterTimelineEnd:
                previewTimelineDirector.time = previewTimelineDirector.duration;
                previewTimelineDirector.Evaluate();
                break;
        }
    }

    private IEnumerator RunFadeOut()
    {
        switch (fadeMode)
        {
            case CameraFadeMode.FadeManager:
                if (FadeManager.Instance != null)
                {
                    yield return FadeOutWithFadeManager();
                }
                else
                {
                    Debug.LogWarning("[CameraManager] FadeManager.Instance がありません。TemporaryTimerで進行します。");
                    yield return WaitFadeOutByTimer();
                }
                break;

            case CameraFadeMode.ExternalNotify:
                yield return WaitFadeOutByNotify();
                break;

            case CameraFadeMode.TemporaryTimer:
                yield return WaitFadeOutByTimer();
                break;
        }
    }

    private IEnumerator RunFadeIn()
    {
        switch (fadeMode)
        {
            case CameraFadeMode.FadeManager:
                if (FadeManager.Instance != null)
                {
                    yield return FadeInWithFadeManager();
                }
                else
                {
                    Debug.LogWarning("[CameraManager] FadeManager.Instance がありません。TemporaryTimerで進行します。");
                    yield return WaitFadeInByTimer();
                }
                break;

            case CameraFadeMode.ExternalNotify:
                yield return WaitFadeInByNotify();
                break;

            case CameraFadeMode.TemporaryTimer:
                yield return WaitFadeInByTimer();
                break;
        }
    }

    private IEnumerator FadeOutWithFadeManager()
    {
        bool completed = false;

        FadeManager.Instance.FadeToBlack(fadeOutPreset, () =>
        {
            completed = true;
        });

        while (!completed)
        {
            UpdatePreviewDuringFadeOut();
            yield return null;
        }

        fadeOutCompleted = true;
    }

    private IEnumerator FadeInWithFadeManager()
    {
        bool completed = false;

        FadeManager.Instance.FadeFromBlack(fadeInPreset, () =>
        {
            completed = true;
        });

        while (!completed)
        {
            yield return null;
        }

        fadeInCompleted = true;
    }

    private IEnumerator WaitFadeOutByNotify()
    {
        while (!fadeOutCompleted)
        {
            UpdatePreviewDuringFadeOut();
            yield return null;
        }
    }

    private IEnumerator WaitFadeInByNotify()
    {
        while (!fadeInCompleted)
        {
            yield return null;
        }
    }

    private IEnumerator WaitFadeOutByTimer()
    {
        float timer = 0f;

        while (timer < temporaryFadeOutWait)
        {
            if (IsPreviewPausedByGamePause())
            {
                yield return null;
                continue;
            }

            UpdatePreviewDuringFadeOut();
            timer += GetDeltaTime();
            yield return null;
        }

        fadeOutCompleted = true;
    }

    private IEnumerator WaitFadeInByTimer()
    {
        float timer = 0f;

        while (timer < temporaryFadeInWait)
        {
            if (IsPreviewPausedByGamePause())
            {
                yield return null;
                continue;
            }

            timer += GetDeltaTime();
            yield return null;
        }

        fadeInCompleted = true;
    }

    private void UpdatePreviewDuringFadeOut()
    {
        if (IsPreviewPausedByGamePause())
            return;

        if (previewEndMode == TimelinePreviewEndMode.ContinueTimelineDuringFade)
        {
            if (previewTimelineDirector == null || previewTimelineDirector.playableAsset == null)
                return;

            double duration = previewTimelineDirector.duration;

            if (duration <= 0.0)
                return;

            double nextTime = previewTimelineDirector.time + GetDeltaTime();

            while (nextTime > duration)
                nextTime -= duration;

            previewTimelineDirector.time = nextTime;
            previewTimelineDirector.Evaluate();
            return;
        }

        if (previewEndMode == TimelinePreviewEndMode.PullBackAfterTimelineEnd)
        {
            if (previewPullBackTarget == null)
                return;

            Vector3 direction = GetPreviewPullBackDirection();

            previewPullBackTarget.position +=
                direction * previewPullBackSpeed * GetDeltaTime();
        }
    }

    private Vector3 GetPreviewPullBackDirection()
    {
        if (previewPullBackTarget == null)
            return Vector3.back;

        Vector3 direction = previewPullBackDirection;

        if (direction.sqrMagnitude < 0.0001f)
            direction = new Vector3(0f, 0f, -1f);

        direction.Normalize();

        if (previewPullBackDirectionSpace == PreviewPullBackDirectionSpace.TargetLocal)
        {
            direction = previewPullBackTarget.TransformDirection(direction);
        }

        if (direction.sqrMagnitude < 0.0001f)
            return -previewPullBackTarget.forward;

        return direction.normalized;
    }

    private void ReleasePreviewTimeline()
    {
        if (previewTimelineDirector != null)
        {
            previewTimelineDirector.Stop();
            previewTimelineDirector.enabled = false;
        }

        SetPreviewTimelineCamerasInactive();

        if (previewTimelineCameraRoot != null)
            previewTimelineCameraRoot.gameObject.SetActive(false);

        Log("Preview解放完了");
    }

    private void SetPreviewTimelineCamerasInactive()
    {
        if (previewTimelineCameraRoot == null)
            return;

        CinemachineVirtualCamera[] cameras =
            previewTimelineCameraRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
                continue;

            cameras[i].Priority = inactivePriority;
        }
    }

    public void NotifyFadeOutCompleted()
    {
        fadeOutCompleted = true;
    }

    public void NotifyFadeInCompleted()
    {
        fadeInCompleted = true;
    }

    #endregion

    #region InGame

    private void EnterInGameState()
    {
        state = CameraManagerState.InGame;

        Log("InGame開始");

        OnInGameStarted();
        onInGameStarted?.Invoke();
    }

    public bool SwitchInGameCamera(int index, bool forceCut = false)
    {
        if (index < 0 || index >= inGameShots.Count)
            return false;

        return SwitchInGameCamera(inGameShots[index], forceCut);
    }

    public bool SwitchInGameCamera(string id, bool forceCut = false)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!shotMap.TryGetValue(id, out CameraShot shot))
            return false;

        if (shot.kind != CameraShotKind.InGame)
            return false;

        return SwitchInGameCamera(shot, forceCut);
    }

    private bool SwitchInGameCamera(CameraShot shot, bool forceCut)
    {
        if (shot == null || shot.VirtualCamera == null)
            return false;

        if (forceCut)
            ApplyCutTransition();
        else
            ApplyTransitionForShot(shot);

        ActivateShotCamera(shot);

        currentInGameShot = shot;

        if (cameraController != null)
            cameraController.SetActiveInGameCamera(shot.VirtualCamera);

        OnInGameCameraChanged(shot);
        onInGameCameraChanged?.Invoke(shot);

        Log($"InGameカメラ切り替え / {shot.name}");

        return true;
    }

    #endregion

    #region Cut

    public void PlayCutCamera(int cutIndex, float duration = -1f, bool returnToInGame = true)
    {
        if (cutIndex < 0 || cutIndex >= cutShots.Count)
            return;

        PlayCutCamera(cutShots[cutIndex], duration, returnToInGame);
    }

    public void PlayCutCamera(string id, float duration = -1f, bool returnToInGame = true)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (!shotMap.TryGetValue(id, out CameraShot shot))
            return;

        if (shot.kind != CameraShotKind.Cut)
            return;

        PlayCutCamera(shot, duration, returnToInGame);
    }

    private void PlayCutCamera(CameraShot shot, float duration, bool returnToInGame)
    {
        if (state != CameraManagerState.InGame && state != CameraManagerState.Cut)
            return;

        if (manualCutActive)
            EndManualCut(false);

        if (cutRoutine != null)
        {
            StopCoroutine(cutRoutine);
            cutRoutine = null;
            FinishInterruptedCutShot();
        }

        cutRoutine = StartCoroutine(CutRoutine(shot, duration, returnToInGame));
    }

    private IEnumerator CutRoutine(CameraShot shot, float duration, bool returnToInGame)
    {
        if (shot == null || shot.VirtualCamera == null)
            yield break;

        CameraShot returnShot = currentInGameShot;

        activeCutShot = shot;
        state = CameraManagerState.Cut;

        ApplyTransitionForShot(shot);
        ActivateShotCamera(shot);

        if (cameraController != null)
            cameraController.SetActiveInGameCamera(shot.VirtualCamera);

        shot.PlayShot();

        OnCutCameraStarted(shot);
        onCutCameraStarted?.Invoke(shot);

        Log($"Cut開始 / {shot.name}");

        float playTime = duration >= 0f ? duration : shot.defaultCutDuration;

        if (playTime > 0f)
            yield return WaitSeconds(playTime);

        OnCutCameraReturnStarted(shot);
        onCutCameraReturnStarted?.Invoke(shot);

        shot.FinishShot();

        if (returnToInGame && returnShot != null)
        {
            ApplyTransitionForShot(returnShot);
            ActivateShotCamera(returnShot);

            currentInGameShot = returnShot;

            if (cameraController != null)
                cameraController.SetActiveInGameCamera(returnShot.VirtualCamera);

            onInGameCameraChanged?.Invoke(returnShot);

            Log($"Cut復帰 / {returnShot.name}");
        }

        OnCutCameraFinished(shot);
        onCutCameraFinished?.Invoke(shot);

        state = CameraManagerState.InGame;
        activeCutShot = null;
        cutRoutine = null;
    }

    private void FinishInterruptedCutShot()
    {
        if (activeCutShot == null)
            return;

        activeCutShot.FinishShot();

        OnCutCameraFinished(activeCutShot);
        onCutCameraFinished?.Invoke(activeCutShot);

        Log($"Cut割り込み終了扱い / {activeCutShot.name}");

        activeCutShot = null;
    }

    public bool StopCurrentCutCamera(bool returnToInGame = true)
    {
        if (manualCutActive)
            return EndManualCut(returnToInGame);

        if (cutRoutine == null || activeCutShot == null)
            return false;

        CameraShot shot = activeCutShot;
        CameraShot returnShot = currentInGameShot;

        StopCoroutine(cutRoutine);
        cutRoutine = null;

        OnCutCameraReturnStarted(shot);
        onCutCameraReturnStarted?.Invoke(shot);

        shot.FinishShot();

        if (returnToInGame && returnShot != null)
        {
            ApplyTransitionForShot(returnShot);
            ActivateShotCamera(returnShot);

            currentInGameShot = returnShot;

            if (cameraController != null)
                cameraController.SetActiveInGameCamera(returnShot.VirtualCamera);

            onInGameCameraChanged?.Invoke(returnShot);

            Log($"Cut手動停止復帰 / {returnShot.name}");
        }

        OnCutCameraFinished(shot);
        onCutCameraFinished?.Invoke(shot);

        state = CameraManagerState.InGame;
        activeCutShot = null;

        Log($"Cut手動停止 / {shot.name}");

        return true;
    }

    public bool BeginManualCut(int cutIndex)
    {
        if (cutIndex < 0 || cutIndex >= cutShots.Count)
            return false;

        return BeginManualCut(cutShots[cutIndex]);
    }

    public bool BeginManualCut(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!shotMap.TryGetValue(id, out CameraShot shot))
            return false;

        if (shot.kind != CameraShotKind.Cut)
            return false;

        return BeginManualCut(shot);
    }

    private bool BeginManualCut(CameraShot shot)
    {
        if (shot == null || shot.VirtualCamera == null)
            return false;

        if (state != CameraManagerState.InGame && state != CameraManagerState.Cut)
            return false;

        if (cutRoutine != null)
        {
            StopCoroutine(cutRoutine);
            cutRoutine = null;
            FinishInterruptedCutShot();
        }


        if (manualCutActive)
        {
            EndManualCut(false);
        }
        else
        {
            manualReturnShot = currentInGameShot;
        }


        manualReturnShot = currentInGameShot;
        manualCutShot = shot;
        manualCutActive = true;

        state = CameraManagerState.Cut;

        ApplyTransitionForShot(shot);
        ActivateShotCamera(shot);

        if (cameraController != null)
            cameraController.SetActiveInGameCamera(shot.VirtualCamera);

        shot.PlayShot();

        OnCutCameraStarted(shot);
        onCutCameraStarted?.Invoke(shot);

        Log($"ManualCut開始 / {shot.name}");

        return true;
    }

    public bool EndManualCut(bool returnToInGame = true)
    {
        if (!manualCutActive)
            return false;

        CameraShot shot = manualCutShot;

        if (shot != null)
        {
            OnCutCameraReturnStarted(shot);
            onCutCameraReturnStarted?.Invoke(shot);

            shot.FinishShot();
        }

        if (returnToInGame && manualReturnShot != null)
        {
            ApplyTransitionForShot(manualReturnShot);
            ActivateShotCamera(manualReturnShot);

            currentInGameShot = manualReturnShot;

            if (cameraController != null)
                cameraController.SetActiveInGameCamera(manualReturnShot.VirtualCamera);

            onInGameCameraChanged?.Invoke(manualReturnShot);

            Log($"ManualCut復帰 / {manualReturnShot.name}");
        }

        if (shot != null)
        {
            OnCutCameraFinished(shot);
            onCutCameraFinished?.Invoke(shot);
        }

        manualCutActive = false;
        manualCutShot = null;
        manualReturnShot = null;

        state = CameraManagerState.InGame;

        Log("ManualCut終了");

        return true;
    }

    #endregion

    #region Skip

    private void UpdatePreviewSkip()
    {
        if (state != CameraManagerState.PreviewTimeline)
            return;

        // Previewポーズ中はスキップ長押し時間も進めない。
        // 途中でボタンを離した場合は、ポーズ解除後に下の判定で0に戻る。
        if (IsPreviewPausedByGamePause())
            return;

        bool pressed = false;

        if (useInputSystemSkip)
        {
            if (skipAction == null)
                CacheSkipInputAction();

            if (skipAction != null)
                pressed = skipAction.IsPressed();
        }
        else
        {
            pressed = IsPreviewSkipPressedByTemporaryKey();
        }

        if (pressed)
        {
            skipTimer += GetDeltaTime();

            if (skipTimer >= skipHoldTime)
            {
                RequestSkipPreview();
            }
        }
        else
        {
            // 途中で離したら最初から押し直し
            skipTimer = 0f;
        }
    }

    protected virtual bool IsPreviewSkipPressedByTemporaryKey()
    {
        return Input.GetKey(temporarySkipKey);
    }

    public void RequestSkipPreview()
    {
        if (state != CameraManagerState.PreviewTimeline)
            return;

        if (skipRequested)
            return;

        skipRequested = true;
        skipTimer = 0f;
    }

    private void CacheSkipInputAction()
    {
        skipActionMap = null;
        skipAction = null;

        if (!useInputSystemSkip)
            return;

        var actions = InputSystem.actions;

        if (actions == null)
        {
            Debug.LogWarning("[CameraManager] InputSystem.actions が null です。Scenes/Skip を取得できません。");
            return;
        }

        skipActionMap = actions.FindActionMap(skipActionMapName, throwIfNotFound: false);

        if (skipActionMap == null)
        {
            Debug.LogWarning($"[CameraManager] ActionMap '{skipActionMapName}' が見つかりません。");
            return;
        }

        skipAction = skipActionMap.FindAction(skipActionName, throwIfNotFound: false);

        if (skipAction == null)
        {
            Debug.LogWarning($"[CameraManager] Action '{skipActionName}' が ActionMap '{skipActionMapName}' 内に見つかりません。");
            return;
        }

        Log($"Skip Action取得成功 / map={skipActionMapName}, action={skipActionName}, interactions={skipAction.interactions}");
    }

    private void EnableSkipInputAction()
    {
        if (!useInputSystemSkip)
            return;

        if (skipAction == null)
            CacheSkipInputAction();

        if (skipAction == null)
            return;

        // ActionMap自体が無効なら有効化する。
        if (skipActionMap != null && !skipActionMap.enabled)
        {
            skipActionMap.Enable();
            skipActionMapEnabledByCameraManager = true;
        }

        if (!skipAction.enabled)
        {
            skipAction.Enable();
            skipActionEnabledByCameraManager = true;
        }

        // performedコールバックではスキップしない。
        // 長押し時間はUpdatePreviewSkip()で自前カウントする。
        skipTimer = 0f;

        Log($"Skip Action 有効化 / map={skipActionMapName}, action={skipActionName}, actionEnabled={skipAction.enabled}");
    }


    private void DisableSkipInputAction()
    {
        if (skipAction != null && skipActionCallbackRegistered)
        {
            skipAction.performed -= OnSkipActionPerformed;
            skipActionCallbackRegistered = false;
        }

        if (skipAction != null && skipActionEnabledByCameraManager)
        {
            skipAction.Disable();
        }

        if (skipActionMap != null && skipActionMapEnabledByCameraManager)
        {
            skipActionMap.Disable();
        }

        skipActionEnabledByCameraManager = false;
        skipActionMapEnabledByCameraManager = false;

        skipTimer = 0f;
    }

    private void OnSkipActionPerformed(InputAction.CallbackContext context)
    {
       
    }

    #endregion

    #region Player Input Lock

    private void CachePlayerInputActionMap()
    {
        playerActionMap = null;

        var actions = InputSystem.actions;

        if (actions == null)
        {
            Debug.LogWarning("[CameraManager] InputSystem.actions が null です。Player ActionMapを取得できません。");
            return;
        }

        playerActionMap = actions.FindActionMap(playerActionMapName, throwIfNotFound: false);

        if (playerActionMap == null)
        {
            Debug.LogWarning($"[CameraManager] ActionMap '{playerActionMapName}' が見つかりません。");
            return;
        }

        Log($"Player ActionMap取得成功 / map={playerActionMapName}");
    }

    private void LockPlayerInput()
    {
        if (!forceDisablePlayerInputUntilInGame)
            return;

        playerInputLockedByCameraManager = true;

        DisablePlayerInputNow();
    }

    private void UnlockPlayerInput()
    {
        playerInputLockedByCameraManager = false;

        EnablePlayerInputNow();
    }

    private void ForceDisablePlayerInputIfLocked()
    {
        if (!forceDisablePlayerInputUntilInGame)
            return;

        if (!playerInputLockedByCameraManager)
            return;

        DisablePlayerInputNow();
    }

    private void DisablePlayerInputNow()
    {
        if (playerActionMap == null)
            CachePlayerInputActionMap();

        if (playerActionMap == null)
            return;

        if (playerActionMap.enabled)
        {
            playerActionMap.Disable();
            Log($"Player ActionMap 強制Disable / map={playerActionMapName}");
        }
    }

    private void EnablePlayerInputNow()
    {
        if (playerActionMap == null)
            CachePlayerInputActionMap();

        if (playerActionMap == null)
            return;

        if (!playerActionMap.enabled)
        {
            playerActionMap.Enable();
            Log($"Player ActionMap Enable / map={playerActionMapName}");
        }
    }

    #endregion

    #region Cinemachine

    private void SetAllShotCamerasInactive()
    {
        for (int i = 0; i < allShots.Count; i++)
        {
            if (allShots[i] == null || allShots[i].VirtualCamera == null)
                continue;

            allShots[i].VirtualCamera.Priority = inactivePriority;
        }
    }

    private void ActivateShotCamera(CameraShot shot)
    {
        if (shot == null || shot.VirtualCamera == null)
            return;

        SetAllShotCamerasInactive();

        shot.VirtualCamera.Priority = activePriority;
        currentShot = shot;
    }

    private void ApplyTransitionForShot(CameraShot shot)
    {
        if (brain == null || shot == null)
            return;

        if (shot.transitionType == CameraTransitionType.Cut)
        {
            ApplyCutTransition();
            return;
        }

        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut,
            Mathf.Max(shot.enterBlendTime, 0f)
        );
    }

    private void ApplyCutTransition()
    {
        if (brain == null)
            return;

        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.Cut,
            0f
        );
    }

    private void ForceBrainUpdate()
    {
        if (brain == null)
            return;

        brain.ManualUpdate();
    }

    public void RestoreDefaultBrainBlend()
    {
        if (brain != null)
            brain.m_DefaultBlend = defaultBrainBlend;
    }

    #endregion

    #region Time

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float timer = 0f;

        while (timer < seconds)
        {
            timer += GetDeltaTime();
            yield return null;
        }
    }

    private void Log(string message)
    {
        if (!debugLog)
            return;

        Debug.Log($"[CameraManager] {message}");
    }

    private bool IsPreviewPausedByGamePause()
    {
        return pausePreviewWhenGamePaused && Time.timeScale <= 0f;
    }

    #endregion

    #region 差し込み用

    protected virtual void OnPreviewStarted()
    {
        EnableSkipInputAction();

        // Preview中はPlayer入力をCameraManager側でロックする。
        // 外部からPlayer ActionMapがEnableされてもUpdateで毎フレームDisableする。
        LockPlayerInput();

        if (TimeUIManager.Instance != null) TimeUIManager.Instance.SetTimerRootFlg(false);
        else Debug.LogWarning("[CameraManager] TimeUIManager.Instance が null です。");

        if (PlayerHPGaugeUI.Instance != null) PlayerHPGaugeUI.Instance.SetVisible(false);
        else Debug.LogWarning("[CameraManager] PlayerHPGaugeUI.Instance が null です。");

        if (TackleGaugeUI.Instance != null) TackleGaugeUI.Instance.SetVisible(false);
        else Debug.LogWarning("[CameraManager] TackleGaugeUI.Instance が null です。");

        if (MissionUIManager.Instance != null) MissionUIManager.Instance.SetUIRootFlg(false);
        else Debug.LogWarning("[CameraManager] MissionUIManager.Instance が null です。");
    }

    protected virtual void OnPreviewTimelineFinished()
    {
        // Timelineが最後まで再生された時。
        // この後FadeOutへ進む。
    }

    protected virtual void OnPreviewSkipped()
    {
        // Previewスキップ成立時。
        // この後FadeOutへ進む。
    }

    protected virtual void OnRequestFadeOut()
    {
        // FadeOut開始要求。
        // FadeManager以外の演出を差し込みたい場合に使う。
    }

    protected virtual void OnFadeOutCompleted()
    {
        // FadeOut完了。
        // この直後にInGameカメラへ切り替わる。
    }

    protected virtual void OnPreviewCameraSwitchedToInGame()
    {
        if (TimeUIManager.Instance != null) TimeUIManager.Instance.SetTimerRootFlg(true);
        else Debug.LogWarning("[CameraManager] TimeUIManager.Instance が null です。");

        if (PlayerHPGaugeUI.Instance != null) PlayerHPGaugeUI.Instance.SetVisible(true);
        else Debug.LogWarning("[CameraManager] PlayerHPGaugeUI.Instance が null です。");

        if (TackleGaugeUI.Instance != null) TackleGaugeUI.Instance.SetVisible(true);
        else Debug.LogWarning("[CameraManager] TackleGaugeUI.Instance が null です。");

        if (MissionUIManager.Instance != null) MissionUIManager.Instance.SetUIRootFlg(true);
        else Debug.LogWarning("[CameraManager] MissionUIManager.Instance が null です。");
    }

    protected virtual void OnRequestFadeIn()
    {
        // FadeIn開始要求。
        // FadeManager以外の演出を差し込みたい場合に使う。
    }

    protected virtual void OnFadeInCompleted()
    {
        // FadeIn完了。
    }

    protected virtual void OnInGameStarted()
    {
        DisableSkipInputAction();

        // FadeIn完了後、通常プレイ開始時だけPlayer入力を解放する
        UnlockPlayerInput();

        if (TimeUIManager.Instance != null) TimeUIManager.Instance.SetCountDownStart(true);
        else Debug.LogWarning("[CameraManager] TimeUIManager.Instance が null です。");
    }

    protected virtual void OnInGameCameraChanged(CameraShot shot)
    {
    }

    protected virtual void OnCutCameraStarted(CameraShot shot)
    {
    }

    protected virtual void OnCutCameraReturnStarted(CameraShot shot)
    {
    }

    protected virtual void OnCutCameraFinished(CameraShot shot)
    {
    }

    #endregion
}