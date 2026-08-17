using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDictionaryCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_dictionary_type",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_dictionary_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_dictionary_item",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DictionaryId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_dictionary_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sys_dictionary_item_sys_dictionary_type_DictionaryId",
                        column: x => x.DictionaryId,
                        principalSchema: "dbo",
                        principalTable: "sys_dictionary_type",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_sys_dictionary_item_DictionaryId_Value",
                schema: "dbo",
                table: "sys_dictionary_item",
                columns: new[] { "DictionaryId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_sys_dictionary_type_Code",
                schema: "dbo",
                table: "sys_dictionary_type",
                column: "Code",
                unique: true);

            // ===== 初始化种子数据 =====
            // 订单状态(示例,供业务模块对接验证)
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "sys_dictionary_type",
                columns: new[] { "Id", "Code", "Name", "Description", "Status", "Sort", "CreateTime" },
                values: new object[,]
                {
                    { 1L, "OrderStatus", "订单状态", "订单生命周期状态", (byte)1, 1, DateTime.Now },
                    { 2L, "ManufactureType", "制造方式", "自制/外协", (byte)1, 2, DateTime.Now }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "sys_dictionary_item",
                columns: new[] { "Id", "DictionaryId", "Value", "Label", "Sort", "Status", "CreateTime" },
                values: new object[,]
                {
                    { 1L, 1L, "10", "新建", 1, (byte)1, DateTime.Now },
                    { 2L, 1L, "20", "审核", 2, (byte)1, DateTime.Now },
                    { 3L, 1L, "30", "生产中", 3, (byte)1, DateTime.Now },
                    { 4L, 1L, "40", "完成", 4, (byte)1, DateTime.Now },
                    { 5L, 2L, "自制", "自制", 1, (byte)1, DateTime.Now },
                    { 6L, 2L, "外协", "外协", 2, (byte)1, DateTime.Now }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_dictionary_item",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "sys_dictionary_type",
                schema: "dbo");
        }
    }
}
