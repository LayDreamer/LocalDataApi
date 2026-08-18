using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Xunit;

namespace LocalDataApi.Tests.Rbac;

// ---------- 测试用控制器(顶层类,存在于测试程序集;不参与主应用路由) ----------

[AllowAnonymous]
public sealed class ScanTestAnonymousController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult Public() => Ok();
}

[HasPermission("Test.View")]
public sealed class ScanTestProtectedController : ControllerBase
{
    [HttpGet("protected")]
    public IActionResult Protected() => Ok();
}

[AuthenticatedOnly]
public sealed class ScanTestAuthOnlyController : ControllerBase
{
    [HttpGet("auth-only")]
    public IActionResult AuthOnly() => Ok();
}

[AuthenticatedOnly] // 类级标记覆盖该控制器全部 Action
public sealed class ScanTestClassLevelAuthOnlyController : ControllerBase
{
    [HttpGet("class-auth-only")]
    public IActionResult ClassAuthOnly() => Ok();
}

/// <summary>真实遗漏:无 AllowAnonymous / 无 HasPermission / 无 AuthenticatedOnly。</summary>
public sealed class ScanTestMissingController : ControllerBase
{
    [HttpGet("missing")]
    public IActionResult Missing() => Ok();
}

/// <summary>
/// WP06 权限扫描噪声收口 —— 权限分类模型测试。
/// 目标:每个 HTTP Action 必须明确属于 AllowAnonymous / HasPermission / AuthenticatedOnly 三类之一,
/// 其余为"未分类接口"必须被扫描发现(真实权限遗漏)。当前基线未分类端点 = 0。
/// </summary>
public sealed class ControllerPermissionScannerTests
{
    /// <summary>场景 1/2/3/4:AllowAnonymous / HasPermission / AuthenticatedOnly(含类级)均不告警;真正未分类仍被发现。</summary>
    [Fact]
    public void Scanner_AllowAnonymous_HasPermission_AuthenticatedOnly_AreClassified_MissingIsFlagged()
    {
        var (total, unclassified) = ControllerPermissionScanner.FindUnclassifiedEndpoints(typeof(ControllerPermissionScannerTests).Assembly);

        // 只有"真实遗漏"控制器被标记为未分类
        var missing = Assert.Single(unclassified);
        Assert.Equal("ScanTestMissingController.Missing", missing);
        Assert.DoesNotContain("ScanTestAnonymousController.Public", unclassified);
        Assert.DoesNotContain("ScanTestProtectedController.Protected", unclassified);
        Assert.DoesNotContain("ScanTestAuthOnlyController.AuthOnly", unclassified);
        Assert.DoesNotContain("ScanTestClassLevelAuthOnlyController.ClassAuthOnly", unclassified);
        Assert.True(total >= 5, "测试程序集应至少包含 5 个扫描用端点");
    }

    /// <summary>AuthenticatedOnly 是纯分类标记:不实现任何授权/过滤器接口,不参与运行时授权;认证由 FallbackPolicy 兜底。</summary>
    [Fact]
    public void AuthenticatedOnly_IsMetadataOnly_AndNotAllowAnonymous()
    {
        var attr = typeof(AuthenticatedOnlyAttribute);

        // 不实现授权/过滤器接口 → 不参与运行时授权(不会绕过认证)
        Assert.False(typeof(IAuthorizationFilter).IsAssignableFrom(attr));
        Assert.False(typeof(IAsyncAuthorizationFilter).IsAssignableFrom(attr));
        Assert.False(typeof(IAsyncActionFilter).IsAssignableFrom(attr));
        Assert.False(typeof(IResultFilter).IsAssignableFrom(attr));

        // 不是 AllowAnonymous 的别名
        Assert.False(typeof(AllowAnonymousAttribute).IsAssignableFrom(attr));
        Assert.NotEqual(typeof(AllowAnonymousAttribute), attr);

        // 可用在类/方法上,且可被扫描器反射读取
        Assert.True(attr.IsDefined(typeof(AttributeUsageAttribute), inherit: false));
        Assert.True(typeof(Attribute).IsAssignableFrom(attr));
    }
}
