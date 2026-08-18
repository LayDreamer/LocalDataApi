using LocalDataApi.Api.Filters;
using LocalDataApi.Application.Audit;
using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Tests.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LocalDataApi.Tests.Audit;

/// <summary>
/// WP05 OperationLog 业务追溯测试:
/// 1) BusinessTraceResolver 集中映射(显式、可审查、无字段猜测);
/// 2) OperationLog 持久化 BusinessType/BusinessId 并经 AuditLogQueryService 按业务键过滤。
/// </summary>
public sealed class OperationLogBusinessTraceTests
{
    [Fact]
    public void BusinessTraceResolver_ReturnsExpectedSpecs_ForWiredActions()
    {
        // 交期评审 Create：保存后由结果 PK(编号)回填
        var create = BusinessTraceResolver.Resolve("PMCDeliveryReview", "AddPMCDeliveryReview");
        Assert.NotNull(create);
        Assert.Equal(BusinessTypes.DeliveryReview, create!.BusinessType);
        Assert.Equal(BusinessIdSource.Result, create.Source);
        Assert.Equal("编号", create.PropertyName);

        // 交期评审 Return：入参 request.ReviewId
        var ret = BusinessTraceResolver.Resolve("PMCDeliveryReview", "ReturnDeliveryReview");
        Assert.NotNull(ret);
        Assert.Equal(BusinessTypes.DeliveryReview, ret!.BusinessType);
        Assert.Equal(BusinessIdSource.Argument, ret.Source);
        Assert.Equal("request", ret.ArgumentName);
        Assert.Equal("ReviewId", ret.PropertyName);

        // 工单销控 批量新增：入参集合逐业务对象提取编号(非仅首元素)
        var woAdd = BusinessTraceResolver.Resolve("PMCWorkOrder", "AddOrUpdateWorkOrderSalesControlList");
        Assert.NotNull(woAdd);
        Assert.Equal(BusinessTypes.WorkOrder, woAdd!.BusinessType);
        Assert.Equal(BusinessIdSource.Collection, woAdd.Source);
        Assert.Equal("list", woAdd.ArgumentName);
        Assert.Equal("编号", woAdd.PropertyName);

        // 工单销控 批量删除：入参集合逐业务对象提取(元素为 string 时取自身)
        var woDel = BusinessTraceResolver.Resolve("PMCWorkOrder", "DeleteWorkOrderSalesControlList");
        Assert.NotNull(woDel);
        Assert.Equal(BusinessTypes.WorkOrder, woDel!.BusinessType);
        Assert.Equal(BusinessIdSource.Collection, woDel.Source);
        Assert.Equal("ids", woDel.ArgumentName);

        // 编码规则 Create：结果 Id
        var nrCreate = BusinessTraceResolver.Resolve("NumberRule", "Create");
        Assert.NotNull(nrCreate);
        Assert.Equal(BusinessTypes.NumberRule, nrCreate!.BusinessType);
        Assert.Equal(BusinessIdSource.Result, nrCreate.Source);
        Assert.Equal("Id", nrCreate.PropertyName);

        // 编码规则 Update / Reset：入参 id(路由)
        var nrUpdate = BusinessTraceResolver.Resolve("NumberRule", "Update");
        Assert.NotNull(nrUpdate);
        Assert.Equal(BusinessIdSource.Argument, nrUpdate!.Source);
        Assert.Equal("id", nrUpdate.ArgumentName);

        var nrReset = BusinessTraceResolver.Resolve("NumberRule", "Reset");
        Assert.NotNull(nrReset);
        Assert.Equal(BusinessIdSource.Argument, nrReset!.Source);
        Assert.Equal("id", nrReset.ArgumentName);

        // 附件 Upload：业务键取表单所属业务(businessType/businessId)
        var up = BusinessTraceResolver.Resolve("Attachment", "Upload");
        Assert.NotNull(up);
        Assert.Equal("businessType", up!.BusinessTypeArgument);
        Assert.Equal(BusinessIdSource.Argument, up.Source);
        Assert.Equal("businessId", up.ArgumentName);

        // 附件 Delete：删除前预读取所属业务键
        var del = BusinessTraceResolver.Resolve("Attachment", "Delete");
        Assert.NotNull(del);
        Assert.Equal(BusinessIdSource.AttachmentLookup, del!.Source);
        Assert.Equal("id", del.ArgumentName);

        // 未登记的读操作 / 不存在的 Action 返回 null
        Assert.Null(BusinessTraceResolver.Resolve("PMCDeliveryReview", "GetPMCDeliveryReviewList"));
        Assert.Null(BusinessTraceResolver.Resolve("NumberRule", "GetList"));
        // NumberRule.GetNextCode 为服务内部取号路径，不暴露为 HTTP Action，亦不登记
        Assert.Null(BusinessTraceResolver.Resolve("NumberRule", "GetNextCode"));
    }

    private static ActionExecutingContext BuildExecutingContext(Dictionary<string, object?> arguments)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), arguments, new object());
    }

    /// <summary>批量新增:集合中每个业务对象都应提取到编号(而非仅首元素)。</summary>
    [Fact]
    public void ExtractBusinessIdsForCollection_EntityList_ReturnsEveryElement()
    {
        var list = new List<WorkOrderSalesControl>
        {
            new() { 编号 = "WO-1" },
            new() { 编号 = "WO-2" },
            new() { 编号 = "WO-3" }
        };
        var executing = BuildExecutingContext(new Dictionary<string, object?> { ["list"] = list });
        var spec = new BusinessTraceSpec { Source = BusinessIdSource.Collection, ArgumentName = "list", PropertyName = "编号" };

        var ids = OperationLogFilter.ExtractBusinessIdsForCollection(executing, spec).ToArray();

        Assert.Equal(["WO-1", "WO-2", "WO-3"], ids);
    }

    /// <summary>批量删除:元素为 string 时取元素自身。</summary>
    [Fact]
    public void ExtractBusinessIdsForCollection_StringList_ReturnsEveryElement()
    {
        var idsArg = new List<string> { "WO-10", "WO-11" };
        var executing = BuildExecutingContext(new Dictionary<string, object?> { ["ids"] = idsArg });
        var spec = new BusinessTraceSpec { Source = BusinessIdSource.Collection, ArgumentName = "ids" };

        var ids = OperationLogFilter.ExtractBusinessIdsForCollection(executing, spec).ToArray();

        Assert.Equal(["WO-10", "WO-11"], ids);
    }

    /// <summary>空批量:不产生任何业务键(过滤器会退化为记录一次空键操作)。</summary>
    [Fact]
    public void ExtractBusinessIdsForCollection_EmptyList_ReturnsEmpty()
    {
        var executing = BuildExecutingContext(new Dictionary<string, object?> { ["ids"] = new List<string>() });
        var spec = new BusinessTraceSpec { Source = BusinessIdSource.Collection, ArgumentName = "ids" };

        var ids = OperationLogFilter.ExtractBusinessIdsForCollection(executing, spec).ToArray();

        Assert.Empty(ids);
    }

    [SqlServerFact]
    public async Task OperationLog_PersistsBusinessTrace_AndQueryServiceFilters()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(conn).Options;
        await using var db = new AppDbContext(options);
        var queryService = new AuditLogQueryService(db);

        var idDr = Guid.NewGuid();
        var idNr = Guid.NewGuid();
        var idNull = Guid.NewGuid();
        db.OperationLogs.Add(new OperationLog
        {
            Id = idDr, OperationTimeUtc = DateTimeOffset.UtcNow,
            Module = "PMCDeliveryReview", Action = "AddPMCDeliveryReview",
            BusinessType = BusinessTypes.DeliveryReview, BusinessId = "DR-TEST-1"
        });
        db.OperationLogs.Add(new OperationLog
        {
            Id = idNr, OperationTimeUtc = DateTimeOffset.UtcNow,
            Module = "NumberRule", Action = "Create",
            BusinessType = BusinessTypes.NumberRule, BusinessId = "NR-TEST-1"
        });
        db.OperationLogs.Add(new OperationLog
        {
            Id = idNull, OperationTimeUtc = DateTimeOffset.UtcNow,
            Module = "Other", Action = "X", BusinessType = null, BusinessId = null
        });
        await db.SaveChangesAsync();

        try
        {
            // 按 BusinessType 过滤
            var byDr = await queryService.QueryOperationLogsAsync(new OperationLogQueryDto { BusinessType = BusinessTypes.DeliveryReview, PageSize = 100 });
            Assert.Contains(byDr.Items, x => x.Id == idDr);
            Assert.DoesNotContain(byDr.Items, x => x.Id == idNr);

            // 按 BusinessType + BusinessId 精确过滤
            var byDrId = await queryService.QueryOperationLogsAsync(new OperationLogQueryDto { BusinessType = BusinessTypes.DeliveryReview, BusinessId = "DR-TEST-1", PageSize = 100 });
            Assert.Single(byDrId.Items);
            Assert.Equal("DR-TEST-1", byDrId.Items[0].BusinessId);

            // 历史 NULL 行不被业务键等值过滤命中
            var byNone = await queryService.QueryOperationLogsAsync(new OperationLogQueryDto { BusinessType = BusinessTypes.WorkOrder, PageSize = 100 });
            Assert.DoesNotContain(byNone.Items, x => x.Id == idNull);

            // 持久化往返一致
            var persisted = await db.OperationLogs.FindAsync(idDr);
            Assert.Equal(BusinessTypes.DeliveryReview, persisted!.BusinessType);
            Assert.Equal("DR-TEST-1", persisted.BusinessId);
        }
        finally
        {
            db.OperationLogs.RemoveRange(db.OperationLogs.Where(x => x.Id == idDr || x.Id == idNr || x.Id == idNull));
            await db.SaveChangesAsync();
        }
    }
}
