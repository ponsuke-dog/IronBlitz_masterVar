using UnityEngine;

/// <summary>
/// ゲーム全体からAudio再生を呼び出すための統一窓口。
/// 各シーンにAudioSystemを1つ配置して使用する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Players")]
    [SerializeField] private BgmPlayer bgmPlayer;
    [SerializeField] private UiPlayer uiPlayer;
    [SerializeField] private SePoolPlayer sePoolPlayer;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// BGMを再生する。
    /// </summary>
    public void PlayBgm(string audioId)
    {
        if (bgmPlayer == null)
        {
            Debug.LogWarning("BgmPlayer is not assigned.");
            return;
        }

        bgmPlayer.PlayBgm(audioId);
    }

    /// <summary>
    /// BGMを停止する。
    /// </summary>
    public void StopBgm()
    {
        if (bgmPlayer == null)
        {
            return;
        }

        bgmPlayer.StopAudio();
    }

    /// <summary>
    /// UI音を再生する。
    /// 従来互換用。個別停止ハンドルは返さない。
    /// </summary>
    public void PlayUi(string audioId)
    {
        if (uiPlayer == null)
        {
            Debug.LogWarning("UiPlayer is not assigned.");
            return;
        }

        uiPlayer.PlayUi(audioId);
    }

    /// <summary>
    /// UI音を再生し、個別停止用ハンドルを返す。
    /// </summary>
    public int PlayUiWithHandle(string audioId)
    {
        if (uiPlayer == null)
        {
            Debug.LogWarning("UiPlayer is not assigned.");
            return -1;
        }

        return uiPlayer.PlayUiWithHandle(audioId);
    }

    /// <summary>
    /// 現在のUI音を停止する。
    /// </summary>
    public void StopUi()
    {
        if (uiPlayer == null)
        {
            return;
        }

        uiPlayer.StopAudio();
    }

    /// <summary>
    /// 指定IDのUI音が再生中なら停止する。
    /// </summary>
    public void StopUi(string audioId)
    {
        if (uiPlayer == null)
        {
            return;
        }

        uiPlayer.StopUi(audioId);
    }

    /// <summary>
    /// 指定ハンドルのUI音を停止する。
    /// </summary>
    public void StopUi(int handle)
    {
        if (uiPlayer == null)
        {
            return;
        }

        uiPlayer.StopUi(handle);
    }

    /// <summary>
    /// SEを指定位置で再生する。
    /// 従来互換用。個別停止ハンドルは返さない。
    /// </summary>
    public void PlaySe(string audioId, Vector3 position)
    {
        if (sePoolPlayer == null)
        {
            Debug.LogWarning("SePoolPlayer is not assigned.");
            return;
        }

        sePoolPlayer.PlaySe(audioId, position);
    }

    /// <summary>
    /// SEを2D扱いで再生する。
    /// 従来互換用。個別停止ハンドルは返さない。
    /// </summary>
    public void PlaySe(string audioId)
    {
        PlaySe(audioId, Vector3.zero);
    }

    /// <summary>
    /// SEを指定位置で再生し、個別停止用ハンドルを返す。
    /// </summary>
    public int PlaySeWithHandle(string audioId, Vector3 position)
    {
        if (sePoolPlayer == null)
        {
            Debug.LogWarning("SePoolPlayer is not assigned.");
            return -1;
        }

        return sePoolPlayer.PlaySeWithHandle(audioId, position);
    }

    /// <summary>
    /// SEを2D扱いで再生し、個別停止用ハンドルを返す。
    /// </summary>
    public int PlaySeWithHandle(string audioId)
    {
        return PlaySeWithHandle(audioId, Vector3.zero);
    }

    /// <summary>
    /// 指定IDのSEをすべて停止する。
    /// </summary>
    public void StopSe(string audioId)
    {
        if (sePoolPlayer == null)
        {
            return;
        }

        sePoolPlayer.StopSe(audioId);
    }

    /// <summary>
    /// 指定ハンドルのSEを1つだけ停止する。
    /// </summary>
    public void StopSe(int handle)
    {
        if (sePoolPlayer == null)
        {
            return;
        }

        sePoolPlayer.StopSe(handle);
    }

    /// <summary>
    /// 再生中のSEをすべて停止する。
    /// </summary>
    public void StopAllSe()
    {
        if (sePoolPlayer == null)
        {
            return;
        }

        sePoolPlayer.StopAllSe();
    }
}