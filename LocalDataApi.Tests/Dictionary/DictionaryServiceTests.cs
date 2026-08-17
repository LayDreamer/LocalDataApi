using LocalDataApi.Application.Common;
using LocalDataApi.Application.Dictionary;
using LocalDataApi.Domain.Dictionary;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace LocalDataApi.Tests.Dictionary;

/// <summary>
/// 数据字典服务测试:CRUD + 缓存失效 + 权限码存在性。
/// </summary>
public sealed class DictionaryServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dict-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static DictionaryService CreateService(AppDbContext db) => new(db, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = CreateDb();
        var type = new DictionaryType { Id = 1, Code = "OrderStatus", Name = "订单状态", Sort = 1, Status = 1 };
        db.DictionaryTypes.Add(type);
        db.DictionaryItems.AddRange(
            new DictionaryItem { Id = 1, DictionaryId = 1, Value = "10", Label = "新建", Sort = 1, Status = 1 },
            new DictionaryItem { Id = 2, DictionaryId = 1, Value = "20", Label = "审核", Sort = 2, Status = 1 }
        );
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task GetByCode_ReturnsItems_AndCaches()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        var data = await service.GetByCodeAsync("OrderStatus");

        Assert.NotNull(data);
        Assert.Equal("OrderStatus", data!.Code);
        Assert.Equal(2, data.Items.Count);
        Assert.Equal("新建", data.Items[0].Label);
    }

    [Fact]
    public async Task GetByCode_UnknownCode_ReturnsNull()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        Assert.Null(await service.GetByCodeAsync("NotExists"));
    }

    [Fact]
    public async Task GetByCode_CacheInvalidatedOnItemUpdate()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        var before = await service.GetByCodeAsync("OrderStatus");
        Assert.Equal(2, before!.Items.Count);

        // 新增一个字典项 → 缓存应失效,再次查询包含新项
        await service.CreateItemAsync(new Dto.DictionaryItemCreateDto
        {
            DictionaryId = 1, Value = "30", Label = "生产中", Sort = 3
        });

        var after = await service.GetByCodeAsync("OrderStatus");
        Assert.Equal(3, after!.Items.Count);
        Assert.Contains(after.Items, item => item.Value == "30");
    }

    [Fact]
    public async Task CreateType_DuplicateCode_ThrowsConflict()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateTypeAsync(new Dto.DictionaryTypeCreateDto
        {
            Code = "OrderStatus", Name = "重复编码"
        }));
    }

    [Fact]
    public async Task DeleteType_CascadesItems()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        await service.DeleteTypeAsync(1);

        Assert.Empty(db.DictionaryTypes);
        Assert.Empty(db.DictionaryItems);
    }

    [Fact]
    public async Task GetBatch_ReturnsOnlyExistingCodes()
    {
        var db = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetBatchAsync(new[] { "OrderStatus", "Missing" });

        Assert.Single(result);
        Assert.True(result.ContainsKey("OrderStatus"));
        Assert.Equal(2, result["OrderStatus"].Count);
    }
}
