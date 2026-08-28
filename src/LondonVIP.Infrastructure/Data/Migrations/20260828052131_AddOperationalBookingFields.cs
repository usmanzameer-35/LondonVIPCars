using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AirportId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "Bookings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNotes",
                table: "Bookings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlightNumber",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "Bookings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAirportPickup",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMeetAndGreet",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [Bookings] SET [BookingReference] = " +
                "'LVC-LEGACY-' + UPPER(LEFT(REPLACE(CONVERT(varchar(36), [Id]), '-', ''), 8)) " +
                "WHERE [BookingReference] IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "BookingReference",
                table: "Bookings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AirportId",
                table: "Bookings",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_BookingReference",
                table: "Bookings",
                columns: new[] { "CompanyId", "BookingReference" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Airports_AirportId",
                table: "Bookings",
                column: "AirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Airports_AirportId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AirportId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CompanyId_BookingReference",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AirportId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CustomerNotes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FlightNumber",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsAirportPickup",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsMeetAndGreet",
                table: "Bookings");
        }
    }
}
