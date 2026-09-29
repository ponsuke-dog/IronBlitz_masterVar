using UnityEngine;

public class TimeDriverTrail : TimeDriverBase
{
    [SerializeField]
    private TrailRenderer[] trails;


    void Reset()
    {
        trails = GetComponentsInChildren<TrailRenderer>();
    }


    void Update()
    {
        float scale = agent.TimeScale;

        foreach (var trail in trails)
        {
            if (trail == null) continue;

            trail.time = scale;
        }
    }
}