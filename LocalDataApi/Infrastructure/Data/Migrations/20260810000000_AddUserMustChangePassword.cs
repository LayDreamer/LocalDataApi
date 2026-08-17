using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 用户表 [用户管理] 扩展列(表为 DB-First 预存在,仅追加列) =====
            // 是否强制修改密码:种子 admin / 重置密码后置 1,用户首次改密后置 0。
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "用户管理",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "用户管理");
        }
    }
}
