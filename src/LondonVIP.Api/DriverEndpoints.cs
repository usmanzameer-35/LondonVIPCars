using LondonVIP.Infrastructure.Data;
using LondonVIP.Infrastructure.Security;
using LondonVIP.Shared.Drivers;
using LondonVIP.Shared.Models;
using LondonVIP.Shared.Security;
using LondonVIP.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LondonVIP.Api;

public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/drivers").RequireRateLimiting("operations");
        group.MapGet("", GetDriversAsync).RequireAuthorization(SecurityPolicies.DriverFleetRead);
        group.MapGet("/{id:guid}", GetDriverAsync).RequireAuthorization(SecurityPolicies.DriverFleetRead);
        group.MapGet("/{id:guid}/dashboard", GetDashboardAsync).RequireAuthorization(SecurityPolicies.DriverFleetRead);
        group.MapPost("", CreateDriverAsync).RequireAuthorization(SecurityPolicies.DriverFleetWrite);
        group.MapPut("/{id:guid}", UpdateDriverAsync).RequireAuthorization(SecurityPolicies.DriverFleetWrite);
        group.MapPatch("/{id:guid}/status", SetStatusAsync).RequireAuthorization(SecurityPolicies.DriverFleetWrite);
        group.MapPatch("/{id:guid}/availability", SetAvailabilityAsync).RequireAuthorization(SecurityPolicies.DriverOperations);
        group.MapPatch("/{id:guid}/vehicle", SetVehicleAsync).RequireAuthorization(SecurityPolicies.DriverOperations);
        return endpoints;
    }

    private static async Task<IResult> GetDashboardAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var driver = await db.Drivers.AsNoTracking().Include(x => x.Vehicle).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        var now = DateTimeOffset.UtcNow; var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); var dayEnd = dayStart.AddDays(1);
        var jobs = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId == id)
            .Select(x => new DriverDashboardJobDto { BookingId=x.Id,BookingReference=x.BookingReference,PickupDateTime=x.PickupDateTime,CustomerName=x.Customer.FirstName+" "+x.Customer.LastName,PickupAddress=x.PickupAddress,Destination=x.Destination,AirportCode=x.Airport==null?null:x.Airport.Code,FlightNumber=x.FlightNumber,Status=x.Status }).ToListAsync(token);
        jobs = jobs.OrderBy(x => x.PickupDateTime).ToList();
        var active = jobs.Where(x => x.Status is BookingStatus.Assigned or BookingStatus.DriverEnRoute or BookingStatus.DriverArrived or BookingStatus.PassengerOnBoard).ToList();
        var rejectionEvents = await db.SecurityAuditEvents.AsNoTracking().Where(x=>x.CompanyId==company.CompanyId&&x.EventType=="DriverRejected").Select(x=>new{x.Timestamp,x.Description}).ToListAsync(token);
        var result = new DriverDashboardDto
        {
            DriverId=driver.Id,DriverName=$"{driver.FirstName} {driver.LastName}".Trim(),AvailabilityStatus=driver.AvailabilityStatus,
            VehicleDisplay=driver.Vehicle is null?null:$"{driver.Vehicle.Make} {driver.Vehicle.Model}",RegistrationNumber=driver.Vehicle?.RegistrationNumber,
            CurrentJob=active.FirstOrDefault(x => x.Status is BookingStatus.DriverEnRoute or BookingStatus.DriverArrived or BookingStatus.PassengerOnBoard) ?? active.FirstOrDefault(),
            UpcomingJobs=active.Where(x=>x.PickupDateTime>=now).ToList(),TodaysJobs=jobs.Where(x=>x.PickupDateTime>=dayStart&&x.PickupDateTime<dayEnd).ToList(),
            NextPickupTime=active.Where(x=>x.PickupDateTime>=now).Select(x=>(DateTimeOffset?)x.PickupDateTime).FirstOrDefault(),
            CompletedToday=jobs.Count(x=>x.Status==BookingStatus.Completed&&x.PickupDateTime>=dayStart&&x.PickupDateTime<dayEnd),
            CancelledToday=jobs.Count(x=>x.Status==BookingStatus.Cancelled&&x.PickupDateTime>=dayStart&&x.PickupDateTime<dayEnd),
            RejectedToday=rejectionEvents.Count(x=>x.Timestamp>=dayStart&&x.Timestamp<dayEnd&&x.Description.Contains(id.ToString(),StringComparison.OrdinalIgnoreCase))
        };
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDriversAsync(string? search, bool? active, DriverAvailabilityStatus? availability, bool? assigned,
        LondonVIPDbContext db, ICompanyContext company, CancellationToken token)
    {
        var query = db.Drivers.AsNoTracking().Include(x => x.Vehicle).Where(x => x.CompanyId == company.CompanyId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.FirstName.Contains(term) || x.LastName.Contains(term) || x.Email.Contains(term) || x.Phone.Contains(term) || (x.DriverNumber != null && x.DriverNumber.Contains(term))); }
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        if (availability.HasValue) query = query.Where(x => x.AvailabilityStatus == availability);
        if (assigned.HasValue) query = assigned.Value ? query.Where(x => x.VehicleId != null) : query.Where(x => x.VehicleId == null);
        var drivers = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(token);
        var bookingRows = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId != null && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.Completed).Select(x => new { DriverId = x.DriverId!.Value, x.PickupDateTime }).ToListAsync(token);
        var now = DateTimeOffset.UtcNow;
        var upcoming = bookingRows.Where(x => x.PickupDateTime >= now).GroupBy(x => x.DriverId).ToDictionary(x => x.Key, x => x.Count());
        return Results.Ok(drivers.Select(x => ToList(x, upcoming.GetValueOrDefault(x.Id))));
    }

    private static async Task<IResult> GetDriverAsync(Guid id, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var driver = await db.Drivers.AsNoTracking().Include(x => x.Vehicle).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        var pickupDates = await db.Bookings.AsNoTracking().Where(x => x.CompanyId == company.CompanyId && x.DriverId == id && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.Completed).Select(x => x.PickupDateTime).ToListAsync(token);
        var count = pickupDates.Count(x => x >= DateTimeOffset.UtcNow);
        return Results.Ok(ToDetail(driver, count));
    }

    private static async Task<IResult> CreateDriverAsync(DriverCreateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var validation = await ValidateAsync(request, null, db, company.CompanyId, token); if (validation.Count > 0) return Results.ValidationProblem(validation);
        var now = DateTimeOffset.UtcNow;
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company.CompanyId, CreatedAt = now, UpdatedAt = now };
        Apply(driver, request); db.Drivers.Add(driver); await db.SaveChangesAsync(token);
        await audit.WriteAsync("DriverCreated", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Driver record created.", "Driver", driver.Id.ToString(), company.CompanyId, token);
        return Results.Created($"/api/drivers/{driver.Id}", ToDetail(driver, 0));
    }

    private static async Task<IResult> UpdateDriverAsync(Guid id, DriverUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        var validation = await ValidateAsync(request, id, db, company.CompanyId, token); if (validation.Count > 0) return Results.ValidationProblem(validation);
        var previousVehicleId = driver.VehicleId;
        Apply(driver, request); driver.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
        await audit.WriteAsync("DriverUpdated", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Driver record updated.", "Driver", id.ToString(), company.CompanyId, token);
        if (previousVehicleId != driver.VehicleId)
            await audit.WriteAsync(driver.VehicleId.HasValue ? "DriverVehicleAssigned" : "DriverVehicleUnassigned", "Fleet", "Succeeded", SecurityEventSeverity.Information, driver.VehicleId.HasValue ? "Vehicle assigned while updating driver." : "Vehicle unassigned while updating driver.", "Driver", id.ToString(), company.CompanyId, token);
        return Results.Ok(ToDetail(driver, 0));
    }

    private static async Task<IResult> SetStatusAsync(Guid id, DriverStatusUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        driver.IsActive = request.IsActive; if (!request.IsActive) driver.AvailabilityStatus = DriverAvailabilityStatus.Offline; driver.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
        await audit.WriteAsync(request.IsActive ? "DriverActivated" : "DriverDeactivated", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Driver active state changed.", "Driver", id.ToString(), company.CompanyId, token);
        return Results.Ok(new { driver.Id, driver.IsActive, driver.AvailabilityStatus });
    }

    private static async Task<IResult> SetAvailabilityAsync(Guid id, DriverAvailabilityUpdateDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        if (!Enum.IsDefined(request.AvailabilityStatus)) return Results.ValidationProblem(new Dictionary<string,string[]> { ["availabilityStatus"]=["Availability status is invalid."] });
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        if (!driver.IsActive && request.AvailabilityStatus != DriverAvailabilityStatus.Offline) return Conflict("An inactive driver must remain offline.");
        driver.AvailabilityStatus = request.AvailabilityStatus; driver.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
        await audit.WriteAsync("DriverAvailabilityChanged", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Driver availability changed.", "Driver", id.ToString(), company.CompanyId, token);
        return Results.Ok(new { driver.Id, driver.AvailabilityStatus });
    }

    private static async Task<IResult> SetVehicleAsync(Guid id, DriverVehicleAssignmentDto request, LondonVIPDbContext db, ICompanyContext company, IAuditService audit, CancellationToken token)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company.CompanyId, token);
        if (driver is null) { await AuditCrossTenantAsync(db, audit, id, company.CompanyId, token); return Results.NotFound(); }
        if (request.VehicleId is null)
        {
            driver.VehicleId = null; driver.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
            await audit.WriteAsync("DriverVehicleUnassigned", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Vehicle unassigned from driver.", "Driver", id.ToString(), company.CompanyId, token);
            return Results.Ok(new { driver.Id, driver.VehicleId });
        }
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == request.VehicleId && x.CompanyId == company.CompanyId, token);
        if (vehicle is null) { await AuditCrossTenantVehicleAsync(db, audit, request.VehicleId.Value, company.CompanyId, token); return Results.NotFound(); }
        if (!driver.IsActive) return Conflict("An inactive driver cannot be assigned a vehicle.");
        if (!vehicle.IsActive) return Conflict("An inactive vehicle cannot be assigned.");
        var existing = await db.Drivers.SingleOrDefaultAsync(x => x.CompanyId == company.CompanyId && x.VehicleId == vehicle.Id && x.Id != id, token);
        if (existing is not null && !request.Reassign) return Conflict("Vehicle is already assigned. Explicit reassignment is required.");
        if (existing is not null) { existing.VehicleId = null; existing.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token); }
        driver.VehicleId = vehicle.Id; driver.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(token);
        await audit.WriteAsync("DriverVehicleAssigned", "Fleet", "Succeeded", SecurityEventSeverity.Information, "Vehicle assigned to driver.", "Driver", id.ToString(), company.CompanyId, token);
        return Results.Ok(new { driver.Id, driver.VehicleId });
    }

    private static async Task<Dictionary<string,string[]>> ValidateAsync(DriverCreateDto request, Guid? id, LondonVIPDbContext db, Guid companyId, CancellationToken token)
    {
        var errors = DriverValidator.Validate(request);
        if (!string.IsNullOrWhiteSpace(request.Email) && await db.Drivers.AnyAsync(x => x.CompanyId == companyId && x.Id != id && x.Email.ToLower() == request.Email.Trim().ToLower(), token)) errors["email"]=["A driver with this email already exists for the current company."];
        if (request.VehicleId is { } vehicleId)
        {
            var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == vehicleId && x.CompanyId == companyId, token);
            if (vehicle is null || !vehicle.IsActive) errors["vehicleId"]=["Vehicle must be active and belong to the current company."];
            else if (await db.Drivers.AnyAsync(x => x.CompanyId == companyId && x.VehicleId == vehicleId && x.Id != id, token)) errors["vehicleId"]=["Vehicle is already assigned to another driver."];
        }
        return errors;
    }
    private static void Apply(Driver d, DriverCreateDto r) { d.FirstName=r.FirstName.Trim(); d.LastName=r.LastName.Trim(); d.Phone=r.Phone.Trim(); d.Email=r.Email.Trim(); d.DriverNumber=Trim(r.DriverNumber); d.Notes=Trim(r.Notes); d.DrivingLicenceNumber=Trim(r.DrivingLicenceNumber); d.DrivingLicenceExpiry=r.DrivingLicenceExpiry; d.PrivateHireLicenceNumber=Trim(r.PrivateHireLicenceNumber); d.PrivateHireLicenceExpiry=r.PrivateHireLicenceExpiry; d.DBSExpiry=r.DBSExpiry; d.MedicalExpiry=r.MedicalExpiry; d.AvailabilityStatus=r.IsActive?r.AvailabilityStatus:DriverAvailabilityStatus.Offline; d.VehicleId=r.VehicleId; d.IsActive=r.IsActive; }
    private static DriverListItemDto ToList(Driver d,int count) => new() { Id=d.Id,FirstName=d.FirstName,LastName=d.LastName,Phone=d.Phone,Email=d.Email,DriverNumber=d.DriverNumber,IsActive=d.IsActive,AvailabilityStatus=d.AvailabilityStatus,VehicleId=d.VehicleId,VehicleDisplay=d.Vehicle is null?null:$"{d.Vehicle.Make} {d.Vehicle.Model}",RegistrationNumber=d.Vehicle?.RegistrationNumber,ComplianceState=ComplianceCalculator.Calculate([d.DrivingLicenceExpiry,d.PrivateHireLicenceExpiry,d.DBSExpiry,d.MedicalExpiry]),UpcomingBookingsCount=count };
    private static DriverDetailDto ToDetail(Driver d,int count) { var x=new DriverDetailDto { Id=d.Id,FirstName=d.FirstName,LastName=d.LastName,Phone=d.Phone,Email=d.Email,DriverNumber=d.DriverNumber,IsActive=d.IsActive,AvailabilityStatus=d.AvailabilityStatus,VehicleId=d.VehicleId,VehicleDisplay=d.Vehicle is null?null:$"{d.Vehicle.Make} {d.Vehicle.Model}",RegistrationNumber=d.Vehicle?.RegistrationNumber,ComplianceState=ComplianceCalculator.Calculate([d.DrivingLicenceExpiry,d.PrivateHireLicenceExpiry,d.DBSExpiry,d.MedicalExpiry]),UpcomingBookingsCount=count,Notes=d.Notes,DrivingLicenceNumber=d.DrivingLicenceNumber,DrivingLicenceExpiry=d.DrivingLicenceExpiry,PrivateHireLicenceNumber=d.PrivateHireLicenceNumber,PrivateHireLicenceExpiry=d.PrivateHireLicenceExpiry,DBSExpiry=d.DBSExpiry,MedicalExpiry=d.MedicalExpiry,CreatedAt=d.CreatedAt,UpdatedAt=d.UpdatedAt }; return x; }
    private static string? Trim(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static IResult Conflict(string message)=>Results.Conflict(new {message});
    private static async Task AuditCrossTenantAsync(LondonVIPDbContext db,IAuditService audit,Guid id,Guid companyId,CancellationToken token){if(await db.Drivers.AnyAsync(x=>x.Id==id&&x.CompanyId!=companyId,token))await audit.WriteAsync("CrossTenantAccessAttempt","Authorization","Denied",SecurityEventSeverity.High,"Cross-tenant driver access was blocked.","Driver",id.ToString(),companyId,token);}
    private static async Task AuditCrossTenantVehicleAsync(LondonVIPDbContext db,IAuditService audit,Guid id,Guid companyId,CancellationToken token){if(await db.Vehicles.AnyAsync(x=>x.Id==id&&x.CompanyId!=companyId,token))await audit.WriteAsync("CrossTenantAccessAttempt","Authorization","Denied",SecurityEventSeverity.High,"Cross-tenant vehicle access was blocked.","Vehicle",id.ToString(),companyId,token);}
}
