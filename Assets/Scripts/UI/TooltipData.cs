using System.Collections.Generic;

/// <summary>
/// ข้อมูลสำหรับแสดงใน TooltipPanel ด้านขวาล่าง (GDD 6.5)
/// - Title / Subtitle: หัวข้อและหมวดหมู่
/// - Summary: ข้อความหลักที่เห็นทันที
/// - Detail: ข้อความละเอียดเมื่อกดปุ่ม Shift ค้าง
/// - Keywords: รายการ AbilityData เพื่อแสดงชิปและคำอธิบายเมื่อกด Shift
/// </summary>
public struct TooltipData
{
    public string Title;
    public string Subtitle;
    public string Summary;
    public string Detail;
    public IReadOnlyList<AbilityData> Keywords;

    public bool IsValid => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Summary);

    public static TooltipData Create(
        string title,
        string summary,
        string detail = null,
        string subtitle = null,
        IReadOnlyList<AbilityData> keywords = null)
    {
        return new TooltipData
        {
            Title = title,
            Subtitle = subtitle,
            Summary = summary,
            Detail = detail,
            Keywords = keywords
        };
    }
}