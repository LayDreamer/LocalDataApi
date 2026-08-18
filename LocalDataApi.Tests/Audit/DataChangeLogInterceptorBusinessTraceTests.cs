using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Tests.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LocalDataApi.Tests.Audit;

/// <summary>
/// WP05 DataChangeLogInterceptor 业务追溯测试:
/// 1) 白名单与业务键映射单元验证(含 NumberRule 不入白名单、DataChangeLog 不递归、Attachment 取所属业务键);
/// 2) 真实 SQL Server 集成验证拦截器写入正确的 BusinessType/BusinessId。
/// </summary>
public sealed class DataChangeLogInterceptorBusinessTraceTests
{
    // ---------- 单元:白名单与业务键映射 ----------

    [Fact]
    public void GetEntityName_IncludesWp05Whitelist_ExcludesNumberRule_AndSelf()
    {
        Assert.Equal("PMCDeliveryReview", DataChangeLogInterceptor.GetEntityName(new PMCDeliveryReview()));
        Assert.Equal("WorkOrderSalesControl", DataChangeLogInterceptor.GetEntityName(new WorkOrderSalesControl()));
        Assert.Equal("SchedulingAnalysis", DataChangeLogInterceptor.GetEntityName(new SchedulingAnalysis()));
        Assert.Equal("Attachment", DataChangeLogInterceptor.GetEntityName(new Attachment()));
        // NumberRule 不在首批白名单
        Assert.Null(DataChangeLogInterceptor.GetEntityName(new NumberRule()));
        // 日志实体自身不递归
        Assert.Null(DataChangeLogInterceptor.GetEntityName(new DataChangeLog()));
    }

    [Fact]
    public void ResolveBusinessKeys_MapsPmcToBusinessTypeAndPk_AndAttachmentToOwnedKey()
    {
        var (drBt, drBid) = DataChangeLogInterceptor.ResolveBusinessKeys(new PMCDeliveryReview { 编号 = "DR-1" }, "DR-1");
        Assert.Equal(BusinessTypes.DeliveryReview, drBt);
        Assert.Equal("DR-1", drBid);

        var (woBt, woBid) = DataChangeLogInterceptor.ResolveBusinessKeys(new WorkOrderSalesControl { 编号 = "WO-1" }, "WO-1");
        Assert.Equal(BusinessTypes.WorkOrder, woBt);
        Assert.Equal("WO-1", woBid);

        var (saBt, saBid) = DataChangeLogInterceptor.ResolveBusinessKeys(new SchedulingAnalysis { 编号 = "SA-1" }, "SA-1");
        Assert.Equal(BusinessTypes.Scheduling, saBt);
        Assert.Equal("SA-1", saBid);

        // Attachment 取所属业务对象,而非 Attachment.Id
        var (attBt, attBid) = DataChangeLogInterceptor.ResolveBusinessKeys(
            new Attachment { Id = 999, BusinessType = "DeliveryReview", BusinessId = "DR-OWNED-1" }, "999");
        Assert.Equal("DeliveryReview", attBt);
        Assert.Equal("DR-OWNED-1", attBid);
    }

    // ---------- 集成:真实 SQL Server ----------

    [SqlServerFact]
    public async Task Interceptor_CapturesPmcDeliveryReview_Create_WithFinalPkAsBusinessId()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        using var provider = BuildProvider(conn);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 仅设主键"编号"(该表其余列均可空;RowVersion 由 SQL Server 自动生成)
        var reviewNo = "WP05-DR-" + Guid.NewGuid().ToString("N")[..10];
        var review = new PMCDeliveryReview { 编号 = reviewNo };
        db.Set<PMCDeliveryReview>().Add(review);
        await db.SaveChangesAsync();

        try
        {
            var log = await db.DataChangeLogs
                .Where(x => x.EntityName == "PMCDeliveryReview" && x.EntityId == reviewNo)
                .OrderByDescending(x => x.ChangeTimeUtc)
                .FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.Equal(BusinessTypes.DeliveryReview, log!.BusinessType);
            Assert.Equal(reviewNo, log.BusinessId); // 最终 PK(编号)即业务键
            Assert.Equal("Added", log.ChangeType);
        }
        finally
        {
            await db.Set<PMCDeliveryReview>().Where(x => x.编号 == reviewNo).ExecuteDeleteAsync();
            var stray = await db.DataChangeLogs
                .Where(x => x.EntityName == "PMCDeliveryReview" && x.EntityId == reviewNo).ToListAsync();
            db.DataChangeLogs.RemoveRange(stray);
            await db.SaveChangesAsync();
        }
    }

    private static ServiceProvider BuildProvider(string conn)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<AppDbContext>((sp, o) =>
            o.UseSqlServer(conn).AddInterceptors(sp.GetRequiredService<DataChangeLogInterceptor>()));
        services.AddSingleton<DataChangeLogInterceptor>();
        return services.BuildServiceProvider();
    }

    [SqlServerFact]
    public async Task Interceptor_CapturesAttachment_WithOwnedBusinessKey_AndExcludesNumberRule()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")!;
        using var provider = BuildProvider(conn);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var att = new Attachment
        {
            BusinessType = "WP05TestBiz",
            BusinessId = "WP05-ATT-1",
            FileName = "t.txt",
            ContentType = "text/plain",
            FileSize = 1,
            SourceType = 0,
            CreatedBy = 0,
            CreateTime = DateTime.Now
        };
        db.Attachments.Add(att);

        var nr = new NumberRule
        {
            RuleCode = "WP05_TEST_RULE_" + Guid.NewGuid().ToString("N")[..8],
            RuleName = "WP05 Test",
            SequenceLength = 5,
            CurrentSequence = 0,
            PeriodType = 0,
            Status = 1,
            CreateTime = DateTime.Now
        };
        db.NumberRules.Add(nr);

        await db.SaveChangesAsync();

        try
        {
            // Attachment 被捕获,且业务键取“所属业务对象”(rev2)
            var attLogs = await db.DataChangeLogs
                .Where(x => x.EntityName == "Attachment" && x.BusinessType == "WP05TestBiz")
                .ToListAsync();
            Assert.NotEmpty(attLogs);
            Assert.All(attLogs, l => Assert.Equal("WP05-ATT-1", l.BusinessId));
            Assert.DoesNotContain(attLogs, l => l.BusinessId == att.Id.ToString());

            // NumberRule 不进入白名单(rev1)→ 不产生 DataChangeLog
            var nrLogs = await db.DataChangeLogs.Where(x => x.EntityName == "NumberRule").ToListAsync();
            Assert.Empty(nrLogs);
        }
        finally
        {
            if (att.Id > 0) db.Attachments.Remove(att);
            if (nr.Id > 0) db.NumberRules.Remove(nr);
            await db.SaveChangesAsync();
            var stray = await db.DataChangeLogs.Where(x => x.BusinessType == "WP05TestBiz").ToListAsync();
            db.DataChangeLogs.RemoveRange(stray);
            await db.SaveChangesAsync();
        }
    }
}
