using UnityEngine;

/// <summary>
/// ButtonのOnClickなどから任意のUI SEを再生するための汎用コンポーネント。
/// </summary>
public class UiSeOnClickPlayer : MonoBehaviour
{
    [Header("Audio ID")]
    [Tooltip("AudioDatabaseに登録しているUI SEのAudio IDを指定します。例：ui_enter")]
    [SerializeField] private string audioId = "ui_enter";

    [Header("Cooldown")]
    [Tooltip("連続再生を防ぐための待ち時間です。0なら制限なしです。")]
    [Min(0.0f)]
    [SerializeField] private float cooldown = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private float lastPlayTime = -999.0f;

    public void Play()
    {
        if (string.IsNullOrEmpty(audioId))
        {
            Debug.LogWarning("[UiSeOnClickPlayer] Audio IDが未設定です。");
            return;
        }

        if (CanPlay() == false)
        {
            return;
        }

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[UiSeOnClickPlayer] AudioManager.Instanceが見つかりません。");
            return;
        }

        lastPlayTime = Time.unscaledTime;

        AudioManager.Instance.PlayUi(audioId);

        if (enableDebugLog)
        {
            Debug.Log($"[UiSeOnClickPlayer] Play UI SE. AudioId = {audioId}");
        }
    }

    public void PlayByAudioId(string targetAudioId)
    {
        audioId = targetAudioId;
        Play();
    }

    private bool CanPlay()
    {
        if (cooldown <= 0.0f)
        {
            return true;
        }

        return Time.unscaledTime - lastPlayTime >= cooldown;
    }
}