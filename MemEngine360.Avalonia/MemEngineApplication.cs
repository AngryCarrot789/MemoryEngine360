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

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MemEngine360.BaseFrontEnd;
using MemEngine360.BaseFrontEnd.MemRegions;
using MemEngine360.BaseFrontEnd.Services;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
using MemEngine360.Commands;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Xbox360XBDM;
using MemEngine360.Xbox360XDevkit;
using MemEngine360.XboxInfo;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Configurations.Pages;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Avalonia.Themes;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Configurations;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Services;
using PFXToolKitUI.Themes;

namespace MemEngine360.Avalonia;

public class MemEngineApplication : AvaloniaApplicationPFX {
    public MemEngineApplication(Application application) : base(application) {
    }

    protected override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);

        manager.Register("commands.pfx.ShowActivityListCommand", new ShowActivityListCommand());
        manager.Register("commands.memengine.OpenConsoleConnectionDialogCommand", new OpenConsoleConnectionDialogCommand());
        manager.Register("commands.memengine.FirstScanCommand", new FirstScanCommand());
        manager.Register("commands.memengine.NextScanCommand", new NextScanCommand());
        manager.Register("commands.memengine.ResetScanCommand", new ResetScanCommand());
        manager.Register("commands.memengine.ClearSavedAddressesCommand", new ClearSavedAddressesCommand());
        manager.Register("commands.memengine.AddSavedAddressCommand", new AddSavedAddressCommand());
        manager.Register("commands.memengine.EditScanResultValueCommand", new EditScanResultValueCommand());
        manager.Register("commands.memengine.CopyScanResultsToClipboardCommand", new CopyScanResultsToClipboardCommand());
        manager.Register("commands.memengine.CopySavedResultsToClipboardCommand", new CopySavedResultsToClipboardCommand());
        manager.Register("commands.memengine.EditSavedAddressValueCommand", new EditSavedAddressValueCommand());
        manager.Register("commands.memengine.EditSavedAddressDataTypeCommand", new EditSavedAddressDataTypeCommand());
        manager.Register("commands.memengine.RefreshSavedAddressesCommand", new RefreshSavedAddressesCommand());
        manager.Register("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand", new AddSelectedScanResultsToSavedAddressListCommand());
        manager.Register("commands.memengine.DeleteSelectedSavedAddressesCommand", new DeleteSelectedSavedAddressesCommand());
        manager.Register("commands.memengine.DeleteSelectedScanResultsCommand", new DeleteSelectedScanResultsCommand());
        manager.Register("commands.memengine.SelectRangeFromMemoryRegionCommand", new SelectRangeFromMemoryRegionCommand());
        manager.Register("commands.memengine.ResetScanOptionsCommand", new ResetScanOptionsCommand());
        manager.Register("commands.memengine.ShowMemoryCommand", new ShowMemoryCommand());
        manager.Register("commands.memengine.DumpMemoryCommand", new DumpMemoryCommand());
        manager.Register("commands.memengine.OpenFileAsSavedAddressesCommand", new OpenFileAsSavedAddressesCommand());
        manager.Register("commands.memengine.SaveFileAsSavedAddressesCommand", new SaveFileAsSavedAddressesCommand());

        // Remote commands
        manager.Register("commands.memengine.remote.MemProtectionCommand", new MemProtectionCommand());
        manager.Register("commands.memengine.remote.SoftRebootCommand", new SoftRebootCommand());
        manager.Register("commands.memengine.remote.ColdRebootCommand", new ColdRebootCommand());
        manager.Register("commands.memengine.remote.ShutdownCommand", new ShutdownCommand());
        manager.Register("commands.memengine.remote.DebugFreezeCommand", new DebugFreezeCommand());
        manager.Register("commands.memengine.remote.DebugUnfreezeCommand", new DebugUnfreezeCommand());


        // Hex editor commands
        manager.Register("commands.hexeditor.ReloadSelectionFromConsole", new ReloadSelectionFromConsole());
        manager.Register("commands.hexeditor.ReadAllFromConsoleCommand", new ReadAllFromConsoleCommand());
        manager.Register("commands.hexeditor.UploadSelectionToConsoleCommand", new UploadSelectionToConsoleCommand());
        manager.Register("commands.hexeditor.GotoAddressCommand", new GotoAddressCommand());
        manager.Register("commands.hexeditor.SetAutoScanRangeAsSelectionCommand", new SetAutoScanRangeAsSelectionCommand());
        manager.Register("commands.hexeditor.ClearAutoScanRangeCommand", new ClearAutoScanRangeCommand());
        manager.Register("commands.hexeditor.SaveSelectionAsFileCommand", new SaveSelectionAsFileCommand());
    }

    protected override async Task OnSetupApplication(IApplicationStartupProgress progress) {
        await base.OnSetupApplication(progress);
        this.PluginLoader.AddCorePlugin(typeof(PluginXbox360Xbdm));

        if (OperatingSystem.IsWindows()) {
            this.PluginLoader.AddCorePlugin(typeof(PluginXbox360XDevkit));
        }
    }

    protected override void RegisterServices(ServiceManager manager) {
        if (this.Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime) {
            manager.RegisterConstant<IDesktopService>(new DesktopServiceImpl(this.Application));
            manager.RegisterConstant<WindowingSystem>(new WindowingSystemImpl());
        }

        base.RegisterServices(manager);

        manager.RegisterConstant<IIconPreferences>(new IconPreferencesImpl());
        manager.RegisterConstant<IStartupManager>(new StartupManagerMemEngine360());
        manager.RegisterConstant<IAboutService>(new AboutServiceImpl());
        manager.RegisterConstant<IHexDisplayService>(new HexDisplayServiceImpl());
        manager.RegisterConstant<ConsoleConnectionManager>(new ConsoleConnectionManagerImpl());
        manager.RegisterConstant<MemoryEngineManager>(new MemoryEngineManagerImpl());
    }

    protected override void RegisterConfigurations() {
        this.PersistentStorageManager.Register<ThemeConfigurationOptions>(new ThemeConfigurationOptionsImpl(), "themes", "themes");
        this.PersistentStorageManager.Register(new BasicApplicationConfiguration(), null, "basic");
    }

    protected override Task OnApplicationFullyLoaded() {
        UserInputDialogView.Registry.RegisterType<SavedResultDataTypeUserInputInfo>(() => new SavedResultDataTypeEditorUserInputControl());
        UserInputDialogView.Registry.RegisterType<MemoryRegionUserInputInfo>(() => new XboxMemoryRegionViewerUIControl());
        UserInputDialogView.Registry.RegisterType<ModuleUserInputInfo>(() => new XboxModuleViewerUIControl());
        ConfigurationPageRegistry.Registry.RegisterType<MemEngineConfigurationPage>(() => new MemEngineConfigurationPageControl());

        ApplicationConfigurationManager.Instance.RootEntry.AddEntry(new ConfigurationEntry() {
            DisplayName = "MemEngine", Id = "config.memengine", Page = new MemEngineConfigurationPage()
        });

        return base.OnApplicationFullyLoaded();
    }

    protected override string? GetSolutionFileName() {
        return "MemEngine.sln";
    }

    public override string GetApplicationName() {
        return "MemEngine360";
    }

    private class IconPreferencesImpl : IIconPreferences {
        public bool UseAntiAliasing { get; set; } = true;
    }

    private class StartupManagerMemEngine360 : IStartupManager {
        public async Task OnApplicationStartupWithArgs(string[] args) {
            // IXboxManager xboxManager = new XboxManager();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
                MemEngineWindow view = new MemEngineWindow();
                system.Register(view, true);
                view.Show();

                // using IDisposable? token = await view.MemoryEngine360.BeginBusyOperationActivityAsync();
                // if (token != null) {
                //     using CancellationTokenSource cts = new CancellationTokenSource();
                //     IConsoleConnection? connection = await ConsoleTypeXbox360Xbdm.Instance.OpenConnection(view.MemoryEngine360, new ConnectToXboxInfo(view.MemoryEngine360) { IpAddress = "192.168.1.202" }, cts);
                //     if (connection != null) {
                //         view.MemoryEngine360.SetConnection(token, connection, ConnectionChangeCause.Custom);
                //
                //         await ApplicationPFX.Instance.ServiceManager.GetService<IHexDisplayService>().ShowHexEditor(new HexDisplayInfo(view.MemoryEngine360) {
                //             StartAddress = 0x8303A000,
                //             Length = 0x2000,
                //             AutoRefreshStartAddress = 0x8303A5C0,
                //             AutoRefreshLength = 1800,
                //         });
                //     }
                // }
            }
        }
    }

    private class AboutServiceImpl : IAboutService {
        public async Task ShowDialog() {
            if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
                system.Register(new AboutWindow()).Show(system.GetActiveWindowOrNull());
            }
        }
    }
}