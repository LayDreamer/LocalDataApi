using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public interface IPositionService
{
    Task<List<PositionDto>> GetPositionsAsync(CancellationToken ct = default);
    Task<PositionDto> CreatePositionAsync(PositionCreateDto dto, string? operatorId = null, CancellationToken ct = default);
    Task<PositionDto> UpdatePositionAsync(long id, PositionUpdateDto dto, string? operatorId = null, CancellationToken ct = default);
    Task DisablePositionAsync(long id, string? operatorId = null, CancellationToken ct = default);
}

public sealed class PositionService(AppDbContext context, IAuditLogService auditLog) : IPositionService
{
    public Task<List<PositionDto>> GetPositionsAsync(CancellationToken ct = default) => context.Positions.AsNoTracking()
        .OrderBy(position => position.Code)
        .Select(position => ToDto(position))
        .ToListAsync(ct);

    public async Task<PositionDto> CreatePositionAsync(PositionCreateDto dto, string? operatorId = null, CancellationToken ct = default)
    {
        var code = NormalizeRequired(dto.Code, "岗位编码");
        var name = NormalizeRequired(dto.Name, "岗位名称");
        await EnsureCodeUniqueAsync(code, null, ct);

        var now = DateTime.Now;
        var position = new Position
        {
            Code = code,
            Name = name,
            Description = TrimOrNull(dto.Description),
            IsActive = dto.IsActive,
            CreatedTime = now,
            UpdatedTime = now
        };
        context.Positions.Add(position);
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "Position.Create", position, ct);
        return ToDto(position);
    }

    public async Task<PositionDto> UpdatePositionAsync(long id, PositionUpdateDto dto, string? operatorId = null, CancellationToken ct = default)
    {
        var position = await context.Positions.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("岗位不存在");
        var wasActive = position.IsActive;

        if (dto.Code is not null)
        {
            var code = NormalizeRequired(dto.Code, "岗位编码");
            await EnsureCodeUniqueAsync(code, id, ct);
            position.Code = code;
        }
        if (dto.Name is not null) position.Name = NormalizeRequired(dto.Name, "岗位名称");
        if (dto.Description is not null) position.Description = TrimOrNull(dto.Description);
        if (dto.IsActive.HasValue) position.IsActive = dto.IsActive.Value;
        position.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, wasActive && !position.IsActive ? "Position.Disable" : "Position.Update", position, ct);
        return ToDto(position);
    }

    public async Task DisablePositionAsync(long id, string? operatorId = null, CancellationToken ct = default)
    {
        var position = await context.Positions.FirstOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("岗位不存在");
        if (!position.IsActive) return;

        position.IsActive = false;
        position.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorId, "Position.Disable", position, ct);
    }

    private async Task EnsureCodeUniqueAsync(string code, long? currentId, CancellationToken ct)
    {
        var exists = await context.Positions.AsNoTracking()
            .AnyAsync(position => position.Code == code && (!currentId.HasValue || position.Id != currentId.Value), ct);
        if (exists) throw new ConflictException("岗位编码已存在");
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{fieldName}不能为空");
        return value.Trim();
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PositionDto ToDto(Position position) => new()
    {
        Id = position.Id, Code = position.Code, Name = position.Name, Description = position.Description,
        IsActive = position.IsActive, CreatedTime = position.CreatedTime, UpdatedTime = position.UpdatedTime
    };

    private async Task TryAuditAsync(string? operatorId, string action, Position position, CancellationToken ct)
    {
        try { await auditLog.LogAsync(operatorId, action, "Position", position.Id.ToString(), new { position.Code, position.Name, position.IsActive }, ct); } catch { }
    }
}
