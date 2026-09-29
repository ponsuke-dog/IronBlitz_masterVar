 using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TimeUIManager : MonoBehaviour
{
    public static TimeUIManager Instance { get; private set; }
    [Header("制限時間の設定(秒)")]
    [SerializeField] public float MaxTimer = 60f;
    public float CurrentTime = 0;

    [Header("制限時間のピンチ時間の設定")]
    [SerializeField] public float PinchTime = 10f;
    [SerializeField] public Color color = Color.red;

    [Header("時間切れ後のシーン遷移先")]
    [SerializeField] SceneData sceneData;

    [Header("時間切れ時の演出時間待ち")]
    [SerializeField] float WaitTime = 0f;

    [Header("オンオフ切り替えをするGameObject")]
    [SerializeField] GameObject root;

    private bool CountDownStart = false;
    private bool TimeUpflg = false;

    //enum TimeState
    //{
    //    Wait,
    //    CountDown,
    //}

    //TimeState State;

    TimeAgent Agent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void Awake()
    {
        // 生成時に他にTimeUIManagerがいるのなら自身を削除
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    

    public void Start()
    {
        CurrentTime = MaxTimer;
        Agent = GetComponent<TimeAgent>();
     //   State = TimeState.Wait;
    }

    private void Update()
    {
        //if (State == TimeState.Wait)
        //{
        //    if (FadeManager.Instance != null && FadeManager.Instance.CurrentFadeProgress != 1)
        //    {
               
        //        return;
        //    }
        //    CountDownStart = true;
        //    State = TimeState.CountDown;
        //    Debug.Log("フェード終了");
        //    return;
        //}
        if (!CountDownStart)
        {
            return;
        }
        if (!Agent)
        {
            Debug.Log("Agentが無い！！！");
            return;
        }

        //経過時間取得
        float dt = Time.deltaTime * Agent.TimeScale;
        CurrentTime -= dt;

        if (CurrentTime <= 0)
        {
            TimeUpflg = true;
        }
        if (TimeUpflg == true)
        {
            TimeUp();
        }

    }

    void TimeUp()
    {
        CurrentTime = 0f;   // 念のため
        CountDownStart = false;   // 2回以上呼ばないため
      

        StartCoroutine(TimeUpRoutine());
    }

    private IEnumerator TimeUpRoutine()
    {
       
        yield return new WaitForSecondsRealtime(WaitTime);

        // ゲームオーバ処理をココ
        var map = InputSystem.actions.FindActionMap("Player");
        map.Disable();
        GameOverManager.Instance.ShowGameOver();
       

    }
    public void SetCountDownStart(bool flg)
    {
        CountDownStart = flg;
    }    
    public void SetTimeUP(bool flg)
    {
        TimeUpflg = flg;
    }
    public void SetTimerRootFlg(bool flg)
    {
        root.SetActive(flg);
    }


    public bool GetCountDownflg()
    {
        return CountDownStart;
    }
    public bool GetTimeUp()
    {
        return TimeUpflg;
    }

    public void AddTime(float time)
    {
        CurrentTime += time;

        if (CurrentTime > MaxTimer)
        {
            CurrentTime = MaxTimer;
        }
    }

}
