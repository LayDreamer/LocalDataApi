using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Application.Platform;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LocalDataApi.Tests.Platform;

/// <summary>
/// WP04 统一附件存储/服务测试(场景 1-8、10-11)。
/// 覆盖: 上传落盘+元数据 / 下载回显 / 扩展名白名单 / 大小超限 / 路径遍历防护 /
///       删除(记录+物理文件+幂等) / 业务关联查询 / 外部来源(SourceType=1 不落盘) /
///       并发上传 / 数据库保存失败补偿删除。
/// </summary>
public sealed class AttachmentStorageTests
{
    // ---------- 夹具 ----------

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"attachment-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static LocalFileStorage CreateStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wp04-att-{Guid.NewGuid():N}");
        return new LocalFileStorage(root);
    }

    private static AttachmentService CreateService(AppDbContext db, IFileStorage storage, long maxSizeBytes = 20L * 1024 * 1024)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AttachmentStorage:MaxSizeBytes"] = maxSizeBytes.ToString()
            })
            .Build();
        var currentUser = new CurrentUserService(new HttpContextAccessor());
        return new AttachmentService(db, storage, currentUser, configuration, NullLogger<AttachmentService>.Instance);
    }

    private static MemoryStream StreamOf(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    // ---------- 场景 1: 上传保存 ----------

    [Fact]
    public async Task Upload_SavesFile_AndPersistsMetadata()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("wp04 attachment test content");

            var dto = await service.UploadAsync("DeliveryReview", "DR-2026081800001", "评审报告.pdf", "application/pdf", content);

            Assert.Equal("DeliveryReview", dto.BusinessType);
            Assert.Equal("DR-2026081800001", dto.BusinessId);
            Assert.Equal("评审报告.pdf", dto.FileName);
            Assert.Equal("pdf", dto.Extension);
            Assert.Equal("application/pdf", dto.ContentType);
            Assert.Equal(0, dto.SourceType);
            Assert.NotNull(dto.StorageKey);

            // StorageKey 必须为相对路径: 不以 / 或 \ 开头、非绝对路径、不包含根目录前缀
            var key = dto.StorageKey!;
            Assert.False(Path.IsPathRooted(key));
            Assert.False(key.StartsWith('/') || key.StartsWith('\\'));
            Assert.False(key.Contains("Data"));
            Assert.Matches(@"^\d{4}/\d{2}/\d{2}/[0-9a-f]{32}\.pdf$", key);

            // 物理文件真实落盘
            var fullPath = Path.Combine(Path.GetFullPath(storage.RootPath), key.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath));
            Assert.Equal(content.Length, dto.FileSize);
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 2: 下载回显 ----------

    [Fact]
    public async Task Download_ReturnsStream_WithOriginalFileName()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("download me");
            var dto = await service.UploadAsync("WorkOrder", "WO-001", "图纸.pdf", "application/pdf", content);

            var result = await service.OpenForDownloadAsync(dto.Id);

            Assert.False(result.IsExternal);
            Assert.Equal("图纸.pdf", result.Attachment.FileName);
            Assert.Equal("application/pdf", result.Attachment.ContentType);
            using var stream = result.Stream;
            Assert.NotNull(stream);
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 3: 扩展名白名单 ----------

    [Fact]
    public async Task Upload_DisallowedExtension_ThrowsValidation()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("evil");
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.UploadAsync("DeliveryReview", "DR-1", "payload.exe", "application/octet-stream", content));
            Assert.Equal(0, db.Attachments.Count()); // 未写元数据
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 3b: MIME 白名单(扩展名 ↔ Content-Type 配对校验) ----------

    /// <summary>.pdf 配 text/html: 拒绝(XSS 攻击面——下载按扩展名 inline 但响应头为可执行 MIME)。</summary>
    [Fact]
    public async Task Upload_PdfWithHtmlContentType_ThrowsValidation()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("<script>alert(1)</script>");
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.UploadAsync("DeliveryReview", "DR-1", "fake.pdf", "text/html", content));
            Assert.Equal(0, db.Attachments.Count());
        }
        finally
        {
            Cleanup(storage);
        }
    }

    /// <summary>.pdf 配不匹配的 text/csv: 拒绝。</summary>
    [Fact]
    public async Task Upload_PdfWithCsvContentType_ThrowsValidation()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("data");
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.UploadAsync("DeliveryReview", "DR-1", "fake.pdf", "text/csv", content));
        }
        finally
        {
            Cleanup(storage);
        }
    }

    /// <summary>application/octet-stream(通用二进制占位): 放行——下载时强制附件不内联,无脚本化风险。</summary>
    [Fact]
    public async Task Upload_OctetStreamContentType_IsAccepted()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("binary-ish");
            var dto = await service.UploadAsync("DeliveryReview", "DR-1", "file.pdf", "application/octet-stream", content);
            Assert.Equal("application/octet-stream", dto.ContentType);
            Assert.Equal(1, db.Attachments.Count());
        }
        finally
        {
            Cleanup(storage);
        }
    }

    /// <summary>带 charset 参数的 MIME(如 text/csv; charset=utf-8): 剥离参数后校验通过。</summary>
    [Fact]
    public async Task Upload_CsvWithCharsetParameter_IsAccepted()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("a,b\n1,2");
            var dto = await service.UploadAsync("DeliveryReview", "DR-1", "data.csv", "text/csv; charset=utf-8", content);
            Assert.Equal("text/csv; charset=utf-8", dto.ContentType);
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 4: 大小超限 ----------

    [Fact]
    public async Task Upload_OverMaxSize_ThrowsValidation()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage, maxSizeBytes: 100); // 上限 100 字节
        try
        {
            using var content = StreamOf(new string('x', 200)); // 200 字节
            await Assert.ThrowsAsync<ValidationException>(() =>
                service.UploadAsync("DeliveryReview", "DR-1", "big.pdf", "application/pdf", content));
            Assert.Equal(0, db.Attachments.Count());
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 5: 路径遍历防护 ----------

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("a/../../secret.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("\\etc\\passwd")]
    [InlineData("C:/windows/system32/config")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidKey_RejectsTraversalKeys(string? key)
    {
        var storage = CreateStorage();
        try
        {
            Assert.False(storage.IsValidKey(key!));
        }
        finally
        {
            Cleanup(storage);
        }
    }

    [Fact]
    public void IsValidKey_AcceptsValidRelativeKey()
    {
        var storage = CreateStorage();
        var root = Path.GetFileName(Path.GetFullPath(storage.RootPath));
        try
        {
            Assert.True(storage.IsValidKey("2026/08/18/abc123.pdf"));
            // 目录名前缀相似不作为越界依据(GetRelativePath 精确判定而非 StartsWith):
            // {root名}_backup 拼在 root 下是 root 内部合法子路径 → 合法
            Assert.True(storage.IsValidKey($"{root}_backup/x.pdf"));
            // 真正的越界(兄弟目录)必须经 .. 段被拒绝
            Assert.False(storage.IsValidKey($"../{root}_backup/x.pdf"));
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 6: 删除(记录+物理文件+幂等) ----------

    [Fact]
    public async Task Delete_RemovesRecordAndPhysicalFile()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var content = StreamOf("to be deleted");
            var dto = await service.UploadAsync("WorkOrder", "WO-001", "tmp.pdf", "application/pdf", content);
            var fullPath = Path.Combine(Path.GetFullPath(storage.RootPath), dto.StorageKey!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath));

            await service.DeleteAsync(dto.Id);

            Assert.False(File.Exists(fullPath));                  // 物理文件已删
            Assert.Equal(0, db.Attachments.Count());              // 记录已删
            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(dto.Id)); // 重复删除幂等(记录不存在 → NotFound)
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 7: 业务关联查询 ----------

    [Fact]
    public async Task GetByBusiness_ReturnsOnlyMatchedAttachments()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            using var c1 = StreamOf("a1");
            using var c2 = StreamOf("a2");
            using var c3 = StreamOf("b1");
            await service.UploadAsync("DeliveryReview", "DR-1", "a1.pdf", "application/pdf", c1);
            await service.UploadAsync("DeliveryReview", "DR-1", "a2.pdf", "application/pdf", c2);
            await service.UploadAsync("DeliveryReview", "DR-2", "b1.pdf", "application/pdf", c3);

            var dr1 = await service.GetByBusinessAsync("DeliveryReview", "DR-1");
            var dr2 = await service.GetByBusinessAsync("DeliveryReview", "DR-2");

            Assert.Equal(2, dr1.Count);
            Assert.Single(dr2);
            Assert.DoesNotContain(dr1, a => a.BusinessId == "DR-2");
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 8: 外部来源(SourceType=1 不落盘) ----------

    [Fact]
    public async Task CreateExternal_StoresReferenceOnly_NoPhysicalFile()
    {
        var db = CreateDb();
        var storage = CreateStorage();
        var service = CreateService(db, storage);
        try
        {
            var dto = await service.CreateExternalAsync(new ExternalAttachmentCreateDto
            {
                BusinessType = "DeliveryReview",
                BusinessId = "DR-1",
                FileName = "企微附件.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                ExternalUrl = "https://wecom.example.com/file/12345"
            });

            Assert.Equal(1, dto.SourceType);
            Assert.Null(dto.StorageKey);          // 不落盘
            Assert.Equal("https://wecom.example.com/file/12345", dto.ExternalUrl);

            // 存储根目录无任何文件
            var root = Path.GetFullPath(storage.RootPath);
            Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>());

            // 下载返回受控外部引用
            var result = await service.OpenForDownloadAsync(dto.Id);
            Assert.True(result.IsExternal);
            Assert.Equal("https://wecom.example.com/file/12345", result.ExternalUrl);
            Assert.Null(result.Stream);
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 10: 并发上传 ----------
    // 注: EF InMemory provider 对并发自增 Id 存在已知竞态(真实 SQL Server identity 安全,非本模块问题);
    // 因此并发测试聚焦「存储层 GUID 命名并发不冲突 + 文件数量一致」(场景 10 的核心断言)。

    [Fact]
    public async Task ConcurrentUploads_AllSucceed_KeysUnique()
    {
        var storage = CreateStorage();
        try
        {
            var tasks = Enumerable.Range(0, 30)
                .Select(i => storage.SaveAsync(StreamOf($"content-{i}"), $"file-{i}.pdf", "application/pdf"));
            var keys = await Task.WhenAll(tasks);

            Assert.Equal(30, keys.Length);
            Assert.Equal(30, keys.Distinct().Count()); // GUID 命名并发不冲突
            Assert.Equal(30, Directory.GetFiles(Path.GetFullPath(storage.RootPath), "*.pdf", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Cleanup(storage);
        }
    }

    // ---------- 场景 11: 数据库保存失败补偿 ----------

    private sealed class FailingAppDbContext : AppDbContext
    {
        public FailingAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated db failure");
    }

    [Fact]
    public async Task Upload_DbSaveFails_CompensatesFileDeletion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"attachment-fail-{Guid.NewGuid():N}")
            .Options;
        await using var failingDb = new FailingAppDbContext(options);
        var storage = CreateStorage();
        var service = CreateService(failingDb, storage);
        try
        {
            using var content = StreamOf("will be compensated");
            // 数据库保存失败 → 原异常抛出
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UploadAsync("DeliveryReview", "DR-FAIL", "补偿.pdf", "application/pdf", content));

            // 补偿生效: 存储根目录无任何文件(已落盘文件被 DeleteAsync 清理)
            var root = Path.GetFullPath(storage.RootPath);
            Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>());
        }
        finally
        {
            Cleanup(storage);
        }
    }

    private static void Cleanup(LocalFileStorage storage)
    {
        try
        {
            if (Directory.Exists(storage.RootPath))
                Directory.Delete(storage.RootPath, recursive: true);
        }
        catch
        {
            // 测试清理失败不影响结论
        }
    }

    // ---------- DoD: 已证明可替换 MinIO/OSS 而不改业务服务边界 ----------
    // AttachmentService 仅依赖 IFileStorage 接口;替换为任意实现(如未来 MinioFileStorage)业务层零改动。
    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
        public int Saved { get; private set; }

        public Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var key = $"fake/{DateTime.Now:yyyyMMddHHmmssfff}/{Saved++}-{fileName}";
            _store[key] = ms.ToArray();
            return Task.FromResult(key);
        }

        public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default)
        {
            if (!_store.TryGetValue(storageKey, out var bytes))
                throw new FileNotFoundException("存储对象不存在", storageKey);
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            _store.Remove(storageKey);
            return Task.CompletedTask;
        }

        public bool IsValidKey(string storageKey) => !string.IsNullOrWhiteSpace(storageKey) && !storageKey.Contains("..");
    }

    [Fact]
    public async Task StorageAbstraction_SwitchToFakeProvider_BusinessFlowUnchanged()
    {
        var db = CreateDb();
        var fakeStorage = new FakeFileStorage();
        var service = CreateService(db, fakeStorage); // 业务层注入接口,与 LocalFileStorage 无关
        try
        {
            using var content = StreamOf("provider-agnostic content");
            var dto = await service.UploadAsync("DeliveryReview", "DR-1", "报告.pdf", "application/pdf", content);

            Assert.NotNull(dto.StorageKey);
            Assert.StartsWith("fake/", dto.StorageKey); // 由 FakeFileStorage 实现命名,业务层不感知

            var result = await service.OpenForDownloadAsync(dto.Id);
            Assert.False(result.IsExternal);
            Assert.Equal("报告.pdf", result.Attachment.FileName);
            using var stream = result.Stream;
            using var reader = new StreamReader(stream);
            Assert.Equal("provider-agnostic content", await reader.ReadToEndAsync());

            await service.DeleteAsync(dto.Id);
            Assert.Equal(0, db.Attachments.Count());
            Assert.Equal(1, fakeStorage.Saved);
        }
        finally
        {
            // 无磁盘目录需清理(Fake 不落盘)
        }
    }
}
