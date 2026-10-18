using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// SQLite 範本庫的基礎資料：內建角色、群組、選單分群與功能授權，內容與 <c>database/App/Seed/accesscontrol.sql</c> 相同。
/// </summary>
/// <remarks>
/// 正式環境由 Seed 腳本寫入；SQLite 無法執行那份 T-SQL，只好在這裡重複一份。改 Seed 時兩邊一起改；
/// 測試改用 LocalDB（導入計畫階段 3）後改為直接執行 Seed 腳本，並刪除這個檔案。
/// </remarks>
internal static class BaselineSeed
{
    private const string Work = "work", Collaboration = "collaboration", System = "system";
    private const string Workspace = "workspace", Administrators = "administrators";

    public static void Apply(AppDbContext db)
    {
        db.AddRange(
            new NavigationGroup { Id = Work, Name = "AI 工作台", SortOrder = 10 },
            new NavigationGroup { Id = Collaboration, Name = "協作", SortOrder = 20 },
            new NavigationGroup { Id = System, Name = "系統管理", SortOrder = 30 });
        db.AddRange(
            new Role { Id = "member", Name = "一般使用者" },
            new Role { Id = "administrator", Name = "平台管理員" });
        db.AddRange(
            new RoleGroup { Id = Workspace, Name = "基本工作區" },
            new RoleGroup { Id = Administrators, Name = "平台管理" });
        db.AddRange(
            new RoleGroupRole { RoleId = "member", GroupId = Workspace },
            new RoleGroupRole { RoleId = "administrator", GroupId = Administrators });

        (string Id, string Name, string Route, int SortOrder, string? GroupId, string Icon, string Grant)[] features =
        [
            ("dashboard", "總覽", "/dashboard", 5, null, "dashboard", Workspace),
            ("chat", "對話", "/chat", 10, Work, "chat", Workspace),
            ("projects", "專案", "/projects", 15, Work, "projects", Workspace),
            ("files", "檔案庫", "/files", 20, Work, "files", Workspace),
            ("knowledge", "知識庫", "/knowledge", 30, Work, "library", Workspace),
            ("artifacts", "成果文件", "/artifacts", 40, Work, "document", Workspace),
            ("shared", "分享", "/shared", 50, Collaboration, "share", Workspace),
            ("tasks", "背景任務", "/tasks", 55, Collaboration, "tasks", Workspace),
            ("repositories", "程式庫", "/repositories", 60, Collaboration, "git", Workspace),
            ("admin", "存取管理", "/admin", 90, System, "lock", Administrators),
            ("operations", "平台營運", "/admin/operations", 91, System, "chart", Administrators),
            ("integrations", "資料來源", "/integrations", 92, System, "integrations", Administrators),
            ("monitoring", "即時監控", "/admin/monitoring", 93, System, "activity", Administrators),
            ("logs.query", "系統日誌", "/admin/logs", 94, System, "logs", Administrators),
            ("logs.detail", "日誌診斷詳情", "", 95, System, "search", Administrators),
            ("logs.export", "日誌匯出", "", 96, System, "download", Administrators),
            ("audit", "活動稽核", "/admin/audit", 97, System, "audit", Administrators),
            ("quality", "品質評測", "/quality", 98, System, "shield", Workspace),
            ("design", "介面元件", "/design", 99, System, "sliders", Administrators),
        ];
        foreach (var x in features)
        {
            db.Add(new Feature { Id = x.Id, Name = x.Name, Route = x.Route, SortOrder = x.SortOrder, GroupId = x.GroupId, Icon = x.Icon });
            db.Add(new RoleGroupFeature { GroupId = x.Grant, FeatureId = x.Id });
        }

        db.SaveChanges();
    }
}
