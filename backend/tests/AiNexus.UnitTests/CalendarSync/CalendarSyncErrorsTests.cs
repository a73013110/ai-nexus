using System.Reflection;
using AiNexus.Features.Office.CalendarSync;

namespace AiNexus.UnitTests.CalendarSync;

public sealed class CalendarSyncErrorsTests
{
    // 排程執行紀錄存的是錯誤代碼，前端依 CalendarSyncFailures 的代碼顯示說明；兩邊寫錯一個字，管理頁就只剩通用訊息。
    [Fact]
    public void EveryFailureCodeMapsToAnErrorWithTheSameCode()
    {
        var codes = typeof(CalendarSyncFailures).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral).Select(x => (string)x.GetRawConstantValue()!).ToList();
        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Equal(code, CalendarSyncErrors.Failure(code).Code));
    }
}
