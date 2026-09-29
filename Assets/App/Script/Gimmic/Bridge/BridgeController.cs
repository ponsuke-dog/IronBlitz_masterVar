using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BridgeController : MonoBehaviour,IHitReceiver
{
    [Header("橋のステータス")]
    public float HP = 1;
    private float MaxHp = 1;

    [Header("橋の傾き関係")]

    [Tooltip("落ちるスピード")]
    public float fallSpeed = 10f;   // 度 / 1秒
    
    [Tooltip("倒れた時の傾き")]
    public float maxAngle = 90f;
 
    [Tooltip("倒れる前の傾き最大%")]
    public float MaxTiltAngle = 10f;


    public List<GameObject> Trigger;

    public enum BridgeType
    {
        HPType,
        RateType,
    }

    [Header("橋のタイプ")]
    public BridgeType type;

    enum BridgeState
    {
        Idle,
        Damage,
        Broken,
        Fallen,
    }

    private BridgeState state;

    private float currentAngle = 0f;

    private Vector3 axis = Vector3.right;
    
    public Transform Pivot;
   
    //public Transform TargetTransform;

    private TimeAgent timeAgent;

    public void Awake()
    {
        timeAgent = GetComponent<TimeAgent>();
        state = BridgeState.Idle;
        MaxHp = HP;
    }

 
    public void Update()
    {

        if (state == BridgeState.Fallen)
        {
            return;
        }

        if (HP <= 0)
        {
            state = BridgeState.Broken;
        }

        if (state == BridgeState.Idle)
        {
            TiltUpdate();
        }

        if (state == BridgeState.Broken)
        {
            FallUpdate();
           
        }
    }


    public void OnHit(HitEventData data)
    {
        foreach (var tr in Trigger)
        {
            if (data.targetHitbox == tr)
            {
                if (state == BridgeState.Broken || state == BridgeState.Fallen)
                {
                    return;
                }
                HP -= 1;
            }
        }
    }

    void TiltUpdate()
    {
     
            // ダメージで徐々に下がる
            float hprate = Mathf.Clamp01(HP / MaxHp);

        float targetAngle = 0;
        if (type == BridgeType.HPType)
        {// 徐々に倒れる→HP０で一気に
            targetAngle = (1 - hprate) * MaxTiltAngle;
        }
        else
        {// HP割合に応じて倒れる
            targetAngle = (1 - hprate) * maxAngle;
        }
        float speed = 60.0f;
            float delta = targetAngle - currentAngle;

            float step = Mathf.Clamp(delta, -speed * timeAgent.LocalDeltaTime, speed * timeAgent.LocalDeltaTime);

            Pivot.Rotate(axis, step, Space.Self);
            currentAngle += step;
        
    }

    void FallUpdate()
    {
        // 壊れて、落ちきる
        float dt = fallSpeed * timeAgent.LocalDeltaTime;

        // 上限チェック
        if (currentAngle + dt > maxAngle)
        {// 次フレームでオーバーする分をカットする
            dt = maxAngle - currentAngle;
        }

        // pivotのローカル座標を軸に回転させる
        Pivot.Rotate(axis, dt, Space.Self);
        
        currentAngle += dt;

        if (currentAngle >= maxAngle)
        {
            state = BridgeState.Fallen;

            foreach (var tr in Trigger)
            {
                // トリガーをすべてオフ
               // tr.SetActive(false);

                var col = tr.GetComponent<Collider>();
                if(col == null) continue;

                if (col.isTrigger)
                {
                    tr.SetActive(false);
                }
            }
        }
    }
}
