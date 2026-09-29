using UnityEngine;
using UnityEngine.UI;

public class HPGaugeUI : MonoBehaviour
{


    public Image hpGauge;

    public float maxHP = 100f;
    public float currentHP = 100f;

    float displayHP;

    public float smoothSpeed = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        displayHP = currentHP;
        UpdateHPUI();
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentHP -= 10f;
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        }

        displayHP = Mathf.MoveTowards(displayHP, currentHP, Time.deltaTime * smoothSpeed * maxHP);

        UpdateHPUI();
    }

    void UpdateHPUI()
    {
        float hpRate = displayHP / maxHP;
        hpGauge.fillAmount = hpRate;

        hpGauge.color = Color.Lerp(Color.red, Color.green, hpRate);
    }
}
