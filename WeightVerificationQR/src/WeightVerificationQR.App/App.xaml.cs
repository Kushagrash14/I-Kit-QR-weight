using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WeightVerificationQR.App.ViewModels;
using WeightVerificationQR.App.Views;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;
using WeightVerificationQR.Data;
using WeightVerificationQR.Data.Repositories;
using WeightVerificationQR.Services;

namespace WeightVerificationQR.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"));
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        Services = services.BuildServiceProvider();

        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            await DbInitializer.InitializeAsync(db, hasher);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database initialization failed");
            MessageBox.Show(
                $"Could not initialize the database:\n{ex.Message}\n\nCheck your connection string in appsettings.json.",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var loginWindow = Services.GetRequiredService<LoginView>();
        loginWindow.Show();
    }

    private static void ConfigureServices(ServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        // ---- Options / settings bound from appsettings.json ----
        var dbSettings = configuration.GetSection("Database").Get<DatabaseSettings>() ?? new DatabaseSettings();
        var serialSettings = configuration.GetSection("SerialPort").Get<SerialPortSettings>() ?? new SerialPortSettings();
        var printerSettings = configuration.GetSection("Printer").Get<PrinterSettings>() ?? new PrinterSettings();
        services.AddSingleton(dbSettings);
        services.AddSingleton(serialSettings);
        services.AddSingleton(printerSettings);

        // ---- Data layer ----
        // Swap UseSqlite for UseSqlServer(dbSettings.ConnectionString) for a production SQL Server rollout.
        // Registered as Transient (not the EF default Scoped) because this is a long-lived WPF
        // process with no per-request scope boundary - each repository call gets its own short-lived
        // DbContext instance, which is the recommended pattern for desktop apps using DI containers
        // that are never explicitly scoped per operation.
        services.AddDbContext<AppDbContext>(
            options => options.UseSqlite(dbSettings.ConnectionString),
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IWeighRecordRepository, WeighRecordRepository>();
        services.AddTransient<IUserRepository, UserRepository>();

        // ---- Services ----
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IPrinterService, PrinterService>();
        services.AddSingleton<ISerialPortService, SerialPortService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
        services.AddTransient<IWeighingEngine, WeighingEngine>();

        // Session holds the logged-in user for the lifetime of the app run.
        services.AddSingleton<SessionContext>();

        // ---- ViewModels ----
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductMasterViewModel>();
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<MachineSettingsViewModel>();
        services.AddTransient<PrinterSettingsViewModel>();
        services.AddTransient<QrReprintViewModel>();

        // ---- Windows/Views ----
        services.AddTransient<LoginView>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
