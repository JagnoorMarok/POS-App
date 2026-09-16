using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class CategoryServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CategoryServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantCategoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "category_test.db");
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
    public async Task CreateCategoryAsync_ShouldCreateCategory_WhenValid()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var request = new CreateCategoryRequest("Appetizers", "Tasty starters", 1, true);
        var result = await service.CreateCategoryAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Appetizers");
        result.Description.Should().Be("Tasty starters");
        result.DisplayOrder.Should().Be(1);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCategoryAsync_ShouldRejectDuplicateCategoryName()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        await service.CreateCategoryAsync(new CreateCategoryRequest("Desserts", "Sweet treats", 1));

        var act = async () => await service.CreateCategoryAsync(new CreateCategoryRequest("desserts", "Another dessert category", 2));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCategoryAsync_ShouldRejectEmptyName(string emptyName)
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var act = async () => await service.CreateCategoryAsync(new CreateCategoryRequest(emptyName, "Desc", 1));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateCategoryAsync_ShouldUpdatePropertiesSuccessfully()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest("Soups", "Hot soups", 1));

        var updateRequest = new UpdateCategoryRequest(created.Id, "Hot Soups & Stews", "Freshly made daily", 2, true);
        var updated = await service.UpdateCategoryAsync(updateRequest);

        updated.Name.Should().Be("Hot Soups & Stews");
        updated.Description.Should().Be("Freshly made daily");
        updated.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task DeactivateAndActivateCategoryAsync_ShouldToggleStatus()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest("Seasonal", "Seasonal items", 5));

        await service.DeactivateCategoryAsync(created.Id);

        var deactivated = await service.GetCategoryByIdAsync(created.Id);
        deactivated.Should().NotBeNull();
        deactivated!.IsActive.Should().BeFalse();

        var activeOnly = await service.GetCategoriesAsync(includeInactive: false);
        activeOnly.Should().NotContain(c => c.Id == created.Id);

        await service.ActivateCategoryAsync(created.Id);
        var activated = await service.GetCategoryByIdAsync(created.Id);
        activated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCategoryAsync_ShouldDeleteCategory_WhenEmpty()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest("Temp Cat", "No items", 10));
        await service.DeleteCategoryAsync(created.Id);

        var deleted = await service.GetCategoryByIdAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCategoryAsync_ShouldThrowValidationException_WhenCategoryContainsProducts()
    {
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new CategoryService(context, _dateTimeProvider, NullLogger<CategoryService>.Instance);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest("NonEmpty Cat", "Has items", 11));
        var product = new Product(Guid.NewGuid(), created.Id, "Item 1", null, 100m, _dateTimeProvider.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var act = async () => await service.DeleteCategoryAsync(created.Id);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*contains*product*");
    }
}
