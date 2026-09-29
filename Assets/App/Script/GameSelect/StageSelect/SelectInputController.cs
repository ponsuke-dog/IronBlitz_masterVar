using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SelectInputController : MonoBehaviour
{
    [SerializeField] private List<Button> worldButton;
    private List<Button> currentStageButtons = new();

    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private string playerMapName = "Player";
    [SerializeField] private string uiMapName = "UI";

    private InputActionMap playerMap;
    private InputActionMap uiMap;


    public GameObject CurrentHoverObject { get; private set; }

    private void Awake()
    {
        playerMap = inputActions.FindActionMap(playerMapName, true);
        uiMap = inputActions.FindActionMap(uiMapName, true);
    }
    private void Update()
    {
        if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0)
        {
            UpdateMaouseSelection();
        }

    }

    private void UpdateMaouseSelection()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);

        pointerData.position = Mouse.current.position.ReadValue();

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            Button button = result.gameObject.GetComponent<Button>();

            if (button != null)
            {
                if (EventSystem.current.currentSelectedGameObject != button.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                }
                return;
            }
        }
    }

    public void Open()
    {
        playerMap.Disable();
        uiMap.Enable();
    }
    public void Close()
    {
        uiMap.Disable();
        playerMap.Enable();
    }

    public void OpenWorldSelect()
    {
        worldButton[0].Select();
    }

    public void SetStageButtons(List<Button>buttons)
    {
        currentStageButtons = buttons;
    }

    public int GetButtonNum()
    {
        
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return -1;

        Button button = selected.GetComponent<Button>();

        if (button == null)
            return -1;

        return currentStageButtons.IndexOf(button);
    }
}
