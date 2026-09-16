using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;

namespace RestaurantManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure layer services and Entity Framework Core SQLite database context.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Core infrastructure services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IAppInfoService>(sp =>
            new AppInfoService(configuration, sp.GetRequiredService<IDateTimeProvider>()));

        // 2. EF Core SQLite Database Configuration
        var rawConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var resolvedConnectionString = DatabasePathResolver.ResolveConnectionString(rawConnectionString);

        services.AddDbContext<RestaurantDbContext>(options =>
        {
            options.UseSqlite(resolvedConnectionString);
        });

        // 3. Database initialization and health check services
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

        // 4. Milestone 3 Application Domain Services (Menu & Tables)
        services.AddScoped<RestaurantManagement.Application.Menu.Interfaces.ICategoryService, CategoryService>();
        services.AddScoped<RestaurantManagement.Application.Menu.Interfaces.IProductService, ProductService>();
        services.AddScoped<RestaurantManagement.Application.Tables.Interfaces.ITableService, TableService>();

        // 5. Milestone 4 Authentication & Employee Services
        services.AddSingleton<IPasswordHasher, RestaurantManagement.Infrastructure.Security.PasswordHasher>();
        services.AddSingleton<RestaurantManagement.Application.Authentication.Interfaces.IAuthorizationService, AuthorizationService>();
        services.AddSingleton<RestaurantManagement.Application.Authentication.Interfaces.ICurrentUserService, CurrentUserService>();
        services.AddScoped<RestaurantManagement.Application.Authentication.Interfaces.IAuthenticationService, AuthenticationService>();
        services.AddScoped<RestaurantManagement.Application.Employees.Interfaces.IEmployeeService, EmployeeService>();

        // 6. Milestone 5, 6 & 7 Order, Kitchen, Billing & Payment Services
        services.AddScoped<RestaurantManagement.Application.Orders.Interfaces.IOrderService, OrderService>();
        services.AddScoped<RestaurantManagement.Application.Kitchen.Interfaces.IKitchenService, KitchenService>();
        services.AddScoped<RestaurantManagement.Application.Billing.Interfaces.IBillingService, BillingService>();
        services.AddScoped<RestaurantManagement.Application.Billing.Interfaces.IPaymentService, PaymentService>();

        // 7. Inventory, Reports & Analytics, and Settings Services
        services.AddScoped<RestaurantManagement.Application.Inventory.Interfaces.IInventoryService, InventoryService>();
        services.AddScoped<RestaurantManagement.Application.Reports.Interfaces.IReportService, ReportService>();
        services.AddScoped<RestaurantManagement.Application.Settings.Interfaces.ISettingsService, SettingsService>();

        return services;
    }
}
