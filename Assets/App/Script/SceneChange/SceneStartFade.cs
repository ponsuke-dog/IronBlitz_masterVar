using UnityEngine;

// 各シーン開始時の状態を決める
public class SceneStartFade : MonoBehaviour
{
    [Header("開始時に適用するか")]
    [SerializeField] private bool applyOnStart = true;

    [Header("開始時に黒画面として扱うか")]
    [SerializeField] private bool startFromBlack = false;

    [Header("開始時に明転するか")]
    [SerializeField] private bool playFadeFromBlack = false;

    [Header("開始時に使う明転フェード")]
    [SerializeField] private FadePreset startFadePreset;

    private void Start()
    {
        if (!applyOnStart) return;
        if (FadeManager.Instance == null) return;

        // ここでは明転だけを扱う
        if (startFromBlack && playFadeFromBlack && startFadePreset != null)
        {
            if (!FadeManager.Instance.IsFading)
            {
                FadeManager.Instance.FadeFromBlack(startFadePreset);
            }
        }
    }
}
