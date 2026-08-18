using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTraceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessId",
                table: "OperationLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "OperationLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessId",
                table: "DataChangeLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "DataChangeLog",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_BusinessType_BusinessId",
                table: "OperationLog",
                columns: new[] { "BusinessType", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeLog_BusinessType_BusinessId",
                table: "DataChangeLog",
                columns: new[] { "BusinessType", "BusinessId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationLog_BusinessType_BusinessId",
                table: "OperationLog");

            migrationBuilder.DropIndex(
                name: "IX_DataChangeLog_BusinessType_BusinessId",
                table: "DataChangeLog");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "OperationLog");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "OperationLog");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "DataChangeLog");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "DataChangeLog");
        }
    }
}
