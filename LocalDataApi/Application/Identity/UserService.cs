using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using LocalDataApi.Application.WeChatWork;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public sealed class UserService(
    AppDbContext context,
    IWechatWorkUserService wxUserService,
    IUserRoleService userRoleService,
    AuthorizationService authorization,
    IAuthSessionService sessions,
    ILoginLogService loginLogs,
    IConfiguration configuration) : IUserService
{
    private const int MaxLoginFailCount = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly string _defaultLoginRole = configuration.GetValue("Rbac:DefaultLoginRole", "VIEWER");

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var normalized = Normalize(request.UserName);
        if (normalized is null || string.IsNullOrWhiteSpace(request.Password))
            return await FailedAsync(null, request.UserName, "Password", "INVALID_REQUEST", "用户名和密码不能为空", stopwatch);

        var user = await context.Users.FirstOrDefaultAsync(item => item.NormalizedUserName == normalized);
        if (user is null) return await FailedAsync(null, request.UserName, "Password", "USER_NOT_FOUND", "用户不存在", stopwatch);
        if (user.Status != UserStatus.Active) return await FailedAsync(user, user.UserName, "Password", "ACCOUNT_DISABLED", "账号已禁用", stopwatch);
        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
            return await FailedAsync(user, user.UserName, "Password", "ACCOUNT_LOCKED", "账号已锁定", stopwatch);

        if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt) || !PasswordHelper.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.LoginFailCount++;
            if (user.LoginFailCount >= MaxLoginFailCount) user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
            user.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return await FailedAsync(user, user.UserName, "Password", "PASSWORD_INVALID", "用户名或密码错误", stopwatch);
        }

        user.LoginFailCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return await SucceedAsync(user, request.RememberMe, ipAddress, userAgent, "Password", stopwatch);
    }

    public async Task<LoginResultDto> LoginByWeChatWorkAsync(string code, string? ipAddress = null, string? userAgent = null)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(code)) return await FailedAsync(null, null, "WeChatWork", "INVALID_REQUEST", "授权 code 不能为空", stopwatch);
        var wxUser = await wxUserService.GetUserInfoByCodeAsync(code);
        if (string.IsNullOrWhiteSpace(wxUser.UserId)) return await FailedAsync(null, null, "WeChatWork", "WECHAT_USER_NOT_FOUND", "未获取到企业微信身份", stopwatch);

        var external = await context.UserExternalIdentities.Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Provider == "WeChatWork" && item.ExternalSubject == wxUser.UserId);
        var user = external?.User;
        if (user is null)
        {
            var detail = await wxUserService.GetUserDetailByUserIdAsync(wxUser.UserId);
            user = new User
            {
                UserName = await EnsureUniqueUserNameAsync($"wx_{wxUser.UserId}"),
                DisplayName = string.IsNullOrWhiteSpace(detail.Name) ? wxUser.UserId : detail.Name,
                Email = detail.Email,
                PhoneNumber = detail.Mobile,
                Status = UserStatus.Active,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            user.NormalizedUserName = Normalize(user.UserName)!;
            context.Users.Add(user);
            await context.SaveChangesAsync();
            context.UserExternalIdentities.Add(new UserExternalIdentity { UserId = user.Id, Provider = "WeChatWork", ExternalSubject = wxUser.UserId });
            await context.SaveChangesAsync();
        }
        if (user.Status != UserStatus.Active) return await FailedAsync(user, user.UserName, "WeChatWork", "ACCOUNT_DISABLED", "账号已禁用", stopwatch);
        user.LastLoginAtUtc = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return await SucceedAsync(user, false, ipAddress, userAgent, "WeChatWork", stopwatch);
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto)
    {
        var normalized = Normalize(dto.UserName);
        if (normalized is null || string.IsNullOrWhiteSpace(dto.Password)) return (false, "用户名和密码不能为空");
        if (!PasswordValidation.Validate(dto.Password, dto.UserName).Valid) return (false, "密码不符合强度要求");
        if (await context.Users.AnyAsync(item => item.NormalizedUserName == normalized)) return (false, "用户名已存在");
        PasswordHelper.CreateHash(dto.Password, out var hash, out var salt);
        context.Users.Add(new User
        {
            UserName = dto.UserName!.Trim(), NormalizedUserName = normalized,
            DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.UserName.Trim() : dto.DisplayName.Trim(),
            Email = dto.Email, PhoneNumber = dto.PhoneNumber, PasswordHash = hash, PasswordSalt = salt,
            PasswordAlgorithm = "PBKDF2-SHA256-100000", PasswordUpdatedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (true, "注册成功");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(string userName, string oldPassword, string newPassword)
    {
        var normalized = Normalize(userName);
        var user = normalized is null ? null : await context.Users.FirstOrDefaultAsync(item => item.NormalizedUserName == normalized);
        if (user is null) return (false, "用户不存在");
        if (!PasswordHelper.Verify(oldPassword, user.PasswordHash ?? string.Empty, user.PasswordSalt ?? string.Empty)) return (false, "原密码错误");
        if (!PasswordValidation.Validate(newPassword, user.UserName).Valid) return (false, "密码不符合强度要求");
        SetPassword(user, newPassword, false);
        await context.SaveChangesAsync();
        await sessions.RevokeAllAsync(user.Id, "password-changed");
        return (true, "密码修改成功");
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(string userName, UpdateProfileDto dto)
    {
        var normalized = Normalize(userName);
        var user = normalized is null ? null : await context.Users.FirstOrDefaultAsync(item => item.NormalizedUserName == normalized);
        if (user is null) return (false, "用户不存在");
        if (dto.DisplayName is not null) user.DisplayName = dto.DisplayName.Trim();
        if (dto.Email is not null) user.Email = dto.Email;
        if (dto.PhoneNumber is not null) user.PhoneNumber = dto.PhoneNumber;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return (true, "资料更新成功");
    }

    public async Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(long userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null) return (false, "用户不存在", null);
        var password = GenerateRandomPassword(12);
        SetPassword(user, password, true);
        user.LoginFailCount = 0;
        user.LockoutEndUtc = null;
        await context.SaveChangesAsync();
        await sessions.RevokeAllAsync(user.Id, "password-reset");
        return (true, "密码已重置", password);
    }

    private async Task<LoginResultDto> SucceedAsync(User user, bool rememberMe, string? ipAddress, string? userAgent, string loginType, Stopwatch stopwatch)
    {
        await userRoleService.EnsureUserHasRoleAsync(user.Id, _defaultLoginRole);
        var (roles, permissions) = await authorization.GetUserRolesAndPermissionsAsync(user.Id);
        var issue = await sessions.CreateAsync(user, rememberMe, ipAddress, userAgent);
        await WriteLoginLogAsync(user.Id, user.UserName, loginType, true, null, null, issue.SessionId, stopwatch);
        return new LoginResultDto
        {
            Success = true, Message = "登录成功", Token = issue.AccessToken, UserId = user.Id, UserName = user.DisplayName,
            Roles = roles, Permissions = permissions, MustChangePassword = user.MustChangePassword,
            User = new UserInfoDto { Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, Email = user.Email }
        };
    }

    private async Task<LoginResultDto> FailedAsync(User? user, string? userName, string loginType, string code, string message, Stopwatch stopwatch)
    {
        await WriteLoginLogAsync(user?.Id, userName ?? user?.UserName, loginType, false, code, message, null, stopwatch);
        return new LoginResultDto { Success = false, Message = message };
    }

    private Task WriteLoginLogAsync(long? userId, string? userName, string type, bool success, string? code, string? message, Guid? sessionId, Stopwatch stopwatch) =>
        loginLogs.WriteAsync(new LoginLogEntry(userId?.ToString(), userName, type, success, code, message, sessionId, (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));

    private async Task<string> EnsureUniqueUserNameAsync(string baseName)
    {
        var name = baseName; var suffix = 1;
        while (await context.Users.AnyAsync(item => item.NormalizedUserName == Normalize(name))) name = $"{baseName}_{suffix++}";
        return name;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static void SetPassword(User user, string password, bool forceChange)
    {
        PasswordHelper.CreateHash(password, out var hash, out var salt);
        user.PasswordHash = hash; user.PasswordSalt = salt; user.PasswordAlgorithm = "PBKDF2-SHA256-100000";
        user.PasswordUpdatedAtUtc = DateTime.UtcNow; user.MustChangePassword = forceChange; user.UpdatedAtUtc = DateTime.UtcNow;
    }
    private static string GenerateRandomPassword(int length)
    {
        const string chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%&*?";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(item => chars[item % chars.Length]).ToArray());
    }
}
