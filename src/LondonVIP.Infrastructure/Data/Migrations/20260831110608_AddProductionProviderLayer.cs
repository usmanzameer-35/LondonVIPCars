using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionProviderLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCommunicationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCommunicationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationCommunicationLogs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegrationCommunicationLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationProviderMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationProviderMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationProviderMetrics_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationResourceReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderResourceId = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationResourceReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationResourceReferences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationWebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationWebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationWebhookDeliveries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCommunicationLogs_BookingId",
                table: "IntegrationCommunicationLogs",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCommunicationLogs_CompanyId_BookingId_CreatedAt",
                table: "IntegrationCommunicationLogs",
                columns: new[] { "CompanyId", "BookingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCommunicationLogs_CompanyId_ProviderKey_ProviderReference",
                table: "IntegrationCommunicationLogs",
                columns: new[] { "CompanyId", "ProviderKey", "ProviderReference" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProviderMetrics_CompanyId_ProviderKey_OccurredAt",
                table: "IntegrationProviderMetrics",
                columns: new[] { "CompanyId", "ProviderKey", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationResourceReferences_CompanyId_ProviderKey_ProviderResourceId",
                table: "IntegrationResourceReferences",
                columns: new[] { "CompanyId", "ProviderKey", "ProviderResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationResourceReferences_CompanyId_ProviderKey_ResourceType_LocalResourceId",
                table: "IntegrationResourceReferences",
                columns: new[] { "CompanyId", "ProviderKey", "ResourceType", "LocalResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationWebhookDeliveries_CompanyId_ProviderKey_DeliveryId",
                table: "IntegrationWebhookDeliveries",
                columns: new[] { "CompanyId", "ProviderKey", "DeliveryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationWebhookDeliveries_CompanyId_Status_NextAttemptAt",
                table: "IntegrationWebhookDeliveries",
                columns: new[] { "CompanyId", "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCommunicationLogs");

            migrationBuilder.DropTable(
                name: "IntegrationProviderMetrics");

            migrationBuilder.DropTable(
                name: "IntegrationResourceReferences");

            migrationBuilder.DropTable(
                name: "IntegrationWebhookDeliveries");
        }
    }
}
