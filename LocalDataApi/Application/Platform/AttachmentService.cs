using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LocalDataApi.Application.Platform;

public interface IAttachmentService
{
    /// <summary>本地上传(SourceType=0): 先落盘再写元数据;数据库保存失败时补偿删除已落盘文件。</summary>
    Task<AttachmentDto> UploadAsync(string businessType, string businessId, string fileName, string contentType, Stream content, CancellationToken ct = default);

    /// <summary>按业务对象查询附件列表。</summary>
    Task<List<AttachmentDto>> GetByBusinessAsync(string businessType, string businessId, CancellationToken ct = default);

    /// <summary>按 Id 查询附件元数据。</summary>
    Task<AttachmentDto> GetAsync(long id, CancellationToken ct = default);

    /// <summary>打开下载/预览: 本地附件返回文件流,外部引用返回受控 ExternalUrl。</summary>
    Task<AttachmentDownloadResult> OpenForDownloadAsync(long id, CancellationToken ct = default);

    /// <summary>删除附件记录,并尽力同步删除物理文件(SourceType=0)。</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 外部来源附件创建(SourceType=1,仅受信任后端 Adapter/内部 Service 调用)。
    /// 公共 Upload API 不接受本方法对应参数,防 SSRF/开放重定向。
    /// </summary>
    Task<AttachmentDto> CreateExternalAsync(ExternalAttachmentCreateDto dto, CancellationToken ct = default);
}

/// <summary>
/// 统一附件服务: 元数据(Sys_Attachment) + 存储编排(IFileStorage) + 外部引用透传。
/// 上传编排: 先落盘 → 写元数据 → 失败则补偿删除物理文件,避免"文件已落盘、数据库无记录"的永久孤儿。
/// </summary>
public sealed class AttachmentService(
    AppDbContext context,
    IFileStorage storage,
    CurrentUserService currentUser,
    IConfiguration configuration,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    private const long DefaultMaxSizeBytes = 20L * 1024 * 1024; // 20MB

    public async Task<AttachmentDto> UploadAsync(string businessType, string businessId, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(businessType)) throw new ValidationException("业务类型不能为空");
        if (string.IsNullOrWhiteSpace(businessId)) throw new ValidationException("业务对象标识不能为空");
        if (string.IsNullOrWhiteSpace(fileName)) throw new ValidationException("文件名不能为空");
        if (content is null) throw new ValidationException("文件内容不能为空");

        var maxSize = configuration.GetValue<long?>("AttachmentStorage:MaxSizeBytes") ?? DefaultMaxSizeBytes;
        var fileSize = content.CanSeek ? content.Length : 0L;
        if (fileSize > maxSize)
            throw new ValidationException($"文件大小超过上限: {fileSize} 字节(允许 {maxSize} 字节)");

        // 1. 先落盘(白名单扩展名校验在存储层)
        var storageKey = await storage.SaveAsync(content, fileName, contentType, ct);

        // 2. 写元数据;失败则补偿删除已落盘文件,并重新抛出原异常
        try
        {
            var entity = new Attachment
            {
                BusinessType = businessType.Trim(),
                BusinessId = businessId.Trim(),
                FileName = fileName.Trim(),
                Extension = NormalizeExtension(fileName),
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
                FileSize = fileSize,
                SourceType = 0,
                StorageKey = storageKey,
                CreatedBy = currentUser.UserId ?? 0,
                CreateTime = DateTime.Now
            };
            context.Attachments.Add(entity);
            await context.SaveChangesAsync(ct);
            return ToDto(entity);
        }
        catch
        {
            try
            {
                await storage.DeleteAsync(storageKey, ct);
                logger.LogWarning("上传补偿: 数据库保存失败,已删除已落盘文件 {StorageKey}", storageKey);
            }
            catch (Exception ex)
            {
                // 补偿失败只记录错误,不覆盖原始数据库异常
                logger.LogError(ex, "上传补偿删除失败(记录未持久化): {StorageKey}", storageKey);
            }
            throw;
        }
    }

    public async Task<List<AttachmentDto>> GetByBusinessAsync(string businessType, string businessId, CancellationToken ct = default)
    {
        return await context.Attachments.AsNoTracking()
            .Where(a => a.BusinessType == businessType && a.BusinessId == businessId)
            .OrderByDescending(a => a.Id)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    public async Task<AttachmentDto> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await context.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("附件不存在");
        return ToDto(entity);
    }

    public async Task<AttachmentDownloadResult> OpenForDownloadAsync(long id, CancellationToken ct = default)
    {
        var entity = await context.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("附件不存在");

        if (entity.SourceType == 1)
        {
            if (string.IsNullOrWhiteSpace(entity.ExternalUrl))
                throw new NotFoundException("外部附件引用地址缺失");
            return new AttachmentDownloadResult(ToDto(entity), null, entity.ExternalUrl);
        }

        if (string.IsNullOrWhiteSpace(entity.StorageKey))
            throw new NotFoundException("附件存储键缺失");
        var stream = await storage.OpenAsync(entity.StorageKey, ct);
        return new AttachmentDownloadResult(ToDto(entity), stream, null);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await context.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("附件不存在");

        context.Attachments.Remove(entity);
        await context.SaveChangesAsync(ct);

        // 记录删除成功后尽力删除物理文件(SourceType=0);失败仅记日志,不阻塞
        if (entity.SourceType == 0 && !string.IsNullOrWhiteSpace(entity.StorageKey))
        {
            try
            {
                await storage.DeleteAsync(entity.StorageKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除附件物理文件失败(记录已删除,可人工清理): {StorageKey}", entity.StorageKey);
            }
        }
    }

    public async Task<AttachmentDto> CreateExternalAsync(ExternalAttachmentCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.BusinessType)) throw new ValidationException("业务类型不能为空");
        if (string.IsNullOrWhiteSpace(dto.BusinessId)) throw new ValidationException("业务对象标识不能为空");
        if (string.IsNullOrWhiteSpace(dto.FileName)) throw new ValidationException("文件名不能为空");
        if (string.IsNullOrWhiteSpace(dto.ExternalUrl)) throw new ValidationException("外部引用地址不能为空");

        var entity = new Attachment
        {
            BusinessType = dto.BusinessType.Trim(),
            BusinessId = dto.BusinessId.Trim(),
            FileName = dto.FileName.Trim(),
            Extension = NormalizeExtension(dto.FileName),
            ContentType = string.IsNullOrWhiteSpace(dto.ContentType) ? "application/octet-stream" : dto.ContentType.Trim(),
            FileSize = Math.Max(0, dto.FileSize),
            SourceType = 1,
            ExternalUrl = dto.ExternalUrl.Trim(),
            Remark = string.IsNullOrWhiteSpace(dto.Remark) ? null : dto.Remark.Trim(),
            CreatedBy = currentUser.UserId ?? 0,
            CreateTime = DateTime.Now
        };
        context.Attachments.Add(entity);
        await context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static string? NormalizeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(ext) ? null : ext.TrimStart('.').ToLowerInvariant();
    }

    private static AttachmentDto ToDto(Attachment a) => new()
    {
        Id = a.Id,
        BusinessType = a.BusinessType,
        BusinessId = a.BusinessId,
        FileName = a.FileName,
        Extension = a.Extension,
        ContentType = a.ContentType,
        FileSize = a.FileSize,
        SourceType = a.SourceType,
        StorageKey = a.StorageKey,
        ExternalUrl = a.ExternalUrl,
        Remark = a.Remark,
        CreatedBy = a.CreatedBy,
        CreateTime = a.CreateTime
    };
}
