using UnityEngine;
using UnityEngine.UI;

public class ResultTimeViewr : MonoBehaviour
{
    public static ResultTimeViewr Instance { get;private set; }
    [SerializeField] private Sprite[] numberSprites;

    [SerializeField] private Image[] digitImages;
    [SerializeField] private Image ColonImages;

    private void Awake()
    {
        // ƒVƒ“ƒOƒ‹ƒgƒ“‰»
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ResultTimerView()
    {
      
        int totalSeconds = Mathf.FloorToInt(TimeUIManager.Instance.MaxTimer - TimeUIManager.Instance.CurrentTime);

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
    }
}
