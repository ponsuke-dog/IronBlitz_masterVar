using UnityEngine;
using System;

public class PlayerHPManager : MonoBehaviour
{
    public static PlayerHPManager Instance;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsDead => isDead;

    [SerializeField] private PlayerController playerController; // プレイヤーコントローラーへの参照

    [SerializeField] private int maxHP = 100;
    private int currentHP;

    public Action<int, int> OnHPChanged;
    public Action OnDead;

    private bool isDead = false;

    private void Awake()
    {
        Instance = this;
        currentHP = maxHP;
        isDead = false;
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    private void Start()
    {
        if (playerController == null)
        {
            //PlayerタグのオブジェクトからPlayerControllerを自動で取得
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerController = playerObj.GetComponent<PlayerController>();
                if (playerController == null)
                {
                    Debug.LogError("PlayerオブジェクトにPlayerControllerコンポーネントが見つかりませんでした。");
                }
            }
            else
            {
                Debug.LogError("Playerタグのオブジェクトが見つかりませんでした。");
            }
        }
    }   

    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.RightShift))
    //    {
    //        Heal(5);
    //    }
    //}

    public void Damage(int value)
    {
        if (value <= 0 || isDead) return;

        currentHP -= value;
        if (currentHP < 0) currentHP = 0;

        OnHPChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0)
        {
            isDead = true;
            HandleDeath();
        }

        AudioManager.Instance.PlaySe("PL_ReceiveDamage");
    }

    void HandleDeath()
    {
        OnDead?.Invoke();

        // ===== 一括処理ここ =====

        // Input停止
       
        if (playerController != null)
        {
            //playerController.DisableInputFromManager();
        }

        AudioManager.Instance.StopAllSe();

        // UIとかここに追加できる
        // GameOverUI.Show();
        GameOverManager.Instance.ShowGameOver();
    }


    public void Heal(int value)
    {
        if (value <= 0) return;

        currentHP += value;
        if (currentHP > maxHP) currentHP = maxHP;

        if (currentHP > 0)
            isDead = false;

        OnHPChanged?.Invoke(currentHP, maxHP);
    }

}