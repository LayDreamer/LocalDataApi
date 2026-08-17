using LocalDataApi.Domain.Employee;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employee", "dbo");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id)
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();
        builder.Property(employee => employee.EmployeeNo)
            .HasColumnType("nvarchar(50)")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(employee => employee.Name)
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(employee => employee.DepartmentId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();
        builder.Property(employee => employee.PositionId)
            .HasColumnType("bigint")
            .IsRequired();
        builder.Property(employee => employee.UserId)
            .HasColumnType("bigint");
        builder.Property(employee => employee.Status)
            .HasColumnType("int")
            .IsRequired();
        builder.Property(employee => employee.CreatedTime)
            .HasColumnType("datetime2")
            .IsRequired();
        builder.Property(employee => employee.UpdatedTime)
            .HasColumnType("datetime2");

        builder.HasIndex(employee => employee.EmployeeNo)
            .IsUnique()
            .HasDatabaseName("UX_Employee_EmployeeNo");

        builder.HasIndex(employee => employee.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("UX_Employee_UserId");

        builder.HasOne(employee => employee.Department)
            .WithMany()
            .HasForeignKey(employee => employee.DepartmentId)
            .HasPrincipalKey(department => department.Id)
            .HasConstraintName("FK_Employee_Department")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(employee => employee.Position)
            .WithMany()
            .HasForeignKey(employee => employee.PositionId)
            .HasPrincipalKey(position => position.Id)
            .HasConstraintName("FK_Employee_Position")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(employee => employee.User)
            .WithMany()
            .HasForeignKey(employee => employee.UserId)
            .HasPrincipalKey(user => user.Id)
            .HasConstraintName("FK_Employee_SysUser")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
