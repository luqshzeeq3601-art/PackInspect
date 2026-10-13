using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackInspect.App.ViewModels;
using PackInspect.App.Views;
using PackInspect.Core.Ai;
using PackInspect.Core.Auth;
using PackInspect.Core.Devices;
using PackInspect.Core.Domain;
using PackInspect.Core.Machine;
using PackInspect.Core.Publishing;
using PackInspect.Core.Sequence;
using PackInspect.Data;
using PackInspect.Devices.Ai;
using PackInspect.Devices.Barcode;
using PackInspect.Devices.Camera;
using PackInspect.Devices.Modbus;
using PackInspect.Devices.Publishing;
using PackInspect.Devices.Scpi;
using PackInspect.Devices.Vision;
using Serilog;

namespace PackInspect.App;

/// <summary>
/// Composition root: the only place that knows which concrete device drivers
/// are in use. Swapping the simulator for real hardware means changing the
/// registrations in <see cref="ConfigureServices"/> and nothing else.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Settings, logs and data live next to the executable, wherever the app is started from.
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        ConfigureServices(builder.Services, builder.Configuration);
        _host = builder.Build();

        var lotViewModel = _host.Services.GetRequiredService<LotViewModel>();
        var recipeEditorViewModel = _host.Services.GetRequiredService<RecipeEditorViewModel>();
        var auditTrailViewModel = _host.Services.GetRequiredService<AuditTrailViewModel>();
        var userManagementViewModel = _host.Services.GetRequiredService<UserManagementViewModel>();
        var reviewViewModel = _host.Services.GetRequiredService<ReviewViewModel>();
        try
        {
            await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            await lotViewModel.LoadRecipesAsync(CancellationToken.None);
            await recipeEditorViewModel.LoadRecipesAsync(CancellationToken.None);
            await auditTrailViewModel.RefreshAsync(CancellationToken.None);
            await userManagementViewModel.RefreshAsync(CancellationToken.None);
            await reviewViewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            // Without its database the station cannot record results, so it must not start.
            _host.Services.GetRequiredService<ILogger<App>>().LogCritical(ex, "The database could not be opened");
            MessageBox.Show($"The database could not be opened:\n{ex.Message}", "PackInspect", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _host.Services.GetRequiredService<MachineController>().StartMonitoring();

        var window = new MainWindow { DataContext = _host.Services.GetRequiredService<MainViewModel>() };
        window.Closed += OnMainWindowClosed;
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        services.AddSerilog(logger => logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "packinspect-.log"),
                rollingInterval: RollingInterval.Day));

        services.AddSingleton(configuration.GetSection("Machine").Get<MachineOptions>() ?? new MachineOptions());
        services.AddSingleton(configuration.GetSection("Sequence").Get<SequenceOptions>() ?? new SequenceOptions());
        services.AddSingleton(configuration.GetSection("Plc").Get<ModbusOptions>() ?? new ModbusOptions());
        services.AddSingleton(configuration.GetSection("Instrument").Get<ScpiOptions>() ?? new ScpiOptions());
        services.AddSingleton(configuration.GetSection("Camera").Get<CameraOptions>() ?? new CameraOptions());
        services.AddSingleton(new ImageStoreOptions { RootDirectory = Path.Combine(dataDirectory, "images") });
        services.AddSingleton(TimeProvider.System);

        // Devices: simulated hardware reached over Modbus TCP, SCPI and a camera socket.
        services.AddSingleton<ModbusConnection>();
        services.AddSingleton<IPlc, ModbusPlc>();
        services.AddSingleton<IMotionAxis, ModbusMotionAxis>();
        services.AddSingleton<IInstrument, ScpiInstrument>();
        services.AddSingleton<ICamera, TcpCamera>();
        services.AddSingleton<IVisionInspector, OpenCvVisionInspector>();
        services.AddSingleton<IImageStore, PngImageStore>();
        services.AddSingleton<IBarcodeReader, LegacyBarcodeReaderAdapter>();

        // Results: local JSON files by default, so the station makes no network call.
        // The HTTP adapter is the seam for a factory server and is switched on in appsettings.json.
        var fabTraceOptions = configuration.GetSection("FabTrace").Get<FabTracePublisherOptions>() ?? new FabTracePublisherOptions();
        if (fabTraceOptions.Enabled)
        {
            if (string.IsNullOrWhiteSpace(fabTraceOptions.QueueDirectory))
            {
                fabTraceOptions.QueueDirectory = Path.Combine(dataDirectory, "store_and_forward");
            }

            services.AddSingleton<IResultPublisher>(sp => new FabTraceHttpResultPublisher(
                fabTraceOptions,
                logger: sp.GetService<ILogger<FabTraceHttpResultPublisher>>()));
        }
        else
        {
            services.AddSingleton<IResultPublisher>(new FileResultPublisher(Path.Combine(dataDirectory, "published")));
        }

        // Data: SQLite through EF Core.
        services.AddPackInspectData(Path.Combine(dataDirectory, "packinspect.db"));

        // Machine logic and access control.
        services.AddSingleton<IUserSession, CurrentUserSession>();
        services.AddSingleton<LotSession>();
        services.AddSingleton<IMachineCycle, InspectionCycle>();
        services.AddSingleton<MachineController>();

        services.AddSingleton<AuthViewModel>();
        services.AddSingleton<RecipeEditorViewModel>();
        services.AddSingleton<AuditTrailViewModel>();
        services.AddSingleton<UserManagementViewModel>();
        services.AddSingleton<LotViewModel>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<MainViewModel>();

        // AI defect classification
        string modelsDir = Path.Combine(AppContext.BaseDirectory, "models");
        if (!Directory.Exists(modelsDir))
        {
            modelsDir = Path.GetFullPath("models");
        }
        string backbonePath = Path.Combine(modelsDir, "dinov2_small_quantized.onnx");
        string headPath = Path.Combine(modelsDir, "defect_classifier_head.zip");

        // Both files or no classifier at all: a backbone without a trained head
        // would label every unit as good, which is worse than giving no opinion.
        if (File.Exists(backbonePath) && File.Exists(headPath))
        {
            services.AddSingleton<IDefectClassifier>(new Dinov2DefectClassifier(backbonePath, headPath));
        }
    }

    // Disposing the host stops the machine controller and flushes the log
    // without blocking the UI thread; only then does the process exit.
    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (_host is IAsyncDisposable host)
        {
            await host.DisposeAsync();
        }

        Shutdown();
    }
}
