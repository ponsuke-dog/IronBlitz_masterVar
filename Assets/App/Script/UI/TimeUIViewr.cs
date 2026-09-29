using UnityEngine;
using UnityEngine.UI;

public class TimeUIViewr : MonoBehaviour
{

    [SerializeField] private Sprite[] numberSprites;

    [SerializeField] private Image[] digitImages;
    [SerializeField] private Image ColonImages;
    private int lastSeconds = -1;
    void Update()
    {
        int currentSeconds = Mathf.CeilToInt(TimeUIManager.Instance.CurrentTime);

        if (currentSeconds == lastSeconds)
        {
            // 1•bŒo‚Á‚Ä–³‚©‚Á‚½‚ç•Ô‚·
            return;
        }
        lastSeconds = currentSeconds;

        UpdateTimerView();
    }

    void UpdateTimerView()
    {
        int totalSeconds = Mathf.CeilToInt(TimeUIManager.Instance.CurrentTime);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // •¶Žš—ñ‚Æ‚µ‚Ä“o˜^
        string text = $"{minutes:00}{seconds:00}";

        for (int i = 0; i < digitImages.Length; i++)
        {
            // •¶Žš—ñ‚ð”Žš‚Æ‚µ‚ÄØ‚èŽæ‚é
            int number = text[i] - '0';
            digitImages[i].sprite = numberSprites[number];
        }

        if (totalSeconds <= TimeUIManager.Instance.PinchTime)
        {
            ChangeColor(TimeUIManager.Instance.color);
        } 
    }

    void ChangeColor(Color color)
    {
        for (int i = 0; i < digitImages.Length; i++)
        {
            digitImages[i].color = color;
        }
        ColonImages.color = color;
    }
}
