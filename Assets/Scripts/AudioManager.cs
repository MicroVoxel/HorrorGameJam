using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ศูนย์รวมเสียงของเกม (BGM / Ambience / SFX) ผ่าน AudioMixer
/// ฟัง SettingsManager.OnSettingsChanged เพื่ออัปเดตระดับเสียงเอง (ทิศทางเดียว)
/// SettingsManager จึงไม่ต้องรู้จัก AudioManager เลย
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : SingletonBehaviour<AudioManager>
{
    protected override bool Persistent => true;

    [Header("Audio Mixer Settings")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string bgmParam = "BGMVolume";
    [SerializeField] private string sfxParam = "SFXVolume";

    [Header("Mixer Output Groups (ต้องลากใส่ ไม่เช่นนั้น Slider เสียงจะไม่มีผล)")]
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("UI & Horror Audio Clips")]
    [SerializeField] private AudioClip defaultMenuBGM;
    [SerializeField] private AudioClip buttonHoverSFX;
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip bellChimeSFX;
    [SerializeField] private AudioClip scareStingerSFX;

    protected override void OnSingletonAwake()
    {
        InitializeAudioSources();
    }

    private void Start()
    {
        // ตัวซ้ำที่กำลังจะถูกทำลาย (เช่น กลับมาซีนเมนูที่มี AudioManager อีกตัว) ไม่ต้องทำอะไร
        if (Instance != this) return;

        // ทำใน Start (หลัง Awake ของทุกตัว) เพื่อให้ SettingsManager โหลดค่าเสร็จก่อน และ Mixer รับค่าได้จริง
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged += ApplySavedVolumes;
            ApplySavedVolumes();
        }

        if (defaultMenuBGM != null)
        {
            PlayBGM(defaultMenuBGM, true);
        }
    }

    protected override void OnDestroy()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.OnSettingsChanged -= ApplySavedVolumes;
        }

        base.OnDestroy();
    }

    private void InitializeAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (ambienceSource == null)
        {
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.loop = true;
            ambienceSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        if (bgmGroup != null) bgmSource.outputAudioMixerGroup = bgmGroup;
        if (ambienceGroup != null) ambienceSource.outputAudioMixerGroup = ambienceGroup;
        if (sfxGroup != null) sfxSource.outputAudioMixerGroup = sfxGroup;

        if (mainMixer != null && (bgmGroup == null || sfxGroup == null))
        {
            Debug.LogWarning("AudioManager: มี Main Mixer แต่ยังไม่ได้กำหนด BGM/SFX Group เสียงจะไม่ผ่าน Mixer และ Slider ไม่มีผล");
        }
    }

    #region Static Shortcuts
    // ใช้แทนโค้ด "if (AudioManager.Instance != null) AudioManager.Instance.PlayX()" ที่เคยซ้ำ ~10 จุด
    // (ไม่ใช้ Instance?.PlayX() เพราะ ?. ข้ามการเช็ก null แบบพิเศษของ Unity)
    public static void Click() { if (Instance != null) Instance.PlayButtonClick(); }
    public static void Hover() { if (Instance != null) Instance.PlayButtonHover(); }
    public static void Bell() { if (Instance != null) Instance.PlayBellChime(); }
    public static void Stinger() { if (Instance != null) Instance.PlayScareStinger(); }
    #endregion

    #region Mixer Volume Controls
    private void ApplySavedVolumes()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings == null) return;

        ApplyVolumes(settings.MasterVolume, settings.BGMVolume, settings.SFXVolume);
    }

    public void ApplyVolumes(float master, float bgm, float sfx)
    {
        SetMasterVolume(master);
        SetBGMVolume(bgm);
        SetSFXVolume(sfx);
    }

    public void SetMasterVolume(float linearVolume)
    {
        if (mainMixer != null)
        {
            mainMixer.SetFloat(masterParam, LinearToDecibel(linearVolume));
        }
        else
        {
            AudioListener.volume = linearVolume;
        }
    }

    public void SetBGMVolume(float linearVolume)
    {
        if (mainMixer != null)
        {
            mainMixer.SetFloat(bgmParam, LinearToDecibel(linearVolume));
        }
        else if (bgmSource != null)
        {
            bgmSource.volume = linearVolume;
        }
    }

    public void SetSFXVolume(float linearVolume)
    {
        if (mainMixer != null)
        {
            mainMixer.SetFloat(sfxParam, LinearToDecibel(linearVolume));
        }
        else if (sfxSource != null)
        {
            sfxSource.volume = linearVolume;
        }
    }

    private float LinearToDecibel(float linear)
    {
        // ป้องกันค่า Log 0 (Decibel Volume Scaling)
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }
    #endregion

    #region Playback Functions
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null || bgmSource.clip == clip) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void PlayAmbience(AudioClip clip)
    {
        if (clip == null || ambienceSource.clip == clip) return;

        ambienceSource.clip = clip;
        ambienceSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    // เมธอดเหล่านี้ยังเป็น public เพราะอาจถูกผูกกับ UnityEvent (Button OnClick) ใน Inspector
    public void PlayButtonHover() { PlaySFX(buttonHoverSFX, 0.6f); }
    public void PlayButtonClick() { PlaySFX(buttonClickSFX, 1.0f); }
    public void PlayBellChime() { PlaySFX(bellChimeSFX, 1.0f); }
    public void PlayScareStinger() { PlaySFX(scareStingerSFX, 1.0f); }
    #endregion
}
