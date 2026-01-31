using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BLFParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BLFNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoilResistance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsulationResistance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsulationStrength = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WithstandVoltageStrength = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InternalLeakage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalLeakage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartingCurrent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaximumFlowRate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hysteresis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedLoopFluctuation1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedLoopFluctuation2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedLoopFluctuation3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedLoopFluctuation4 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BLFParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrentFlowRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Current = table.Column<float>(type: "real", nullable: false),
                    FlowRate = table.Column<float>(type: "real", nullable: false),
                    BLFParameterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentFlowRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrentFlowRates_BLFParameters_BLFParameterId",
                        column: x => x.BLFParameterId,
                        principalTable: "BLFParameters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PressureFlowRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pressure = table.Column<float>(type: "real", nullable: false),
                    FlowRate = table.Column<float>(type: "real", nullable: false),
                    BLFParameterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PressureFlowRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PressureFlowRates_BLFParameters_BLFParameterId",
                        column: x => x.BLFParameterId,
                        principalTable: "BLFParameters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrentFlowRates_BLFParameterId",
                table: "CurrentFlowRates",
                column: "BLFParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_PressureFlowRates_BLFParameterId",
                table: "PressureFlowRates",
                column: "BLFParameterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrentFlowRates");

            migrationBuilder.DropTable(
                name: "PressureFlowRates");

            migrationBuilder.DropTable(
                name: "BLFParameters");
        }
    }
}
