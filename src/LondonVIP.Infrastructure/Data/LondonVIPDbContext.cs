using LondonVIP.Shared.Models;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Security;
using LondonVIP.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(user => new { user.CompanyId, user.NormalizedEmail });
            entity.HasOne<Company>().WithMany().HasForeignKey(user => user.CompanyId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(rule => new { rule.CompanyId, rule.AirportId, rule.VehicleType, rule.IsActive });
            entity.HasOne(rule => rule.Company).WithMany(company => company.PricingRules)
                .HasForeignKey(rule => rule.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Airport>().WithMany().HasForeignKey(rule => rule.AirportId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Airport>().HasIndex(airport => airport.Code).IsUnique();

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
    }
}
