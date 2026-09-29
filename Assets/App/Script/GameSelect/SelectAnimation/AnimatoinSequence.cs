using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Select/AnimationSequence")]
public class AnimationSequence : ScriptableObject
{
    public List<AnimationStep> steps;   
}
