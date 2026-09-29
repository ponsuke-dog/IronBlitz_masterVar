using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ResultUIAnimation : MonoBehaviour
{
    public static ResultUIAnimation Instance { get; private set; }

    [SerializeField] private UIAnimationController anime;

    [SerializeField] private List<InputActionReference> skipAction;

    [SerializeField] List<Image> stars;
    [SerializeField] List<GameObject> Lines;
    [SerializeField] List<Button> Buttons;

    [SerializeField] SelectAnimationData StarAnimedata;
    [SerializeField] SelectAnimationData LineAnimedata;
    [SerializeField] SelectAnimationData ButtonAnimedata;

    [SerializeField] float StarFirstSize = 5f;
    [SerializeField] int LinsFirstPosistion = 3;

    private RectTransform[] starData;
    private RectTransform[] lineData;
    private RectTransform[] buttonData;

    private bool isAnimating = false;
    private bool isSkip = false;

    private Vector2[] lineEndPositions;
    private Vector2[] buttonEndPositions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        starData = new RectTransform[stars.Count];
        lineData = new RectTransform[Lines.Count];
        buttonData = new RectTransform[Buttons.Count];
        // alphaを0に
        foreach (var star in stars)
        {
            Color color = star.color;
            color.a = 0f;
            star.color = color;
        }

        for (int i = 0; i < starData.Length; i++)
        {
            starData[i] = stars[i].rectTransform;
            starData[i].localScale = new Vector3(StarFirstSize, StarFirstSize, StarFirstSize);
            starData[i].localEulerAngles = new Vector3(0, 0, 180);
        }

        lineEndPositions = new Vector2[lineData.Length];
        buttonEndPositions = new Vector2[buttonData.Length];

        for (int i = 0; i < lineData.Length; i++)
        {
            lineData[i] = Lines[i].GetComponent<RectTransform>();

            lineEndPositions[i] = lineData[i].anchoredPosition;

            lineData[i].anchoredPosition = new Vector2(lineEndPositions[i].x * LinsFirstPosistion, lineEndPositions[i].y);
        }    
        for (int i = 0; i < buttonData.Length; i++)
        {
            buttonData[i] = Buttons[i].GetComponent<RectTransform>();

            buttonEndPositions[i] = buttonData[i].anchoredPosition;

            buttonData[i].anchoredPosition = new Vector2(buttonEndPositions[i].x * LinsFirstPosistion, buttonEndPositions[i].y);
        }
    }
    private void OnEnable()
    {
        foreach (var action in skipAction)
        {
            action.action.Enable();
            action.action.performed += OnSkip;
    }
    }

    private void OnDisable()
    {
        foreach (var action in skipAction)
        {
            action.action.performed -= OnSkip;
            action.action.Disable();
    }
    }
    public void ResultAnime()
    {
        isAnimating = true;
        isSkip = false;

        anime.ResetSkip();

        StartCoroutine(ResultUIsCoroutine());

    }

    private IEnumerator ResultUIsCoroutine()
    {
        yield return StartCoroutine(ResultMissionsLinesCoroutine());
        yield return StartCoroutine(StarsGetCoroutine());
        yield return StartCoroutine(ResultButtonsLinesCoroutine());

        isAnimating = false;

        yield return null;

        Buttons[0].Select();
    }

    private IEnumerator ResultMissionsLinesCoroutine()
    {
        float interval = 0.3f;
        for (int i = 0; i < lineData.Length; i++)
        {
            anime.Move(lineData[i], lineEndPositions[i], LineAnimedata.time, LineAnimedata.delay, LineAnimedata.easetype);

            float timer = 0;

            while (timer < interval)
            {
                if (isSkip)
                    yield break;

                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        yield return new WaitForSecondsRealtime(LineAnimedata.time - interval);
    }

    private IEnumerator ResultButtonsLinesCoroutine()
    {
        float interval = 0.3f;
        for (int i = 0; i < buttonData.Length; i++)
        {
            anime.Move(buttonData[i], buttonEndPositions[i], ButtonAnimedata.time, ButtonAnimedata.delay, ButtonAnimedata.easetype);
            float timer = 0;

            while (timer < interval)
            {
                if (isSkip)
                    yield break;

                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        yield return new WaitForSecondsRealtime(ButtonAnimedata.time);

    }

    private IEnumerator StarsGetCoroutine()
    {
        float interval = 0.3f;


        for (int i = 0; i < starData.Length; i++)
        {
            if (MissionManager.Instance.GetSubMissionClearFlg(i))
            {
                anime.Scale(starData[i], StarAnimedata.endScale, StarAnimedata.time, StarAnimedata.delay, StarAnimedata.easetype);
                anime.Rotate(starData[i], StarAnimedata.endRotate, StarAnimedata.time, StarAnimedata.delay, StarAnimedata.easetype);
                anime.FadeImage(stars[i], 1, StarAnimedata.time / 2, StarAnimedata.delay, StarAnimedata.easetype);
                float timer = 0;

                while (timer < interval)
                {
                    if (isSkip)
                        yield break;

                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
        yield return new WaitForSecondsRealtime(StarAnimedata.time);
    }

    private void OnSkip(InputAction.CallbackContext context)
    {
        if (!isAnimating) return; // アニメーション中以外は無視

        if (isSkip) return;       // 二重押し防止


        isSkip = true;
        anime.Skip();
        ShowAll();

        EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(SelectFirstButton());
    }

    private void ShowAll()
    {
        // ライン
        for (int i = 0; i < lineData.Length; i++)
        {
            lineData[i].anchoredPosition = lineEndPositions[i];
        }

        // 星
        for (int i = 0; i < starData.Length; i++)
        {
            if (!MissionManager.Instance.GetSubMissionClearFlg(i))
                continue;

            starData[i].localScale = StarAnimedata.endScale;
            starData[i].localEulerAngles = StarAnimedata.endRotate;

            Color c = stars[i].color;
            c.a = 1f;
            stars[i].color = c;
        }

        // ボタン
        for (int i = 0; i < buttonData.Length; i++)
        {
            buttonData[i].anchoredPosition = buttonEndPositions[i];
        }

        isAnimating = false;
    }
    private IEnumerator SelectFirstButton()
    {
        yield return null; // 1フレーム待つ

        Buttons[0].Select();
    }
}
