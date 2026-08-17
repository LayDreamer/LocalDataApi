using System.Collections.Concurrent;
using System.Data;
using System.Text;
using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Platform;

public interface INumberRuleService
{
    /// <summary>按规则编码生成下一个完整编号(并发安全)。</summary>
    Task<string> GetNextCodeAsync(string ruleCode, CancellationToken ct = default);

    /// <summary>获取全部编码规则(管理列表)。</summary>
    Task<List<NumberRuleDto>> GetRulesAsync(CancellationToken ct = default);

    /// <summary>按 Id 获取编码规则。</summary>
    Task<NumberRuleDto> GetRuleAsync(long id, CancellationToken ct = default);

    /// <summary>创建编码规则。</summary>
    Task<NumberRuleDto> CreateRuleAsync(NumberRuleCreateDto dto, CancellationToken ct = default);

    /// <summary>更新编码规则(仅更新传入字段)。</summary>
    Task<NumberRuleDto> UpdateRuleAsync(long id, NumberRuleUpdateDto dto, CancellationToken ct = default);

    /// <summary>手动重置流水号(如跨年人工重开)。</summary>
    Task<NumberRuleDto> ResetSequenceAsync(long id, long startFrom = 0, CancellationToken ct = default);
}

/// <summary>
/// 统一业务编码规则服务。
/// 并发安全: SQL Server 下事务 + WITH (UPDLOCK, ROWLOCK) 行锁串行化取号;
/// 另以按规则的 SemaphoreSlim 做单实例内兜底(测试环境 InMemory 亦可靠)。
/// 日期重置: 取号时惰性判断(按日/按月/按年),不引入定时任务。
/// </summary>
public sealed class NumberRuleService(AppDbContext context) : INumberRuleService
{
    // 按规则编码的信号量,用于单实例内串行化(生产与 UPDLOCK 双保险,测试 InMemory 可靠)
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    // ---------- 核心取号 ----------

    public async Task<string> GetNextCodeAsync(string ruleCode, CancellationToken ct = default)
    {
        var normalized = ruleCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("规则编码不能为空");

        var gate = _gates.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (context.Database.IsSqlServer())
            {
                // SQL Server: 原子 UPDATE + OUTPUT,单语句完成"读-改-写"且周期重置,
                // UPDLOCK+ROWLOCK 行锁天然串行化跨连接并发取号,杜绝 ReadCommitted 下"读-改-写"竞态。
                var now = DateTime.Now;
                const string sql = """
                    UPDATE Sys_NumberRule WITH (UPDLOCK, ROWLOCK)
                    SET
                        CurrentSequence = CASE
                            WHEN (PeriodType = 1 AND (LastResetDate IS NULL OR CAST(LastResetDate AS date) <> CAST({1} AS date)))
                              OR (PeriodType = 2 AND (LastResetDate IS NULL OR DATEPART(year, LastResetDate) <> DATEPART(year, {1}) OR DATEPART(month, LastResetDate) <> DATEPART(month, {1})))
                              OR (PeriodType = 3 AND (LastResetDate IS NULL OR DATEPART(year, LastResetDate) <> DATEPART(year, {1})))
                            THEN 1
                            ELSE CurrentSequence + 1
                        END,
                        LastResetDate = CASE
                            WHEN (PeriodType = 1 AND (LastResetDate IS NULL OR CAST(LastResetDate AS date) <> CAST({1} AS date)))
                              OR (PeriodType = 2 AND (LastResetDate IS NULL OR DATEPART(year, LastResetDate) <> DATEPART(year, {1}) OR DATEPART(month, LastResetDate) <> DATEPART(month, {1})))
                              OR (PeriodType = 3 AND (LastResetDate IS NULL OR DATEPART(year, LastResetDate) <> DATEPART(year, {1})))
                            THEN {1}
                            ELSE LastResetDate
                        END,
                        UpdateTime = {1}
                    OUTPUT inserted.CurrentSequence, inserted.Prefix, inserted.DateFormat, inserted.SequenceLength
                    WHERE RuleCode = {0} AND Status = 1;
                    """;

                var rows = await context.Database
                    .SqlQueryRaw<NumberRuleSeqResult>(sql, normalized, now)
                    .ToListAsync(ct);

                if (rows.Count == 0)
                {
                    // 规则不存在或已停用,做一次只读判断以保留与原实现相同的异常语义
                    var status = await context.NumberRules.AsNoTracking()
                        .Where(r => r.RuleCode == normalized)
                        .Select(r => (byte?)r.Status)
                        .SingleOrDefaultAsync(ct);
                    if (status is null)
                        throw new NotFoundException($"编码规则不存在: {normalized}");
                    throw new ConflictException($"编码规则已停用: {normalized}");
                }

                return BuildCodeFromSeq(rows[0], now);
            }
            else
            {
                // 测试环境(InMemory): 不支持原生 SQL,由 SemaphoreSlim 保证单实例串行,读-改-写安全
                var rule = await context.NumberRules
                    .FirstOrDefaultAsync(r => r.RuleCode == normalized, ct)
                    ?? throw new NotFoundException($"编码规则不存在: {normalized}");

                if (rule.Status != 1)
                    throw new ConflictException($"编码规则已停用: {normalized}");

                ApplyPeriodReset(rule, DateTime.Now);
                rule.CurrentSequence += 1;
                rule.UpdateTime = DateTime.Now;
                await context.SaveChangesAsync(ct);

                return BuildCode(rule);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>原子 UPDATE+OUTPUT 返回的最小投影,EF Core SqlQueryRaw 按列名绑定。</summary>
    private sealed class NumberRuleSeqResult
    {
        public long CurrentSequence { get; set; }
        public string? Prefix { get; set; }
        public string? DateFormat { get; set; }
        public int SequenceLength { get; set; }
    }

    private static string BuildCodeFromSeq(NumberRuleSeqResult r, DateTime now)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(r.Prefix)) sb.Append(r.Prefix);
        if (!string.IsNullOrWhiteSpace(r.DateFormat)) sb.Append(now.ToString(r.DateFormat));
        sb.Append(r.CurrentSequence.ToString().PadLeft(r.SequenceLength, '0'));
        return sb.ToString();
    }

    // ---------- 规则管理 ----------

    public async Task<List<NumberRuleDto>> GetRulesAsync(CancellationToken ct = default)
    {
        return await context.NumberRules.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => ToDto(r))
            .ToListAsync(ct);
    }

    public async Task<NumberRuleDto> GetRuleAsync(long id, CancellationToken ct = default)
    {
        var rule = await context.NumberRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("编码规则不存在");
        return ToDto(rule);
    }

    public async Task<NumberRuleDto> CreateRuleAsync(NumberRuleCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RuleCode)) throw new ValidationException("规则编码不能为空");
        if (string.IsNullOrWhiteSpace(dto.RuleName)) throw new ValidationException("规则名称不能为空");
        if (!string.IsNullOrWhiteSpace(dto.DateFormat)) ValidateDateFormat(dto.DateFormat);

        var code = dto.RuleCode.Trim();
        var exists = await context.NumberRules.AnyAsync(r => r.RuleCode == code, ct);
        if (exists) throw new ConflictException($"规则编码已存在: {code}");

        var entity = new NumberRule
        {
            RuleCode = code,
            RuleName = dto.RuleName.Trim(),
            Prefix = string.IsNullOrWhiteSpace(dto.Prefix) ? null : dto.Prefix.Trim(),
            DateFormat = string.IsNullOrWhiteSpace(dto.DateFormat) ? null : dto.DateFormat.Trim(),
            SequenceLength = dto.SequenceLength <= 0 ? 5 : dto.SequenceLength,
            CurrentSequence = 0,
            PeriodType = dto.PeriodType is >= 0 and <= 3 ? dto.PeriodType : 0,
            LastResetDate = null,
            Status = 1,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CreateTime = DateTime.Now
        };
        context.NumberRules.Add(entity);
        await context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<NumberRuleDto> UpdateRuleAsync(long id, NumberRuleUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await context.NumberRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("编码规则不存在");

        if (dto.RuleName is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.RuleName)) throw new ValidationException("规则名称不能为空");
            entity.RuleName = dto.RuleName.Trim();
        }
        if (dto.Prefix is not null) entity.Prefix = string.IsNullOrWhiteSpace(dto.Prefix) ? null : dto.Prefix.Trim();
        if (dto.DateFormat is not null)
        {
            if (!string.IsNullOrWhiteSpace(dto.DateFormat)) ValidateDateFormat(dto.DateFormat);
            entity.DateFormat = string.IsNullOrWhiteSpace(dto.DateFormat) ? null : dto.DateFormat.Trim();
        }
        if (dto.SequenceLength.HasValue) entity.SequenceLength = dto.SequenceLength.Value <= 0 ? 5 : dto.SequenceLength.Value;
        if (dto.PeriodType.HasValue) entity.PeriodType = dto.PeriodType.Value is >= 0 and <= 3 ? dto.PeriodType.Value : entity.PeriodType;
        if (dto.Status.HasValue) entity.Status = dto.Status.Value is 0 or 1 ? dto.Status.Value : entity.Status;
        if (dto.Description is not null) entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.UpdateTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<NumberRuleDto> ResetSequenceAsync(long id, long startFrom = 0, CancellationToken ct = default)
    {
        var entity = await context.NumberRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("编码规则不存在");
        entity.CurrentSequence = Math.Max(0, startFrom);
        entity.LastResetDate = DateTime.Now;
        entity.UpdateTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    // ---------- 私有 ----------

    /// <summary>惰性周期重置:跨周期则流水号归零并更新重置日期。</summary>
    private static void ApplyPeriodReset(NumberRule rule, DateTime now)
    {
        if (rule.PeriodType <= 0 || rule.PeriodType > 3) return; // 不重置

        var last = rule.LastResetDate;
        bool shouldReset = last is null;
        if (!shouldReset)
        {
            shouldReset = rule.PeriodType switch
            {
                1 => last.Value.Date != now.Date,                       // 按日
                2 => last.Value.Year != now.Year || last.Value.Month != now.Month, // 按月
                3 => last.Value.Year != now.Year,                       // 按年
                _ => false
            };
        }

        if (shouldReset)
        {
            rule.CurrentSequence = 0;
            rule.LastResetDate = now;
        }
    }

    /// <summary>校验日期格式合法(用当前时间试算,非法格式符会抛 FormatException)。</summary>
    private static void ValidateDateFormat(string format)
    {
        try
        {
            _ = DateTime.Now.ToString(format);
        }
        catch (FormatException)
        {
            throw new ValidationException($"日期格式不合法: {format}");
        }
    }

    /// <summary>
    /// 生成完整编号: {Prefix}{Date(DateFormat非空时)}{Sequence补零}。
    /// 扩位行为: 流水号超过 SequenceLength 时自动扩位(不截断,不拒绝),保证编号永不重复。
    /// </summary>
    private static string BuildCode(NumberRule rule)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(rule.Prefix)) sb.Append(rule.Prefix);
        if (!string.IsNullOrWhiteSpace(rule.DateFormat))
            sb.Append(DateTime.Now.ToString(rule.DateFormat));
        sb.Append(rule.CurrentSequence.ToString().PadLeft(rule.SequenceLength, '0'));
        return sb.ToString();
    }

    private static NumberRuleDto ToDto(NumberRule r) => new()
    {
        Id = r.Id,
        RuleCode = r.RuleCode,
        RuleName = r.RuleName,
        Prefix = r.Prefix,
        DateFormat = r.DateFormat,
        SequenceLength = r.SequenceLength,
        CurrentSequence = r.CurrentSequence,
        PeriodType = r.PeriodType,
        LastResetDate = r.LastResetDate,
        Status = r.Status,
        Description = r.Description,
        CreateTime = r.CreateTime,
        UpdateTime = r.UpdateTime
    };
}
