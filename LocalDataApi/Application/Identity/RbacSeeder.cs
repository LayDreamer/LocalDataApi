using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Identity
{
    /// <summary>
    /// RBAC 初始化数据(启动时执行,幂等)。
    /// 负责: 权限点初始化(PermissionSeeder) + 默认角色初始化(RoleSeeder) + 角色权限矩阵绑定 + 历史 Admin 用户兜底绑定。
    /// 遵循约定: 不使用 Migration 维护业务权限数据;按 Code 检查存在性,可重复执行。
    /// </summary>
    public sealed class RbacSeeder
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RbacSeeder> _logger;
        private readonly IConfiguration _configuration;

        public RbacSeeder(AppDbContext context, ILogger<RbacSeeder> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// 执行初始化(幂等)。任何失败仅记录警告,不阻断应用启动(便于 DB-First 部署分步执行 SQL 脚本)。
        /// </summary>
        public async Task SeedAsync(CancellationToken ct = default)
        {
            try
            {
                await EnsurePermissionsAsync(ct);
                var roles = await EnsureRolesAsync(ct);
                await EnsureRolePermissionsAsync(roles, ct);
                await EnsureLegacyAdminUsersAsync(ct);
                await EnsureDefaultAdminUserAsync(ct);
                await EnsureMenusAsync(ct);
                await EnsurePmcMenuIntegrationAsync(ct);
                await EnsureDictionaryMenuAsync(ct);
                await EnsureNumberRuleMenuAsync(ct);
                await EnsureAttachmentMenuAsync(ct);
                _logger.LogInformation("RBAC 初始化数据检查完成。");
            }
            catch (Exception ex)
            {
                // RBAC 表可能尚未通过 SQL 脚本创建;降级为警告,不影响原有业务
                _logger.LogWarning(ex, "RBAC 初始化数据执行失败(请确认已执行 DatabaseScripts/20260808_RbacTables.sql): {Message}", ex.Message);
            }
        }

        // ---------- 1. 权限点初始化(幂等) ----------
        private async Task EnsureMenusAsync(CancellationToken ct)
        {
            var manufacturingCenterId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var pmcId = Guid.Parse("10000000-0000-0000-0000-000000000002");
            var definitions = new[]
            {
                new Menu { Id = manufacturingCenterId, Name = "制造中心", Path = "/manufacturing", Icon = "Factory", Type = "Directory", Sort = 10 },
                new Menu { Id = pmcId, ParentId = manufacturingCenterId, Name = "PMC", Path = "/pmc", Icon = "Calendar", Type = "Directory", Sort = 10 },
                new Menu { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), ParentId = pmcId, Name = "生产计划", Path = "production-plan", Component = "pmc/production-plan", Type = "Menu", Sort = 10 },
                new Menu { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), ParentId = pmcId, Name = "订单管理", Path = "order-management", Component = "pmc/order-management", Type = "Menu", Sort = 20 },
                new Menu { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), ParentId = pmcId, Name = "工单管理", Path = "work-order-management", Component = "pmc/work-order-management", Type = "Menu", Sort = 30 }
            };
            var definitionIds = definitions.Select(menu => menu.Id).ToArray();
            var existingIds = await _context.Menus.AsNoTracking()
                .Where(menu => definitionIds.Contains(menu.Id))
                .Select(menu => menu.Id)
                .ToHashSetAsync(ct);
            var now = DateTime.Now;
            foreach (var menu in definitions.Where(menu => !existingIds.Contains(menu.Id)))
            {
                menu.CreatedTime = now;
                menu.UpdatedTime = now;
                _context.Menus.Add(menu);
            }

            if (existingIds.Count < definitions.Length)
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("基础菜单初始化完成：新增 {Count} 条。", definitions.Length - existingIds.Count);
            }
        }

        private async Task EnsurePmcMenuIntegrationAsync(CancellationToken ct)
        {
            var manufacturingCenterId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var pmcId = Guid.Parse("10000000-0000-0000-0000-000000000002");
            var deliveryReviewId = Guid.Parse("10000000-0000-0000-0000-000000000003");
            var workOrderTrackingId = Guid.Parse("10000000-0000-0000-0000-000000000004");
            var obsoleteMenuId = Guid.Parse("10000000-0000-0000-0000-000000000005");
            var ids = new[] { manufacturingCenterId, pmcId, deliveryReviewId, workOrderTrackingId, obsoleteMenuId };
            var menus = await _context.Menus.Where(menu => ids.Contains(menu.Id)).ToDictionaryAsync(menu => menu.Id, ct);
            if (!menus.TryGetValue(manufacturingCenterId, out var manufacturingCenter) ||
                !menus.TryGetValue(pmcId, out var pmc) ||
                !menus.TryGetValue(deliveryReviewId, out var deliveryReview) ||
                !menus.TryGetValue(workOrderTrackingId, out var workOrderTracking))
            {
                _logger.LogWarning("PMC menu integration skipped because the base menu seed did not complete.");
                return;
            }

            var now = DateTime.Now;
            manufacturingCenter.Name = "Manufacturing Center";
            manufacturingCenter.Path = "/manufacturing";
            manufacturingCenter.Component = null;
            manufacturingCenter.Icon = "AppstoreOutlined";
            manufacturingCenter.Type = "Directory";
            manufacturingCenter.Sort = 10;
            manufacturingCenter.Status = true;
            manufacturingCenter.UpdatedTime = now;

            pmc.ParentId = manufacturingCenterId;
            pmc.Name = "PMC";
            pmc.Path = "/pmc";
            pmc.Component = null;
            pmc.Icon = "LineChartOutlined";
            pmc.Type = "Directory";
            pmc.Sort = 10;
            pmc.Status = true;
            pmc.UpdatedTime = now;

            deliveryReview.ParentId = pmcId;
            deliveryReview.Name = "DeliveryReview";
            deliveryReview.Path = "/pmc/deliveryreview";
            deliveryReview.Component = "PMC/DeliveryReview/DeliveryReview";
            deliveryReview.Icon = "FileDoneOutlined";
            deliveryReview.Type = "Menu";
            deliveryReview.Sort = 10;
            deliveryReview.Status = true;
            deliveryReview.UpdatedTime = now;

            workOrderTracking.ParentId = pmcId;
            workOrderTracking.Name = "WorkOrderTracking";
            workOrderTracking.Path = "/pmc/workOrderTracking";
            workOrderTracking.Component = "PMC/WorkOrderTracking/WorkOrderTracking";
            workOrderTracking.Icon = "ContainerOutlined";
            workOrderTracking.Type = "Menu";
            workOrderTracking.Sort = 20;
            workOrderTracking.Status = true;
            workOrderTracking.UpdatedTime = now;

            if (menus.TryGetValue(obsoleteMenuId, out var obsoleteMenu))
            {
                obsoleteMenu.Status = false;
                obsoleteMenu.UpdatedTime = now;
            }
            await _context.SaveChangesAsync(ct);

            var expectedBindings = new[]
            {
                (MenuId: deliveryReviewId, PermissionCode: PermissionCodes.DeliveryReviewView),
                (MenuId: workOrderTrackingId, PermissionCode: PermissionCodes.WorkOrderView)
            };
            var currentBindings = await _context.MenuPermissions.AsNoTracking()
                .Where(binding => expectedBindings.Select(expected => expected.MenuId).Contains(binding.MenuId))
                .Select(binding => new { binding.MenuId, binding.PermissionCode })
                .ToListAsync(ct);
            foreach (var expected in expectedBindings.Where(expected => !currentBindings.Any(binding => binding.MenuId == expected.MenuId && binding.PermissionCode == expected.PermissionCode)))
            {
                _context.MenuPermissions.Add(new MenuPermission
                {
                    Id = Guid.NewGuid(), MenuId = expected.MenuId, PermissionCode = expected.PermissionCode, CreatedTime = now
                });
            }
            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// 数据字典中心菜单种子(顶级菜单,绑定 System.Dictionary.View 权限)。
        /// Guid 前缀 20000000- 与 PMC 菜单(10000000-)区分,避免冲突。
        /// </summary>
        private async Task EnsureDictionaryMenuAsync(CancellationToken ct)
        {
            var dictionaryId = Guid.Parse("20000000-0000-0000-0000-000000000001");
            var dictionary = await _context.Menus.FirstOrDefaultAsync(menu => menu.Id == dictionaryId, ct);
            var now = DateTime.Now;
            if (dictionary is null)
            {
                dictionary = new Menu
                {
                    Id = dictionaryId,
                    Name = "数据字典中心",
                    Path = "/system/dictionary",
                    Component = "System/Dictionary",
                    Icon = "DatabaseOutlined",
                    Type = "Menu",
                    Sort = 50,
                    Status = true,
                    CreatedTime = now,
                    UpdatedTime = now
                };
                _context.Menus.Add(dictionary);
            }
            else
            {
                dictionary.Name = "数据字典中心";
                dictionary.Path = "/system/dictionary";
                dictionary.Component = "System/Dictionary";
                dictionary.Icon = "DatabaseOutlined";
                dictionary.Type = "Menu";
                dictionary.Sort = 50;
                dictionary.Status = true;
                dictionary.UpdatedTime = now;
            }
            await _context.SaveChangesAsync(ct);

            // 绑定菜单-权限(System.Dictionary.View,普通用户需此权限才可见)
            var expectedPermissionCode = PermissionCodes.SystemDictionaryView;
            var hasBinding = await _context.MenuPermissions.AsNoTracking()
                .AnyAsync(binding => binding.MenuId == dictionaryId && binding.PermissionCode == expectedPermissionCode, ct);
            if (!hasBinding)
            {
                _context.MenuPermissions.Add(new MenuPermission
                {
                    Id = Guid.NewGuid(),
                    MenuId = dictionaryId,
                    PermissionCode = expectedPermissionCode,
                    CreatedTime = now
                });
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("数据字典菜单权限已绑定: System.Dictionary.View");
            }
        }

        /// <summary>
        /// 统一编码规则中心菜单种子(顶级菜单,绑定 Platform.NumberRule.View 权限)。
        /// 页面路由 /system/number-rule,前端组件 System/NumberRule。
        /// 注意: 具体业务默认规则(如 DeliveryReview)由 NumberRuleSeeder 播种,不在此处。
        /// </summary>
        private async Task EnsureNumberRuleMenuAsync(CancellationToken ct)
        {
            var numberRuleId = Guid.Parse("20000000-0000-0000-0000-000000000002");
            var numberRule = await _context.Menus.FirstOrDefaultAsync(menu => menu.Id == numberRuleId, ct);
            var now = DateTime.Now;
            if (numberRule is null)
            {
                numberRule = new Menu
                {
                    Id = numberRuleId,
                    Name = "编码规则中心",
                    Path = "/system/number-rule",
                    Component = "System/NumberRule",
                    Icon = "NumberOutlined",
                    Type = "Menu",
                    Sort = 60,
                    Status = true,
                    CreatedTime = now,
                    UpdatedTime = now
                };
                _context.Menus.Add(numberRule);
            }
            else
            {
                numberRule.Name = "编码规则中心";
                numberRule.Path = "/system/number-rule";
                numberRule.Component = "System/NumberRule";
                numberRule.Icon = "NumberOutlined";
                numberRule.Type = "Menu";
                numberRule.Sort = 60;
                numberRule.Status = true;
                numberRule.UpdatedTime = now;
            }
            await _context.SaveChangesAsync(ct);

            // 绑定菜单-权限(Platform.NumberRule.View,普通用户需此权限才可见)
            var expectedPermissionCode = PermissionCodes.PlatformNumberRuleView;
            var hasBinding = await _context.MenuPermissions.AsNoTracking()
                .AnyAsync(binding => binding.MenuId == numberRuleId && binding.PermissionCode == expectedPermissionCode, ct);
            if (!hasBinding)
            {
                _context.MenuPermissions.Add(new MenuPermission
                {
                    Id = Guid.NewGuid(),
                    MenuId = numberRuleId,
                    PermissionCode = expectedPermissionCode,
                    CreatedTime = now
                });
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("编码规则中心菜单权限已绑定: Platform.NumberRule.View");
            }
        }

        /// <summary>
        /// 统一附件中心菜单种子(顶级菜单,绑定 Platform.Attachment.View 权限)。
        /// 页面路由 /system/attachment,前端组件 System/Attachment。
        /// 仅在实现管理页时播种(本工作包已实现 Attachment.vue)。
        /// </summary>
        private async Task EnsureAttachmentMenuAsync(CancellationToken ct)
        {
            var attachmentId = Guid.Parse("20000000-0000-0000-0000-000000000003");
            var attachmentMenu = await _context.Menus.FirstOrDefaultAsync(menu => menu.Id == attachmentId, ct);
            var now = DateTime.Now;
            if (attachmentMenu is null)
            {
                attachmentMenu = new Menu
                {
                    Id = attachmentId,
                    Name = "附件中心",
                    Path = "/system/attachment",
                    Component = "System/Attachment",
                    Icon = "PaperClipOutlined",
                    Type = "Menu",
                    Sort = 70,
                    Status = true,
                    CreatedTime = now,
                    UpdatedTime = now
                };
                _context.Menus.Add(attachmentMenu);
            }
            else
            {
                attachmentMenu.Name = "附件中心";
                attachmentMenu.Path = "/system/attachment";
                attachmentMenu.Component = "System/Attachment";
                attachmentMenu.Icon = "PaperClipOutlined";
                attachmentMenu.Type = "Menu";
                attachmentMenu.Sort = 70;
                attachmentMenu.Status = true;
                attachmentMenu.UpdatedTime = now;
            }
            await _context.SaveChangesAsync(ct);

            // 绑定菜单-权限(Platform.Attachment.View,普通用户需此权限才可见)
            var expectedPermissionCode = PermissionCodes.PlatformAttachmentView;
            var hasBinding = await _context.MenuPermissions.AsNoTracking()
                .AnyAsync(binding => binding.MenuId == attachmentId && binding.PermissionCode == expectedPermissionCode, ct);
            if (!hasBinding)
            {
                _context.MenuPermissions.Add(new MenuPermission
                {
                    Id = Guid.NewGuid(),
                    MenuId = attachmentId,
                    PermissionCode = expectedPermissionCode,
                    CreatedTime = now
                });
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("附件中心菜单权限已绑定: Platform.Attachment.View");
            }
        }

        private async Task EnsurePermissionsAsync(CancellationToken ct)
        {
            var defs = BuildPermissionDefinitions();
            var existingCodes = await _context.Permissions.AsNoTracking()
                .Select(p => p.Code).ToHashSetAsync(ct);

            var now = DateTime.Now;
            foreach (var def in defs)
            {
                if (existingCodes.Contains(def.Code))
                    continue;
                _context.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    Module = def.Module,
                    Resource = def.Resource,
                    Action = def.Action,
                    DisplayName = def.DisplayName,
                    Description = def.Description,
                    Enabled = true,
                    CreateTime = now,
                    ModifyTime = now
                });
            }
            await _context.SaveChangesAsync(ct);
        }

        /// <summary>全量权限定义(与 PermissionCodes.cs / 权限编码字典保持一致)。</summary>
        private static IReadOnlyList<(string Code, string Module, string Resource, string Action, string DisplayName, string Description)> BuildPermissionDefinitions()
        {
            return new List<(string, string, string, string, string, string)>
            {
                // ===== Identity 用户管理 =====
                (PermissionCodes.UserView, "Identity", "User", "View", "查看用户", "查看用户列表与详情"),
                (PermissionCodes.UserCreate, "Identity", "User", "Create", "新增用户", "创建系统用户"),
                (PermissionCodes.UserUpdate, "Identity", "User", "Update", "修改用户", "修改用户资料"),
                (PermissionCodes.UserDelete, "Identity", "User", "Delete", "删除用户", "删除系统用户"),
                (PermissionCodes.UserAssignRole, "Identity", "User", "AssignRole", "分配用户角色", "为用户分配/调整角色"),
                (PermissionCodes.EmployeeBindUser, "Identity", "Employee", "BindUser", "绑定员工账号", "为员工绑定或解绑系统账号"),
                // ===== Identity 角色管理 =====
                (PermissionCodes.RoleView, "Identity", "Role", "View", "查看角色", "查看角色列表与详情"),
                (PermissionCodes.RoleCreate, "Identity", "Role", "Create", "新增角色", "创建角色"),
                (PermissionCodes.RoleUpdate, "Identity", "Role", "Update", "修改角色", "修改角色信息"),
                (PermissionCodes.RoleDelete, "Identity", "Role", "Delete", "删除角色", "删除角色"),
                (PermissionCodes.RoleAssignPermission, "Identity", "Role", "AssignPermission", "分配角色权限", "为角色分配权限点"),
                // ===== Identity 权限管理 =====
                (PermissionCodes.PermissionView, "Identity", "Permission", "View", "查看权限", "查看权限字典"),
                (PermissionCodes.PermissionUpdate, "Identity", "Permission", "Update", "维护权限", "启用/停用权限点"),
                // ===== Identity 部门管理 =====
                (PermissionCodes.DepartmentView, "Identity", "Department", "View", "查看部门", "查看组织部门树"),
                (PermissionCodes.DepartmentSync, "Identity", "Department", "Sync", "同步部门", "从企业微信同步组织架构"),
                (PermissionCodes.PlatformMenuView, "Platform", "Menu", "View", "查看菜单", "查询后台菜单列表与树"),
                (PermissionCodes.PlatformMenuCreate, "Platform", "Menu", "Create", "新增菜单", "创建后台菜单"),
                (PermissionCodes.PlatformMenuUpdate, "Platform", "Menu", "Update", "修改菜单", "修改后台菜单"),
                (PermissionCodes.PlatformMenuDelete, "Platform", "Menu", "Delete", "删除菜单", "逻辑删除后台菜单"),
                (PermissionCodes.PlatformPositionView, "Platform", "Position", "View", "查看岗位", "查询岗位列表"),
                (PermissionCodes.PlatformPositionCreate, "Platform", "Position", "Create", "新增岗位", "创建岗位"),
                (PermissionCodes.PlatformPositionEdit, "Platform", "Position", "Edit", "修改岗位", "修改岗位信息或启停状态"),
                (PermissionCodes.PlatformPositionDelete, "Platform", "Position", "Delete", "停用岗位", "停用岗位"),
                (PermissionCodes.SystemDictionaryView, "System", "Dictionary", "View", "查看字典", "查询数据字典类型与字典项"),
                (PermissionCodes.SystemDictionaryCreate, "System", "Dictionary", "Create", "新增字典", "创建字典类型/字典项"),
                (PermissionCodes.SystemDictionaryUpdate, "System", "Dictionary", "Update", "修改字典", "修改字典类型/字典项"),
                (PermissionCodes.SystemDictionaryDelete, "System", "Dictionary", "Delete", "删除字典", "删除字典类型/字典项"),
                (PermissionCodes.PlatformNumberRuleView, "Platform", "NumberRule", "View", "查看编码规则", "查询统一业务编码规则配置"),
                (PermissionCodes.PlatformNumberRuleCreate, "Platform", "NumberRule", "Create", "新增编码规则", "创建编码规则"),
                (PermissionCodes.PlatformNumberRuleUpdate, "Platform", "NumberRule", "Update", "修改编码规则", "修改编码规则配置或重置流水号"),
                (PermissionCodes.PlatformAttachmentView, "Platform", "Attachment", "View", "查看附件", "查询统一附件列表与下载"),
                (PermissionCodes.PlatformAttachmentUpload, "Platform", "Attachment", "Upload", "上传附件", "上传本地附件到统一附件中心"),
                (PermissionCodes.PlatformAttachmentDelete, "Platform", "Attachment", "Delete", "删除附件", "删除附件记录与物理文件"),
                (PermissionCodes.PlatformLoginLogView, "Platform", "LoginLog", "View", "查看登录日志", "查询登录审计日志"),
                (PermissionCodes.PlatformOperationLogView, "Platform", "OperationLog", "View", "查看操作日志", "查询操作审计日志"),
                (PermissionCodes.PlatformDataChangeLogView, "Platform", "DataChangeLog", "View", "查看数据变更日志", "查询数据变更审计日志"),
                // ===== PMC 排产管理 =====
                (PermissionCodes.ScheduleView, "PMC", "Schedule", "View", "查看排产", "查看排产数据"),
                (PermissionCodes.ScheduleCreate, "PMC", "Schedule", "Create", "新建排产", "创建排产分析"),
                (PermissionCodes.ScheduleUpdate, "PMC", "Schedule", "Update", "修改排产", "修改排产数据"),
                (PermissionCodes.ScheduleDelete, "PMC", "Schedule", "Delete", "删除排产", "删除排产数据"),
                (PermissionCodes.SchedulePublish, "PMC", "Schedule", "Publish", "发布排产", "发布排产计划"),
                (PermissionCodes.ScheduleExport, "PMC", "Schedule", "Export", "导出排产", "导出排产数据"),
                // ===== PMC 工单管理 =====
                (PermissionCodes.WorkOrderView, "PMC", "WorkOrder", "View", "查看工单", "查看工单数据"),
                (PermissionCodes.WorkOrderCreate, "PMC", "WorkOrder", "Create", "新建工单", "创建工单"),
                (PermissionCodes.WorkOrderUpdate, "PMC", "WorkOrder", "Update", "修改工单", "修改工单数据"),
                (PermissionCodes.WorkOrderDelete, "PMC", "WorkOrder", "Delete", "删除工单", "删除工单数据"),
                (PermissionCodes.WorkOrderClose, "PMC", "WorkOrder", "Close", "关闭工单", "关闭工单"),
                // ===== PMC 交期评审 =====
                (PermissionCodes.DeliveryReviewView, "PMC", "DeliveryReview", "View", "查看交期评审", "查看交期评审数据"),
                (PermissionCodes.DeliveryReviewCreate, "PMC", "DeliveryReview", "Create", "新建交期评审", "创建交期评审"),
                (PermissionCodes.DeliveryReviewUpdate, "PMC", "DeliveryReview", "Update", "修改交期评审", "修改交期评审数据"),
                (PermissionCodes.DeliveryReviewApprove, "PMC", "DeliveryReview", "Approve", "审核交期评审", "通过交期评审"),
                (PermissionCodes.DeliveryReviewReject, "PMC", "DeliveryReview", "Reject", "驳回交期评审", "驳回交期评审"),
                // ===== PMC 外产管理 =====
                (PermissionCodes.ExternalProductionView, "PMC", "ExternalProduction", "View", "查看外产", "查看外产数据"),
                (PermissionCodes.ExternalProductionCreate, "PMC", "ExternalProduction", "Create", "新建外产", "创建外产记录"),
                (PermissionCodes.ExternalProductionUpdate, "PMC", "ExternalProduction", "Update", "修改外产", "修改外产数据"),
                (PermissionCodes.ExternalProductionDelete, "PMC", "ExternalProduction", "Delete", "删除外产", "删除外产数据"),
                (PermissionCodes.ExternalProductionApprove, "PMC", "ExternalProduction", "Approve", "审核外产", "审核外产记录"),
                (PermissionCodes.ProductView, "PMC", "Product", "View", "查看产品资料", "查看 PMC 产品、装配及搜索数据"),
                (PermissionCodes.BlfParameterView, "BLF", "Parameter", "View", "查看比例阀参数", "查看比例阀参数"),
                (PermissionCodes.BlfParameterCreate, "BLF", "Parameter", "Create", "新建比例阀参数", "创建比例阀参数"),
                (PermissionCodes.BlfParameterUpdate, "BLF", "Parameter", "Update", "修改比例阀参数", "更新比例阀参数"),
                (PermissionCodes.BlfParameterDelete, "BLF", "Parameter", "Delete", "删除比例阀参数", "删除比例阀参数"),
                // ===== ERP 工单 =====
                (PermissionCodes.ErpWorkOrderView, "ERP", "WorkOrder", "View", "查看ERP工单", "查看ERP工单数据"),
                (PermissionCodes.ErpWorkOrderUpdate, "ERP", "WorkOrder", "Update", "修改ERP工单", "修改ERP工单数据"),
                // ===== ERP 物料 =====
                (PermissionCodes.ErpMaterialView, "ERP", "Material", "View", "查看物料", "查看物料数据"),
                (PermissionCodes.ErpMaterialImport, "ERP", "Material", "Import", "导入物料", "导入物料数据"),
                (PermissionCodes.ErpMaterialExport, "ERP", "Material", "Export", "导出物料", "导出物料数据"),
                (PermissionCodes.ErpUserView, "ERP", "User", "View", "查看 ERP 用户", "查看 ERP 用户列表"),
                (PermissionCodes.ErpUserValidate, "ERP", "User", "Validate", "校验 ERP 用户", "校验 ERP 用户账号"),
                // ===== WeChatWork =====
                (PermissionCodes.WeChatWorkMessageSend, "WeChatWork", "Message", "Send", "发送企微消息", "通过企业微信发送消息"),
                (PermissionCodes.WeChatWorkDepartmentView, "WeChatWork", "Department", "View", "查看企微部门", "查看企业微信部门"),
                (PermissionCodes.WeChatWorkDepartmentSync, "WeChatWork", "Department", "Sync", "同步企微部门", "同步企业微信部门"),
                (PermissionCodes.WeChatWorkUserSync, "WeChatWork", "User", "Sync", "同步企微用户", "同步企业微信用户"),
                (PermissionCodes.WeChatWorkSmartSheetView, "WeChatWork", "SmartSheet", "View", "查看智能表格", "查看企业微信智能表格"),
                (PermissionCodes.WeChatWorkSmartSheetSync, "WeChatWork", "SmartSheet", "Sync", "同步智能表格", "同步企业微信智能表格"),
                (PermissionCodes.WeChatWorkUserView, "WeChatWork", "User", "View", "查看企微用户", "查询企业微信用户"),
                (PermissionCodes.WeChatWorkGroupChatView, "WeChatWork", "GroupChat", "View", "查看企微群聊", "查询企业微信群聊"),
                (PermissionCodes.WeChatWorkJsSdkView, "WeChatWork", "JsSdk", "View", "获取企微 JS-SDK 配置", "获取企业微信 JS-SDK 签名配置"),
                (PermissionCodes.SystemTestAccess, "System", "Test", "Access", "访问测试页面", "访问开发测试功能")
            };
        }

        // ---------- 2. 默认角色初始化(幂等) ----------
        private async Task<Dictionary<string, Role>> EnsureRolesAsync(CancellationToken ct)
        {
            var now = DateTime.Now;
            var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

            var defs = new[]
            {
                (Code: "ADMIN", Name: "系统管理员", DisplayName: "系统管理员", Description: "系统管理员,拥有全部权限", IsSystem: true),
                (Code: "PMC_ADMIN", Name: "PMC管理员", DisplayName: "PMC管理员", Description: "PMC模块管理员,拥有 PMC 全部权限", IsSystem: false),
                (Code: "SCHEDULER", Name: "排产员", DisplayName: "排产员", Description: "负责排产操作", IsSystem: false),
                (Code: "REVIEWER", Name: "审核员", DisplayName: "审核员", Description: "负责交期评审审核", IsSystem: false),
                (Code: "OPERATOR", Name: "操作员", DisplayName: "操作员", Description: "生产操作人员", IsSystem: false),
                (Code: "VIEWER", Name: "查看者", DisplayName: "查看者", Description: "默认普通用户,仅拥有查看权限", IsSystem: false)
            };

            foreach (var def in defs)
            {
                var role = await _context.Roles.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Code == def.Code, ct);
                if (role == null)
                {
                    role = new Role
                    {
                        Id = Guid.NewGuid(),
                        Code = def.Code,
                        Name = def.Name,
                        DisplayName = def.DisplayName,
                        Description = def.Description,
                        IsBuiltIn = true,
                        IsSystem = def.IsSystem,
                        Enabled = true,
                        CreateTime = now,
                        ModifyTime = now
                    };
                    _context.Roles.Add(role);
                    await _context.SaveChangesAsync(ct);
                    _logger.LogInformation("RBAC 角色初始化: {Code}", def.Code);
                }
                roles[def.Code] = role;
            }

            return roles;
        }

        // ---------- 3. 角色权限矩阵绑定(幂等) ----------
        private async Task EnsureRolePermissionsAsync(Dictionary<string, Role> roles, CancellationToken ct)
        {
            var permissions = await _context.Permissions.AsNoTracking().ToListAsync(ct);
            var permissionByCode = permissions.ToDictionary(p => p.Code, StringComparer.Ordinal);
            var now = DateTime.Now;

            var matrix = new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                // Admin: 全部权限
                ["ADMIN"] = code => true,
                // PMCAdmin: PMC 模块全部权限
                ["PMC_ADMIN"] = code => code.StartsWith("PMC.", StringComparison.Ordinal),
                // Scheduler: 排产查看/新建/修改/导出
                ["SCHEDULER"] = code => code is PermissionCodes.ScheduleView or PermissionCodes.ScheduleCreate or PermissionCodes.ScheduleUpdate or PermissionCodes.ScheduleExport
                    or PermissionCodes.WorkOrderView or PermissionCodes.ProductView or PermissionCodes.ExternalProductionView,
                // Reviewer: 交期评审查看/审核/驳回
                ["REVIEWER"] = code => code is PermissionCodes.DeliveryReviewView or PermissionCodes.DeliveryReviewApprove or PermissionCodes.DeliveryReviewReject
                    or PermissionCodes.ScheduleView or PermissionCodes.WorkOrderView or PermissionCodes.ProductView or PermissionCodes.ExternalProductionView,
                // Operator: 工单查看/修改
                ["OPERATOR"] = code => code is PermissionCodes.WorkOrderView or PermissionCodes.WorkOrderUpdate,
                // Viewer: 全部查看权限
                ["VIEWER"] = code => code.StartsWith("PMC.", StringComparison.Ordinal) && code.EndsWith(".View", StringComparison.Ordinal)
            };

            foreach (var (roleCode, matcher) in matrix)
            {
                if (!roles.TryGetValue(roleCode, out var role))
                    continue;

                var targetCodes = permissions.Where(p => p.Enabled && matcher(p.Code)).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
                var boundCodes = await (
                    from rp in _context.RolePermissions
                    join p in _context.Permissions on rp.PermissionId equals p.Id
                    where rp.RoleId == role.Id
                    select p.Code).ToListAsync(ct);

                var missing = targetCodes.Except(boundCodes).ToList();
                foreach (var code in missing)
                {
                    if (!permissionByCode.TryGetValue(code, out var permission))
                        continue;
                    _context.RolePermissions.Add(new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        CreateTime = now
                    });
                }
                if (missing.Count > 0)
                {
                    await _context.SaveChangesAsync(ct);
                    _logger.LogInformation("RBAC 角色权限绑定: {RoleCode} +{Count}", roleCode, missing.Count);
                }
            }
        }

        // ---------- 4. 历史 Admin 用户兜底绑定(防锁死) ----------
        private async Task EnsureLegacyAdminUsersAsync(CancellationToken ct)
        {
            var adminRole = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == "ADMIN", ct);
            if (adminRole == null)
                return;

            // 旧系统通过 User.Role="Admin" 标记管理员,迁移后自动绑定 Admin 角色
            var legacyAdmins = await _context.Users.AsNoTracking()
                .Where(u => u.NormalizedUserName == "ADMIN")
                .Select(u => u.Id)
                .ToListAsync(ct);

            var boundUserIds = await _context.UserRoles.AsNoTracking()
                .Where(ur => ur.RoleId == adminRole.Id && ur.IsActive)
                .Select(ur => ur.UserId)
                .ToHashSetAsync(ct);

            var now = DateTime.Now;
            var added = 0;
            foreach (var userId in legacyAdmins)
            {
                if (boundUserIds.Contains(userId))
                    continue;
                _context.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = adminRole.Id,
                    AssignedAt = now,
                    IsActive = true
                });
                added++;
            }
            if (added > 0)
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("RBAC 历史管理员绑定: {Count} 个用户绑定 ADMIN 角色", added);
            }
        }

        // ---------- 5. 默认管理员账号初始化(幂等,满足"首次部署可用") ----------
        /// <summary>
        /// 确保存在一个拥有 ADMIN 角色的管理员账号。
        /// 场景: 全新环境数据库为空,既无 admin 用户也无任何 ADMIN 绑定用户时,自动创建 admin 账号,
        /// 使用配置中的默认密码(未配置则回退开发默认),强制首次登录修改密码。
        /// 已存在 ADMIN 角色用户(含历史绑定)时跳过,避免覆盖既有账号。
        /// </summary>
        private async Task EnsureDefaultAdminUserAsync(CancellationToken ct)
        {
            var adminRole = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == "ADMIN", ct);
            if (adminRole == null)
                return;

            // 已存在绑定 ADMIN 的有效用户 → 跳过
            var hasAdmin = await _context.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.RoleId == adminRole.Id && ur.IsActive, ct);
            if (hasAdmin)
                return;

            // 已存在 admin 用户名但可能未绑角色 → 仅补绑,不重建(防重复账号)
            var existingAdmin = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.NormalizedUserName == "ADMIN", ct);
            if (existingAdmin != null)
            {
                _context.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = existingAdmin.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.Now,
                    IsActive = true
                });
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("RBAC 默认管理员补绑: 已有 admin 账号绑定 ADMIN 角色");
                return;
            }

            // 全新环境: 创建 admin 账号
            // 注意: 配置为空字符串时 IConfiguration[index] 返回 "" 而非 null, 故用 IsNullOrEmpty 判断回退
            var defaultPassword = _configuration["Rbac:DefaultAdminPassword"];
            if (string.IsNullOrEmpty(defaultPassword))
                defaultPassword = "Yc@Admin2026"; // 开发期回退默认密码;生产务必通过 Rbac__DefaultAdminPassword 注入强密码
            PasswordHelper.CreateHash(defaultPassword, out var hash, out var salt);
            var now = DateTime.UtcNow;

            var admin = new User
            {
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                DisplayName = "系统管理员",
                PasswordHash = hash,
                PasswordSalt = salt,
                PasswordAlgorithm = "PBKDF2-SHA256-100000",
                PasswordUpdatedAtUtc = now,
                Status = UserStatus.Active,
                MustChangePassword = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _context.Users.Add(admin);
            await _context.SaveChangesAsync(ct);

            _context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                RoleId = adminRole.Id,
                AssignedAt = DateTime.Now,
                IsActive = true
            });
            await _context.SaveChangesAsync(ct);

            _logger.LogWarning(
                "RBAC 默认管理员已创建: 账号=admin, 默认密码已在配置/开发回退值中,首次登录将强制修改密码。请尽快在生产环境修改默认密码!");
        }
    }
}
