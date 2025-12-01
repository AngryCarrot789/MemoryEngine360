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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MemEngine360.BaseFrontEnd;
using MemEngine360.BaseFrontEnd.Connections;
using MemEngine360.BaseFrontEnd.Debugging;
using MemEngine360.BaseFrontEnd.EventViewing;
using MemEngine360.BaseFrontEnd.FileBrowsing;
using MemEngine360.BaseFrontEnd.FileConnections;
using MemEngine360.BaseFrontEnd.MemRegions;
using MemEngine360.BaseFrontEnd.ModTools;
using MemEngine360.BaseFrontEnd.PointerScanning;
using MemEngine360.BaseFrontEnd.Scripting;
using MemEngine360.BaseFrontEnd.Services;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
using MemEngine360.BaseFrontEnd.StructViewing;
using MemEngine360.BaseFrontEnd.TaskSequencing;
using MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;
using MemEngine360.BaseFrontEnd.Themes;
using MemEngine360.BaseFrontEnd.XboxBase;
using MemEngine360.Commands;
using MemEngine360.Commands.ATM;
using MemEngine360.Commands.ATM.XML;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Connections.Testing;
using MemEngine360.Engine;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Debugging.Commands;
using MemEngine360.Engine.FileBrowsing;
using MemEngine360.Engine.FileBrowsing.Commands;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Engine.StructViewing;
using MemEngine360.Engine.View;
using MemEngine360.ModTools;
using MemEngine360.ModTools.Commands;
using MemEngine360.PointerScanning;
using MemEngine360.PS3;
using MemEngine360.Scripting;
using MemEngine360.Scripting.Commands;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Commands;
using MemEngine360.Xbox360XBDM;
using MemEngine360.Xbox360XDevkit;
using MemEngine360.XboxBase;
using MemEngine360.XboxInfo;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia;
using PFXToolKitUI.Avalonia.Configurations.Pages;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop.Impl;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Overlays.Impl;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.UserInputs;
using PFXToolKitUI.Avalonia.Themes;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Configurations;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Services;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;
using SkiaSharp;

namespace MemEngine360.Avalonia;

public class MemoryEngineApplication : AvaloniaApplicationPFX {
    public MemoryEngineApplication(Application application) : base(application) {
    }

    static MemoryEngineApplication() {
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        if (Debugger.IsAttached) {
            Debug.Fail("Unobserved task exception");

            Instance.Dispatcher.Post(() => ExceptionDispatchInfo.Throw(e.Exception), DispatchPriority.Send);
        }
    }

    protected override void RegisterCommands(CommandManager manager) {
        base.RegisterCommands(manager);

        manager.Register("commands.pfx.ShowActivityListCommand", new ShowActivityListCommand());
        manager.Register("commands.memengine.OpenConsoleConnectionDialogCommand", new OpenConsoleConnectionDialogCommand());
        manager.Register("commands.memengine.OpenConsoleConnectionInSequencerCommand", new OpenConsoleConnectionInSequencerCommand());
        manager.Register("commands.memengine.FirstScanCommand", new FirstScanCommand());
        manager.Register("commands.memengine.NextScanCommand", new NextScanCommand());
        manager.Register("commands.memengine.ResetScanCommand", new ResetScanCommand());
        manager.Register("commands.memengine.CancelScanCommand", new CancelScanCommand());
        manager.Register("commands.memengine.ClearSavedAddressesCommand", new ClearSavedAddressesCommand());
        manager.Register("commands.memengine.AddSavedAddressCommand", new AddSavedAddressCommand());
        manager.Register("commands.memengine.EditScanResultValueCommand", new EditScanResultValueCommand());
        manager.Register("commands.memengine.CopyScanResultsToClipboardCommand", new CopyScanResultsToClipboardCommand());
        manager.Register("commands.memengine.CopyAbsoluteAddressToClipboardCommand", new CopyAbsoluteAddressToClipboardCommand());
        manager.Register("commands.memengine.CopyAddressToClipboardCommand", new CopyAddressToClipboardCommand());
        manager.Register("commands.memengine.CopySavedAddressValuesToClipboardCommand", new CopySavedAddressValuesToClipboardCommand());
        manager.Register("commands.memengine.EditSavedAddressValueCommand", new EditSavedAddressValueCommand());
        manager.Register("commands.memengine.EditSavedAddressDataTypeCommand", new EditSavedAddressDataTypeCommand());
        manager.Register("commands.memengine.EditSavedAddressDescriptionCommand", new EditSavedAddressDescriptionCommand());
        manager.Register("commands.memengine.EditSavedAddressAddressCommand", new EditSavedAddressAddressCommand());
        manager.Register("commands.memengine.RefreshSavedAddressesCommand", new RefreshSavedAddressesCommand());
        manager.Register("commands.memengine.AddSelectedScanResultsToSavedAddressListCommand", new AddSelectedScanResultsToSavedAddressListCommand());
        manager.Register("commands.memengine.DeleteSelectedSavedAddressesCommand", new DeleteSelectedSavedAddressesCommand());
        manager.Register("commands.memengine.DuplicateSelectedSavedAddressesCommand", new DuplicateSelectedSavedAddressesCommand());
        manager.Register("commands.memengine.DeleteSelectedScanResultsCommand", new DeleteSelectedScanResultsCommand());
        manager.Register("commands.memengine.SelectRangeFromMemoryRegionCommand", new SelectRangeFromMemoryRegionCommand());
        manager.Register("commands.memengine.ResetScanOptionsCommand", new ResetScanOptionsCommand());
        manager.Register("commands.memengine.ShowMemoryViewCommand", new ShowMemoryViewCommand());
        manager.Register("commands.memengine.ShowFileBrowserCommand", new ShowFileBrowserCommand());
        manager.Register("commands.memengine.LaunchFileCommand", new LaunchFileCommand());
        manager.Register("commands.memengine.RenameFileCommand", new RenameFileCommand());
        manager.Register("commands.memengine.DeleteFilesCommand", new DeleteFilesCommand());
        manager.Register("commands.memengine.CreateAbsoluteDirectoryCommand", new CreateAbsoluteDirectoryCommand());
        manager.Register("commands.memengine.CreateDirectoryInDirectoryCommand", new CreateDirectoryInDirectoryCommand());
        manager.Register("commands.memengine.MoveFileCommand", new MoveFileCommand());
        manager.Register("commands.memengine.DumpMemoryCommand", new DumpMemoryCommand());
        manager.Register("commands.memengine.GroupEntriesCommand", new GroupEntriesCommand());
        manager.Register("commands.memengine.OpenXMLFileCommand", new OpenXMLFileCommand());
        manager.Register("commands.memengine.SaveSavedAddressesToFileCommand", new SaveSavedAddressesToFileCommand());
        manager.Register("commands.memengine.ToggleSavedAddressAutoRefreshCommand", new ToggleSavedAddressAutoRefreshCommand());
        manager.Register("commands.memengine.ShowModulesCommand", new ShowModulesCommand());
        manager.Register("commands.memengine.ShowPointerScannerCommand", new ShowPointerScannerCommand());
        manager.Register("commands.memengine.ShowConsoleEventViewerCommand", new ShowConsoleEventViewerCommand());
        manager.Register("commands.moduleviewer.ShowModuleSectionInfoInDialogCommand", new ShowModuleSectionInfoInDialogCommand());

        // Remote commands
        manager.Register("commands.memengine.remote.ShowMemoryRegionsCommand", new ShowMemoryRegionsCommand());
        manager.Register("commands.memengine.remote.SoftRebootCommand", new SoftRebootCommand());
        manager.Register("commands.memengine.remote.ColdRebootCommand", new ColdRebootCommand());
        manager.Register("commands.memengine.remote.ShutdownCommand", new ShutdownCommand());
        manager.Register("commands.memengine.remote.DebugFreezeCommand", new DebugFreezeCommand());
        manager.Register("commands.memengine.remote.DebugUnfreezeCommand", new DebugUnfreezeCommand());
        manager.Register("commands.memengine.remote.GetCPUKeyCommand", new GetCPUKeyCommand());
        manager.Register("commands.memengine.remote.GetDashboardVersionCommand", new GetDashboardVersionCommand());
        manager.Register("commands.memengine.remote.GetTemperaturesCommand", new GetTemperaturesCommand());
        manager.Register("commands.memengine.remote.GetTitleIDCommand", new GetTitleIDCommand());
        manager.Register("commands.memengine.remote.GetMoBoTypeCommand", new GetMoBoTypeCommand());

        // Hex editor commands
        manager.Register("commands.hexeditor.ReloadSelectionFromConsole", new ReloadSelectionFromConsole());
        manager.Register("commands.hexeditor.UploadSelectionToConsoleCommand", new UploadSelectionToConsoleCommand());
        manager.Register("commands.hexeditor.GotoAddressCommand", new GotoAddressCommand());
        manager.Register("commands.hexeditor.SetAutoScanRangeAsSelectionCommand", new SetAutoScanRangeAsSelectionCommand());
        manager.Register("commands.hexeditor.ClearAutoScanRangeCommand", new ClearAutoScanRangeCommand());
        manager.Register("commands.hexeditor.SaveSelectionAsFileCommand", new SaveSelectionAsFileCommand());

        // Sequencer
        manager.Register("commands.memengine.ShowTaskSequencerCommand", new ShowTaskSequencerCommand());
        manager.Register("commands.sequencer.DeleteSequenceSelectionCommand", new DeleteSequenceSelectionCommand());
        manager.Register("commands.sequencer.DeleteOperationSelectionCommand", new DeleteOperationSelectionCommand());
        manager.Register("commands.sequencer.DeleteConditionSelectionCommand", new DeleteConditionSelectionCommand());
        manager.Register("commands.sequencer.ConnectToDedicatedConsoleCommand", new ConnectToDedicatedConsoleCommand());
        manager.Register("commands.sequencer.ClearSequencesCommand", new ClearSequencesCommand());
        manager.Register("commands.sequencer.NewSequenceCommand", new NewSequenceCommand());
        manager.Register("commands.sequencer.DuplicateSequenceCommand", new DuplicateSequencesCommand());
        manager.Register("commands.sequencer.DuplicateOperationsCommand", new DuplicateOperationsCommand());
        manager.Register("commands.sequencer.DuplicateConditionsCommand", new DuplicateConditionsCommand());
        manager.Register("commands.sequencer.EditConditionOutputModeCommand", new EditConditionOutputModeCommand());
        manager.Register("commands.sequencer.RenameSequenceCommand", new RenameSequenceCommand());
        manager.Register("commands.sequencer.StopSpecificSequenceCommand", new StopSpecificSequenceCommand());
        manager.Register("commands.sequencer.StopSelectedSequencesCommand", new StopSelectedSequencesCommand());
        manager.Register("commands.sequencer.RunSequenceCommand", new RunSequenceCommand());
        manager.Register("commands.sequencer.ToggleOperationEnabledCommand", new ToggleOperationEnabledCommand());
        manager.Register("commands.sequencer.ToggleOperationConditionBehaviourCommand", new ToggleOperationConditionBehaviourCommand());
        manager.Register("commands.sequencer.ToggleConditionEnabledCommand", new ToggleConditionEnabledCommand());
        manager.Register("commands.sequencer.SaveTaskSequencesToFileCommand", new SaveTaskSequencesToFileCommand());
        manager.Register("commands.sequencer.OpenTaskSequencesFromFileCommand", new OpenTaskSequencesFromFileCommand());

        // Debugger
        manager.Register("commands.memengine.ShowDebuggerCommand", new ShowDebuggerCommand());
        manager.Register("commands.debugger.OpenDebuggerConnectionCommand", new OpenDebuggerConnectionCommand());
        manager.Register("commands.debugger.RefreshRegistersCommand", new RefreshRegistersCommand());
        manager.Register("commands.debugger.RefreshAllCommand", new RefreshAllCommand());
        manager.Register("commands.debugger.DebugStepCommand", new DebugStepCommand());
        manager.Register("commands.debugger.FreezeConsoleCommand", new FreezeConsoleCommand());
        manager.Register("commands.debugger.UnfreezeConsoleCommand", new UnfreezeConsoleCommand());
        manager.Register("commands.debugger.SuspendThreadCommand", new SuspendThreadCommand());
        manager.Register("commands.debugger.ResumeThreadCommand", new ResumeThreadCommand());
        manager.Register("commands.debugger.GoToDebugMemoryCommand", new GoToDebugMemoryCommand());

        // scripting
        manager.Register("commands.scripting.ShowScriptingWindowCommand", new ShowScriptingWindowCommand());
        manager.Register("commands.scripting.AddNewScriptCommand", new AddNewScriptCommand());
        manager.Register("commands.scripting.RenameScriptCommand", new RenameScriptCommand());
        manager.Register("commands.scripting.RunScriptCommand", new RunScriptCommand());
        manager.Register("commands.scripting.StopScriptCommand", new StopScriptCommand());
        manager.Register("commands.scripting.CloseScriptCommand", new CloseScriptCommand());
        manager.Register("commands.scripting.SaveScriptCommand", new SaveScriptCommand(false));
        manager.Register("commands.scripting.SaveScriptAsCommand", new SaveScriptCommand(true));
        manager.Register("commands.scripting.SaveAllScriptsCommand", new SaveAllScriptsCommand());
        manager.Register("commands.scripting.ConnectScriptToConsoleCommand", new ConnectScriptToConsoleCommand());
        manager.Register("commands.scripting.OpenScriptFileCommand", new OpenScriptFileCommand());
        manager.Register("commands.scripting.CopyScriptFilePathCommand", new CopyScriptFilePathCommand());

        // struct viewer
        manager.Register("commands.structviewer.ShowStructViewerWindowCommand", new ShowStructViewerWindowCommand());

        // mod tools
        manager.Register("commands.modtools.ShowModToolsWindowCommand", new ShowModToolsWindowCommand());
        manager.Register("commands.modtools.CreateModToolCommand", new CreateModToolCommand());
        manager.Register("commands.modtools.ConnectModToolToConsoleCommand", new ConnectModToolToConsoleCommand());
        manager.Register("commands.modtools.OpenModToolScriptFileCommand", new OpenModToolScriptFileCommand());
        manager.Register("commands.modtools.RestartModToolCommand", new RestartModToolCommand());
        manager.Register("commands.modtools.SaveModToolCommand", new SaveModToolCommand(false));
        manager.Register("commands.modtools.SaveModToolAsCommand", new SaveModToolCommand(true));
        manager.Register("commands.modtools.SaveAllModToolsCommand", new SaveAllModToolsCommand());
        manager.Register("commands.modtools.CloseModToolCommand", new CloseModToolCommand());
        manager.Register("commands.modtools.RenameModToolCommand", new RenameModToolCommand());
        manager.Register("commands.modtools.CopyModToolFilePathCommand", new CopyModToolFilePathCommand());

        // History
        // manager.Register("commands.application.UndoCommand", new UndoCommand());
        // manager.Register("commands.application.RedoCommand", new RedoCommand());
    }

    protected override void RegisterComponents(ComponentStorage manager) {
        if (this.Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime) {
            manager.AddComponent<IDesktopService>(new DesktopServiceImpl(this.Application));

            DesktopWindowManager dwm = new DesktopWindowManager(new Uri("avares://MemoryEngine360/Icons/icon-16.bmp", UriKind.RelativeOrAbsolute));
            manager.AddComponent<IWindowManager>(dwm);
            manager.AddComponent<ITopLevelManager>(dwm);
            manager.AddComponent<IForegroundActivityService>(new DesktopForegroundActivityServiceImpl());
        }

        base.RegisterComponents(manager);

        manager.AddComponent<IIconPreferences>(new IconPreferencesImpl());
        manager.AddComponent<IStartupManager>(new StartupManagerMemoryEngine360());
        manager.AddComponent<IAboutService>(new AboutServiceImpl());
        manager.AddComponent<IMemoryViewerViewService>(new MemoryViewerViewServiceImpl());
        manager.AddComponent<ConsoleConnectionManager>(new ConsoleConnectionManagerImpl());
        manager.AddComponent<ITaskSequencerService>(new TaskSequencerServiceImpl());
        manager.AddComponent<MemoryEngineManager>(new MemoryEngineManagerImpl());
        manager.AddComponent<IEditConditionOutputModeService>(new EditConditionOutputModeServiceImpl());
        manager.AddComponent<IPointerScanService>(new PointerScanServiceImpl());
        manager.AddComponent<IConsoleEventViewerService>(new ConsoleEventViewerServiceImpl());
        manager.AddComponent<IDebuggerViewService>(new DebuggerViewServiceImpl());
        manager.AddComponent<IFileBrowserService>(new FileBrowserServiceImpl());
        manager.AddComponent<IScriptingViewService>(new DesktopScriptingViewServiceImpl());
        manager.AddComponent<IStructViewerService>(new DesktopStructViewerServiceImpl());
        manager.AddComponent<IModToolViewService>(new DesktopModToolViewServiceImpl());
        manager.AddComponent<ILuaScriptDocumentService>(new LuaScriptDocumentServiceImpl());

        ThemeManager.Instance.ActiveThemeChanged += OnActiveThemeChanged;
    }

    private static void OnActiveThemeChanged(object? sender, ValueChangedEventArgs<Theme> e) {
        BasicApplicationConfiguration.Instance.DefaultTheme = e.NewValue.Name;
    }

    protected override async Task OnSetupApplication(IApplicationStartupProgress progress) {
        await base.OnSetupApplication(progress);
        bool isWindows = OperatingSystem.IsWindows();
        this.PluginLoader.AddCorePlugin(typeof(PluginXbox360Xbdm));

        if (isWindows) {
            this.PluginLoader.AddCorePlugin(typeof(PluginXbox360XDevkit));
            this.PluginLoader.AddCorePlugin(typeof(PluginPS3));
        }

        MemoryEngineBrushLoader.Init();
        ThemeManagerImpl manager = (ThemeManagerImpl) this.ComponentStorage.GetComponent<ThemeManager>();
        Theme darkTheme = manager.GetThemeByVariant(ThemeVariant.Dark)!;

        Theme redTheme = manager.RegisterTheme("Red Theme (Built In)", darkTheme, false);
        redTheme.SetThemeColour("ABrush.Tone1.Background.Static", SKColor.Parse("ff181818"));
        redTheme.SetThemeColour("ABrush.Tone2.Background.Static", SKColor.Parse("ff2e0000"));
        redTheme.SetThemeColour("ABrush.Tone3.Background.Static", SKColor.Parse("ff161616"));
        redTheme.SetThemeColour("ABrush.Tone4.Background.Static", SKColor.Parse("ff200000"));
        redTheme.SetThemeColour("ABrush.Tone1.Border.Static", SKColor.Parse("ffff0000"));
        redTheme.SetThemeColour("ABrush.Tone2.Border.Static", SKColor.Parse("ffff0000"));
        redTheme.SetThemeColour("ABrush.Tone3.Border.Static", SKColor.Parse("ffff0000"));
        redTheme.SetThemeColour("ABrush.Tone0.Background.Static", SKColor.Parse("ff141414"));
        redTheme.SetThemeColour("ABrush.Tone5.Background.Static", SKColor.Parse("ff2d0000"));
        redTheme.SetThemeColour("ABrush.Tone5.Border.Static", SKColor.Parse("ffff0000"));
        redTheme.SetThemeColour("ABrush.Tone4.Border.Static", SKColor.Parse("ff810000"));
        redTheme.SetThemeColour("ABrush.Tone6.Background.Static", SKColor.Parse("ffb60000"));
        redTheme.SetThemeColour("ABrush.Tone6.Border.Static", SKColor.Parse("ffb80000"));

        Theme hcTheme = manager.RegisterTheme("High Contrast (Built In)", darkTheme, false);
        hcTheme.SetThemeColour("ABrush.Foreground.Static", SKColor.Parse("ffededed"));
        hcTheme.SetThemeColour("ABrush.Tone0.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone1.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone2.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone1.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone0.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone2.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone3.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone3.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone4.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone4.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone5.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone5.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone6.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone6.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone7.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone7.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.Tone8.Background.Static", SKColors.Black);
        hcTheme.SetThemeColour("ABrush.Tone8.Border.Static", SKColors.White);
        hcTheme.SetThemeColour("ABrush.PFX.StatusBar.Background", SKColors.Black);
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

        ConsoleConnectionManager manager = this.ComponentStorage.GetComponent<ConsoleConnectionManager>();
        manager.Register(ConnectionTypeBinaryFile.TheID, ConnectionTypeBinaryFile.Instance);

#if DEBUG
        if (Debugger.IsAttached) {
            OpenConnectionView.Registry.RegisterType<TestConnectionInfo>(() => new OpenTestConnectionView());

            manager.Register(ConnectionTypeTest.TheID, ConnectionTypeTest.Instance);
        }
#endif

        OpenConnectionView.Registry.RegisterType<OpenBinaryFileInfo>(() => new OpenBinaryFileView());

        Theme? theme;
        ThemeManager themeManager = this.ComponentStorage.GetComponent<ThemeManager>();
        string defaultThemeName = BasicApplicationConfiguration.Instance.DefaultTheme;
        if (!string.IsNullOrWhiteSpace(defaultThemeName) && (theme = themeManager.GetTheme(defaultThemeName)) != null) {
            themeManager.SetTheme(theme);
        }

        TaskSequenceSerializer.RegisterTaskSequencerSerialization();
        return base.OnApplicationFullyLoaded();
    }

    protected override string? GetSolutionFileName() => "MemEngine360.sln";

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

            await progress.ProgressAndWaitForRender("Startup completed. Loading engine window...", 1.0);
            if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
                IDesktopService.TryGetInstance(out IDesktopService? desktop);
                if (desktop != null)
                    desktop.ApplicationLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                (progress as SplashScreenWindow)?.Close();

                if (desktop != null)
                    desktop.ApplicationLifetime.ShutdownMode = ShutdownMode.OnMainWindowClose;

                IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                    Title = "Memory Engine 360 v1.2.1",
                    FocusPath = "EngineWindow",
                    Content = new OverlayContentHostRoot() {
                        Content = new EngineView()
                    },
                    MinWidth = 600, MinHeight = 520,
                    Width = 680, Height = 630,
                    // rely on default icon for the DesktopWindowManager
                    // Icon = new WindowIcon(new Uri("avares://MemoryEngine360/Icons/icon-16.bmp", UriKind.RelativeOrAbsolute)),
                    TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.MainView.TitleBarBackground"),
                    BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                    MainWindow = true
                });

                MemoryEngine engine = ((EngineView) ((OverlayContentHostRoot) window.Content!).Content!).MemoryEngine;

                // Instance.ComponentStorage.AddComponent<IOverlayWindowManager>(new OverlayWindowManagerImpl((OverlayContentHostRoot) window.Content!));

                window.Opened += static (s, e) => {
                    EngineView view = (EngineView) ((OverlayContentHostRoot) ((IDesktopWindow) s!).Content!).Content!;
                    view.MemoryEngine.UserContext.Set(ITopLevel.TopLevelDataKey, (IDesktopWindow) s!);
                    MemoryEngineViewState.GetInstance(view.MemoryEngine).IsActivityListVisible = false;
                    DataManager.GetContextData(((IDesktopWindow) s!).Control).Set(MemoryEngine.EngineDataKey, view.MemoryEngine);

                    ((MemoryEngineManagerImpl) GetComponent<MemoryEngineManager>()).OnEngineOpened(view.MemoryEngine);
                };

                window.ClosingAsync += static (s, e) => {
                    return Instance.Dispatcher.InvokeAsync(() => {
                        return CommandManager.Instance.RunActionAsync(_ => OnEngineWindowAboutToClose((IDesktopWindow) s!), ((IDesktopWindow) s!).LocalContextData);
                    }).Unwrap();
                };

                window.Closed += static (s, e) => {
                    EngineView view = (EngineView) ((OverlayContentHostRoot) ((IDesktopWindow) s!).Content!).Content!;
                    view.MemoryEngine.UserContext.Remove(ITopLevel.TopLevelDataKey);
                    ((MemoryEngineManagerImpl) GetComponent<MemoryEngineManager>()).OnEngineClosed(view.MemoryEngine);
                    DataManager.GetContextData(((IDesktopWindow) s!).Control).Remove(MemoryEngine.EngineDataKey);
                };

                await window.ShowAsync();

                _ = ActivityManager.Instance.RunTask(async () => {
                    ActivityTask activity = ActivityTask.Current;
                    activity.Progress.SetCaptionAndText("Reload last mod tools");
                    foreach (string path in BasicApplicationConfiguration.Instance.LoadedModToolPaths) {
                        activity.CancellationToken.ThrowIfCancellationRequested();
                        try {
                            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".lua") || !File.Exists(path)) {
                                continue;
                            }
                        }
                        catch {
                            continue; // Not sure if File.Exists() throws for invalid paths
                        }

                        string text;
                        try {
                            text = await File.ReadAllTextAsync(path, activity.CancellationToken);
                        }
                        catch (Exception e) {
                            AppLogger.Instance.WriteLine("Failed to reload mod tool file from config: " + e.GetToString());
                            continue; // ignored
                        }

                        Instance.Dispatcher.Post(() => {
                            ModTool script = new ModTool() {
                                Document = { Text = text },
                                HasUnsavedChanges = false
                            };

                            script.SetFilePath(path);
                            engine.ModToolManager.AddModTool(script);
                        }, DispatchPriority.Background);
                    }
                }, true);

                // using CancellationTokenSource taskCts1 = new CancellationTokenSource();
                // using CancellationTokenSource taskCts2 = new CancellationTokenSource();
                // using CancellationTokenSource taskCts3 = new CancellationTokenSource();
                //
                // ActivityTask task1 = await ActivityManager.Instance.RunTask(() => RunTask("Task 1", "My cool task 1"), taskCts1).GetRunningAwaitable();
                // ActivityTask task2 = await ActivityManager.Instance.RunTask(() => RunTask("Task 2", "My cool task 2"), taskCts2).GetRunningAwaitable();
                // ActivityTask task3 = await ActivityManager.Instance.RunTask(() => RunTask("Task 3", "My cool task 3"), taskCts3).GetRunningAwaitable();
                // ActivityTask task4 = await ActivityManager.Instance.RunTask(() => RunTask("Task 4", "My cool task 4")).GetRunningAwaitable();
                // ActivityTask task5 = await ActivityManager.Instance.RunTask(() => RunTask("Task 5", "My cool task 5")).GetRunningAwaitable();
                //
                // await Task.Delay(2000);
                //
                // if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? foreground)) {
                //     using CancellationTokenSource cts3 = new CancellationTokenSource(2000);
                //     await foreground.WaitForActivity(window, task3, cts3.Token);
                //  
                //     using CancellationTokenSource cts1 = new CancellationTokenSource(2000);
                //     await foreground.WaitForActivity(window, task1, cts1.Token);
                //     
                //     using CancellationTokenSource cts5 = new CancellationTokenSource(2000);
                //     await foreground.WaitForActivity(window, task5, cts5.Token);
                // }
                //
                // // if (IForegroundActivityService.TryGetInstance(out IForegroundActivityService? foreground)) {
                // //     using CancellationTokenSource cts = new CancellationTokenSource(3000);
                // //     await foreground.WaitForSubActivities(window, new[] { task1, task3, task5 }.Select(SubActivity.FromActivity), cts.Token);
                // // }
                //
                // return;
                //
                // static Task RunTask(string caption, string desc) {
                //     ActivityTask task = ActivityTask.Current;
                //     task.Progress.Caption = caption;
                //     task.Progress.Text = desc;
                //     task.Progress.IsIndeterminate = true;
                //     return Task.Delay(12000, task.CancellationToken);
                // }
            }
            else {
                Instance.Dispatcher.Shutdown();
            }
        }

        private static async Task OnEngineWindowAboutToClose(IDesktopWindow window) {
            EngineView view = (EngineView) ((OverlayContentHostRoot) window.Content!).Content!;
            MemoryEngine engine = view.MemoryEngine;

            {
                List<string> pathsToSave = new List<string>();
                foreach (Script script in engine.ScriptingManager.Scripts) {
                    if (!string.IsNullOrWhiteSpace(script.FilePath) && File.Exists(script.FilePath)) {
                        pathsToSave.Add(script.FilePath);
                    }
                }

                BasicApplicationConfiguration.Instance.LoadedScriptPaths = pathsToSave.ToArray();
            }

            {
                List<string> pathsToSave = new List<string>();
                foreach (ModTool tool in engine.ModToolManager.ModTools) {
                    if (!string.IsNullOrWhiteSpace(tool.FilePath) && File.Exists(tool.FilePath)) {
                        pathsToSave.Add(tool.FilePath);
                    }
                }

                BasicApplicationConfiguration.Instance.LoadedModToolPaths = pathsToSave.ToArray();
            }

            engine.IsShuttingDown = true;
            ulong frame = engine.GetNextConnectionChangeFrame();
            await engine.BroadcastConnectionAboutToChange(window, frame);

            List<ActivityTask> tasks = ActivityManager.Instance.ActiveTasks.ToList();
            foreach (ActivityTask task in tasks) {
                task.TryCancel();
            }

            if (engine.ScanningProcessor.IsScanning) {
                ActivityTask? activity = engine.ScanningProcessor.ScanningActivity;
                if (activity != null && activity.TryCancel()) {
                    await activity;
                }

                Debug.Assert(!engine.ScanningProcessor.IsScanning);
            }

            using (CancellationTokenSource cts = new CancellationTokenSource()) {
                // Grace period for all activities to become cancelled
                try {
                    await Task.WhenAny(Task.Delay(1000, cts.Token), Task.Run(() => Task.WhenAll(tasks.Select(x => x.Task)), cts.Token));
                    await cts.CancelAsync();
                }
                catch (OperationCanceledException) {
                    // ignored
                }
            }

            IBusyToken? token = await engine.BeginBusyOperationAsync(500);
            while (token == null) {
                MessageBoxInfo info = new MessageBoxInfo() {
                    Caption = "Engine busy",
                    Message = $"Cannot close window yet because the engine is still busy and cannot be shutdown safely.{Environment.NewLine}" + "What do you want to do?",
                    Buttons = MessageBoxButtons.YesNo,
                    DefaultButton = MessageBoxResult.Yes,
                    YesOkText = "Wait for operations",
                    NoText = "Force Close"
                };

                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
                if (result != MessageBoxResult.Yes /* Yes == wait for ops */) {
                    return; // force close - let tcp things timeout
                }

                token = await TryGetTokenWithForegroundDialog(window, engine.BusyLock);
            }

            IConsoleConnection? connection = engine.Connection;
            try {
                if (connection != null) {
                    engine.SetConnection(token, frame, null, ConnectionChangeCause.User);
                    connection.Close();
                }
            }
            catch {
                // ignored
            }
            finally {
                token.Dispose();
            }

            IBusyToken? debuggerToken = await engine.ConsoleDebugger.BusyLock.BeginBusyOperation(1000);
            while (debuggerToken == null) {
                MessageBoxInfo info = new MessageBoxInfo() {
                    Caption = "Debugger busy",
                    Message = $"Cannot close window yet because the debugger is still busy and cannot be shutdown safely.{Environment.NewLine}" + "What do you want to do?",
                    Buttons = MessageBoxButtons.YesNo,
                    DefaultButton = MessageBoxResult.Yes,
                    YesOkText = "Wait for operations",
                    NoText = "Force Close"
                };

                MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
                if (result != MessageBoxResult.Yes /* Yes == wait for ops */) {
                    return; // force close - let tcp things timeout
                }

                debuggerToken = await TryGetTokenWithForegroundDialog(window, engine.ConsoleDebugger.BusyLock);
            }

            IConsoleConnection? debugConnection = engine.ConsoleDebugger.Connection;
            try {
                if (debugConnection != null) {
                    engine.ConsoleDebugger.SetConnection(debuggerToken, null);
                    debugConnection.Close();
                }
            }
            catch {
                // ignored
            }
            finally {
                debuggerToken.Dispose();
            }
        }

        private static async Task<IBusyToken?> TryGetTokenWithForegroundDialog(IDesktopWindow window, BusyLock busyLock) {
            IBusyToken? token;
            if ((token = busyLock.TryBeginBusyOperation()) == null) {
                token = await busyLock.BeginBusyOperationUsingActivity(new BusyTokenRequestUsingActivity() {
                    Progress = {
                        Caption = "Safely disconnect",
                        Text = BusyLock.WaitingMessage,
                    },
                    ForegroundInfo = new InForegroundInfo(window)
                });
            }

            return token;
        }
    }

    private class AboutServiceImpl : IAboutService {
        public Task ShowDialog() {
            ITopLevel? topLevel = TopLevelContextUtils.GetTopLevelFromContext();
            if (topLevel != null) {
                if (WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out IDesktopWindow? parentWindow)) {
                    IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                        Title = "About MemoryEngine360",
                        Content = new AboutView(),
                        TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone7.Background.Static"),
                        BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                        MinWidth = 500, MinHeight = 200,
                        Width = 600, Height = 250,
                        Parent = parentWindow
                    });

                    return window.ShowAsync();
                }
            }

            return Task.CompletedTask;
        }
    }
}