using LondonVIP.Shared.Models;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(booking => booking.BaseFare).HasPrecision(18, 2);
            entity.Property(booking => booking.Extras).HasPrecision(18, 2);
            entity.Property(booking => booking.TotalFare).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.Property(rule => rule.BasePrice).HasPrecision(18, 2);
            entity.Property(rule => rule.AirportPickupSupplement).HasPrecision(18, 2);
            entity.Property(rule => rule.WaitingChargePerHour).HasPrecision(18, 2);
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
