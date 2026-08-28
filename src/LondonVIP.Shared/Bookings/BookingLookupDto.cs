namespace LondonVIP.Shared.Bookings;

public sealed record BookingLookupItemDto(Guid Id, string Label);
public sealed record AirportLookupItemDto(Guid Id, string Code, string Name);

public sealed class BookingLookupsDto
{
    public IReadOnlyList<BookingLookupItemDto> Customers { get; set; } = [];
    public IReadOnlyList<BookingLookupItemDto> Drivers { get; set; } = [];
    public IReadOnlyList<AirportLookupItemDto> Airports { get; set; } = [];
}
