using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Main Buttons")]
    [SerializeField] private Button startRitualButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Back Buttons")]
    [SerializeField] private Button backFromSettingsButton;
    [SerializeField] private Button backFromCreditsButton;

    [Header("Target Gameplay Scene")]
#if UNITY_EDITOR
    [Tooltip("ลากไฟล์ Scene (.unity) จาก Project Window มาวางที่นี่ได้โดยตรง")]
    [SerializeField] private SceneAsset gameSceneAsset;
#endif
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Screen Fader (CanvasGroup)")]
    [SerializeField] private CanvasGroup screenFaderGroup;

    [Header("1. Fade ตอนเปิดเกม (Startup Fade In)")]
    [Tooltip("ให้หน้าจอค่อยๆ สว่างขึ้นเองเมื่อเปิดเกมเข้ามาที่หน้าเมนูหรือไม่")]
    [SerializeField] private bool autoFadeInOnStart = true;
    [Tooltip("ระยะเวลาที่ใช้ในการทำให้ภาพสว่างขึ้น (วินาที)")]
    [SerializeField] private float fadeInDuration = 2.0f;
    [Tooltip("หน่วงเวลาก่อนเริ่มสว่าง (วินาที) เพื่อให้เสียงเริ่มทำงานก่อน")]
    [SerializeField] private float fadeInDelay = 0.5f;

    [Header("2. Fade ตอนเข้าเกม (Enter Game Fade Out)")]
    [Tooltip("ระยะเวลาที่ใช้ในการทำให้ภาพดับมืดลงเมื่อกดเริ่มเกม (วินาที)")]
    [SerializeField] private float fadeOutDuration = 1.0f;
    [Tooltip("หน่วงเวลาค้างไว้ในความมืดก่อนเปลี่ยนฉาก (วินาที)")]
    [SerializeField] private float enterGameDelay = 0.3f;

    private Coroutine currentFadeCoroutine;
    private bool isStartingGame = false;

    private void Awake()
    {
        // บังคับให้หน้าจอดำสนิททันทีก่อนเริ่มเฟรมแรก ป้องกันภาพเมนูกะพริบ
        if (screenFaderGroup != null && autoFadeInOnStart)
        {
            screenFaderGroup.alpha = 1f;
            screenFaderGroup.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        ShowMainMenu();
        RegisterEvents();

        if (screenFaderGroup != null && autoFadeInOnStart)
        {
            StartFade(1f, 0f, fadeInDuration, fadeInDelay, () =>
            {
                screenFaderGroup.blocksRaycasts = false; // ปลดล็อกให้คลิกปุ่มได้เมื่อสว่างแล้ว
            });
        }
        else if (screenFaderGroup != null)
        {
            screenFaderGroup.alpha = 0f;
            screenFaderGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    private void RegisterEvents()
    {
        if (startRitualButton != null)
            startRitualButton.onClick.AddListener(OnStartRitualClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(OnBackToMainClicked);

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.AddListener(OnBackToMainClicked);
    }

    private void UnregisterEvents()
    {
        if (startRitualButton != null)
            startRitualButton.onClick.RemoveListener(OnStartRitualClicked);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);

        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.RemoveListener(OnBackToMainClicked);

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.RemoveListener(OnBackToMainClicked);
    }

    public void ShowMainMenu() => ShowPanel(mainPanel);

    public void ShowSettings() => ShowPanel(settingsPanel);

    public void ShowCredits() => ShowPanel(creditsPanel);

    private void ShowPanel(GameObject target)
    {
        // เปิดเฉพาะ panel ที่เลือก ปิดที่เหลือ
        if (mainPanel != null) mainPanel.SetActive(mainPanel == target);
        if (settingsPanel != null) settingsPanel.SetActive(settingsPanel == target);
        if (creditsPanel != null) creditsPanel.SetActive(creditsPanel == target);
    }

    private void OnStartRitualClicked()
    {
        if (isStartingGame) return;
        isStartingGame = true;

        PlayButtonSound();

        // เล่นเสียงระฆังศักดิ์สิทธิ์
        AudioManager.Bell();

        // ล็อกไม่ให้คลิกปุ่มซ้ำระหว่างกำลังเริ่มเกม
        if (screenFaderGroup != null)
        {
            screenFaderGroup.blocksRaycasts = true;
        }

        StartCoroutine(TransitionToGameRoutine());
    }

    private IEnumerator TransitionToGameRoutine()
    {
        if (screenFaderGroup != null)
        {
            yield return StartCoroutine(FadeRoutine(0f, 1f, fadeOutDuration, 0f, null));
        }

        if (enterGameDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(enterGameDelay);
        }

        if (!string.IsNullOrEmpty(gameSceneName))
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
            while (asyncLoad != null && !asyncLoad.isDone)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogError("MainMenuController: ยังไม่ได้ระบุชื่อหรือลาก GameScene Asset มาใส่ใน Inspector!");
        }
    }

    private void OnSettingsClicked()
    {
        PlayButtonSound();
        ShowSettings();
    }

    private void OnCreditsClicked()
    {
        PlayButtonSound();
        ShowCredits();
    }

    private void OnBackToMainClicked()
    {
        PlayButtonSound();
        ShowMainMenu();
    }

    private void OnQuitClicked()
    {
        PlayButtonSound();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayButtonSound()
    {
        AudioManager.Click();
    }

    private void StartFade(float fromAlpha, float toAlpha, float duration, float delay, Action onComplete)
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }

        currentFadeCoroutine = StartCoroutine(FadeRoutine(fromAlpha, toAlpha, duration, delay, onComplete));
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, float delay, Action onComplete)
    {
        if (screenFaderGroup == null)
            yield break;

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        float elapsed = 0f;
        screenFaderGroup.alpha = fromAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFaderGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            yield return null;
        }

        screenFaderGroup.alpha = toAlpha;
        onComplete?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gameSceneAsset != null)
        {
            gameSceneName = gameSceneAsset.name;
        }
    }
#endif
}