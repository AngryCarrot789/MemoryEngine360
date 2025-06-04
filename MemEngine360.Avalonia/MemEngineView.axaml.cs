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
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.XboxBase;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity.Selecting;
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

public partial class MemEngineView : UserControl, IMemEngineUI {
    #region BINDERS

    // PFX framework uses binders to simplify "binding" model values to controls
    // and vice versa. There's a bunch of different binders that exist for us to use.

    private readonly EventPropertyBinder<MemoryEngine360> connectedHostNameBinder =
        new EventPropertyBinder<MemoryEngine360>(
            nameof(MemoryEngine360.ConnectionChanged),
            (b) => {
                // TODO: Maybe implement a custom control that represents the connection state?
                // I don't see any point in doing it though, since what would it present except text?
                string text = b.Model.Connection != null ? b.Model.Connection.ConnectionType.GetStatusBarText(b.Model.Connection) : "Disconnected";
                b.Control.SetValue(TextBlock.TextProperty, text);
            } /* UI changes do not reflect back into models, so no updateModel */);

    private readonly EventPropertyBinder<ScanningProcessor> isScanningBinder =
        new EventPropertyBinder<ScanningProcessor>(
            nameof(ScanningProcessor.IsScanningChanged),
            (b) => {
                MemEngineView w = (MemEngineView) b.Control;
                w.PART_ScanOptionsControl.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
                w.UpdateScanResultCounterText();
            });

    private readonly IBinder<ScanningProcessor> scanAddressBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.StartAddress:X8}", async (b, x) => {
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
    });

    private readonly IBinder<ScanningProcessor> scanLengthBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.ScanLength:X8}", async (b, x) => {
        if (uint.TryParse(x, NumberStyles.HexNumber, null, out uint value)) {
            if (value == b.Model.ScanLength) {
                return true;
            }

            if (b.Model.StartAddress + value < value) {
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
    });

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

    private readonly IBinder<ScanningProcessor> alignmentBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.AlignmentChanged), (b) => ((MemEngineView) b.Control).PART_ScanOption_Alignment.Content = b.Model.Alignment.ToString());
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);

    // Will reimplement at some point
    // private readonly IBinder<MemoryEngine360> forceLEBinder = new AvaloniaPropertyToEventPropertyBinder<MemoryEngine360>(ToggleButton.IsCheckedProperty, nameof(MemoryEngine360.IsForcedLittleEndianChanged), (b) => {
    //     ((ToggleButton) b.Control).IsChecked = b.Model.IsForcedLittleEndian;
    //     ((ToggleButton) b.Control).Content = b.Model.IsForcedLittleEndian is bool state ? ((state ? "Endianness: Little" : "Endianness: Big") + " (mostly works)") : "Endianness: Automatic";
    // }, (b) => {
    //     b.Model.IsForcedLittleEndian = ((ToggleButton) b.Control).IsChecked;
    //     b.Model.ScanningProcessor.RefreshSavedAddressesLater();
    // });

    private readonly IBinder<ScanningProcessor> scanMemoryPagesBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanMemoryPagesChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanMemoryPages, (b) => b.Model.ScanMemoryPages = ((ToggleButton) b.Control).IsChecked == true);
    private readonly AsyncRelayCommand editAlignmentCommand;

    #endregion

    public MemoryEngine360 MemoryEngine360 { get; }

    public NotificationManager NotificationManager => this.PART_NotificationListBox.NotificationManager!;

    public TopLevelMenuRegistry TopLevelMenuRegistry { get; }

    public ContextEntryGroup RemoteCommandsContextEntry { get; }

    public IListSelectionManager<ScanResultViewModel> ScanResultSelectionManager { get; }

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

    private class TestThing : CustomContextEntry {
        private IMemEngineUI? ctxMemUI;

        public TestThing(string displayName, string? description, Icon? icon = null) : base(displayName, description, icon) {
        }

        // Sort of pointless unless the user tries to connect to a console while it's booting
        // and then they open the File menu, they'll see that this entry is greyed out until we
        // connect, then once connected, it's either now invisible or clickable. This is just a POF really
        protected override void OnContextChanged() {
            base.OnContextChanged();
            if (this.CapturedContext != null) {
                if (IMemEngineUI.MemUIDataKey.TryGetContext(this.CapturedContext, out this.ctxMemUI)) {
                    this.ctxMemUI.MemoryEngine360.ConnectionChanged += this.OnContextMemUIConnectionChanged;
                }
            }
            else if (this.ctxMemUI != null) {
                this.ctxMemUI.MemoryEngine360.ConnectionChanged -= this.OnContextMemUIConnectionChanged;
                this.ctxMemUI = null;
            }
        }

        private void OnContextMemUIConnectionChanged(MemoryEngine360 sender, ulong frame, IConsoleConnection? oldC, IConsoleConnection? newC, ConnectionChangeCause cause) {
            this.RaiseCanExecuteChanged();
        }

        public override bool CanExecute(IContextData context) {
            if (!IMemEngineUI.MemUIDataKey.TryGetContext(context, out IMemEngineUI? ui)) {
                return false;
            }

            return ui.MemoryEngine360.Connection is XbdmConsoleConnection;
        }

        public static string ConvertStringToHex(string input, Encoding encoding) {
            byte[] stringBytes = encoding.GetBytes(input);
            StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
            foreach (byte b in stringBytes) {
                sbBytes.Append($"{b:X2}");
            }

            return sbBytes.ToString();
        }

        public override async Task OnExecute(IContextData context) {
            if (!IMemEngineUI.MemUIDataKey.TryGetContext(context, out IMemEngineUI? ui)) {
                return;
            }

            using IDisposable? token = await ui.MemoryEngine360.BeginBusyOperationActivityAsync();
            if (token == null || !(ui.MemoryEngine360.Connection is XbdmConsoleConnection xbdm)) {
                return;
            }

            DataParameterEnumInfo<XNotiyLogo> dpEnumInfo = DataParameterEnumInfo<XNotiyLogo>.All();
            DoubleUserInputInfo info = new DoubleUserInputInfo("Thank you for using MemEngine360 <3", nameof(XNotiyLogo.FLASHING_HAPPY_FACE)) {
                Caption = "Test Notification",
                Message = "Shows a custom notification on your xbox!",
                ValidateA = (b) => {
                    if (string.IsNullOrWhiteSpace(b.Input))
                        b.Errors.Add("Input cannot be empty or whitespaces only");
                },
                ValidateB = (b) => {
                    if (!dpEnumInfo.TextToEnum.TryGetValue(b.Input, out XNotiyLogo val))
                        b.Errors.Add("Unknown logo type");
                },
                LabelA = "Message",
                LabelB = "Logo (search for XNotiyLogo)"
            };

            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                XNotiyLogo logo = dpEnumInfo.TextToEnum[info.TextB];
                int msgLen = info.TextA.Length;
                string msgHex = ConvertStringToHex(info.TextA, Encoding.ASCII);
                string command = $"consolefeatures ver=2 type=12 params=\"A\\0\\A\\2\\2/{msgLen}\\{msgHex}\\1\\{(int) logo}\\\"";
                await xbdm.SendCommand(command);
            }
        }
    }

    // TODO: we need a better way to raise the CanExecuteChanged event than this, because this is awful
    // private class CommandContextEntryEx : CommandContextEntry {
    //     private readonly IMemEngineUI ui;
    //     private readonly RapidDispatchAction rda;
    //
    //     public CommandContextEntryEx(IMemEngineUI ui, string commandId, string displayName, string? description = null, Icon? icon = null, StretchMode stretchMode = StretchMode.None) : base(commandId, displayName, description, icon, stretchMode) {
    //         this.ui = ui;
    //         this.ui.MemoryEngine360.IsBusyChanged += this.MemoryEngine360OnIsBusyChanged;
    //         this.rda = new RapidDispatchAction(this.RaiseCanExecuteChanged, DispatchPriority.Loaded, nameof(CommandContextEntryEx));
    //     }
    //
    //     private void MemoryEngine360OnIsBusyChanged(MemoryEngine360 sender) {
    //         this.rda.InvokeAsync();
    //     }
    // }

    public MemEngineView() {
        this.InitializeComponent();

        this.TopLevelMenuRegistry = new TopLevelMenuRegistry();
        {
            ContextEntryGroup entry = new ContextEntryGroup("File");
            entry.Items.Add(new CommandContextEntry("commands.memengine.OpenConsoleConnectionDialogCommand", "Connect to console...", icon: SimpleIcons.ConnectToConsoleIcon));
            entry.Items.Add(new CommandContextEntry("commands.memengine.DumpMemoryCommand", "Memory Dump...", icon: SimpleIcons.DownloadMemoryIcon));
            entry.Items.Add(new SeparatorEntry());
            entry.Items.Add(new CommandContextEntry("commands.memengine.remote.SendCmdCommand", "Send Custom Command...", "This lets you send a completely custom Xbox Debug Monitor command. Please be careful with it."));
            entry.Items.Add(new TestThing("Test Notification (XBDM)", null, null));
            entry.Items.Add(new SeparatorEntry());
            entry.Items.Add(new CommandContextEntry("commands.mainWindow.OpenEditorSettings", "Preferences"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        {
            this.RemoteCommandsContextEntry = new ContextEntryGroup("Remote Controls");
            this.TopLevelMenuRegistry.Items.Add(this.RemoteCommandsContextEntry);
        }

        {
            ContextEntryGroup entry = new ContextEntryGroup("Tools");
            entry.Items.Add(new CommandContextEntry("commands.memengine.ShowDebuggerCommand", "Open debugger"));
            entry.Items.Add(new CommandContextEntry("commands.memengine.OpenTaskSequencerCommand", "Open Sequencer"));
            entry.Items.Add(new CommandContextEntry("commands.memengine.ShowModulesCommand", "Show Modules"));
            entry.Items.Add(new CommandContextEntry("commands.memengine.remote.ShowMemoryRegionsCommand", "Show Memory Regions"));
            entry.Items.Add(new CommandContextEntry("commands.memengine.PointerScanCommand", "Pointer Scan [DEBUG ONLY]"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        this.themesSubList = new ContextEntryGroup("Themes");
        this.TopLevelMenuRegistry.Items.Add(this.themesSubList);

        {
            ContextEntryGroup entry = new ContextEntryGroup("About");
            entry.Items.Add(new CommandContextEntry("commands.application.AboutApplicationCommand", "About MemEngine360"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        this.PART_TopLevelMenu.TopLevelMenuRegistry = this.TopLevelMenuRegistry;

        this.MemoryEngine360 = new MemoryEngine360();
        this.ScanResultSelectionManager = new DataGridSelectionManager<ScanResultViewModel>(this.PART_ScanListResults);
        // this.SavedAddressesSelectionManager = new DataGridSelectionManager<AddressTableEntry>(this.PART_SavedAddressList);
        this.AddressTableSelectionManager = new TreeViewSelectionManager<IAddressTableEntryUI>(this.PART_SavedAddressTree);

        this.PART_LatestActivity.Text = "Welcome to MemEngine360.";
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine360.ScanningProcessor.ScanResults;
        this.PART_SavedAddressTree.AddressTableManager = this.MemoryEngine360.AddressTableManager;

        this.MemoryEngine360.ScanningProcessor.ScanResults.CollectionChanged += (sender, args) => {
            this.UpdateScanResultCounterText();
        };

        this.editAlignmentCommand = new AsyncRelayCommand(async () => {
            ScanningProcessor p = this.MemoryEngine360.ScanningProcessor;
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

            SingleUserInputInfo.TextParameter.AddValueChangedHandler(info, (parameter, owner) => UpdateFooter((SingleUserInputInfo) owner));
            UpdateFooter(info);
            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) == true) {
                p.Alignment = NumberUtils.ParseHexOrRegular<uint>(info.Text);
            }
        });

        this.PART_ScanOptionsControl.MemoryEngine360 = this.MemoryEngine360;
        this.PART_ActivityListPanel.KeyDown += this.PART_ActivityListPanelOnKeyDown;

        this.PART_NotificationListBox.NotificationManager = new NotificationManager();
    }

    private void PART_ActivityListPanelOnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            this.IsActivtyListVisible = false;
        }
    }

    private void UpdateScanResultCounterText() {
        ScanningProcessor processor = this.MemoryEngine360.ScanningProcessor;

        int pending = processor.ActualScanResultCount;
        int count = processor.ScanResults.Count;
        pending -= count;
        this.PART_Run_CountResults.Text = $"{count} results{(pending > 0 ? $" ({pending} {(processor.IsScanning ? "pending" : "hidden")})" : "")}";
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.connectedHostNameBinder.Attach(this.PART_ConnectedHostName, this.MemoryEngine360);
        this.isScanningBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.scanAddressBinder.Attach(this.PART_ScanOption_StartAddress, this.MemoryEngine360.ScanningProcessor);
        this.scanLengthBinder.Attach(this.PART_ScanOption_Length, this.MemoryEngine360.ScanningProcessor);
        this.alignmentBinder.Attach(this, this.MemoryEngine360.ScanningProcessor);
        this.pauseXboxBinder.Attach(this.PART_ScanOption_PauseConsole, this.MemoryEngine360.ScanningProcessor);
        // this.forceLEBinder.Attach(this.PART_ForcedEndianness, this.MemoryEngine360);
        this.scanMemoryPagesBinder.Attach(this.PART_ScanOption_ScanMemoryPages, this.MemoryEngine360.ScanningProcessor);
        this.MemoryEngine360.ConnectionChanged += this.OnConnectionChanged;

        this.themeListHandler = ObservableItemProcessor.MakeIndexable(ThemeManager.Instance.Themes, (sender, index, item) => {
            this.themesSubList.Items.Insert(index, new SetThemeContextEntry(item));
        }, (sender, index, item) => {
            this.themesSubList.Items.RemoveAt(index);
        }, (sender, oldIndex, newIndex, item) => {
            this.themesSubList.Items.Move(oldIndex, newIndex);
        }).AddExistingItems();
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);

        this.MemoryEngine360.ConnectionChanged -= this.OnConnectionChanged;
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
    }

    private class SetThemeContextEntry : CustomContextEntry {
        private readonly Theme theme;

        public SetThemeContextEntry(Theme theme, Icon? icon = null) : base(theme.Name, $"Sets the application's theme to '{theme.Name}'", icon) {
            this.theme = theme;
        }

        public override Task OnExecute(IContextData context) {
            this.theme.ThemeManager.SetTheme(this.theme);
            return Task.CompletedTask;
        }
    }

    private void OnConnectionChanged(MemoryEngine360 sender, ulong frame, IConsoleConnection? oldConn, IConsoleConnection? newConn, ConnectionChangeCause cause) {
        TextNotification notification = this.connectionNotification ??= new TextNotification() {
            ContextData = new ContextData().Set(IMemEngineUI.MemUIDataKey, this)
        };

        if (newConn != null) {
            notification.Caption = "Connected";
            notification.Text = $"Connected to '{newConn.ConnectionType.DisplayName}'";
            notification.Commands.Clear();
            notification.Commands.Add(this.connectionNotificationCommandGetStarted ??= new LambdaNotificationCommand("Get Started", static async (c) => {
                await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                    const string url = "https://github.com/AngryCarrot789/MemoryEngine360/wiki#quick-start";
                    IMemEngineUI mem = IMemEngineUI.MemUIDataKey.GetContext(c.ContextData!)!;
                    return TopLevel.GetTopLevel((MemEngineView) mem)?.Launcher.LaunchUriAsync(new Uri(url)) ?? Task.FromResult(false);
                });
            }) { ToolTip = "Opens a link to MemoryEngine360's quick start guide on the wiki" });

            notification.Commands.Add(this.connectionNotificationCommandDisconnect ??= new LambdaNotificationCommand("Disconnect", static async (c) => {
                // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                IMemEngineUI mem = IMemEngineUI.MemUIDataKey.GetContext(c.ContextData!)!;
                if (mem.MemoryEngine360.Connection != null) {
                    ((ContextData) c.ContextData!).Set(IMemEngineUI.IsDisconnectFromNotification, true);
                    await OpenConsoleConnectionDialogCommand.DisconnectInActivity(mem, 0);
                    ((ContextData) c.ContextData!).Set(IMemEngineUI.IsDisconnectFromNotification, null);
                }

                c.Notification?.Close();
            }) { ToolTip = "Disconnect from the connection" });

            notification.CanAutoHide = true;
            notification.Open(this.NotificationManager);
            this.PART_LatestActivity.Text = notification.Text;
        }
        else {
            notification.Text = $"Disconnected from '{oldConn!.ConnectionType.DisplayName}'";
            this.PART_LatestActivity.Text = notification.Text;
            if (cause != ConnectionChangeCause.ClosingWindow && (!IMemEngineUI.IsDisconnectFromNotification.TryGetContext(notification.ContextData!, out bool b) || !b)) {
                notification.Caption = cause switch {
                    ConnectionChangeCause.LostConnection => "Lost Connection",
                    ConnectionChangeCause.ConnectionError => "Connection error",
                    _ => "Disconnected"
                };

                notification.Commands.Clear();
                if (cause == ConnectionChangeCause.LostConnection || cause == ConnectionChangeCause.ConnectionError) {
                    notification.CanAutoHide = false;
                    notification.Commands.Add(this.connectionNotificationCommandReconnect ??= new LambdaNotificationCommand("Reconnect", static async (c) => {
                        // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                        IMemEngineUI mem = IMemEngineUI.MemUIDataKey.GetContext(c.ContextData!)!;
                        MemoryEngine360 eng = mem.MemoryEngine360;
                        if (eng.Connection != null) {
                            c.Notification?.Close();
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
                                c.Notification?.Close();
                                eng.SetConnection(busyToken, 0, connection, ConnectionChangeCause.User, eng.LastUserConnectionInfo);
                            }
                        }
                        else {
                            c.Notification?.Close();
                            await CommandManager.Instance.Execute("commands.memengine.OpenConsoleConnectionDialogCommand", c.ContextData!, true);
                        }
                    }) {
                        ToolTip = "Attempt to reconnect to the console, using the same options (e.g. IP address) specified when it was opened initially." + Environment.NewLine +
                                  "If it wasn't opened by you like that, this just shows the Open Connection dialog."
                    });
                }
                else {
                    notification.CanAutoHide = true;
                }

                notification.Open(this.NotificationManager);
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
}