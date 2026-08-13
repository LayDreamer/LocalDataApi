using LocalDataApi.Application.WeChatWork;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;

namespace LocalDataApi.Application.Identity;

/// <summary>
/// 用户账户与登录用例(配合登录页使用)。
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IWechatWorkUserService _wxUserService;
    private readonly IUserRoleService _userRoleService;
    private readonly AuthorizationService _authorization;
    private readonly string _defaultLoginRole;
    private readonly IAuthSessionService _sessions;
    private readonly ILoginLogService _loginLogs;

    // 连续登录失败达到该次数后锁定账号
    private const int MaxLoginFailCount = 5;
    // 锁定时长
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public UserService(
        AppDbContext context,
        IConfiguration configuration,
        IWechatWorkUserService wxUserService,
        IUserRoleService userRoleService,
        AuthorizationService authorization,
        IAuthSessionService sessions,
        ILoginLogService loginLogs)
    {
        _context = context;
        _wxUserService = wxUserService;
        _userRoleService = userRoleService;
        _authorization = authorization;
        _sessions = sessions;
        _loginLogs = loginLogs;
        // 新用户默认角色(企微免登自动建号时绑定)
        _defaultLoginRole = configuration.GetValue("Rbac:DefaultLoginRole", "VIEWER");
    }

    /// <summary>
    /// 用户登录:校验账号密码,返回令牌与用户信息。
    /// </summary>
    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LoginResultDto { Success = false };

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            result.Message = "用户名和密码不能为空";
            await WriteLoginLogAsync(null, request.UserName, "Password", false, "INVALID_REQUEST", result.Message, null, stopwatch);
            return result;
        }

        var user = await _context.用户管理
            .AsTracking()
            .FirstOrDefaultAsync(u => u.UserName != null &&
                                      u.UserName.ToLower() == request.UserName.ToLower());

        if (user == null)
        {
            result.Message = "用户不存在";
            await WriteLoginLogAsync(null, request.UserName, "Password", false, "USER_NOT_FOUND", result.Message, null, stopwatch);
            return result;
        }

        if (user.IsActive != "true")
        {
            result.Message = "账号已被禁用";
            await WriteLoginLogAsync(user.Id, user.UserName, "Password", false, "ACCOUNT_DISABLED", result.Message, null, stopwatch);
            return result;
        }

        if (!string.IsNullOrEmpty(user.LockoutEnd) && DateTime.Parse(user.LockoutEnd) > DateTime.Now)
        {
            result.Message = "账号已锁定,请稍后再试";
            await WriteLoginLogAsync(user.Id, user.UserName, "Password", false, "ACCOUNT_LOCKED", result.Message, null, stopwatch);
            return result;
        }

        if (!PasswordHelper.Verify(request.Password!, user.PasswordHash!, user.PasswordSalt!))
        {
            user.LoginFailCount = (int.Parse(user.LoginFailCount ?? "0") + 1).ToString();
            if (int.Parse(user.LoginFailCount ?? "0") >= MaxLoginFailCount)
            {
                user.LockoutEnd = DateTime.Now.Add(LockoutDuration).ToString("yyyy-MM-dd HH:mm:ss");
                result.Message = "密码错误次数过多,账号已锁定 15 分钟";
            }
            else
            {
                result.Message = "用户名或密码错误";
            }

            await _context.SaveChangesAsync();
            await WriteLoginLogAsync(user.Id, user.UserName, "Password", false, "PASSWORD_INVALID", result.Message, null, stopwatch);
            return result;
        }

        // 登录成功:重置失败计数并记录登录信息
        user.LoginFailCount = "0";
        user.LockoutEnd = null;
        user.LastLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        user.LastLoginIp = ipAddress;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();

        // 仅对「零角色」用户兜底绑定默认角色;已有角色的用户(如 ADMIN)不再强行附加默认 VIEWER
        var hasActiveRole = await _context.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == user.Id && ur.IsActive, default);
        if (!hasActiveRole && !string.IsNullOrWhiteSpace(_defaultLoginRole))
        {
            await _userRoleService.EnsureUserHasRoleAsync(user.Id!, _defaultLoginRole, null);
        }

        // 重新读取权限版本(绑定默认角色后已 +1)
        var freshUser = await _context.用户管理.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        var (roles, permissions) = await _authorization.GetUserRolesAndPermissionsAsync(user.Id!, default);

        result.Success = true;
        result.Message = "登录成功";
        var issue = await _sessions.CreateAsync(user, request.RememberMe, ipAddress, userAgent);
        result.Token = issue.AccessToken;
        result.User = new UserInfoDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Email = user.Email,
            PrimaryDepartmentName = user.PrimaryDepartmentName,
            Position = user.Position
        };
        result.UserId = user.Id;
        result.UserName = user.DisplayName ?? user.UserName;
        result.Roles = roles;
        result.Permissions = permissions;
        result.MustChangePassword = user.MustChangePassword;
        await WriteLoginLogAsync(user.Id, user.UserName, "Password", true, null, null, issue.SessionId, stopwatch);
        return result;
    }

    /// <summary>
    /// 企业微信工作台免登:通过授权 code 换取企微身份并登录;账号不存在时自动建号绑定。
    /// </summary>
    public async Task<LoginResultDto> LoginByWeChatWorkAsync(string code, string? ipAddress = null, string? userAgent = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LoginResultDto { Success = false };

        if (string.IsNullOrWhiteSpace(code))
        {
            result.Message = "授权 code 不能为空";
            await WriteLoginLogAsync(null, null, "WeChatWork", false, "INVALID_REQUEST", result.Message, null, stopwatch);
            return result;
        }

        // 1. code → 企业微信用户身份
        WechatWorkUserInfo wxUser;
        try
        {
            wxUser = await _wxUserService.GetUserInfoByCodeAsync(code);
        }
        catch (Exception ex)
        {
            result.Message = $"企业微信授权校验失败: {ex.Message}";
            await WriteLoginLogAsync(null, null, "WeChatWork", false, "WECHAT_AUTH_FAILED", "企业微信授权校验失败", null, stopwatch);
            return result;
        }

        if (string.IsNullOrWhiteSpace(wxUser.UserId))
        {
            result.Message = "未获取到企业微信用户身份";
            await WriteLoginLogAsync(null, null, "WeChatWork", false, "WECHAT_USER_NOT_FOUND", result.Message, null, stopwatch);
            return result;
        }

        // 2. 查找已绑定账号
        var user = await _context.用户管理.AsTracking()
            .FirstOrDefaultAsync(u => u.WeChatWorkUserId != null && u.WeChatWorkUserId == wxUser.UserId);

        // 3. 未绑定 → 自动建号
        if (user == null)
        {
            WechatWorkUserDetailInfo detail;
            try
            {
                detail = await _wxUserService.GetUserDetailByUserIdAsync(wxUser.UserId);
            }
            catch (Exception ex)
            {
                result.Message = $"获取企业微信用户详情失败: {ex.Message}";
                await WriteLoginLogAsync(null, wxUser.UserId, "WeChatWork", false, "WECHAT_USER_DETAIL_FAILED", "获取企业微信用户详情失败", null, stopwatch);
                return result;
            }

            var userName = await EnsureUniqueUserName($"wx_{wxUser.UserId}");
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userName,
                WeChatWorkUserId = wxUser.UserId,
                DisplayName = detail.Name,
                Email = detail.Email,
                PhoneNumber = detail.Mobile,
                Role = "User",
                IsActive = "true",
                CreateDate = now,
                ModifyDate = now
            };
            _context.用户管理.Add(user);
            await _context.SaveChangesAsync();

            // RBAC: 新用户(零角色)自动绑定默认角色(如 Viewer),不触发企微全量部门同步
            var hasActiveRole = await _context.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.UserId == user.Id && ur.IsActive, default);
            if (!hasActiveRole && !string.IsNullOrWhiteSpace(_defaultLoginRole))
            {
                await _userRoleService.EnsureUserHasRoleAsync(user.Id!, _defaultLoginRole, null);
            }
        }

        // 4. 复用登录风控(禁用 / 锁定)
        if (user.IsActive != "true")
        {
            result.Message = "账号已被禁用";
            await WriteLoginLogAsync(user.Id, user.UserName, "WeChatWork", false, "ACCOUNT_DISABLED", result.Message, null, stopwatch);
            return result;
        }
        if (!string.IsNullOrEmpty(user.LockoutEnd) && DateTime.Parse(user.LockoutEnd) > DateTime.Now)
        {
            result.Message = "账号已锁定,请稍后再试";
            await WriteLoginLogAsync(user.Id, user.UserName, "WeChatWork", false, "ACCOUNT_LOCKED", result.Message, null, stopwatch);
            return result;
        }

        // 5. 登录审计
        user.LastLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        user.LastLoginIp = ipAddress;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();

        // 6. 签发令牌(令牌携带用户ID与权限版本,不含完整权限列表)
        var freshUser = await _context.用户管理.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        var (roles, permissions) = await _authorization.GetUserRolesAndPermissionsAsync(user.Id!, default);

        result.Success = true;
        result.Message = "企业微信登录成功";
        var issue = await _sessions.CreateAsync(user, rememberMe: false, ipAddress, userAgent);
        result.Token = issue.AccessToken;
        result.User = new UserInfoDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Email = user.Email,
            PrimaryDepartmentName = user.PrimaryDepartmentName,
            Position = user.Position
        };
        result.UserId = user.Id;
        result.UserName = user.DisplayName ?? user.UserName;
        result.Roles = roles;
        result.Permissions = permissions;
        result.MustChangePassword = user.MustChangePassword;
        await WriteLoginLogAsync(user.Id, user.UserName, "WeChatWork", true, null, null, issue.SessionId, stopwatch);
        return result;
    }

    private Task WriteLoginLogAsync(string? userId, string? userName, string loginType, bool success,
        string? failReasonCode, string? failReason, Guid? sessionId, Stopwatch stopwatch)
        => _loginLogs.WriteAsync(new LoginLogEntry(
            userId, userName, loginType, success, failReasonCode, failReason, sessionId,
            (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));

    /// <summary>
    /// 注册新用户。
    /// </summary>
    public async Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
            return (false, "用户名和密码不能为空");

        var strength = PasswordValidation.Validate(dto.Password, dto.UserName);
        if (!strength.Valid)
            return (false, strength.Error);

        var exists = await _context.用户管理
            .AsNoTracking()
            .AnyAsync(u => u.UserName != null &&
                           u.UserName.ToLower() == dto.UserName.ToLower());
        if (exists)
            return (false, "该用户名已被注册");

        PasswordHelper.CreateHash(dto.Password!, out var hash, out var salt);

        var now = DateTime.Now;
        _context.用户管理.Add(new User
        {
            Id = Guid.NewGuid().ToString(),
            UserName = dto.UserName,
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = dto.DisplayName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role,
            IsActive = "true",
            MustChangePassword = false,
            CreateDate = now.ToString("yyyy-MM-dd HH:mm:ss"),
            ModifyDate = now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        await _context.SaveChangesAsync();
        return (true, "注册成功");
    }

    /// <summary>
    /// 修改密码(需校验原密码,用户名取自登录令牌;强制密码强度;成功后清除强制改密标志)。
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(string userName, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "新密码不能为空");

        var strength = PasswordValidation.Validate(newPassword, userName);
        if (!strength.Valid)
            return (false, strength.Error);

        var user = await _context.用户管理
            .AsTracking()
            .FirstOrDefaultAsync(u => u.UserName != null && u.UserName == userName);
        if (user == null)
            return (false, "用户不存在");

        if (!PasswordHelper.Verify(oldPassword, user.PasswordHash!, user.PasswordSalt!))
            return (false, "原密码错误");

        PasswordHelper.CreateHash(newPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();
        await _sessions.RevokeAllAsync(user.Id!, "password-changed");
        return (true, "密码修改成功");
    }

    /// <summary>
    /// 更新个人资料(显示名 / 邮箱 / 手机号)。
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateProfileAsync(string userName, UpdateProfileDto dto)
    {
        var user = await _context.用户管理
            .AsTracking()
            .FirstOrDefaultAsync(u => u.UserName != null && u.UserName == userName);
        if (user == null)
            return (false, "用户不存在");

        if (dto.DisplayName != null) user.DisplayName = dto.DisplayName;
        if (dto.Email != null) user.Email = dto.Email;
        if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();
        await _sessions.RevokeAllAsync(user.Id!, "password-reset");
        return (true, "资料更新成功");
    }

    /// <summary>
    /// 管理员重置密码:生成随机临时密码并覆盖存储,清空登录失败计数与锁定,返回明文临时密码。
    /// 不强制用户改密(MustChangePassword=false),用户可在「修改密码」中随时自行修改。
    /// </summary>
    public async Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "用户 ID 不能为空", null);

        var user = await _context.用户管理
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "用户不存在", null);

        // 生成满足强度策略的随机临时密码(大写/小写/数字/特殊字符,≥3 类);
        // 二次兜底:若生成的密码恰好不满足弱密码规则,则重新生成(最多 20 次)
        var tempPassword = GenerateRandomPassword(12);
        var attempt = 0;
        while (!PasswordValidation.Validate(tempPassword, user.UserName ?? string.Empty).Valid && attempt < 20)
        {
            tempPassword = GenerateRandomPassword(12);
            attempt++;
        }

        PasswordHelper.CreateHash(tempPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        // 不强制改密:用户拿到临时密码后可自行决定是否立即修改
        user.MustChangePassword = false;
        // 重置失败计数并解除锁定,确保用户忘记密码多次尝试后仍能立即登录
        user.LoginFailCount = "0";
        user.LockoutEnd = null;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();

        return (true, "密码重置成功", tempPassword);
    }

    /// <summary>
    /// 生成随机临时密码:从 4 类字符(大写/小写/数字/特殊)中各取至少 1 个,总长度 length,
    /// 并对结果做 Fisher–Yates 洗牌,避免弱密码规则(连续/重复序列)拦截。
    /// </summary>
    private static string GenerateRandomPassword(int length)
    {
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digit = "23456789";
        const string special = "!@#$%&*?";
        var pools = new[] { lower, upper, digit, special };

        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[1];

        char RandomChar(string pool)
        {
            rng.GetBytes(bytes);
            return pool[bytes[0] % pool.Length];
        }

        var sb = new StringBuilder();
        // 每类至少取 1 个,保证 ≥3 类(实际 4 类)通过强度校验
        foreach (var pool in pools)
        {
            sb.Append(RandomChar(pool));
        }
        // 剩余长度随机填充(每次随机选一个池再取字符)
        while (sb.Length < length)
        {
            rng.GetBytes(bytes);
            var pool = pools[bytes[0] % pools.Length];
            sb.Append(RandomChar(pool));
        }

        // Fisher–Yates 洗牌,打散固定前缀
        var arr = sb.ToString().ToCharArray();
        for (int i = arr.Length - 1; i > 0; i--)
        {
            rng.GetBytes(bytes);
            int j = bytes[0] % (i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return new string(arr);
    }

    /// <summary>
    /// 生成不与其他账号冲突的用户名(自动建号时调用)。
    /// </summary>
    private async Task<string> EnsureUniqueUserName(string baseName)
    {
        var name = baseName;
        var suffix = 1;
        while (await _context.用户管理.AsNoTracking().AnyAsync(u => u.UserName != null && u.UserName == name))
        {
            name = $"{baseName}_{suffix++}";
        }
        return name;
    }
}
