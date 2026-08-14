using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentityIdCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserIdentityIdCounter",
                schema: "dbo",
                columns: table => new
                {
                    CounterKey = table.Column<byte>(type: "tinyint", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentityIdCounter", x => x.CounterKey);
                });

            // 兼容 SQL Server 2008：使用单行计数器表替代 SQL Server 2012 才支持的 SEQUENCE。
            // NextValue 保存下一次可分配的编号，首次取号返回历史最大值的下一位。
            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[UserIdentityIdCounter] ([CounterKey], [NextValue])
                SELECT 1, ISNULL(MAX([IdentityId]), 0) + 1
                FROM [dbo].[用户管理];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIdentityIdCounter",
                schema: "dbo");
        }
    }
}
