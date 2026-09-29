using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Input System導入前に、Menuの表示切替を確認するためのデバッグ用クラス。
/// 完成後、またはInput System導入後に削除する。
/// </summary>
public class MenuDebugController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuManager menuManager;

    [Header("Debug Buttons")]
    [SerializeField] private Button openPauseButton;
    [SerializeField] private Button openConfigButton;
    [SerializeField] private Button closeMenuButton;

    private void Awake()
    {
        openPauseButton.onClick.AddListener(menuManager.OpenPauseMenu);
        openConfigButton.onClick.AddListener(menuManager.OpenConfigMenu);
        closeMenuButton.onClick.AddListener(menuManager.CloseAllMenus);
    }
}
