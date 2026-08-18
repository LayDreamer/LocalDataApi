using System.Reflection;
using LocalDataApi.Api.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LocalDataApi.Application.Common;

/// <summary>
/// 控制器权限覆盖扫描(WP06 补充收口版)。
/// 分类模型 —— 每个 HTTP Action 必须属于以下三类之一,否则视为"未分类接口"(权限遗漏):
///   1. [AllowAnonymous]      → 显式公开接口,无需认证,不告警;
///   2. [HasPermission(...)]  → 业务权限接口,需要认证 + PermissionCode,不告警(支持类级/方法级);
///   3. [AuthenticatedOnly]   → 仅认证接口,需要登录、无独立 PermissionCode,不告警(支持类级/方法级);
///   其余(无任何分类标记)     → 未分类接口,扫描必须告警,视为新增接口权限遗漏。
/// 本扫描仅输出告警,不阻断启动;与全局 FallbackPolicy = RequireAuthenticatedUser() 共同构成权限护栏。
/// </summary>
internal static class ControllerPermissionScanner
{
    /// <summary>返回 (端点总数, 未分类端点列表)。未分类 = 无 AllowAnonymous / 无 HasPermission / 无 AuthenticatedOnly。</summary>
    public static (int Total, IReadOnlyList<string> Unclassified) FindUnclassifiedEndpoints(Assembly assembly)
    {
        var unclassified = new List<string>();
        var controllerTypes = assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
            .ToList();

        var total = 0;
        foreach (var controller in controllerTypes)
        {
            var controllerAnonymous = controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            var controllerHasPermission = controller.GetCustomAttributes<HasPermissionAttribute>(true).Any();
            var controllerAuthenticatedOnly = controller.GetCustomAttributes<AuthenticatedOnlyAttribute>(true).Any();
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>(true).Any());

            foreach (var action in actions)
            {
                total++;
                // 1. 显式公开接口(类级或方法级 AllowAnonymous):不构成权限遗漏
                if (controllerAnonymous || action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()) continue;
                // 2. 权限受控接口(类级或方法级 HasPermission)
                if (controllerHasPermission || action.GetCustomAttributes<HasPermissionAttribute>(true).Any()) continue;
                // 3. 显式"仅认证"分类(类级或方法级 AuthenticatedOnly);认证仍由 FallbackPolicy 兜底
                if (controllerAuthenticatedOnly || action.GetCustomAttributes<AuthenticatedOnlyAttribute>(true).Any()) continue;
                // 4. 未分类:真实权限遗漏
                unclassified.Add($"{controller.Name}.{action.Name}");
            }
        }

        return (total, unclassified);
    }
}
