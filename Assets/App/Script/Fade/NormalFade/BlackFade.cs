using UnityEngine;
using UnityEngine.UI;

public class BlackFade : MonoBehaviour , FadeBase
{
    [SerializeField] private Image fadeimage;

    public void Initialize()
    {
        if (fadeimage == null)
        {
            fadeimage = GetComponent<Image>();
        }

        if(fadeimage == null)
        {
            Debug.LogError("BlackFade : fadeimage が設定されていません。");
        }
    }

    public void Begin(FadePreset preset, bool fadetoblack)
    {
        if (fadeimage == null) return;

        Color c = preset.fadecolor;
        c.a = fadetoblack ? 0.0f : 1.0f;//true かfalseで値を1.0fか0.0f
        fadeimage.color = c;
        fadeimage.enabled = true;
    }

    public void Apply(float progress , FadePreset preset , bool fadetoblack)
    {
        if (fadeimage == null) return;

        Color c = preset .fadecolor;
        c.a = fadetoblack ? progress : 1.0f - progress;
        fadeimage.color = c;
    }

    public void End(FadePreset preset, bool fadetoblack)
    {
        if (fadeimage == null) return;

        Color c = preset.fadecolor;
        c.a = fadetoblack ? 1.0f : 0.0f;
        fadeimage.color = c;
    }

    // 初期状態を強制的に透明にしたい時用
    public void ForceClear(FadePreset preset)
    {
        if (fadeimage == null) return;

        Color c = preset.fadecolor;
        c.a = 0.0f;
        fadeimage.color = c;
    }

    // 黒に固定したい時用
    public void ForceBlack(FadePreset preset)
    {
        if (fadeimage == null) return;

        Color c = preset.fadecolor;
        c.a = 1.0f;
        fadeimage.color = c;
    }

}
