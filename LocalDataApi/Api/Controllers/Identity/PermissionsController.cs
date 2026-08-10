using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 权限字典接口(查询 / 启用停用 / 权限码同步)。
    /// </summary>
    [ApiController]
    [Route("api/identity/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly CurrentUserService _currentUser;

        public PermissionsController(IPermissionService permissionService, CurrentUserService currentUser)
        {
            _permissionService = permissionService;
            _currentUser = currentUser;
        }

        /// <summary>查询权限字典(可按模块过滤)。</summary>
        [HttpGet]
        [HasPermission(PermissionCodes.PermissionView)]
        public async Task<ActionResult<ApiResponse<List<PermissionDto>>>> GetPermissions([FromQuery] string? module)
        {
            var permissions = await _permissionService.GetPermissionsAsync(module, HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<PermissionDto>> { Success = true, Data = permissions });
        }

        /// <summary>
        /// 查询全部权限编码(含停用)。公开字典接口,供前端初始化校验/CI检查/权限差异分析使用。
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAllPermissionCodes()
        {
            var codes = await _permissionService.GetAllPermissionCodesAsync(HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<string>> { Success = true, Data = codes });
        }

        /// <summary>启用/停用权限点(记录审计;停用后相关用户权限实时失效)。</summary>
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionCodes.PermissionUpdate)]
        public async Task<ActionResult<ApiResponse<PermissionDto>>> UpdatePermission(Guid id, UpdatePermissionRequestDto dto)
        {
            var permission = await _permissionService.UpdatePermissionAsync(id, dto.Enabled, _currentUser.UserId, HttpContext.RequestAborted);
            return Ok(new ApiResponse<PermissionDto> { Success = true, Message = dto.Enabled ? "权限已启用" : "权限已停用", Data = permission });
        }
    }
}
