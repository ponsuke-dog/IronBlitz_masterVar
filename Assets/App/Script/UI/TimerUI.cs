using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{

    [Header("åƒÇ—èoÇµÇΩÇ¢ SceneInputTransition")]
    [SerializeField] private SceneInputTransition targetTransition;

    public TextMeshProUGUI timerText;

    public float startTime = 60f; // ïbÅi1ï™Åj
    private float currentTime;

    private bool endFlag;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        currentTime = startTime;
        endFlag = false;
        UpdateTimerText();

    }

    // Update is called once per frame
    void Update()
    {
        if(endFlag)
        {
            targetTransition.StartTransition();
        }


        if (currentTime <= 0f)
        {
            return;
        }

        currentTime -= Time.deltaTime;

        if (currentTime < 0f)
        {
            currentTime = 0f;
            endFlag = true;
        }

        if(currentTime < 30f)
        {
            timerText.color = Color.red;
        }

        UpdateTimerText();

    }

    void UpdateTimerText()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timerText.text = minutes.ToString() + ":" + seconds.ToString("00");
    }
}
