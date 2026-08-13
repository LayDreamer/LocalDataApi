using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Migrations
{
    public partial class AddAuthSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IdleExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AbsoluteExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RememberMe = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_AuthSession", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_AuthSession_UserId", table: "AuthSession", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_AuthSession_RevokedAtUtc_IdleExpiresAtUtc", table: "AuthSession", columns: new[] { "RevokedAtUtc", "IdleExpiresAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AuthSession");
    }
}
