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

namespace LocalDataApi.Tests.Pmc;

/// <summary>
/// WP08 Gate-B 权限回归 —— DeliveryReview(Approve/Update/Reject) 与 ExternalProduction(View/Create/Update/Delete)。
/// 使用真实 HasPermissionAttribute + 真实 AuthorizationService(InMemory 权限链路数据)。
/// 语义:无权限 → 403(Fail-Close);有权限 → 放行;多码(ExternalProduction Create+Update)为 OR 语义。
/// </summary>
public sealed class PmcPermissionTests
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
            .UseInMemoryDatabase($"pmc-perm-test-{Guid.NewGuid():N}")
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

    private static async Task<ActionExecutingContext> RunFilterAsync(AppDbContext db, long userId, params string[] codes)
    {
        var (executing, executed) = BuildContext(db, userId);
        var filter = new HasPermissionAttribute(codes);
        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        return executing;
    }

    private static void AssertForbidden(ActionExecutingContext executing)
    {
        Assert.NotNull(executing.Result);
        var result = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    private static void SeedPermission(AppDbContext db, string code)
    {
        var permission = new Permission
        {
            Id = Guid.NewGuid(), Code = code, Module = "PMC", Resource = "Test", Action = "X",
            DisplayName = "测试权限", Enabled = true, CreateTime = DateTime.Now
        };
        var role = new Role { Id = Guid.NewGuid(), Code = "PMC_TESTER", Enabled = true };
        db.Permissions.Add(permission);
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = 999, RoleId = role.Id, IsActive = true });
        db.RolePermissions.Add(new RolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = permission.Id });
        db.SaveChanges();
    }

    // ============ DeliveryReview 无权限 → 403 ============

    [Fact]
    public async Task DeliveryReview_Approve_WithoutPermission_Gets403()
    {
        var db = CreateDb(); // 空库:无任何权限
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewApprove));
    }

    [Fact]
    public async Task DeliveryReview_Update_WithoutPermission_Gets403()
    {
        var db = CreateDb();
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewUpdate));
    }

    [Fact]
    public async Task DeliveryReview_Reject_WithoutPermission_Gets403()
    {
        var db = CreateDb();
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewReject));
    }

    // ============ DeliveryReview 有权限 → 放行 ============

    [Fact]
    public async Task DeliveryReview_Approve_WithPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.DeliveryReviewApprove);
        var executing = await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewApprove);
        Assert.Null(executing.Result); // 未拦截,继续执行
    }

    [Fact]
    public async Task DeliveryReview_Update_WithPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.DeliveryReviewUpdate);
        var executing = await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewUpdate);
        Assert.Null(executing.Result);
    }

    [Fact]
    public async Task DeliveryReview_Reject_WithPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.DeliveryReviewReject);
        var executing = await RunFilterAsync(db, 999, PermissionCodes.DeliveryReviewReject);
        Assert.Null(executing.Result);
    }

    // ============ ExternalProduction 无权限 → 403 ============

    [Fact]
    public async Task ExternalProduction_View_WithoutPermission_Gets403()
    {
        var db = CreateDb();
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionView));
    }

    [Fact]
    public async Task ExternalProduction_CreateUpdate_WithoutPermission_Gets403()
    {
        var db = CreateDb();
        // AddOrUpdate 端点声明 Create+Update(OR);两者都无 → 403
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionCreate, PermissionCodes.ExternalProductionUpdate));
    }

    [Fact]
    public async Task ExternalProduction_Delete_WithoutPermission_Gets403()
    {
        var db = CreateDb();
        AssertForbidden(await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionDelete));
    }

    // ============ ExternalProduction 有权限 → 放行(含 OR 语义) ============

    [Fact]
    public async Task ExternalProduction_View_WithPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.ExternalProductionView);
        var executing = await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionView);
        Assert.Null(executing.Result);
    }

    [Fact]
    public async Task ExternalProduction_CreateUpdate_OnlyCreate_IsAllowed_ByOrSemantics()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.ExternalProductionCreate); // 仅 Create
        var executing = await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionCreate, PermissionCodes.ExternalProductionUpdate);
        Assert.Null(executing.Result); // OR:任一满足即放行
    }

    [Fact]
    public async Task ExternalProduction_CreateUpdate_OnlyUpdate_IsAllowed_ByOrSemantics()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.ExternalProductionUpdate); // 仅 Update
        var executing = await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionCreate, PermissionCodes.ExternalProductionUpdate);
        Assert.Null(executing.Result);
    }

    [Fact]
    public async Task ExternalProduction_Delete_WithPermission_IsAllowed()
    {
        var db = CreateDb();
        SeedPermission(db, PermissionCodes.ExternalProductionDelete);
        var executing = await RunFilterAsync(db, 999, PermissionCodes.ExternalProductionDelete);
        Assert.Null(executing.Result);
    }

    // ============ 权限码授权闭环 ============

    [Fact]
    public void PermissionCodes_DeliveryReviewAndExternalProduction_AreInAllList()
    {
        var all = PermissionCodes.All;
        Assert.Contains(PermissionCodes.DeliveryReviewApprove, all);
        Assert.Contains(PermissionCodes.DeliveryReviewUpdate, all);
        Assert.Contains(PermissionCodes.DeliveryReviewReject, all);
        Assert.Contains(PermissionCodes.ExternalProductionView, all);
        Assert.Contains(PermissionCodes.ExternalProductionCreate, all);
        Assert.Contains(PermissionCodes.ExternalProductionUpdate, all);
        Assert.Contains(PermissionCodes.ExternalProductionDelete, all);
    }
}
