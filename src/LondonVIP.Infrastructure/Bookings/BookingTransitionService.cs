using LondonVIP.Shared.Models;

namespace LondonVIP.Infrastructure.Bookings;

public sealed class BookingTransitionService
{
    public bool CanAssign(BookingStatus status) => status is BookingStatus.Confirmed or BookingStatus.Assigned;

    public bool CanTransition(BookingStatus current, BookingStatus next, bool hasDriver) =>
        (current, next) switch
        {
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            (BookingStatus.Assigned, BookingStatus.DriverEnRoute) => hasDriver,
            (BookingStatus.Assigned, BookingStatus.Cancelled) => true,
            (BookingStatus.DriverEnRoute, BookingStatus.DriverArrived) => hasDriver,
            // Retained for clients using the pre-foundation dispatch progression.
            (BookingStatus.DriverEnRoute, BookingStatus.PassengerOnBoard) => hasDriver,
            (BookingStatus.DriverEnRoute, BookingStatus.Cancelled) => true,
            (BookingStatus.DriverArrived, BookingStatus.PassengerOnBoard) => hasDriver,
            (BookingStatus.DriverArrived, BookingStatus.NoShow) => hasDriver,
            (BookingStatus.DriverArrived, BookingStatus.UnableToComplete) => hasDriver,
            (BookingStatus.PassengerOnBoard, BookingStatus.Completed) => hasDriver,
            (BookingStatus.PassengerOnBoard, BookingStatus.UnableToComplete) => hasDriver,
            _ => false
        };
}
