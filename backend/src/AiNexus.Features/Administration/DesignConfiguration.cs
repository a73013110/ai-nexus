using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Administration;

/// <summary>
/// 介面元件工作臺（前端 <c>/design</c>）的選單入口。純前端頁面、沒有 API，所以不註冊 policy；
/// 獨立成一個 grant 才能放進系統管理選單並單獨授予，排序放在系統管理最後。
/// </summary>
public sealed class DesignConfiguration() : FeatureSeed(new PlatformFeature(FeatureIds.Design, "介面元件", "/design", 99, BuiltInAccess.SystemNavigation, "sliders", AdministratorsOnly: true));
