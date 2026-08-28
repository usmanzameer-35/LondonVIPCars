using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyTenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RegistrationNumber",
                table: "Vehicles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Vehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "PricingRules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Drivers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Airports",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyBranding",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondaryColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccentColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FaviconUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerWebsiteTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerWebsiteTagline = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyBranding", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_CompanyBranding_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinimumBookingNoticeMinutes = table.Column<int>(type: "int", nullable: false),
                    FreeAirportWaitingMinutes = table.Column<int>(type: "int", nullable: false),
                    WaitingChargePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultAirportPickupSupplement = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MeetAndGreetEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DriverCommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DriverWeeklySubscriptionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatEnabled = table.Column<bool>(type: "bit", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    InvoicePrefix = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_CompanySettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "AddressLine1", "AddressLine2", "City", "Country", "CreatedAt", "CurrencyCode", "Email", "IsActive", "LegalName", "Phone", "Postcode", "Slug", "TimeZone", "TradingName", "UpdatedAt", "WebsiteUrl" },
                values: new object[] { new Guid("a26e555d-6b9b-4d9c-86b1-b0ba606a47d8"), "", "", "London", "United Kingdom", new DateTimeOffset(new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "GBP", "", true, "", "", "", "london-vip-cars", "Europe/London", "London VIP Cars", new DateTimeOffset(new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "" });

            migrationBuilder.InsertData(
                table: "CompanyBranding",
                columns: new[] { "CompanyId", "AccentColour", "CustomerWebsiteTagline", "CustomerWebsiteTitle", "FaviconUrl", "LogoUrl", "PrimaryColour", "SecondaryColour" },
                values: new object[] { new Guid("a26e555d-6b9b-4d9c-86b1-b0ba606a47d8"), "#C49A4A", "Every journey, considered.", "London VIP Cars", "", "", "#153F37", "#0C2E29" });

            migrationBuilder.InsertData(
                table: "CompanySettings",
                columns: new[] { "CompanyId", "DefaultAirportPickupSupplement", "DefaultLanguage", "DriverCommissionPercentage", "DriverWeeklySubscriptionAmount", "FreeAirportWaitingMinutes", "InvoicePrefix", "MeetAndGreetEnabled", "MinimumBookingNoticeMinutes", "VatEnabled", "VatRate", "WaitingChargePerHour" },
                values: new object[] { new Guid("a26e555d-6b9b-4d9c-86b1-b0ba606a47d8"), 0m, "en-GB", 0m, 0m, 0, "LVC", false, 0, false, 0m, 0m });

            const string companyId = "a26e555d-6b9b-4d9c-86b1-b0ba606a47d8";
            migrationBuilder.Sql($"UPDATE [Vehicles] SET [CompanyId] = '{companyId}' WHERE [CompanyId] IS NULL");
            migrationBuilder.Sql($"UPDATE [PricingRules] SET [CompanyId] = '{companyId}' WHERE [CompanyId] IS NULL");
            migrationBuilder.Sql($"UPDATE [Drivers] SET [CompanyId] = '{companyId}' WHERE [CompanyId] IS NULL");
            migrationBuilder.Sql($"UPDATE [Customers] SET [CompanyId] = '{companyId}' WHERE [CompanyId] IS NULL");
            migrationBuilder.Sql($"UPDATE [Bookings] SET [CompanyId] = '{companyId}' WHERE [CompanyId] IS NULL");

            foreach (var table in new[] { "Vehicles", "PricingRules", "Drivers", "Customers", "Bookings" })
            {
                migrationBuilder.AlterColumn<Guid>(
                    name: "CompanyId",
                    table: table,
                    type: "uniqueidentifier",
                    nullable: false,
                    oldClrType: typeof(Guid),
                    oldType: "uniqueidentifier",
                    oldNullable: true);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CompanyId_RegistrationNumber",
                table: "Vehicles",
                columns: new[] { "CompanyId", "RegistrationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_AirportId",
                table: "PricingRules",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CompanyId_AirportId_VehicleType_IsActive",
                table: "PricingRules",
                columns: new[] { "CompanyId", "AirportId", "VehicleType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_CompanyId_Email",
                table: "Drivers",
                columns: new[] { "CompanyId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_VehicleId",
                table: "Drivers",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_Email",
                table: "Customers",
                columns: new[] { "CompanyId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_PickupDateTime",
                table: "Bookings",
                columns: new[] { "CompanyId", "PickupDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_Status",
                table: "Bookings",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DriverId",
                table: "Bookings",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_Code",
                table: "Airports",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Companies_CompanyId",
                table: "Bookings",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Customers_CustomerId",
                table: "Bookings",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Drivers_DriverId",
                table: "Bookings",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_Companies_CompanyId",
                table: "Drivers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_Vehicles_VehicleId",
                table: "Drivers",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PricingRules_Airports_AirportId",
                table: "PricingRules",
                column: "AirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PricingRules_Companies_CompanyId",
                table: "PricingRules",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Companies_CompanyId",
                table: "Vehicles",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Companies_CompanyId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Customers_CustomerId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Drivers_DriverId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_Companies_CompanyId",
                table: "Drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_Vehicles_VehicleId",
                table: "Drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_PricingRules_Airports_AirportId",
                table: "PricingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_PricingRules_Companies_CompanyId",
                table: "PricingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Companies_CompanyId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "CompanyBranding");

            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CompanyId_RegistrationNumber",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_PricingRules_AirportId",
                table: "PricingRules");

            migrationBuilder.DropIndex(
                name: "IX_PricingRules_CompanyId_AirportId_VehicleType_IsActive",
                table: "PricingRules");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_CompanyId_Email",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_VehicleId",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CompanyId_PickupDateTime",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CompanyId_Status",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_DriverId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Airports_Code",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Bookings");

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationNumber",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Airports",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
