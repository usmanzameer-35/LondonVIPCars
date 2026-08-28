namespace LondonVIP.Shared.Bookings;

public static class BookingValidator
{
    public static Dictionary<string, string[]> Validate(BookingCreateDto? booking, DateTimeOffset now)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages)) errors[field] = messages = [];
            messages.Add(message);
        }

        if (booking is null)
        {
            Add(string.Empty, "Booking data is required.");
            return ToResult(errors);
        }

        if (booking.CustomerId == Guid.Empty) Add("customerId", "Customer is required.");
        if (string.IsNullOrWhiteSpace(booking.PickupAddress)) Add("pickupAddress", "Pickup address is required.");
        else if (booking.PickupAddress.Length > 500) Add("pickupAddress", "Pickup address cannot exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(booking.Destination)) Add("destination", "Destination is required.");
        else if (booking.Destination.Length > 500) Add("destination", "Destination cannot exceed 500 characters.");
        if (booking.PickupDateTime <= now) Add("pickupDateTime", "Pickup date and time must be in the future.");
        if (booking.PickupDateTime > now.AddYears(2)) Add("pickupDateTime", "Pickup date and time cannot be more than two years ahead.");
        if (booking.PassengerCount is < 1 or > 8) Add("passengerCount", "Passenger count must be between 1 and 8.");
        if (booking.LuggageCount is < 0 or > 20) Add("luggageCount", "Luggage count must be between 0 and 20.");
        if (!Enum.IsDefined(booking.VehicleType)) Add("vehicleType", "Vehicle type is invalid.");
        if (!Enum.IsDefined(booking.Status)) Add("status", "Booking status is invalid.");
        if (string.IsNullOrWhiteSpace(booking.PaymentStatus)) Add("paymentStatus", "Payment status is required.");
        else if (booking.PaymentStatus.Length > 50) Add("paymentStatus", "Payment status cannot exceed 50 characters.");
        if (booking.BaseFare < 0) Add("baseFare", "Base fare cannot be negative.");
        if (booking.Extras < 0) Add("extras", "Extras cannot be negative.");
        if (booking.TotalFare < 0) Add("totalFare", "Total fare cannot be negative.");
        if (booking.BaseFare > 100000 || booking.Extras > 100000 || booking.TotalFare > 200000) Add("totalFare", "Fare values exceed the supported operational limit.");
        if (booking.TotalFare != booking.BaseFare + booking.Extras) Add("totalFare", "Total fare must equal base fare plus extras.");
        if ((booking.IsAirportPickup || !string.IsNullOrWhiteSpace(booking.FlightNumber)) && booking.AirportId is null)
            Add("airportId", "Airport is required for an airport pickup or flight number.");
        if (booking.FlightNumber?.Length > 20) Add("flightNumber", "Flight number cannot exceed 20 characters.");
        if (booking.CustomerNotes?.Length > 2000) Add("customerNotes", "Customer notes cannot exceed 2000 characters.");
        if (booking.InternalNotes?.Length > 4000) Add("internalNotes", "Internal notes cannot exceed 4000 characters.");

        return ToResult(errors);
    }

    private static Dictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
}
