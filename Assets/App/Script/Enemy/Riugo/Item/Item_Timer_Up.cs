using UnityEngine;

public class Item_Timer_Up : MonoBehaviour
{
    private enum SpinAxis
    {
        X_Axis,
        Y_Axis,
        Z_Axis
    }

    [SerializeField]
    [Header("Time Adding")]
    [Tooltip("ŽžŠÔ‚Ì‘‰Á—Ê")]
    private float m_Time;

    [SerializeField]
    [Header("SpinningModel")]
    [Tooltip("‰ñ“]‚³‚¹‚éƒ‚ƒfƒ‹")]
    private Transform m_modelRoot;

    [SerializeField]
    [Header("Flag Spin Model")]
    [Tooltip("‰ñ“]‚³‚¹‚é‚©‚Ìƒtƒ‰ƒO")]
    private bool m_spiinEnable;

    [SerializeField]
    [Header("Spin Axis")]
    [Tooltip("‰ñ“]Ž²")]
    private SpinAxis m_spinAxis = SpinAxis.Y_Axis;

    [SerializeField]
    [Header("SpinSpeed")]
    [Tooltip("‰ñ“]‘¬“x")]
    private float m_Speed;

    [Header("Pickup Collision Delay")]
    [SerializeField]
    [Tooltip("¶¬’¼Œã‚É“–‚½‚è”»’è‚ðˆêŽž“I‚É–³Œø‰»‚·‚é")]
    private bool disablePickupOnSpawn = true;

    [SerializeField]
    [Tooltip("‰½•bŒã‚É“–‚½‚è”»’è‚ð–ß‚·‚©")]
    private float pickupEnableDelay = 0.5f;

    [SerializeField]
    [Tooltip("Žæ“¾”»’è‚ÉŽg‚¤ColliderB–¢Ý’è‚È‚çŽq‚ðŠÜ‚ß‚ÄŽ©“®Žæ“¾")]
    private Collider[] pickupColliders;

    [Header("Time")]
    [SerializeField] private TimeAgent timeAgent;

    private float TimeScale => timeAgent != null ? timeAgent.TimeScale : 1f;

    private float pickupDelayTimer = 0f;
    private bool waitingPickupEnable = false;
    private bool pickedUp = false;

    private void Awake()
    {
        if (timeAgent == null)
            timeAgent = GetComponent<TimeAgent>();

        if (pickupColliders == null || pickupColliders.Length == 0)
            pickupColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        pickedUp = false;

        if (disablePickupOnSpawn)
        {
            pickupDelayTimer = pickupEnableDelay;
            waitingPickupEnable = pickupEnableDelay > 0f;

            SetPickupCollidersActive(false);

            if (!waitingPickupEnable)
                SetPickupCollidersActive(true);
        }
        else
        {
            pickupDelayTimer = 0f;
            waitingPickupEnable = false;
            SetPickupCollidersActive(true);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime * TimeScale;

        UpdatePickupDelay(dt);
        UpdateSpin(dt);
    }

    private void UpdatePickupDelay(float dt)
    {
        if (!waitingPickupEnable)
            return;

        pickupDelayTimer -= dt;

        if (pickupDelayTimer > 0f)
            return;

        waitingPickupEnable = false;
        SetPickupCollidersActive(true);
    }

    private void UpdateSpin(float dt)
    {
        if (!m_spiinEnable)
            return;

        if (m_modelRoot == null)
            return;

        Vector3 spinAxis = Vector3.zero;

        switch (m_spinAxis)
        {
            case SpinAxis.X_Axis:
                spinAxis.x = 1f;
                break;

            case SpinAxis.Y_Axis:
                spinAxis.y = 1f;
                break;

            case SpinAxis.Z_Axis:
                spinAxis.z = 1f;
                break;

            default:
                return;
        }

        m_modelRoot.Rotate(spinAxis, m_Speed * dt);
    }

    private void SetPickupCollidersActive(bool active)
    {
        if (pickupColliders == null)
            return;

        for (int i = 0; i < pickupColliders.Length; i++)
        {
            if (pickupColliders[i] != null)
                pickupColliders[i].enabled = active;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (pickedUp)
            return;

        if (waitingPickupEnable)
            return;

        if (other.CompareTag("Player"))
        {
            pickedUp = true;

            if (TimeUIManager.Instance != null)
                TimeUIManager.Instance.AddTime(m_Time);

            Destroy(gameObject);
        }

        //Debug.Log("Is Hitting Object");
    }
}