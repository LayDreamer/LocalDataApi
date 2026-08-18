using System.Security.Claims;
using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LocalDataApi.Tests.Audit;

/// <summary>
/// WP05 审计查询接口权限过滤器行为测试(V1.1 §H 权限项):
/// - 已登录但无 Platform.OperationLog.View / Platform.DataChangeLog.View → 403(Fail-Close)
/// - 已登录且有对应权限 → 放行
/// - 两个权限码必须并入 PermissionCodes.All(管理员授权闭环)
/// 使用真实 HasPermissionAttribute + 真实 AuthorizationService(InMemory 权限链路数据)。
/// </summary>
public sealed class AuditQueryPermissionTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LocalDataApi.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-query-perm-test-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static (ActionExecutingContext Executing, ActionExecutedContext Executed) BuildContext(
        AppDbContext db, long userId)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, "tester") },
            "TestAuth"));

        var httpContext = new DefaultHttpContext { User = claims };
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<CurrentUserService>();
        services.AddSingleton(db);
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<PermissionCache>();
        services.AddSingleton<AuthorizationService>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment());
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var arguments = new Dictionary<string, object?>();
        return (
            new ActionExecutingContext(actionContext, filters, arguments, new object()),
            new ActionExecutedContext(actionContext, filters, new object()));
    }

    /// <summary>已登录但无 Platform.OperationLog.View 权限 → 403(操作日志查询 Fail-Close)。</summary>
    [Fact]
    public async Task LoggedInUser_WithoutOperationLogViewPermission_Gets403()
    {
        var db = CreateDb(); // 空库:用户 999 无任何权限
        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformOperationLogView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.NotNull(executing.Result);
        var result = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    /// <summary>已登录但无 Platform.DataChangeLog.View 权限 → 403(数据变更日志查询 Fail-Close)。</summary>
    [Fact]
    public async Task LoggedInUser_WithoutDataChangeLogViewPermission_Gets403()
    {
        var db = CreateDb(); // 空库:用户 999 无任何权限
        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformDataChangeLogView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.NotNull(executing.Result);
        var result = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    /// <summary>已登录且有 Platform.OperationLog.View 权限 → 放行(next 执行,Result 保持 null)。</summary>
    [Fact]
    public async Task User_WithOperationLogViewPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.PlatformOperationLogView);

        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformOperationLogView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.Null(executing.Result); // 未拦截,继续执行
    }

    /// <summary>已登录且有 Platform.DataChangeLog.View 权限 → 放行。</summary>
    [Fact]
    public async Task User_WithDataChangeLogViewPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.PlatformDataChangeLogView);

        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformDataChangeLogView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.Null(executing.Result);
    }

    /// <summary>审计查询权限码必须并入 PermissionCodes.All(管理员授权闭环)。</summary>
    [Fact]
    public void PermissionCodes_AuditQueryCodes_AreInAllList()
    {
        var all = PermissionCodes.All;
        Assert.Contains(PermissionCodes.PlatformOperationLogView, all);
        Assert.Contains(PermissionCodes.PlatformDataChangeLogView, all);
        Assert.Contains(PermissionCodes.PlatformLoginLogView, all);
    }

    private static void SeedPermission(AppDbContext db, string code)
    {
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = code,
            Module = "Platform",
            Resource = "Audit",
            Action = "View",
            DisplayName = "查看审计日志",
            Enabled = true,
            CreateTime = DateTime.Now
        };
        var role = new Role { Id = Guid.NewGuid(), Code = "AUDIT_VIEWER", Enabled = true };
        db.Permissions.Add(permission);
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = 999, RoleId = role.Id, IsActive = true });
        db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = permission.Id });
        db.SaveChanges();
    }
}
