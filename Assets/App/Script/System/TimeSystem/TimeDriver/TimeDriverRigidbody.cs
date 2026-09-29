using UnityEngine;

// ============================================
// Rigidbody時間制御
// ============================================

public class TimeDriverRigidbody : TimeDriverBase
{
    [SerializeField]
    private Rigidbody[] bodies;

    void Reset()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
    }

    void FixedUpdate()
    {
        float scale = agent.TimeScale;

        foreach (var rb in bodies)
        {
            if (rb == null) continue;

            // ヒットストップ
            if (scale <= 0f)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}