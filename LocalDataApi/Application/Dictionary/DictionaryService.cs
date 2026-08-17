using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Dictionary;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LocalDataApi.Application.Dictionary;

public interface IDictionaryService
{
    Task<List<DictionaryTypeDto>> GetTypesAsync(CancellationToken ct = default);
    Task<DictionaryDataDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Dictionary<string, List<DictionaryItemDto>>> GetBatchAsync(IEnumerable<string> codes, CancellationToken ct = default);
    Task<DictionaryTypeDto> CreateTypeAsync(DictionaryTypeCreateDto dto, CancellationToken ct = default);
    Task<DictionaryTypeDto> UpdateTypeAsync(long id, DictionaryTypeUpdateDto dto, CancellationToken ct = default);
    Task DeleteTypeAsync(long id, CancellationToken ct = default);
    Task<DictionaryItemDto> CreateItemAsync(DictionaryItemCreateDto dto, CancellationToken ct = default);
    Task<DictionaryItemDto> UpdateItemAsync(long id, DictionaryItemUpdateDto dto, CancellationToken ct = default);
    Task DeleteItemAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// 数据字典服务。缓存键 Dictionary:{Code},更新/删除时主动失效缓存(执行文档 §六)。
/// 当前使用 IMemoryCache(项目无 Redis 依赖,遵循"不引入无必要框架"原则)。
/// </summary>
public sealed class DictionaryService(AppDbContext context, IMemoryCache cache) : IDictionaryService
{
    private const string CachePrefix = "Dictionary:";

    private static string CacheKey(string code) => $"{CachePrefix}{code}";

    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    // ---------- 查询 ----------

    public async Task<List<DictionaryTypeDto>> GetTypesAsync(CancellationToken ct = default)
    {
        return await context.DictionaryTypes.AsNoTracking()
            .OrderBy(type => type.Sort).ThenBy(type => type.Id)
            .Select(type => new DictionaryTypeDto
            {
                Id = type.Id, Code = type.Code, Name = type.Name,
                Description = type.Description, Status = type.Status, Sort = type.Sort,
                CreateTime = type.CreateTime
            })
            .ToListAsync(ct);
    }

    public async Task<DictionaryDataDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        // 缓存优先
        if (cache.TryGetValue(CacheKey(normalized), out DictionaryDataDto? cached) && cached is not null)
            return cached;

        var type = await context.DictionaryTypes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == normalized, ct);
        if (type is null)
            return null;

        var items = await context.DictionaryItems.AsNoTracking()
            .Where(item => item.DictionaryId == type.Id && item.Status == 1)
            .OrderBy(item => item.Sort).ThenBy(item => item.Id)
            .Select(item => new DictionaryItemDto
            {
                Id = item.Id, DictionaryId = item.DictionaryId,
                Value = item.Value, Label = item.Label, Sort = item.Sort, Status = item.Status
            })
            .ToListAsync(ct);

        var data = new DictionaryDataDto { Id = type.Id, Code = type.Code, Name = type.Name, Items = items };
        cache.Set(CacheKey(normalized), data, new MemoryCacheEntryOptions { SlidingExpiration = SlidingExpiration });
        return data;
    }

    public async Task<Dictionary<string, List<DictionaryItemDto>>> GetBatchAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, List<DictionaryItemDto>>();
        foreach (var code in codes.Distinct())
        {
            var data = await GetByCodeAsync(code, ct);
            if (data is not null)
                result[data.Code] = data.Items;
        }
        return result;
    }

    // ---------- 字典类型 CRUD ----------

    public async Task<DictionaryTypeDto> CreateTypeAsync(DictionaryTypeCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ValidationException("字典编码不能为空");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("字典名称不能为空");

        var exists = await context.DictionaryTypes.AnyAsync(item => item.Code == dto.Code.Trim(), ct);
        if (exists) throw new ConflictException($"字典编码已存在: {dto.Code.Trim()}");

        var entity = new DictionaryType
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Sort = dto.Sort,
            Status = 1,
            CreateTime = DateTime.Now
        };
        context.DictionaryTypes.Add(entity);
        await context.SaveChangesAsync(ct);
        return ToTypeDto(entity);
    }

    public async Task<DictionaryTypeDto> UpdateTypeAsync(long id, DictionaryTypeUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await context.DictionaryTypes.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("字典类型不存在");
        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("字典名称不能为空");
            entity.Name = dto.Name.Trim();
        }
        if (dto.Description is not null) entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (dto.Sort.HasValue) entity.Sort = dto.Sort.Value;
        if (dto.Status.HasValue) entity.Status = dto.Status.Value;
        await context.SaveChangesAsync(ct);
        Invalidate(entity.Code);
        return ToTypeDto(entity);
    }

    public async Task DeleteTypeAsync(long id, CancellationToken ct = default)
    {
        var entity = await context.DictionaryTypes.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("字典类型不存在");
        context.DictionaryTypes.Remove(entity); // 级联删除字典项(配置 Cascade)
        await context.SaveChangesAsync(ct);
        Invalidate(entity.Code);
    }

    // ---------- 字典项 CRUD ----------

    public async Task<DictionaryItemDto> CreateItemAsync(DictionaryItemCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Value)) throw new ValidationException("字典值不能为空");
        if (string.IsNullOrWhiteSpace(dto.Label)) throw new ValidationException("字典显示名不能为空");

        var type = await context.DictionaryTypes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == dto.DictionaryId, ct)
            ?? throw new NotFoundException("字典类型不存在");

        var exists = await context.DictionaryItems.AnyAsync(item => item.DictionaryId == dto.DictionaryId && item.Value == dto.Value.Trim(), ct);
        if (exists) throw new ConflictException($"字典项已存在: {dto.Value.Trim()}");

        var entity = new DictionaryItem
        {
            DictionaryId = dto.DictionaryId,
            Value = dto.Value.Trim(),
            Label = dto.Label.Trim(),
            Sort = dto.Sort,
            Status = 1,
            CreateTime = DateTime.Now
        };
        context.DictionaryItems.Add(entity);
        await context.SaveChangesAsync(ct);
        Invalidate(type.Code);
        return ToItemDto(entity);
    }

    public async Task<DictionaryItemDto> UpdateItemAsync(long id, DictionaryItemUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await context.DictionaryItems.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("字典项不存在");
        if (dto.Label is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Label)) throw new ValidationException("字典显示名不能为空");
            entity.Label = dto.Label.Trim();
        }
        if (dto.Sort.HasValue) entity.Sort = dto.Sort.Value;
        if (dto.Status.HasValue) entity.Status = dto.Status.Value;
        await context.SaveChangesAsync(ct);

        var type = await context.DictionaryTypes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == entity.DictionaryId, ct);
        if (type is not null) Invalidate(type.Code);
        return ToItemDto(entity);
    }

    public async Task DeleteItemAsync(long id, CancellationToken ct = default)
    {
        var entity = await context.DictionaryItems.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("字典项不存在");
        var type = await context.DictionaryTypes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == entity.DictionaryId, ct);
        context.DictionaryItems.Remove(entity);
        await context.SaveChangesAsync(ct);
        if (type is not null) Invalidate(type.Code);
    }

    // ---------- 私有 ----------

    private void Invalidate(string code) => cache.Remove(CacheKey(code));

    private static DictionaryTypeDto ToTypeDto(DictionaryType entity) => new()
    {
        Id = entity.Id, Code = entity.Code, Name = entity.Name,
        Description = entity.Description, Status = entity.Status, Sort = entity.Sort,
        CreateTime = entity.CreateTime
    };

    private static DictionaryItemDto ToItemDto(DictionaryItem entity) => new()
    {
        Id = entity.Id, DictionaryId = entity.DictionaryId,
        Value = entity.Value, Label = entity.Label, Sort = entity.Sort, Status = entity.Status
    };
}
