using LocalDataApi.Application.Common;
using LocalDataApi.Application.Platform;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LocalDataApi.Tests.Platform;

/// <summary>
/// 统一编码规则服务测试:取号格式 / 并发唯一 / 日期重置 / 停用拦截 / CRUD。
/// 注:测试环境 InMemory provider 不支持 UPDLOCK,并发串行由服务内按规则的 SemaphoreSlim 保证;
/// SQL Server 生产路径的并发安全由 WITH (UPDLOCK, ROWLOCK) 行锁保证(见 NumberRuleService 实现)。
/// </summary>
public sealed class NumberRuleServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"number-rule-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static NumberRuleService CreateService(AppDbContext db) => new(db);

    private static async Task<NumberRuleService> CreateSeededAsync(long seq = 0)
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
            CurrentSequence = seq,
            PeriodType = 0,
            Status = 1,
            CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        return CreateService(db);
    }

    // ---------- 取号格式与自增 ----------

    [Fact]
    public async Task Generate_ReturnsFormattedCode_WithPrefixDateAndPaddedSequence()
    {
        var service = await CreateSeededAsync();
        var code = await service.GetNextCodeAsync("DeliveryReview");
        Assert.Equal($"DR-{DateTime.Now:yyyyMMdd}00001", code);
    }

    [Fact]
    public async Task Generate_IncrementsSequence()
    {
        var service = await CreateSeededAsync();
        var c1 = await service.GetNextCodeAsync("DeliveryReview");
        var c2 = await service.GetNextCodeAsync("DeliveryReview");
        var c3 = await service.GetNextCodeAsync("DeliveryReview");
        Assert.Equal($"DR-{DateTime.Now:yyyyMMdd}00001", c1);
        Assert.Equal($"DR-{DateTime.Now:yyyyMMdd}00002", c2);
        Assert.Equal($"DR-{DateTime.Now:yyyyMMdd}00003", c3);
    }

    [Fact]
    public async Task Generate_NoDateSegment_WhenDateFormatEmpty()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Plain", RuleName = "无日期", Prefix = "WO-", DateFormat = null,
            SequenceLength = 4, CurrentSequence = 0, PeriodType = 0, Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("Plain");
        Assert.Equal("WO-0001", code);
    }

    // ---------- 日期周期重置 ----------

    [Fact]
    public async Task Generate_DailyPeriod_ResetsOnNewDay()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Daily", RuleName = "按日", Prefix = "D-", DateFormat = "yyyyMMdd",
            SequenceLength = 5, CurrentSequence = 99, PeriodType = 1,
            LastResetDate = DateTime.Now.AddDays(-1), Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("Daily");
        Assert.Equal($"D-{DateTime.Now:yyyyMMdd}00001", code); // 跨日归零后取 1
    }

    [Fact]
    public async Task Generate_DailyPeriod_SameDayKeepsSequence()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Daily2", RuleName = "按日", Prefix = "D-", DateFormat = "yyyyMMdd",
            SequenceLength = 5, CurrentSequence = 5, PeriodType = 1,
            LastResetDate = DateTime.Now, Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("Daily2");
        Assert.Equal($"D-{DateTime.Now:yyyyMMdd}00006", code); // 同日不重置
    }

    [Fact]
    public async Task Generate_MonthlyPeriod_ResetsOnNewMonth()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Monthly", RuleName = "按月", Prefix = "M-", DateFormat = "yyyyMM",
            SequenceLength = 4, CurrentSequence = 42, PeriodType = 2,
            LastResetDate = DateTime.Now.AddMonths(-1), Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("Monthly");
        Assert.Equal($"M-{DateTime.Now:yyyyMM}0001", code);
    }

    [Fact]
    public async Task Generate_YearlyPeriod_ResetsOnNewYear()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Yearly", RuleName = "按年", Prefix = "Y-", DateFormat = "yyyy",
            SequenceLength = 4, CurrentSequence = 365, PeriodType = 3,
            LastResetDate = DateTime.Now.AddYears(-1), Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("Yearly");
        Assert.Equal($"Y-{DateTime.Now:yyyy}0001", code);
    }

    [Fact]
    public async Task Generate_NoPeriod_IgnoresOldLastReset()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "NoReset", RuleName = "不重置", Prefix = "N-", DateFormat = null,
            SequenceLength = 5, CurrentSequence = 10, PeriodType = 0,
            LastResetDate = DateTime.Now.AddYears(-2), Status = 1, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var code = await service.GetNextCodeAsync("NoReset");
        Assert.Equal("N-00011", code); // 不重置,继续自增
    }

    // ---------- 停用 / 不存在 ----------

    [Fact]
    public async Task Generate_DisabledRule_ThrowsConflict()
    {
        var db = CreateDb();
        db.NumberRules.Add(new NumberRule
        {
            Id = 1, RuleCode = "Disabled", RuleName = "停用", Prefix = "X-", DateFormat = null,
            SequenceLength = 5, CurrentSequence = 0, PeriodType = 0, Status = 0, CreateTime = DateTime.Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await Assert.ThrowsAsync<ConflictException>(() => service.GetNextCodeAsync("Disabled"));
    }

    [Fact]
    public async Task Generate_UnknownRule_ThrowsNotFound()
    {
        var service = await CreateSeededAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetNextCodeAsync("NotExists"));
    }

    [Fact]
    public async Task Generate_EmptyCode_ThrowsValidation()
    {
        var service = await CreateSeededAsync();
        await Assert.ThrowsAsync<ValidationException>(() => service.GetNextCodeAsync("  "));
    }

    // ---------- 并发唯一性 ----------

    [Fact]
    public async Task Generate_ConcurrentCalls_AllCodesUnique()
    {
        var service = await CreateSeededAsync();
        const int total = 30;
        var tasks = Enumerable.Range(0, total).Select(_ => service.GetNextCodeAsync("DeliveryReview"));
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(total, codes.Distinct().Count());
    }

    // ---------- CRUD ----------

    [Fact]
    public async Task CreateRule_ThenList_ContainsNewRule()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var created = await service.CreateRuleAsync(new NumberRuleCreateDto
        {
            RuleCode = "SalesOrder", RuleName = "销售订单号", Prefix = "SO-", DateFormat = "yyyyMMdd",
            SequenceLength = 6, PeriodType = 1, Description = "销售订单"
        });

        Assert.Equal("SalesOrder", created.RuleCode);
        Assert.Equal(0, created.CurrentSequence);

        var list = await service.GetRulesAsync();
        Assert.Contains(list, r => r.RuleCode == "SalesOrder");
    }

    [Fact]
    public async Task CreateRule_DuplicateCode_ThrowsConflict()
    {
        var service = await CreateSeededAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateRuleAsync(new NumberRuleCreateDto
        {
            RuleCode = "DeliveryReview", RuleName = "重复"
        }));
    }

    [Fact]
    public async Task UpdateRule_Disable_ThenGenerateThrows()
    {
        var service = await CreateSeededAsync();

        await service.UpdateRuleAsync(1, new NumberRuleUpdateDto { Status = 0 });

        await Assert.ThrowsAsync<ConflictException>(() => service.GetNextCodeAsync("DeliveryReview"));
    }

    [Fact]
    public async Task ResetSequence_ThenGenerateStartsFromOne()
    {
        var service = await CreateSeededAsync();
        await service.GetNextCodeAsync("DeliveryReview"); // 序列到 1
        await service.ResetSequenceAsync(1, 0);

        var code = await service.GetNextCodeAsync("DeliveryReview");
        Assert.Equal($"DR-{DateTime.Now:yyyyMMdd}00001", code);
    }
}
