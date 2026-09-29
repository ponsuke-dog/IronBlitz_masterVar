using System;
using System.Collections;
using UnityEngine;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance {  get; private set; }

    [Header("通常フェード用")]
    [SerializeField] private BlackFade blackFade;

    [Header("タイルフェード用")]
    [SerializeField] private TileFade tilefade;

    private Coroutine currentcoroutine;

    public float CurrentFadeProgress { get; private set; }
    public bool IsFading { get; private set; }
    public bool IsBlackScreen {  get; private set; }

    private void Awake()
    {
        //シングルトン化
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

      

        if(blackFade != null)
        {
            blackFade.Initialize();
        }

        if (tilefade != null)
        {
            tilefade.Initialize();
        }

    }

    private FadeBase GetRenderer(FadePreset preset)
    {
        if (preset == null)
        {
            return blackFade;
        }

        switch (preset.fadetype)
        {
            case FadeType.Tile:
                return tilefade;
            case FadeType.Black:
            default:
                return blackFade;
        }
    }

    private void PrepareFadeTargets(FadePreset activePreset)
    {
        if (activePreset == null) return;

        // 今回使うもの以外は透明状態へ戻す
        if (activePreset.fadetype == FadeType.Black)
        {
            if (tilefade != null)
            {
                tilefade.ForceClear(activePreset);
            }
        }
        else if (activePreset.fadetype == FadeType.Tile)
        {
            if (blackFade != null)
            {
                blackFade.ForceClear(activePreset);
            }
        }
    }
    public void SetClear(FadePreset preset)
    {
        FadeBase renderer = GetRenderer(preset);
        if (renderer == null) return;

        renderer.ForceClear(preset);
        IsBlackScreen = false;
    }

    public void SetBlack(FadePreset preset)
    {
        FadeBase renderer = GetRenderer(preset);
        if (renderer == null) return;

        renderer.ForceBlack(preset);
        IsBlackScreen = true;
    }


    public void FadeToBlack(FadePreset preset, Action oncomplete = null)
    {
        StartFade(preset , true , oncomplete);
    }

    public void FadeFromBlack(FadePreset preset, Action oncomplete = null)
    {
        StartFade( preset , false , oncomplete);
    }

    public IEnumerator FadeToBlackRoutine(FadePreset preset)
    {
        bool finished = false;

        FadeToBlack(preset, () => finished = true );

        while (!finished)
        {
            yield return null;
        }
    }

    public IEnumerator FadeFromBlackRoutine(FadePreset preset)
    {
        bool finished = false;

        FadeFromBlack(preset, () => finished = true);

        while (!finished)
        {
            yield return null;
        }
    }


    private void StartFade(FadePreset preset, bool fadetoblack , Action oncomplete)
    {
        if(currentcoroutine != null)
        {
            StopCoroutine(currentcoroutine);
        }

        CurrentFadeProgress = 0f;

        PrepareFadeTargets(preset);

        currentcoroutine = StartCoroutine(FadeRoutine(preset, fadetoblack, oncomplete));
    }

    private IEnumerator FadeRoutine(FadePreset preset , bool fadetoblack , Action oncomplete)
    {
        IsFading = true;

        FadeBase renderer = GetRenderer(preset);

        if (renderer == null)
        {
            Debug.LogError("FadeManager : 使用可能な FadeRenderer がありません。");
            IsFading = false;
            yield break;
        }

        renderer.Begin(preset, fadetoblack);

        float time = 0f;
        float duration = Mathf.Max(0.0001f, preset != null ? preset.duration : 1.0f);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
           CurrentFadeProgress = Mathf.Clamp01(time / duration);

            renderer.Apply(CurrentFadeProgress, preset, fadetoblack);

            yield return null;
        }

        renderer.End(preset, fadetoblack);

        CurrentFadeProgress = 1f;

        IsFading = false;
        IsBlackScreen = fadetoblack;
        currentcoroutine = null;

        oncomplete?.Invoke();
    }

}
