using System;
using UnityEngine;

public class GameOverUIAnimation : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform target;

    [Header("Animation Offset")]
    [Tooltip("開始位置（最終位置からのオフセット）")]
    [SerializeField] private float startYOffset = 500f;

    [Tooltip("行き過ぎる量（マイナスで下へ）")]
    [SerializeField] private float overShootYOffset = -25f;

    [Header("Animation Time")]
    [SerializeField] private float moveTime = 0.45f;

    [SerializeField] private float settleTime = 0.15f;

    /// <summary>
    /// アニメーション終了イベント
    /// </summary>
    public Action OnAnimationFinished;

    private Vector2 endPos;
    private Vector2 startPos;
    private Vector2 overPos;

    private float timer;
    private bool playing;

    private void Awake()
    {
        if (target == null)
            target = GetComponent<RectTransform>();

        // 配置されている位置を最終位置として保存
        endPos = target.anchoredPosition;
    }

    // アニメーション開始
    public void Play()
    {
        timer = 0f;
        playing = true;

        // 毎回現在の配置位置を基準にする
        endPos = target.anchoredPosition;

        startPos = endPos + Vector2.up * startYOffset;
        overPos = endPos + Vector2.up * overShootYOffset;

        target.anchoredPosition = startPos;
    }

    private void Update()
    {
        if (!playing)
            return;

        timer += Time.unscaledDeltaTime;

        if (timer <= moveTime)
        {
            float t = timer / moveTime;
            t = EaseOutBack(t);

            target.anchoredPosition =
                Vector2.LerpUnclamped(startPos, overPos, t);

            return;
        }

        if (timer <= moveTime + settleTime)
        {
            float t = (timer - moveTime) / settleTime;

            target.anchoredPosition =
                Vector2.Lerp(overPos, endPos, t);

            return;
        }

        target.anchoredPosition = endPos;
        playing = false;

        OnAnimationFinished?.Invoke();
    }

    // 少し行き過ぎて戻るイージング
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f
             + c3 * Mathf.Pow(x - 1f, 3f)
             + c1 * Mathf.Pow(x - 1f, 2f);
    }
}