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
    /// 可通过配置 Rbac:PermissionCheckEnabled=false 一键关闭(上线灰度/回滚用)。
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

            // 权限检查总开关(回滚方案:出现大量误判 403 时关闭)
            var configuration = services.GetRequiredService<IConfiguration>();
            if (!configuration.GetValue("Rbac:PermissionCheckEnabled", true))
            {
                await next();
                return;
            }

            // 1. 解析当前用户
            var currentUser = services.GetRequiredService<CurrentUserService>();
            var userId = currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Result = new ObjectResult(new ApiResponse<object>
                {
                    Success = false,
                    Message = "未登录或登录已失效",
                    Data = new { code = "AUTH_TOKEN_INVALID" }
                })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            // 2. 权限校验(任一匹配即通过)
            var authorization = services.GetRequiredService<AuthorizationService>();
            var allowed = await authorization.HasAnyPermissionAsync(userId, _permissionCodes, context.HttpContext.RequestAborted);
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
