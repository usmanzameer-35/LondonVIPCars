using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Dispatch;

public sealed class DispatchStatusUpdateDto
{
    public BookingStatus Status { get; set; }
}
