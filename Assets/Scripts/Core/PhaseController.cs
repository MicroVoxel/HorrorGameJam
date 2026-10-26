using System;
using UnityEngine;
using TMPro;

/// <summary>
/// คุมแสงและป้ายเฟสตาม GameState (แทนส่วนแสง/HUD ของ MoonPhaseManager เดิม)
/// Dawn = แสงเช้า | Planning = Moonless (มืด) | Resolution/RoundEnd = Moonlit (แสงจันทร์)
/// ไม่ได้ตัดสินสถานะเอง แค่ฟัง GameStateManager
/// </summary>
[DisallowMultipleComponent]
public class PhaseController : SingletonBehaviour<PhaseController>
{
    [Serializable]
    public struct LightProfile
    {
        public Color color;
        public float intensity;

        public LightProfile(Color color, float intensity)
        {
            this.color = color;
            this.intensity = intensity;
        }
    }

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private float lightTransitionSpeed = 4.0f;

    [Header("Light Profiles")]
    [SerializeField] private LightProfile dawnLight = new LightProfile(new Color(1.0f, 0.92f, 0.75f, 1.0f), 1.3f);
    [Tooltip("Planning (Moonless) - dark, the player prepares the ritual")]
    [SerializeField] private LightProfile moonlessLight = new LightProfile(new Color(0.18f, 0.1f, 0.28f, 1.0f), 0.25f);
    [Tooltip("Resolution and Round End (Moonlit) - the parade is awakened")]
    [SerializeField] private LightProfile moonlitLight = new LightProfile(new Color(0.7f, 0.8f, 1.0f, 1.0f), 1.1f);

    [Header("HUD (Optional)")]
    [SerializeField] private TextMeshProUGUI phaseStatusText;

    /// <summary>ฉากอยู่ในโหมดแสงจันทร์ (Resolution หรือ RoundEnd)</summary>
    public static bool IsMoonlitNow => GameStateManager.IsResolution || GameStateManager.IsRoundEnd;

    private void Start()
    {
        ApplyLightImmediate(GameStateManager.Current);
        UpdateHUD(GameStateManager.Current);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
        }
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        if (directionalLight == null) return;

        LightProfile target = GetProfile(GameStateManager.Current);
        float t = Time.unscaledDeltaTime * lightTransitionSpeed;

        directionalLight.color = Color.Lerp(directionalLight.color, target.color, t);
        directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, target.intensity, t);
    }

    private void HandleStateChanged(GameState newState)
    {
        UpdateHUD(newState);

        if (newState == GameState.Resolution)
        {
            AudioManager.Bell();
        }
    }

    private LightProfile GetProfile(GameState state)
    {
        switch (state)
        {
            case GameState.Dawn: return dawnLight;
            case GameState.Resolution:
            case GameState.RoundEnd: return moonlitLight;
            default: return moonlessLight;
        }
    }

    private void ApplyLightImmediate(GameState state)
    {
        if (directionalLight == null) return;

        LightProfile profile = GetProfile(state);
        directionalLight.color = profile.color;
        directionalLight.intensity = profile.intensity;
    }

    private void UpdateHUD(GameState state)
    {
        if (phaseStatusText != null)
        {
            phaseStatusText.text = UITextProvider.Text.GetPhaseLabel(state);
        }
    }
}
