using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LondonVIP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalesMarketingGrowthPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SocialPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "SocialPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "SocialPosts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishStatus",
                table: "SocialPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmationExpiresAt",
                table: "NewsletterSubscribers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationSendCount",
                table: "NewsletterSubscribers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CdnUrl",
                table: "MediaAssets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "MediaAssets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "MediaAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "MediaAssets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "MediaAssets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebPPath",
                table: "MediaAssets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceRule",
                table: "MarketingCampaigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MarketingCampaigns",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "AiMarketingGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMarketingGenerations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnonymousIdHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SessionKeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Medium = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Campaign = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferrerHost = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketingCampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignDeliveries_MarketingCampaigns_MarketingCampaignId",
                        column: x => x.MarketingCampaignId,
                        principalTable: "MarketingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentTaxonomies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTaxonomies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthAuthorizationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    StateHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeVerifierProtected = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthAuthorizationStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialProviderConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    AccountKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProtectedAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastHealthCheckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialProviderConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnalyticsSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    QuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarketingCampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsEvents_AnalyticsSessions_AnalyticsSessionId",
                        column: x => x.AnalyticsSessionId,
                        principalTable: "AnalyticsSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMarketingGenerations_CompanyId_GenerationType_CreatedAt",
                table: "AiMarketingGenerations",
                columns: new[] { "CompanyId", "GenerationType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_AnalyticsSessionId",
                table: "AnalyticsEvents",
                column: "AnalyticsSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_CompanyId_Type_OccurredAt",
                table: "AnalyticsEvents",
                columns: new[] { "CompanyId", "Type", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSessions_CompanyId_SessionKeyHash",
                table: "AnalyticsSessions",
                columns: new[] { "CompanyId", "SessionKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSessions_CompanyId_StartedAt",
                table: "AnalyticsSessions",
                columns: new[] { "CompanyId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignDeliveries_CompanyId_MarketingCampaignId_Recipient",
                table: "CampaignDeliveries",
                columns: new[] { "CompanyId", "MarketingCampaignId", "Recipient" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignDeliveries_CompanyId_Status_NextAttemptAt",
                table: "CampaignDeliveries",
                columns: new[] { "CompanyId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignDeliveries_MarketingCampaignId",
                table: "CampaignDeliveries",
                column: "MarketingCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTaxonomies_CompanyId_Type_Slug",
                table: "ContentTaxonomies",
                columns: new[] { "CompanyId", "Type", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthAuthorizationStates_CompanyId_ExpiresAt_ConsumedAt",
                table: "OAuthAuthorizationStates",
                columns: new[] { "CompanyId", "ExpiresAt", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthAuthorizationStates_CompanyId_StateHash",
                table: "OAuthAuthorizationStates",
                columns: new[] { "CompanyId", "StateHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialProviderConnections_CompanyId_Provider_AccountKey",
                table: "SocialProviderConnections",
                columns: new[] { "CompanyId", "Provider", "AccountKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialProviderConnections_CompanyId_Status_TokenExpiresAt",
                table: "SocialProviderConnections",
                columns: new[] { "CompanyId", "Status", "TokenExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMarketingGenerations");

            migrationBuilder.DropTable(
                name: "AnalyticsEvents");

            migrationBuilder.DropTable(
                name: "CampaignDeliveries");

            migrationBuilder.DropTable(
                name: "ContentTaxonomies");

            migrationBuilder.DropTable(
                name: "OAuthAuthorizationStates");

            migrationBuilder.DropTable(
                name: "SocialProviderConnections");

            migrationBuilder.DropTable(
                name: "AnalyticsSessions");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "PublishStatus",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "ConfirmationExpiresAt",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "ConfirmationSendCount",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "CdnUrl",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "WebPPath",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "RecurrenceRule",
                table: "MarketingCampaigns");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MarketingCampaigns");
        }
    }
}
