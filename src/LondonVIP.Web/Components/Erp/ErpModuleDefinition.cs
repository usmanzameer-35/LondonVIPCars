namespace LondonVIP.Web.Components.Erp;

public sealed record ErpModuleDefinition(
    string Category,
    string Title,
    string Route,
    string Description,
    string[] FutureFeatures,
    string Status = "Planned");
