using System;
using UnityEngine;

/// <summary>
/// AudioSystemの動作確認用スクリプト。
/// 
/// 【用途】
/// - AudioManager経由でBGM / SE / UI音を再生確認する
/// - AudioClipVolumeDatabaseによる素材ごとの音量補正が反映されているか確認する
/// - 0〜9キーで各音を鳴らせるようにする
/// 
/// 【キー割り当て】
/// 0 : BGM停止
/// 1 : デフォルトBGM再生
/// 2 : BGMスロット2再生
/// 3 : BGMスロット3再生
/// 4〜9 : SEまたはUI音の再生
/// 
/// 【注意】
/// - AudioClipを直接指定するのではなく、AudioDatabaseに登録しているaudioIdを指定する
/// - AudioSystem Prefabがシーン上に存在している状態で使用する
/// - 本番用ではなく、確認完了後は無効化または削除する
/// </summary>
public class AudioSystemPrefabTest : MonoBehaviour
{
    /// <summary>
    /// 4〜9キーで再生する音の種類。
    /// </summary>
    private enum TestAudioType
    {
        SE,
        UI
    }

    /// <summary>
    /// BGM確認用スロット。
    /// </summary>
    [Serializable]
    private class BgmTestSlot
    {
        [Header("表示用メモ")]
        public string memo;

        [Header("AudioDatabaseに登録しているBGMのID")]
        public string audioId;
    }

    /// <summary>
    /// SE / UI確認用スロット。
    /// </summary>
    [Serializable]
    private class EffectTestSlot
    {
        [Header("表示用メモ")]
        public string memo;

        [Header("再生する音の種類")]
        public TestAudioType audioType = TestAudioType.SE;

        [Header("AudioDatabaseに登録しているAudio ID")]
        public string audioId;

        [Header("SE再生位置オフセット")]
        public Vector3 sePositionOffset;
    }

    [Header("BGM Test Slots")]
    [SerializeField]
    private BgmTestSlot key1DefaultBgm = new BgmTestSlot
    {
        memo = "1キー：基準にするデフォルトBGM",
        audioId = "default_bgm"
    };

    [SerializeField]
    private BgmTestSlot key2Bgm = new BgmTestSlot
    {
        memo = "2キー：BGM確認用",
        audioId = "test_bgm_01"
    };

    [SerializeField]
    private BgmTestSlot key3Bgm = new BgmTestSlot
    {
        memo = "3キー：BGM確認用",
        audioId = "test_bgm_02"
    };

    [Header("SE / UI Test Slots")]
    [SerializeField]
    private EffectTestSlot key4Slot = new EffectTestSlot
    {
        memo = "4キー：SEまたはUI確認用",
        audioType = TestAudioType.SE,
        audioId = "test_se_01"
    };

    [SerializeField]
    private EffectTestSlot key5Slot = new EffectTestSlot
    {
        memo = "5キー：SEまたはUI確認用",
        audioType = TestAudioType.SE,
        audioId = "test_se_02"
    };

    [SerializeField]
    private EffectTestSlot key6Slot = new EffectTestSlot
    {
        memo = "6キー：SEまたはUI確認用",
        audioType = TestAudioType.SE,
        audioId = "test_se_03"
    };

    [SerializeField]
    private EffectTestSlot key7Slot = new EffectTestSlot
    {
        memo = "7キー：SEまたはUI確認用",
        audioType = TestAudioType.UI,
        audioId = "test_ui_01"
    };

    [SerializeField]
    private EffectTestSlot key8Slot = new EffectTestSlot
    {
        memo = "8キー：SEまたはUI確認用",
        audioType = TestAudioType.UI,
        audioId = "test_ui_02"
    };

    [SerializeField]
    private EffectTestSlot key9Slot = new EffectTestSlot
    {
        memo = "9キー：SEまたはUI確認用",
        audioType = TestAudioType.UI,
        audioId = "test_ui_03"
    };

    [Header("SE Position")]
    [SerializeField] private bool useThisObjectPositionForSe = true;
    [SerializeField] private Vector3 fixedSePosition = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private bool hasWarnedMissingAudioManager;

    private void Update()
    {
        AudioManager audioManager = AudioManager.Instance;

        if (audioManager == null)
        {
            ShowMissingAudioManagerWarningOnce();
            return;
        }

        if (GetNumberKeyDown(0))
        {
            StopBgm(audioManager);
        }

        if (GetNumberKeyDown(1))
        {
            PlayBgm(audioManager, key1DefaultBgm, "1");
        }

        if (GetNumberKeyDown(2))
        {
            PlayBgm(audioManager, key2Bgm, "2");
        }

        if (GetNumberKeyDown(3))
        {
            PlayBgm(audioManager, key3Bgm, "3");
        }

        if (GetNumberKeyDown(4))
        {
            PlayEffect(audioManager, key4Slot, "4");
        }

        if (GetNumberKeyDown(5))
        {
            PlayEffect(audioManager, key5Slot, "5");
        }

        if (GetNumberKeyDown(6))
        {
            PlayEffect(audioManager, key6Slot, "6");
        }

        if (GetNumberKeyDown(7))
        {
            PlayEffect(audioManager, key7Slot, "7");
        }

        if (GetNumberKeyDown(8))
        {
            PlayEffect(audioManager, key8Slot, "8");
        }

        if (GetNumberKeyDown(9))
        {
            PlayEffect(audioManager, key9Slot, "9");
        }
    }

    /// <summary>
    /// BGMを停止する。
    /// </summary>
    private void StopBgm(AudioManager audioManager)
    {
        if (audioManager == null)
        {
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log("[AudioSystemPrefabTest] 0キー：BGM停止");
        }

        audioManager.StopBgm();
    }

    /// <summary>
    /// 指定BGMスロットを再生する。
    /// </summary>
    private void PlayBgm(AudioManager audioManager, BgmTestSlot slot, string keyLabel)
    {
        if (audioManager == null)
        {
            return;
        }

        if (slot == null || string.IsNullOrEmpty(slot.audioId))
        {
            Debug.LogWarning($"[AudioSystemPrefabTest] {keyLabel}キー：BGM audioId が未設定です。");
            return;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[AudioSystemPrefabTest] {keyLabel}キー：BGM再生 audioId = {slot.audioId}");
        }

        audioManager.PlayBgm(slot.audioId);
    }

    /// <summary>
    /// 指定スロットのSEまたはUI音を再生する。
    /// </summary>
    private void PlayEffect(AudioManager audioManager, EffectTestSlot slot, string keyLabel)
    {
        if (audioManager == null)
        {
            return;
        }

        if (slot == null || string.IsNullOrEmpty(slot.audioId))
        {
            Debug.LogWarning($"[AudioSystemPrefabTest] {keyLabel}キー：SE/UI audioId が未設定です。");
            return;
        }

        switch (slot.audioType)
        {
            case TestAudioType.SE:
                PlaySe(audioManager, slot, keyLabel);
                break;

            case TestAudioType.UI:
                PlayUi(audioManager, slot, keyLabel);
                break;
        }
    }

    /// <summary>
    /// SEを再生する。
    /// </summary>
    private void PlaySe(AudioManager audioManager, EffectTestSlot slot, string keyLabel)
    {
        Vector3 playPosition = GetSePlayPosition(slot);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[AudioSystemPrefabTest] {keyLabel}キー：SE再生 audioId = {slot.audioId}, position = {playPosition}");
        }

        audioManager.PlaySe(slot.audioId, playPosition);
    }

    /// <summary>
    /// UI音を再生する。
    /// </summary>
    private void PlayUi(AudioManager audioManager, EffectTestSlot slot, string keyLabel)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[AudioSystemPrefabTest] {keyLabel}キー：UI音再生 audioId = {slot.audioId}");
        }

        audioManager.PlayUi(slot.audioId);
    }

    /// <summary>
    /// SEの再生位置を取得する。
    /// </summary>
    private Vector3 GetSePlayPosition(EffectTestSlot slot)
    {
        Vector3 basePosition = useThisObjectPositionForSe
            ? transform.position
            : fixedSePosition;

        if (slot == null)
        {
            return basePosition;
        }

        return basePosition + slot.sePositionOffset;
    }

    /// <summary>
    /// 数字キー入力を取得する。
    /// 上段数字キーとテンキーの両方に対応する。
    /// </summary>
    private bool GetNumberKeyDown(int number)
    {
        switch (number)
        {
            case 0:
                return Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0);

            case 1:
                return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);

            case 2:
                return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);

            case 3:
                return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);

            case 4:
                return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);

            case 5:
                return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);

            case 6:
                return Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6);

            case 7:
                return Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7);

            case 8:
                return Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8);

            case 9:
                return Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9);

            default:
                return false;
        }
    }

    /// <summary>
    /// AudioManagerが存在しない場合に一度だけ警告を出す。
    /// </summary>
    private void ShowMissingAudioManagerWarningOnce()
    {
        if (hasWarnedMissingAudioManager)
        {
            return;
        }

        hasWarnedMissingAudioManager = true;

        Debug.LogWarning(
            "[AudioSystemPrefabTest] AudioManager.Instance が見つかりません。シーン上にAudioSystem Prefabがあるか確認してください。");
    }
}