using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Common.Models;

namespace RestaurantManagement.Infrastructure.Services;

public class AppInfoService : IAppInfoService
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AppInfoService(IConfiguration configuration, IDateTimeProvider dateTimeProvider)
    {
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
    }

    public AppInfoResult GetAppInfo()
    {
        var appName = _configuration["Application:Name"] ?? "Restaurant Management System";
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        var environment = _configuration["Environment"] ?? "Production";
        var architecture = RuntimeInformation.ProcessArchitecture.ToString();

        return new AppInfoResult(
            ApplicationName: appName,
            Version: version,
            Environment: environment,
            Architecture: architecture,
            ServerTimeUtc: _dateTimeProvider.UtcNow
        );
    }
}
