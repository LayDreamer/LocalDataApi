using LocalDataApi.Application.Common;
using LocalDataApi.Application.WeChatWork;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// 组织部门管理服务(部门树查询 + 企微部门同步)。
    /// </summary>
    public interface IDepartmentService
    {
        /// <summary>查询启用的部门树。</summary>
        Task<List<DepartmentTreeNodeDto>> GetDepartmentTreeAsync(CancellationToken ct = default);

        /// <summary>
        /// 从企业微信同步部门架构。
        /// 规则: 不存在→插入;存在→更新;企微已删除→IsActive=false(禁止物理删除)。
        /// </summary>
        Task<DepartmentSyncResultDto> SyncFromWeChatWorkAsync(string? operatorId = null, CancellationToken ct = default);
    }

    /// <summary>组织部门管理服务实现。</summary>
    public sealed class DepartmentService : IDepartmentService
    {
        private readonly AppDbContext _context;
        private readonly WeChatWorkOrganizationService _wxOrgService;
        private readonly IAuditLogService _auditLog;

        public DepartmentService(
            AppDbContext context,
            WeChatWorkOrganizationService wxOrgService,
            IAuditLogService auditLog)
        {
            _context = context;
            _wxOrgService = wxOrgService;
            _auditLog = auditLog;
        }

        public async Task<List<DepartmentTreeNodeDto>> GetDepartmentTreeAsync(CancellationToken ct = default)
        {
            var all = await _context.Departments.AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Path)
                .ToListAsync(ct);

            var nodes = all.Select(d => new DepartmentTreeNodeDto
            {
                Id = d.Id,
                Name = d.Name,
                ParentId = d.ParentId,
                Path = d.Path,
                IsActive = d.IsActive
            }).ToList();

            var dict = nodes.ToDictionary(n => n.Id);
            var roots = new List<DepartmentTreeNodeDto>();
            foreach (var node in nodes)
            {
                if (node.ParentId.HasValue && dict.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }
            return roots;
        }

        public async Task<DepartmentSyncResultDto> SyncFromWeChatWorkAsync(string? operatorId = null, CancellationToken ct = default)
        {
            var response = await _wxOrgService.GetDepartmentsAsync(ct);
            if (!response.IsSuccessful())
            {
                throw new ServiceException($"企业微信部门拉取失败: [{response.ErrorCode}] {response.ErrorMessage}");
            }

            var wxDepartments = response.DepartmentList?.ToList() ?? new List<CgibinDepartmentListResponse.Types.Department>();
            var now = DateTime.Now;
            int created = 0, updated = 0, disabled = 0;

            var localByCorpId = await _context.Departments.AsTracking()
                .ToDictionaryAsync(d => d.CorpDepartmentId, ct);
            var seenCorpIds = new HashSet<string>(StringComparer.Ordinal);

            // 第一遍:确保所有企微部门在本地存在(存在→更新,不存在→插入)
            foreach (var wx in wxDepartments)
            {
                var corpId = wx.DepartmentId.ToString();
                seenCorpIds.Add(corpId);
                var leader = wx.LeaderUserIdList?.FirstOrDefault();

                if (localByCorpId.TryGetValue(corpId, out var local))
                {
                    local.Name = wx.Name ?? string.Empty;
                    local.LeaderExternalUserId = leader;
                    local.IsActive = true;
                    local.ModifyTime = now;
                    updated++;
                }
                else
                {
                    var dept = new Department
                    {
                        Id = Guid.NewGuid(),
                        CorpDepartmentId = corpId,
                        Name = wx.Name ?? string.Empty,
                        LeaderExternalUserId = leader,
                        IsActive = true,
                        CreateTime = now,
                        ModifyTime = now
                    };
                    _context.Departments.Add(dept);
                    localByCorpId[corpId] = dept;
                    created++;
                }
            }

            // 第二遍:解析父级关系与路径(企微返回顺序不定,需二次遍历)
            foreach (var wx in wxDepartments)
            {
                var local = localByCorpId[wx.DepartmentId.ToString()];
                if (wx.ParentDepartmentId > 0 &&
                    localByCorpId.TryGetValue(wx.ParentDepartmentId.ToString(), out var parent))
                {
                    local.ParentId = parent.Id;
                    local.Path = (parent.Path ?? $"/{parent.CorpDepartmentId}") + $"/{local.CorpDepartmentId}";
                }
                else
                {
                    local.ParentId = null;
                    local.Path = $"/{local.CorpDepartmentId}";
                }
            }

            // 第三遍:本地存在但企微已不存在 → 软删除
            foreach (var local in localByCorpId.Values)
            {
                if (!seenCorpIds.Contains(local.CorpDepartmentId) && local.IsActive)
                {
                    local.IsActive = false;
                    local.ModifyTime = now;
                    disabled++;
                }
            }

            await _context.SaveChangesAsync(ct);

            try
            {
                await _auditLog.LogAsync(operatorId, "SyncDepartment", "Department", null,
                    new { Created = created, Updated = updated, Disabled = disabled, Total = wxDepartments.Count });
            }
            catch
            {
                // 忽略审计异常
            }

            return new DepartmentSyncResultDto
            {
                Total = wxDepartments.Count,
                Created = created,
                Updated = updated,
                Disabled = disabled,
                Message = $"同步完成:新增 {created},更新 {updated},停用 {disabled}"
            };
        }
    }
}
