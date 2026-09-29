using UnityEngine;

/// <summary>
/// PressAnyButton用SEを再生する補助コンポーネント。
/// </summary>
public class PressAnyButtonSePlayer : MonoBehaviour
{
    /// <summary>
    /// PressAnyButton入力受付音を再生する。
    /// </summary>
    public void PlayPressAny()
    {
        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.PressAny);
    }

    /// <summary>
    /// ゲームスタート音を再生する。
    /// </summary>
    public void PlayGameStart()
    {
        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.GameStart);
    }
}