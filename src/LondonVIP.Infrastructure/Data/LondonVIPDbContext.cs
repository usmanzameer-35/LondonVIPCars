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
    }
}
