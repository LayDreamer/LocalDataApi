using Microsoft.EntityFrameworkCore;
using LocalDataApi.Models;
namespace LocalDataApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<BLFParameter> BLFParameters { get; set; }

        public DbSet<CurrentFlowRate> CurrentFlowRates { get; set; }

        public DbSet<PressureFlowRate> PressureFlowRates { get; set; }  
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置表名（可选）
            modelBuilder.Entity<User>().ToTable("LocalUsers");
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
            base.OnModelCreating(modelBuilder);
        }
    }
}
