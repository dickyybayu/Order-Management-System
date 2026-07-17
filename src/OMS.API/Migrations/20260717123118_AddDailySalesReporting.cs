using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS.API.Migrations;

/// <inheritdoc />
public partial class AddDailySalesReporting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BackgroundJobExecutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JobName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BackgroundJobExecutions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DailySalesReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                TotalOrders = table.Column<int>(type: "int", nullable: false),
                TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailySalesReports", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DailySalesReportItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DailySalesReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductSku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                QuantitySold = table.Column<int>(type: "int", nullable: false),
                Revenue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailySalesReportItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_DailySalesReportItems_DailySalesReports_DailySalesReportId",
                    column: x => x.DailySalesReportId,
                    principalTable: "DailySalesReports",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_DailySalesReportItems_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BackgroundJobExecutions_JobName",
            table: "BackgroundJobExecutions",
            column: "JobName");

        migrationBuilder.CreateIndex(
            name: "IX_BackgroundJobExecutions_StartedAtUtc",
            table: "BackgroundJobExecutions",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_BackgroundJobExecutions_Status",
            table: "BackgroundJobExecutions",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_DailySalesReportItems_DailySalesReportId",
            table: "DailySalesReportItems",
            column: "DailySalesReportId");

        migrationBuilder.CreateIndex(
            name: "IX_DailySalesReportItems_ProductId",
            table: "DailySalesReportItems",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_DailySalesReports_ReportDate",
            table: "DailySalesReports",
            column: "ReportDate",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BackgroundJobExecutions");

        migrationBuilder.DropTable(
            name: "DailySalesReportItems");

        migrationBuilder.DropTable(
            name: "DailySalesReports");
    }
}
