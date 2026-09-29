using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif
// 各シーンに置く、入力やボタンから遷移を呼ぶ管理スクリプト
public class SceneInputTransition : MonoBehaviour
{
    [Header("遷移先シーン")]
    [SerializeField] private SceneData nextScene;

    //[Header("キーボード入力")]
    //[SerializeField] private KeyCode keyboardKey1 = KeyCode.Space;
    //[SerializeField] private KeyCode keyboardKey2 = KeyCode.Return;

    //[Header("ゲームパッド入力（旧Input Manager）")]
    //[SerializeField] private KeyCode padKey1 = KeyCode.JoystickButton0;
    //[SerializeField] private KeyCode padKey2 = KeyCode.JoystickButton7;

    [SerializeField] private bool EnableInput = false;

    private InputAction ChangeInput = null;

    //[Header("入力受付を有効にするか")]
    //[SerializeField] private bool enableInput = true;

    private void Start()
    {
        var map = InputSystem.actions.FindActionMap("Scene");
        if (map == null) return;

        if (!EnableInput)
        {
            map.Disable();
        }
        else
        {
            map.Enable();

            ChangeInput = map.FindAction("Change");

        }

    }
    private void Update()
    {
        if (!EnableInput) return;
        if (SceneChangeManager.Instance == null) return;

        if(ChangeInput != null && ChangeInput.WasPressedThisFrame())
        {
            SceneChangeManager.Instance.ChangeScene(nextScene);
        }

    
    }

    // UI Button の OnClick からも呼べる
    public void StartTransition()
    {
        if (SceneChangeManager.Instance == null) return;

        SceneChangeManager.Instance.ChangeScene(nextScene);
    }
}