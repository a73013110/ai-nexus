using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Office.Flow.Approvals;
using AiNexus.Features.Office.Flow.Definitions;
using AiNexus.Features.Office.Flow.Rules;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.Flow;

/// <summary>
/// 簽核引擎（ADR 0036）：線性關卡、審核人規則外掛、指派實體化。業務模組以 <see cref="FlowRegistration.AddFlowKind{TKind}"/> 註冊申請種類，
/// 以 <see cref="FlowService"/> 送出；審核、代審、取消、提醒與流程設定都在這個模組。
/// </summary>
public sealed class FlowModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<FlowOptions, FlowOptionsValidator>(FlowOptions.Section);
        services.AddApproverRule<ApplicantManagerRule>(ApplicantManagerRule.Definition)
            .AddApproverRule<UsersRule>(UsersRule.Definition)
            .AddApproverRule<FeatureHoldersRule>(FeatureHoldersRule.Definition);
        services.AddMailSample(new FlowMail(), "簽核通知", "關卡啟動通知審核人、久未審核的每日提醒，以及申請結束時通知申請人。",
            x => new FlowMailModel(
                x.Text(nameof(FlowMailModel.DisplayName), "收件者顯示名稱", MailSampleContext.ExampleName),
                x.Text(nameof(FlowMailModel.Subject), "主旨，也是信中的標題", "待審核：申請 example/project 寫入權限"),
                x.Text(nameof(FlowMailModel.Message), "接在「您好，」後面的說明", $"{MailSampleContext.ExampleName} 送出的「儲存庫權限」申請等你審核（主管）。"),
                x.Text(nameof(FlowMailModel.Path), "按鈕連到的站內路徑", FlowNotices.Path(Guid.Empty))));
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整時需同步重產 openapi.json。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/flow").RequireFeature(FeatureCatalog.FlowApprovals)
            .MapSlice<ListFlowTasks>()
            .MapSlice<ListFlowInstances>()
            .MapSlice<GetFlowInstance>()
            .MapSlice<DecideFlowInstance>()
            .MapSlice<CancelFlowInstance>();
        api.MapGroup("/admin/flow-definitions").RequireFeature(FeatureCatalog.FlowDefinitions)
            .MapSlice<ListFlowDefinitions>()
            .MapSlice<SaveFlowDefinition>();
    }
}
