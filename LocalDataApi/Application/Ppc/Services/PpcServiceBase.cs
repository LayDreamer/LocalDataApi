using LocalDataApi.Infrastructure.Data;

namespace LocalDataApi.Application.Ppc.Services;

/// <summary>
/// PMC 域应用服务基类:共享 DbContext 与行版本(乐观并发)处理逻辑。
/// </summary>
public abstract class PpcServiceBase
{
    protected readonly AppDbContext _context;

    protected PpcServiceBase(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 应用客户端行版本(乐观并发):客户端传入 RowVersion 时,将其设为实体的 OriginalValue,
    /// 使 EF Core 在 SaveChanges 时以该版本做并发校验。
    /// </summary>
    protected void ApplyClientRowVersion<TEntity>(TEntity trackedEntity, byte[]? clientRowVersion)
        where TEntity : class
    {
        if (clientRowVersion is { Length: > 0 })
        {
            _context.Entry(trackedEntity).Property("RowVersion").OriginalValue = clientRowVersion;
        }
    }

    /// <summary>将字符串安全解析为 double(空或无法解析时返回 0)</summary>
    protected static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0d;
        return double.TryParse(value, out var d) ? d : 0d;
    }
}
