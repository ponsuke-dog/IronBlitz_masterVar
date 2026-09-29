using UnityEngine;
using UnityEngine.UI;

public class TileFade : MonoBehaviour, FadeBase
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private Material sourceMaterial;

    private Material runtimeMaterial;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FadeColorId = Shader.PropertyToID("_FadeColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int ColumnsId = Shader.PropertyToID("_Columns");
    private static readonly int RowsId = Shader.PropertyToID("_Rows");
    private static readonly int EdgeId = Shader.PropertyToID("_Edge");
    private static readonly int RandomnessId = Shader.PropertyToID("_Randomness");
    private static readonly int FadeToBlackId = Shader.PropertyToID("_FadeToBlack");
    private static readonly int RandomSeedId = Shader.PropertyToID("_RandomSeed");
    private static readonly int OrderModeId = Shader.PropertyToID("_OrderMode");
    private static readonly int StartCornerId = Shader.PropertyToID("_StartCorner");

    public void Initialize()
    {
        if (fadeImage == null)
        {
            fadeImage = GetComponent<Image>();
        }

        if (fadeImage == null)
        {
            Debug.LogError("TileFade : fadeImage が設定されていません。");
            return;
        }

        if (sourceMaterial == null)
        {
            Debug.LogError("TileFade : sourceMaterial が設定されていません。");
            return;
        }

        runtimeMaterial = new Material(sourceMaterial);
        fadeImage.material = runtimeMaterial;

        fadeImage.color = Color.white;

        runtimeMaterial.SetFloat(FadeToBlackId, 1f);
        SetProgress(0f);
        fadeImage.enabled = false;
    }

    public void Begin(FadePreset preset, bool fadeToBlack)
    {
        if (runtimeMaterial == null || fadeImage == null) return;

        ApplyPreset(preset);
        fadeImage.enabled = true;

        runtimeMaterial.SetFloat(FadeToBlackId, fadeToBlack ? 1f : 0f);
        SetProgress(0f);
    }

    public void Apply(float progress, FadePreset preset, bool fadeToBlack)
    {
        if (runtimeMaterial == null) return;
        SetProgress(progress);
    }

    public void End(FadePreset preset, bool fadeToBlack)
    {
        if (runtimeMaterial == null || fadeImage == null) return;

        if (fadeToBlack)
        {
            runtimeMaterial.SetFloat(FadeToBlackId, 1f);
            SetProgress(1f);
            fadeImage.enabled = true;
        }
        else
        {
            runtimeMaterial.SetFloat(FadeToBlackId, 0f);
            SetProgress(1f);

            // 次回用に完全透明へ戻す
            runtimeMaterial.SetFloat(FadeToBlackId, 1f);
            SetProgress(0f);
            fadeImage.enabled = false;
        }
    }

    public void ForceClear(FadePreset preset)
    {
        if (runtimeMaterial == null || fadeImage == null) return;

        ApplyPreset(preset);
        runtimeMaterial.SetFloat(FadeToBlackId, 1f);
        SetProgress(0f);
        fadeImage.enabled = false;
    }

    public void ForceBlack(FadePreset preset)
    {
        if (runtimeMaterial == null || fadeImage == null) return;

        ApplyPreset(preset);
        runtimeMaterial.SetFloat(FadeToBlackId, 1f);
        SetProgress(1f);
        fadeImage.enabled = true;
    }

    private void ApplyPreset(FadePreset preset)
    {
        if (runtimeMaterial == null || preset == null) return;

        runtimeMaterial.SetColor(FadeColorId, preset.fadecolor);
        runtimeMaterial.SetColor(EdgeColorId, preset.edgecolor);
        runtimeMaterial.SetFloat(ColumnsId, Mathf.Max(1, preset.columns));
        runtimeMaterial.SetFloat(RowsId, Mathf.Max(1, preset.rows));
        runtimeMaterial.SetFloat(EdgeId, preset.edgewidth);
        runtimeMaterial.SetFloat(RandomnessId, preset.randomness);
        runtimeMaterial.SetFloat(RandomSeedId, preset.randomSeed);
        runtimeMaterial.SetFloat(OrderModeId, (float)preset.tileOrderMode);
        runtimeMaterial.SetFloat(StartCornerId, (float)preset.tileStartCorner);
    }

    private void SetProgress(float value)
    {
        if (runtimeMaterial == null) return;
        runtimeMaterial.SetFloat(ProgressId, Mathf.Clamp01(value));
    }
}