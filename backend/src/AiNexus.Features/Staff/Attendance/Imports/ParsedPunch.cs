namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>打卡檔解析出的一筆刷卡；<see cref="PunchedAt"/> 固定 +08:00，<see cref="AttendanceDate"/> 是它的本地日期。</summary>
public sealed record ParsedPunch(string CardNumber, DateTimeOffset PunchedAt, DateOnly AttendanceDate, string MachineCode, string EventCode)
{
    internal (string Card, long UtcTicks, string Machine) Key => KeyOf(CardNumber, PunchedAt, MachineCode);

    /// <summary>
    /// 刷卡的去重鍵，對應 <c>ux_Punches_CardNumber_PunchedAt_MachineCode</c>：卡號與機器代號不分大小寫（與索引的定序一致），時間以瞬間比較。
    /// 檔內去重與跨檔比對都用它，兩邊規則不同就會在寫入時撞唯一索引。
    /// </summary>
    internal static (string Card, long UtcTicks, string Machine) KeyOf(string card, DateTimeOffset at, string machine)
        => (card.ToUpperInvariant(), at.UtcTicks, machine.ToUpperInvariant());
}
