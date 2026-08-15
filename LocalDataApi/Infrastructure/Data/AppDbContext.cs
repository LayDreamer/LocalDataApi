using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using LocalDataApi.Domain.Pmc;
using LocalDataApi.Domain.Blf;
using LocalDataApi.Domain.Erp;
using LocalDataApi.Domain.Employee;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Domain.WeChatWork;
namespace LocalDataApi.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // 为所有查询配置默认行为
            //ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
        public DbSet<BLFParameter> BLFParameters { get; set; }

        public DbSet<CurrentFlowRate> CurrentFlowRates { get; set; }

        public DbSet<PressureFlowRate> PressureFlowRates { get; set; }

        public DbSet<PMCUserProductInfo> 外销合同客户产品 { get; set; }

        public DbSet<PMCProductInfo> 外销合同产品 { get; set; }

        public DbSet<PMCBasicInfo> 外销合同基本信息 { get; set; }

        public DbSet<ProductData> 产品资料 { get; set; }

        public DbSet<WarehouseGoods> 仓库货品 { get; set; }
        public DbSet<WarehouseInfo> 仓库信息 { get; set; }
        public DbSet<ProductDataAssembly> 产品资料装配 { get; set; }

        public DbSet<ProductDataAssemblyList> 产品资料装配清单 { get; set; }
        
        public DbSet<ERPUser> tb_control_user { get; set; }

        public DbSet<ERPId> tb_control_id { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<UserExternalIdentity> UserExternalIdentities { get; set; }
        public DbSet<UserLegacyMap> UserLegacyMaps { get; set; }

        // ========== RBAC 权限中心(2026-08-08 新增,表落地见 DatabaseScripts/20260808_RbacTables.sql) ==========
        public DbSet<Department> Departments { get; set; }

        public DbSet<Position> Positions { get; set; }

        public DbSet<Employee> Employees { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<Menu> Menus { get; set; }

        public DbSet<MenuPermission> MenuPermissions { get; set; }

        public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        public DbSet<AuthSession> AuthSessions { get; set; }

        public DbSet<LoginLog> LoginLogs { get; set; }

        public DbSet<OperationLog> OperationLogs { get; set; }

        public DbSet<DataChangeLog> DataChangeLogs { get; set; }
        
        public DbSet<SchedulingAnalysis> 排产分析单 { get; set; }
        
        // public DbSet<PMCDeliveryReview> 信息交期评审 { get; set; }

        public DbSet<PMCDeliveryReview> 外产_订单 { get; set; }

        public DbSet<ProductionTypeOverride> 生产类型修改 { get; set; }
         
        public DbSet<ExternalProductionShipment> 外产_发运 { get; set; }

        public DbSet<ExternalProduction> 外产_生产 { get; set; }

        public DbSet<ExternalProductionWarehousing> 外产_入库 { get; set; }

        public DbSet<ExternalProductionPickMaterial> 外产_领料 { get; set; }

        public DbSet<ExternalProductionBOM> 外产_BOM { get; set; }

        public DbSet<DeliveryPlan> 交货计划 { get; set; }

        public DbSet<WorkOrderSalesControl> 工单销控表 { get; set; }

        public DbSet<WorkOrderSalesControlDetail> 工单销控表明细 { get; set; }

        public DbSet<BOMStructureProcess> BOM结构工序 { get; set; }

        public DbSet<ProductionDemand> 在产需求量 { get; set; }
       public DbSet<InTransitQuantity> 在途数 { get; set; }

        public DbSet<WechatWorkGroupChat> 企业微信群聊 { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // 配置BLFParameter实体
            modelBuilder.Entity<BLFParameter>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // 配置CurrentFlowRate实体
            modelBuilder.Entity<CurrentFlowRate>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // 配置PressureFlowRate实体
            modelBuilder.Entity<PressureFlowRate>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<PMCProductInfo>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.货号, e.合同号, e.分析单号, e.层 });
            });
            modelBuilder.Entity<PMCBasicInfo>(entity =>
            {
                entity.HasKey(e => e.编号);
            });
            modelBuilder.Entity<ProductDataAssembly>(entity =>
            {
                entity.HasKey(e => e.编号);
            });
            modelBuilder.Entity<ProductDataAssemblyList>(entity =>
            {
                entity.HasKey(e => e.编号);
            });
            modelBuilder.Entity<ProductData>(entity =>
            {
                entity.HasKey(e => e.编号);
            });
            modelBuilder.Entity<PMCDeliveryReview>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.状态, e.排产编号, e.货号 });
            });
            modelBuilder.Entity<ProductionTypeOverride>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.合同号, e.排产编号, e.货号 }).IsUnique();
            });
            modelBuilder.Entity<SchedulingAnalysis>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => e.分析单号).IsUnique();
                entity.HasIndex(e => e.排产编号);
            });
            modelBuilder.Entity<ERPUser>(entity =>
            {
                entity.HasKey(e => e.ID);
            });

            modelBuilder.Entity<ERPId>(entity =>
            {
                entity.HasKey(e => e.ID);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Sys_User", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.UserName).HasMaxLength(128).IsRequired();
                entity.Property(e => e.NormalizedUserName).HasMaxLength(128).IsRequired();
                entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.PhoneNumber).HasMaxLength(32);
                entity.Property(e => e.PasswordHash).HasMaxLength(512);
                entity.Property(e => e.PasswordSalt).HasMaxLength(256);
                entity.Property(e => e.PasswordAlgorithm).HasMaxLength(32);
                entity.Property(e => e.LastLoginIp).HasMaxLength(64);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.HasIndex(e => e.NormalizedUserName).IsUnique().HasDatabaseName("UX_Sys_User_NormalizedUserName");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_Sys_User_Status");
            });

            modelBuilder.Entity<UserExternalIdentity>(entity =>
            {
                entity.ToTable("Sys_UserExternalIdentity", "dbo");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
                entity.Property(e => e.ExternalSubject).HasMaxLength(128).IsRequired();
                entity.HasIndex(e => new { e.Provider, e.ExternalSubject }).IsUnique()
                    .HasDatabaseName("UX_Sys_UserExternalIdentity_Provider_Subject");
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserLegacyMap>(entity =>
            {
                entity.ToTable("Sys_UserLegacyMap", "dbo");
                entity.HasKey(e => e.LegacyUserId);
                entity.Property(e => e.LegacyUserId).HasMaxLength(450);
                entity.HasIndex(e => e.UserId).IsUnique().HasDatabaseName("UX_Sys_UserLegacyMap_UserId");
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            modelBuilder.Entity<WarehouseGoods>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<WarehouseInfo>(entity =>
            {
                entity.HasKey(e => e.编号);
            });
          

            modelBuilder.Entity<WorkOrderSalesControl>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => e.货号).IsUnique();
            });

            modelBuilder.Entity<WorkOrderSalesControlDetail>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.货号, e.分析单号 }).IsUnique();
                entity.HasIndex(e => e.父级编号);
            });

            modelBuilder.Entity<BOMStructureProcess>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProductionShipment>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.分析单号, e.货号 }).IsUnique();
                entity.HasIndex(e => new { e.排产编号, e.货号 });
            });

            modelBuilder.Entity<ExternalProduction>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.分析单号, e.货号 }).IsUnique();
                entity.HasIndex(e => new { e.排产编号, e.货号 });
            });

            modelBuilder.Entity<ExternalProductionWarehousing>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.分析单号, e.货号 });
            });

            modelBuilder.Entity<ExternalProductionPickMaterial>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.分析单号, e.货号 });
            });

            modelBuilder.Entity<ExternalProductionBOM>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.分析单号, e.货号 });
                entity.HasIndex(e => e.父级编号);
            });

            modelBuilder.Entity<DeliveryPlan>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<PMCUserProductInfo>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => new { e.创建时间, e.合同号, e.货号 });
            });

            // 尝试使用dbo模式前缀，确保视图名称正确
            modelBuilder.Entity<ProductionDemand>().ToView("vw_在产需求量").HasNoKey();
            
            // 配置InTransitQuantity实体映射到vw_甘特图在产量视图
            modelBuilder.Entity<InTransitQuantity>().ToView("vw_甘特图在产量").HasNoKey();

            modelBuilder.Entity<WechatWorkGroupChat>(entity =>
            {
                entity.HasKey(e => e.编号);
                entity.HasIndex(e => e.ChatId).IsUnique();
            });

            // ========== RBAC 实体配置(2026-08-08) ==========
            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Department");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CorpDepartmentId).IsUnique();
                entity.HasIndex(e => e.ParentId);
                entity.HasIndex(e => e.Path);
                entity.HasIndex(e => e.LeaderUserId);
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.LeaderUserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Department_Sys_User_LeaderUserId");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Role");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("Permission");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => new { e.Module, e.Resource });
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRole");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
                entity.HasIndex(e => e.RoleId);
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("RolePermission");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLog");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CreateTime, e.Action });
            });

            modelBuilder.Entity<AuthSession>(entity =>
            {
                entity.ToTable("AuthSession");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RevokedReason).HasMaxLength(64);
                entity.Property(e => e.IpAddress).HasMaxLength(128);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.RevokedAtUtc, e.IdleExpiresAtUtc });
                entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LoginLog>(entity =>
            {
                entity.ToTable("LoginLog");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.UserName).HasMaxLength(128);
                entity.Property(e => e.LoginType).HasMaxLength(32);
                entity.Property(e => e.FailReasonCode).HasMaxLength(64);
                entity.Property(e => e.FailReason).HasMaxLength(256);
                entity.Property(e => e.IpAddress).HasMaxLength(128);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.Property(e => e.ClientType).HasMaxLength(32);
                entity.Property(e => e.Device).HasMaxLength(128);
                entity.Property(e => e.TraceId).HasMaxLength(64);
                entity.HasIndex(e => e.LoginTimeUtc);
                entity.HasIndex(e => new { e.UserId, e.LoginTimeUtc });
                entity.HasIndex(e => new { e.Success, e.LoginTimeUtc });
                entity.HasIndex(e => new { e.IpAddress, e.LoginTimeUtc });
            });

            modelBuilder.Entity<OperationLog>(entity =>
            {
                entity.ToTable("OperationLog");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.UserName).HasMaxLength(128);
                entity.Property(e => e.Module).HasMaxLength(64);
                entity.Property(e => e.Action).HasMaxLength(128);
                entity.Property(e => e.HttpMethod).HasMaxLength(16);
                entity.Property(e => e.ApiPath).HasMaxLength(256);
                entity.Property(e => e.ExceptionType).HasMaxLength(256);
                entity.Property(e => e.ExceptionMessage).HasMaxLength(1024);
                entity.Property(e => e.TraceId).HasMaxLength(64);
                entity.Property(e => e.IpAddress).HasMaxLength(128);
                entity.Property(e => e.UserAgent).HasMaxLength(512);
                entity.HasIndex(e => e.OperationTimeUtc);
                entity.HasIndex(e => new { e.UserId, e.OperationTimeUtc });
                entity.HasIndex(e => new { e.Module, e.OperationTimeUtc });
                entity.HasIndex(e => e.TraceId);
                entity.HasIndex(e => new { e.Success, e.OperationTimeUtc });
            });

            modelBuilder.Entity<DataChangeLog>(entity =>
            {
                entity.ToTable("DataChangeLog");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityName).HasMaxLength(128);
                entity.Property(e => e.EntityId).HasMaxLength(450);
                entity.Property(e => e.ChangeType).HasMaxLength(16);
                entity.Property(e => e.OperatorUserId).HasMaxLength(450);
                entity.Property(e => e.OperatorUserName).HasMaxLength(128);
                entity.Property(e => e.TraceId).HasMaxLength(64);
                entity.Property(e => e.Source).HasMaxLength(32);
                entity.HasIndex(e => e.ChangeTimeUtc);
                entity.HasIndex(e => new { e.EntityName, e.EntityId, e.ChangeTimeUtc });
                entity.HasIndex(e => new { e.OperatorUserId, e.ChangeTimeUtc });
                entity.HasIndex(e => e.TraceId);
            });

            // ========== EF 迁移边界配置(2026-08-08) ==========
            // 本项目为 DB-First 混合模式:仅 BLF 三表与 RBAC 新表由 EF Migration 管理,
            // 其余中文表/视图/用户表等均为数据库预存在实体,必须排除出迁移范围;
            // 否则 dotnet ef migrations add 会为这些已存在的表生成 CREATE TABLE,
            // 执行 database update 时因"对象已存在"而失败。
            // 用户表 [用户管理] 的 RBAC 扩展列以手写 AddColumn 方式加入迁移(见 AddRbacTables 迁移)。
            var typesManagedByMigrations = new HashSet<Type>
            {
                typeof(BLFParameter), typeof(CurrentFlowRate), typeof(PressureFlowRate),
                typeof(Department), typeof(Position), typeof(Employee), typeof(Role), typeof(Permission),
                typeof(User), typeof(UserExternalIdentity), typeof(UserLegacyMap),
                typeof(UserRole), typeof(RolePermission), typeof(AuditLog), typeof(AuthSession),
                typeof(LoginLog), typeof(OperationLog), typeof(DataChangeLog),
                typeof(Menu), typeof(MenuPermission)
            };
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // 视图(ToView)天然不参与迁移,直接跳过(避免快照生成 ToTable(null) 报错)
                if (!string.IsNullOrEmpty(entityType.GetViewName()))
                {
                    continue;
                }
                if (!typesManagedByMigrations.Contains(entityType.ClrType))
                {
                    entityType.SetIsTableExcludedFromMigrations(true);
                }
            }

            base.OnModelCreating(modelBuilder);
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder
        //        .UseSqlServer()
        //        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        //        .ConfigureWarnings(w => w.Default(WarningBehavior.Throw));
        //}
    }
}
