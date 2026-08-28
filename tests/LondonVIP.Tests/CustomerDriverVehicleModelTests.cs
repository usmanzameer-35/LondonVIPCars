using LondonVIP.Shared.Models;

namespace LondonVIP.Tests;

public class CustomerDriverVehicleModelTests
{
    [Fact]
    public void Customer_CanBeInstantiated()
    {
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var customer = new Customer
        {
            Id = customerId,
            CompanyId = companyId,
            FirstName = "Test",
            LastName = "Customer",
            Email = "customer@example.test",
            Phone = "0000000000",
            CreatedAt = createdAt,
            IsActive = true
        };

        Assert.Equal(customerId, customer.Id);
        Assert.Equal(companyId, customer.CompanyId);
        Assert.Equal("Test", customer.FirstName);
        Assert.Equal("Customer", customer.LastName);
        Assert.Equal("customer@example.test", customer.Email);
        Assert.Equal("0000000000", customer.Phone);
        Assert.Equal(createdAt, customer.CreatedAt);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void Driver_CanBeInstantiated()
    {
        var driverId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var driver = new Driver
        {
            Id = driverId,
            CompanyId = companyId,
            FirstName = "Test",
            LastName = "Driver",
            Phone = "0000000000",
            Email = "driver@example.test",
            VehicleId = null,
            IsActive = true
        };

        Assert.Equal(driverId, driver.Id);
        Assert.Equal(companyId, driver.CompanyId);
        Assert.Equal("Test", driver.FirstName);
        Assert.Equal("Driver", driver.LastName);
        Assert.Equal("0000000000", driver.Phone);
        Assert.Equal("driver@example.test", driver.Email);
        Assert.Null(driver.VehicleId);
        Assert.True(driver.IsActive);
    }

    [Fact]
    public void Vehicle_CanBeInstantiated()
    {
        var vehicleId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var vehicle = new Vehicle
        {
            Id = vehicleId,
            CompanyId = companyId,
            RegistrationNumber = "TEST-REG",
            Make = "Test Make",
            Model = "Test Model",
            VehicleType = VehicleType.MPV,
            PassengerCapacity = 6,
            LuggageCapacity = 4,
            IsActive = true
        };

        Assert.Equal(vehicleId, vehicle.Id);
        Assert.Equal(companyId, vehicle.CompanyId);
        Assert.Equal("TEST-REG", vehicle.RegistrationNumber);
        Assert.Equal("Test Make", vehicle.Make);
        Assert.Equal("Test Model", vehicle.Model);
        Assert.Equal(VehicleType.MPV, vehicle.VehicleType);
        Assert.Equal(6, vehicle.PassengerCapacity);
        Assert.Equal(4, vehicle.LuggageCapacity);
        Assert.True(vehicle.IsActive);
    }
}
