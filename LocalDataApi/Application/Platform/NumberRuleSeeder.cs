using LocalDataApi.Domain.Platform;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalDataApi.Application.Platform;

/// <summary>
/// 编码规则默认数据初始化(启动时执行,幂等)。
/// 职责边界: 仅播种业务默认编码规则(如交期评审单号);
/// 权限码与菜单初始化保留在 RbacSeeder,避免平台权限 Seeder 持续吸收业务初始化逻辑。
/// </summary>
public sealed class NumberRuleSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<NumberRuleSeeder> _logger;

    public NumberRuleSeeder(AppDbContext context, ILogger<NumberRuleSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            var expected = new (string Code, string Name, string? Prefix, string? DateFormat, int SeqLen, int Period, string? Desc)[]
            {
                ("DeliveryReview", "交期评审单号", "DR-", "yyyyMMdd", 5, 0, "交期评审(外产_订单)新增时自动生成,格式 DR-日期+5位流水,不重置")
            };
            var existingCodes = await _context.NumberRules.AsNoTracking()
                .Select(r => r.RuleCode).ToHashSetAsync(ct);

            var now = DateTime.Now;
            foreach (var def in expected.Where(def => !existingCodes.Contains(def.Code)))
            {
                _context.NumberRules.Add(new NumberRule
                {
                    RuleCode = def.Code,
                    RuleName = def.Name,
                    Prefix = def.Prefix,
                    DateFormat = def.DateFormat,
                    SequenceLength = def.SeqLen,
                    CurrentSequence = 0,
                    PeriodType = def.Period,
                    LastResetDate = null,
                    Status = 1,
                    Description = def.Desc,
                    CreateTime = now
                });
            }
            if (expected.Any(def => !existingCodes.Contains(def.Code)))
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("默认编码规则初始化完成。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "默认编码规则初始化失败: {Message}", ex.Message);
        }
    }
}
