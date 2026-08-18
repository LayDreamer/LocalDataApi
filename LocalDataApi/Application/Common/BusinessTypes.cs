namespace LocalDataApi.Application.Common;

/// <summary>
/// 业务追溯类型常量(WP05 制造追溯增强)。
/// 取值写入 OperationLog / DataChangeLog 的 BusinessType 列，作为跨日志按业务对象聚合的统一键前缀。
/// 与 WP04 Attachment.BusinessType 取值约定保持一致。
/// </summary>
public static class BusinessTypes
{
    /// <summary>交期评审(PMCDeliveryReview)。</summary>
    public const string DeliveryReview = "DeliveryReview";

    /// <summary>工单销控(WorkOrderSalesControl)。</summary>
    public const string WorkOrder = "WorkOrder";

    /// <summary>排产分析(SchedulingAnalysis)。</summary>
    public const string Scheduling = "Scheduling";

    /// <summary>编码规则(NumberRule)。</summary>
    public const string NumberRule = "NumberRule";

    /// <summary>统一附件(Attachment)；其 BusinessId 取附件所属业务对象，而非 Attachment.Id。</summary>
    public const string Attachment = "Attachment";

    // 注:ExternalProduction 属第二批接入范围，本期不纳入。
}
