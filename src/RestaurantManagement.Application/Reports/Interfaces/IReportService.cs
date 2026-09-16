using RestaurantManagement.Application.Reports.DTOs;

namespace RestaurantManagement.Application.Reports.Interfaces;

/// <summary>
/// Service contract generating offline sales summaries, payment breakdowns, and item performance analytics.
/// </summary>
public interface IReportService
{
    Task<SalesReportDto> GenerateSalesReportAsync(GenerateReportRequest request, CancellationToken cancellationToken = default);
}
