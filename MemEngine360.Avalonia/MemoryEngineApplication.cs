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
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MemEngine360.BaseFrontEnd;
using MemEngine360.BaseFrontEnd.Debugging;
using MemEngine360.BaseFrontEnd.EventViewing;
using MemEngine360.BaseFrontEnd.FileBrowsing;
using MemEngine360.BaseFrontEnd.FileConnections;
using MemEngine360.BaseFrontEnd.MemRegions;
using MemEngine360.BaseFrontEnd.PointerScanning;
using MemEngine360.BaseFrontEnd.Scripting;
using MemEngine360.BaseFrontEnd.Services;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.BaseFrontEnd.Services.HexEditing;
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
using MemEngine360.Engine.Addressing;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.Debugging.Commands;
using MemEngine360.Engine.FileBrowsing;
using MemEngine360.Engine.FileBrowsing.Commands;
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.View;
using MemEngine360.PointerScanning;
using MemEngine360.Scripting;
using MemEngine360.Scripting.Commands;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Commands;
using MemEngine360.Sequencing.Conditions;
using MemEngine360.Sequencing.DataProviders;
using MemEngine360.Sequencing.Operations;
using MemEngine360.ValueAbstraction;
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
using PFXToolKitUI.Services;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using SkiaSharp;

namespace MemEngine360.Avalonia;

public class MemoryEngineApplication : AvaloniaApplicationPFX {
    public MemoryEngineApplication(Application application) : base(application) {
    }

    static MemoryEngineApplication() {
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        Instance.Dispatcher.Post(() => ExceptionDispatchInfo.Throw(e.Exception), DispatchPriority.Send);
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
        manager.Register("commands.memengine.remote.TestRPCCommand", new TestRPCCommand());

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

        ThemeManager.Instance.ActiveThemeChanged += OnActiveThemeChanged;
    }

    private static void OnActiveThemeChanged(ThemeManager manager, Theme oldTheme, Theme newTheme) {
        BasicApplicationConfiguration.Instance.DefaultTheme = newTheme.Name;
    }

    protected override async Task OnSetupApplication(IApplicationStartupProgress progress) {
        await base.OnSetupApplication(progress);
        bool isWindows = OperatingSystem.IsWindows();
        this.PluginLoader.AddCorePlugin(typeof(PluginXbox360Xbdm));

        if (isWindows)
            this.PluginLoader.AddCorePlugin(typeof(PluginXbox360XDevkit));

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

        RegisterTaskSequencerSerialization();
        return base.OnApplicationFullyLoaded();
    }

    private static void RegisterTaskSequencerSerialization() {
        XmlTaskSequenceSerialization.RegisterCondition("CompareMemory", typeof(CompareMemoryCondition), (document, element, _cond) => {
            CompareMemoryCondition condition = (CompareMemoryCondition) _cond;
            element.SetAttribute("Address", condition.Address.ToString());
            element.SetAttribute("CompareType", condition.CompareType.ToString());
            element.SetAttribute("ParseIntInputAsHex", condition.ParseIntAsHex ? "true" : "false");
            if (condition.CompareTo != null) {
                XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) element.AppendChild(document.CreateElement("CompareTo"))!, condition.CompareTo);
            }
        }, (element, _cond) => {
            CompareMemoryCondition condition = (CompareMemoryCondition) _cond;
            if (!MemoryAddressUtils.TryParse(element.GetAttribute("Address"), out IMemoryAddress? address, out string? error))
                throw new Exception("Invalid address' " + element.GetAttribute("Address") + "': " + error!);
            if (!Enum.TryParse(element.GetAttribute("CompareType"), true, out CompareType compareType))
                throw new Exception("Invalid CompareType: " + element.GetAttribute("CompareType"));
            if (!bool.TryParse(element.GetAttribute("ParseIntInputAsHex"), out bool parseIntAsHex))
                throw new Exception("Invalid bool for ParseIntInputAsHex: " + element.GetAttribute("ParseIntInputAsHex"));

            condition.Address = address;
            condition.CompareType = compareType;
            condition.ParseIntAsHex = parseIntAsHex;
            if (element.GetElementsByTagName("CompareTo").OfType<XmlElement>().FirstOrDefault() is XmlElement dataTypeElement)
                condition.CompareTo = XmlTaskSequenceSerialization.DeserializeDataValue(dataTypeElement);
        });

        XmlTaskSequenceSerialization.RegisterOperation("Delay", typeof(DelayOperation), (document, element, _op) => {
            DelayOperation op = (DelayOperation) _op;
            element.SetAttribute("Delay", TimeSpanUtils.ConvertToString(op.Delay));
        }, (element, _op) => {
            DelayOperation op = (DelayOperation) _op;
            if (!TimeSpanUtils.TryParseTime(element.GetAttribute("Delay"), out TimeSpan delay, out string? errorMessage))
                throw new Exception($"Invalid delay value '{element.GetAttribute("Delay")}'. " + errorMessage);
            op.Delay = delay;
        });

        XmlTaskSequenceSerialization.RegisterOperation("JumpTo", typeof(JumpToLabelOperation), (document, element, _op) => {
            JumpToLabelOperation op = (JumpToLabelOperation) _op;
            element.SetAttribute("TargetName", op.CurrentTarget?.LabelName);
        }, (element, _op) => {
            JumpToLabelOperation op = (JumpToLabelOperation) _op;
            string name = element.GetAttribute("TargetName");
            // targets will be updated once all operations are fully deserialized
            op.SetTarget(string.IsNullOrWhiteSpace(name) ? null : name, null);
        });

        XmlTaskSequenceSerialization.RegisterOperation("Label", typeof(LabelOperation), (document, element, _op) => {
            LabelOperation op = (LabelOperation) _op;
            element.SetAttribute("Name", op.LabelName);
        }, (element, _op) => {
            LabelOperation op = (LabelOperation) _op;
            string name = element.GetAttribute("Name");
            op.LabelName = string.IsNullOrWhiteSpace(name) ? null : name;
        });

        XmlTaskSequenceSerialization.RegisterOperation("StopSequence", typeof(StopSequenceOperation), (document, element, _op) => {
        }, (element, _op) => {
        });

        XmlTaskSequenceSerialization.RegisterOperation("SetMemory", typeof(SetMemoryOperation), (document, element, _op) => {
            SetMemoryOperation op = (SetMemoryOperation) _op;
            element.SetAttribute("Address", op.Address.ToString());
            element.SetAttribute("IterateCount", op.IterateCount.ToString());
            element.SetAttribute("WriteMode", op.WriteMode.ToString());
            if (op.DataValueProvider is ConstantDataProvider constProvider) {
                XmlElement providerElement = (XmlElement) element.AppendChild(document.CreateElement("ConstantProvider"))!;
                if (constProvider.DataValue is IDataValue value)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("Value"))!, value);
                providerElement.SetAttribute("DataType", constProvider.DataType.ToString());
                providerElement.SetAttribute("ParseIntInputAsHex", constProvider.ParseIntAsHex ? "true" : "false");
                providerElement.SetAttribute("AppendNullCharToString", constProvider.AppendNullCharToString ? "true" : "false");
            }
            else if (op.DataValueProvider is RandomNumberDataProvider randomProvider) {
                XmlElement providerElement = (XmlElement) element.AppendChild(document.CreateElement("RandomProvider"))!;
                if (randomProvider.Minimum is IDataValue minVal)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("MinNumber"))!, minVal);
                if (randomProvider.Maximum is IDataValue maxVal)
                    XmlTaskSequenceSerialization.SerializeDataValue((XmlElement) providerElement.AppendChild(document.CreateElement("MaxNumber"))!, maxVal);
                providerElement.SetAttribute("DataType", randomProvider.DataType.ToString());
                providerElement.SetAttribute("ParseIntInputAsHex", randomProvider.ParseIntAsHex ? "true" : "false");
                providerElement.SetAttribute("AppendNullCharToString", randomProvider.AppendNullCharToString ? "true" : "false");
            }
        }, (element, _op) => {
            SetMemoryOperation op = (SetMemoryOperation) _op;
            if (!MemoryAddressUtils.TryParse(GetRequiredAttribute(element, "Address", false), out IMemoryAddress? addr, out string? errMsg))
                throw new Exception("Invalid memory address. " + errMsg);

            op.Address = addr;
            op.IterateCount = GetOptionalAttribute(element, "IterateCount", uint.Parse, op.IterateCount);
            op.WriteMode = GetOptionalAttribute(element, "WriteMode", s => Enum.Parse<SetMemoryWriteMode>(s, true), op.WriteMode);

            if (element.GetElementsByTagName("ConstantProvider").OfType<XmlElement>().FirstOrDefault() is XmlElement constElement) {
                ConstantDataProvider constProvider = (ConstantDataProvider) (op.DataValueProvider = new ConstantDataProvider());
                constProvider.DataType = GetRequiredAttribute(constElement, "DataType", s => Enum.Parse<DataType>(s, true));
                constProvider.AppendNullCharToString = GetOptionalAttribute(constElement, "AppendNullCharToString", bool.Parse, constProvider.AppendNullCharToString);
                constProvider.ParseIntAsHex = GetOptionalAttribute(constElement, "ParseIntInputAsHex", bool.Parse, constProvider.ParseIntAsHex);
                if (constElement.GetElementsByTagName("Value").OfType<XmlElement>().FirstOrDefault() is XmlElement valueElement)
                    constProvider.DataValue = XmlTaskSequenceSerialization.DeserializeDataValue(valueElement);
            }
            else if (element.GetElementsByTagName("RandomProvider").OfType<XmlElement>().FirstOrDefault() is XmlElement randElement) {
                RandomNumberDataProvider provider = (RandomNumberDataProvider) (op.DataValueProvider = new RandomNumberDataProvider());
                provider.DataType = GetRequiredAttribute(randElement, "DataType", s => Enum.Parse<DataType>(s, true));
                provider.AppendNullCharToString = GetOptionalAttribute(randElement, "AppendNullCharToString", bool.Parse, provider.AppendNullCharToString);
                provider.ParseIntAsHex = GetOptionalAttribute(randElement, "ParseIntInputAsHex", bool.Parse, provider.ParseIntAsHex);
                if (randElement.GetElementsByTagName("MinNumber").OfType<XmlElement>().FirstOrDefault() is XmlElement minElement)
                    provider.Minimum = (DataValueNumeric) XmlTaskSequenceSerialization.DeserializeDataValue(minElement);
                if (randElement.GetElementsByTagName("MaxNumber").OfType<XmlElement>().FirstOrDefault() is XmlElement maxElement)
                    provider.Maximum = (DataValueNumeric) XmlTaskSequenceSerialization.DeserializeDataValue(maxElement);
            }
        });
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
                    Title = "Memory Engine 360 v1.1.8",
                    FocusPath = "EngineWindow",
                    Content = new OverlayContentHostRoot() {
                        Content = new EngineView()
                    },
                    MinWidth = 600, MinHeight = 520,
                    Width = 680, Height = 630,
                    // rely on default icon for the DesktopWindowManager
                    // Icon = new WindowIcon(new Uri("avares://MemoryEngine360/Icons/icon-16.bmp", UriKind.RelativeOrAbsolute)),
                    TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.MainView.TitleBarBackground"),
                    BorderBrush = BrushManager.Instance.GetDynamicThemeBrush("PanelBorderBrush"),
                    MainWindow = true
                });

                // Instance.ComponentStorage.AddComponent<IOverlayWindowManager>(new OverlayWindowManagerImpl((PopupOverlayContentHost) window.Content!));

                window.Opened += static (s, e) => {
                    EngineView view = (EngineView) ((OverlayContentHostRoot) s.Content!).Content!;
                    MemoryEngineViewState.GetInstance(view.MemoryEngine).IsActivityListVisible = false;
                    DataManager.GetContextData(s.Control).Set(MemoryEngine.EngineDataKey, view.MemoryEngine);

                    ((MemoryEngineManagerImpl) GetComponent<MemoryEngineManager>()).OnEngineOpened(view.MemoryEngine);
                };

                window.ClosingAsync += static (s, e) => {
                    return Instance.Dispatcher.InvokeAsync(() => {
                        return CommandManager.Instance.RunActionAsync(_ => OnEngineWindowAboutToClose(s), s.LocalContextData);
                    }).Unwrap();
                };

                window.Closed += static (s, e) => {
                    EngineView view = (EngineView) ((OverlayContentHostRoot) s.Content!).Content!;
                    ((MemoryEngineManagerImpl) GetComponent<MemoryEngineManager>()).OnEngineClosed(view.MemoryEngine);
                    DataManager.GetContextData(s.Control).Remove(MemoryEngine.EngineDataKey);
                };

                await window.ShowAsync();

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
                //     ActivityTask task = ActivityManager.Instance.CurrentTask;
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
                    Buttons = MessageBoxButton.YesNo,
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
                    Buttons = MessageBoxButton.YesNo,
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

    private static T GetOptionalAttribute<T>(XmlElement srcElement, string attributeName, Func<string, T> converter, T notPresentValue) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            return notPresentValue;

        try {
            return converter(node.Value);
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {typeof(T).Name}", e);
        }
    }

    private static T GetRequiredAttribute<T>(XmlElement srcElement, string attributeName, Func<string, T> converter) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            throw new Exception($"Missing required attribute '{attributeName}'");

        try {
            return converter(node.Value);
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {typeof(T).Name}", e);
        }
    }

    private static string GetRequiredAttribute(XmlElement srcElement, string attributeName, bool canBeWhitespaces) {
        XmlAttribute? node = srcElement.GetAttributeNode(attributeName);
        if (node == null)
            throw new Exception($"Missing required attribute '{attributeName}'");

        if (!canBeWhitespaces && string.IsNullOrWhiteSpace(node.Value))
            throw new Exception($"Attribute '{attributeName}' cannot be an empty string or consist of only whitespaces");

        try {
            return node.Value;
        }
        catch (Exception e) {
            throw new Exception($"Failed to parse attribute '{attributeName}' as {nameof(String)}", e);
        }
    }
}