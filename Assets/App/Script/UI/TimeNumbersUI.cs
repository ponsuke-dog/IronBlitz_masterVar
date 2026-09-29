using UnityEngine;

public class TimeNumbersUI : MonoBehaviour
{
    public bool StartFlg { get; set; }

    [SerializeField]
    public float timer = 100f;

    [SerializeField]
    public TimeAgent timeAgent;

    private void Update()
    {
        timer -= timeAgent.LocalDeltaTime;

        if (timer < 0)
        {// タイマーが0以下にならないようにする。
            timer = 0;
        }
    }
}
