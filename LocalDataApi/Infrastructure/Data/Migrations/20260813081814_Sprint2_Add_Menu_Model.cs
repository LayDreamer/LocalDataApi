using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_Add_Menu_Model : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sys_Menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Component = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sys_Menu", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sys_Menu_Sys_Menu_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Sys_Menu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sys_MenuPermission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sys_MenuPermission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sys_MenuPermission_Sys_Menu_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Sys_Menu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sys_MenuPermission_MenuId_PermissionCode",
                table: "Sys_MenuPermission",
                columns: new[] { "MenuId", "PermissionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sys_MenuPermission_PermissionCode",
                table: "Sys_MenuPermission",
                column: "PermissionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Sys_Menu_ParentId",
                table: "Sys_Menu",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sys_Menu_ParentId_Sort",
                table: "Sys_Menu",
                columns: new[] { "ParentId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_Sys_Menu_Path",
                table: "Sys_Menu",
                column: "Path");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sys_MenuPermission");

            migrationBuilder.DropTable(
                name: "Sys_Menu");
        }
    }
}
