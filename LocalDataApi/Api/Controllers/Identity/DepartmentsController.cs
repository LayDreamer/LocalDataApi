using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 组织部门接口(部门树查询 / 企微部门同步)。
    /// </summary>
    [ApiController]
    [Route("api/identity/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _departmentService;
        private readonly CurrentUserService _currentUser;

        public DepartmentsController(IDepartmentService departmentService, CurrentUserService currentUser)
        {
            _departmentService = departmentService;
            _currentUser = currentUser;
        }

        /// <summary>获取部门树。</summary>
        [HttpGet]
        [HasPermission(PermissionCodes.DepartmentView)]
        public async Task<ActionResult<ApiResponse<List<DepartmentTreeNodeDto>>>> GetDepartments()
        {
            var tree = await _departmentService.GetDepartmentTreeAsync(HttpContext.RequestAborted);
            return Ok(new ApiResponse<List<DepartmentTreeNodeDto>> { Success = true, Data = tree });
        }

        /// <summary>从企业微信同步部门架构(新增/更新/软删除)。</summary>
        [HttpPost("sync")]
        [HasPermission(PermissionCodes.DepartmentSync)]
        public async Task<ActionResult<ApiResponse<DepartmentSyncResultDto>>> SyncDepartments()
        {
            var result = await _departmentService.SyncFromWeChatWorkAsync(_currentUser.UserId?.ToString(), HttpContext.RequestAborted);
            return Ok(new ApiResponse<DepartmentSyncResultDto> { Success = true, Message = result.Message, Data = result });
        }
    }
}
