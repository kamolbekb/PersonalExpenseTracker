using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatesAndGold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Buy = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    Sell = table.Column<decimal>(type: "numeric(18,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoldPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Item = table.Column<string>(type: "text", nullable: false),
                    SellPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BuyBackPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_AsOfDate",
                table: "CurrencyRates",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_Source_CurrencyCode_AsOfDate",
                table: "CurrencyRates",
                columns: new[] { "Source", "CurrencyCode", "AsOfDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoldPrices_AsOfDate_Item",
                table: "GoldPrices",
                columns: new[] { "AsOfDate", "Item" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropTable(
                name: "GoldPrices");

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseCurrency = table.Column<string>(type: "text", nullable: false),
                    QuoteCurrency = table.Column<string>(type: "text", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_BaseCurrency_QuoteCurrency_AsOfDate",
                table: "ExchangeRates",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "AsOfDate" },
                unique: true);
        }
    }
}
