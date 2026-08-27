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
}
