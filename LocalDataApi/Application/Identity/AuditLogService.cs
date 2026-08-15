using System.Text.Json;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 审计日志服务。记录用户角色变化、角色权限变化、部门同步等敏感操作。
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>记录一条审计日志。</summary>
        /// <param name="userId">操作人用户ID(系统自动操作为 null)。</param>
        /// <param name="action">行为标识(如 AssignUserRole)。</param>
        /// <param name="targetType">对象类型(如 User / Role / Department)。</param>
        /// <param name="targetId">对象ID。</param>
        /// <param name="content">变更内容(可为对象,自动 JSON 序列化)。</param>
        /// <param name="ct">取消令牌。</param>
        Task LogAsync(string? userId, string action, string targetType, string? targetId, object? content = null, CancellationToken ct = default);
    }

    /// <summary>审计日志服务实现。</summary>
    public sealed class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;

        public AuditLogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string? userId, string action, string targetType, string? targetId, object? content = null, CancellationToken ct = default)
        {
            var hasPlatformUserId = long.TryParse(userId, out var platformUserId);
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                // New platform operations use Sys_User.Id.  Keep non-numeric values only
                // for historical/system callers that have not crossed the cutover yet.
                UserId = hasPlatformUserId ? null : userId,
                PlatformUserId = hasPlatformUserId ? platformUserId : null,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Content = content == null ? null : JsonSerializer.Serialize(content),
                CreateTime = DateTime.Now
            });
            await _context.SaveChangesAsync(ct);
        }
    }
}
