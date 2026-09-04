using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Pricing;
using LondonVIP.Shared.Pricing;
using LondonVIP.Infrastructure.Tenancy;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Api.Security;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Invoicing;
using LondonVIP.Infrastructure.Invoicing;
using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Infrastructure.Quotations;
using LondonVIP.Shared.Quotations;
using LondonVIP.Infrastructure.Notifications;
using LondonVIP.Shared.Notifications;
using LondonVIP.Infrastructure.Dashboard;
using LondonVIP.Shared.Dashboard;
using LondonVIP.Infrastructure.Dispatch;
using LondonVIP.Shared.Dispatch;
using LondonVIP.Infrastructure.Workflows;
using LondonVIP.Shared.Workflows;
using LondonVIP.Infrastructure.Maps;
using LondonVIP.Infrastructure.Integrations;
using LondonVIP.Shared.Integrations;
using LondonVIP.Infrastructure.Crm;
using LondonVIP.Shared.Crm;
using LondonVIP.Shared.Maps;
using LondonVIP.Infrastructure.Drivers;
using LondonVIP.Shared.Drivers;
using LondonVIP.Infrastructure.Customers;
using LondonVIP.Shared.CustomerPortal;
using LondonVIP.Infrastructure.Growth;
using LondonVIP.Shared.Growth;
using LondonVIP.Infrastructure.Accounting;
using LondonVIP.Shared.Accounting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;

namespace LondonVIP.Api;

public static class Program
{
    public static void Main(string[] args)
    {
        CreateApp(args).Run();
    }

    public static WebApplication CreateApp(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = ResolveContentRoot("LondonVIP.Api"),
            EnvironmentName = ResolveEnvironment(args)
        });

        builder.Services.AddOpenApi();
        builder.Services.AddSignalR();
        builder.Services.AddProblemDetails();
        builder.Services.AddDataProtection().SetApplicationName("LondonVIPCars")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "LondonVIPCars-DataProtectionKeys")));
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
        builder.Services.AddDbContext<LondonVIPDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));
        var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = security.MaxFailedAccessAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(security.LockoutMinutes);
            options.Lockout.AllowedForNewUsers = true;
            options.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<LondonVIPDbContext>().AddDefaultTokenProviders();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".LondonVIP.Erp.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(security.CookieExpirationMinutes);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(SecurityPolicies.ErpAccess, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher, SecurityRoles.Finance, SecurityRoles.Driver))
            .AddPolicy(SecurityPolicies.BookingOperations, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher))
            .AddPolicy(SecurityPolicies.DispatchOperations, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher))
            .AddPolicy(SecurityPolicies.FinanceOperations, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.CustomerRead, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.CustomerWrite, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher))
            .AddPolicy(SecurityPolicies.PricingRead, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.PricingWrite, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.DriverFleetRead, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.DriverFleetWrite, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.DriverOperations, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher))
            .AddPolicy(SecurityPolicies.CorporateAccountsRead, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Dispatcher, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.CorporateAccountsWrite, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.CorporateAccountsFinancialWrite, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin, SecurityRoles.Finance))
            .AddPolicy(SecurityPolicies.CompanyAdministration, policy => policy.RequireRole(SecurityRoles.SuperAdmin, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.PlatformAdministration, policy => policy.RequireRole(SecurityRoles.SuperAdmin));
        builder.Services.AddAuthorizationBuilder().AddPolicy(SecurityPolicies.CrmRead,policy=>policy.RequireRole(SecurityRoles.SuperAdmin,SecurityRoles.Admin,SecurityRoles.Dispatcher,SecurityRoles.Finance)).AddPolicy(SecurityPolicies.CrmWrite,policy=>policy.RequireRole(SecurityRoles.SuperAdmin,SecurityRoles.Admin,SecurityRoles.Dispatcher));
        builder.Services.AddAuthorizationBuilder().AddPolicy(SecurityPolicies.DriverPortal, policy => policy.RequireRole(SecurityRoles.Driver)).AddPolicy(SecurityPolicies.CustomerPortal, policy => policy.RequireRole(SecurityRoles.Customer));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("login", limiter => { limiter.PermitLimit = security.RateLimits.LoginPermitLimit; limiter.Window = TimeSpan.FromMinutes(security.RateLimits.WindowMinutes); limiter.QueueLimit = 0; });
            options.AddFixedWindowLimiter("public-quotes", limiter => { limiter.PermitLimit = security.RateLimits.PublicQuotePermitLimit; limiter.Window = TimeSpan.FromMinutes(security.RateLimits.WindowMinutes); limiter.QueueLimit = 0; });
            options.AddPolicy("operations", context => RateLimitPartition.GetFixedWindowLimiter(context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = security.RateLimits.OperationalPermitLimit, Window = TimeSpan.FromMinutes(security.RateLimits.WindowMinutes), QueueLimit = 0 }));
        });
        builder.Services.AddCors(options => options.AddPolicy("configured-origins", policy =>
        {
            if (security.AllowedOrigins.Length > 0) policy.WithOrigins(security.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IPricingService, PricingService>();
        builder.Services.AddScoped<ICompanyContext, AuthenticatedCompanyContext>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IBookingInvoiceService, BookingInvoiceService>();
        builder.Services.AddScoped<IInvoiceTotalsCalculator, InvoiceTotalsCalculator>();
        builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        builder.Services.AddScoped<BookingTransitionService>();
        builder.Services.AddScoped<IQuotationWorkflowService, QuotationWorkflowService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddScoped<INotificationProvider, DevelopmentEmailProvider>(); builder.Services.AddScoped<IEmailProvider, DevelopmentEmailProvider>();
            builder.Services.AddScoped<INotificationProvider, DevelopmentSmsProvider>(); builder.Services.AddScoped<ISmsProvider, DevelopmentSmsProvider>();
        }
        else
        {
            builder.Services.AddScoped<SendGridNotificationBridge>(); builder.Services.AddScoped<TwilioSmsNotificationBridge>();
            builder.Services.AddScoped<INotificationProvider>(sp => sp.GetRequiredService<SendGridNotificationBridge>()); builder.Services.AddScoped<IEmailProvider>(sp => sp.GetRequiredService<SendGridNotificationBridge>());
            builder.Services.AddScoped<INotificationProvider>(sp => sp.GetRequiredService<TwilioSmsNotificationBridge>()); builder.Services.AddScoped<ISmsProvider>(sp => sp.GetRequiredService<TwilioSmsNotificationBridge>());
        }
        builder.Services.AddScoped<INotificationProvider, DevelopmentInternalProvider>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IDashboardService, DashboardService>();
        builder.Services.AddScoped<IDispatchService, DispatchService>();
        builder.Services.AddScoped<IDriverAvailabilityService, DriverAvailabilityService>();
        builder.Services.AddScoped<IConflictDetectionService, ConflictDetectionService>();
        builder.Services.AddScoped<IAssignmentEngine, AssignmentEngine>();
        builder.Services.AddScoped<IDispatchTimelineService, DispatchTimelineService>();
        builder.Services.AddScoped<IDriverRecommendationService, DriverRecommendationService>();
        builder.Services.AddScoped<IDispatchDashboardService, DispatchDashboardService>();
        builder.Services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        builder.Services.AddScoped<IWorkflowScheduler, WorkflowScheduler>();
        builder.Services.AddScoped<IBusinessEventPublisher, BusinessEventPublisher>();
        builder.Services.AddScoped<IBusinessEventHandler, BusinessEventHandler>();
        builder.Services.AddScoped<IRuleEngine, RuleEngine>();
        builder.Services.AddScoped<IReminderService, ReminderService>();
        builder.Services.AddScoped<IEscalationService, EscalationService>();
        builder.Services.AddScoped<IMapProvider, GoogleMapsProvider>();
        builder.Services.AddScoped<IGeocodingService, GeocodingService>();
        builder.Services.AddScoped<IRouteService, RouteService>();
        builder.Services.AddScoped<ILiveTrackingService, LiveTrackingService>();
        builder.Services.AddScoped<IGPSLocationService, GPSLocationService>();
        builder.Services.AddScoped<IJourneyMonitoringService, JourneyMonitoringService>();
        builder.Services.AddScoped<IGeofenceService, GeofenceService>();
        builder.Services.AddScoped<IAirportMonitoringService, AirportMonitoringService>();
        builder.Services.AddScoped<IDriverIdentityResolver, DriverIdentityResolver>();
        builder.Services.AddScoped<IDriverPortalService, DriverPortalService>();
        builder.Services.AddScoped<IDriverJobService, DriverJobService>();
        builder.Services.AddScoped<IDriverShiftService, DriverShiftService>();
        builder.Services.AddScoped<IDriverEarningsService, DriverEarningsService>();
        builder.Services.AddScoped<IDriverDocumentService, DriverDocumentService>();
        builder.Services.AddScoped<ICustomerIdentityResolver, CustomerIdentityResolver>();
        builder.Services.AddHttpClient("LondonVIP.Integrations", client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<StripePaymentProvider>();
        builder.Services.AddScoped<GoogleMapsPlatformProvider>();
        builder.Services.AddScoped<TwilioSmsProvider>();
        builder.Services.AddScoped<TwilioWhatsAppProvider>();
        builder.Services.AddScoped<SendGridEmailProvider>();
        builder.Services.AddScoped<TwilioVoiceProvider>();
        builder.Services.AddScoped<AviationStackFlightProvider>();
        builder.Services.AddScoped<AzureBlobStorageProvider>();
        builder.Services.AddScoped<ProductionPdfProvider>();
        builder.Services.AddSingleton<UnconfiguredPushProvider>();
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<GoogleMapsPlatformProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<TwilioSmsProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<TwilioWhatsAppProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<SendGridEmailProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<TwilioVoiceProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<AviationStackFlightProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<AzureBlobStorageProvider>());
        builder.Services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<ProductionPdfProvider>());
        builder.Services.AddSingleton<IIntegrationProvider>(sp => sp.GetRequiredService<UnconfiguredPushProvider>());
        builder.Services.AddScoped<IIntegrationPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        builder.Services.AddScoped<IPaymentLifecycleProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        builder.Services.AddScoped<IPaymentGateway, StripeCustomerPaymentGateway>();
        builder.Services.AddScoped<ICrmService, CrmService>();
        builder.Services.AddScoped<IPromotionEngine, PromotionEngine>();
        builder.Services.AddScoped<IReferralService, ReferralService>();
        builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
        builder.Services.AddScoped<IJournalService, JournalService>();
        builder.Services.AddScoped<IAccountingReportService, AccountingReportService>();
        builder.Services.AddScoped<IDriverSettlementService, DriverSettlementService>();
        builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();
        builder.Services.AddScoped<IRecurringInvoiceService, RecurringInvoiceService>();
        builder.Services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
        builder.Services.AddScoped<IBankImportService, BankImportService>();
        builder.Services.AddScoped<IFiscalPeriodService, FiscalPeriodService>();
        builder.Services.AddScoped<IFinancialStatementService, FinancialStatementService>();
        builder.Services.AddSingleton<IPayrollExportProvider, CsvPayrollExportProvider>();
        builder.Services.AddScoped<IAutomaticJournalService, AutomaticJournalService>();
        builder.Services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        builder.Services.AddScoped<IVatSubmissionService, VatSubmissionService>();
        builder.Services.AddScoped<IFinanceAutomationService, FinanceAutomationService>();
        builder.Services.AddScoped<IFinanceAdministrationService, FinanceAdministrationService>();
        builder.Services.AddSingleton<ISocialProviderRegistry, SocialProviderRegistry>();
        builder.Services.AddScoped<ISocialOperationsService, SocialOperationsService>();
        builder.Services.AddScoped<INewsletterOptInService, NewsletterOptInService>();
        builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
        builder.Services.AddScoped<IAiMarketingService, AiMarketingService>();
        builder.Services.AddScoped<ICampaignEngine, CampaignEngine>();
        builder.Services.AddScoped<IGeocodingProvider>(sp => sp.GetRequiredService<GoogleMapsPlatformProvider>());
        builder.Services.AddScoped<IRoutingProvider>(sp => sp.GetRequiredService<GoogleMapsPlatformProvider>());
        builder.Services.AddScoped<IDistanceMatrixProvider>(sp => sp.GetRequiredService<GoogleMapsPlatformProvider>());
        builder.Services.AddScoped<IPlacesProvider>(sp => sp.GetRequiredService<GoogleMapsPlatformProvider>());
        builder.Services.AddScoped<IWhatsAppProvider>(sp => sp.GetRequiredService<TwilioWhatsAppProvider>());
        builder.Services.AddSingleton<IPushNotificationProvider>(sp => sp.GetRequiredService<UnconfiguredPushProvider>());
        builder.Services.AddScoped<IVoiceCallingProvider>(sp => sp.GetRequiredService<TwilioVoiceProvider>());
        builder.Services.AddScoped<IVoiceCallProvider>(sp => sp.GetRequiredService<TwilioVoiceProvider>());
        builder.Services.AddScoped<IFlightDataProvider>(sp => sp.GetRequiredService<AviationStackFlightProvider>());
        builder.Services.AddScoped<IFlightMonitoringProvider>(sp => sp.GetRequiredService<AviationStackFlightProvider>());
        builder.Services.AddScoped<IFileStorageProvider>(sp => sp.GetRequiredService<AzureBlobStorageProvider>());
        builder.Services.AddScoped<IPdfGenerationProvider>(sp => sp.GetRequiredService<ProductionPdfProvider>());
        builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
        builder.Services.AddScoped<IProviderRegistry, ProviderRegistry>();
        builder.Services.AddSingleton<IIntegrationExecutionPolicy, IntegrationExecutionPolicy>();
        builder.Services.AddSingleton<IWebhookSignatureValidator, HmacWebhookSignatureValidator>();
        builder.Services.AddScoped<PersistentWebhookEngine>();
        builder.Services.AddScoped<IWebhookEngine>(sp => sp.GetRequiredService<PersistentWebhookEngine>());
        builder.Services.AddScoped<IWebhookAdministrationService>(sp => sp.GetRequiredService<PersistentWebhookEngine>());
        builder.Services.AddScoped<IntegrationDiagnosticsService>();
        builder.Services.AddHostedService<IdentityBootstrapper>();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseCors("configured-origins");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<AuthorizationAuditMiddleware>();
        app.UseAuthorization();

        app.MapAuthenticationEndpoints();

        app.MapGet("/api/status", () => new
        {
            service = "London VIP Cars",
            status = "online"
        });

        app.MapPost("/api/quotes", async (
            QuoteRequest request,
            IPricingService pricingService,
            CancellationToken cancellationToken) =>
        {
            var errors = ValidateQuote(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var quote = await pricingService.CalculateQuoteAsync(request, cancellationToken);
            return Results.Ok(quote);
        }).AllowAnonymous().RequireRateLimiting("public-quotes");

        app.MapCompanySetupEndpoints();
        app.MapBookingEndpoints();
        app.MapDispatchEndpoints();
        app.MapCustomerEndpoints();
        app.MapCustomerPortalEndpoints();
        app.MapPricingAdministrationEndpoints();
        app.MapDriverEndpoints();
        app.MapVehicleEndpoints();
        app.MapCorporateAccountEndpoints();
        app.MapInvoiceEndpoints();
        app.MapPaymentEndpoints();
        app.MapQuotationEndpoints();
        app.MapNotificationEndpoints();
        app.MapDashboardEndpoints();
        app.MapWorkflowEndpoints();
        app.MapMapEndpoints();
        app.MapDriverPortalEndpoints();
        app.MapCustomerPlatformEndpoints();
        app.MapIntegrationEndpoints();
        app.MapCrmEndpoints();
        app.MapGrowthEndpoints();
        app.MapAccountingEndpoints();
        app.MapHub<DispatchHub>("/hubs/dispatch").RequireAuthorization(SecurityPolicies.DispatchOperations);
        app.MapHub<JourneyHub>("/hubs/journeys").RequireAuthorization(SecurityPolicies.ErpAccess);

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", () =>
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast").RequireAuthorization();

        return app;
    }

    private static Dictionary<string, string[]> ValidateQuote(QuoteRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.PickupAddress) || request.PickupAddress.Length > 500) errors["pickupAddress"] = ["Pickup address is required and cannot exceed 500 characters."];
        if (string.IsNullOrWhiteSpace(request.Destination) || request.Destination.Length > 500) errors["destination"] = ["Destination is required and cannot exceed 500 characters."];
        if (!Enum.IsDefined(request.VehicleType)) errors["vehicleType"] = ["Vehicle type is invalid."];
        if (request.PassengerCount is < 1 or > 8) errors["passengerCount"] = ["Passenger count must be between 1 and 8."];
        if (request.LuggageCount is < 0 or > 20) errors["luggageCount"] = ["Luggage count must be between 0 and 20."];
        if (request.WaitingMinutes is < 0 or > 1440) errors["waitingMinutes"] = ["Waiting minutes must be between 0 and 1440."];
        if (request.IsAirportPickup && request.AirportId is null) errors["airportId"] = ["Airport is required for airport pickup quotes."];
        return errors;
    }

    private static string ResolveContentRoot(string projectName)
    {
        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory is not null; directory = directory.Parent)
            {
                var workspaceProject = Path.Combine(directory.FullName, "src", projectName);
                if (File.Exists(Path.Combine(workspaceProject, $"{projectName}.csproj"))) return workspaceProject;
                if (File.Exists(Path.Combine(directory.FullName, $"{projectName}.csproj"))) return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? ResolveEnvironment(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument is "--environment" or "-e");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
