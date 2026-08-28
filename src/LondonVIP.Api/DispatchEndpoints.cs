using LondonVIP.Infrastructure.Data;
using LondonVIP.Shared.Dispatch;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class DispatchEndpoints
{
    private static readonly BookingStatus[] OperationalStatuses =
        [BookingStatus.Confirmed, BookingStatus.Assigned, BookingStatus.DriverEnRoute, BookingStatus.PassengerOnBoard];

    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dispatch", GetBoardAsync);
        endpoints.MapGet("/api/dispatch/unassigned", GetUnassignedAsync);
        endpoints.MapGet("/api/dispatch/drivers", GetDriversAsync);
        endpoints.MapPatch("/api/dispatch/{bookingId:guid}/assign", AssignDriverAsync);
        endpoints.MapPatch("/api/dispatch/{bookingId:guid}/unassign", UnassignDriverAsync);
        endpoints.MapPatch("/api/dispatch/{bookingId:guid}/status", UpdateStatusAsync);
        return endpoints;
    }

    private static async Task<IResult> GetBoardAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken) =>
        Results.Ok(await LoadBoardAsync(db, company.CompanyId, false, cancellationToken));

    private static async Task<IResult> GetUnassignedAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken) =>
        Results.Ok(await LoadBoardAsync(db, company.CompanyId, true, cancellationToken));

    private static async Task<IResult> GetDriversAsync(LondonVIPDbContext db, ICompanyContext company, CancellationToken cancellationToken)
    {
        var drivers = await db.Drivers.AsNoTracking()
            .Where(driver => driver.CompanyId == company.CompanyId && driver.IsActive)
            .OrderBy(driver => driver.LastName).ThenBy(driver => driver.FirstName)
            .Select(driver => new DriverAvailabilityDto
            {
                DriverId = driver.Id,
                DriverName = driver.FirstName + " " + driver.LastName,
                Phone = driver.Phone,
                VehicleId = driver.VehicleId,
                VehicleDisplay = driver.Vehicle == null ? null : driver.Vehicle.Make + " " + driver.Vehicle.Model,
                RegistrationNumber = driver.Vehicle == null ? null : driver.Vehicle.RegistrationNumber,
                VehicleType = driver.Vehicle == null ? null : driver.Vehicle.VehicleType,
                IsActive = driver.IsActive
            }).ToListAsync(cancellationToken);

        return Results.Ok(drivers);
    }

    private static async Task<IResult> AssignDriverAsync(
        Guid bookingId,
        AssignDriverRequest request,
        LondonVIPDbContext db,
        ICompanyContext company,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Assigned))
            return Conflict("Only confirmed or assigned bookings can be assigned or reassigned.");
        if (request.DriverId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["driverId"] = ["Driver is required."] });

        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.DriverId && item.CompanyId == company.CompanyId, cancellationToken);
        if (driver is null) return Results.NotFound();
        if (!driver.IsActive) return Conflict("The selected driver is inactive.");

        if (driver.VehicleId is { } vehicleId)
        {
            var validVehicle = await db.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId && vehicle.CompanyId == company.CompanyId && vehicle.IsActive, cancellationToken);
            if (!validVehicle) return Conflict("The driver's vehicle is unavailable or does not belong to the current company.");
        }

        booking.DriverId = driver.Id;
        booking.Status = BookingStatus.Assigned;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> UnassignDriverAsync(
        Guid bookingId,
        UnassignDriverRequest request,
        LondonVIPDbContext db,
        ICompanyContext company,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();
        if (booking.Status != BookingStatus.Assigned || booking.DriverId is null)
            return Conflict("Only an assigned booking can be unassigned before the journey starts.");

        booking.DriverId = null;
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid bookingId,
        DispatchStatusUpdateDto request,
        LondonVIPDbContext db,
        ICompanyContext company,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == bookingId && item.CompanyId == company.CompanyId, cancellationToken);
        if (booking is null) return Results.NotFound();
        if (!IsAllowedTransition(booking.Status, request.Status, booking.DriverId is not null))
            return Conflict($"Status cannot move from {booking.Status} to {request.Status} in dispatch.");

        booking.Status = request.Status;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (OperationalStatuses.Contains(booking.Status))
            return Results.Ok(await LoadItemAsync(db, booking.Id, company.CompanyId, cancellationToken));

        return Results.Ok(new DispatchStatusUpdateDto { Status = booking.Status });
    }

    private static bool IsAllowedTransition(BookingStatus current, BookingStatus next, bool hasDriver) =>
        (current, next) switch
        {
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            (BookingStatus.Assigned, BookingStatus.DriverEnRoute) => hasDriver,
            (BookingStatus.Assigned, BookingStatus.Cancelled) => true,
            (BookingStatus.DriverEnRoute, BookingStatus.PassengerOnBoard) => hasDriver,
            (BookingStatus.DriverEnRoute, BookingStatus.Cancelled) => true,
            (BookingStatus.PassengerOnBoard, BookingStatus.Completed) => hasDriver,
            _ => false
        };

    private static async Task<List<DispatchBoardItemDto>> LoadBoardAsync(
        LondonVIPDbContext db,
        Guid companyId,
        bool unassignedOnly,
        CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking()
            .Where(booking => booking.CompanyId == companyId && OperationalStatuses.Contains(booking.Status));
        if (unassignedOnly)
            query = query.Where(booking => booking.Status == BookingStatus.Confirmed && booking.DriverId == null);

        var items = await Project(query).ToListAsync(cancellationToken);
        SetTimingStates(items, DateTimeOffset.UtcNow);
        return items.OrderBy(item => item.PickupDateTime).ToList();
    }

    private static async Task<DispatchBoardItemDto?> LoadItemAsync(LondonVIPDbContext db, Guid bookingId, Guid companyId, CancellationToken cancellationToken)
    {
        var item = await Project(db.Bookings.AsNoTracking().Where(booking => booking.Id == bookingId && booking.CompanyId == companyId))
            .SingleOrDefaultAsync(cancellationToken);
        if (item is not null) SetTimingStates([item], DateTimeOffset.UtcNow);
        return item;
    }

    private static IQueryable<DispatchBoardItemDto> Project(IQueryable<Booking> query) =>
        query.Select(booking => new DispatchBoardItemDto
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            PickupDateTime = booking.PickupDateTime,
            CustomerName = booking.Customer.FirstName + " " + booking.Customer.LastName,
            PickupAddress = booking.PickupAddress,
            Destination = booking.Destination,
            AirportCode = booking.Airport == null ? null : booking.Airport.Code,
            FlightNumber = booking.FlightNumber,
            VehicleType = booking.VehicleType,
            DriverId = booking.DriverId,
            DriverName = booking.Driver == null ? null : booking.Driver.FirstName + " " + booking.Driver.LastName,
            DriverVehicleRegistration = booking.Driver == null || booking.Driver.Vehicle == null ? null : booking.Driver.Vehicle.RegistrationNumber,
            TotalFare = booking.TotalFare,
            PaymentStatus = booking.PaymentStatus,
            Status = booking.Status
        });

    private static void SetTimingStates(IEnumerable<DispatchBoardItemDto> items, DateTimeOffset now)
    {
        foreach (var item in items)
        {
            item.TimingState = item.Status is BookingStatus.DriverEnRoute or BookingStatus.PassengerOnBoard
                ? DispatchTimingState.Active
                : item.PickupDateTime < now
                    ? DispatchTimingState.Overdue
                    : item.PickupDateTime <= now.AddHours(1)
                        ? DispatchTimingState.Approaching
                        : DispatchTimingState.Scheduled;
        }
    }

    private static IResult Conflict(string message) => Results.Conflict(new { message });
}
