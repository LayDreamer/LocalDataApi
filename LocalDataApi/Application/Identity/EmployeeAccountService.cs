using LocalDataApi.Application.Common;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity;

public interface IEmployeeAccountService
{
    Task<EmployeeAccountDto> BindUserAsync(long employeeId, BindEmployeeUserRequestDto dto, long? operatorUserId = null, CancellationToken ct = default);
    Task<EmployeeAccountDto> UnbindUserAsync(long employeeId, long? operatorUserId = null, CancellationToken ct = default);
    Task<EmployeeAccountDto> GetAccountAsync(long employeeId, CancellationToken ct = default);
}

public sealed class EmployeeAccountService(AppDbContext context, IAuditLogService auditLog) : IEmployeeAccountService
{
    public async Task<EmployeeAccountDto> BindUserAsync(long employeeId, BindEmployeeUserRequestDto dto, long? operatorUserId = null, CancellationToken ct = default)
    {
        if (dto.UserId <= 0) throw new ValidationException("用户 ID 必须大于 0");
        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct) ?? throw new NotFoundException("员工不存在");
        if (employee.UserId.HasValue) throw new ConflictException("员工已绑定系统账号");
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == dto.UserId, ct) ?? throw new NotFoundException("系统账号不存在");
        if (await context.Employees.AnyAsync(item => item.UserId == dto.UserId, ct)) throw new ConflictException("系统账号已绑定其他员工");
        employee.UserId = user.Id;
        employee.UpdatedTime = DateTime.UtcNow;
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 }) { throw new ConflictException("系统账号已绑定其他员工"); }
        await TryAuditAsync(operatorUserId, "Employee.BindUser", employee.Id, null, user.Id, ct);
        return ToDto(employee.Id, user);
    }

    public async Task<EmployeeAccountDto> UnbindUserAsync(long employeeId, long? operatorUserId = null, CancellationToken ct = default)
    {
        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, ct) ?? throw new NotFoundException("员工不存在");
        var previous = employee.UserId;
        if (!previous.HasValue) return Unbound(employee.Id);
        employee.UserId = null;
        employee.UpdatedTime = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        await TryAuditAsync(operatorUserId, "Employee.UnbindUser", employee.Id, previous, null, ct);
        return Unbound(employee.Id);
    }

    public async Task<EmployeeAccountDto> GetAccountAsync(long employeeId, CancellationToken ct = default)
    {
        var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employeeId, ct) ?? throw new NotFoundException("员工不存在");
        if (!employee.UserId.HasValue) return Unbound(employee.Id);
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employee.UserId.Value, ct);
        return user is null ? new EmployeeAccountDto { EmployeeId = employee.Id, IsBound = true, UserId = employee.UserId } : ToDto(employee.Id, user);
    }

    private static EmployeeAccountDto ToDto(long employeeId, Domain.Identity.User user) => new()
    {
        EmployeeId = employeeId, IsBound = true, UserId = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, IsActive = user.IsActive
    };
    private static EmployeeAccountDto Unbound(long employeeId) => new() { EmployeeId = employeeId, IsBound = false };
    private async Task TryAuditAsync(long? operatorId, string action, long employeeId, long? oldUserId, long? newUserId, CancellationToken ct)
    {
        try { await auditLog.LogAsync(operatorId?.ToString(), action, "Employee", employeeId.ToString(), new { EmployeeId = employeeId, OldUserId = oldUserId, NewUserId = newUserId }, ct); } catch { }
    }
}
