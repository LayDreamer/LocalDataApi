using Microsoft.EntityFrameworkCore;
using LocalDataApi.Models;
namespace LocalDataApi.Data
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

        public DbSet<User> 用户管理 { get; set; }
        
        public DbSet<SchedulingAnalysis> 排产分析单 { get; set; }
        
        // public DbSet<PMCDeliveryReview> 信息交期评审 { get; set; }

        public DbSet<PMCDeliveryReview> 外产_订单 { get; set; }
         
        public DbSet<ExternalProductionShipment> 外产_发运 { get; set; }

        public DbSet<ExternalProduction> 外产_生产 { get; set; }

        public DbSet<ExternalProductionWarehousing> 外产_入库 { get; set; }

        public DbSet<ExternalProductionPickMaterial> 外产_领料 { get; set; }

        public DbSet<ExternalProductionBOM> 外产_BOM { get; set; }

        public DbSet<DeliveryPlan> 交货计划 { get; set; }

        public DbSet<PMCSalesControl> 产品销控表 { get; set; }

        public DbSet<WorkOrderSalesControl> 工单销控表 { get; set; }

        public DbSet<WorkOrderSalesControlDetail> 工单销控表明细 { get; set; }

        public DbSet<ProductSalesControlDetail> 成品销控表明细 { get; set; }

        public DbSet<BOMStructureProcess> BOM结构工序 { get; set; }

        // public DbSet<PMCWorkOrder> 工单管理 { get; set; }

        public DbSet<ProductionDemand> 在产需求量 { get; set; }
       public DbSet<InTransitQuantity> 在途数 { get; set; }

        public DbSet<WechatWorkGroupChat> 企业微信群聊 { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
            });
            modelBuilder.Entity<SchedulingAnalysis>(entity =>
            {
                entity.HasKey(e => e.编号);
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
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserName).IsUnique();
            });


            modelBuilder.Entity<PMCSalesControl>(entity =>
            {
                entity.HasKey(e => e.货号);
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
            });

            modelBuilder.Entity<WorkOrderSalesControlDetail>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ProductSalesControlDetail>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<BOMStructureProcess>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProductionShipment>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProduction>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProductionWarehousing>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProductionPickMaterial>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<ExternalProductionBOM>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<DeliveryPlan>(entity =>
            {
                entity.HasKey(e => e.编号);
            });

            modelBuilder.Entity<PMCUserProductInfo>(entity =>
            {
                entity.HasKey(e => e.编号);
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