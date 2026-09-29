using UnityEngine;

// ============================================
// TimeDriver基底クラス
// ============================================

[RequireComponent(typeof(TimeAgent))]
public abstract class TimeDriverBase : MonoBehaviour
{
    protected TimeAgent agent;

    protected virtual void Awake()
    {
        agent = GetComponent<TimeAgent>();

        if (agent == null)
        {
            Debug.LogError(
                "TimeDriverにはTimeAgentが必要です");
        }
    }
}
