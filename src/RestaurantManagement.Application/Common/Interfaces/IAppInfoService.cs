using RestaurantManagement.Application.Common.Models;

namespace RestaurantManagement.Application.Common.Interfaces;

/// <summary>
/// Service abstraction to query application metadata and environment status.
/// </summary>
public interface IAppInfoService
{
    AppInfoResult GetAppInfo();
}
