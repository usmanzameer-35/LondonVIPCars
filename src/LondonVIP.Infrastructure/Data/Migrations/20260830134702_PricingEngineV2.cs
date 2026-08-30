using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PricingEngineV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PricingRules_CompanyId_AirportId_VehicleType_IsActive",
                table: "PricingRules");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "PricingRules",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPostcode",
                table: "PricingRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationZone",
                table: "PricingRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                table: "PricingRules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveTo",
                table: "PricingRules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncludedUnits",
                table: "PricingRules",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "PricingRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Percentage",
                table: "PricingRules",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PickupPostcode",
                table: "PricingRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupZone",
                table: "PricingRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PromotionCode",
                table: "PricingRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuleType",
                table: "PricingRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitRate",
                table: "PricingRules",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CompanyId_EffectiveFrom_EffectiveTo",
                table: "PricingRules",
                columns: new[] { "CompanyId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CompanyId_RuleType_VehicleType_IsActive_Priority",
                table: "PricingRules",
                columns: new[] { "CompanyId", "RuleType", "VehicleType", "IsActive", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PricingRules_CompanyId_EffectiveFrom_EffectiveTo",
                table: "PricingRules");

            migrationBuilder.DropIndex(
                name: "IX_PricingRules_CompanyId_RuleType_VehicleType_IsActive_Priority",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "DestinationPostcode",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "DestinationZone",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "IncludedUnits",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "PickupPostcode",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "PickupZone",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "PromotionCode",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "RuleType",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "UnitRate",
                table: "PricingRules");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CompanyId_AirportId_VehicleType_IsActive",
                table: "PricingRules",
                columns: new[] { "CompanyId", "AirportId", "VehicleType", "IsActive" });
        }
    }
}
