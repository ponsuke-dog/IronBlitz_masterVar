using UnityEngine;

// ============================================
// Particle“¯Šú
// ============================================

public class TimeDriverParticle : TimeDriverBase
{
    [SerializeField]
    private ParticleSystem[] particles;


    void Reset()
    {
        particles = GetComponentsInChildren<ParticleSystem>();
    }


    void Update()
    {
        float scale = agent.TimeScale;

        foreach (var ps in particles)
        {
            if (ps == null) continue;

            var main = ps.main;
            main.simulationSpeed = scale;
        }
    }
}