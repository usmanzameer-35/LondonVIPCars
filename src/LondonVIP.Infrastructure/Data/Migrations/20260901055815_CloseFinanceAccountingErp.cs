using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CloseFinanceAccountingErp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingPostingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DebitAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPostingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingPostingProfiles_LedgerAccounts_CreditAccountId",
                        column: x => x.CreditAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingPostingProfiles_LedgerAccounts_DebitAccountId",
                        column: x => x.DebitAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceRecordHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceRecordHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceRecordStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceRecordStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPostingProfiles_CompanyId_EventType",
                table: "AccountingPostingProfiles",
                columns: new[] { "CompanyId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPostingProfiles_CreditAccountId",
                table: "AccountingPostingProfiles",
                column: "CreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPostingProfiles_DebitAccountId",
                table: "AccountingPostingProfiles",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRecordHistories_CompanyId_ResourceType_ResourceId_CreatedAt",
                table: "FinanceRecordHistories",
                columns: new[] { "CompanyId", "ResourceType", "ResourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRecordStates_CompanyId_ResourceType_IsArchived_IsDeleted",
                table: "FinanceRecordStates",
                columns: new[] { "CompanyId", "ResourceType", "IsArchived", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceRecordStates_CompanyId_ResourceType_ResourceId",
                table: "FinanceRecordStates",
                columns: new[] { "CompanyId", "ResourceType", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingPostingProfiles");

            migrationBuilder.DropTable(
                name: "FinanceRecordHistories");

            migrationBuilder.DropTable(
                name: "FinanceRecordStates");
        }
    }
}
