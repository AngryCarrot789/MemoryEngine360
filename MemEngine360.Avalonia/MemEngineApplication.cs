// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

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
using PFXToolKitUI.Avalonia.Services.Windowing;
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
        manager.Register("commands.memengine.remote.ShowConsoleIDCommand", new ShowConsoleIDCommand());
        manager.Register("commands.memengine.remote.ShowCPUKeyCommand", new ShowCPUKeyCommand());
    }

    protected override void RegisterServices(ServiceManager manager) {
        manager.RegisterConstant<IDesktopService>(new DesktopServiceImpl(this.Application));
        manager.RegisterConstant<WindowingSystem>(new WindowingSystemDesktop());
        base.RegisterServices(manager);

        manager.RegisterConstant<IIconPreferences>(new IconPreferencesImpl());
        manager.RegisterConstant<IStartupManager>(new StartupManagerMemEngine360());
        manager.RegisterConstant(new ConsoleConnectionService());
        manager.RegisterConstant<IAboutService>(new AboutServiceImpl());
        manager.RegisterConstant<IEditSavedAddressService>(new EditSavedAddressServiceImpl());
    }

    private class EditSavedAddressServiceImpl : IEditSavedAddressService {
        public async Task<bool?> ShowDialog(SavedResultDataTypeUserInputInfo info) {
            return await UserInputDialogView.ShowDialogAsync(info);
        }
    }

    protected override void RegisterConfigurations() {
        this.PersistentStorageManager.Register<ThemeConfigurationOptions>(new ThemeConfigurationOptionsImpl(), "themes", "themes");
        this.PersistentStorageManager.Register(new BasicApplicationConfiguration(), null, "basic");
    }

    protected override Task OnApplicationFullyLoaded() {
        UserInputDialogView.Registry.RegisterType<SavedResultDataTypeUserInputInfo>(() => new SavedResultDataTypeUserInputControl());

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

    private class StartupManagerMemEngine360 : IStartupManager {
        public Task OnApplicationStartupWithArgs(string[] args) {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            }
            
            if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
                system.CreateWindow(new MemEngineView(), true).Show(null);
            }

            return Task.CompletedTask;
        }
    }

    private class AboutServiceImpl : IAboutService {
        public async Task ShowDialog() {
            if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
                system.CreateWindow(new AboutView()).Show(system.GetActiveWindowOrNull());
            }
        }
    }
}