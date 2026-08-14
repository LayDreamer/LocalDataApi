using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeUserIdentityIdUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee");

            migrationBuilder.CreateIndex(
                name: "UX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee",
                column: "UserIdentityId",
                unique: true,
                filter: "[UserIdentityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_UserIdentityId",
                schema: "dbo",
                table: "Employee",
                column: "UserIdentityId");
        }
    }
}
