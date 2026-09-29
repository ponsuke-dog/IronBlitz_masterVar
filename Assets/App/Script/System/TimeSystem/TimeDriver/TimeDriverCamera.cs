using UnityEngine;

public class TimeDriverCamera : TimeDriverBase
{
    [SerializeField]
    private Camera targetCamera;


    void Update()
    {
        if (targetCamera == null)
            return;

        float scale = agent.TimeScale;

        // ó·ÅFFOVââèo
        targetCamera.fieldOfView =
            Mathf.Lerp(
                targetCamera.fieldOfView,
                60f * scale,
                Time.unscaledDeltaTime * 5f);
    }
}