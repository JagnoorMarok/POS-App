using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests;

public class AppInfoServiceTests
{
    private class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Now => new(2026, 9, 16, 17, 30, 0, DateTimeKind.Local);
    }

    [Fact]
    public void GetAppInfo_ShouldReturnConfiguredMetadata()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Application:Name", "Test Restaurant App" },
            { "Environment", "Testing" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var dateTimeProvider = new FakeDateTimeProvider();
        var service = new AppInfoService(configuration, dateTimeProvider);

        // Act
        var result = service.GetAppInfo();

        // Assert
        result.Should().NotBeNull();
        result.ApplicationName.Should().Be("Test Restaurant App");
        result.Environment.Should().Be("Testing");
        result.ServerTimeUtc.Should().Be(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
        result.Architecture.Should().NotBeNullOrWhiteSpace();
    }
}
