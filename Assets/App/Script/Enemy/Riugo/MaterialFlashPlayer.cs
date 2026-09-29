using System;
using System.Collections.Generic;
using UnityEngine;

public class MaterialFlashPlayer : MonoBehaviour
{
    #region Serializable Params

    [Serializable]
    private class FlashParam
    {
        [Header("Flash")]
        [Tooltip("Material差し替えFlashを使う")]
        public bool enable = true;

        [Tooltip("Awake時に対象Rendererを自動で初期化する")]
        public bool initializeOnAwake = true;

        [Tooltip("未設定なら子Rendererを自動取得する")]
        public bool autoCollectRenderers = true;

        [Tooltip("手動で対象Rendererを指定したい場合に使用")]
        public Renderer[] renderers;

        [Header("Material")]
        [Tooltip("Flash中に一瞬だけ差し替えるMaterial")]
        public Material flashMaterial;

        [Tooltip("ONならRenderer内の全Material SlotをFlash Materialに差し替える")]
        public bool replaceAllMaterialSlots = true;

        [Tooltip("replaceAllMaterialSlotsがOFFの時、差し替えるMaterial Index")]
        public int targetMaterialIndex = 0;

        [Header("Timing")]
        [Tooltip("Flash Materialに差し替えている時間")]
        public float duration = 0.12f;

        [Tooltip("連続再生時に最初から再生し直す")]
        public bool restartOnPlay = true;

        [Tooltip("Time.timeScaleの影響を受けない時間で戻す")]
        public bool useUnscaledTime = false;

        [Header("Original Material")]
        [Tooltip("PlayFlash時に現在のMaterialを元Materialとして取り直す")]
        public bool recaptureOriginalOnPlay = false;

        [Tooltip("Disable時に元Materialへ戻す")]
        public bool restoreOnDisable = true;

        [Tooltip("Destroy時に元Materialへ戻す")]
        public bool restoreOnDestroy = true;

        [Header("Target Filter")]
        [Tooltip("名前にこの文字列を含むRendererは除外する。空なら使わない")]
        public string[] excludeNameContains =
        {
            "Arrow",
            "Hitbox",
            "Collision",
            "Muzzle"
        };

        [Tooltip("除外判定で大文字小文字を無視する")]
        public bool ignoreCase = true;

        [Header("Debug")]
        public bool logFlash = false;
    }

    private class FlashCache
    {
        public Renderer renderer;
        public Material[] originalSharedMaterials;
    }

    #endregion

    #region Inspector Fields

    [Header("Material Flash")]
    [SerializeField] private FlashParam flashParam = new FlashParam();

    #endregion

    #region Runtime Fields

    private readonly List<FlashCache> caches = new List<FlashCache>();

    private bool initialized;
    private bool playing;
    private float timer;

    #endregion

    #region Unity Events

    private void Awake()
    {
        if (flashParam.initializeOnAwake)
            Initialize();
    }

    private void Update()
    {
        UpdateFlash(GetDeltaTime());
    }

    private void OnDisable()
    {
        if (flashParam.restoreOnDisable)
            RestoreOriginalMaterials();

        playing = false;
    }

    private void OnDestroy()
    {
        if (flashParam.restoreOnDestroy)
            RestoreOriginalMaterials();
    }

    #endregion

    #region Public API

    public void PlayFlash()
    {
        if (!flashParam.enable)
            return;

        if (flashParam.flashMaterial == null)
            return;

        if (!initialized)
            Initialize();

        if (flashParam.recaptureOriginalOnPlay)
            CaptureCurrentMaterialsAsOriginal();

        if (caches.Count == 0)
            return;

        if (playing && !flashParam.restartOnPlay)
            return;

        timer = 0f;
        playing = true;

        ApplyFlashMaterial();

        if (flashParam.logFlash)
            Debug.Log($"{name} MaterialFlash Play");
    }

    public void StopFlash(bool restore = true)
    {
        playing = false;
        timer = 0f;

        if (restore)
            RestoreOriginalMaterials();

        if (flashParam.logFlash)
            Debug.Log($"{name} MaterialFlash Stop restore:{restore}");
    }

    public void Initialize()
    {
        caches.Clear();

        if (!flashParam.enable)
            return;

        Renderer[] targetRenderers = flashParam.renderers;

        if ((targetRenderers == null || targetRenderers.Length == 0) &&
            flashParam.autoCollectRenderers)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];

            if (renderer == null)
                continue;

            if (ShouldExcludeRenderer(renderer))
                continue;

            Material[] originals = renderer.sharedMaterials;

            if (originals == null || originals.Length == 0)
                continue;

            caches.Add(
                new FlashCache
                {
                    renderer = renderer,
                    originalSharedMaterials = originals
                }
            );
        }

        initialized = true;

        if (flashParam.logFlash)
        {
            Debug.Log(
                $"{name} MaterialFlash initialized. " +
                $"RendererCount:{caches.Count}"
            );
        }
    }

    public void RebuildCache()
    {
        initialized = false;
        Initialize();
    }

    public void CaptureCurrentMaterialsAsOriginal()
    {
        if (!initialized)
            Initialize();

        for (int i = 0; i < caches.Count; i++)
        {
            FlashCache cache = caches[i];

            if (cache == null || cache.renderer == null)
                continue;

            cache.originalSharedMaterials = cache.renderer.sharedMaterials;
        }
    }

    public void RestoreOriginalMaterials()
    {
        for (int i = 0; i < caches.Count; i++)
        {
            FlashCache cache = caches[i];

            if (cache == null || cache.renderer == null)
                continue;

            if (cache.originalSharedMaterials == null)
                continue;

            cache.renderer.sharedMaterials = cache.originalSharedMaterials;
        }

        if (flashParam.logFlash)
            Debug.Log($"{name} MaterialFlash Restore");
    }

    #endregion

    #region Flash Logic

    private void UpdateFlash(float dt)
    {
        if (!playing)
            return;

        timer += dt;

        if (timer < flashParam.duration)
            return;

        RestoreOriginalMaterials();

        playing = false;
    }

    private void ApplyFlashMaterial()
    {
        for (int i = 0; i < caches.Count; i++)
        {
            FlashCache cache = caches[i];

            if (cache == null || cache.renderer == null)
                continue;

            Material[] originals = cache.originalSharedMaterials;

            if (originals == null || originals.Length == 0)
                continue;

            Material[] flashMaterials = new Material[originals.Length];

            for (int m = 0; m < flashMaterials.Length; m++)
            {
                if (flashParam.replaceAllMaterialSlots)
                {
                    flashMaterials[m] = flashParam.flashMaterial;
                }
                else
                {
                    flashMaterials[m] = originals[m];

                    if (m == flashParam.targetMaterialIndex)
                        flashMaterials[m] = flashParam.flashMaterial;
                }
            }

            cache.renderer.sharedMaterials = flashMaterials;
        }
    }

    private bool ShouldExcludeRenderer(Renderer renderer)
    {
        if (renderer == null)
            return true;

        string[] filters = flashParam.excludeNameContains;

        if (filters == null)
            return false;

        string objectName = renderer.gameObject.name;

        if (flashParam.ignoreCase)
            objectName = objectName.ToLowerInvariant();

        for (int i = 0; i < filters.Length; i++)
        {
            string filter = filters[i];

            if (string.IsNullOrEmpty(filter))
                continue;

            if (flashParam.ignoreCase)
                filter = filter.ToLowerInvariant();

            if (objectName.Contains(filter))
                return true;
        }

        return false;
    }

    private float GetDeltaTime()
    {
        return flashParam.useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    #endregion
}