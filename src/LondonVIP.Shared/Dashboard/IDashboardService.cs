namespace LondonVIP.Shared.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken token = default);
    Task<DashboardRevenueDto> GetRevenueAsync(CancellationToken token = default);
    Task<DashboardBookingsDto> GetBookingsAsync(CancellationToken token = default);
    Task<DashboardOperationsDto> GetOperationsAsync(CancellationToken token = default);
    Task<DashboardDriversDto> GetDriversAsync(CancellationToken token = default);
}
