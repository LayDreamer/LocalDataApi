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

        public DbSet<PMCProductInfo> 外销合同产品 { get; set; }        

        public DbSet<PMCBasicInfo> 外销合同基本信息 { get; set; }

        public DbSet<ProductData> 产品资料 { get; set; }
        public DbSet<ProductDataAssembly> 产品资料装配 { get; set; }

        public DbSet<ProductDataAssemblyList> 产品资料装配清单 { get; set; }

        public DbSet<PMCDeliveryReview> 信息交期评审 { get; set; }

        public DbSet<SchedulingAnalysis> 排产分析单 { get; set; }

        public DbSet<PMCSalesControl> 产品销控表 { get; set; }

        public DbSet<WarehouseGoods> 仓库货品 { get; set; }
        

        public DbSet<ERPUser> tb_control_user { get; set; }
        

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

            modelBuilder.Entity<PMCSalesControl>(entity =>
            {
                entity.HasKey(e => e.货号);
            });

            modelBuilder.Entity<WarehouseGoods>(entity =>
            {
                entity.HasKey(e => e.编号);
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
