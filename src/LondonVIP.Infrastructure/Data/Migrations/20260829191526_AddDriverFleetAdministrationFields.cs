using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverFleetAdministrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Colour",
                table: "Vehicles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Vehicles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsuranceExpiry",
                table: "Vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MOTExpiry",
                table: "Vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Vehicles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrivateHireLicenceExpiry",
                table: "Vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Vehicles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvailabilityStatus",
                table: "Drivers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Drivers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DBSExpiry",
                table: "Drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverNumber",
                table: "Drivers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DrivingLicenceExpiry",
                table: "Drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrivingLicenceNumber",
                table: "Drivers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MedicalExpiry",
                table: "Drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Drivers",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PrivateHireLicenceExpiry",
                table: "Drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateHireLicenceNumber",
                table: "Drivers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Drivers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Drivers] SET [CreatedAt] = SYSDATETIMEOFFSET(), [UpdatedAt] = SYSDATETIMEOFFSET() WHERE [CreatedAt] IS NULL OR [UpdatedAt] IS NULL");
            migrationBuilder.Sql("UPDATE [Vehicles] SET [CreatedAt] = SYSDATETIMEOFFSET(), [UpdatedAt] = SYSDATETIMEOFFSET() WHERE [CreatedAt] IS NULL OR [UpdatedAt] IS NULL");

            migrationBuilder.AlterColumn<DateTimeOffset>(name: "CreatedAt", table: "Drivers", type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
            migrationBuilder.AlterColumn<DateTimeOffset>(name: "UpdatedAt", table: "Drivers", type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
            migrationBuilder.AlterColumn<DateTimeOffset>(name: "CreatedAt", table: "Vehicles", type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);
            migrationBuilder.AlterColumn<DateTimeOffset>(name: "UpdatedAt", table: "Vehicles", type: "datetimeoffset", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "datetimeoffset", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_AvailabilityStatus_IsActive",
                table: "Drivers",
                columns: new[] { "CompanyId", "AvailabilityStatus", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_VehicleId",
                table: "Drivers",
                columns: new[] { "CompanyId", "VehicleId" },
                unique: true,
                filter: "[VehicleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_AvailabilityStatus_IsActive",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_VehicleId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "Colour",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "MOTExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PrivateHireLicenceExpiry",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DBSExpiry",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DriverNumber",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceExpiry",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceNumber",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "MedicalExpiry",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PrivateHireLicenceExpiry",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PrivateHireLicenceNumber",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Drivers");
        }
    }
}
