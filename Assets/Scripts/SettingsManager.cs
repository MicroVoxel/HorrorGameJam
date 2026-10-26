using System;
using UnityEngine;

/// <summary>
/// เก็บ/โหลดการตั้งค่า (เสียง, กราฟิก) ผ่าน PlayerPrefs
/// ไม่รู้จัก AudioManager: ใครสนใจค่าเสียงให้ฟัง OnSettingsChanged เอง (AudioManager ทำอยู่แล้ว)
/// </summary>
[DisallowMultipleComponent]
public class SettingsManager : SingletonBehaviour<SettingsManager>
{
    protected override bool Persistent => true;

    private const string KEY_MASTER_VOL = "Setting_MasterVolume";
    private const string KEY_BGM_VOL = "Setting_BGMVolume";
    private const string KEY_SFX_VOL = "Setting_SFXVolume";
    private const string KEY_QUALITY = "Setting_QualityLevel";
    private const string KEY_FULLSCREEN = "Setting_Fullscreen";
    private const string KEY_RES_WIDTH = "Setting_ResWidth";
    private const string KEY_RES_HEIGHT = "Setting_ResHeight";

    public float MasterVolume { get; private set; } = 0.8f;
    public float BGMVolume { get; private set; } = 0.7f;
    public float SFXVolume { get; private set; } = 0.9f;
    public int QualityLevel { get; private set; } = 2;
    public bool IsFullscreen { get; private set; } = true;
    public int ResolutionWidth { get; private set; } = 1920;
    public int ResolutionHeight { get; private set; } = 1080;

    /// <summary>ส่งสัญญาณเมื่อค่าตั้งค่าใดๆ เปลี่ยน (เสียงหรือกราฟิก)</summary>
    public event Action OnSettingsChanged;

    protected override void OnSingletonAwake()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOL, 0.8f);
        BGMVolume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.7f);
        SFXVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, 0.9f);
        QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        IsFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;

        ResolutionWidth = PlayerPrefs.GetInt(KEY_RES_WIDTH, Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt(KEY_RES_HEIGHT, Screen.currentResolution.height);

        ApplyGraphicsSettings();
        OnSettingsChanged?.Invoke();
    }

    public void SetAudioSettings(float master, float bgm, float sfx)
    {
        MasterVolume = master;
        BGMVolume = bgm;
        SFXVolume = sfx;

        PlayerPrefs.SetFloat(KEY_MASTER_VOL, MasterVolume);
        PlayerPrefs.SetFloat(KEY_BGM_VOL, BGMVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOL, SFXVolume);
        PlayerPrefs.Save();

        OnSettingsChanged?.Invoke();
    }

    public void SetGraphicsSettings(int qualityIndex, bool fullscreen, int width, int height)
    {
        QualityLevel = qualityIndex;
        IsFullscreen = fullscreen;
        ResolutionWidth = width;
        ResolutionHeight = height;

        PlayerPrefs.SetInt(KEY_QUALITY, QualityLevel);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, IsFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KEY_RES_WIDTH, ResolutionWidth);
        PlayerPrefs.SetInt(KEY_RES_HEIGHT, ResolutionHeight);
        PlayerPrefs.Save();

        ApplyGraphicsSettings();
        OnSettingsChanged?.Invoke();
    }

    private void ApplyGraphicsSettings()
    {
        QualitySettings.SetQualityLevel(QualityLevel);
        Screen.SetResolution(ResolutionWidth, ResolutionHeight, IsFullscreen);
    }
}
