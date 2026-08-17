using LocalDataApi.Dto;

namespace LocalDataApi.Application.Identity;

public interface IUserService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null);
    Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto);
    Task<(bool Success, string Message)> ChangePasswordAsync(string userName, string oldPassword, string newPassword);
    Task<(bool Success, string Message)> UpdateProfileAsync(string userName, UpdateProfileDto dto);
    Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(long userId);
    Task<LoginResultDto> LoginByWeChatWorkAsync(string code, string? ipAddress = null, string? userAgent = null);
}
