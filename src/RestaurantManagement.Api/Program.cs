using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Application;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Inventory.Interfaces;
using RestaurantManagement.Application.Menu.DTOs;
using RestaurantManagement.Application.Menu.Interfaces;
using RestaurantManagement.Application.Settings.Interfaces;
using RestaurantManagement.Infrastructure;
using Serilog;

// Configure bootstrap Serilog logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Restaurant Management API host...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Integrate Serilog with full configuration
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();
    });

    // Add CORS for web client access
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Register Clean Architecture layers
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // ==========================================
    // 1. Health and Status Endpoints
    // ==========================================
    app.MapGet("/health", async (IAppInfoService appInfoService, IDatabaseHealthService databaseHealthService) =>
    {
        var appInfo = appInfoService.GetAppInfo();
        var dbHealth = await databaseHealthService.CheckHealthAsync();
        var isOverallHealthy = dbHealth.IsHealthy;

        return Results.Json(new
        {
            Status = isOverallHealthy ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Application = appInfo,
            Database = new
            {
                Status = dbHealth.StatusMessage,
                IsHealthy = dbHealth.IsHealthy,
                ResponseTimeMs = dbHealth.ResponseTime?.TotalMilliseconds
            }
        }, statusCode: isOverallHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    })
    .WithName("HealthCheck");

    // ==========================================
    // 2. Restaurant Profile & Settings
    // ==========================================
    app.MapGet("/api/profile", async (ISettingsService settingsService, CancellationToken ct) =>
    {
        var profile = await settingsService.GetRestaurantProfileAsync(ct);
        var config = await settingsService.GetAppConfigAsync(ct);
        return Results.Ok(new
        {
            Profile = profile,
            Config = config
        });
    });

    // ==========================================
    // 3. Menu Aggregation Endpoint (Complete Menu)
    // ==========================================
    app.MapGet("/api/menu", async (
        ICategoryService categoryService,
        IProductService productService,
        ISettingsService settingsService,
        IInventoryService inventoryService,
        CancellationToken ct) =>
    {
        var profile = await settingsService.GetRestaurantProfileAsync(ct);
        var categories = await categoryService.GetCategoriesAsync(includeInactive: false, ct);
        var products = await productService.GetProductsAsync(includeInactive: false, ct);
        var inventorySummary = await inventoryService.GetInventorySummaryAsync(cancellationToken: ct);

        var inventoryMap = inventorySummary.Items.ToDictionary(i => i.ProductId, i => i);

        var menuCategories = categories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c =>
            {
                var categoryProducts = products
                    .Where(p => p.CategoryId == c.Id)
                    .OrderBy(p => p.DisplayOrder)
                    .ThenBy(p => p.Name)
                    .Select(p =>
                    {
                        var hasInv = inventoryMap.TryGetValue(p.Id, out var inv);
                        return new
                        {
                            p.Id,
                            p.Name,
                            p.Description,
                            p.Price,
                            p.CategoryId,
                            p.CategoryName,
                            p.IsActive,
                            p.IsAvailable,
                            p.DisplayOrder,
                            p.ImagePath,
                            StockStatus = hasInv && inv != null
                                ? inv.StockStatus
                                : (p.IsAvailable ? "InStock" : "OutOfStock"),
                            CurrentStock = hasInv && inv != null ? (int?)inv.CurrentStock : null
                        };
                    })
                    .ToList();

                return new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.DisplayOrder,
                    c.IsActive,
                    ProductCount = categoryProducts.Count,
                    Products = categoryProducts
                };
            })
            .ToList();

        return Results.Ok(new
        {
            Restaurant = new
            {
                profile.RestaurantName,
                profile.Address,
                profile.PhoneNumber,
                profile.CurrencySymbol,
                profile.DefaultTaxRatePercent
            },
            Categories = menuCategories
        });
    });

    // ==========================================
    // 4. Categories Endpoints
    // ==========================================
    app.MapGet("/api/categories", async (ICategoryService categoryService, [FromQuery] bool? includeInactive, CancellationToken ct) =>
    {
        var categories = await categoryService.GetCategoriesAsync(includeInactive ?? false, ct);
        return Results.Ok(categories);
    });

    app.MapGet("/api/categories/{id:guid}", async (Guid id, ICategoryService categoryService, CancellationToken ct) =>
    {
        var category = await categoryService.GetCategoryByIdAsync(id, ct);
        return category is not null ? Results.Ok(category) : Results.NotFound();
    });

    app.MapPost("/api/categories", async (CreateCategoryRequest request, ICategoryService categoryService, CancellationToken ct) =>
    {
        try
        {
            var created = await categoryService.CreateCategoryAsync(request, ct);
            return Results.Created($"/api/categories/{created.Id}", created);
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPut("/api/categories/{id:guid}", async (Guid id, UpdateCategoryRequest request, ICategoryService categoryService, CancellationToken ct) =>
    {
        if (id != request.Id) return Results.BadRequest(new { error = "Id mismatch in URL and payload." });
        try
        {
            var updated = await categoryService.UpdateCategoryAsync(request, ct);
            return Results.Ok(updated);
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapDelete("/api/categories/{id:guid}", async (Guid id, ICategoryService categoryService, CancellationToken ct) =>
    {
        try
        {
            await categoryService.DeleteCategoryAsync(id, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    // ==========================================
    // 5. Products Endpoints
    // ==========================================
    app.MapGet("/api/products", async (IProductService productService, [FromQuery] bool? includeInactive, [FromQuery] Guid? categoryId, CancellationToken ct) =>
    {
        if (categoryId.HasValue)
        {
            var categoryProducts = await productService.GetProductsByCategoryAsync(categoryId.Value, includeInactive ?? false, ct);
            return Results.Ok(categoryProducts);
        }

        var products = await productService.GetProductsAsync(includeInactive ?? false, ct);
        return Results.Ok(products);
    });

    app.MapGet("/api/products/{id:guid}", async (Guid id, IProductService productService, CancellationToken ct) =>
    {
        var product = await productService.GetProductByIdAsync(id, ct);
        return product is not null ? Results.Ok(product) : Results.NotFound();
    });

    app.MapPost("/api/products", async (CreateProductRequest request, IProductService productService, CancellationToken ct) =>
    {
        try
        {
            var created = await productService.CreateProductAsync(request, ct);
            return Results.Created($"/api/products/{created.Id}", created);
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, IProductService productService, CancellationToken ct) =>
    {
        if (id != request.Id) return Results.BadRequest(new { error = "Id mismatch in URL and payload." });
        try
        {
            var updated = await productService.UpdateProductAsync(request, ct);
            return Results.Ok(updated);
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapPatch("/api/products/{id:guid}/availability", async (Guid id, [FromBody] AvailabilityToggleRequest payload, IProductService productService, CancellationToken ct) =>
    {
        try
        {
            await productService.SetProductAvailabilityAsync(id, payload.IsAvailable, ct);
            return Results.Ok(new { Id = id, IsAvailable = payload.IsAvailable });
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    app.MapDelete("/api/products/{id:guid}", async (Guid id, IProductService productService, CancellationToken ct) =>
    {
        try
        {
            await productService.DeleteProductAsync(id, ct);
            return Results.NoContent();
        }
        catch (RestaurantManagement.Application.Common.Exceptions.ValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    Log.Information("Restaurant Management API host successfully configured and ready.");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Restaurant Management API terminated unexpectedly during startup.");
    throw;
}
finally
{
    Log.Information("Restaurant Management API shutting down.");
    Log.CloseAndFlush();
}

public record AvailabilityToggleRequest(bool IsAvailable);
