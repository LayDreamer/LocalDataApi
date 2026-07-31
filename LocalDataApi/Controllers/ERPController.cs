using LocalDataApi.Dto;
using LocalDataApi.Models;
using LocalDataApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ERPController : ControllerBase
    {
        private readonly ERPBaseService _erpBaseService;

        public ERPController(ERPBaseService erpBaseService)
        {
            _erpBaseService = erpBaseService;
        }

        /// <summary>
        /// 获取 tb_control_user 表中所有用户的 username 列表。
        /// </summary>
        [HttpGet("users")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetUsers()
        {
            var users = await _erpBaseService.GetAllUsersAsync();
            return Ok(new ApiResponse<List<string>>
            {
                Success = true,
                Message = "获取成功",
                Data = users
            });
        }

        /// <summary>
        /// 校验 ERP 用户（tb_control_user）：用户名不存在返回“用户名错误”，
        /// 密码不匹配返回“密码错误”，均通过返回校验成功及用户信息。
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<ApiResponse<ERPUser?>>> ValidateUser(ERPUserLoginDto dto)
        {
            var (success, message, user) = await _erpBaseService.ValidateUserAsync(dto.Username!, dto.Upwd!);

            // 不回传明文密码
            if (user != null)
                user.upwd = null;

            return Ok(new ApiResponse<ERPUser?>
            {
                Success = success,
                Message = message,
                Data = user
            });
        }
    }
}
