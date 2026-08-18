using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IAuthSessionService _sessions;
    private readonly CurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService users,
        IAuthSessionService sessions,
        CurrentUserService currentUser,
        ILogger<AuthController> logger)
    {
        _users = users;
        _sessions = sessions;
        _currentUser = currentUser;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new ApiResponse<LoginResultDto> { Success = false, Message = "用户名和密码不能为空！" });

        var result = await _users.LoginAsync(dto, RemoteIp(), Request.Headers.UserAgent.ToString());
        return Ok(new ApiResponse<LoginResultDto> { Success = result.Success, Message = result.Message, Data = result });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<object>>> Register(RegisterUserDto dto)
    {
        var (success, message) = await _users.RegisterAsync(dto);
        return success
            ? Ok(new ApiResponse<object> { Success = true, Message = message })
            : BadRequest(new ApiResponse<object> { Success = false, Message = message });
    }

    [Authorize]
    [AuthenticatedOnly]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserName))
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "未登录或登录已失效" });
        var (success, message) = await _users.ChangePasswordAsync(_currentUser.UserName, dto.OldPassword!, dto.NewPassword!);
        return success
            ? Ok(new ApiResponse<object> { Success = true, Message = message })
            : BadRequest(new ApiResponse<object> { Success = false, Message = message });
    }

    [Authorize]
    [AuthenticatedOnly]
    [HttpPost("update-profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(UpdateProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserName))
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "未登录或登录已失效" });
        var (success, message) = await _users.UpdateProfileAsync(_currentUser.UserName, dto);
        return success
            ? Ok(new ApiResponse<object> { Success = true, Message = message })
            : BadRequest(new ApiResponse<object> { Success = false, Message = message });
    }

    [AllowAnonymous]
    [HttpPost("login-by-wechatwork")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> LoginByWeChatWork(WeChatWorkLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new ApiResponse<LoginResultDto> { Success = false, Message = "授权 code 不能为空" });
        _logger.LogInformation("收到企微免登请求, CodeLength={CodeLength}, RemoteIp={RemoteIp}", dto.Code.Length, RemoteIp());
        var result = await _users.LoginByWeChatWorkAsync(dto.Code, RemoteIp(), Request.Headers.UserAgent.ToString());
        return Ok(new ApiResponse<LoginResultDto> { Success = result.Success, Message = result.Message, Data = result });
    }

    [Authorize]
    [AuthenticatedOnly]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (_currentUser.SessionId is { } sessionId)
            await _sessions.RevokeAsync(sessionId, "logout", HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "已退出登录" });
    }

    [Authorize]
    [AuthenticatedOnly]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        if (_currentUser.UserId is { } userId)
            await _sessions.RevokeAllAsync(userId, "logout-all", HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "已退出全部设备" });
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
