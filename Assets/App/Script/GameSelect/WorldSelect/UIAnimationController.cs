using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIAnimationController : MonoBehaviour
{

    public bool IsSkip { get; private set; }

    public void Skip()
    {
        IsSkip = true;
    }
    public void ResetSkip()
    {
        IsSkip = false;
    }

    private IEnumerator Animate(float duration,float delay,EasingType easingType,Action<float>onUpdate)
    {
        yield return new WaitForSeconds(delay);
        float time = 0;

        while(time < duration)
        {
            if (IsSkip)
            {
                onUpdate?.Invoke(1f);
                yield break;
            }
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = EasingFunction.Evaluate(easingType, t);

            onUpdate?.Invoke(t);
            yield return null;
        }
        onUpdate?.Invoke(1f);
    }
    // 動かす対象,終わりの位置,時間,遅延時間
    public Coroutine Move(RectTransform target, Vector2 endpos, float duration = 1f,float delay = 0f ,EasingType type = EasingType.Linear)
    {
        Vector2 start = target.anchoredPosition;

        return StartCoroutine(Animate(duration, delay, type, t => { target.anchoredPosition = Vector2.Lerp(start, endpos, t); }));
    }

    // 動かす対象,終わりの位置,時間
    public Coroutine Scale(RectTransform target,Vector3 endscale,float duration = 1,float delay = 0f,EasingType type = EasingType.Linear)
    {
        Vector3 start = target.localScale;
        return StartCoroutine(Animate(duration, delay, type, t => { target.localScale = Vector3.Lerp(start, endscale, t); }));
    }

    public Coroutine Rotate(RectTransform target, Vector3 endEuler, float duration = 1, float delay = 0f, EasingType type = EasingType.Linear)
    {
        Quaternion start = target.localRotation;
        Quaternion end = Quaternion.Euler(endEuler);

        return StartCoroutine(Animate(duration, delay, type, t => { target.localRotation = Quaternion.Lerp(start, end, t); }));
    }  
    public Coroutine FadeImage(Image image, float endAlpha, float duration = 1, float delay = 0f, EasingType type = EasingType.Linear)
    {

        Color start = image.color;
        Color end = start;
        end.a = endAlpha;

        return StartCoroutine(Animate(duration, delay, type, t => { image.color = Color.Lerp(start, end, t); }));
    }
    public Coroutine FadeCanvasGroup(CanvasGroup group, float endAlpha, float duration = 1, float delay = 0f, EasingType type = EasingType.Linear)
    {
        if (group == null)
        {
            Debug.LogError("FadeCanvasGroup: CanvasGroup が null です。呼び出し側で AddComponent してください。");
            return null;
        }

        float start = group.alpha;

        return StartCoroutine(Animate(duration, delay, type, t =>
        {
            group.alpha = Mathf.Lerp(start, endAlpha, t);
        }));
    }
    public Coroutine PlayParallel(params Coroutine[] animations)
    {
        return StartCoroutine(WaitAll(animations));
    }

    private IEnumerator WaitAll(Coroutine[] animations)
    {
        foreach (var anim in animations)
            yield return anim;
    }
    public Coroutine FadeLoop(CanvasGroup group, float duration, float delay)
    {
        return StartCoroutine(FadeLoopRoutine(group, duration, delay));
    }

    private IEnumerator FadeLoopRoutine(CanvasGroup group, float duration, float delay)
    {
        while (true)
        {
            // フェードイン
            yield return FadeCanvasGroup(group, 1f, duration, 0f, EasingType.Linear);

            yield return new WaitForSeconds(delay);

            // フェードアウト
            yield return FadeCanvasGroup(group, 0f, duration, 0f, EasingType.Linear);

            yield return new WaitForSeconds(delay);
        }
    }
    public Coroutine CrossFade(CanvasGroup oldGroup, CanvasGroup newGroup, float duration)
    {
        return StartCoroutine(CrossFadeRoutine(oldGroup, newGroup, duration));
    }

    private IEnumerator CrossFadeRoutine(CanvasGroup oldGroup, CanvasGroup newGroup, float duration)
    {
        float time = 0f;

        float startA = oldGroup.alpha; // usually 1
        float startB = newGroup.alpha; // usually 0

        // 念のため Raycast を遮断しないように
        oldGroup.blocksRaycasts = false;
        newGroup.blocksRaycasts = false;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);

            oldGroup.alpha = Mathf.Lerp(startA, 0f, t);
            newGroup.alpha = Mathf.Lerp(startB, 1f, t);

            yield return null;
        }

        oldGroup.alpha = 0f;
        newGroup.alpha = 1f;
    }

}
