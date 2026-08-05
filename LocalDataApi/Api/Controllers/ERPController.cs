using LocalDataApi.Application.Common;
using LocalDataApi.Application.Erp;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers;

/// <summary>
/// ERP 基础接口(用户列表、用户校验)。
/// </summary>
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
    /// 校验 ERP 用户(tb_control_user):用户名不存在返回"用户名错误",密码不匹配返回"密码错误"。
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<ERPUserDto?>>> ValidateUser(ERPUserLoginDto dto)
    {
        var (success, message, user) = await _erpBaseService.ValidateUserAsync(dto.Username!, dto.Upwd!);

        // 不回传明文密码:转换为不含密码字段的响应模型
        var userDto = user == null
            ? null
            : new ERPUserDto
            {
                ID = user.ID,
                username = user.username,
                usercode = user.usercode
            };

        return Ok(new ApiResponse<ERPUserDto?>
        {
            Success = success,
            Message = message,
            Data = userDto
        });
    }
}
