namespace LondonVIP.Shared.CustomerPortal;

public sealed class CustomerPortalCustomerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class CustomerPortalDashboardDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<CustomerPortalBookingDto> Bookings { get; set; } = [];
    public List<CustomerPortalInvoiceDto> Invoices { get; set; } = [];
    public List<CustomerPortalPaymentDto> Payments { get; set; } = [];
}

public sealed class CustomerPortalBookingDto
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public DateTimeOffset PickupDateTime { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public decimal TotalFare { get; set; }
    public string? DriverName { get; set; }
    public string? FlightNumber { get; set; }
    public string? InvoiceNumber { get; set; }
}

public sealed class CustomerPortalBookingDetailDto
{
    public CustomerPortalBookingDto Booking { get; set; } = new();
    public int PassengerCount { get; set; }
    public int LuggageCount { get; set; }
    public bool IsAirportPickup { get; set; }
    public bool IsMeetAndGreet { get; set; }
    public string? CustomerNotes { get; set; }
}

public sealed class CustomerPortalInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTimeOffset InvoiceDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
}

public sealed class CustomerPortalPaymentDto
{
    public Guid Id { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public DateTimeOffset PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AllocatedAmount { get; set; }
}
