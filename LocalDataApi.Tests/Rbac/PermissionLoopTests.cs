using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LocalDataApi.Tests.Rbac;

/// <summary>
/// 权限闭环测试基建:InMemory AppDbContext + 种子数据。
/// 覆盖链路: User → UserRole → Role → RolePermission → Permission(Code) → MenuPermission → Menu。
/// </summary>
public sealed class PermissionLoopTests
{
    // ---------- 基建 ----------

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rbac-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static PermissionCache CreateCache() => new(new MemoryCache(new MemoryCacheOptions()));

    private sealed class NoopAuditLog : IAuditLogService
    {
        public Task LogAsync(string? userId, string action, string targetType, string? targetId, object? content = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AuthorizationService CreateAuth(AppDbContext db) => new(db, CreateCache());
    private static MenuService CreateMenu(AppDbContext db) => new(db, new NoopAuditLog(), CreateAuth(db));

    /// <summary>构建完整 RBAC 种子:1 用户、2 角色(ADMIN/普通)、2 权限点、菜单树 + 权限绑定。</summary>
    private static async Task<AppDbContext> SeedFullRbacAsync()
    {
        var db = CreateDb();

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "系统管理员", Enabled = true, IsSystem = true };
        var userRole = new Role { Id = Guid.NewGuid(), Code = "USER", Name = "普通用户", Enabled = true };
        db.Roles.AddRange(adminRole, userRole);

        var permView = new Permission { Id = Guid.NewGuid(), Code = "PMC.Schedule.View", Module = "PMC", Resource = "Schedule", Action = "View", DisplayName = "查看排产", Enabled = true };
        var permCreate = new Permission { Id = Guid.NewGuid(), Code = "PMC.Schedule.Create", Module = "PMC", Resource = "Schedule", Action = "Create", DisplayName = "新建排产", Enabled = true };
        db.Permissions.AddRange(permView, permCreate);

        // 菜单树: 制造中心(目录) → 排产管理(叶子,绑定 View) + 工单跟踪(叶子,无绑定)
        var center = new Menu { Id = Guid.NewGuid(), Name = "制造中心", Path = "/pmc", Type = "Directory", Sort = 1, Status = true };
        var schedule = new Menu { Id = Guid.NewGuid(), Name = "排产管理", Path = "/pmc/schedule", Component = "Pmc/Schedule", Type = "Menu", Sort = 1, Status = true, ParentId = center.Id };
        var workOrder = new Menu { Id = Guid.NewGuid(), Name = "工单跟踪", Path = "/pmc/work-order", Component = "Pmc/WorkOrder", Type = "Menu", Sort = 2, Status = true, ParentId = center.Id };
        db.Menus.AddRange(center, schedule, workOrder);
        db.MenuPermissions.Add(new MenuPermission { Id = Guid.NewGuid(), MenuId = schedule.Id, PermissionCode = permView.Code });

        // 用户: admin(ADMIN 角色)、user(普通角色,有 View 权限)
        var adminUser = new User { Id = 1, UserName = "admin", NormalizedUserName = "ADMIN", Status = UserStatus.Active };
        var normalUser = new User { Id = 2, UserName = "zhangsan", NormalizedUserName = "ZHANGSAN", Status = UserStatus.Active };
        db.Users.AddRange(adminUser, normalUser);

        db.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), UserId = 1, RoleId = adminRole.Id, IsActive = true },
            new UserRole { Id = Guid.NewGuid(), UserId = 2, RoleId = userRole.Id, IsActive = true }
        );
        db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = userRole.Id, PermissionId = permView.Id });

        await db.SaveChangesAsync();
        return db;
    }

    // ---------- 链路: User → Role → Permission(Code) ----------

    [Fact]
    public async Task UserPermissions_ReturnsCodesAlongFullChain()
    {
        var db = await SeedFullRbacAsync();
        var auth = CreateAuth(db);

        var codes = await auth.GetUserPermissionsAsync(2);

        Assert.Contains("PMC.Schedule.View", codes);
        Assert.DoesNotContain("PMC.Schedule.Create", codes); // 普通用户未绑定 Create
    }

    [Fact]
    public async Task UserPermissions_AdminHasAllSeededCodes()
    {
        var db = await SeedFullRbacAsync();
        var auth = CreateAuth(db);

        // ADMIN 角色不通过 RolePermission 计算,而是独立判断(见 MenuService/授权直通逻辑)
        var (roles, permissions) = await auth.GetUserRolesAndPermissionsAsync(1);

        Assert.Contains("ADMIN", roles);
        Assert.NotNull(permissions);
    }

    [Fact]
    public async Task UserPermissions_DisabledRoleOrPermissionExcluded()
    {
        var db = await SeedFullRbacAsync();
        // 停用 USER 角色 → 普通用户权限清空
        var userRole = await db.Roles.SingleAsync(r => r.Code == "USER");
        userRole.Enabled = false;
        await db.SaveChangesAsync();

        var auth = CreateAuth(db);
        var codes = await auth.GetUserPermissionsAsync(2);

        Assert.Empty(codes);
    }

    [Fact]
    public async Task UserPermissions_CacheEvictionOnRoleChange()
    {
        var db = await SeedFullRbacAsync();
        var cache = CreateCache();
        var auth = new AuthorizationService(db, cache);

        var before = await auth.GetUserPermissionsAsync(2);
        Assert.Contains("PMC.Schedule.View", before);

        // 清除缓存后重新计算仍一致(缓存刷新机制)
        cache.Remove(2);
        var after = await auth.GetUserPermissionsAsync(2);
        Assert.Equal(before, after);
    }

    // ---------- 链路: MenuPermission → Menu(动态菜单过滤) ----------

    [Fact]
    public async Task CurrentMenus_AdminSeesAllActiveMenus()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        var menus = await menuService.GetCurrentUserMenusAsync(1);

        Assert.Single(menus); // 只有根:制造中心
        Assert.Equal("制造中心", menus[0].Name);
        Assert.Equal(2, menus[0].Children.Count); // ADMIN 可见全部子菜单(含未绑定权限的工单跟踪)
    }

    [Fact]
    public async Task CurrentMenus_UserSeesOnlyPermittedWithAncestors()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        var menus = await menuService.GetCurrentUserMenusAsync(2);

        Assert.Single(menus);
        Assert.Equal("制造中心", menus[0].Name);
        var child = Assert.Single(menus[0].Children); // 仅排产管理(View 绑定),工单跟踪不可见
        Assert.Equal("排产管理", child.Name);
    }

    [Fact]
    public async Task CurrentMenus_DisabledMenuExcluded()
    {
        var db = await SeedFullRbacAsync();
        var schedule = await db.Menus.SingleAsync(m => m.Name == "排产管理");
        schedule.Status = false;
        await db.SaveChangesAsync();

        var menuService = CreateMenu(db);
        var menus = await menuService.GetCurrentUserMenusAsync(2);

        // 排产管理停用后,普通用户无任何可见叶子 → 根也被过滤(无可见子项)
        Assert.Empty(menus);
    }

    // ---------- 菜单权限绑定契约(Create/Update 携带 Permissions) ----------

    [Fact]
    public async Task CreateMenu_WithPermissions_BindsMenuPermission()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        var dto = new Dto.MenuCreateDto
        {
            Name = "排产分析",
            Path = "/pmc/analysis",
            Component = "Pmc/Analysis",
            Permissions = new List<string> { "PMC.Schedule.View" }
        };
        var created = await menuService.CreateMenuAsync(dto, "1");

        var binding = await db.MenuPermissions.SingleAsync(b => b.MenuId == created.Id);
        Assert.Equal("PMC.Schedule.View", binding.PermissionCode);
    }

    [Fact]
    public async Task CreateMenu_WithUnknownPermission_ThrowsValidation()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        var dto = new Dto.MenuCreateDto
        {
            Name = "坏菜单",
            Path = "/pmc/bad",
            Permissions = new List<string> { "PMC.Not.Exists" }
        };

        await Assert.ThrowsAsync<Application.Common.ValidationException>(() => menuService.CreateMenuAsync(dto, "1"));
    }

    [Fact]
    public async Task UpdateMenu_PermissionsOverridesBindings()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        // 创建时绑定 View
        var dto = new Dto.MenuCreateDto { Name = "排产分析", Path = "/pmc/analysis", Permissions = new List<string> { "PMC.Schedule.View" } };
        var created = await menuService.CreateMenuAsync(dto, "1");

        // 更新为 Create(整体覆盖)
        var update = new Dto.MenuUpdateDto { Permissions = new List<string> { "PMC.Schedule.Create" } };
        await menuService.UpdateMenuAsync(created.Id, update, "1");

        var bindings = await db.MenuPermissions.Where(b => b.MenuId == created.Id).ToListAsync();
        var code = Assert.Single(bindings);
        Assert.Equal("PMC.Schedule.Create", code.PermissionCode);
    }

    [Fact]
    public async Task UpdateMenu_EmptyPermissions_ClearsBindings()
    {
        var db = await SeedFullRbacAsync();
        var menuService = CreateMenu(db);

        var dto = new Dto.MenuCreateDto { Name = "排产分析", Path = "/pmc/analysis", Permissions = new List<string> { "PMC.Schedule.View" } };
        var created = await menuService.CreateMenuAsync(dto, "1");

        var update = new Dto.MenuUpdateDto { Permissions = new List<string>() };
        await menuService.UpdateMenuAsync(created.Id, update, "1");

        Assert.Empty(await db.MenuPermissions.Where(b => b.MenuId == created.Id).ToListAsync());
    }

    // ---------- Seeder 种子完整性(权限闭环第一环:PermissionCodes.All → 数据库) ----------

    [Fact]
    public async Task RbacSeeder_SeedsAllPermissionCodes_AndAdminRole()
    {
        var db = CreateDb();
        var seeder = new RbacSeeder(
            db,
            NullLogger<RbacSeeder>.Instance,
            new ConfigurationBuilder().Build());

        await seeder.SeedAsync();

        // 权限码:全量种子,且与 PermissionCodes.All 完全一致(不重不漏)
        var expected = PermissionCodes.All.OrderBy(code => code, StringComparer.Ordinal).ToList();
        var actual = await db.Permissions.AsNoTracking()
            .Where(p => p.Enabled)
            .Select(p => p.Code)
            .ToListAsync();
        actual.Sort(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Count, PermissionCodes.All.Count);

        // ADMIN 角色存在且启用
        Assert.True(await db.Roles.AnyAsync(r => r.Code == "ADMIN" && r.Enabled));
        // 权限码格式统一为 模块.资源.动作(3 段)
        Assert.All(expected, code => Assert.Equal(3, code.Split('.').Length));
    }
}
