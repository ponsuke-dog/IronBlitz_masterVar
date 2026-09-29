using TMPro;
using UnityEngine;

public class MissionUILine : MonoBehaviour
{
    public enum UIState
    {
        Normal,
        Failed,
        Cleared,
        Complete,
    }


    [SerializeField] private TMP_Text TextUI;

    private string format;

    public void Initialize(string formatText)
    {
        format = formatText;
    }

    public void UpdateText(params object[] args)
    {
        TextUI.text = string.Format(format, args);
    }

    public void SetUIState(UIState state)
    {
        switch(state)
        {
            case UIState.Normal:
                TextUI.color = Color.white;
                break;
            case UIState.Failed:
                TextUI.color = Color.darkRed;
                break;
            case UIState.Cleared:
                TextUI.color = Color.gray;
                break;
            case UIState.Complete:
                TextUI.color = Color.yellow;
                break;
        }
    }
}
