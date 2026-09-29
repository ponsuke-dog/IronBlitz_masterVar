using UnityEngine;

public class TimeDriverShader : TimeDriverBase
{
    [SerializeField]
    private Renderer[] renderers;

    [SerializeField]
    private string timeProperty = "_CustomTime";


    void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }


    void Update()
    {
        float scale = agent.TimeScale;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            foreach (var mat in r.materials)
            {
                if (mat.HasProperty(timeProperty))
                {
                    mat.SetFloat(timeProperty, scale);
                }
            }
        }
    }
}