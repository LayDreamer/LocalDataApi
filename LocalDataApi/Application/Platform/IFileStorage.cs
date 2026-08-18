using LocalDataApi.Application.Common;

namespace LocalDataApi.Application.Platform;

/// <summary>
/// 文件存储抽象。本期唯一实现 <see cref="LocalFileStorage"/>;
/// 未来 MinIO/OSS 作为实现替换点——新增实现类 + 修改 DI 注册即可,业务层(AttachmentService/API)零改动。
/// 边界: 接口不生成对外 URL,下载统一走附件 API(权限内);不做配额/限流/审计。
/// </summary>
public interface IFileStorage
{
    /// <summary>保存文件流,返回存储键(提供者内相对路径,如 2026/08/18/{guid}.pdf)。文件名为原始文件名(用于扩展名推导与白名单校验)。</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>按存储键打开只读流;不存在时抛 FileNotFoundException。</summary>
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);

    /// <summary>删除存储对象;对象不存在视为成功(幂等)。</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>校验存储键是否属于本提供者(路径遍历/越界防护,下载/删除前必检)。</summary>
    bool IsValidKey(string storageKey);
}
