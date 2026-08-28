namespace LondonVIP.Infrastructure.Bookings;

public static class BookingReferenceGenerator
{
    public static string Generate(Guid bookingId, DateTimeOffset createdAt) =>
        $"LVC-{createdAt:yyyyMMdd}-{bookingId:N}"[..20].ToUpperInvariant();
}
