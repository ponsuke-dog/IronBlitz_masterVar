using UnityEngine;
using UnityEngine.Events;
using Cinemachine;

/// <summary>
/// CameraShotの用途。
/// PreviewはTimelineで扱うため、基本的にCameraShotではInGame/Cutを使う。
/// </summary>
public enum CameraShotKind
{
    Preview, // 基本未使用。簡易Previewを残したい場合用。
    InGame,  // 通常プレイ中のカメラ。
    Cut      // 一時的に割り込む演出カメラ。
}

/// <summary>
/// CameraShotへ切り替える時の方式。
/// </summary>
public enum CameraTransitionType
{
    Blend,
    Cut
}

/// <summary>
/// InGame / Cut用のCinemachineVirtualCamera設定。
/// Preview演出はTimeline側で管理する。
/// </summary>
[DisallowMultipleComponent]
public class CameraShot : MonoBehaviour
{
    #region 基本設定

    [Header("基本設定")]
    [Tooltip("このカメラの用途。InGameCameras配下ならInGame、CutCameras配下ならCutにCameraManagerが自動設定します。")]
    public CameraShotKind kind = CameraShotKind.InGame;

    [Tooltip("手動IDを使いたい場合だけ入力します。空ならInGame_00 / Cut_00 のように自動IDが付きます。")]
    public string idOverride;

    #endregion

    #region 切り替え設定

    [Header("切り替え設定")]
    [Tooltip("このカメラへ切り替える時の方式です。BlendならCinemachine補間、Cutなら瞬間切り替えです。")]
    public CameraTransitionType transitionType = CameraTransitionType.Blend;

    [Tooltip("Blend時に、このカメラへ移るまでの時間です。Cutの場合は無視されます。")]
    public float enterBlendTime = 0.5f;

    #endregion

    #region Cut設定

    [Header("Cut Camera用")]
    [Tooltip("CutCameraとして使う場合のデフォルト表示時間です。外部からdurationを指定した場合はそちらが優先されます。")]
    public float defaultCutDuration = 0.5f;

    #endregion

    #region Virtual Camera

    [Header("Virtual Camera")]
    [Tooltip("このShotで有効化するCinemachineVirtualCameraです。未設定なら同じGameObjectから自動取得します。")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    #endregion

    #region 差し込みイベント

    [Header("差し込みイベント")]
    [Tooltip("このShotが有効になった時に呼ばれます。個別カメラ用SEなどに使えます。")]
    public UnityEvent onShotStarted;

    [Tooltip("このShotが終了した時に呼ばれます。Cut終了時などに使えます。")]
    public UnityEvent onShotFinished;

    #endregion

    #region Runtime

    private string runtimeId;
    private int runtimeIndex = -1;

    public CinemachineVirtualCamera VirtualCamera
    {
        get
        {
            if (virtualCamera == null)
                virtualCamera = GetComponent<CinemachineVirtualCamera>();

            return virtualCamera;
        }
    }

    public string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(idOverride))
                return idOverride;

            if (!string.IsNullOrEmpty(runtimeId))
                return runtimeId;

            return gameObject.name;
        }
    }

    public int RuntimeIndex => runtimeIndex;

    public void SetRuntimeId(string id, int index)
    {
        runtimeId = id;
        runtimeIndex = index;
    }

    public void PlayShot()
    {
        onShotStarted?.Invoke();
    }

    public void FinishShot()
    {
        onShotFinished?.Invoke();
    }

    #endregion
}