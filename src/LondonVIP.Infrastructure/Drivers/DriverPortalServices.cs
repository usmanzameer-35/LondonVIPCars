using System.Security.Claims;
using System.Text.Json;
using LondonVIP.Infrastructure.Bookings;
using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using LondonVIP.Shared.Workflows;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Infrastructure.Drivers;

public interface IDriverIdentityResolver { Task<Guid?> GetDriverIdAsync(CancellationToken token = default); }

public sealed class DriverIdentityResolver(IHttpContextAccessor http, LondonVIPDbContext db, ICompanyContext company) : IDriverIdentityResolver
{
    public async Task<Guid?> GetDriverIdAsync(CancellationToken token = default)
    {
        var raw = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) return null;
        return await db.Users.AsNoTracking().Where(x => x.Id == userId && x.CompanyId == company.CompanyId && x.IsActive).Select(x => x.DriverId).SingleOrDefaultAsync(token);
    }
}

public sealed class DriverPortalService(LondonVIPDbContext db, ICompanyContext company, IDriverIdentityResolver identity, IDriverShiftService shifts, IDriverEarningsService earnings, TimeProvider clock) : IDriverPortalService
{
    public async Task<DriverPortalProfileDto?> GetProfileAsync(CancellationToken token = default)
    {
        var id = await identity.GetDriverIdAsync(token); if (id is null) return null;
        var driver = await db.Drivers.AsNoTracking().Include(x => x.Vehicle).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) return null;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var compliance = !driver.IsActive ? "Inactive" : driver.DrivingLicenceExpiry < today || driver.PrivateHireLicenceExpiry < today ? "Blocked" : "Valid";
        DriverPortalVehicleDto? vehicle = driver.Vehicle is null ? null : new(driver.Vehicle.Id, driver.Vehicle.RegistrationNumber, driver.Vehicle.Make, driver.Vehicle.Model, driver.Vehicle.VehicleType, driver.Vehicle.Colour, driver.Vehicle.MOTExpiry, driver.Vehicle.InsuranceExpiry, driver.Vehicle.PrivateHireLicenceExpiry, driver.Vehicle.IsActive && !(driver.Vehicle.MOTExpiry < today || driver.Vehicle.InsuranceExpiry < today || driver.Vehicle.PrivateHireLicenceExpiry < today) ? "Valid" : "Blocked");
        return new(driver.Id, driver.FirstName + " " + driver.LastName, driver.Email, driver.Phone, driver.DriverNumber, driver.AvailabilityStatus, compliance, vehicle);
    }

    public async Task<DriverPortalDashboardDto?> GetDashboardAsync(CancellationToken token = default)
    {
        var profile = await GetProfileAsync(token); if (profile is null) return null;
        var now = clock.GetUtcNow(); var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var end = start.AddDays(1);
        var jobs = (await DriverJobService.Query(db, company.CompanyId, profile.DriverId).ToListAsync(token)).Where(x => x.PickupDateTime < end).OrderBy(x => x.PickupDateTime).ToList();
        var active = jobs.Where(x => x.Status is BookingStatus.Assigned or BookingStatus.DriverEnRoute or BookingStatus.DriverArrived or BookingStatus.PassengerOnBoard).ToList();
        var money = await earnings.GetAsync(token);
        var latest = await db.DriverLocations.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId == profile.DriverId).MaxAsync(x => (DateTimeOffset?)x.RecordedAt, token);
        var warnings = Documents(profile).Count(x => x.Status != "Valid");
        var alerts = await db.DriverVehicleIssues.CountAsync(x => x.CompanyId == company.CompanyId && x.DriverId == profile.DriverId && x.Status == "Open", token);
        return new(profile, await shifts.GetCurrentAsync(token), active.FirstOrDefault(x => x.Status is BookingStatus.DriverEnRoute or BookingStatus.DriverArrived or BookingStatus.PassengerOnBoard), active.FirstOrDefault(x => x.PickupDateTime >= now), jobs.Count(x => x.PickupDateTime >= start), jobs.Count(x => x.Status == BookingStatus.Completed && x.PickupDateTime >= start), money?.GrossToday, alerts, warnings, latest);
    }

    internal static IReadOnlyList<DriverDocumentDto> Documents(DriverPortalProfileDto profile)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow); var driver = profile;
        return driver.Vehicle is null ? [] : [Document("Vehicle MOT", null, driver.Vehicle.MotExpiry, today), Document("Vehicle Insurance", null, driver.Vehicle.InsuranceExpiry, today), Document("Vehicle PHV Licence", null, driver.Vehicle.LicenceExpiry, today)];
    }
    internal static DriverDocumentDto Document(string type, string? reference, DateOnly? expiry, DateOnly today) => new(type, reference, expiry, expiry is null ? "Missing" : expiry < today ? "Expired" : expiry <= today.AddDays(30) ? "Expiring" : "Valid", expiry is null ? null : expiry.Value.DayNumber - today.DayNumber);
}

public sealed class DriverJobService(LondonVIPDbContext db, ICompanyContext company, IDriverIdentityResolver identity, BookingTransitionService transitions, IAuditService audit, IBusinessEventPublisher events, TimeProvider clock) : IDriverJobService
{
    private static readonly string[] DeclineReasons = ["Unavailable", "Too far", "Vehicle issue", "Personal emergency", "Shift ending", "Other"];
    public static IQueryable<DriverPortalJobDto> Query(LondonVIPDbContext db, Guid companyId, Guid driverId, Guid? bookingId = null) { var query = db.Bookings.AsNoTracking().Where(x => x.CompanyId == companyId && x.DriverId == driverId); if (bookingId is not null) query = query.Where(x => x.Id == bookingId); return query.OrderBy(x => x.PickupDateTime).Take(500).Select(x => new DriverPortalJobDto(x.Id, x.BookingReference, x.PickupDateTime, x.PickupAddress, x.Destination, x.Customer.FirstName + " " + x.Customer.LastName, x.Customer.Phone, x.VehicleType, x.PassengerCount, x.LuggageCount, x.FlightNumber, x.IsMeetAndGreet, x.CorporateAccount == null ? null : x.CorporateAccount.AccountName, x.PaymentStatus, x.CustomerNotes, x.Status)); }
    public async Task<IReadOnlyList<DriverPortalJobDto>> GetJobsAsync(CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); return id is null ? [] : await Query(db, company.CompanyId, id.Value).ToListAsync(token); }
    public async Task<DriverPortalJobDto?> GetJobAsync(Guid bookingId, CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); return id is null ? null : await Query(db, company.CompanyId, id.Value, bookingId).SingleOrDefaultAsync(token); }
    public async Task<DriverCommandResult> AcceptAsync(Guid bookingId, CancellationToken token = default)
    {
        var booking = await Owned(bookingId, token); if (booking is null) return Missing(); if (booking.Status != BookingStatus.Assigned) return Invalid(booking.Status);
        await Record("DriverAccepted", booking, token); return Ok(booking.Status, "Job accepted.");
    }
    public async Task<DriverCommandResult> DeclineAsync(Guid bookingId, DriverDeclineRequest request, CancellationToken token = default)
    {
        var booking = await Owned(bookingId, token); if (booking is null) return Missing(); if (booking.Status != BookingStatus.Assigned) return Invalid(booking.Status);
        if (!DeclineReasons.Contains(request.Reason, StringComparer.OrdinalIgnoreCase) || (request.Reason.Equals("Other", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(request.Note))) return new(false, "ValidationFailure", "A valid decline reason and note for Other are required.", booking.Status);
        var driverId = booking.DriverId!.Value; db.DriverJobDeclines.Add(new() { Id = Guid.NewGuid(), CompanyId = company.CompanyId, DriverId = driverId, BookingId = booking.Id, Reason = request.Reason, Note = request.Note?.Trim(), CreatedAt = clock.GetUtcNow() }); booking.DriverId = null; booking.Status = BookingStatus.Confirmed; booking.UpdatedAt = clock.GetUtcNow(); await db.SaveChangesAsync(token); await Record("DriverDeclined", booking, token, driverId); return Ok(booking.Status, "Job declined and returned to dispatch.");
    }
    public async Task<DriverCommandResult> TransitionAsync(Guid bookingId, BookingStatus next, DriverExceptionRequest? details = null, CancellationToken token = default)
    {
        var booking = await Owned(bookingId, token); if (booking is null) return Missing(); if (booking.Status == next) return Ok(next, "Action already completed.");
        if (next is BookingStatus.NoShow or BookingStatus.UnableToComplete && (details is null || !details.Confirmed || string.IsNullOrWhiteSpace(details.Reason))) return new(false, "ConfirmationRequired", "Confirmation and a reason are required.", booking.Status);
        if (!transitions.CanTransition(booking.Status, next, true)) return Invalid(booking.Status);
        booking.Status = next; booking.UpdatedAt = clock.GetUtcNow(); if (details?.Note is not null) booking.InternalNotes = string.Join(Environment.NewLine, new[] { booking.InternalNotes, $"Driver: {details.Reason} — {details.Note}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var driver = await db.Drivers.SingleAsync(x => x.Id == booking.DriverId && x.CompanyId == company.CompanyId, token);
        driver.AvailabilityStatus = next is BookingStatus.Completed or BookingStatus.NoShow or BookingStatus.UnableToComplete ? DriverAvailabilityStatus.Available : DriverAvailabilityStatus.Busy;
        if (next == BookingStatus.Completed) { var shift = await db.DriverShifts.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.DriverId == driver.Id && x.EndedAt == null, token); if (shift is not null) shift.JobsCompleted++; }
        await db.SaveChangesAsync(token); await Record(next.ToString(), booking, token); return Ok(next, $"Booking moved to {next}.");
    }
    private async Task<Booking?> Owned(Guid bookingId, CancellationToken token) { var id = await identity.GetDriverIdAsync(token); return id is null ? null : await db.Bookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.CompanyId == company.CompanyId && x.DriverId == id, token); }
    private async Task Record(string action, Booking booking, CancellationToken token, Guid? driver = null) { await audit.WriteAsync(action, "DriverOperations", "Succeeded", SecurityEventSeverity.Information, $"Driver {driver ?? booking.DriverId} performed {action}.", "Booking", booking.Id.ToString(), company.CompanyId, token); await events.PublishAsync(new(action, "Booking", booking.Id, JsonSerializer.Serialize(new { booking.BookingReference, DriverId = driver ?? booking.DriverId })), token); }
    private static DriverCommandResult Missing() => new(false, "NotFound", "Job was not found."); private static DriverCommandResult Invalid(BookingStatus status) => new(false, "InvalidTransition", $"Action is not valid from {status}.", status); private static DriverCommandResult Ok(BookingStatus status, string message) => new(true, "Success", message, status);
}

public sealed class DriverShiftService(LondonVIPDbContext db, ICompanyContext company, IDriverIdentityResolver identity, IAuditService audit, IBusinessEventPublisher events, TimeProvider clock) : IDriverShiftService
{
    public async Task<DriverShiftDto?> GetCurrentAsync(CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); return id is null ? null : await db.DriverShifts.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId == id && x.EndedAt == null).Select(x => new DriverShiftDto(x.Id, x.StartedAt, x.EndedAt, x.BreakStartedAt, x.BreakMinutes, x.JobsCompleted, true)).SingleOrDefaultAsync(token); }
    public async Task<DriverCommandResult> StartAsync(CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); if (id is null) return Missing(); if (await GetCurrentAsync(token) is not null) return Success("Shift already active."); db.DriverShifts.Add(new() { Id = Guid.NewGuid(), CompanyId = company.CompanyId, DriverId = id.Value, StartedAt = clock.GetUtcNow() }); await db.SaveChangesAsync(token); await Record("ShiftStarted", id.Value, token); return Success("Shift started."); }
    public async Task<DriverCommandResult> EndAsync(CancellationToken token = default) { var shift = await CurrentEntity(token); if (shift is null) return Missing("No active shift."); if (shift.BreakStartedAt is not null) CloseBreak(shift); shift.EndedAt = clock.GetUtcNow(); var driver = await db.Drivers.FindAsync([shift.DriverId], token); if (driver is not null) driver.AvailabilityStatus = DriverAvailabilityStatus.Offline; await db.SaveChangesAsync(token); await Record("ShiftEnded", shift.DriverId, token); return Success("Shift ended."); }
    public async Task<DriverCommandResult> StartBreakAsync(CancellationToken token = default) { var shift = await CurrentEntity(token); if (shift is null) return Missing("Start a shift first."); if (shift.BreakStartedAt is not null) return Success("Break already active."); shift.BreakStartedAt = clock.GetUtcNow(); var driver = await db.Drivers.FindAsync([shift.DriverId], token); if (driver is not null) driver.AvailabilityStatus = DriverAvailabilityStatus.OnBreak; await db.SaveChangesAsync(token); await Record("DriverBreakStarted", shift.DriverId, token); return Success("Break started."); }
    public async Task<DriverCommandResult> EndBreakAsync(CancellationToken token = default) { var shift = await CurrentEntity(token); if (shift?.BreakStartedAt is null) return Missing("No active break."); CloseBreak(shift); var driver = await db.Drivers.FindAsync([shift.DriverId], token); if (driver is not null) driver.AvailabilityStatus = DriverAvailabilityStatus.Available; await db.SaveChangesAsync(token); await Record("DriverBreakEnded", shift.DriverId, token); return Success("Break ended."); }
    public async Task<DriverAvailabilityResult> SetOnlineAsync(bool online, CancellationToken token = default)
    {
        var id = await identity.GetDriverIdAsync(token); if (id is null) return new(false, DriverAvailabilityStatus.Offline, ["Driver identity is not linked."]);
        var d = await db.Drivers.Include(x => x.Vehicle).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token); if (d is null) return new(false, DriverAvailabilityStatus.Offline, ["Driver was not found."]);
        if (!online) { d.AvailabilityStatus = DriverAvailabilityStatus.Offline; await db.SaveChangesAsync(token); await Record("DriverOffline", d.Id, token); return new(true, d.AvailabilityStatus, []); }
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); var reasons = new List<string>(); if (!d.IsActive) reasons.Add("Driver is inactive or suspended."); if (d.DrivingLicenceExpiry is null || d.DrivingLicenceExpiry < today) reasons.Add("Driving licence is missing or expired."); if (d.PrivateHireLicenceExpiry is null || d.PrivateHireLicenceExpiry < today) reasons.Add("Private-hire licence is missing or expired."); if (d.Vehicle is null || !d.Vehicle.IsActive) reasons.Add("An active vehicle must be assigned."); else { if (d.Vehicle.MOTExpiry is null || d.Vehicle.MOTExpiry < today) reasons.Add("Vehicle MOT is missing or expired."); if (d.Vehicle.InsuranceExpiry is null || d.Vehicle.InsuranceExpiry < today) reasons.Add("Vehicle insurance is missing or expired."); if (d.Vehicle.PrivateHireLicenceExpiry is null || d.Vehicle.PrivateHireLicenceExpiry < today) reasons.Add("Vehicle private-hire licence is missing or expired."); }
        d.AvailabilityStatus = reasons.Count == 0 ? DriverAvailabilityStatus.Available : DriverAvailabilityStatus.ComplianceBlocked; await db.SaveChangesAsync(token); if (reasons.Count > 0) return new(false, d.AvailabilityStatus, reasons); await Record("DriverOnline", d.Id, token); return new(true, d.AvailabilityStatus, []);
    }
    private async Task<DriverShift?> CurrentEntity(CancellationToken token) { var id = await identity.GetDriverIdAsync(token); return id is null ? null : await db.DriverShifts.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.DriverId == id && x.EndedAt == null, token); }
    private void CloseBreak(DriverShift shift) { shift.BreakMinutes += (int)Math.Max(0, (clock.GetUtcNow() - shift.BreakStartedAt!.Value).TotalMinutes); shift.BreakStartedAt = null; }
    private async Task Record(string action, Guid driverId, CancellationToken token) { await audit.WriteAsync(action, "DriverOperations", "Succeeded", SecurityEventSeverity.Information, action, "Driver", driverId.ToString(), company.CompanyId, token); await events.PublishAsync(new(action, "Driver", driverId, "{}"), token); }
    private static DriverCommandResult Success(string text) => new(true, "Success", text); private static DriverCommandResult Missing(string text = "Driver was not found.") => new(false, "NotFound", text);
}

public sealed class DriverEarningsService(LondonVIPDbContext db, ICompanyContext company, IDriverIdentityResolver identity, TimeProvider clock) : IDriverEarningsService
{
    public async Task<DriverEarningsDto?> GetAsync(CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); if (id is null) return null; var now = clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var week = day.AddDays(-((int)day.DayOfWeek + 6) % 7); var jobs = db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId == id && x.Status == BookingStatus.Completed && x.PickupDateTime >= week); var rows = await jobs.Select(x => new { x.PickupDateTime, x.TotalFare }).ToListAsync(token); var rate = await db.CompanySettings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId).Select(x => (decimal?)x.DriverCommissionPercentage).SingleOrDefaultAsync(token); var today = rows.Where(x => x.PickupDateTime >= day).Sum(x => x.TotalFare); var total = rows.Sum(x => x.TotalFare); return new(today, total, rate is null ? null : today * rate / 100m, rate is null ? null : total * rate / 100m, rate is not null, rows.Count(x => x.PickupDateTime >= day), rows.Count); }
}

public sealed class DriverDocumentService(LondonVIPDbContext db, ICompanyContext company, IDriverIdentityResolver identity) : IDriverDocumentService
{
    public async Task<IReadOnlyList<DriverDocumentDto>?> GetAsync(CancellationToken token = default) { var id = await identity.GetDriverIdAsync(token); if (id is null) return null; var d = await db.Drivers.AsNoTracking().Include(x => x.Vehicle).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token); if (d is null) return null; var today = DateOnly.FromDateTime(DateTime.UtcNow); var docs = new List<DriverDocumentDto> { DriverPortalService.Document("Driving Licence", d.DrivingLicenceNumber, d.DrivingLicenceExpiry, today), DriverPortalService.Document("Private Hire Driver Licence", d.PrivateHireLicenceNumber, d.PrivateHireLicenceExpiry, today), DriverPortalService.Document("DBS", null, d.DBSExpiry, today), DriverPortalService.Document("Medical", null, d.MedicalExpiry, today) }; if (d.Vehicle is not null) { docs.Add(DriverPortalService.Document("Vehicle MOT", null, d.Vehicle.MOTExpiry, today)); docs.Add(DriverPortalService.Document("Vehicle Insurance", null, d.Vehicle.InsuranceExpiry, today)); docs.Add(DriverPortalService.Document("Vehicle PHV Licence", null, d.Vehicle.PrivateHireLicenceExpiry, today)); } return docs; }
}
