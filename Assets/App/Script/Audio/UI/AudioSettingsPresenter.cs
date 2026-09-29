using UnityEngine;

/// <summary>
/// 音量設定 UI と AudioVolumeService を接続するプレゼンター
/// UI を交換してもここは基本そのまま使える
/// </summary>
public class AudioSettingsPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioVolumeService audioVolumeService;
    [SerializeField] private MonoBehaviour viewComponent;

    [Header("Open Button")]
    [SerializeField] private GameObject openButtonRoot;

    private IAudioSettingsView view;

    private void Awake()
    {
        view = viewComponent as IAudioSettingsView;

        if (view == null)
        {
            Debug.LogError("viewComponent does not implement IAudioSettingsView.");
            return;
        }

        view.OnMasterChanged += audioVolumeService.SetMasterVolume;
        view.OnBgmChanged += audioVolumeService.SetBgmVolume;
        view.OnSeChanged += audioVolumeService.SetSeVolume;
        view.OnUiChanged += audioVolumeService.SetUiVolume;
        view.OnResetClicked += audioVolumeService.ResetVolumes;
        view.OnCloseClicked += Close;

        audioVolumeService.OnVolumeChanged += view.SetValues;
    }

    private void Start()
    {
        view.SetValues(audioVolumeService.CurrentVolumeData);
        view.Hide();

        if (openButtonRoot != null)
        {
            openButtonRoot.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (view == null || audioVolumeService == null)
        {
            return;
        }

        view.OnMasterChanged -= audioVolumeService.SetMasterVolume;
        view.OnBgmChanged -= audioVolumeService.SetBgmVolume;
        view.OnSeChanged -= audioVolumeService.SetSeVolume;
        view.OnUiChanged -= audioVolumeService.SetUiVolume;
        view.OnResetClicked -= audioVolumeService.ResetVolumes;
        view.OnCloseClicked -= Close;

        audioVolumeService.OnVolumeChanged -= view.SetValues;
    }

    /// <summary>
    /// 音量設定画面を開く
    /// </summary>
    public void Open()
    {
        view.SetValues(audioVolumeService.CurrentVolumeData);
        view.Show();

        if (openButtonRoot != null)
        {
            openButtonRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 音量設定画面を閉じる
    /// </summary>
    public void Close()
    {
        view.Hide();

        if (openButtonRoot != null)
        {
            openButtonRoot.SetActive(true);
        }
    }
}