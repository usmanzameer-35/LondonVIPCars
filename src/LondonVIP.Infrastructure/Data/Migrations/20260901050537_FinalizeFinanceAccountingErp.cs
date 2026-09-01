using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeFinanceAccountingErp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AccountingJournalLinks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalId",
                table: "AccountingJournalLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankTransactionMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LedgerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MatchType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReversedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactionMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankTransactionMatches_BankTransactions_BankTransactionId",
                        column: x => x.BankTransactionId,
                        principalTable: "BankTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionMatches_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionMatches_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactionMatches_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VatSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VatReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VatSubmissions_VatReturns_VatReturnId",
                        column: x => x.VatReturnId,
                        principalTable: "VatReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingJournalLinks_CompanyId_CorrelationId",
                table: "AccountingJournalLinks",
                columns: new[] { "CompanyId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingJournalLinks_ReversalJournalId",
                table: "AccountingJournalLinks",
                column: "ReversalJournalId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_BankTransactionId",
                table: "BankTransactionMatches",
                column: "BankTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_CompanyId_BankTransactionId_Status",
                table: "BankTransactionMatches",
                columns: new[] { "CompanyId", "BankTransactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_CompanyId_CorrelationId",
                table: "BankTransactionMatches",
                columns: new[] { "CompanyId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_LedgerAccountId",
                table: "BankTransactionMatches",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_PaymentId",
                table: "BankTransactionMatches",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionMatches_SupplierInvoiceId",
                table: "BankTransactionMatches",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VatSubmissions_CompanyId_CorrelationId",
                table: "VatSubmissions",
                columns: new[] { "CompanyId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VatSubmissions_CompanyId_VatReturnId_CreatedAt",
                table: "VatSubmissions",
                columns: new[] { "CompanyId", "VatReturnId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VatSubmissions_VatReturnId",
                table: "VatSubmissions",
                column: "VatReturnId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingJournalLinks_Journals_ReversalJournalId",
                table: "AccountingJournalLinks",
                column: "ReversalJournalId",
                principalTable: "Journals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingJournalLinks_Journals_ReversalJournalId",
                table: "AccountingJournalLinks");

            migrationBuilder.DropTable(
                name: "BankTransactionMatches");

            migrationBuilder.DropTable(
                name: "VatSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_AccountingJournalLinks_CompanyId_CorrelationId",
                table: "AccountingJournalLinks");

            migrationBuilder.DropIndex(
                name: "IX_AccountingJournalLinks_ReversalJournalId",
                table: "AccountingJournalLinks");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AccountingJournalLinks");

            migrationBuilder.DropColumn(
                name: "ReversalJournalId",
                table: "AccountingJournalLinks");
        }
    }
}
