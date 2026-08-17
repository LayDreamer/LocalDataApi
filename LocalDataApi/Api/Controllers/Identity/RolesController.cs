using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 角色管理接口(角色 CRUD + 角色权限分配)。
    /// </summary>
    [ApiController]
    [Route("api/identity/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly CurrentUserService _currentUser;

        public RolesController(IRoleService roleService, CurrentUserService currentUser)
        {
            _roleService = roleService;
            _currentUser = currentUser;
        }

        /// <summary>查询角色列表(含角色已绑定的权限ID)。</summary>
        [HttpGet]
        [HasPermission(PermissionCodes.RoleView)]
        public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
        {
            var roles = await _roleService.GetRolesAsync(HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<RoleDto>> { Success = true, Data = roles });
        }

        /// <summary>查询角色基础信息;与权限树、已选权限接口相互独立。</summary>
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionCodes.RoleView)]
        public async Task<ActionResult<ApiResponse<RoleDto>>> GetRole(Guid id)
        {
            var role = await _roleService.GetRoleAsync(id, HttpContext.RequestAborted);
            return Ok(new ApiResponse<RoleDto> { Success = true, Data = role });
        }

        /// <summary>查询角色已绑定的权限ID。</summary>
        [HttpGet("{id:guid}/permissions")]
        [HasPermission(PermissionCodes.RoleView)]
        public async Task<ActionResult<ApiResponse<List<Guid>>>> GetRolePermissions(Guid id)
        {
            var permissionIds = await _roleService.GetRolePermissionIdsAsync(id, HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<Guid>> { Success = true, Data = permissionIds });
        }

        /// <summary>创建角色。</summary>
        [HttpPost]
        [HasPermission(PermissionCodes.RoleCreate)]
        public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole(RoleCreateDto dto)
        {
            var role = await _roleService.CreateRoleAsync(dto, _currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<RoleDto> { Success = true, Message = "角色创建成功", Data = role });
        }

        /// <summary>复制角色及其权限,自动生成不冲突的名称和编码。</summary>
        [HttpPost("{id:guid}/copy")]
        [HasPermission(PermissionCodes.RoleCreate)]
        public async Task<ActionResult<ApiResponse<RoleDto>>> CopyRole(Guid id, CopyRoleRequestDto dto)
        {
            var role = await _roleService.CopyRoleAsync(id, dto, _currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<RoleDto> { Success = true, Message = "角色复制成功", Data = role });
        }

        /// <summary>修改角色。</summary>
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionCodes.RoleUpdate)]
        public async Task<ActionResult<ApiResponse<RoleDto>>> UpdateRole(Guid id, RoleUpdateDto dto)
        {
            var role = await _roleService.UpdateRoleAsync(id, dto, _currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<RoleDto> { Success = true, Message = "角色修改成功", Data = role });
        }

        /// <summary>删除角色(系统角色与已绑定用户的角色禁止删除)。</summary>
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionCodes.RoleDelete)]
        public async Task<ActionResult<ApiResponse<object>>> DeleteRole(Guid id)
        {
            await _roleService.DeleteRoleAsync(id, _currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<object> { Success = true, Message = "角色删除成功" });
        }

        /// <summary>覆盖式分配角色权限(自动刷新受影响用户权限版本与缓存)。</summary>
        [HttpPut("{id:guid}/permissions")]
        [HasPermission(PermissionCodes.RoleAssignPermission)]
        public async Task<ActionResult<ApiResponse<object>>> AssignPermissions(Guid id, AssignPermissionsRequestDto dto)
        {
            await _roleService.AssignPermissionsAsync(id, dto, _currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<object> { Success = true, Message = "角色权限分配成功" });
        }
    }
}
