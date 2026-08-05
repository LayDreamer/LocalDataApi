using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

/// <summary>
/// 用户账户与登录用例(配合登录页使用)。
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly string _tokenSecret;

    // 连续登录失败达到该次数后锁定账号
    private const int MaxLoginFailCount = 5;
    // 锁定时长
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public UserService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        // 令牌签名密钥;生产环境必须通过配置注入,缺失时开发回退值仅用于本地调试
        _tokenSecret = configuration["Auth:Secret"]
                       ?? "LocalDataApi-Default-Dev-Secret-Change-Me";
    }

    /// <summary>
    /// 用户登录:校验账号密码,返回令牌与用户信息。
    /// </summary>
    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null)
    {
        var result = new LoginResultDto { Success = false };

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            result.Message = "用户名和密码不能为空";
            return result;
        }

        var user = await _context.用户管理
            .AsTracking()
            .FirstOrDefaultAsync(u => u.UserName != null &&
                                      u.UserName.ToLower() == request.UserName.ToLower());

        if (user == null)
        {
            result.Message = "用户不存在";
            return result;
        }

        if (user.IsActive != "true")
        {
            result.Message = "账号已被禁用";
            return result;
        }

        if (!string.IsNullOrEmpty(user.LockoutEnd) && DateTime.Parse(user.LockoutEnd) > DateTime.Now)
        {
            result.Message = "账号已锁定,请稍后再试";
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
            return result;
        }

        // 登录成功:重置失败计数并记录登录信息
        user.LoginFailCount = "0";
        user.LockoutEnd = null;
        user.LastLoginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        user.LastLoginIp = ipAddress;
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();

        result.Success = true;
        result.Message = "登录成功";
        result.Token = TokenHelper.CreateToken(user.UserName!, _tokenSecret);
        result.User = new UserInfoDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Email = user.Email
        };
        return result;
    }

    /// <summary>
    /// 注册新用户。
    /// </summary>
    public async Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
            return (false, "用户名和密码不能为空");

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
            CreateDate = now.ToString("yyyy-MM-dd HH:mm:ss"),
            ModifyDate = now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        await _context.SaveChangesAsync();
        return (true, "注册成功");
    }

    /// <summary>
    /// 修改密码(需校验原密码,用户名取自登录令牌)。
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(string userName, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "新密码不能为空");

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
        user.ModifyDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await _context.SaveChangesAsync();
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
        return (true, "资料更新成功");
    }
}
