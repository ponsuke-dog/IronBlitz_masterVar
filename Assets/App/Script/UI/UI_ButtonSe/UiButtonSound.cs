using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UIボタンの移動音と決定音を再生する。
/// </summary>
public class UiButtonSound : MonoBehaviour,
    ISelectHandler,
    IPointerClickHandler,
    ISubmitHandler
{
    [Header("Audio ID")]
    [SerializeField] private string moveAudioId = "UI_Select";
    [SerializeField] private string clickAudioId = "UI_Enter";

    /// <summary>
    /// キーボードやゲームパッドで選択されたとき。
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        PlayUi(moveAudioId);
    }

    /// <summary>
    /// マウスでクリックされたとき。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        PlayUi(clickAudioId);
    }

    /// <summary>
    /// Enterキーやゲームパッドの決定ボタンで押されたとき。
    /// </summary>
    public void OnSubmit(BaseEventData eventData)
    {
        PlayUi(clickAudioId);
    }

    private void PlayUi(string audioId)
    {
        if (string.IsNullOrEmpty(audioId))
            return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("AudioManagerが存在しません。");
            return;
        }

        AudioManager.Instance.PlayUi(audioId);
    }
}