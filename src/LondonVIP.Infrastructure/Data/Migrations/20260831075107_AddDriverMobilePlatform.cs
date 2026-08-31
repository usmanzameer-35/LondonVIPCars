using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverMobilePlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverJobDeclines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverJobDeclines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverJobDeclines_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverJobDeclines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverJobDeclines_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BreakStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    JobsCompleted = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverShifts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverShifts_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverVehicleIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverVehicleIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverVehicleIssues_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DriverVehicleIssues_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverVehicleIssues_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverVehicleIssues_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId_DriverId",
                table: "AspNetUsers",
                columns: new[] { "CompanyId", "DriverId" },
                unique: true,
                filter: "[DriverId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DriverId",
                table: "AspNetUsers",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverJobDeclines_BookingId",
                table: "DriverJobDeclines",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverJobDeclines_CompanyId_BookingId",
                table: "DriverJobDeclines",
                columns: new[] { "CompanyId", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverJobDeclines_CompanyId_DriverId_CreatedAt",
                table: "DriverJobDeclines",
                columns: new[] { "CompanyId", "DriverId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverJobDeclines_DriverId",
                table: "DriverJobDeclines",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverShifts_CompanyId_DriverId_StartedAt",
                table: "DriverShifts",
                columns: new[] { "CompanyId", "DriverId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverShifts_DriverId",
                table: "DriverShifts",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleIssues_BookingId",
                table: "DriverVehicleIssues",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleIssues_CompanyId_DriverId_CreatedAt",
                table: "DriverVehicleIssues",
                columns: new[] { "CompanyId", "DriverId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleIssues_CompanyId_Status_CreatedAt",
                table: "DriverVehicleIssues",
                columns: new[] { "CompanyId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleIssues_DriverId",
                table: "DriverVehicleIssues",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverVehicleIssues_VehicleId",
                table: "DriverVehicleIssues",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Drivers_DriverId",
                table: "AspNetUsers",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Drivers_DriverId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "DriverJobDeclines");

            migrationBuilder.DropTable(
                name: "DriverShifts");

            migrationBuilder.DropTable(
                name: "DriverVehicleIssues");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId_DriverId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DriverId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "AspNetUsers");
        }
    }
}
