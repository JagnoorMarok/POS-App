using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Tables.DTOs;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class TableServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TableServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantTableTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "table_test.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        _dateTimeProvider = new DateTimeProvider();

        using var context = new RestaurantDbContext(_dbContextOptions);
        context.Database.Migrate();
    }

    public void Dispose()
    {
        try
        {
            var directory = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CreateTableAsync_ShouldCreateTable_WhenValid()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var request = new CreateTableRequest("T1", 4, 1, true);
        var result = await service.CreateTableAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.TableNumber.Should().Be("T1");
        result.Capacity.Should().Be(4);
        result.DisplayOrder.Should().Be(1);
        result.IsOccupied.Should().BeFalse();
        result.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task CreateTableAsync_ShouldRejectNonPositiveCapacity(int invalidCapacity)
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var request = new CreateTableRequest("T-INV", invalidCapacity, 1);
        var act = async () => await service.CreateTableAsync(request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task CreateTableAsync_ShouldRejectDuplicateActiveTableNumber()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        await service.CreateTableAsync(new CreateTableRequest("Table 5", 4, 1));

        var act = async () => await service.CreateTableAsync(new CreateTableRequest("table 5", 6, 2));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateTableAsync_ShouldUpdateCapacityAndDisplayOrder()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var table = await service.CreateTableAsync(new CreateTableRequest("T-10", 2, 1));

        var updateRequest = new UpdateTableRequest(table.Id, "T-10", 4, 2, IsActive: true);
        var updated = await service.UpdateTableAsync(updateRequest);

        updated.Capacity.Should().Be(4);
        updated.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task SetTableOccupancyAsync_ShouldChangeOccupancy()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var table = await service.CreateTableAsync(new CreateTableRequest("T-20", 4, 1));

        await service.SetTableOccupancyAsync(table.Id, true);

        var occupied = await service.GetTableByIdAsync(table.Id);
        occupied!.IsOccupied.Should().BeTrue();

        await service.SetTableOccupancyAsync(table.Id, false);
        var freed = await service.GetTableByIdAsync(table.Id);
        freed!.IsOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateTableAsync_ShouldMarkAsInactiveWithoutPhysicalDeletion()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var table = await service.CreateTableAsync(new CreateTableRequest("T-30", 6, 1));

        await service.DeactivateTableAsync(table.Id);

        var activeOnly = await service.GetActiveTablesAsync();
        activeOnly.Should().NotContain(t => t.Id == table.Id);

        var allTables = await service.GetAllTablesAsync(includeInactive: true);
        allTables.Should().Contain(t => t.Id == table.Id && !t.IsActive);
    }

    [Fact]
    public async Task DeleteTableAsync_ShouldPermanentlyDeleteTable_WhenNoActiveOrders()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var table = await service.CreateTableAsync(new CreateTableRequest("T-DEL", 4, 1));
        await service.DeleteTableAsync(table.Id);

        var deleted = await service.GetTableByIdAsync(table.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTableAsync_ShouldThrowValidationException_WhenTableHasActiveOrders()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new TableService(context, _dateTimeProvider, NullLogger<TableService>.Instance);

        var table = await service.CreateTableAsync(new CreateTableRequest("T-OCC", 4, 1));
        await service.SetTableOccupancyAsync(table.Id, true);

        var act = async () => await service.DeleteTableAsync(table.Id);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*active orders*");
    }
}
