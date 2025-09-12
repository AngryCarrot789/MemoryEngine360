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
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd;
using MemEngine360.Commands;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Notifications;
using PFXToolKitUI.PropertyEditing.DataTransfer.Enums;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Avalonia;

public partial class EngineView : UserControl, IEngineUI {
    private readonly IBinder<MemoryEngine> connectedHostNameBinder =
        new EventUpdateBinder<MemoryEngine>(
            nameof(MemoryEngine.ConnectionChanged),
            (b) => {
                // TODO: Maybe implement a custom control that represents the connection state?
                // I don't see any point in doing it though, since what would it present except text?
                string text = b.Model.Connection != null ? b.Model.Connection.ConnectionType.GetStatusBarText(b.Model.Connection) : "Disconnected";
                b.Control.SetValue(TextBlock.TextProperty, text);
            } /* UI changes do not reflect back into models, so no updateModel */);

    private readonly IBinder<ScanningProcessor> isScanningBinder =
        new EventUpdateBinder<ScanningProcessor>(
            nameof(ScanningProcessor.IsScanningChanged),
            (b) => {
                EngineView w = (EngineView) b.Control;
                w.PART_Grid_ScanInput.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
                w.UpdateScanResultCounterText();
            });

    private readonly IBinder<ScanningProcessor> scanAddressBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.StartAddress:X8}", ParseAndUpdateScanAddress);
    private readonly IBinder<ScanningProcessor> scanLengthBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.ScanLength:X8}", ParseAndUpdateScanLength);
    private readonly IBinder<ScanningProcessor> alignmentBinder = new EventUpdateBinder<ScanningProcessor>(nameof(ScanningProcessor.AlignmentChanged), (b) => ((EngineView) b.Control).PART_ScanOption_Alignment.Content = b.Model.Alignment.ToString());
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> scanMemoryPagesBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanMemoryPagesChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanMemoryPages, (b) => b.Model.ScanMemoryPages = ((ToggleButton) b.Control).IsChecked == true);

    private readonly EventPropertyEnumBinder<FloatScanOption> floatScanModeBinder = new EventPropertyEnumBinder<FloatScanOption>(typeof(ScanningProcessor), nameof(ScanningProcessor.FloatScanModeChanged), (x) => ((ScanningProcessor) x).FloatScanOption, (x, v) => ((ScanningProcessor) x).FloatScanOption = v);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(ScanningProcessor), nameof(ScanningProcessor.StringScanModeChanged), (x) => ((ScanningProcessor) x).StringScanOption, (x, v) => ((ScanningProcessor) x).StringScanOption = v);
    private readonly IBinder<ScanningProcessor> int_isHexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.IsIntInputHexadecimalChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.IsIntInputHexadecimal, (b) => b.Model.IsIntInputHexadecimal = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> useFirstValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UseFirstValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UseFirstValueForNextScan, (b) => b.Model.UseFirstValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> usePrevValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UsePreviousValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UsePreviousValueForNextScan, (b) => b.Model.UsePreviousValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(ScanningProcessor), nameof(ScanningProcessor.DataTypeChanged), (x) => ((ScanningProcessor) x).DataType, (x, y) => ((ScanningProcessor) x).DataType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder1 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder2 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);

    private readonly IBinder<ScanningProcessor> selectedTabIndexBinder = new AvaloniaPropertyToMultiEventPropertyBinder<ScanningProcessor>(SelectingItemsControl.SelectedIndexProperty, [nameof(ScanningProcessor.DataTypeChanged), nameof(ScanningProcessor.ScanForAnyDataTypeChanged)], (b) => {
        if (b.Model.ScanForAnyDataType) {
            ((TabControl) b.Control).SelectedIndex = 3;
        }
        else {
            switch (b.Model.DataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32:
                case DataType.Int64: {
                    ((TabControl) b.Control).SelectedIndex = 0;
                    break;
                }
                case DataType.Float:
                case DataType.Double: {
                    ((TabControl) b.Control).SelectedIndex = 1;
                    break;
                }
                case DataType.String: {
                    ((TabControl) b.Control).SelectedIndex = 2;
                    break;
                }
                case DataType.ByteArray: {
                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }
        }

        (TopLevel.GetTopLevel(b.Control) as EngineWindow)?.PART_MemEngineView.UpdateUIForScanTypeAndDataType();
    }, (b) => {
        EngineView view = ((EngineWindow) TopLevel.GetTopLevel(b.Control)!).PART_MemEngineView;
        if (!b.Model.HasDoneFirstScan) {
            int idx = ((TabControl) b.Control).SelectedIndex;
            if (idx == 3) {
                b.Model.ScanForAnyDataType = true;
                b.Model.Alignment = 1;
            }
            else {
                b.Model.ScanForAnyDataType = false;
                switch (idx) {
                    case 0: b.Model.DataType = view.lastIntegerDataType; break;
                    case 1: b.Model.DataType = view.lastFloatDataType; break;
                    case 2: b.Model.DataType = DataType.String; break;
                }

                // update anyway just in case old DT equals new DT
                b.Model.Alignment = b.Model.DataType.GetAlignmentFromDataType();
            }
        }
    });

    private readonly IBinder<ScanningProcessor> scanForAnyBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanForAnyDataTypeChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanForAnyDataType, (b) => b.Model.ScanForAnyDataType = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> inputValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenABinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenBBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputBChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputB, (b) => b.Model.InputB = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> stringIgnoreCaseBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.StringIgnoreCaseChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.StringIgnoreCase, (b) => b.Model.StringIgnoreCase = ((ToggleButton) b.Control).IsChecked == true);

    private readonly IBinder<UnknownDataTypeOptions> canScanFloatBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForFloatChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForFloat, (b) => b.Model.CanSearchForFloat = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<UnknownDataTypeOptions> canScanDoubleBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForDoubleChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForDouble, (b) => b.Model.CanSearchForDouble = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<UnknownDataTypeOptions> canScanStringBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForStringChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForString, (b) => b.Model.CanSearchForString = ((ToggleButton) b.Control).IsChecked == true);

    private readonly IBinder<ScanningProcessor> updatedEnabledControlsBinder = new MultiEventUpdateBinder<ScanningProcessor>([nameof(ScanningProcessor.ScanForAnyDataTypeChanged), nameof(ScanningProcessor.HasFirstScanChanged)], b => {
        EngineView view = (EngineView) b.Control;
        bool scanAny = b.Model.ScanForAnyDataType;
        view.PART_DataTypeCombo.IsEnabled = !scanAny && !b.Model.HasDoneFirstScan;
        view.PART_TabItemInteger.IsEnabled = !scanAny;
        view.PART_TabItemFloat.IsEnabled = !scanAny;
        view.PART_TabItemString.IsEnabled = !scanAny;
        view.PART_UseFirstValue.IsEnabled = !scanAny && b.Model.HasDoneFirstScan;
        view.PART_UsePreviousValue.IsEnabled = !scanAny && b.Model.HasDoneFirstScan;
        view.PART_ToggleUnknownDataType.IsEnabled = !b.Model.HasDoneFirstScan || scanAny;
    });

    private DataType lastIntegerDataType = DataType.Int32, lastFloatDataType = DataType.Float;

    private readonly AsyncRelayCommand editAlignmentCommand;

    public MemoryEngine MemoryEngine { get; }

    public NotificationManager NotificationManager => this.PART_NotificationListBox.NotificationManager!;

    public TopLevelMenuRegistry TopLevelMenuRegistry { get; }

    public ContextEntryGroup RemoteCommandsContextEntry { get; }

    public IListSelectionManager<IAddressTableEntryUI> AddressTableSelectionManager { get; }

    public bool IsActivtyListVisible {
        get => this.PART_ActivityListPanel.IsVisible;
        set {
            if (this.PART_ActivityListPanel.IsVisible != value) {
                this.PART_ActivityListPanel.IsVisible = value;
                this.PART_ActivityList.ActivityManager = value ? ActivityManager.Instance : null;

                this.PART_ActivityListPanel.Focus();
            }
        }
    }

    private readonly ContextEntryGroup themesSubList;
    private ObservableItemProcessorIndexing<Theme>? themeListHandler;
    private TextNotification? connectionNotification;
    private LambdaNotificationCommand? connectionNotificationCommandGetStarted;
    private LambdaNotificationCommand? connectionNotificationCommandDisconnect;
    private LambdaNotificationCommand? connectionNotificationCommandReconnect;
    private readonly DataGridSelectionModelBinder<ScanResultViewModel> scanResultSelectionBinder;

    public EngineView() {
        this.InitializeComponent();

        this.TopLevelMenuRegistry = new TopLevelMenuRegistry();
        this.themesSubList = new ContextEntryGroup("Themes");
        this.RemoteCommandsContextEntry = new ContextEntryGroup("Remote Controls");
        this.MemoryEngine = new MemoryEngine();
        this.SetupMainMenu();

        this.scanResultSelectionBinder = new DataGridSelectionModelBinder<ScanResultViewModel>(this.PART_ScanListResults, MemoryEngineViewState.GetInstance(this.MemoryEngine).SelectedScanResults);

        // this.SavedAddressesSelectionManager = new DataGridSelectionManager<AddressTableEntry>(this.PART_SavedAddressList);
        this.AddressTableSelectionManager = new TreeViewSelectionManager<IAddressTableEntryUI>(this.PART_SavedAddressTree);

        this.PART_LatestActivity.Text = "Welcome to MemoryEngine360.";
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine.ScanningProcessor.ScanResults;
        this.PART_SavedAddressTree.AddressTableManager = this.MemoryEngine.AddressTableManager;
        // this.PART_FileBrowser.FileTreeManager = this.MemoryEngine.FileTreeManager;

        this.MemoryEngine.ScanningProcessor.ScanResults.CollectionChanged += (sender, args) => {
            this.UpdateScanResultCounterText();
        };

        this.editAlignmentCommand = new AsyncRelayCommand(async () => {
            ScanningProcessor p = this.MemoryEngine.ScanningProcessor;
            SingleUserInputInfo info = new SingleUserInputInfo(p.Alignment.ToString("X")) {
                Caption = "Edit alignment",
                Message = "Alignment is the offset added to each memory address",
                Label = "Alignment (prefix with '0x' to parse as hex)",
                Validate = (e) => {
                    if (!NumberUtils.TryParseHexOrRegular<uint>(e.Input, out uint number)) {
                        e.Errors.Add("-".StartsWith(e.Input) ? "Alignment cannot be negative" : "Invalid unsigned integer");
                    }
                    else if (number == 0)
                        e.Errors.Add("Alignment cannot be zero!");
                }
            };

            static void UpdateFooter(SingleUserInputInfo inf) {
                if (inf.TextErrors != null) {
                    inf.Footer = "Cannot show examples: invalid alignment";
                }
                else {
                    int align = (int) NumberUtils.ParseHexOrRegular<uint>(inf.Text);
                    StringBuilder sb = new StringBuilder().Append(0);
                    for (int i = 1, j = align; i < 5; i++, j += align)
                        sb.Append(", ").Append(j);
                    inf.Footer = "We will scan " + sb.Append(", etc.");
                }
            }

            info.TextChanged += UpdateFooter;
            UpdateFooter(info);
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                p.Alignment = NumberUtils.ParseHexOrRegular<uint>(info.Text);
            }
        });

        this.stringIgnoreCaseBinder.AttachControl(this.PART_IgnoreCases);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_Truncate, FloatScanOption.TruncateToQuery);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_RoundToQuery, FloatScanOption.RoundToQuery);
        this.stringScanModeBinder.Assign(this.PART_DTString_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF32, StringType.UTF32);

        // Close activity list when user presses ESC
        this.PART_ActivityListPanel.AddHandler(KeyDownEvent, (sender, e) => {
            if (e.Key == Key.Escape) {
                this.IsActivtyListVisible = false;
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        this.PART_NotificationListBox.NotificationManager = new NotificationManager();
    }

    private void SetupMainMenu() {
        // ### File ###
        ContextEntryGroup fileEntry = new ContextEntryGroup("File");
        fileEntry.Items.Add(new CommandContextEntry("commands.memengine.OpenConsoleConnectionDialogCommand", "Connect to console...", icon: SimpleIcons.ConnectToConsoleIcon));
        fileEntry.Items.Add(new CommandContextEntry("commands.memengine.DumpMemoryCommand", "Memory Dump...", icon: SimpleIcons.DownloadMemoryIcon));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandContextEntry("commands.memengine.remote.SendCmdCommand", "Send Custom Command...", "This lets you send a completely custom Xbox Debug Monitor command. Please be careful with it."));
        fileEntry.Items.Add(new TestThing("Test Notification (XBDM)", null, null));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandContextEntry("commands.mainWindow.OpenEditorSettings", "Preferences"));
        this.TopLevelMenuRegistry.Items.Add(fileEntry);

        // ### Remote Commands ###
        this.TopLevelMenuRegistry.Items.Add(this.RemoteCommandsContextEntry);

        // ### Tools ###
        this.TopLevelMenuRegistry.Items.Add(this.MemoryEngine.ToolsMenu);

        // ### Themes ###
        this.TopLevelMenuRegistry.Items.Add(this.themesSubList);

        // ### Help ###
        ContextEntryGroup helpEntry = new ContextEntryGroup("Help");
        helpEntry.Items.Add(new CommandContextEntry("commands.application.ShowLogsCommand", "Show Logs"));
        helpEntry.Items.Add(new SeparatorEntry());
        helpEntry.Items.Add(new CustomLambdaContextEntry("Open Wiki", (c) => {
            const string url = "https://github.com/AngryCarrot789/MemoryEngine360/wiki#quick-start";
            return TopLevel.GetTopLevel(this /* EngineView */)?.Launcher.LaunchUriAsync(new Uri(url)) ?? Task.FromResult(false);
        }, (c) => TopLevel.GetTopLevel(this) != null));
        helpEntry.Items.Add(new CommandContextEntry("commands.application.AboutApplicationCommand", "About MemoryEngine360"));
        this.TopLevelMenuRegistry.Items.Add(helpEntry);

        this.PART_TopLevelMenu.TopLevelMenuRegistry = this.TopLevelMenuRegistry;
    }

    private void UpdateScanResultCounterText() {
        ScanningProcessor processor = this.MemoryEngine.ScanningProcessor;

        int pending = processor.ActualScanResultCount;
        int count = processor.ScanResults.Count;
        pending -= count;
        this.PART_Run_CountResults.Text = $"{count} results{(pending > 0 ? $" ({pending} {(processor.IsScanning ? "pending" : "hidden")})" : "")}";
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        MemoryEngineViewState.GetInstance(this.MemoryEngine).RequestWindowFocus += this.OnRequestWindowFocus;

        ScanningProcessor processor = this.MemoryEngine.ScanningProcessor;
        this.connectedHostNameBinder.Attach(this.PART_ConnectedHostName, this.MemoryEngine);
        this.isScanningBinder.Attach(this, processor);
        this.scanAddressBinder.Attach(this.PART_ScanOption_StartAddress, processor);
        this.scanLengthBinder.Attach(this.PART_ScanOption_Length, processor);
        this.alignmentBinder.Attach(this, processor);
        this.pauseXboxBinder.Attach(this.PART_ScanOption_PauseConsole, processor);
        // this.forceLEBinder.Attach(this.PART_ForcedEndianness, this.MemoryEngine);
        this.scanMemoryPagesBinder.Attach(this.PART_ScanOption_ScanMemoryPages, processor);
        this.MemoryEngine.ConnectionChanged += this.OnConnectionChanged;

        this.themeListHandler = ObservableItemProcessor.MakeIndexable(ThemeManager.Instance.Themes, (sender, index, item) => {
            this.themesSubList.Items.Insert(index, new SetThemeContextEntry(item));
        }, (s, idx, item) => {
            this.themesSubList.Items.RemoveAt(idx);
        }, (s, oldIndex, newIndex, item) => {
            this.themesSubList.Items.Move(oldIndex, newIndex);
        }).AddExistingItems();

        this.stringIgnoreCaseBinder.AttachModel(processor);
        this.floatScanModeBinder.Attach(processor);
        this.stringScanModeBinder.Attach(processor);
        this.int_isHexBinder.Attach(this.PART_DTInt_IsHex, processor);
        this.useFirstValueBinder.Attach(this.PART_UseFirstValue, processor);
        this.usePrevValueBinder.Attach(this.PART_UsePreviousValue, processor);
        this.dataTypeBinder.Attach(this.PART_DataTypeCombo, processor);
        this.scanTypeBinder1.Attach(this.PART_ScanTypeCombo1, processor);
        this.scanTypeBinder2.Attach(this.PART_ScanTypeCombo2, processor);
        this.selectedTabIndexBinder.Attach(this.PART_ScanSettingsTabControl, processor);
        this.scanForAnyBinder.Attach(this.PART_ToggleUnknownDataType, processor);
        this.updatedEnabledControlsBinder.Attach(this, processor);

        this.canScanFloatBinder.Attach(this.PART_Toggle_Float, processor.UnknownDataTypeOptions);
        this.canScanDoubleBinder.Attach(this.PART_Toggle_Double, processor.UnknownDataTypeOptions);
        this.canScanStringBinder.Attach(this.PART_Toggle_String, processor.UnknownDataTypeOptions);

        processor.NumericScanTypeChanged += this.ScanningProcessorOnNumericScanTypeChanged;
        processor.DataTypeChanged += this.OnScanningProcessorOnDataTypeChanged;
        processor.UseFirstValueForNextScanChanged += this.UpdateNonBetweenInput;
        processor.UsePreviousValueForNextScanChanged += this.UpdateNonBetweenInput;
        processor.ScanForAnyDataTypeChanged += this.UpdateNonBetweenInput;

        this.UpdateUIForScanTypeAndDataType();

        this.PART_OrderListBox.SetScanningProcessor(processor);
    }

    private void OnRequestWindowFocus(object? sender, EventArgs e) {
        if (TopLevel.GetTopLevel(this) is DesktopWindow window) {
            window.Activate();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);
        MemoryEngineViewState.GetInstance(this.MemoryEngine).RequestWindowFocus -= this.OnRequestWindowFocus;

        this.MemoryEngine.ConnectionChanged -= this.OnConnectionChanged;
        this.themeListHandler?.RemoveExistingItems();
        this.themeListHandler?.Dispose();
        this.themeListHandler = null;

        this.connectedHostNameBinder.Detach();
        this.isScanningBinder.Detach();
        this.scanAddressBinder.Detach();
        this.scanLengthBinder.Detach();
        this.alignmentBinder.Detach();
        this.pauseXboxBinder.Detach();
        // this.forceLEBinder.Detach();
        this.scanMemoryPagesBinder.Detach();

        this.PART_SavedAddressTree.AddressTableManager = null;
        this.PART_TopLevelMenu.TopLevelMenuRegistry = null;

        if (this.inputValueBinder.IsFullyAttached)
            this.inputValueBinder.Detach();
        if (this.inputBetweenABinder.IsFullyAttached)
            this.inputBetweenABinder.Detach();
        if (this.inputBetweenBBinder.IsFullyAttached)
            this.inputBetweenBBinder.Detach();

        this.stringIgnoreCaseBinder.DetachModel();
        this.floatScanModeBinder.Detach();
        this.stringScanModeBinder.Detach();
        this.int_isHexBinder.Detach();
        this.useFirstValueBinder.Detach();
        this.usePrevValueBinder.Detach();
        this.dataTypeBinder.Detach();
        this.scanTypeBinder1.Detach();
        this.scanTypeBinder2.Detach();
        this.selectedTabIndexBinder.Detach();
        this.scanForAnyBinder.Detach();
        this.updatedEnabledControlsBinder.Detach();

        this.canScanFloatBinder.Detach();
        this.canScanDoubleBinder.Detach();
        this.canScanStringBinder.Detach();

        this.MemoryEngine.ScanningProcessor.NumericScanTypeChanged -= this.ScanningProcessorOnNumericScanTypeChanged;
        this.MemoryEngine.ScanningProcessor.DataTypeChanged -= this.OnScanningProcessorOnDataTypeChanged;
        this.MemoryEngine.ScanningProcessor.UseFirstValueForNextScanChanged -= this.UpdateNonBetweenInput;
        this.MemoryEngine.ScanningProcessor.UsePreviousValueForNextScanChanged -= this.UpdateNonBetweenInput;
        this.MemoryEngine.ScanningProcessor.ScanForAnyDataTypeChanged -= this.UpdateNonBetweenInput;

        this.PART_OrderListBox.SetScanningProcessor(null);
    }

    private void ScanningProcessorOnNumericScanTypeChanged(ScanningProcessor sender) {
        this.UpdateUIForScanTypeAndDataType();
    }

    private void UpdateUIForScanTypeAndDataType() {
        ScanningProcessor sp = this.MemoryEngine.ScanningProcessor;
        bool isNumeric = sp.DataType.IsNumeric();
        bool isBetween = sp.NumericScanType.IsBetween();
        bool isAny = sp.ScanForAnyDataType;

        if (isBetween && isNumeric && !isAny) {
            this.PART_Input_Value1.IsVisible = false;
            this.PART_Grid_Input_Between.IsVisible = true;
            this.PART_ValueOrBetweenTextBlock.Text = "Between";
            this.PART_UseFirstOrPrevButtonGrid.IsVisible = false;

            if (this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Detach();
            if (!this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Attach(this.PART_Input_BetweenA, sp);
            if (!this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Attach(this.PART_Input_BetweenB, sp);
        }
        else {
            this.PART_Input_Value1.IsVisible = true;
            this.PART_Grid_Input_Between.IsVisible = false;
            this.PART_ValueOrBetweenTextBlock.Text = "Value";
            this.PART_UseFirstOrPrevButtonGrid.IsVisible = !isAny && isNumeric;

            if (this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Detach();
            if (this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Detach();
            if (!this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Attach(this.PART_Input_Value1, sp);
        }

        this.UpdateNonBetweenInput(sp);
    }

    private void OnScanningProcessorOnDataTypeChanged(ScanningProcessor p) {
        if (p.DataType.IsFloatingPoint()) {
            this.lastFloatDataType = p.DataType;
        }
        else if (p.DataType.IsInteger()) {
            this.lastIntegerDataType = p.DataType;
        }
    }

    private void UpdateNonBetweenInput(ScanningProcessor p) {
        this.PART_Input_Value1.IsEnabled = p.ScanForAnyDataType || p.DataType == DataType.ByteArray || p.DataType == DataType.String || (!p.UseFirstValueForNextScan && !p.UsePreviousValueForNextScan);
    }

    private class SetThemeContextEntry(Theme theme, Icon? icon = null) : CustomContextEntry(theme.Name, $"Sets the application's theme to '{theme.Name}'", icon) {
        public override Task OnExecute(IContextData context) {
            theme.ThemeManager.SetTheme(theme);
            return Task.CompletedTask;
        }
    }

    private void OnConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldConn, IConsoleConnection? newConn, ConnectionChangeCause cause) {
        TextNotification notification = this.connectionNotification ??= new TextNotification() {
            ContextData = new ContextData().Set(IEngineUI.DataKey, this)
        };

        if (newConn != null) {
            notification.Caption = "Connected";
            notification.Text = $"Connected to '{newConn.ConnectionType.DisplayName}'";
            notification.Commands.Clear();
            notification.Commands.Add(this.connectionNotificationCommandGetStarted ??= new LambdaNotificationCommand("Get Started", static async (c) => {
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    const string url = "https://github.com/AngryCarrot789/MemoryEngine360/wiki#quick-start";
                    IEngineUI mem = IEngineUI.DataKey.GetContext(c.ContextData!)!;
                    return TopLevel.GetTopLevel((EngineView) mem)?.Launcher.LaunchUriAsync(new Uri(url)) ?? Task.FromResult(false);
                });
            }) { ToolTip = "Opens a link to MemoryEngine360's quick start guide on the wiki" });

            notification.Commands.Add(this.connectionNotificationCommandDisconnect ??= new LambdaNotificationCommand("Disconnect", static async (c) => {
                // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                IEngineUI mem = IEngineUI.DataKey.GetContext(c.ContextData!)!;
                if (mem.MemoryEngine.Connection != null) {
                    ((ContextData) c.ContextData!).Set(IEngineUI.IsDisconnectFromNotification, true);
                    await OpenConsoleConnectionDialogCommand.DisconnectInActivity(mem, 0);
                    ((ContextData) c.ContextData!).Set(IEngineUI.IsDisconnectFromNotification, null);
                }

                c.Notification?.Hide();
            }) { ToolTip = "Disconnect from the connection" });

            notification.CanAutoHide = true;
            notification.Show(this.NotificationManager);
            this.PART_LatestActivity.Text = notification.Text;
        }
        else {
            notification.Text = $"Disconnected from '{oldConn!.ConnectionType.DisplayName}'";
            this.PART_LatestActivity.Text = notification.Text;
            if (cause != ConnectionChangeCause.ClosingWindow && (!IEngineUI.IsDisconnectFromNotification.TryGetContext(notification.ContextData!, out bool b) || !b)) {
                notification.Caption = cause switch {
                    ConnectionChangeCause.LostConnection => "Lost Connection",
                    ConnectionChangeCause.ConnectionError => "Connection error",
                    _ => "Disconnected"
                };

                notification.AlertMode =
                    cause == ConnectionChangeCause.LostConnection
                        ? NotificationAlertMode.UntilUserInteraction
                        : NotificationAlertMode.None;

                notification.Commands.Clear();
                if (cause == ConnectionChangeCause.LostConnection || cause == ConnectionChangeCause.ConnectionError) {
                    notification.CanAutoHide = false;
                    notification.Commands.Add(this.connectionNotificationCommandReconnect ??= new LambdaNotificationCommand("Reconnect", static async (c) => {
                        // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                        IEngineUI mem = IEngineUI.DataKey.GetContext(c.ContextData!)!;
                        MemoryEngine eng = mem.MemoryEngine;
                        if (eng.Connection != null) {
                            c.Notification?.Hide();
                            return;
                        }

                        // oh...
                        using IDisposable? busyToken = await eng.BeginBusyOperationActivityAsync("Reconnect to console");
                        if (busyToken == null) {
                            return;
                        }

                        if (eng.LastUserConnectionInfo != null) {
                            RegisteredConnectionType type = eng.LastUserConnectionInfo.ConnectionType;

                            using CancellationTokenSource cts = new CancellationTokenSource();
                            IConsoleConnection? connection;
                            try {
                                connection = await type.OpenConnection(eng.LastUserConnectionInfo, cts);
                            }
                            catch (Exception e) {
                                await IMessageDialogService.Instance.ShowMessage("Error", "An unhandled exception occurred while opening connection", e.GetToString());
                                connection = null;
                            }

                            if (connection != null) {
                                c.Notification?.Hide();
                                eng.SetConnection(busyToken, 0, connection, ConnectionChangeCause.User, eng.LastUserConnectionInfo);
                            }
                        }
                        else {
                            c.Notification?.Hide();
                            await CommandManager.Instance.Execute("commands.memengine.OpenConsoleConnectionDialogCommand", c.ContextData!, null, null);
                        }
                    }) {
                        ToolTip = "Attempt to reconnect to the console, using the same options (e.g. IP address) specified when it was opened initially." + Environment.NewLine +
                                  "If it wasn't opened by you like that, this just shows the Open Connection dialog."
                    });
                }
                else {
                    notification.CanAutoHide = true;
                }

                notification.Show(this.NotificationManager);
            }
        }

        ContextEntryGroup entry = this.RemoteCommandsContextEntry;
        if (newConn != null) {
            foreach (IContextObject en in newConn.ConnectionType.GetRemoteContextOptions()) {
                entry.Items.Add(en);
            }
        }
        else {
            entry.Items.Clear();
        }
    }

    private void PART_ScanOption_Alignment_OnDoubleTapped(object? sender, TappedEventArgs e) {
        this.editAlignmentCommand.Execute(null);
    }

    private void CloseActivityListButtonClicked(object? sender, RoutedEventArgs e) {
        this.IsActivtyListVisible = false;
    }

    public IAddressTableEntryUI GetATEntryUI(BaseAddressTableEntry entry) {
        return this.PART_SavedAddressTree.ItemMap.GetControl(entry);
    }

    private class TestThing : CustomContextEntry {
        private IEngineUI? ctxMemUI;

        public TestThing(string displayName, string? description, Icon? icon = null) : base(displayName, description, icon) {
            this.CapturedContextChanged += this.OnCapturedContextChanged;
        }

        // Sort of pointless unless the user tries to connect to a console while it's booting
        // and then they open the File menu, they'll see that this entry is greyed out until we
        // connect, then once connected, it's either now invisible or clickable. This is just a POF really
        private void OnCapturedContextChanged(BaseContextEntry sender, IContextData? oldCapturedContext, IContextData? newCapturedContext) {
            if (newCapturedContext != null) {
                if (IEngineUI.DataKey.TryGetContext(newCapturedContext, out IEngineUI? newUI) && !ReferenceEquals(this.ctxMemUI, newUI)) {
                    if (this.ctxMemUI != null)
                        this.ctxMemUI.MemoryEngine.ConnectionChanged -= this.OnContextMemUIConnectionChanged;
                    (this.ctxMemUI = newUI).MemoryEngine.ConnectionChanged += this.OnContextMemUIConnectionChanged;
                }
            }
            else if (this.ctxMemUI != null) {
                this.ctxMemUI.MemoryEngine.ConnectionChanged -= this.OnContextMemUIConnectionChanged;
                this.ctxMemUI = null;
            }
        }

        private void OnContextMemUIConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
            this.RaiseCanExecuteChanged();
        }

        public override bool CanExecute(IContextData context) {
            if (!IEngineUI.DataKey.TryGetContext(context, out IEngineUI? ui)) {
                return false;
            }

            return ui.MemoryEngine.Connection is XbdmConsoleConnection;
        }

        public override async Task OnExecute(IContextData context) {
            if (!IEngineUI.DataKey.TryGetContext(context, out IEngineUI? ui)) {
                return;
            }

            using IDisposable? token = await ui.MemoryEngine.BeginBusyOperationActivityAsync();
            if (token == null || !(ui.MemoryEngine.Connection is XbdmConsoleConnection xbdm)) {
                return;
            }

            DataParameterEnumInfo<XNotifyLogo> dpEnumInfo = DataParameterEnumInfo<XNotifyLogo>.All();
            DoubleUserInputInfo info = new DoubleUserInputInfo("Thank you for using MemoryEngine360 <3", nameof(XNotifyLogo.FLASHING_HAPPY_FACE)) {
                Caption = "Test Notification",
                Message = "Shows a custom notification on your xbox!",
                ValidateA = (b) => {
                    if (string.IsNullOrWhiteSpace(b.Input))
                        b.Errors.Add("Input cannot be empty or whitespaces only");
                },
                ValidateB = (b) => {
                    if (!dpEnumInfo.TextToEnum.TryGetValue(b.Input, out XNotifyLogo val))
                        b.Errors.Add("Unknown logo type");
                },
                LabelA = "Message",
                LabelB = "Logo (search for XNotifyLogo)"
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                XNotifyLogo logo = dpEnumInfo.TextToEnum[info.TextB];
                int msgLen = info.TextA.Length;
                string msgHex = NumberUtils.ConvertStringToHex(info.TextA, Encoding.ASCII);
                string command = $"consolefeatures ver=2 type=12 params=\"A\\0\\A\\2\\2/{msgLen}\\{msgHex}\\1\\{(int) logo}\\\"";
                await xbdm.SendCommand(command);
            }
        }
    }

    private static async Task<bool> ParseAndUpdateScanAddress(IBinder<ScanningProcessor> b, string x) {
        if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
            if (value == b.Model.StartAddress) {
                return true;
            }

            if (value + b.Model.ScanLength < value) {
                return await OnAddressOrLengthOutOfRange(b.Model, value, b.Model.ScanLength);
            }
            else {
                b.Model.SetScanRange(value, b.Model.ScanLength);
                return true;
            }
        }
        else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start Address is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Start address is invalid", defaultButton: MessageBoxResult.OK);
        }

        return false;
    }

    private static async Task<bool> ParseAndUpdateScanLength(IBinder<ScanningProcessor> b, string x) {
        if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
            if (value == b.Model.ScanLength) {
                return true;
            }
            else if (b.Model.StartAddress + value < value) {
                return await OnAddressOrLengthOutOfRange(b.Model, b.Model.StartAddress, value);
            }
            else {
                b.Model.SetScanRange(b.Model.StartAddress, value);
                return true;
            }
        }
        else if (ulong.TryParse(x, NumberStyles.HexNumber, null, out _)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Scan Length is too long. It can only be 4 bytes", defaultButton: MessageBoxResult.OK);
        }
        else {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", "Length address is invalid", defaultButton: MessageBoxResult.OK);
        }

        return false;
    }

    private static async Task<bool> OnAddressOrLengthOutOfRange(ScanningProcessor processor, uint start, uint length) {
        bool didChangeStart = processor.StartAddress != start;
        Debug.Assert(didChangeStart || processor.ScanLength != length);
        ulong overflowAmount = (ulong) start + (ulong) length - uint.MaxValue;
        MessageBoxInfo info = new MessageBoxInfo() {
            Caption = $"Invalid {(didChangeStart ? "start address" : "scan length")}",
            Message = $"Scan Length causes scan to exceed 32 bit address space by 0x{overflowAmount:X8}.{Environment.NewLine}" +
                      $"Do you want to auto-adjust the {(didChangeStart ? "scan length" : "start address")} to fit?",
            Buttons = MessageBoxButton.OKCancel, DefaultButton = MessageBoxResult.OK,
            YesOkText = "Yes"
        };

        MessageBoxResult result = await IMessageDialogService.Instance.ShowMessage(info);
        if (result == MessageBoxResult.Cancel || result == MessageBoxResult.None) {
            return false;
        }

        if (didChangeStart) {
            processor.SetScanRange(start, uint.MaxValue - start);
        }
        else {
            processor.SetScanRange((uint) (start - overflowAmount), length);
        }

        return true;
    }
}