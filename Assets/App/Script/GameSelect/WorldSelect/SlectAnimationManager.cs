using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using UnityEngine.UI;

public class SelectAnimationManager : MonoBehaviour
{
    public static SelectAnimationManager Instance { get; private set; }

    [SerializeField] private AnimationSequence worldInitSequence;
    [SerializeField] private AnimationSequence stageInitSequence;

    [SerializeField] private AnimationSequence worldToStageSequence;

    [SerializeField] private AnimationSequence stageToWorldSequence;

    [SerializeField] private GameObject WorldButton;
    [SerializeField] private GameObject StageButton;
 
    [SerializeField] private UIAnimationController anime;

    [SerializeField] private CanvasGroup uiBlocker;

    [SerializeField] private List<InputActionReference> skipActions;
    
    
    private bool isAnimating = false;
    private bool isSkip = false;
    private Coroutine playingSequenceCoroutine = null;
    private AnimationSequence currentSequence;

    private void Awake()
    {
        // シングルトン処理
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (GameData.CurrentStage == null)
        {
            ApplyInitSequence(worldInitSequence);
        }
        else
        {
            ApplyInitSequence(stageInitSequence);
        }
    }
    private void OnEnable()
    {
        foreach (var action in skipActions)
        {
            action.action.Enable();
            action.action.performed += OnSkip;
        }
    }

    private void OnDisable()
    {
        foreach (var action in skipActions)
        {
            action.action.performed -= OnSkip;
            action.action.Disable();
        }
    }


    public void WorldtoStage()
    {
        Debug.Log("WorldtoStage開始");
        StartCoroutine(PlayStageSelect(worldToStageSequence,true));
    }
    
    public void StagetoWorld()
    {
        StartCoroutine(PlayStageSelect(stageToWorldSequence,false));
    }

    private RectTransform FindTarget(string name)
    {
        var obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogError($"Animation target not found: {name}");
            return null;
        }
        return obj.GetComponent<RectTransform>();
    }

    private IEnumerator PlayStageSelect(AnimationSequence sequence, bool stage)
    {
        currentSequence = sequence;
        isAnimating = true;
        isSkip = false;
        anime.ResetSkip();

        playingSequenceCoroutine = StartCoroutine(PlaySequence(sequence));
        yield return playingSequenceCoroutine;
        if (!isSkip)
        {
            WorldButton.SetActive(!stage);
            StageButton.SetActive(stage);
        }
        isAnimating = false;
    }
    private IEnumerator PlaySequence(AnimationSequence sequence)
    {
        foreach (var step in sequence.steps)
        {
            if (isSkip)
            {
                ApplyFinalState(step);
                continue;
            }

            RectTransform target = FindTarget(step.targetName);
            Debug.Log(target);
            if (target == null)
            {
                continue;
            }
            Coroutine anim = null;
            target.gameObject.SetActive(true);
            switch (step.type)
            {
                case AnimationType.Move:
                    anim = anime.Move(
                        target,
                        step.data.endPosition,
                        step.data.time,
                        step.data.delay,
                        step.data.easetype
                    );
                    break;

                case AnimationType.Fade:
                    var group = target.GetComponent<CanvasGroup>();
                    if (group == null)
                        group = target.gameObject.AddComponent<CanvasGroup>();

                    anim = anime.FadeCanvasGroup(
                        group,
                        step.fadeAlpha,
                        step.data.time,
                        step.data.delay,
                        step.data.easetype
                    );
                    break;

                case AnimationType.Scale:
                    anim = anime.Scale(
                        target,
                        step.data.endScale,
                        step.data.time,
                        step.data.delay,
                        step.data.easetype
                    );
                    break;
            }

            yield return anim; // 直列再生
        }
    }
    private void ApplyInitSequence(AnimationSequence sequence)
    {
        foreach (var step in sequence.steps)
        {
            RectTransform target = FindTarget(step.targetName);
            if (target == null) continue;

            // 表示/非表示
            target.gameObject.SetActive(step.active);

            // 初期位置
            target.anchoredPosition = step.startPosition;

            // 初期スケール
            target.localScale = step.startScale;

            // 初期フェード
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.gameObject.AddComponent<CanvasGroup>();

            group.alpha = step.startAlpha;
        }
    }

    public void SkipAnimation()
    {
        if (!isAnimating) return;
        if (isSkip) return;

        isSkip = true;
        anime.Skip();

        if (playingSequenceCoroutine != null)
        {
            StopCoroutine(playingSequenceCoroutine);
            playingSequenceCoroutine = null;

        }
    }
    private void ApplyFinalState(AnimationStep step)
    {
        RectTransform target = FindTarget(step.targetName);
        if (target == null) return;

        target.gameObject.SetActive(true);

        // Move
        target.anchoredPosition = step.data.endPosition;

        // Scale
        target.localScale = step.data.endScale;

        // Fade
        var group = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
        group.alpha = step.fadeAlpha;
    }
    private void ApplyStageButtonState()
    {
        // currentSequence が worldToStageSequence なら stage = true
        bool stage = (currentSequence == worldToStageSequence);

        WorldButton.SetActive(!stage);
        StageButton.SetActive(stage);
    }


    private void OnSkip(InputAction.CallbackContext context)
    {
        if (!isAnimating) return;
        if (isSkip) return;

        StageSelectManager.Instance.InputClose();

        EventSystem.current.SetSelectedGameObject(null);

        isSkip = true;
        anime.Skip(); // UIAnimationController にスキップ通知
        uiBlocker.blocksRaycasts = false;

        if (playingSequenceCoroutine != null)
        {
            StopCoroutine(playingSequenceCoroutine);
            playingSequenceCoroutine = null;
        }

        ApplySequenceFinalState(currentSequence);
        ApplyStageButtonState();
        EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(SelectDefaultButton());
    }
    private void ApplySequenceFinalState(AnimationSequence sequence)
    {
        if (sequence == null) return;

        foreach (var step in sequence.steps)
        {
            RectTransform target = FindTarget(step.targetName);
            if (target == null)
            {
                Debug.LogWarning($"Skip: target not found: {step.targetName}");
                continue;
            }

            target.gameObject.SetActive(true);

            switch (step.type)
            {
                case AnimationType.Move:
                    target.anchoredPosition = step.data.endPosition;
                    break;

                case AnimationType.Scale:
                    target.localScale = step.data.endScale;
                    break;

                case AnimationType.Fade:
                    var group = target.GetComponent<CanvasGroup>();
                    if (group == null)
                        group = target.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = step.fadeAlpha;
                    break;

                default:
                    // 何もしない
                    break;
            }
        }
    }

    private IEnumerator SelectDefaultButton()
    {
        yield return null; // 1フレーム待つ
        yield return null;

        uiBlocker.blocksRaycasts = true;

        StageSelectManager.Instance.InputOpen();
        // WorldButton / StageButton どちらでも OK
        if (StageButton.activeSelf)
            StageButton.GetComponentInChildren<Button>().Select();
        else
            WorldButton.GetComponentInChildren<Button>().Select();
    }

}
