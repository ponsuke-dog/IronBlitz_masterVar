using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Goal : MonoBehaviour,IHitReceiver
{

    [SerializeField] private GameObject Trigger;

    [Header("GOALの体力")]
    [SerializeField]  private int HP = 1;

    [Header("シーン遷移するまでの時間")]
    [SerializeField] private float Timer = 0f;

    private enum GoalState
    {
        Idle,
        Brokn,
        SceneChange,
        Done,
    }

    GoalState goalState = GoalState.Idle;

    private float timer = 0f;
    public void Update()
    {
        switch(goalState)
        {
            case GoalState.Idle:
                if (HP <= 0)
                {
                    goalState = GoalState.Brokn;
                    // 破壊後の演出を生成させるならココ



                    // ここ以降はほぼ意味なし
                }
                break;
            case GoalState.Brokn:
                // ここにゲームの時間を止める処理
                

                
                // 破壊後の演出更新させるならココ


                // シーン遷移させるまでの待機時間
                timer += Time.deltaTime;
                if (timer >= Timer)
                {
                    goalState = GoalState.SceneChange;
                }
                break;
            case GoalState.SceneChange:

                // ゲームのクリアチェック
                StageClearManager.Instance.ReachGoal();

                goalState = GoalState.Done;
                break;
            case GoalState.Done:

                break;
        }
    }

    public void OnHit(HitEventData data)
    {
        if (data.targetHitbox != Trigger)
        {
            return;
        }
        HP -= 1;       
    }
}
