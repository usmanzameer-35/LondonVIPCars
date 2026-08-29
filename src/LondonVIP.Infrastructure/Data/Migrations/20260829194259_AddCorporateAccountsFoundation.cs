using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateAccountsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingReference",
                table: "Bookings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorporateAccountId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderReference",
                table: "Bookings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorporateAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TradingName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BillingEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TownCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingTerms = table.Column<int>(type: "int", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsOnHold = table.Column<bool>(type: "bit", nullable: false),
                    PurchaseOrderRequired = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPurchaseOrderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CompanyId_CorporateAccountId",
                table: "Bookings",
                columns: new[] { "CompanyId", "CorporateAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CorporateAccountId",
                table: "Bookings",
                column: "CorporateAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_CompanyId_AccountNumber",
                table: "CorporateAccounts",
                columns: new[] { "CompanyId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_CompanyId_IsActive",
                table: "CorporateAccounts",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateAccounts_CompanyId_IsOnHold",
                table: "CorporateAccounts",
                columns: new[] { "CompanyId", "IsOnHold" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_CorporateAccounts_CorporateAccountId",
                table: "Bookings",
                column: "CorporateAccountId",
                principalTable: "CorporateAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_CorporateAccounts_CorporateAccountId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "CorporateAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CompanyId_CorporateAccountId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CorporateAccountId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BillingReference",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CorporateAccountId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderReference",
                table: "Bookings");
        }
    }
}
