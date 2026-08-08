using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 权限字典接口。
    /// </summary>
    [ApiController]
    [Route("api/identity/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionsController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        /// <summary>查询权限字典(可按模块过滤)。</summary>
        [HttpGet]
        [HasPermission(PermissionCodes.PermissionView)]
        public async Task<ActionResult<ApiResponse<List<PermissionDto>>>> GetPermissions([FromQuery] string? module)
        {
            var permissions = await _permissionService.GetPermissionsAsync(module, HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<PermissionDto>> { Success = true, Data = permissions });
        }
    }
}
