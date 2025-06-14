// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MemEngine360.BaseFrontEnd;
using MemEngine360.BaseFrontEnd.FileConnections;
using MemEngine360.BaseFrontEnd.MemRegions;
using MemEngine360.BaseFrontEnd.Services;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
using MemEngine360.BaseFrontEnd.TaskSequencing;
using MemEngine360.BaseFrontEnd.Themes;
using MemEngine360.BaseFrontEnd.XboxBase;
using MemEngine360.Commands;
using MemEngine360.Commands.ATM;
using MemEngine360.Commands.ATM.XML;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Testing;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Commands;
using MemEngine360.Xbox360XBDM;
using MemEngine360.Xbox360XDevkit;
using MemEngine360.XboxBase;
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

public class MemoryEngineApplication : AvaloniaApplicationPFX {
    public MemoryEngineApplication(Application application) : base(application) {
    }

    protected override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);

        manager.Register("commands.pfx.ShowActivityListCommand", new ShowActivityListCommand());
        manager.Register("commands.memengine.OpenConsoleConnectionDialogCommand", new OpenConsoleConnectionDialogCommand());
        manager.Register("commands.memengine.OpenConsoleConnectionInSequencerCommand", new OpenConsoleConnectionInSequencerCommand());
        manager.Register("commands.memengine.FirstScanCommand", new FirstScanCommand());
        manager.Register("commands.memengine.NextScanCommand", new NextScanCommand());
        manager.Register("commands.memengine.ResetScanCommand", new ResetScanCommand());
        manager.Register("commands.memengine.ClearSavedAddressesCommand", new ClearSavedAddressesCommand());
        manager.Register("commands.memengine.AddSavedAddressCommand", new AddSavedAddressCommand());
        manager.Register("commands.memengine.EditScanResultValueCommand", new EditScanResultValueCommand());
        manager.Register("commands.memengine.CopyScanResultsToClipboardCommand", new CopyScanResultsToClipboardCommand());
        manager.Register("commands.memengine.CopyAddressTableEntryToClipboard", new CopyAddressTableEntryToClipboardCommand());
        manager.Register("commands.memengine.CopyAbsoluteAddressToClipboardCommand", new CopyAbsoluteAddressToClipboardCommand());
        manager.Register("commands.memengine.EditSavedAddressValueCommand", new EditSavedAddressValueCommand());
        manager.Register("commands.memengine.EditSavedAddressDataTypeCommand", new EditSavedAddressDataTypeCommand());
        manager.Register("commands.memengine.EditSavedAddressDescriptionCommand", new EditSavedAddressDescriptionCommand());
        manager.Register("commands.memengine.EditSavedAddressAddressCommand", new EditSavedAddressAddressCommand());
        manager.Register("commands.memengine.RefreshSavedAddressesCommand", new RefreshSavedAddressesCommand());
        manager.Register("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand", new AddSelectedScanResultsToSavedAddressListCommand());
        manager.Register("commands.memengine.DeleteSelectedSavedAddressesCommand", new DeleteSelectedSavedAddressesCommand());
        manager.Register("commands.memengine.DeleteSelectedScanResultsCommand", new DeleteSelectedScanResultsCommand());
        manager.Register("commands.memengine.SelectRangeFromMemoryRegionCommand", new SelectRangeFromMemoryRegionCommand());
        manager.Register("commands.memengine.ResetScanOptionsCommand", new ResetScanOptionsCommand());
        manager.Register("commands.memengine.ShowMemoryCommand", new ShowMemoryViewCommand());
        manager.Register("commands.memengine.DumpMemoryCommand", new DumpMemoryCommand());
        manager.Register("commands.memengine.GroupEntriesCommand", new GroupEntriesCommand());
        manager.Register("commands.memengine.OpenCSVFileCommand", new OpenCSVFileCommand());
        manager.Register("commands.memengine.OpenXMLFileCommand", new OpenXMLFileCommand());
        manager.Register("commands.memengine.SaveSavedAddressesToFileCommand", new SaveSavedAddressesToFileCommand());
        manager.Register("commands.memengine.ToggleSavedAddressAutoRefreshCommand", new ToggleSavedAddressAutoRefreshCommand());
        manager.Register("commands.memengine.ShowModulesCommand", new ShowModulesCommand());
        manager.Register("commands.memengine.PointerScanCommand", new PointerScanCommand());
        manager.Register("commands.moduleviewer.ShowModuleSectionInfoInDialogCommand", new ShowModuleSectionInfoInDialogCommand());

        // Remote commands
        manager.Register("commands.memengine.remote.ShowMemoryRegionsCommand", new ShowMemoryRegionsCommand());
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

        manager.Register("commands.memengine.OpenTaskSequencerCommand", new OpenTaskSequencerCommand());
        manager.Register("commands.sequencer.DeleteSequenceSelectionCommand", new DeleteSequenceSelectionCommand());
        manager.Register("commands.sequencer.DeleteOperationSelectionCommand", new DeleteOperationSelectionCommand());
        manager.Register("commands.sequencer.DeleteConditionSelectionCommand", new DeleteConditionSelectionCommand());
        manager.Register("commands.sequencer.ConnectToDedicatedConsoleCommand", new ConnectToDedicatedConsoleCommand());
        manager.Register("commands.sequencer.ClearSequencesCommand", new ClearSequencesCommand());
        manager.Register("commands.sequencer.NewSequenceCommand", new NewSequenceCommand());
        manager.Register("commands.sequencer.DuplicateSequenceCommand", new DuplicateSequencesCommand());
        manager.Register("commands.sequencer.DuplicateOperationsCommand", new DuplicateOperationsCommand());
        manager.Register("commands.sequencer.DuplicateConditionsCommand", new DuplicateConditionsCommand());
        manager.Register("commands.sequencer.RenameSequenceCommand", new RenameSequenceCommand());
        manager.Register("commands.sequencer.StopSequenceCommand", new StopSequenceCommand());
        manager.Register("commands.sequencer.RunSequenceCommand", new RunSequenceCommand());
        manager.Register("commands.sequencer.ToggleOperationEnabledCommand", new ToggleOperationEnabledCommand());
        manager.Register("commands.sequencer.ToggleConditionEnabledCommand", new ToggleConditionEnabledCommand());
    }

    protected override void RegisterServices(ServiceManager manager) {
        if (this.Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime) {
            manager.RegisterConstant<IDesktopService>(new DesktopServiceImpl(this.Application));
            manager.RegisterConstant<WindowingSystem>(new WindowingSystemImpl(new Uri("avares://MemoryEngine360/Icons/icon-16.bmp", UriKind.RelativeOrAbsolute)));
        }

        base.RegisterServices(manager);

        manager.RegisterConstant<IIconPreferences>(new IconPreferencesImpl());
        manager.RegisterConstant<IStartupManager>(new StartupManagerMemoryEngine360());
        manager.RegisterConstant<IAboutService>(new AboutServiceImpl());
        manager.RegisterConstant<IHexDisplayService>(new HexDisplayServiceImpl());
        manager.RegisterConstant<ConsoleConnectionManager>(new ConsoleConnectionManagerImpl());
        manager.RegisterConstant<ITaskSequencerService>(new TaskSequencerServiceImpl());
        manager.RegisterConstant<MemoryEngineManager>(new MemoryEngineManagerImpl());

        ThemeManager.Instance.ActiveThemeChanged += OnActiveThemeChanged;
    }

    private static void OnActiveThemeChanged(ThemeManager manager, Theme oldTheme, Theme newTheme) {
        BasicApplicationConfiguration.Instance.DefaultTheme = newTheme.Name;
    }

    protected override async Task OnSetupApplication(IApplicationStartupProgress progress) {
        await base.OnSetupApplication(progress);
        this.PluginLoader.AddCorePlugin(typeof(PluginXbox360Xbdm));

        if (OperatingSystem.IsWindows()) {
            this.PluginLoader.AddCorePlugin(typeof(PluginXbox360XDevkit));
        }

        MemoryEngineBrushLoader.Init();
    }

    protected override void RegisterConfigurations() {
        base.RegisterConfigurations();
        this.PersistentStorageManager.Register<ThemeConfigurationOptions>(new ThemeConfigurationOptionsImpl(), "themes", "themes");
        this.PersistentStorageManager.Register(new BasicApplicationConfiguration(), null, "basic");
    }

    protected override Task OnApplicationFullyLoaded() {
        UserInputDialogView.Registry.RegisterType<SavedResultDataTypeUserInputInfo>(() => new SavedResultDataTypeEditorUserInputControl());
        UserInputDialogView.Registry.RegisterType<MemoryRegionUserInputInfo>(() => new XboxMemoryRegionViewerUIControl());
        ConfigurationPageRegistry.Registry.RegisterType<MemoryEngineConfigurationPage>(() => new MemoryEngineConfigurationPageControl());

        ApplicationConfigurationManager.Instance.RootEntry.AddEntry(new ConfigurationEntry() {
            DisplayName = "Memory Engine", Id = "config.memoryengine", Page = new MemoryEngineConfigurationPage()
        });

#if DEBUG
        if (Debugger.IsAttached) {
            OpenConnectionView.Registry.RegisterType<TestConnectionInfo>(() => new OpenTestConnectionView());
            
            ConsoleConnectionManager manager = Instance.ServiceManager.GetService<ConsoleConnectionManager>();
            manager.Register(ConnectionTypeDebugFile.TheID, ConnectionTypeDebugFile.Instance);
            manager.Register(ConnectionTypeTest.TheID, ConnectionTypeTest.Instance);
        }
#endif

        OpenConnectionView.Registry.RegisterType<OpenDebuggingFileInfo>(() => new OpenDebuggingFileView());

        Theme? theme;
        ThemeManager themeManager = this.ServiceManager.GetService<ThemeManager>();
        string defaultThemeName = BasicApplicationConfiguration.Instance.DefaultTheme;
        if (!string.IsNullOrWhiteSpace(defaultThemeName) && (theme = themeManager.GetTheme(defaultThemeName)) != null) {
            themeManager.SetTheme(theme);
        }

        return base.OnApplicationFullyLoaded();
    }

    protected override string? GetSolutionFileName() => "MemEngine.sln";

    public override string GetApplicationName() => "MemoryEngine360";

    private class IconPreferencesImpl : IIconPreferences {
        public bool UseAntiAliasing { get; set; } = true;
    }

    private class StartupManagerMemoryEngine360 : IStartupManager {
        public async Task OnApplicationStartupWithArgs(IApplicationStartupProgress progress, string[] args) {
            // IXboxManager xboxManager = new XboxManager();

            if (Design.IsDesignMode) {
                // Designer runtime does not have IClassicDesktopStyleApplicationLifetime, so
                // we don't have access to a windowing system, so the app would shut down.
                return;
            }
            
            await progress.ProgressAndSynchroniseAsync("Startup completed. Loading engine window...", 1.0);
            if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
                EngineWindow view = new EngineWindow();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                    (progress as SplashScreenWindow)?.Close();
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                }
                
                system.Register(view, true);
                view.Show();

                // using IDisposable? token = await view.MemoryEngine.BeginBusyOperationActivityAsync();
                // if (token != null) {
                //     using CancellationTokenSource cts = new CancellationTokenSource();
                //     IConsoleConnection? connection = await ConsoleTypeXbox360Xbdm.Instance.OpenConnection(view.MemoryEngine, new ConnectToXboxInfo(view.MemoryEngine) { IpAddress = "192.168.1.202" }, cts);
                //     if (connection != null) {
                //         view.MemoryEngine.SetConnection(token, connection, ConnectionChangeCause.Custom);
                //
                //         await ApplicationPFX.Instance.ServiceManager.GetService<IHexDisplayService>().ShowHexEditor(new HexDisplayInfo(view.MemoryEngine) {
                //             StartAddress = 0x8303A000,
                //             Length = 0x2000,
                //             AutoRefreshStartAddress = 0x8303A5C0,
                //             AutoRefreshLength = 1800,
                //         });
                //     }
                // }
            }
            else {
                Instance.Dispatcher.InvokeShutdown();
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