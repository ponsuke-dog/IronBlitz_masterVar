using System;
using UnityEngine;

public class UISlideAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    [Header("開始位置オフセット")]
    [SerializeField] private Vector2 startOffset = new Vector2(-800f, 0f);

    [Header("時間")]
    [SerializeField] private float duration = 0.35f;

    public Action OnAnimationFinished;

    private Vector2 startPos;
    private Vector2 endPos;

    private float timer;
    private bool playing;

    private void Awake()
    {
        if (target == null)
            target = GetComponent<RectTransform>();

        endPos = target.anchoredPosition;
    }

    public void Play()
    {
        endPos = target.anchoredPosition;
        startPos = endPos + startOffset;

        target.anchoredPosition = startPos;

        timer = 0f;
        playing = true;
    }

    private void Update()
    {
        if (!playing)
            return;

        timer += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(timer / duration);

        // 少し減速して止まる
        t = Mathf.SmoothStep(0f, 1f, t);

        target.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

        if (t >= 1f)
        {
            target.anchoredPosition = endPos;
            playing = false;

            OnAnimationFinished?.Invoke();
        }
    }
}