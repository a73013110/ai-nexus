using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.UnitTests.Attendance;

public sealed class AttendanceFeaturesTests
{
    /// <summary>
    /// 卡號對應、匯入紀錄與假日設定是 HR 的管理頁：管理員以測試身分扮成 HR 時只能讀，不能寫（寫入會以被扮演者的名義留紀錄，假日設定還會回寫 GD）。
    /// 出勤紀錄是每個員工自己的頁面，不在此列。
    /// </summary>
    [Fact]
    public void EveryAttendanceManagementFeatureIsReadOnlyForATestIdentity()
    {
        var attendance = FeatureCatalog.All.Where(x => x.Module.Id == "attendance").Select(x => x.Id).ToList();

        Assert.Equal(
            [FeatureCatalog.AttendanceCards, FeatureCatalog.AttendanceCardsMaintain, FeatureCatalog.AttendanceImports, FeatureCatalog.AttendanceImportsReimport,
             FeatureCatalog.AttendanceCalendar, FeatureCatalog.AttendanceCalendarMaintain],
            attendance.Where(AdministrativeFeatures.All.Contains));
        Assert.Equal([FeatureCatalog.AttendanceRecords], attendance.Where(x => !AdministrativeFeatures.All.Contains(x)));
    }
}
