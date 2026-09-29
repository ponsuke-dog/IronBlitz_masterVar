using UnityEngine;

[CreateAssetMenu(menuName = "Select/SelectAnimationData")]
public class SelectAnimationData : ScriptableObject
{

    [SerializeField] public Vector2 endPosition = Vector2.zero;
    [SerializeField] public Vector3 endScale = Vector3.zero;
    [SerializeField] public Vector3 endRotate = Vector3.zero;
    [SerializeField] public float time = 1;
    [SerializeField] public float delay = 0;
    [SerializeField] public EasingType easetype = EasingType.Linear;
}
