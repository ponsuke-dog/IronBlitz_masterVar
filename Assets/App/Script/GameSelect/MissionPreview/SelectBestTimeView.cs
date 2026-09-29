using TMPro;
using UnityEngine;

public class SelectBestTimeView : MonoBehaviour
{
    [SerializeField] private TMP_Text TextUI;
    [SerializeField] private TMP_FontAsset font;
    private string format;

    public void Initialize(string formatText)
    {
        format = formatText;
    }

    public void UpdateText(params object[] args)
    {
        TextUI.text = string.Format(format, args);
        TextUI.font = font;
    }

}
