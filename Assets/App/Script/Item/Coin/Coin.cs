//using UnityEngine;

//public class Coin : MonoBehaviour, IHitReceiver
//{
//    [SerializeField] private float rotateSpeed = 30f;
//    [SerializeField] private float DestroyTime = 1.5f;
//    [SerializeField] private GameObject trigger;
//    private TimeAgent timeAgent;
//    private float timer = 0f;
//    private bool DestroyFlg = false;

//    public int CoinIndex { get; private set; }

//    private void Start()
//    {
//        Debug.Log("coinê∂ê¨");
//        timeAgent = GetComponent<TimeAgent>();
//    }

//    public void Initialize(int index)
//    {
//        CoinIndex = index;
//    }

//    private void Update()
//    {
//        transform.Rotate(0,rotateSpeed * timeAgent.LocalDeltaTime,0);
//        if (DestroyFlg)
//        {

//            CountDown();
//        }
//    }

//    public void OnHit(HitEventData data)
//    {
//        Debug.Log("CoinGet");
//        if (data.targetHitbox == trigger)
//        {
//            rotateSpeed *= 100;
//            DestroyFlg = true;
//            CoinCounter.Instance.CoinGetAnnounce(this);
//            trigger.SetActive(false);
//        }
//    }

//    private void CountDown()
//    {
//        transform.position += Vector3.up * timeAgent.LocalDeltaTime;
//        timer += timeAgent.LocalDeltaTime;
//        if (timer > DestroyTime)
//        {
//            Destroy(gameObject);
//        }
//    }
//}
