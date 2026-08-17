using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sys_NumberRule",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DateFormat = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SequenceLength = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<long>(type: "bigint", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    LastResetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sys_NumberRule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Sys_NumberRule_RuleCode",
                schema: "dbo",
                table: "Sys_NumberRule",
                column: "RuleCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sys_NumberRule",
                schema: "dbo");
        }
    }
}
