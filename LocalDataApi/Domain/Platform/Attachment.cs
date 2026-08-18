namespace LocalDataApi.Domain.Platform;

/// <summary>
/// 统一附件(表 Sys_Attachment)。
/// 通用附件表 + 多态关联(BusinessType + BusinessId),业务表不冗余文件字段。
/// SourceType: 0=本地上传存储(经 IFileStorage 落盘); 1=外部引用(企微 file_url 等,仅受信任后端写入,不落盘)。
/// StorageKey 仅保存相对路径(如 2026/08/18/{guid}.pdf),绝对禁止物理绝对路径(见 WP03 §11 安全决策)。
/// </summary>
public sealed class Attachment
{
    /// <summary>主键(bigint 自增)</summary>
    public long Id { get; set; }

    /// <summary>业务对象类型(如 DeliveryReview / WorkOrder,与业务模块编码一致)</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象主键(字符串化,兼容 long/string 主键)</summary>
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>原始文件名(含扩展名,下载时使用)</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>扩展名(小写去点,如 pdf)</summary>
    public string? Extension { get; set; }

    /// <summary>MIME 类型(如 application/pdf)</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>字节数</summary>
    public long FileSize { get; set; }

    /// <summary>来源类型: 0=本地上传存储, 1=外部引用(企微 file_url 等)</summary>
    public byte SourceType { get; set; }

    /// <summary>SourceType=0: IFileStorage 相对存储键(如 2026/08/18/{guid}.pdf,不携带根目录)</summary>
    public string? StorageKey { get; set; }

    /// <summary>SourceType=1: 外部文件引用地址(仅受信任后端写入)</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>上传人用户 Id</summary>
    public long CreatedBy { get; set; }

    /// <summary>上传时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
