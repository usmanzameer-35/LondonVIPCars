using LondonVIP.Shared.Models;
using LondonVIP.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Data;

public class LondonVIPDbContext(DbContextOptions<LondonVIPDbContext> options) : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(booking => booking.BookingReference).HasMaxLength(40);
            entity.Property(booking => booking.FlightNumber).HasMaxLength(20);
            entity.Property(booking => booking.CustomerNotes).HasMaxLength(2000);
            entity.Property(booking => booking.InternalNotes).HasMaxLength(4000);
            entity.Property(booking => booking.BaseFare).HasPrecision(18, 2);
            entity.Property(booking => booking.Extras).HasPrecision(18, 2);
            entity.Property(booking => booking.TotalFare).HasPrecision(18, 2);
            entity.HasIndex(booking => new { booking.CompanyId, booking.BookingReference }).IsUnique();
            entity.HasIndex(booking => new { booking.CompanyId, booking.PickupDateTime });
            entity.HasIndex(booking => new { booking.CompanyId, booking.Status });
            entity.HasOne(booking => booking.Company).WithMany(company => company.Bookings)
                .HasForeignKey(booking => booking.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Customer).WithMany().HasForeignKey(booking => booking.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Driver).WithMany().HasForeignKey(booking => booking.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Airport).WithMany().HasForeignKey(booking => booking.AirportId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(customer => new { customer.CompanyId, customer.Email });
            entity.HasOne(customer => customer.Company).WithMany(company => company.Customers)
                .HasForeignKey(customer => customer.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasIndex(driver => new { driver.CompanyId, driver.Email });
            entity.HasOne(driver => driver.Company).WithMany(company => company.Drivers)
                .HasForeignKey(driver => driver.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(driver => driver.Vehicle).WithMany().HasForeignKey(driver => driver.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
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
