using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MemEngine360.Avalonia.Services;
using MemEngine360.Commands;
using MemEngine360.Configs;
using MemEngine360.Connections;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Avalonia.Themes;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Services;
using PFXToolKitUI.Themes;

namespace MemEngine360.Avalonia;

public class MemEngineApplication : AvaloniaApplicationPFX {
    public MemEngineApplication(Application application) : base(application) {
    }

    protected override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);
        
        manager.Register("commands.memengine.ConnectToConsoleCommand", new ConnectToConsoleCommand());
        manager.Register("commands.memengine.FirstScanCommand", new FirstScanCommand());
        manager.Register("commands.memengine.NextScanCommand", new NextScanCommand());
        manager.Register("commands.memengine.ResetScanCommand", new ResetScanCommand());
        manager.Register("commands.memengine.ClearSavedAddressesCommand", new ClearSavedAddressesCommand());
        manager.Register("commands.memengine.AddSavedAddressCommand", new AddSavedAddressCommand());
        manager.Register("commands.memengine.EditScanResultValueCommand", new EditScanResultValueCommand());
        manager.Register("commands.memengine.EditSavedAddressValueCommand", new EditSavedAddressValueCommand());
        manager.Register("commands.memengine.EditSavedAddressDataTypeCommand", new EditSavedAddressDataTypeCommand());
        manager.Register("commands.memengine.RefreshSavedAddressesCommand", new RefreshSavedAddressesCommand());
        manager.Register("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand", new AddSelectedScanResultsToSavedAddressListCommand());
        manager.Register("commands.memengine.remote.EjectDiskTrayCommand", new EjectDiskTrayCommand());
        manager.Register("commands.memengine.remote.ShutdownCommand", new ShutdownCommand());
        manager.Register("commands.memengine.remote.SoftRebootCommand", new SoftRebootCommand());
        manager.Register("commands.memengine.remote.ColdRebootCommand", new ColdRebootCommand());
        manager.Register("commands.memengine.remote.DebugFreezeCommand", new DebugFreezeCommand());
        manager.Register("commands.memengine.remote.DebugUnfreezeCommand", new DebugUnfreezeCommand());
        manager.Register("commands.memengine.remote.ListHelpCommand", new ListHelpCommand());
    }

    protected override void RegisterServices(ServiceManager manager) {
        base.RegisterServices(manager);
        
        manager.RegisterConstant<IIconPreferences>(new IconPreferencesImpl());
        manager.RegisterConstant<IStartupManager>(new StartupManagerFramePFX());
        manager.RegisterConstant<IDesktopService>(new DesktopServiceImpl(this.Application));
        manager.RegisterConstant(new ConsoleConnectionService());
        manager.RegisterConstant<IAboutService>(new AboutServiceImpl());
        manager.RegisterConstant<IEditSavedAddressService>(new EditSavedAddressServiceImpl());
    }

    private class EditSavedAddressServiceImpl : IEditSavedAddressService {
        public Task<bool?> ShowDialog(SavedResultDataTypeUserInputInfo info) => UserInputDialog.ShowDialogAsync(info);
    }
    
    protected override void RegisterConfigurations() {
        this.PersistentStorageManager.Register<ThemeConfigurationOptions>(new ThemeConfigurationOptionsImpl(), "themes", "themes");
        this.PersistentStorageManager.Register(new BasicApplicationConfiguration(), null, "basic");
    }

    protected override Task OnApplicationFullyLoaded() {
        UserInputDialog.Registry.RegisterType<SavedResultDataTypeUserInputInfo>(() => new SavedResultDataTypeUserInputControl());
        
        return base.OnApplicationFullyLoaded();
    }

    protected override string? GetSolutionFileName() {
        return "MemEngine.sln";
    }

    public override string GetApplicationName() {
        return "MemEngine360";
    }
    
    private class IconPreferencesImpl : IIconPreferences {
        public bool UseAntiAliasing { get; set; }
    }
    
    private class StartupManagerFramePFX : IStartupManager {
        public async Task OnApplicationStartupWithArgs(string[] args) {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                MainWindow window = new MainWindow();
                desktop.MainWindow = window;
                window.Show();
            
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            }
        }
    }

    private class AboutServiceImpl : IAboutService {
        public Task ShowDialog() {
            if (IDesktopService.TryGetInstance(out IDesktopService? service) && service.TryGetActiveWindow(out Window? window))
                return new AboutWindow().ShowDialog(window);

            new AboutWindow().Show();
            return Task.CompletedTask;
        }
    }
}