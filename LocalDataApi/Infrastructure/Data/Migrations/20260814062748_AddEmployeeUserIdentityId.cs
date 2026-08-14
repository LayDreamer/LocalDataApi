using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeUserIdentityId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UserIdentityId",
                schema: "dbo",
                table: "Employee",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee",
                column: "UserIdentityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employee_UserIdentity",
                schema: "dbo",
                table: "Employee",
                column: "UserIdentityId",
                principalTable: "用户管理",
                principalColumn: "IdentityId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employee_UserIdentity",
                schema: "dbo",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee");

            migrationBuilder.DropColumn(
                name: "UserIdentityId",
                schema: "dbo",
                table: "Employee");
        }
    }
}
