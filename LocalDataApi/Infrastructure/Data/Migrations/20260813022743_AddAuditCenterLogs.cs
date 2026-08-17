using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCenterLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataChangeLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BeforeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedProperties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OperatorUserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataChangeLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LoginTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LoginType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    FailReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FailReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ClientType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Device = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AuthSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApiPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeLog_ChangeTimeUtc",
                table: "DataChangeLog",
                column: "ChangeTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeLog_EntityName_EntityId_ChangeTimeUtc",
                table: "DataChangeLog",
                columns: new[] { "EntityName", "EntityId", "ChangeTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeLog_OperatorUserId_ChangeTimeUtc",
                table: "DataChangeLog",
                columns: new[] { "OperatorUserId", "ChangeTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DataChangeLog_TraceId",
                table: "DataChangeLog",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLog_IpAddress_LoginTimeUtc",
                table: "LoginLog",
                columns: new[] { "IpAddress", "LoginTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLog_LoginTimeUtc",
                table: "LoginLog",
                column: "LoginTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLog_Success_LoginTimeUtc",
                table: "LoginLog",
                columns: new[] { "Success", "LoginTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLog_UserId_LoginTimeUtc",
                table: "LoginLog",
                columns: new[] { "UserId", "LoginTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_Module_OperationTimeUtc",
                table: "OperationLog",
                columns: new[] { "Module", "OperationTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_OperationTimeUtc",
                table: "OperationLog",
                column: "OperationTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_Success_OperationTimeUtc",
                table: "OperationLog",
                columns: new[] { "Success", "OperationTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_TraceId",
                table: "OperationLog",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLog_UserId_OperationTimeUtc",
                table: "OperationLog",
                columns: new[] { "UserId", "OperationTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataChangeLog");

            migrationBuilder.DropTable(
                name: "LoginLog");

            migrationBuilder.DropTable(
                name: "OperationLog");
        }
    }
}
