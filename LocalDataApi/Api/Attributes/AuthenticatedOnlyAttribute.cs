namespace LocalDataApi.Api.Attributes;

/// <summary>
/// 权限设计分类标记(WP06 补充收口):标识接口为「仅认证」端点 —— 当前登录用户即可使用,无独立业务 PermissionCode。
/// 
/// 语义:
/// - 必须登录,但不需要独立 PermissionCode(如账户自管理 / 我的信息 / 动态菜单 / 基础字典读取);
/// - 认证仍由全局 FallbackPolicy = RequireAuthenticatedUser() 兜底,本标记**绝不豁免认证**,
///   与 [AllowAnonymous] 语义相反:匿名访问此类端点仍会被拒绝;
/// - 仅用于权限扫描器(ControllerPermissionScanner)对接口的显式分类,不参与运行时授权逻辑;
/// - 不得将其当作 [Authorize] 的替代或 [AllowAnonymous] 的别名。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthenticatedOnlyAttribute : Attribute
{
}
