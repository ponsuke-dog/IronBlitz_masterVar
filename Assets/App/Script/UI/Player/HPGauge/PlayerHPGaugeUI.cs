using UnityEngine;
using UnityEngine.UI;


public class PlayerHPGaugeUI : MonoBehaviour
{
    public static PlayerHPGaugeUI Instance;

    [Header("参照")]
    [Tooltip("HPUIのRoot")]
    [SerializeField] private GameObject HPRoot = null;

    [Header("Materials")]
    public Material greenMat; // 即時
    public Material redMat;   // 遅延ダメージ
    public Material whiteMat; // 回復

    [Header("Speed")]
    public float damageDelaySpeed = 2.5f;
    public float healDelaySpeed = 3.5f;

    private float targetFill;
    private float greenFill;
    private float redFill;
    private float whiteFill;

    private bool isHealing = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (HPRoot == null)
        {
            Debug.LogError("PlayerHPGaugeUI : HPRoot が設定されていません。");
            enabled = false;
            return;
        }
    }

    void Start()
    {

        PlayerHPManager.Instance.OnHPChanged += OnHPChanged;

        float init = (float)PlayerHPManager.Instance.CurrentHP / PlayerHPManager.Instance.MaxHP;

        targetFill = greenFill = redFill = whiteFill = init;

        ApplyAll();
    }

    void OnDestroy()
    {
        if (PlayerHPManager.Instance != null)
            PlayerHPManager.Instance.OnHPChanged -= OnHPChanged;
    }

    void OnHPChanged(int current, int max)
    {
        float newFill = (float)current / max;

        // ===== ダメージ =====

        if (newFill < greenFill)
        {
            isHealing = false;

            // ★赤は「ダメージ前の値」を保持する
            redFill = Mathf.Max(redFill, greenFill);

            greenFill = newFill;
            targetFill = newFill;
            whiteFill = newFill;
        }

        // ===== 回復 =====
        else
        {
            // （回復開始時に基準を揃える）
            redFill = greenFill;

            whiteFill = newFill;
            targetFill = newFill;
            isHealing = true;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ===== ダメージ遅延（赤）=====
        if (!isHealing)
        {
            if (redFill > targetFill)
            {
                redFill -= damageDelaySpeed * dt;
                if (redFill < targetFill)
                    redFill = targetFill;
            }
        }

        // ===== 回復遅延（緑）=====
        else
        {
            if (greenFill < targetFill)
            {
                greenFill += healDelaySpeed * dt;
                if (greenFill > targetFill)
                {
                    greenFill = targetFill;

                    // ★ここが重要
                    redFill = targetFill;   // ←追加

                    whiteFill = targetFill;
                    isHealing = false;
                }
            }
        }


        ApplyAll();
    }

    void ApplyAll()
    {
        greenMat.SetFloat("_Fill", greenFill);
        redMat.SetFloat("_Fill", redFill);
        whiteMat.SetFloat("_Fill", whiteFill);
    }

    public void SetVisible(bool visible)
    {
        if (HPRoot != null)
            HPRoot.SetActive(visible);
    }

}
