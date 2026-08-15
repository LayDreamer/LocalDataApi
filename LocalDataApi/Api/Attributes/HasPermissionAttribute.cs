using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalDataApi.Api.Attributes
{
    /// <summary>
    /// 接口权限过滤器。用法: [HasPermission(PermissionCodes.ScheduleUpdate)]
    /// 多个权限编码之间为 OR 关系(满足任一即通过)。
    /// 未携带有效令牌 → 401;令牌有效但无权限 → 403。
    /// 权限检查总开关 Rbac:PermissionCheckEnabled 仅在开发环境可关闭,生产环境强制开启(防误配放行)。
    /// 权限声明错误(空/未配置)采用 Fail Close:拒绝访问,而非放行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class HasPermissionAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string[] _permissionCodes;

        public HasPermissionAttribute(params string[] permissionCodes)
        {
            _permissionCodes = permissionCodes ?? Array.Empty<string>();
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;

            // 权限检查总开关:仅开发环境可关闭(回滚方案:开发期出现误判 403 时可关闭);生产环境强制校验
            var configuration = services.GetRequiredService<IConfiguration>();
            var env = services.GetRequiredService<IHostEnvironment>();
            var permissionCheckEnabled = configuration.GetValue("Rbac:PermissionCheckEnabled", true);
            if (!permissionCheckEnabled && env.IsDevelopment())
            {
                await next();
                return;
            }

            // Fail Close:权限声明错误(未传或全为空) → 拒绝访问,避免接口因配置错误而暴露
            if (_permissionCodes.Length == 0 || _permissionCodes.All(string.IsNullOrWhiteSpace))
            {
                context.Result = new ObjectResult(new ApiResponse<object>
                {
                    Success = false,
                    Message = "接口权限配置错误,请联系管理员",
                    Data = new { code = "AUTH_PERMISSION_DENIED", permission = "EMPTY" }
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            // 1. 解析当前用户
            var currentUser = services.GetRequiredService<CurrentUserService>();
            var userId = currentUser.UserId;
            if (!userId.HasValue)
            {
                var code = context.HttpContext.Items["AuthErrorCode"]?.ToString() ?? "AUTH_SESSION_REVOKED";
                context.Result = new ObjectResult(new ApiResponse<object>
                {
                    Success = false,
                    Message = "未登录或登录已失效",
                    Data = new { code }
                })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            // 2. 权限校验(任一匹配即通过)
            var authorization = services.GetRequiredService<AuthorizationService>();
            var allowed = await authorization.HasAnyPermissionAsync(userId.Value, _permissionCodes, context.HttpContext.RequestAborted);
            if (!allowed)
            {
                context.Result = new ObjectResult(new ApiResponse<object>
                {
                    Success = false,
                    Message = "没有权限",
                    Data = new { code = "AUTH_PERMISSION_DENIED", permission = string.Join(",", _permissionCodes) }
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            await next();
        }
    }
}
