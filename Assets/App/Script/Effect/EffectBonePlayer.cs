using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボーン配下などにあらかじめ配置してあるEffectを、
/// Element番号をIDとして再生・停止する管理クラス。
/// </summary>
public class EffectBonePlayer : MonoBehaviour
{
    [Header("登録済みEffect")]
    [Tooltip("Element番号がそのままIDになります。Element 0なら PlayEffect(0) で再生。")]
    [SerializeField]
    private List<GameObject> effectPrefabs = new();

    [Header("再生設定")]
    [SerializeField]
    private bool restartOnPlay = true;

    [SerializeField]
    private bool activateOnPlay = true;

    [Header("停止設定")]
    [SerializeField]
    private bool clearOnStop = true;

    [SerializeField]
    private bool deactivateOnStop = true;

    /// <summary>
    /// 各Effect内のParticleSystemキャッシュ。
    /// effectPrefabsのElement番号と対応。
    /// </summary>
    private readonly List<ParticleSystem[]> cachedParticles = new();

    private void Awake()
    {
        CacheEffects();

        // 開始時に全Effectを必ず停止状態にする
        StopAll();
    }

    private void OnEnable()
    {
        // 有効化された時も、勝手に再生されないように止める
        StopAll();
    }

    private void OnDisable()
    {
        // 敵が非表示・無効化された時にEffectが残らないようにする
        StopAll();
    }

    private void OnDestroy()
    {
        StopAll();
    }

    // =========================================================
    // 再生
    // =========================================================

    /// <summary>
    /// Element番号をIDとしてEffectを再生。
    /// </summary>
    public void PlayEffect(int id)
    {
        GameObject effect = GetEffect(id);
        if (effect == null)
            return;

        if (activateOnPlay)
        {
            effect.SetActive(true);
        }

        ParticleSystem[] particles = GetParticles(id);

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            if (restartOnPlay)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
            else
            {
                if (!ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
        }
    }

    // =========================================================
    // 停止
    // =========================================================

    /// <summary>
    /// Element番号をIDとしてEffectを停止。
    /// </summary>
    public void StopEffect(int id)
    {
        GameObject effect = GetEffect(id);
        if (effect == null)
            return;

        ParticleSystem[] particles = GetParticles(id);

        ParticleSystemStopBehavior stopBehavior =
            clearOnStop
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

        foreach (ParticleSystem ps in particles)
        {
            if (ps == null)
                continue;

            ps.Stop(true, stopBehavior);
        }

        if (deactivateOnStop)
        {
            effect.SetActive(false);
        }
    }

    /// <summary>
    /// 登録されているすべてのEffectを停止。
    /// </summary>
    public void StopAll()
    {
        for (int i = 0; i < effectPrefabs.Count; i++)
        {
            StopEffect(i);
        }
    }

    // =========================================================
    // 状態確認
    // =========================================================

    public bool IsPlaying(int id)
    {
        ParticleSystem[] particles = GetParticles(id);

        foreach (ParticleSystem ps in particles)
        {
            if (ps != null && ps.isPlaying)
                return true;
        }

        return false;
    }

    public int EffectCount => effectPrefabs != null ? effectPrefabs.Count : 0;

    public bool HasEffect(int id)
    {
        if (id < 0 || id >= EffectCount)
            return false;

        return effectPrefabs[id] != null;
    }

    // =========================================================
    // 内部処理
    // =========================================================

    private void CacheEffects()
    {
        cachedParticles.Clear();

        for (int i = 0; i < effectPrefabs.Count; i++)
        {
            GameObject effect = effectPrefabs[i];

            if (effect == null)
            {
                cachedParticles.Add(System.Array.Empty<ParticleSystem>());
                continue;
            }

            ParticleSystem[] particles =
                effect.GetComponentsInChildren<ParticleSystem>(true);

            cachedParticles.Add(particles);
        }
    }

    public GameObject GetEffect(int id)
    {
        if (id < 0 || id >= effectPrefabs.Count)
        {
            Debug.LogWarning($"{nameof(EffectBonePlayer)} : Effect ID is out of range : {id}", this);
            return null;
        }

        GameObject effect = effectPrefabs[id];

        if (effect == null)
        {
            Debug.LogWarning($"{nameof(EffectBonePlayer)} : Effect is null : ID {id}", this);
            return null;
        }

        return effect;
    }

    private ParticleSystem[] GetParticles(int id)
    {
        if (id < 0 || id >= effectPrefabs.Count)
            return System.Array.Empty<ParticleSystem>();

        if (cachedParticles.Count != effectPrefabs.Count)
        {
            CacheEffects();
        }

        ParticleSystem[] particles = cachedParticles[id];

        if (particles == null || particles.Length == 0)
        {
            GameObject effect = effectPrefabs[id];

            if (effect == null)
                return System.Array.Empty<ParticleSystem>();

            particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            cachedParticles[id] = particles;
        }

        return particles;
    }
}