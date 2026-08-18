using System.Globalization;
using LocalDataApi.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalDataApi.Application.Platform;

/// <summary>
/// 本地磁盘文件存储实现(AttachmentStorage:Provider=Local)。
/// 安全决策(WP03 §11 修订冻结版):
/// - 受控附件严禁存放在 wwwroot 或任何 Web 静态资源目录下,根目录由 AttachmentStorage:RootPath 指定
///   (默认 ./Data/Attachments,相对进程工作目录;生产配置为 Web 目录外的绝对路径)。
/// - StorageKey 仅保存相对路径(如 2026/08/18/{guid}.pdf),物理路径运行时用 Path.Combine(RootPath, StorageKey) 拼装。
/// - 命名: {yyyy}/{MM}/{dd}/{Guid:N}{ext},日期分目录 + GUID 并发唯一、防猜测。
/// - 安全: 扩展名白名单(AttachmentStorage:AllowedExtensions) + 存储键路径遍历校验(IsValidKey)。
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    public const string DefaultAllowedExtensions =
        ".jpg .jpeg .png .gif .pdf .xls .xlsx .doc .docx .csv .zip";

    /// <summary>
    /// 扩展名 → 允许的 Content-Type 映射(MIME 白名单)。
    /// 目的: 防止「.pdf 配 text/html / image/svg+xml」等不匹配组合——下载时若按扩展名 inline 而响应头是可执行/脚本化
    /// MIME,会造成 XSS。application/octet-stream 为通用二进制占位,下载时浏览器强制附件下载(不内联),故放行。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedContentTypesByExtension =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [".jpg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
            [".jpeg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
            [".png"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/png" },
            [".gif"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/gif" },
            [".pdf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf" },
            [".xls"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.ms-excel" },
            [".xlsx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            [".doc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/msword" },
            [".docx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            [".csv"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text/csv" },
            [".zip"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/zip", "application/x-zip-compressed" }
        };

    private readonly string _rootPath;
    private readonly IReadOnlySet<string> _allowedExtensions;
    private readonly ILogger<LocalFileStorage> _logger;

    /// <summary>存储根目录(规范化后的物理路径,服务端受控;StorageKey 与之拼接得到完整路径)。</summary>
    public string RootPath => _rootPath;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        var root = configuration["AttachmentStorage:RootPath"];
        _rootPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(root) ? Path.Combine(Directory.GetCurrentDirectory(), "Data", "Attachments") : root);

        var configured = configuration["AttachmentStorage:AllowedExtensions"];
        _allowedExtensions = ParseExtensions(configured);
        _logger = logger;

        Directory.CreateDirectory(_rootPath);
        _logger.LogInformation("LocalFileStorage 就绪: RootPath={RootPath}, 允许扩展名=[{Extensions}]", _rootPath, string.Join(' ', _allowedExtensions));
    }

    /// <summary>测试构造:显式根目录与白名单。</summary>
    internal LocalFileStorage(string rootPath, string? allowedExtensions = null, ILogger<LocalFileStorage>? logger = null)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _allowedExtensions = ParseExtensions(allowedExtensions);
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalFileStorage>();
        Directory.CreateDirectory(_rootPath);
    }

    private static IReadOnlySet<string> ParseExtensions(string? configured)
    {
        var source = string.IsNullOrWhiteSpace(configured) ? DefaultAllowedExtensions : configured;
        return source
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (content is null) throw new ValidationException("文件内容不能为空");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
        {
            throw new ValidationException($"不支持的文件类型: {extension}(允许: {string.Join(' ', _allowedExtensions)})");
        }

        // MIME 白名单: 已知 Content-Type 必须与扩展名匹配(防 .pdf 配 text/html 等 XSS 组合)
        var normalizedContentType = NormalizeContentType(contentType);
        if (normalizedContentType != "application/octet-stream"
            && AllowedContentTypesByExtension.TryGetValue(extension, out var allowedTypes)
            && !allowedTypes.Contains(normalizedContentType))
        {
            throw new ValidationException($"Content-Type 与文件扩展名不匹配: {extension} / {normalizedContentType}");
        }

        // 使用 InvariantCulture 并转义 / 为字面量,避免日期分隔符被 culture 替换(如 2026-08-18)
        var storageKey = $"{DateTime.Now.ToString("yyyy\\/MM\\/dd", CultureInfo.InvariantCulture)}/{Guid.NewGuid():N}{extension}";
        var fullPath = ToFullPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
        }
        return storageKey;
    }

    /// <summary>规范化 Content-Type: 剥离参数(charset 等)、去空白、转小写;空值视为 application/octet-stream。</summary>
    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return "application/octet-stream";
        return contentType.Split(';')[0].Trim().ToLowerInvariant();
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        if (!IsValidKey(storageKey))
            throw new ValidationException($"非法的存储键: {storageKey}");

        var fullPath = ToFullPath(storageKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("存储对象不存在", storageKey);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        // 空键/不存在对象视为成功(幂等)
        if (string.IsNullOrWhiteSpace(storageKey))
            return Task.CompletedTask;
        if (!IsValidKey(storageKey))
            throw new ValidationException($"非法的存储键: {storageKey}");

        var fullPath = ToFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("已删除存储对象: {StorageKey}", storageKey);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 存储键合法性校验(路径遍历/越界防护)。
    /// 拒绝: 空值、以 / 或 \ 开头、绝对路径、含 .. 段。
    /// 将根目录与目标路径分别 GetFullPath 规范化后,用 GetRelativePath 判断目标是否仍位于根目录内
    /// (拒绝 .. 开头、绝对路径或越界结果),避免简单 StartsWith 产生 attachments_backup 一类前缀误判。
    /// </summary>
    public bool IsValidKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return false;
        if (storageKey.StartsWith('/') || storageKey.StartsWith('\\')) return false;
        if (Path.IsPathRooted(storageKey)) return false;
        if (storageKey.Split('/', '\\').Any(segment => segment == "..")) return false;

        var rootFull = Path.GetFullPath(_rootPath);
        var targetFull = Path.GetFullPath(Path.Combine(rootFull, storageKey));
        var relative = Path.GetRelativePath(rootFull, targetFull);
        return !relative.StartsWith("..") && !Path.IsPathRooted(relative);
    }

    private string ToFullPath(string storageKey)
    {
        if (!IsValidKey(storageKey))
            throw new ValidationException($"非法的存储键: {storageKey}");
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_rootPath, normalized);
    }
}
