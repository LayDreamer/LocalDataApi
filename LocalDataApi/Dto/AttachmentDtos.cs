namespace LocalDataApi.Dto;

/// <summary>统一附件传输模型(列表/详情/上传返回)。</summary>
public class AttachmentDto
{
    public long Id { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte SourceType { get; set; }
    public string? StorageKey { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 外部来源附件创建请求(SourceType=1)。
/// 仅受信任的后端 Adapter / 内部 Service 调用;公共 Upload API 不接受本模型,防 SSRF/开放重定向。
/// </summary>
public class ExternalAttachmentCreateDto
{
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

/// <summary>下载/预览打开结果:本地附件携带文件流,外部引用携带受控 ExternalUrl。</summary>
public sealed record AttachmentDownloadResult(AttachmentDto Attachment, Stream? Stream, string? ExternalUrl)
{
    public bool IsExternal => ExternalUrl is not null;
}
