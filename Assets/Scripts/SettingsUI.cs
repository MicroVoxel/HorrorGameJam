using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SettingsUI : MonoBehaviour
{
    [Header("Audio Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Graphics Controls")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private Resolution[] availableResolutions;
    private readonly List<Resolution> filteredResolutions = new List<Resolution>();

    private void Start()
    {
        InitializeResolutions();
        PopulateQualityDropdown();
        SyncUIWithCurrentSettings();
        RegisterUIListeners();
    }

    private void InitializeResolutions()
    {
        if (resolutionDropdown == null) return;

        availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        filteredResolutions.Clear();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution res = availableResolutions[i];
            // คัดเลือกเฉพาะสัดส่วน 16:9 เป็นหลักเพื่อป้องกัน UI ผิดเพี้ยน
            float aspect = (float)res.width / res.height;
            if (Mathf.Abs(aspect - 16f / 9f) < 0.05f)
            {
                filteredResolutions.Add(res);
                string option = $"{res.width} x {res.height} @ {res.refreshRateRatio.value:0}Hz";
                options.Add(option);

                if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        if (options.Count == 0) // กรณีไม่มี 16:9 ให้ใส่ fallback ทั้งหมด
        {
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                Resolution res = availableResolutions[i];
                filteredResolutions.Add(res);
                options.Add($"{res.width} x {res.height}");
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void PopulateQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        string[] names = QualitySettings.names;
        qualityDropdown.AddOptions(new List<string>(names));
        qualityDropdown.value = QualitySettings.GetQualityLevel();
        qualityDropdown.RefreshShownValue();
    }

    private void SyncUIWithCurrentSettings()
    {
        if (SettingsManager.Instance == null) return;

        if (masterSlider) masterSlider.value = SettingsManager.Instance.MasterVolume;
        if (bgmSlider) bgmSlider.value = SettingsManager.Instance.BGMVolume;
        if (sfxSlider) sfxSlider.value = SettingsManager.Instance.SFXVolume;
        if (fullscreenToggle) fullscreenToggle.isOn = SettingsManager.Instance.IsFullscreen;
        if (qualityDropdown) qualityDropdown.value = SettingsManager.Instance.QualityLevel;
    }

    private void RegisterUIListeners()
    {
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
        if (bgmSlider) bgmSlider.onValueChanged.AddListener(OnAudioSliderChanged);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(OnAudioSliderChanged);

        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(OnGraphicsChanged);
        if (qualityDropdown) qualityDropdown.onValueChanged.AddListener(delegate { OnGraphicsChanged(false); });
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(delegate { OnGraphicsChanged(false); });
    }

    private void OnAudioSliderChanged(float _)
    {
        if (SettingsManager.Instance != null && masterSlider != null && bgmSlider != null && sfxSlider != null)
        {
            SettingsManager.Instance.SetAudioSettings(masterSlider.value, bgmSlider.value, sfxSlider.value);
        }
    }

    private void OnGraphicsChanged(bool _)
    {
        if (SettingsManager.Instance == null || filteredResolutions.Count == 0 || qualityDropdown == null || fullscreenToggle == null || resolutionDropdown == null) return;

        int resIndex = Mathf.Clamp(resolutionDropdown.value, 0, filteredResolutions.Count - 1);
        Resolution selectedRes = filteredResolutions[resIndex];

        SettingsManager.Instance.SetGraphicsSettings(
            qualityDropdown.value,
            fullscreenToggle.isOn,
            selectedRes.width,
            selectedRes.height
        );
    }
}