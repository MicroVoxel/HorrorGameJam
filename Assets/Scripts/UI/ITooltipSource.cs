/// <summary>
/// อินเทอร์เฟซสำหรับ UI หรือวัตถุที่ให้ข้อมูล Tooltip กับ TooltipPanel ได้โดยตรง
/// </summary>
public interface ITooltipSource
{
    bool TryGetTooltipData(out TooltipData data);
}