using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// メニューUI用SEを一括管理するクラス。
/// 
/// 【役割】
/// - メニュー内で使うSEの種類とAudioDatabase上のAudio IDを紐づける
/// - AudioManager経由でUI音を再生する
/// - SEごとのクールタイムで連続再生を防ぐ
/// </summary>
public class MenuUiSeController : MonoBehaviour
{
    public enum MenuUiSeType
    {
        Select,
        Enter,
        Back,
        SwitchPage,
        Slider,
        Toggle,
        CategorySelect,
        CursorMove,
        PressAny,
        GameStart,
        GuideSlide
    }

    [Serializable]
    private class MenuUiSeEntry
    {
        [Header("SE種別")]
        public MenuUiSeType seType;

        [Header("AudioDatabaseに登録しているAudio ID")]
        public string audioId;

        [Header("連続再生防止秒数")]
        [Min(0.0f)]
        public float cooldown = 0.05f;

        [Header("未設定時に警告を出す")]
        public bool showWarningIfMissing = true;
    }

    [Header("SE Settings")]
    [SerializeField] private List<MenuUiSeEntry> seEntries = new List<MenuUiSeEntry>();

    [Header("Singleton")]
    [SerializeField] private bool useSingleton = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    public static MenuUiSeController Instance { get; private set; }

    private readonly Dictionary<MenuUiSeType, MenuUiSeEntry> seEntryDictionary =
        new Dictionary<MenuUiSeType, MenuUiSeEntry>();

    private readonly Dictionary<MenuUiSeType, float> lastPlayTimes =
        new Dictionary<MenuUiSeType, float>();

    private void Awake()
    {
        if (useSingleton)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        BuildDictionary();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            BuildDictionary();
        }
    }

    /// <summary>
    /// コンポーネント追加時に初期設定を自動生成する。
    /// Audio IDは必要に応じてInspectorで変更する。
    /// </summary>
    private void Reset()
    {
        seEntries = new List<MenuUiSeEntry>
        {
            new MenuUiSeEntry { seType = MenuUiSeType.Enter, audioId = "ui_enter", cooldown = 0.08f },
            new MenuUiSeEntry { seType = MenuUiSeType.Back, audioId = "ui_back", cooldown = 0.08f },
            new MenuUiSeEntry { seType = MenuUiSeType.SwitchPage, audioId = "ui_switch_page", cooldown = 0.06f },
            new MenuUiSeEntry { seType = MenuUiSeType.Slider, audioId = "ui_slider_single", cooldown = 0.07f },
            new MenuUiSeEntry { seType = MenuUiSeType.Toggle, audioId = "ui_toggle", cooldown = 0.08f },
            new MenuUiSeEntry { seType = MenuUiSeType.CategorySelect, audioId = "ui_category_select", cooldown = 0.08f },
            new MenuUiSeEntry { seType = MenuUiSeType.CursorMove, audioId = "ui_selected", cooldown = 0.04f },
            new MenuUiSeEntry { seType = MenuUiSeType.Select, audioId = "ui_selected", cooldown = 0.04f, showWarningIfMissing = false },
            new MenuUiSeEntry { seType = MenuUiSeType.PressAny, audioId = "ui_pressany", cooldown = 0.10f },
            new MenuUiSeEntry { seType = MenuUiSeType.GameStart, audioId = "ui_gamestart", cooldown = 0.10f },
            new MenuUiSeEntry { seType = MenuUiSeType.GuideSlide, audioId = "ui_guide_slide", cooldown = 0.08f }
        };
    }
#endif

    private void BuildDictionary()
    {
        seEntryDictionary.Clear();

        if (seEntries == null)
        {
            return;
        }

        for (int i = 0; i < seEntries.Count; i++)
        {
            MenuUiSeEntry entry = seEntries[i];

            if (entry == null)
            {
                continue;
            }

            seEntryDictionary[entry.seType] = entry;
        }
    }

    public void Play(MenuUiSeType seType)
    {
        if (seEntryDictionary.TryGetValue(seType, out MenuUiSeEntry entry) == false)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning($"[MenuUiSeController] SE設定が見つかりません。Type = {seType}");
            }

            return;
        }

        if (entry == null || string.IsNullOrEmpty(entry.audioId))
        {
            if (entry != null && entry.showWarningIfMissing)
            {
                Debug.LogWarning($"[MenuUiSeController] Audio IDが未設定です。Type = {seType}");
            }

            return;
        }

        if (CanPlay(seType, entry.cooldown) == false)
        {
            return;
        }

        if (AudioManager.Instance == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[MenuUiSeController] AudioManager.Instanceが見つかりません。");
            }

            return;
        }

        lastPlayTimes[seType] = Time.unscaledTime;

        AudioManager.Instance.PlayUi(entry.audioId);

        if (enableDebugLog)
        {
            Debug.Log($"[MenuUiSeController] Play SE. Type = {seType}, AudioId = {entry.audioId}");
        }
    }

    private bool CanPlay(MenuUiSeType seType, float cooldown)
    {
        if (cooldown <= 0.0f)
        {
            return true;
        }

        if (lastPlayTimes.TryGetValue(seType, out float lastPlayTime) == false)
        {
            return true;
        }

        return Time.unscaledTime - lastPlayTime >= cooldown;
    }

    public void PlaySelect()
    {
        Play(MenuUiSeType.Select);
    }

    public void PlayEnter()
    {
        Play(MenuUiSeType.Enter);
    }

    public void PlayBack()
    {
        Play(MenuUiSeType.Back);
    }

    public void PlaySwitchPage()
    {
        Play(MenuUiSeType.SwitchPage);
    }

    public void PlaySlider()
    {
        Play(MenuUiSeType.Slider);
    }

    public void PlayToggle()
    {
        Play(MenuUiSeType.Toggle);
    }

    public void PlayCategorySelect()
    {
        Play(MenuUiSeType.CategorySelect);
    }

    public void PlayCursorMove()
    {
        Play(MenuUiSeType.CursorMove);
    }

    public void PlayPressAny()
    {
        Play(MenuUiSeType.PressAny);
    }

    public void PlayGameStart()
    {
        Play(MenuUiSeType.GameStart);
    }

    public void PlayGuideSlide()
    {
        Play(MenuUiSeType.GuideSlide);
    }
}