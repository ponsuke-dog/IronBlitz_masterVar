using UnityEngine;




[System.Serializable]
public class AnimationStep
{
    public string targetName;

    public bool active = true;

    public Vector2 startPosition;
    public Vector3 startScale = Vector3.one;
    public float startAlpha = 1f;

    
    public AnimationType type;


    public SelectAnimationData data;   // Move / Scale —p
    public float fadeAlpha;            // Fade —p
}
