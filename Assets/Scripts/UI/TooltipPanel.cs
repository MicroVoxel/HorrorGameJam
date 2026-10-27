using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// แผงแสดงคำอธิบายคงที่ด้านขวาล่างของจอ (GDD 6.2 & 6.5)
/// - ตอนว่างแสดงหัวข้อคงที่ tooltipEmptyTitle ("Ritual Guide") และ tooltipEmptyTip (ไม่ดึงชื่อเฟสมาแสดงซ้ำกับ TurnTrackerUI)
/// - ตอนชี้แสดง Title, Subtitle, Summary และชิป Keyword
/// - เมื่อกด Shift ค้าง จะขยายแสดง Detail และคำอธิบายของแต่ละ Keyword
/// </summary>
[DisallowMultipleComponent]
public class TooltipPanel : SingletonBehaviour<TooltipPanel>
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI keywordsText;
    [SerializeField] private TextMeshProUGUI footerHintText;

    private TooltipData currentData;
    private bool hasData;
    private object currentOwner;
    private object lockedOwner;
    private bool lastShiftHeld;

    public bool IsLocked => lockedOwner != null;

    private void Start()
    {
        ShowEmpty();
    }

    private void Update()
    {
        if (!hasData) return;

        bool shift = GameInput.ShiftHeld;
        if (shift != lastShiftHeld)
        {
            lastShiftHeld = shift;
            RenderCurrent();
        }
    }

    public void Show(object owner, in TooltipData data)
    {
        if (lockedOwner != null && owner != lockedOwner) return;
        if (!data.IsValid)
        {
            Clear(owner);
            return;
        }

        currentOwner = owner;
        currentData = data;
        hasData = true;
        lastShiftHeld = GameInput.ShiftHeld;
        RenderCurrent();
    }

    public void Lock(object owner, in TooltipData data)
    {
        lockedOwner = owner;
        currentOwner = owner;
        currentData = data;
        hasData = data.IsValid;
        lastShiftHeld = GameInput.ShiftHeld;

        if (hasData) RenderCurrent();
        else ShowEmpty();
    }

    public void Unlock(object owner)
    {
        if (lockedOwner != owner) return;

        lockedOwner = null;
        ShowEmpty();
    }

    public void Clear(object owner)
    {
        if (lockedOwner != null && owner != lockedOwner) return;
        if (currentOwner != owner) return;

        currentOwner = null;
        hasData = false;
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        if (lockedOwner != null) return;

        hasData = false;
        currentOwner = null;

        UITextData t = UITextProvider.Text;
        if (titleText != null) titleText.text = t.tooltipEmptyTitle;
        if (subtitleText != null) subtitleText.text = string.Empty;
        if (bodyText != null) bodyText.text = t.tooltipEmptyTip;
        if (keywordsText != null) keywordsText.text = string.Empty;
        if (footerHintText != null) footerHintText.text = string.Empty;
    }

    private void RenderCurrent()
    {
        UITextData t = UITextProvider.Text;
        bool shift = GameInput.ShiftHeld;

        if (titleText != null) titleText.text = currentData.Title ?? string.Empty;
        if (subtitleText != null) subtitleText.text = currentData.Subtitle ?? string.Empty;

        if (bodyText != null)
        {
            if (shift && !string.IsNullOrEmpty(currentData.Detail))
            {
                bodyText.text = string.IsNullOrEmpty(currentData.Summary)
                    ? currentData.Detail
                    : $"{currentData.Summary}\n\n{currentData.Detail}";
            }
            else
            {
                bodyText.text = currentData.Summary ?? string.Empty;
            }
        }

        bool hasExtraDetail = !string.IsNullOrEmpty(currentData.Detail)
                              || (currentData.Keywords != null && currentData.Keywords.Count > 0);

        if (keywordsText != null)
        {
            keywordsText.text = FormatKeywords(currentData.Keywords, shift);
        }

        if (footerHintText != null)
        {
            footerHintText.text = (!shift && hasExtraDetail) ? t.tooltipDetailHint : string.Empty;
        }
    }

    private static string FormatKeywords(System.Collections.Generic.IReadOnlyList<AbilityData> list, bool detailed)
    {
        if (list == null || list.Count == 0) return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            AbilityData a = list[i];
            if (a == null) continue;

            if (!detailed)
            {
                if (sb.Length > 0) sb.Append("  |  ");
                sb.Append('[').Append(a.DisplayName).Append(']');
            }
            else
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append('[').Append(a.DisplayName).Append("]: ").Append(a.GetDescription());
            }
        }

        return sb.ToString();
    }
}