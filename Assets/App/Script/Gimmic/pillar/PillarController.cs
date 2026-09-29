using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PillarController : MonoBehaviour,IHitReceiver
{
    [Header("橋のステータス")]
    public float HP = 1;
    private float MaxHp = 1;

    [Header("橋の傾き関係")]

    [Tooltip("落ちるスピード")]
    public float fallSpeed = 90;   // 度 / 1秒
    
    [Tooltip("倒れた時の傾き")]
    public float maxAngle = 90f;
 
    [Tooltip("倒れる前の傾き最大%")]
    public float MaxTiltAngle = 10f;

   

    public List<GameObject> Trigger;

    public enum PillarFallType
    {
        DirectionType4,
        DirectionType8,
        AllDirection,
    }

    [Header("倒れる方向タイプ")]
    public PillarFallType falltype;

    public enum PillarType
    {
        HPType,
        RateType,
    }

    [Header("橋のタイプ")]
    public PillarType type;

    enum PillarState
    {
        Idle,
        Damage,
        Broken,
        Fallen,
    }

    enum PillerAxis
    {
        Front,
        Right,
        Left,
        Back,
    }

    private PillarState state;

    private float currentAngle = 0f;

    private Vector3 axis = Vector3.forward;

    private Vector3 fallDirection;

    public Transform Pivot;
   
    //public Transform TargetTransform;

    private TimeAgent timeAgent;

    public void Awake()
    {
        timeAgent = GetComponent<TimeAgent>();
        state = PillarState.Idle;
        MaxHp = HP;
    }

 
    public void Update()
    {

        if (state == PillarState.Fallen)
        {
            return;
        }

        if (HP <= 0)
        {
            state = PillarState.Broken;
        }

        if (state == PillarState.Idle)
        {
            TiltUpdate();
        }

        if (state == PillarState.Broken)
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
                if (state == PillarState.Broken || state == PillarState.Fallen)
                {
                    return;
                }

                // ワールド座標
                //Vector3 dir = (transform.position - data.attackerObject.transform.position).normalized;
                Vector3 dir = (transform.position - data.attackerHitbox.transform.position).normalized;
                dir.y = 0f;
                dir.Normalize();

                // ローカル座標へ変換
                Vector3 localDir = transform.InverseTransformDirection(dir);

                fallDirection = localDir.normalized;

                switch (falltype)
                {
                    case PillarFallType.DirectionType4:
                        SetFallAxis4(localDir);
                        break;
                    case PillarFallType.DirectionType8:
                        SetFallAxis8(localDir);
                        break;
                    case PillarFallType.AllDirection:
                        break;
                }

                axis = Vector3.Cross(Vector3.up, fallDirection).normalized;

                HP -= 1;
            }
        }
    }

    void TiltUpdate()
    {
     
            // ダメージで徐々に下がる
            float hprate = Mathf.Clamp01(HP / MaxHp);

        float targetAngle = 0;
        if (type == PillarType.HPType)
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
            state = PillarState.Fallen;

            foreach (var tr in Trigger)
            {
                // トリガーをすべてオフ
                // tr.SetActive(false);

                var col = tr.GetComponent<Collider>();
                if (col == null) continue;

                if (col.isTrigger)
                {
                    tr.SetActive(false);
                }
            }
        }
    }

    void SetFallAxis4(Vector3 localDir)
    {
        // プレイヤーが左右方向にいる時
        if (Mathf.Abs(localDir.x) > Mathf.Abs(localDir.z))
        {
            // 左右
            if (localDir.x > 0)
            {
                fallDirection = Vector3.right;
            }
            else
            {
                fallDirection = Vector3.left;
            }
        }
        else
        {
            // 前後
            if (localDir.z > 0)
            {
                fallDirection = Vector3.forward;
            }
            else
            {
                fallDirection = Vector3.back;
            }
        }
    }

    void SetFallAxis8(Vector3 localDir)
    {
        float angle = Mathf.Atan2(localDir.x, localDir.z)* Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45) * 45;
        // 補正
        fallDirection = Quaternion.Euler(0, snapped, 0) * Vector3.forward;
    }
}
