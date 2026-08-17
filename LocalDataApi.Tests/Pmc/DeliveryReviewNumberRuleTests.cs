using LocalDataApi.Application.Common;
using LocalDataApi.Application.Platform;
using LocalDataApi.Application.Pmc.Services;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LocalDataApi.Tests.Pmc;

/// <summary>
/// 交期评审接入 NumberRule 的业务回归测试:
/// 新增生成 DR 编号 / 更新不重新生成 / 规则缺失与停用返回可诊断错误。
/// </summary>
public sealed class DeliveryReviewNumberRuleTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dr-numberrule-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static (PmcDeliveryReviewService Service, AppDbContext Db, NumberRuleService RuleService) CreateWithRule(byte status = 1)
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1,
            RuleCode = "DeliveryReview",
            RuleName = "交期评审单号",
            Prefix = "DR-",
            DateFormat = "yyyyMMdd",
            SequenceLength = 5,
            CurrentSequence = 0,
            PeriodType = 0,
            Status = status,
            CreateTime = DateTime.Now
        });
        db.SaveChanges();
        var ruleService = new NumberRuleService(db);
        var service = new PmcDeliveryReviewService(db, ruleService);
        return (service, db, ruleService);
    }

    private static PMCDeliveryReview NewReview(string contractNo = "HT20260801") => new()
    {
        合同号 = contractNo,
        货号 = "P001",
        状态 = "新建"
    };

    [Fact]
    public async Task Add_NewReview_GeneratesDrRuleCode()
    {
        var (service, _, _) = CreateWithRule();

        var created = await service.AddPMCDeliveryReview(NewReview());

        Assert.NotNull(created.编号);
        Assert.StartsWith("DR-", created.编号);
        Assert.Matches($"^DR-{DateTime.Now:yyyyMMdd}\\d{{5}}$", created.编号);
    }

    [Fact]
    public async Task Add_WithExistingCode_DoesNotRegenerate()
    {
        var (service, db, _) = CreateWithRule();
        var first = await service.AddPMCDeliveryReview(NewReview());
        Assert.StartsWith("DR-", first.编号);

        // 第二次传入同一编号 → 走更新分支,编号必须保持不变
        var second = await service.AddPMCDeliveryReview(new PMCDeliveryReview
        {
            合同号 = "HT20260801",
            货号 = "P001",
            状态 = "已评审",
            编号 = first.编号
        });

        Assert.Equal(first.编号, second.编号);
        Assert.Equal("已评审", second.状态);

        // 只生成过一条记录(未因重复调用新增)
        Assert.Single(db.外产_订单);
    }

    [Fact]
    public async Task Add_MissingRule_ThrowsNotFound_WithDiagnosableMessage()
    {
        var db = CreateDb();
        var service = new PmcDeliveryReviewService(db, new NumberRuleService(db)); // 无规则

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.AddPMCDeliveryReview(NewReview()));

        Assert.Contains("DeliveryReview", ex.Message);
    }

    [Fact]
    public async Task Add_DisabledRule_ThrowsConflict()
    {
        var (service, _, _) = CreateWithRule(status: 0); // 停用

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.AddPMCDeliveryReview(NewReview()));

        Assert.Contains("已停用", ex.Message);
    }
}
