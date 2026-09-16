using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Desktop.Services;
using RestaurantManagement.Desktop.ViewModels;
using RestaurantManagement.Infrastructure;
using Serilog;

namespace RestaurantManagement.Desktop;

/// <summary>
/// Interaction logic for App.xaml with Generic Host, DI, Serilog, Database Initialization, and centralized error handling.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure bootstrap Serilog logger
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        // Setup global error handling
        SetupExceptionHandling();

        try
        {
            Log.Information("Initializing Restaurant Management Desktop Generic Host...");

            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .UseSerilog((context, services, configuration) =>
                {
                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .Enrich.FromLogContext();
                })
                .ConfigureServices((context, services) =>
                {
                    // Clean Architecture layer registrations
                    services.AddApplicationServices();
                    services.AddInfrastructureServices(context.Configuration);

                    // Desktop services & MVVM
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<LoginViewModel>();
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<OrdersViewModel>();
                    services.AddSingleton<KitchenViewModel>();
                    services.AddSingleton<MenuViewModel>();
                    services.AddSingleton<TablesViewModel>();
                    services.AddSingleton<EmployeesViewModel>();
                    services.AddSingleton<BillingViewModel>();
                    services.AddSingleton<InventoryViewModel>();
                    services.AddSingleton<ReportsViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<PlaceholderViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            Log.Information("Restaurant Management Desktop Host started successfully.");

            // Initialize and migrate local SQLite database
            using (var scope = _host.Services.CreateScope())
            {
                var databaseInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await databaseInitializer.InitializeAsync();

                var healthService = scope.ServiceProvider.GetRequiredService<IDatabaseHealthService>();
                var health = await healthService.CheckHealthAsync();
                if (!health.IsHealthy)
                {
                    Log.Warning("Database health check returned unhealthy status on startup: {Message}", health.StatusMessage);
                }
                else
                {
                    Log.Information("Database health verification passed ({ElapsedMs} ms).", health.ResponseTime?.TotalMilliseconds);
                }
            }

            // Launch Main UI Shell
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical failure during Desktop application startup or database initialization.");
            MessageBox.Show(
                "A critical error occurred while initializing the local database or starting the application. " +
                "Please check the log files or contact system administration.",
                "Database / Application Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Restaurant Management Desktop application exiting (Code: {ExitCode})...", e.ApplicationExitCode);

        if (_host != null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while stopping the Generic Host.");
            }
        }

        Log.Information("Restaurant Management Desktop shutdown complete.");
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private void SetupExceptionHandling()
    {
        // 1. WPF UI Thread Unhandled Exceptions
        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI Dispatcher Exception caught.");

            MessageBox.Show(
                "An unexpected error occurred in the application. The system has recorded the incident to the log file.",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            args.Handled = true; // Prevent abrupt crash
        };

        // 2. AppDomain Unhandled Exceptions (Non-UI threads)
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled AppDomain Exception (IsTerminating: {IsTerminating}).", args.IsTerminating);

            if (args.IsTerminating)
            {
                MessageBox.Show(
                    "A fatal non-recoverable error occurred. The application must close.",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        // 3. TaskScheduler Unobserved Task Exceptions
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unobserved Task Exception caught.");
            args.SetObserved(); // Prevent process termination
        };
    }
}
