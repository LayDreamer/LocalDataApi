using LocalDataApi.Application.Common;

namespace LocalDataApi.Application.Audit;

/// <summary>OperationLog BusinessId 提取来源(WP05)。</summary>
public enum BusinessIdSource
{
    /// <summary>不提取(绝大多数 Action 无需业务追溯)。</summary>
    None,

    /// <summary>从 Action 入参(含表单字段)显式读取。</summary>
    Argument,

    /// <summary>从 Action 返回结果(如 ApiResponse&lt;T&gt;.Data)显式读取;用于“保存后才产生业务主键”的 Create 场景。</summary>
    Result,

    /// <summary>删除前先从已存在记录预读取其所属业务键(Attachment.Delete 等)。</summary>
    AttachmentLookup,

    /// <summary>批量操作:入参为集合时,为每个业务对象分别写入一行 OperationLog(而非只取首元素)。</summary>
    Collection
}

/// <summary>单个 Action 的业务追溯显式配置(WP05)。集中登记,可审查、可测试,不做字段名猜测。</summary>
public sealed class BusinessTraceSpec
{
    /// <summary>常量业务类型(取自 <see cref="BusinessTypes"/>);与 <see cref="BusinessTypeArgument"/> 二选一。</summary>
    public string? BusinessType { get; init; }

    /// <summary>动态业务类型:从指定 Action 入参名读取(如 Attachment.Upload 的 businessType 表单字段)。</summary>
    public string? BusinessTypeArgument { get; init; }

    /// <summary>BusinessId 提取来源。</summary>
    public BusinessIdSource Source { get; init; }

    /// <summary>提取 BusinessId 所用的入参名(Argument/AttachmentLookup 场景)。</summary>
    public string? ArgumentName { get; init; }

    /// <summary>在入参对象/结果对象/集合首元素上读取的业务主键属性名(如 编号、ReviewId、Id)。为空表示取对象本身或集合首元素。</summary>
    public string? PropertyName { get; init; }
}

/// <summary>
/// WP05 制造追溯增强 —— OperationLog 业务键集中映射器。
/// 以 (ControllerName, ActionName) 为键显式登记首批 Action 的 BusinessType / BusinessId 来源,
/// 由 <c>OperationLogFilter</c> 在 Action 执行前后按此映射提取并写入,禁止通用反射猜字段。
/// 不修改任何业务 Controller。
/// </summary>
public static class BusinessTraceResolver
{
    private static readonly Dictionary<(string Controller, string Action), BusinessTraceSpec> Map = new()
    {
        // 交期评审
        [("PMCDeliveryReview", "AddPMCDeliveryReview")] = new()
        {
            BusinessType = BusinessTypes.DeliveryReview,
            Source = BusinessIdSource.Result,
            PropertyName = "编号"
        },
        [("PMCDeliveryReview", "ReturnDeliveryReview")] = new()
        {
            BusinessType = BusinessTypes.DeliveryReview,
            Source = BusinessIdSource.Argument,
            ArgumentName = "request",
            PropertyName = "ReviewId"
        },

        // 工单销控(批量写操作:为每个业务对象分别记录,而非仅首元素)
        [("PMCWorkOrder", "AddOrUpdateWorkOrderSalesControlList")] = new()
        {
            BusinessType = BusinessTypes.WorkOrder,
            Source = BusinessIdSource.Collection,
            ArgumentName = "list",
            PropertyName = "编号"
        },
        [("PMCWorkOrder", "DeleteWorkOrderSalesControlList")] = new()
        {
            BusinessType = BusinessTypes.WorkOrder,
            Source = BusinessIdSource.Collection,
            ArgumentName = "ids"
        },

        // 编码规则(仅配置类写操作;GetNextCode 不记录)
        [("NumberRule", "Create")] = new()
        {
            BusinessType = BusinessTypes.NumberRule,
            Source = BusinessIdSource.Result,
            PropertyName = "Id"
        },
        [("NumberRule", "Update")] = new()
        {
            BusinessType = BusinessTypes.NumberRule,
            Source = BusinessIdSource.Argument,
            ArgumentName = "id"
        },
        [("NumberRule", "Reset")] = new()
        {
            BusinessType = BusinessTypes.NumberRule,
            Source = BusinessIdSource.Argument,
            ArgumentName = "id"
        },

        // 统一附件(Upload 业务键取表单所属业务;Delete 删除前预读取所属业务键)
        [("Attachment", "Upload")] = new()
        {
            BusinessTypeArgument = "businessType",
            Source = BusinessIdSource.Argument,
            ArgumentName = "businessId"
        },
        [("Attachment", "Delete")] = new()
        {
            Source = BusinessIdSource.AttachmentLookup,
            ArgumentName = "id"
        }
    };

    public static BusinessTraceSpec? Resolve(string? controller, string? action) =>
        controller is null || action is null ? null : Map.TryGetValue((controller, action), out var spec) ? spec : null;
}
