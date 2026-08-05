using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Utils;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers;

/// <summary>
/// 账户认证接口(登录 / 注册 / 修改密码 / 更新资料)。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly string _tokenSecret;

    public AuthController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        // 生产环境必须通过配置注入密钥;缺失时回退值仅用于本地调试
        _tokenSecret = configuration["Auth:Secret"]
                       ?? "LocalDataApi-Default-Dev-Secret-Change-Me";
    }

    /// <summary>
    /// 从请求头 Authorization 中解析并校验登录令牌,取出用户名。
    /// </summary>
    private bool TryGetUserNameFromToken(out string userName)
    {
        userName = string.Empty;
        var auth = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth))
            return false;

        var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth[7..]
            : auth;

        return TokenHelper.TryValidate(token, _tokenSecret, out userName);
    }

    /// <summary>用户登录</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new ApiResponse<LoginResultDto>
            {
                Success = false,
                Message = "用户名和密码不能为空！"
            });
        }

        var result = await _userService.LoginAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new ApiResponse<LoginResultDto>
        {
            Success = result.Success,
            Message = result.Message,
            Data = result
        });
    }

    /// <summary>注册新用户</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<object>>> Register(RegisterUserDto dto)
    {
        var (success, message) = await _userService.RegisterAsync(dto);
        if (!success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = message
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = message
        });
    }

    /// <summary>修改密码(需携带登录令牌,身份从令牌获取)</summary>
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordDto dto)
    {
        if (!TryGetUserNameFromToken(out var userName))
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "未登录或登录已失效"
            });
        }

        var (success, message) = await _userService.ChangePasswordAsync(userName, dto.OldPassword!, dto.NewPassword!);
        if (!success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = message
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = message
        });
    }

    /// <summary>更新个人资料(显示名 / 邮箱 / 手机号,需携带登录令牌)</summary>
    [HttpPost("update-profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(UpdateProfileDto dto)
    {
        if (!TryGetUserNameFromToken(out var userName))
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "未登录或登录已失效"
            });
        }

        var (success, message) = await _userService.UpdateProfileAsync(userName, dto);
        if (!success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = message
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = message
        });
    }
}
