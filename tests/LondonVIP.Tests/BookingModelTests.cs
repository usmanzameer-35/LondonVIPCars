using LondonVIP.Shared.Models;

namespace LondonVIP.Tests;

public class BookingModelTests
{
    [Fact]
    public void CoreEnums_ContainExpectedValues()
    {
        Assert.Equal(
            ["Customer", "Driver", "Dispatcher", "Admin", "SuperAdmin"],
            Enum.GetNames<UserRole>());

        Assert.Equal(
            ["Quote", "Pending", "Confirmed", "Assigned", "DriverEnRoute", "PassengerOnBoard", "Completed", "Cancelled"],
            Enum.GetNames<BookingStatus>());

        Assert.Equal(
            ["Saloon", "Estate", "MPV", "EightSeater"],
            Enum.GetNames<VehicleType>());
    }

    [Fact]
    public void Booking_CanBeInstantiated()
    {
        var bookingId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var pickupDateTime = DateTimeOffset.UtcNow.AddDays(1);
        var createdAt = DateTimeOffset.UtcNow;

        var booking = new Booking
        {
            Id = bookingId,
            CompanyId = companyId,
            CustomerId = customerId,
            PickupAddress = "Heathrow Airport",
            Destination = "Central London",
            PickupDateTime = pickupDateTime,
            PassengerCount = 2,
            LuggageCount = 3,
            VehicleType = VehicleType.Estate,
            BaseFare = 80m,
            Extras = 10m,
            TotalFare = 90m,
            DriverId = null,
            Status = BookingStatus.Pending,
            PaymentStatus = "Pending",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        Assert.Equal(bookingId, booking.Id);
        Assert.Equal(companyId, booking.CompanyId);
        Assert.Equal(customerId, booking.CustomerId);
        Assert.Equal("Heathrow Airport", booking.PickupAddress);
        Assert.Equal("Central London", booking.Destination);
        Assert.Equal(pickupDateTime, booking.PickupDateTime);
        Assert.Equal(2, booking.PassengerCount);
        Assert.Equal(3, booking.LuggageCount);
        Assert.Equal(VehicleType.Estate, booking.VehicleType);
        Assert.Equal(80m, booking.BaseFare);
        Assert.Equal(10m, booking.Extras);
        Assert.Equal(90m, booking.TotalFare);
        Assert.Null(booking.DriverId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal("Pending", booking.PaymentStatus);
        Assert.Equal(createdAt, booking.CreatedAt);
        Assert.Equal(createdAt, booking.UpdatedAt);
    }
}
