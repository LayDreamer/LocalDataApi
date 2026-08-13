using System;
using System.Linq;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.WeChatWork;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LocalDataApi.Api.Attributes;
using Microsoft.Extensions.Configuration;

namespace LocalDataApi.Api.Controllers;

/// <summary>
/// 企业微信网页授权接口(授权跳转、回调、成员查询)。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WechatController : ControllerBase
{
    private readonly IWechatWorkUserService _userService;
    private readonly IConfiguration _configuration;

    public WechatController(IWechatWorkUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    /// <summary>生成授权跳转URL(redirectUri 须为可信域名白名单内)</summary>
    [HttpGet("authorize-url")]
    [AllowAnonymous]
    public IActionResult GetAuthorizeUrl([FromQuery] string redirectUri, [FromQuery] string state = "STATE")
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "redirectUri 不能为空"
            });

        // 前端可能已做 URL 编码,这里还原为原始 URL 后再校验、再生成企微授权链接
        var actualRedirectUri = NormalizeRedirectUri(redirectUri);

        if (!IsRedirectUriAllowed(actualRedirectUri))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "redirectUri 不在可信域名白名单内"
            });

        var url = _userService.GenerateAuthorizeUrl(actualRedirectUri, state);
        return Ok(new { url });
    }

    /// <summary>授权回调处理</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(string code, string state)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new { error = "code不能为空" });
        }

        try
        {
            // 1. 获取用户基本信息(userid)
            var userInfo = await _userService.GetUserInfoByCodeAsync(code);

            // 2. 获取用户详细信息
            var userDetail = await _userService.GetUserDetailByUserIdAsync(userInfo.UserId);

            return Ok(new
            {
                userInfo.UserId,
                userDetail.Name,
                userDetail.Position,
                userDetail.Mobile,
                userDetail.Email,
                userDetail.Avatar,
                userDetail.Department
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>直接通过userid获取用户信息</summary>
    [HttpGet("user/{userId}")]
    [HasPermission(PermissionCodes.WeChatWorkUserView)]
    public async Task<IActionResult> GetUserByUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { error = "userId不能为空" });
        }

        try
        {
            var userDetail = await _userService.GetUserDetailByUserIdAsync(userId);
            return Ok(userDetail);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 校验 redirectUri 的 Host 是否在 WeChatWork:AllowedRedirectHosts 白名单内,防止开放重定向。
    /// </summary>
    private bool IsRedirectUriAllowed(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return false;

        var allowed = _configuration["WeChatWork:AllowedRedirectHosts"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      ?? Array.Empty<string>();
        return allowed.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 将前端传过来的 redirectUri 还原为原始 URL。
    /// 兼容情况:原始 URL、单次 URL 编码、双重 URL 编码。
    /// </summary>
    private static string NormalizeRedirectUri(string redirectUri)
    {
        var current = redirectUri;
        // 最多解码两次,避免对已经是原始 URL 的字符串过度解码
        for (var i = 0; i < 2; i++)
        {
            if (Uri.TryCreate(current, UriKind.Absolute, out _))
                break;

            var decoded = Uri.UnescapeDataString(current);
            if (decoded == current)
                break;

            current = decoded;
        }

        return current;
    }
}
