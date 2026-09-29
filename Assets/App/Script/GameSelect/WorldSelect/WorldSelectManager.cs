using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.VisualScripting;

public class WorldSelectManager : MonoBehaviour
{
    public static WorldSelectManager Instance { get; private set; }

    [Header("WorldSet")]
    [SerializeField] private List<WorldData> worlds;

    [Header("TitleScene")]
    [SerializeField] private SceneData TitleScene;

    [Header("WorldImage")]
    [SerializeField] private Image screenA;
    [SerializeField] private Image screenB;

    private CanvasGroup cgA;
    private CanvasGroup cgB;

    private bool useA = true;

    [SerializeField] private Button firstSelectButton;
    private GameObject lastSelected;
    private CanvasGroup screenCG;

    [SerializeField] private float fadeTime = 0.3f;
    private Coroutine fadeRoutine;

    [SerializeField] private GameObject worldButton1;
    [SerializeField] private GameObject worldButton2;
    [SerializeField] private GameObject worldButton3;

    private void Awake()
    {
        // シングルトン
        if (Instance!=null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        worldButton2.SetActive(false);
        worldButton3.SetActive(false);
        SaveSystem.Load();
    }

    private void Start()
    {
        if (SaveSystem.GetUnlock(5))
        {
            World2Open();
        }
        if (SaveSystem.GetUnlock(10))
        {
            World3Open();
        }

        Debug.Log(firstSelectButton.name);
        firstSelectButton.Select();

        // CanvasGroup を必ず確保
        cgA = screenA.GetComponent<CanvasGroup>();
        if (cgA == null) cgA = screenA.gameObject.AddComponent<CanvasGroup>();
        cgA.alpha = 1f;

        cgB = screenB.GetComponent<CanvasGroup>();
        if (cgB == null) cgB = screenB.gameObject.AddComponent<CanvasGroup>();
        cgB.alpha = 0f;


        if (GameData.CurrentStage == null)
        {
            return;
        }
        foreach (var world in worlds)
        {
            foreach (var stage in world.stages)
            {
                if (stage == GameData.CurrentStage)
                {
                  //  SelectAnimationManager.Instance.BackFromStage();
                    StageSelectManager.Instance.SetUP(world);
                }
            }
        }

      
    }

    private void Update()
    {
        var current = EventSystem.current.currentSelectedGameObject;
        if (current == null) return;

        if (current != lastSelected)
        {
            lastSelected = current;

            if (current.name.StartsWith("World"))
            {
                string num = current.name.Replace("World", "");
                int id = int.Parse(num) - 1;
                UpdateWorldPreview(id);
            }
        }
    }

    public void GoToWorld(int id)
    {
        if (id >= worlds.Count || id < 0)
        {
            Debug.LogError("IDが現存するWorldに合いません");
            return;
        }
        StageSelectManager.Instance.SetUP(worlds[id]);
        SelectAnimationManager.Instance.WorldtoStage();
        StageSlectScrool.Instance.ButtonGet();
    }

    public void OnBack()
    {
        StageSelectManager.Instance.InputClose();
        SceneChangeManager.Instance.ChangeScene(TitleScene);
    }

    private void UpdateWorldPreview(int id)
    {
        Sprite next = worlds[id].WorldImage;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(CrossFade(next));
    }

    private IEnumerator CrossFade(Sprite nextSprite)
    {
        Image fadeOutImg = useA ? screenA : screenB;
        Image fadeInImg = useA ? screenB : screenA;

        CanvasGroup fadeOut = useA ? cgA : cgB;
        CanvasGroup fadeIn = useA ? cgB : cgA;

        fadeInImg.sprite = nextSprite;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float a = t / fadeTime;

            fadeOut.alpha = Mathf.Lerp(1f, 0f, a);
            fadeIn.alpha = Mathf.Lerp(0f, 1f, a);

            yield return null;
        }

        useA = !useA;
    }

    public void World2Open()
    {
        worldButton2.SetActive(true);
    }
    
    public void World3Open()
    {
        worldButton3.SetActive(true);
    }
}