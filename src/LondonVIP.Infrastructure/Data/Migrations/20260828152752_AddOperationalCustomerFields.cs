using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalCustomerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Customers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Postcode",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryPhone",
                table: "Customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Customers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Customers] SET [UpdatedAt] = [CreatedAt] WHERE [UpdatedAt] IS NULL");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Customers",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Postcode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SecondaryPhone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Customers");
        }
    }
}
