using UnityEngine;

/// <summary>
/// Inspectorでプルダウン選択可能なイージングタイプ
/// </summary>
public enum EasingType
{
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad
}

/// <summary>
/// 指定したEasingTypeで0～1の補間値を取得
/// </summary>
public static class EasingFunction
{
    public static float Evaluate(EasingType type, float t)
    {
        t = Mathf.Clamp01(t);
        switch (type)
        {
            case EasingType.EaseInQuad: return t * t;
            case EasingType.EaseOutQuad: return 1f - (1f - t) * (1f - t);
            case EasingType.EaseInOutQuad:
                if (t < 0.5f) return 2f * t * t;
                else return 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            case EasingType.Linear:
            default: return t;
        }
    }
}