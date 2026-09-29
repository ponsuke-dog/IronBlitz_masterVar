using System;

/// <summary>
/// ‰¹—Êİ’è UI ‚Ì‹¤’Ê‘‹Œû
/// ‰¼UI‚Å‚à³‹KUI‚Å‚à‚±‚ÌŒ`®‚É‡‚í‚¹‚ê‚Î·‚µ‘Ö‚¦‰Â”\
/// </summary>
public interface IAudioSettingsView
{
    event Action<float> OnMasterChanged;
    event Action<float> OnBgmChanged;
    event Action<float> OnSeChanged;
    event Action<float> OnUiChanged;
    event Action OnResetClicked;
    event Action OnCloseClicked;

    void SetValues(AudioVolumeData data);
    void Show();
    void Hide();
}