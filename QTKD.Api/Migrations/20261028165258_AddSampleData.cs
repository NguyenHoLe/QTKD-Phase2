using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QTKD.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Name", "Status" },
                values: new object[,]
                {
                    { 1, "Công ty Minh Phát", "Active" },
                    { 2, "Công ty An Bình", "Active" }
                });

            migrationBuilder.InsertData(
                table: "PriceLists",
                columns: new[] { "Id", "EffectiveDate", "Name" },
                values: new object[] { 1, new DateOnly(2026, 1, 1), "Bảng giá demo 2026" });

            migrationBuilder.InsertData(
                table: "Contracts",
                columns: new[] { "Id", "ContractNo", "CustomerId", "EndDate", "StartDate", "Status" },
                values: new object[] { 1, "HD-2026-018", 1, new DateOnly(2027, 9, 30), new DateOnly(2026, 10, 1), "Draft" });

            migrationBuilder.InsertData(
                table: "PriceItems",
                columns: new[] { "PriceListId", "ServiceCode", "UnitPrice" },
                values: new object[,]
                {
                    { 1, "SVC-PORT-001", 100000m },
                    { 1, "SVC-PORT-002", 250000m }
                });

            // HasData inserts explicit identity values. Advance each sequence so
            // later API-created rows receive IDs that don't collide with these samples.
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"Customers"', 'Id'), (SELECT MAX("Id") FROM "Customers"), true);
                SELECT setval(pg_get_serial_sequence('"Contracts"', 'Id'), (SELECT MAX("Id") FROM "Contracts"), true);
                SELECT setval(pg_get_serial_sequence('"PriceLists"', 'Id'), (SELECT MAX("Id") FROM "PriceLists"), true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Contracts",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "PriceItems",
                keyColumns: new[] { "PriceListId", "ServiceCode" },
                keyValues: new object[] { 1, "SVC-PORT-001" });

            migrationBuilder.DeleteData(
                table: "PriceItems",
                keyColumns: new[] { "PriceListId", "ServiceCode" },
                keyValues: new object[] { 1, "SVC-PORT-002" });

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "PriceLists",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
