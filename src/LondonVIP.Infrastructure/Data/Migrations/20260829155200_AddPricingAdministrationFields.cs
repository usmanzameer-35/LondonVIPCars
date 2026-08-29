using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAdministrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PricingRules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "PricingRules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE [PricingRules] SET [CreatedAt] = SYSDATETIMEOFFSET(), [UpdatedAt] = SYSDATETIMEOFFSET() WHERE [CreatedAt] IS NULL OR [UpdatedAt] IS NULL");

            migrationBuilder.AlterColumn<DateTimeOffset>(name: "CreatedAt", table: "PricingRules", type: "datetimeoffset", nullable: false,
                oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
            migrationBuilder.AlterColumn<DateTimeOffset>(name: "UpdatedAt", table: "PricingRules", type: "datetimeoffset", nullable: false,
                oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PricingRules");
        }
    }
}
