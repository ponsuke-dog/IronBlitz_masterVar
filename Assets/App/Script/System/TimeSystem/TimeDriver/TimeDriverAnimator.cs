using UnityEngine;

// ============================================
// Animatoréûä‘êßå‰
// ============================================

public class TimeDriverAnimator : TimeDriverBase
{
    [SerializeField]
    Animator[] animators;


    void Reset()
    {
        animators =
            GetComponentsInChildren<Animator>();
    }


    void Update()
    {
        float scale = agent.TimeScale;

        foreach (var anim in animators)
        {
            if (anim == null) continue;

            // Animatorë¨ìxïœçX
            anim.speed = scale;
        }
    }
}