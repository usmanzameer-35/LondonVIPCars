using LondonVIP.Shared.Models;

namespace LondonVIP.Shared.Vehicles;

public static class VehicleValidator
{
    public static string NormalizeRegistration(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static Dictionary<string,string[]> Validate(VehicleCreateDto value)
    {
        var errors = new Dictionary<string,string[]>();
        if (string.IsNullOrWhiteSpace(value.RegistrationNumber) || NormalizeRegistration(value.RegistrationNumber).Length is < 2 or > 15) errors["registrationNumber"]=["Registration is required and must contain 2 to 15 letters or numbers."];
        if (string.IsNullOrWhiteSpace(value.Make) || value.Make.Trim().Length > 100) errors["make"]=["Make is required and must not exceed 100 characters."];
        if (string.IsNullOrWhiteSpace(value.Model) || value.Model.Trim().Length > 100) errors["model"]=["Model is required and must not exceed 100 characters."];
        if (!Enum.IsDefined(value.VehicleType)) errors["vehicleType"]=["Vehicle type is invalid."];
        if (value.PassengerCapacity is < 1 or > 50) errors["passengerCapacity"]=["Passenger capacity must be between 1 and 50."];
        if (value.LuggageCapacity is < 0 or > 100) errors["luggageCapacity"]=["Luggage capacity must be between 0 and 100."];
        if (value.Year is < 1900 || value.Year > DateTime.UtcNow.Year + 1) errors["year"]=["Vehicle year is outside the sensible range."];
        if (value.Colour?.Length > 50) errors["colour"]=["Colour must not exceed 50 characters."];
        if (value.Notes?.Length > 4000) errors["notes"]=["Notes must not exceed 4000 characters."];
        return errors;
    }
}
