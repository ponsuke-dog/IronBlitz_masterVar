using UnityEngine;
using UnityEngine.InputSystem;

public class AudioMenuDebugTester : MonoBehaviour
{
    [Header("Audio Id")]
    [SerializeField] private string bgmId = "test_bgm";
    [SerializeField] private string uiId = "test_ui";
    [SerializeField] private string seId = "test_se";

    private void Update()
    {
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            AudioManager.Instance.PlayBgm(bgmId);
        }

        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            AudioManager.Instance.StopBgm();
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            AudioManager.Instance.PlayUi(uiId);
        }

        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            AudioManager.Instance.PlaySe(seId, transform.position);
        }
    }
}
