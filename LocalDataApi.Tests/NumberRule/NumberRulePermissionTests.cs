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

namespace LocalDataApi.Tests.Platform;

/// <summary>
/// NumberRule 接口权限过滤器行为测试:
/// - 已登录但无 Platform.NumberRule.View 权限 → 403 (P0-01 闭环)
/// - 已登录且有 View 权限 → 放行
/// 使用真实 HasPermissionAttribute + 真实 AuthorizationService(InMemory 权限链路数据)。
/// </summary>
public sealed class NumberRulePermissionTests
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
            .UseInMemoryDatabase($"perm-test-{Guid.NewGuid():N}")
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
        var controller = new object();
        return (
            new ActionExecutingContext(actionContext, filters, arguments, controller),
            new ActionExecutedContext(actionContext, filters, controller));
    }

    /// <summary>已登录但无 View 权限 → 403 (查询接口权限闭环)。</summary>
    [Fact]
    public async Task LoggedInUser_WithoutViewPermission_Gets403()
    {
        var db = CreateDb(); // 空库:用户 999 无任何权限
        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformNumberRuleView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.NotNull(executing.Result);
        var result = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    /// <summary>已登录且有 View 权限 → 放行 (next 执行,Result 保持 null)。</summary>
    [Fact]
    public async Task User_WithViewPermission_IsAllowed()
    {
        var db = CreateDb();
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = PermissionCodes.PlatformNumberRuleView,
            Module = "Platform",
            Resource = "NumberRule",
            Action = "View",
            DisplayName = "查看编码规则",
            Enabled = true,
            CreateTime = DateTime.Now
        };
        var role = new Role { Id = Guid.NewGuid(), Code = "NR_ADMIN", Enabled = true };
        db.Permissions.Add(permission);
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = 999, RoleId = role.Id, IsActive = true });
        db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var (executing, executed) = BuildContext(db, userId: 999);
        var filter = new HasPermissionAttribute(PermissionCodes.PlatformNumberRuleView);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.Null(executing.Result); // 未拦截,继续执行
    }

    /// <summary>权限码必须存在于 All 集合(管理员授权闭环)。</summary>
    [Fact]
    public void PermissionCodes_NumberRuleCodes_AreInAllList()
    {
        var all = PermissionCodes.All;
        Assert.Contains(PermissionCodes.PlatformNumberRuleView, all);
        Assert.Contains(PermissionCodes.PlatformNumberRuleCreate, all);
        Assert.Contains(PermissionCodes.PlatformNumberRuleUpdate, all);
    }
}
