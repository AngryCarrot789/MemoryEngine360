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
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MemEngine360.BaseFrontEnd;
using MemEngine360.BaseFrontEnd.Debugging;
using MemEngine360.BaseFrontEnd.EventViewing;
using MemEngine360.BaseFrontEnd.FileConnections;
using MemEngine360.BaseFrontEnd.MemRegions;
using MemEngine360.BaseFrontEnd.PointerScanning;
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
using MemEngine360.Engine.HexEditing;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.Engine.Modes;
using MemEngine360.PointerScanning;
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
using PFXToolKitUI.Utils;

namespace MemEngine360.Avalonia;

public class MemoryEngineApplication : AvaloniaApplicationPFX {
    public MemoryEngineApplication(Application application) : base(application) {
    }

    static MemoryEngineApplication() {
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
    }

    private static void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        Instance.Dispatcher.Post(() => throw e.Exception, DispatchPriority.Send);
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
        manager.Register("commands.memengine.CopyATEValueToClipboardCommand", new CopyATEValueToClipboardCommand());
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
        manager.Register("commands.memengine.ShowMemoryCommand", new ShowMemoryViewCommand());
        manager.Register("commands.memengine.DumpMemoryCommand", new DumpMemoryCommand());
        manager.Register("commands.memengine.GroupEntriesCommand", new GroupEntriesCommand());
        manager.Register("commands.memengine.OpenXMLFileCommand", new OpenXMLFileCommand());
        manager.Register("commands.memengine.SaveSavedAddressesToFileCommand", new SaveSavedAddressesToFileCommand());
        manager.Register("commands.memengine.ToggleSavedAddressAutoRefreshCommand", new ToggleSavedAddressAutoRefreshCommand());
        manager.Register("commands.memengine.ShowModulesCommand", new ShowModulesCommand());
        manager.Register("commands.memengine.PointerScanCommand", new PointerScanCommand());
        manager.Register("commands.memengine.ShowConsoleEventViewerCommand", new ShowConsoleEventViewerCommand());
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
        manager.Register("commands.hexeditor.UploadSelectionToConsoleCommand", new UploadSelectionToConsoleCommand());
        manager.Register("commands.hexeditor.GotoAddressCommand", new GotoAddressCommand());
        manager.Register("commands.hexeditor.SetAutoScanRangeAsSelectionCommand", new SetAutoScanRangeAsSelectionCommand());
        manager.Register("commands.hexeditor.ClearAutoScanRangeCommand", new ClearAutoScanRangeCommand());
        manager.Register("commands.hexeditor.SaveSelectionAsFileCommand", new SaveSelectionAsFileCommand());

        // Sequencer
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
        manager.Register("commands.sequencer.EditConditionOutputModeCommand", new EditConditionOutputModeCommand());
        manager.Register("commands.sequencer.RenameSequenceCommand", new RenameSequenceCommand());
        manager.Register("commands.sequencer.StopSpecificSequenceCommand", new StopSpecificSequenceCommand());
        manager.Register("commands.sequencer.StopSelectedSequencesCommand", new StopSelectedSequencesCommand());
        manager.Register("commands.sequencer.RunSequenceCommand", new RunSequenceCommand());
        manager.Register("commands.sequencer.ToggleOperationEnabledCommand", new ToggleOperationEnabledCommand());
        manager.Register("commands.sequencer.ToggleConditionEnabledCommand", new ToggleConditionEnabledCommand());
        manager.Register("commands.sequencer.SaveTaskSequencesCommand", new SaveTaskSequencesCommand());
        manager.Register("commands.sequencer.LoadTaskSequencesCommand", new LoadTaskSequencesCommand());

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
        manager.RegisterConstant<IEditConditionOutputModeService>(new EditConditionOutputModeServiceImpl());
        manager.RegisterConstant<IPointerScanService>(new PointerScanServiceImpl());
        manager.RegisterConstant<IConsoleEventViewerService>(new ConsoleEventViewerServiceImpl());
        manager.RegisterConstant<IDebuggerViewService>(new DebuggerViewServiceImpl());

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

        ConsoleConnectionManager manager = Instance.ServiceManager.GetService<ConsoleConnectionManager>();
        manager.Register(ConnectionTypeBinaryFile.TheID, ConnectionTypeBinaryFile.Instance);

#if DEBUG
        if (Debugger.IsAttached) {
            OpenConnectionView.Registry.RegisterType<TestConnectionInfo>(() => new OpenTestConnectionView());

            manager.Register(ConnectionTypeTest.TheID, ConnectionTypeTest.Instance);
        }
#endif

        OpenConnectionView.Registry.RegisterType<OpenBinaryFileInfo>(() => new OpenBinaryFileView());

        Theme? theme;
        ThemeManager themeManager = this.ServiceManager.GetService<ThemeManager>();
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
                    provider.Minimum = (BaseNumericDataValue) XmlTaskSequenceSerialization.DeserializeDataValue(minElement);
                if (randElement.GetElementsByTagName("MaxNumber").OfType<XmlElement>().FirstOrDefault() is XmlElement maxElement)
                    provider.Maximum = (BaseNumericDataValue) XmlTaskSequenceSerialization.DeserializeDataValue(maxElement);
            }
        });
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
                system.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                EngineWindow view = new EngineWindow();
                (progress as SplashScreenWindow)?.Close();

                system.ShutdownMode = ShutdownMode.OnMainWindowClose;
                system.Register(view, true);
                view.Show();

                Application.Current!.Resources.TryGetResource("FontOxanium", ThemeVariant.Default, out object? font);
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