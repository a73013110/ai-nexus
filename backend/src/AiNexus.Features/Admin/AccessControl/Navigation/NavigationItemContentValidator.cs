using System.Text.RegularExpressions;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using FluentValidation;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 節點種類與欄位的組合（與資料庫的 <c>ck_NavigationItems_KindColumns</c> 相同）及網址格式。只看 request 本身；
/// 父節點、功能、群組是否存在與主機允許清單要查資料或設定，留給 handler 回 <c>Result</c> 錯誤並寫稽核。
/// </summary>
internal sealed partial class NavigationItemContentValidator : AbstractValidator<INavigationItemContent>
{
    public NavigationItemContentValidator()
    {
        RuleFor(x => x.Kind).Must(x => x is NavigationItem.GroupKind or NavigationItem.PageKind or NavigationItem.LinkKind).WithErrorCode("unknown");
        When(x => x.Kind == NavigationItem.PageKind, () =>
        {
            RuleFor(x => x.FeatureId).Must(x => x is not null && AccessRules.FeatureKeys([x]) is null).WithErrorCode("required");
            RuleFor(x => x.Name).Null().WithErrorCode("not_allowed");
            // 頁面的圖示一律來自目錄，避免同一頁面在不同位置出現不同圖示。
            RuleFor(x => x.Icon).Null().WithErrorCode("not_allowed");
        }).Otherwise(() =>
        {
            RuleFor(x => x.FeatureId).Null().WithErrorCode("not_allowed");
            RuleFor(x => x.Name).Must(x => x is not null && AccessRules.Name(x) is null).WithErrorCode("required");
            RuleFor(x => x.Icon).Must(x => x is null || IconName().IsMatch(x)).WithErrorCode("format");
        });
        When(x => x.Kind == NavigationItem.LinkKind, () =>
        {
            RuleFor(x => x.Url).Must(NavigationLinks.WellFormed).WithErrorCode("https_url");
            RuleFor(x => x.AudienceGroupIds).Must(x => x is null || AccessRules.Keys(x) is null).WithErrorCode("format");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Url).Null().WithErrorCode("not_allowed");
            // 可見群組只對外部連結有意義；頁面的可見範圍就是功能授權，不另設一層。
            RuleFor(x => x.AudienceGroupIds).Must(x => x is null || x.Count == 0).WithErrorCode("not_allowed");
        });
    }

    // 圖示名稱只檢查形狀；是否為前端已有的圖示由前端決定，找不到時顯示預設圖示。
    [GeneratedRegex("^[a-z][a-z0-9-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex IconName();
}
