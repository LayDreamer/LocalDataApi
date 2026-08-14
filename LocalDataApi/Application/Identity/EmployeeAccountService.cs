using LocalDataApi.Application.Common;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public interface IEmployeeAccountService
{
    Task<EmployeeAccountDto> BindUserAsync(long employeeId, BindEmployeeUserRequestDto dto, string? operatorUserId = null, CancellationToken ct = default);
    Task<EmployeeAccountDto> UnbindUserAsync(long employeeId, string? operatorUserId = null, CancellationToken ct = default);
    Task<EmployeeAccountDto> GetAccountAsync(long employeeId, CancellationToken ct = default);
}

public sealed class EmployeeAccountService(AppDbContext context, IAuditLogService auditLog) : IEmployeeAccountService
{
    public async Task<EmployeeAccountDto> BindUserAsync(
        long employeeId,
        BindEmployeeUserRequestDto dto,
        string? operatorUserId = null,
        CancellationToken ct = default)
    {
        if (dto.UserIdentityId <= 0)
            throw new ValidationException("用户身份编号必须大于 0");

        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("员工不存在");
        if (employee.UserIdentityId.HasValue)
            throw new ConflictException("员工已绑定系统账号");

        var user = await context.用户管理.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdentityId == dto.UserIdentityId, ct)
            ?? throw new NotFoundException("系统账号不存在");

        var userAlreadyBound = await context.Employees.AsNoTracking()
            .AnyAsync(item => item.UserIdentityId == dto.UserIdentityId, ct);
        if (userAlreadyBound)
            throw new ConflictException("系统账号已绑定其他员工");

        employee.UserIdentityId = user.IdentityId;
        employee.UpdatedTime = DateTime.Now;
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("系统账号已绑定其他员工");
        }

        await TryAuditAsync(operatorUserId, "Employee.BindUser", employee.Id, null, user.IdentityId, ct);
        return ToDto(employee.Id, user.IdentityId, user.Id, user.UserName, user.DisplayName, user.IsActive);
    }

    public async Task<EmployeeAccountDto> UnbindUserAsync(
        long employeeId,
        string? operatorUserId = null,
        CancellationToken ct = default)
    {
        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("员工不存在");
        var previousUserIdentityId = employee.UserIdentityId;

        if (!previousUserIdentityId.HasValue)
            return ToDto(employee.Id, null, null, null, null, null);

        employee.UserIdentityId = null;
        employee.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorUserId, "Employee.UnbindUser", employee.Id, previousUserIdentityId, null, ct);
        return ToDto(employee.Id, null, null, null, null, null);
    }

    public async Task<EmployeeAccountDto> GetAccountAsync(long employeeId, CancellationToken ct = default)
    {
        var employee = await context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("员工不存在");

        if (!employee.UserIdentityId.HasValue)
            return ToDto(employee.Id, null, null, null, null, null);

        var user = await context.用户管理.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdentityId == employee.UserIdentityId.Value, ct);
        return user is null
            ? ToDto(employee.Id, employee.UserIdentityId, null, null, null, null)
            : ToDto(employee.Id, user.IdentityId, user.Id, user.UserName, user.DisplayName, user.IsActive);
    }

    private static EmployeeAccountDto ToDto(
        long employeeId,
        long? userIdentityId,
        string? userId,
        string? userName,
        string? displayName,
        string? isActive) => new()
    {
        EmployeeId = employeeId,
        IsBound = userIdentityId.HasValue,
        UserIdentityId = userIdentityId,
        UserId = userId,
        UserName = userName,
        DisplayName = displayName,
        IsActive = isActive
    };

    private async Task TryAuditAsync(string? operatorUserId, string action, long employeeId, long? oldUserIdentityId, long? newUserIdentityId, CancellationToken ct)
    {
        try
        {
            await auditLog.LogAsync(
                operatorUserId,
                action,
                "Employee",
                employeeId.ToString(),
                new { EmployeeId = employeeId, OldUserIdentityId = oldUserIdentityId, NewUserIdentityId = newUserIdentityId },
                ct);
        }
        catch
        {
            // 审计失败不影响已成功的账号绑定操作。
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
