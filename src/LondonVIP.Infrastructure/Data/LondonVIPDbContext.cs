using LondonVIP.Shared.Models;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;
using LondonVIP.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LondonVIP.Infrastructure.Data;

public class LondonVIPDbContext(DbContextOptions<LondonVIPDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Airport> Airports => Set<Airport>();

    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<CompanyBranding> CompanyBranding => Set<CompanyBranding>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<CorporateAccount> CorporateAccounts => Set<CorporateAccount>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BusinessEventRecord> BusinessEvents => Set<BusinessEventRecord>();
    public DbSet<WorkflowJob> WorkflowJobs => Set<WorkflowJob>();
    public DbSet<WorkflowRule> WorkflowRules => Set<WorkflowRule>();
    public DbSet<DriverLocation> DriverLocations => Set<DriverLocation>();
    public DbSet<JourneySnapshot> JourneySnapshots => Set<JourneySnapshot>();
    public DbSet<CustomerTrackingToken> CustomerTrackingTokens => Set<CustomerTrackingToken>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<DriverShift> DriverShifts => Set<DriverShift>();
    public DbSet<DriverJobDecline> DriverJobDeclines => Set<DriverJobDecline>();
    public DbSet<DriverVehicleIssue> DriverVehicleIssues => Set<DriverVehicleIssue>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<CustomerPreferences> CustomerPreferences => Set<CustomerPreferences>();
    public DbSet<CustomerAccountActivity> CustomerAccountActivities => Set<CustomerAccountActivity>();
    public DbSet<IntegrationWebhookDelivery> IntegrationWebhookDeliveries => Set<IntegrationWebhookDelivery>();
    public DbSet<IntegrationProviderMetric> IntegrationProviderMetrics => Set<IntegrationProviderMetric>();
    public DbSet<IntegrationResourceReference> IntegrationResourceReferences => Set<IntegrationResourceReference>();
    public DbSet<IntegrationCommunicationLog> IntegrationCommunicationLogs => Set<IntegrationCommunicationLog>();
    public DbSet<CrmLead> CrmLeads => Set<CrmLead>(); public DbSet<CrmPipelineStage> CrmPipelineStages => Set<CrmPipelineStage>();
    public DbSet<CrmOpportunity> CrmOpportunities => Set<CrmOpportunity>(); public DbSet<CrmConversation> CrmConversations => Set<CrmConversation>();
    public DbSet<CrmMessage> CrmMessages => Set<CrmMessage>(); public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>(); public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
    public DbSet<CrmDocument> CrmDocuments => Set<CrmDocument>(); public DbSet<CrmReview> CrmReviews => Set<CrmReview>(); public DbSet<CrmCampaign> CrmCampaigns => Set<CrmCampaign>();
    public DbSet<Promotion> Promotions => Set<Promotion>(); public DbSet<PromotionRedemption> PromotionRedemptions => Set<PromotionRedemption>();
    public DbSet<Referral> Referrals => Set<Referral>(); public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>(); public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>(); public DbSet<MarketingCampaign> MarketingCampaigns => Set<MarketingCampaign>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>(); public DbSet<BlogArticle> BlogArticles => Set<BlogArticle>(); public DbSet<SeoRedirect> SeoRedirects => Set<SeoRedirect>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>(); public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>(); public DbSet<LeadCapture> LeadCaptures => Set<LeadCapture>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>(); public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>(); public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<Journal> Journals => Set<Journal>(); public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>(); public DbSet<Supplier> Suppliers => Set<Supplier>(); public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>(); public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>(); public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<VatCode> VatCodes => Set<VatCode>(); public DbSet<VatReturn> VatReturns => Set<VatReturn>(); public DbSet<Budget> Budgets => Set<Budget>(); public DbSet<DriverSettlement> DriverSettlements => Set<DriverSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(user => new { user.CompanyId, user.NormalizedEmail });
            entity.HasOne<Company>().WithMany().HasForeignKey(user => user.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(user => new { user.CompanyId, user.DriverId }).IsUnique().HasFilter("[DriverId] IS NOT NULL");
            entity.HasOne<Driver>().WithMany().HasForeignKey(user => user.DriverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(user => new { user.CompanyId, user.CustomerId }).IsUnique().HasFilter("[CustomerId] IS NOT NULL");
            entity.HasOne<Customer>().WithMany().HasForeignKey(user => user.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntegrationWebhookDelivery>(entity =>
        {
            entity.Property(x => x.ProviderKey).HasMaxLength(100);
            entity.Property(x => x.EventType).HasMaxLength(150);
            entity.Property(x => x.DeliveryId).HasMaxLength(200);
            entity.Property(x => x.Signature).HasMaxLength(1000);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x => new { x.CompanyId, x.ProviderKey, x.DeliveryId }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.NextAttemptAt });
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CrmLead>(e => { e.Property(x=>x.Reference).HasMaxLength(40);e.Property(x=>x.FirstName).HasMaxLength(100);e.Property(x=>x.LastName).HasMaxLength(100);e.Property(x=>x.Email).HasMaxLength(254);e.Property(x=>x.Phone).HasMaxLength(30);e.Property(x=>x.Source).HasMaxLength(100);e.Property(x=>x.InternalNotes).HasMaxLength(4000);e.Property(x=>x.Probability).HasPrecision(5,2);e.Property(x=>x.ExpectedRevenue).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.FollowUpAt});e.HasIndex(x=>new{x.CompanyId,x.Email});e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Customer).WithMany().HasForeignKey(x=>x.CustomerId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<CrmPipelineStage>(e=>{e.Property(x=>x.Name).HasMaxLength(100);e.Property(x=>x.DefaultProbability).HasPrecision(5,2);e.HasIndex(x=>new{x.CompanyId,x.SortOrder}).IsUnique();e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<CrmOpportunity>(e=>{e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.Value).HasPrecision(18,2);e.Property(x=>x.Probability).HasPrecision(5,2);e.Property(x=>x.WinLossReason).HasMaxLength(500);e.HasIndex(x=>new{x.CompanyId,x.PipelineStageId,x.ExpectedCloseAt});e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.PipelineStage).WithMany().HasForeignKey(x=>x.PipelineStageId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Lead).WithMany().HasForeignKey(x=>x.LeadId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Customer).WithMany().HasForeignKey(x=>x.CustomerId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.CorporateAccount).WithMany().HasForeignKey(x=>x.CorporateAccountId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<CrmConversation>(e=>{e.Property(x=>x.ExternalThreadId).HasMaxLength(250);e.Property(x=>x.Subject).HasMaxLength(500);e.Property(x=>x.ParticipantAddress).HasMaxLength(320);e.Property(x=>x.Tags).HasMaxLength(1000);e.HasIndex(x=>new{x.CompanyId,x.Channel,x.ExternalThreadId}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.IsArchived,x.IsUnread,x.UpdatedAt});e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Customer).WithMany().HasForeignKey(x=>x.CustomerId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Lead).WithMany().HasForeignKey(x=>x.LeadId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Booking).WithMany().HasForeignKey(x=>x.BookingId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Quotation).WithMany().HasForeignKey(x=>x.QuotationId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Invoice).WithMany().HasForeignKey(x=>x.InvoiceId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<CrmMessage>(e=>{e.Property(x=>x.ExternalMessageId).HasMaxLength(250);e.Property(x=>x.Sender).HasMaxLength(320);e.Property(x=>x.AttachmentUrl).HasMaxLength(2000);e.Property(x=>x.Status).HasMaxLength(50);e.HasIndex(x=>new{x.CompanyId,x.ExternalMessageId}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.ConversationId,x.SentAt});e.HasOne(x=>x.Conversation).WithMany(x=>x.Messages).HasForeignKey(x=>x.ConversationId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<CrmActivity>(e=>{e.Property(x=>x.Subject).HasMaxLength(500);e.Property(x=>x.Details).HasMaxLength(4000);e.Property(x=>x.ResourceType).HasMaxLength(100);e.HasIndex(x=>new{x.CompanyId,x.CustomerId,x.OccurredAt});e.HasIndex(x=>new{x.CompanyId,x.LeadId,x.OccurredAt});});
        modelBuilder.Entity<CrmTask>(e=>{e.Property(x=>x.Title).HasMaxLength(250);e.Property(x=>x.Description).HasMaxLength(4000);e.Property(x=>x.Recurrence).HasMaxLength(100);e.HasIndex(x=>new{x.CompanyId,x.Status,x.DueAt});});
        modelBuilder.Entity<CrmDocument>(e=>{e.Property(x=>x.Category).HasMaxLength(100);e.Property(x=>x.FileName).HasMaxLength(260);e.Property(x=>x.StoragePath).HasMaxLength(1000);e.HasIndex(x=>new{x.CompanyId,x.CustomerId,x.CreatedAt});e.HasIndex(x=>new{x.CompanyId,x.CorporateAccountId,x.CreatedAt});});
        modelBuilder.Entity<CrmReview>(e=>{e.Property(x=>x.ExternalId).HasMaxLength(250);e.Property(x=>x.Resolution).HasMaxLength(4000);e.HasIndex(x=>new{x.CompanyId,x.Source,x.ExternalId}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.IsComplaint,x.ReviewedAt});});
        modelBuilder.Entity<CrmCampaign>(e=>{e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.AudienceDefinition).HasMaxLength(4000);e.Property(x=>x.Status).HasMaxLength(50);e.Property(x=>x.Cost).HasPrecision(18,2);e.Property(x=>x.Revenue).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Status,x.StartsAt});});
        modelBuilder.Entity<Promotion>(e=>{e.Property(x=>x.Code).HasMaxLength(50);e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.Value).HasPrecision(18,2);e.Property(x=>x.MaximumDiscount).HasPrecision(18,2);e.Property(x=>x.MinimumSpend).HasPrecision(18,2);e.Property(x=>x.PickupPattern).HasMaxLength(250);e.Property(x=>x.DestinationPattern).HasMaxLength(250);e.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.IsActive,x.EffectiveFrom,x.EffectiveTo});e.HasOne(x=>x.Company).WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Airport).WithMany().HasForeignKey(x=>x.AirportId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<PromotionRedemption>(e=>{e.Property(x=>x.DiscountAmount).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.PromotionId,x.CustomerId});e.HasIndex(x=>new{x.CompanyId,x.BookingId}).IsUnique().HasFilter("[BookingId] IS NOT NULL");e.HasOne(x=>x.Promotion).WithMany(x=>x.Redemptions).HasForeignKey(x=>x.PromotionId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<Referral>(e=>{e.Property(x=>x.Code).HasMaxLength(40);e.Property(x=>x.ReferrerType).HasMaxLength(30);e.Property(x=>x.RewardAmount).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.ReferrerType,x.ReferrerId,x.Status});});
        modelBuilder.Entity<LoyaltyAccount>(e=>{e.HasIndex(x=>new{x.CompanyId,x.CustomerId}).IsUnique();e.HasOne(x=>x.Customer).WithMany().HasForeignKey(x=>x.CustomerId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<LoyaltyTransaction>(e=>{e.Property(x=>x.Reason).HasMaxLength(250);e.Property(x=>x.VoucherCode).HasMaxLength(50);e.HasIndex(x=>new{x.CompanyId,x.LoyaltyAccountId,x.CreatedAt});e.HasOne(x=>x.LoyaltyAccount).WithMany(x=>x.Transactions).HasForeignKey(x=>x.LoyaltyAccountId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<NewsletterSubscriber>(e=>{e.Property(x=>x.Email).HasMaxLength(254);e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.Lists).HasMaxLength(1000);e.Property(x=>x.Segments).HasMaxLength(1000);e.Property(x=>x.ConfirmationTokenHash).HasMaxLength(128);e.HasIndex(x=>new{x.CompanyId,x.Email}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.IsConfirmed,x.UnsubscribedAt});});
        modelBuilder.Entity<MarketingCampaign>(e=>{e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.AudienceDefinition).HasMaxLength(4000);e.Property(x=>x.TemplateName).HasMaxLength(200);e.Property(x=>x.Cost).HasPrecision(18,2);e.Property(x=>x.Revenue).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Status,x.ScheduledAt});});
        modelBuilder.Entity<CmsPage>(e=>{e.Property(x=>x.Slug).HasMaxLength(250);e.Property(x=>x.Title).HasMaxLength(250);e.Property(x=>x.PageType).HasMaxLength(80);e.Property(x=>x.MetaTitle).HasMaxLength(70);e.Property(x=>x.MetaDescription).HasMaxLength(170);e.Property(x=>x.CanonicalUrl).HasMaxLength(2000);e.HasIndex(x=>new{x.CompanyId,x.Slug}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.PublishAt});});
        modelBuilder.Entity<BlogArticle>(e=>{e.Property(x=>x.Slug).HasMaxLength(250);e.Property(x=>x.Title).HasMaxLength(250);e.Property(x=>x.Author).HasMaxLength(200);e.Property(x=>x.Category).HasMaxLength(100);e.Property(x=>x.Tags).HasMaxLength(1000);e.Property(x=>x.MetaTitle).HasMaxLength(70);e.Property(x=>x.MetaDescription).HasMaxLength(170);e.HasIndex(x=>new{x.CompanyId,x.Slug}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.PublishAt});});
        modelBuilder.Entity<SeoRedirect>(e=>{e.Property(x=>x.SourcePath).HasMaxLength(1000);e.Property(x=>x.DestinationUrl).HasMaxLength(2000);e.HasIndex(x=>new{x.CompanyId,x.SourcePath}).IsUnique();});
        modelBuilder.Entity<SocialPost>(e=>{e.Property(x=>x.AccountKey).HasMaxLength(100);e.Property(x=>x.ProviderReference).HasMaxLength(250);e.HasIndex(x=>new{x.CompanyId,x.Status,x.ScheduledAt});});
        modelBuilder.Entity<MediaAsset>(e=>{e.Property(x=>x.Folder).HasMaxLength(500);e.Property(x=>x.FileName).HasMaxLength(260);e.Property(x=>x.ContentType).HasMaxLength(150);e.Property(x=>x.StoragePath).HasMaxLength(2000);e.Property(x=>x.Tags).HasMaxLength(1000);e.HasIndex(x=>new{x.CompanyId,x.Folder,x.FileName,x.Version}).IsUnique();});
        modelBuilder.Entity<LeadCapture>(e=>{e.Property(x=>x.Type).HasMaxLength(80);e.Property(x=>x.Source).HasMaxLength(100);e.Property(x=>x.Campaign).HasMaxLength(200);e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.Email).HasMaxLength(254);e.Property(x=>x.Phone).HasMaxLength(30);e.HasIndex(x=>new{x.CompanyId,x.Type,x.CreatedAt});});
        modelBuilder.Entity<LedgerAccount>(e=>{e.Property(x=>x.Code).HasMaxLength(30);e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.OpeningBalance).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();e.HasOne(x=>x.Parent).WithMany().HasForeignKey(x=>x.ParentId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<FiscalYear>(e=>{e.Property(x=>x.Name).HasMaxLength(100);e.HasIndex(x=>new{x.CompanyId,x.StartsOn,x.EndsOn}).IsUnique();});
        modelBuilder.Entity<AccountingPeriod>(e=>{e.Property(x=>x.Name).HasMaxLength(100);e.HasIndex(x=>new{x.CompanyId,x.StartsOn,x.EndsOn}).IsUnique();e.HasOne(x=>x.FiscalYear).WithMany(x=>x.Periods).HasForeignKey(x=>x.FiscalYearId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<Journal>(e=>{e.Property(x=>x.Reference).HasMaxLength(80);e.Property(x=>x.Description).HasMaxLength(500);e.Property(x=>x.SourceType).HasMaxLength(80);e.HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.JournalDate});});
        modelBuilder.Entity<JournalEntry>(e=>{e.Property(x=>x.Description).HasMaxLength(500);e.Property(x=>x.Debit).HasPrecision(18,2);e.Property(x=>x.Credit).HasPrecision(18,2);e.Property(x=>x.Department).HasMaxLength(100);e.Property(x=>x.CostCentre).HasMaxLength(100);e.HasIndex(x=>new{x.CompanyId,x.LedgerAccountId,x.JournalId});e.HasOne(x=>x.Journal).WithMany(x=>x.Entries).HasForeignKey(x=>x.JournalId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.LedgerAccount).WithMany().HasForeignKey(x=>x.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<Supplier>(e=>{e.Property(x=>x.SupplierNumber).HasMaxLength(50);e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.Email).HasMaxLength(254);e.HasIndex(x=>new{x.CompanyId,x.SupplierNumber}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Name});});
        modelBuilder.Entity<SupplierInvoice>(e=>{e.Property(x=>x.SupplierReference).HasMaxLength(100);e.Property(x=>x.NetAmount).HasPrecision(18,2);e.Property(x=>x.VatAmount).HasPrecision(18,2);e.Property(x=>x.TotalAmount).HasPrecision(18,2);e.Property(x=>x.AmountPaid).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.SupplierId,x.SupplierReference}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.DueDate});e.HasOne(x=>x.Supplier).WithMany().HasForeignKey(x=>x.SupplierId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<BankAccount>(e=>{e.Property(x=>x.Name).HasMaxLength(200);e.Property(x=>x.CurrencyCode).HasMaxLength(3);e.Property(x=>x.OpeningBalance).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Name}).IsUnique();});
        modelBuilder.Entity<BankTransaction>(e=>{e.Property(x=>x.Reference).HasMaxLength(100);e.Property(x=>x.Description).HasMaxLength(500);e.Property(x=>x.Amount).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.BankAccountId,x.TransactionDate});e.HasIndex(x=>new{x.CompanyId,x.ReconciliationStatus,x.Amount});e.HasOne(x=>x.BankAccount).WithMany().HasForeignKey(x=>x.BankAccountId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<Expense>(e=>{e.Property(x=>x.Reference).HasMaxLength(80);e.Property(x=>x.Category).HasMaxLength(100);e.Property(x=>x.Description).HasMaxLength(500);e.Property(x=>x.NetAmount).HasPrecision(18,2);e.Property(x=>x.VatAmount).HasPrecision(18,2);e.Property(x=>x.TotalAmount).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.Status,x.ExpenseDate});});
        modelBuilder.Entity<VatCode>(e=>{e.Property(x=>x.Code).HasMaxLength(30);e.Property(x=>x.Name).HasMaxLength(100);e.Property(x=>x.Rate).HasPrecision(5,2);e.HasIndex(x=>new{x.CompanyId,x.Code}).IsUnique();});
        modelBuilder.Entity<VatReturn>(e=>{e.Property(x=>x.OutputVat).HasPrecision(18,2);e.Property(x=>x.InputVat).HasPrecision(18,2);e.Property(x=>x.VatDue).HasPrecision(18,2);e.Property(x=>x.Status).HasMaxLength(30);e.HasIndex(x=>new{x.CompanyId,x.PeriodStart,x.PeriodEnd}).IsUnique();});
        modelBuilder.Entity<Budget>(e=>{e.Property(x=>x.Department).HasMaxLength(100);e.Property(x=>x.CostCentre).HasMaxLength(100);e.Property(x=>x.Amount).HasPrecision(18,2);e.Property(x=>x.ForecastAmount).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.FiscalYearId,x.Department,x.CostCentre,x.LedgerAccountId}).IsUnique();});
        modelBuilder.Entity<DriverSettlement>(e=>{e.Property(x=>x.Reference).HasMaxLength(80);e.Property(x=>x.GrossFares).HasPrecision(18,2);e.Property(x=>x.Commission).HasPrecision(18,2);e.Property(x=>x.Bonuses).HasPrecision(18,2);e.Property(x=>x.Penalties).HasPrecision(18,2);e.Property(x=>x.Adjustments).HasPrecision(18,2);e.Property(x=>x.NetPayable).HasPrecision(18,2);e.HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();e.HasIndex(x=>new{x.CompanyId,x.DriverId,x.PeriodStart,x.PeriodEnd});e.HasOne(x=>x.Driver).WithMany().HasForeignKey(x=>x.DriverId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<IntegrationProviderMetric>(entity =>
        {
            entity.Property(x => x.ProviderKey).HasMaxLength(100);
            entity.Property(x => x.Operation).HasMaxLength(150);
            entity.Property(x => x.ErrorCode).HasMaxLength(100);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x => new { x.CompanyId, x.ProviderKey, x.OccurredAt });
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntegrationResourceReference>(entity =>
        {
            entity.Property(x => x.ProviderKey).HasMaxLength(100);
            entity.Property(x => x.ResourceType).HasMaxLength(100);
            entity.Property(x => x.ProviderResourceId).HasMaxLength(250);
            entity.HasIndex(x => new { x.CompanyId, x.ProviderKey, x.ResourceType, x.LocalResourceId }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.ProviderKey, x.ProviderResourceId }).IsUnique();
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntegrationCommunicationLog>(entity =>
        {
            entity.Property(x => x.ProviderKey).HasMaxLength(100);
            entity.Property(x => x.Channel).HasMaxLength(50);
            entity.Property(x => x.Recipient).HasMaxLength(320);
            entity.Property(x => x.ProviderReference).HasMaxLength(250);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x => new { x.CompanyId, x.ProviderKey, x.ProviderReference });
            entity.HasIndex(x => new { x.CompanyId, x.BookingId, x.CreatedAt });
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.Property(item => item.EventType).HasMaxLength(100);
            entity.Property(item => item.Category).HasMaxLength(100);
            entity.Property(item => item.Outcome).HasMaxLength(50);
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.IpAddress).HasMaxLength(64);
            entity.Property(item => item.UserAgent).HasMaxLength(512);
            entity.Property(item => item.CorrelationId).HasMaxLength(100);
            entity.Property(item => item.ResourceType).HasMaxLength(100);
            entity.Property(item => item.ResourceIdentifier).HasMaxLength(200);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => new { item.CompanyId, item.Timestamp });
            entity.HasIndex(item => new { item.EventType, item.Timestamp });
        });
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(booking => booking.BookingReference).HasMaxLength(40);
            entity.Property(booking => booking.FlightNumber).HasMaxLength(20);
            entity.Property(booking => booking.CustomerNotes).HasMaxLength(2000);
            entity.Property(booking => booking.InternalNotes).HasMaxLength(4000);
            entity.Property(booking => booking.PurchaseOrderReference).HasMaxLength(100);
            entity.Property(booking => booking.BillingReference).HasMaxLength(100);
            entity.Property(booking => booking.BaseFare).HasPrecision(18, 2);
            entity.Property(booking => booking.Extras).HasPrecision(18, 2);
            entity.Property(booking => booking.TotalFare).HasPrecision(18, 2);
            entity.HasIndex(booking => new { booking.CompanyId, booking.BookingReference }).IsUnique();
            entity.HasIndex(booking => new { booking.CompanyId, booking.PickupDateTime });
            entity.HasIndex(booking => new { booking.CompanyId, booking.Status });
            entity.HasIndex(booking => new { booking.CompanyId, booking.CorporateAccountId });
            entity.HasOne(booking => booking.Company).WithMany(company => company.Bookings)
                .HasForeignKey(booking => booking.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Customer).WithMany().HasForeignKey(booking => booking.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Driver).WithMany().HasForeignKey(booking => booking.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Airport).WithMany().HasForeignKey(booking => booking.AirportId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.CorporateAccount).WithMany().HasForeignKey(booking => booking.CorporateAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CorporateAccount>(entity =>
        {
            entity.Property(x => x.AccountNumber).HasMaxLength(30);
            entity.Property(x => x.AccountName).HasMaxLength(200);
            entity.Property(x => x.TradingName).HasMaxLength(200);
            entity.Property(x => x.PrimaryContactName).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.BillingEmail).HasMaxLength(254);
            entity.Property(x => x.AddressLine1).HasMaxLength(250);
            entity.Property(x => x.AddressLine2).HasMaxLength(250);
            entity.Property(x => x.TownCity).HasMaxLength(100);
            entity.Property(x => x.Postcode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(100);
            entity.Property(x => x.DefaultPurchaseOrderReference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
            entity.Property(x => x.CurrentBalance).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.CompanyId, x.AccountNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.IsActive });
            entity.HasIndex(x => new { x.CompanyId, x.IsOnHold });
            entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(customer => customer.SecondaryPhone).HasMaxLength(30);
            entity.Property(customer => customer.Address).HasMaxLength(500);
            entity.Property(customer => customer.Postcode).HasMaxLength(20);
            entity.Property(customer => customer.Notes).HasMaxLength(4000);
            entity.HasIndex(customer => new { customer.CompanyId, customer.Email });
            entity.HasOne(customer => customer.Company).WithMany(company => company.Customers)
                .HasForeignKey(customer => customer.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.Property(driver => driver.DriverNumber).HasMaxLength(50);
            entity.Property(driver => driver.Notes).HasMaxLength(4000);
            entity.Property(driver => driver.DrivingLicenceNumber).HasMaxLength(100);
            entity.Property(driver => driver.PrivateHireLicenceNumber).HasMaxLength(100);
            entity.HasIndex(driver => new { driver.CompanyId, driver.Email });
            entity.HasIndex(driver => new { driver.CompanyId, driver.VehicleId }).IsUnique().HasFilter("[VehicleId] IS NOT NULL");
            entity.HasIndex(driver => new { driver.CompanyId, driver.AvailabilityStatus, driver.IsActive });
            entity.HasOne(driver => driver.Company).WithMany(company => company.Drivers)
                .HasForeignKey(driver => driver.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(driver => driver.Vehicle).WithMany().HasForeignKey(driver => driver.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.Property(vehicle => vehicle.Colour).HasMaxLength(50);
            entity.Property(vehicle => vehicle.Notes).HasMaxLength(4000);
            entity.HasIndex(vehicle => new { vehicle.CompanyId, vehicle.RegistrationNumber }).IsUnique();
            entity.HasOne(vehicle => vehicle.Company).WithMany(company => company.Vehicles)
                .HasForeignKey(vehicle => vehicle.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.Property(rule => rule.BasePrice).HasPrecision(18, 2);
            entity.Property(rule => rule.AirportPickupSupplement).HasPrecision(18, 2);
            entity.Property(rule => rule.WaitingChargePerHour).HasPrecision(18, 2);
            entity.Property(rule => rule.Amount).HasPrecision(18, 2);
            entity.Property(rule => rule.Percentage).HasPrecision(7, 4);
            entity.Property(rule => rule.UnitRate).HasPrecision(18, 4);
            entity.Property(rule => rule.IncludedUnits).HasPrecision(18, 4);
            entity.Property(rule => rule.Name).HasMaxLength(200);
            entity.Property(rule => rule.PickupPostcode).HasMaxLength(20);
            entity.Property(rule => rule.DestinationPostcode).HasMaxLength(20);
            entity.Property(rule => rule.PickupZone).HasMaxLength(100);
            entity.Property(rule => rule.DestinationZone).HasMaxLength(100);
            entity.Property(rule => rule.PromotionCode).HasMaxLength(100);
            entity.HasIndex(rule => new { rule.CompanyId, rule.RuleType, rule.VehicleType, rule.IsActive, rule.Priority });
            entity.HasIndex(rule => new { rule.CompanyId, rule.EffectiveFrom, rule.EffectiveTo });
            entity.HasOne(rule => rule.Company).WithMany(company => company.PricingRules)
                .HasForeignKey(rule => rule.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Airport>().WithMany().HasForeignKey(rule => rule.AirportId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Airport>().HasIndex(airport => airport.Code).IsUnique();

        modelBuilder.Entity<Quotation>(entity =>
        {
            entity.Property(item=>item.QuoteReference).HasMaxLength(40);entity.Property(item=>item.PickupAddress).HasMaxLength(500);entity.Property(item=>item.Destination).HasMaxLength(500);
            entity.Property(item=>item.FlightNumber).HasMaxLength(20);entity.Property(item=>item.Notes).HasMaxLength(2000);
            entity.Property(item=>item.BaseFare).HasPrecision(18,2);entity.Property(item=>item.Extras).HasPrecision(18,2);entity.Property(item=>item.DiscountTotal).HasPrecision(18,2);entity.Property(item=>item.TotalFare).HasPrecision(18,2);
            entity.HasIndex(item=>new{item.CompanyId,item.QuoteReference}).IsUnique();entity.HasIndex(item=>new{item.CompanyId,item.Status,item.ExpiresAt});entity.HasIndex(item=>new{item.CompanyId,item.CustomerId});entity.HasIndex(item=>item.ConvertedBookingId).IsUnique().HasFilter("[ConvertedBookingId] IS NOT NULL");
            entity.HasOne(item=>item.Company).WithMany(item=>item.Quotations).HasForeignKey(item=>item.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item=>item.Customer).WithMany().HasForeignKey(item=>item.CustomerId).OnDelete(DeleteBehavior.Restrict);entity.HasOne(item=>item.CorporateAccount).WithMany().HasForeignKey(item=>item.CorporateAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item=>item.ConvertedBooking).WithOne().HasForeignKey<Quotation>(item=>item.ConvertedBookingId).OnDelete(DeleteBehavior.Restrict);entity.HasOne(item=>item.Airport).WithMany().HasForeignKey(item=>item.AirportId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Notification>(entity=>
        {
            entity.Property(x=>x.Recipient).HasMaxLength(320);entity.Property(x=>x.Subject).HasMaxLength(300);entity.Property(x=>x.Body).HasMaxLength(4000);entity.Property(x=>x.TemplateName).HasMaxLength(100);entity.Property(x=>x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x=>new{x.CompanyId,x.Status,x.CreatedAt});entity.HasIndex(x=>new{x.CompanyId,x.Recipient});entity.HasIndex(x=>new{x.CompanyId,x.CorrelationId});
            entity.HasOne(x=>x.Company).WithMany(x=>x.Notifications).HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BusinessEventRecord>(entity=>
        {
            entity.Property(x=>x.EventType).HasMaxLength(100);entity.Property(x=>x.ResourceType).HasMaxLength(100);entity.Property(x=>x.PayloadJson).HasMaxLength(8000);entity.Property(x=>x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x=>new{x.CompanyId,x.OccurredAt});entity.HasIndex(x=>new{x.CompanyId,x.CorrelationId});entity.HasOne(x=>x.Company).WithMany(x=>x.BusinessEvents).HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkflowJob>(entity=>
        {
            entity.Property(x=>x.WorkflowType).HasMaxLength(100);entity.Property(x=>x.PayloadJson).HasMaxLength(8000);entity.Property(x=>x.CorrelationId).HasMaxLength(100);entity.Property(x=>x.LastError).HasMaxLength(2000);entity.Property(x=>x.Recurrence).HasMaxLength(100);
            entity.HasIndex(x=>new{x.CompanyId,x.Status,x.ScheduledAt});entity.HasIndex(x=>new{x.CompanyId,x.CorrelationId,x.WorkflowType}).IsUnique();entity.HasOne(x=>x.Company).WithMany(x=>x.WorkflowJobs).HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);entity.HasOne(x=>x.BusinessEvent).WithMany().HasForeignKey(x=>x.BusinessEventId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<WorkflowRule>(entity=>
        {
            entity.Property(x=>x.Name).HasMaxLength(150);entity.Property(x=>x.EventType).HasMaxLength(100);entity.Property(x=>x.ConditionField).HasMaxLength(100);entity.Property(x=>x.Operator).HasMaxLength(30);entity.Property(x=>x.ComparisonValue).HasMaxLength(500);entity.Property(x=>x.Action).HasMaxLength(100);
            entity.HasIndex(x=>new{x.CompanyId,x.EventType,x.IsActive,x.Priority});entity.HasOne(x=>x.Company).WithMany(x=>x.WorkflowRules).HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DriverLocation>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.DriverId, x.RecordedAt });
            entity.HasIndex(x => new { x.CompanyId, x.BookingId, x.RecordedAt });
            entity.HasOne(x => x.Company).WithMany(x => x.DriverLocations).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<JourneySnapshot>(entity =>
        {
            entity.Property(x => x.RouteJson).HasMaxLength(16000);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.HasIndex(x => new { x.CompanyId, x.BookingId, x.CapturedAt });
            entity.HasOne(x => x.Company).WithMany(x => x.JourneySnapshots).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CustomerTrackingToken>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.BookingId, x.ExpiresAt });
            entity.HasOne(x => x.Company).WithMany(x => x.CustomerTrackingTokens).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Geofence>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Type).HasMaxLength(50);
            entity.HasIndex(x => new { x.CompanyId, x.Type, x.IsActive });
            entity.HasOne(x => x.Company).WithMany(x => x.Geofences).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DriverShift>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyId, x.DriverId, x.StartedAt });
            entity.HasOne(x => x.Company).WithMany(x => x.DriverShifts).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DriverJobDecline>(entity =>
        {
            entity.Property(x => x.Reason).HasMaxLength(100); entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.CompanyId, x.DriverId, x.CreatedAt });
            entity.HasIndex(x => new { x.CompanyId, x.BookingId });
            entity.HasOne(x => x.Company).WithMany(x => x.DriverJobDeclines).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DriverVehicleIssue>(entity =>
        {
            entity.Property(x => x.Category).HasMaxLength(100); entity.Property(x => x.Severity).HasMaxLength(30); entity.Property(x => x.Description).HasMaxLength(2000); entity.Property(x => x.Status).HasMaxLength(30);
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.CompanyId, x.DriverId, x.CreatedAt });
            entity.HasOne(x => x.Company).WithMany(x => x.DriverVehicleIssues).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.Property(x => x.Label).HasMaxLength(80); entity.Property(x => x.AddressLine1).HasMaxLength(250); entity.Property(x => x.AddressLine2).HasMaxLength(250); entity.Property(x => x.City).HasMaxLength(100); entity.Property(x => x.Postcode).HasMaxLength(20);
            entity.HasIndex(x => new { x.CompanyId, x.CustomerId, x.Label });
            entity.HasOne(x => x.Company).WithMany(x => x.CustomerAddresses).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CustomerPreferences>(entity =>
        {
            entity.HasKey(x => x.CustomerId); entity.Property(x => x.EmergencyContactName).HasMaxLength(150); entity.Property(x => x.EmergencyContactPhone).HasMaxLength(30); entity.Property(x => x.Language).HasMaxLength(10);
            entity.HasIndex(x => x.CompanyId); entity.HasOne(x => x.Customer).WithOne().HasForeignKey<CustomerPreferences>(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CustomerAccountActivity>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(100); entity.Property(x => x.IpAddress).HasMaxLength(64); entity.HasIndex(x => new { x.CompanyId, x.CustomerId, x.OccurredAt }); entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(company => company.Slug).IsUnique();
            entity.Property(company => company.TradingName).HasMaxLength(200);
            entity.Property(company => company.LegalName).HasMaxLength(200);
            entity.Property(company => company.Slug).HasMaxLength(100);
            entity.Property(company => company.CurrencyCode).HasMaxLength(3);
            entity.Property(company => company.TimeZone).HasMaxLength(100);
        });

        modelBuilder.Entity<CompanySettings>(entity =>
        {
            entity.HasKey(settings => settings.CompanyId);
            entity.HasOne(settings => settings.Company).WithOne(company => company.Settings)
                .HasForeignKey<CompanySettings>(settings => settings.CompanyId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(settings => settings.WaitingChargePerHour).HasPrecision(18, 2);
            entity.Property(settings => settings.DefaultAirportPickupSupplement).HasPrecision(18, 2);
            entity.Property(settings => settings.DriverCommissionPercentage).HasPrecision(5, 2);
            entity.Property(settings => settings.DriverWeeklySubscriptionAmount).HasPrecision(18, 2);
            entity.Property(settings => settings.VatRate).HasPrecision(5, 2);
        });

        modelBuilder.Entity<CompanyBranding>(entity =>
        {
            entity.HasKey(branding => branding.CompanyId);
            entity.HasOne(branding => branding.Company).WithOne(company => company.Branding)
                .HasForeignKey<CompanyBranding>(branding => branding.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice Configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(invoice => invoice.InvoiceNumber).HasMaxLength(50);
            entity.Property(invoice => invoice.Subtotal).HasPrecision(18, 2);
            entity.Property(invoice => invoice.TaxAmount).HasPrecision(18, 2);
            entity.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
            entity.Property(invoice => invoice.AmountPaid).HasPrecision(18, 2);
            entity.Property(invoice => invoice.BalanceDue).HasPrecision(18, 2);
            entity.Property(invoice => invoice.Notes).HasMaxLength(4000);
            
            // Tenant-safety indexes
            entity.HasIndex(invoice => new { invoice.CompanyId, invoice.InvoiceNumber }).IsUnique();
            entity.HasIndex(invoice => new { invoice.CompanyId, invoice.Status });
            entity.HasIndex(invoice => new { invoice.CompanyId, invoice.DueDate });
            entity.HasIndex(invoice => new { invoice.CompanyId, invoice.CorporateAccountId });
            entity.HasIndex(invoice => new { invoice.CompanyId, invoice.CustomerId });
            
            entity.HasOne(invoice => invoice.Company).WithMany(company => company.Invoices)
                .HasForeignKey(invoice => invoice.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(invoice => invoice.CorporateAccount).WithMany()
                .HasForeignKey(invoice => invoice.CorporateAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(invoice => invoice.Customer).WithMany()
                .HasForeignKey(invoice => invoice.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceNumberSequence>(entity =>
        {
            entity.HasKey(sequence => sequence.CompanyId);
            entity.Property(sequence => sequence.NextNumber).IsConcurrencyToken();
            entity.HasOne(sequence => sequence.Company).WithMany()
                .HasForeignKey(sequence => sequence.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // InvoiceLine Configuration
        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.Property(line => line.Description).HasMaxLength(500);
            entity.Property(line => line.Quantity).HasPrecision(12, 2);
            entity.Property(line => line.UnitPrice).HasPrecision(18, 2);
            entity.Property(line => line.TaxRate).HasPrecision(5, 2);
            entity.Property(line => line.LineSubtotal).HasPrecision(18, 2);
            entity.Property(line => line.TaxAmount).HasPrecision(18, 2);
            entity.Property(line => line.LineTotal).HasPrecision(18, 2);
            
            entity.HasIndex(line => line.InvoiceId);
            entity.HasIndex(line => line.BookingId);
            
            entity.HasOne(line => line.Invoice).WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => line.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(line => line.Booking).WithMany()
                .HasForeignKey(line => line.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Payment Configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(payment => payment.PaymentReference).HasMaxLength(100);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Notes).HasMaxLength(4000);
            
            // Tenant-safety indexes
            entity.HasIndex(payment => new { payment.CompanyId, payment.PaymentReference });
            entity.HasIndex(payment => new { payment.CompanyId, payment.PaymentDate });
            entity.HasIndex(payment => new { payment.CompanyId, payment.CorporateAccountId });
            entity.HasIndex(payment => new { payment.CompanyId, payment.CustomerId });
            
            entity.HasOne(payment => payment.Company).WithMany(company => company.Payments)
                .HasForeignKey(payment => payment.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.CorporateAccount).WithMany()
                .HasForeignKey(payment => payment.CorporateAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.Customer).WithMany()
                .HasForeignKey(payment => payment.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PaymentAllocation Configuration
        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.Property(allocation => allocation.Amount).HasPrecision(18, 2);
            
            entity.HasIndex(allocation => allocation.PaymentId);
            entity.HasIndex(allocation => allocation.InvoiceId);
            
            entity.HasOne(allocation => allocation.Payment).WithMany(payment => payment.Allocations)
                .HasForeignKey(allocation => allocation.PaymentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(allocation => allocation.Invoice).WithMany(invoice => invoice.Allocations)
                .HasForeignKey(allocation => allocation.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        var seedTimestamp = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Company>().HasData(new Company
        {
            Id = LondonVipCompany.Id,
            TradingName = "London VIP Cars",
            LegalName = string.Empty,
            Slug = LondonVipCompany.Slug,
            Email = string.Empty,
            Phone = string.Empty,
            WebsiteUrl = string.Empty,
            AddressLine1 = string.Empty,
            AddressLine2 = string.Empty,
            City = "London",
            Postcode = string.Empty,
            Country = "United Kingdom",
            TimeZone = "Europe/London",
            CurrencyCode = "GBP",
            IsActive = true,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        modelBuilder.Entity<CompanySettings>().HasData(new CompanySettings
        {
            CompanyId = LondonVipCompany.Id,
            MinimumBookingNoticeMinutes = 0,
            FreeAirportWaitingMinutes = 0,
            WaitingChargePerHour = 0m,
            DefaultAirportPickupSupplement = 0m,
            MeetAndGreetEnabled = false,
            DriverCommissionPercentage = 0m,
            DriverWeeklySubscriptionAmount = 0m,
            VatEnabled = false,
            VatRate = 0m,
            InvoicePrefix = "LVC",
            DefaultLanguage = "en-GB"
        });

        modelBuilder.Entity<CompanyBranding>().HasData(new CompanyBranding
        {
            CompanyId = LondonVipCompany.Id,
            PrimaryColour = "#153F37",
            SecondaryColour = "#0C2E29",
            AccentColour = "#C49A4A",
            LogoUrl = string.Empty,
            FaviconUrl = string.Empty,
            CustomerWebsiteTitle = "London VIP Cars",
            CustomerWebsiteTagline = "Every journey, considered."
        });

        modelBuilder.Entity<Airport>().HasData(
            new Airport
            {
                Id = new Guid("6cbe8f65-2943-4ce1-91fe-f1966d37b334"),
                Code = "LHR",
                Name = "Heathrow",
                IsActive = true
            },
            new Airport
            {
                Id = new Guid("a816bb40-d225-4c24-bdbc-a7c2b96f6b9b"),
                Code = "LGW",
                Name = "Gatwick",
                IsActive = true
            },
            new Airport
            {
                Id = new Guid("12cb02c5-a575-4a50-ab17-b92d81dd331e"),
                Code = "LTN",
                Name = "Luton",
                IsActive = true
            },
            new Airport
            {
                Id = new Guid("1e83d9e4-d35a-40f9-9a4e-fba1ee003b55"),
                Code = "STN",
                Name = "Stansted",
                IsActive = true
            });

        // SQLite cannot natively compare or order DateTimeOffset values. The test provider
        // stores them as UTC ticks so relational dashboard queries retain SQL semantics.
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true)
        {
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(value => value.HasValue ? value.Value.UtcTicks : null, value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset)) property.SetValueConverter(dateTimeOffsetConverter);
                else if (property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(nullableDateTimeOffsetConverter);
            }
        }
    }
}
