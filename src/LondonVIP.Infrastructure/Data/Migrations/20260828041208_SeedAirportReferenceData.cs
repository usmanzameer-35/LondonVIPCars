using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAirportReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Airports",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("12cb02c5-a575-4a50-ab17-b92d81dd331e"), "LTN", true, "Luton" },
                    { new Guid("1e83d9e4-d35a-40f9-9a4e-fba1ee003b55"), "STN", true, "Stansted" },
                    { new Guid("6cbe8f65-2943-4ce1-91fe-f1966d37b334"), "LHR", true, "Heathrow" },
                    { new Guid("a816bb40-d225-4c24-bdbc-a7c2b96f6b9b"), "LGW", true, "Gatwick" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Id",
                keyValue: new Guid("12cb02c5-a575-4a50-ab17-b92d81dd331e"));

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Id",
                keyValue: new Guid("1e83d9e4-d35a-40f9-9a4e-fba1ee003b55"));

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Id",
                keyValue: new Guid("6cbe8f65-2943-4ce1-91fe-f1966d37b334"));

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Id",
                keyValue: new Guid("a816bb40-d225-4c24-bdbc-a7c2b96f6b9b"));
        }
    }
}
