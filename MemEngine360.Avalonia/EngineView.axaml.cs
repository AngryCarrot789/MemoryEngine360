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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
using MemEngine360.Connections.Traits;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.Scanners;
using MemEngine360.ValueAbstraction;
using MemEngine360.Xbox360XBDM.Consoles.Xbdm;
using MemEngine360.XboxBase;
using PFXToolKitUI;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity;
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

public partial class EngineView : UserControl, IEngineUI {
    #region BINDERS

    // PFX framework uses binders to simplify "binding" model values to controls
    // and vice versa. There's a bunch of different binders that exist for us to use.

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
                w.PART_ScanOptionsControl.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
                w.UpdateScanResultCounterText();
            });

    private readonly IBinder<ScanningProcessor> scanAddressBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(
        nameof(ScanningProcessor.ScanRangeChanged),
        (b) => $"{b.Model.StartAddress:X8}",
        async (b, x) => {
            DataManager.EvaluateContextDataRaw(b.Control);

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

    private readonly IBinder<ScanningProcessor> scanLengthBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(
        nameof(ScanningProcessor.ScanRangeChanged),
        (b) => $"{b.Model.ScanLength:X8}",
        async (b, x) => {
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

    private readonly IBinder<ScanningProcessor> alignmentBinder = new EventUpdateBinder<ScanningProcessor>(nameof(ScanningProcessor.AlignmentChanged), (b) => ((EngineView) b.Control).PART_ScanOption_Alignment.Content = b.Model.Alignment.ToString());
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);

    // Will reimplement at some point
    // private readonly IBinder<MemoryEngine> forceLEBinder = new AvaloniaPropertyToEventPropertyBinder<MemoryEngine>(ToggleButton.IsCheckedProperty, nameof(MemoryEngine.IsForcedLittleEndianChanged), (b) => {
    //     ((ToggleButton) b.Control).IsChecked = b.Model.IsForcedLittleEndian;
    //     ((ToggleButton) b.Control).Content = b.Model.IsForcedLittleEndian is bool state ? ((state ? "Endianness: Little" : "Endianness: Big") + " (mostly works)") : "Endianness: Automatic";
    // }, (b) => {
    //     b.Model.IsForcedLittleEndian = ((ToggleButton) b.Control).IsChecked;
    //     b.Model.ScanningProcessor.RefreshSavedAddressesLater();
    // });

    private readonly IBinder<ScanningProcessor> scanMemoryPagesBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanMemoryPagesChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanMemoryPages, (b) => b.Model.ScanMemoryPages = ((ToggleButton) b.Control).IsChecked == true);
    private readonly AsyncRelayCommand editAlignmentCommand;

    #endregion

    public MemoryEngine MemoryEngine { get; }

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

        public static string ConvertStringToHex(string input, Encoding encoding) {
            byte[] stringBytes = encoding.GetBytes(input);
            StringBuilder sbBytes = new StringBuilder(stringBytes.Length * 2);
            foreach (byte b in stringBytes) {
                sbBytes.Append($"{b:X2}");
            }

            return sbBytes.ToString();
        }

        public override async Task OnExecute(IContextData context) {
            if (!IEngineUI.DataKey.TryGetContext(context, out IEngineUI? ui)) {
                return;
            }

            using IDisposable? token = await ui.MemoryEngine.BeginBusyOperationActivityAsync();
            if (token == null || !(ui.MemoryEngine.Connection is XbdmConsoleConnection xbdm)) {
                return;
            }

            DataParameterEnumInfo<XNotiyLogo> dpEnumInfo = DataParameterEnumInfo<XNotiyLogo>.All();
            DoubleUserInputInfo info = new DoubleUserInputInfo("Thank you for using MemoryEngine360 <3", nameof(XNotiyLogo.FLASHING_HAPPY_FACE)) {
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
    //         this.ui.MemoryEngine.IsBusyChanged += this.MemoryEngineOnIsBusyChanged;
    //         this.rda = new RapidDispatchAction(this.RaiseCanExecuteChanged, DispatchPriority.Loaded, nameof(CommandContextEntryEx));
    //     }
    //
    //     private void MemoryEngineOnIsBusyChanged(MemoryEngine sender) {
    //         this.rda.InvokeAsync();
    //     }
    // }

    public EngineView() {
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
            ContextEntryGroup entry = new ContextEntryGroup("Tools") {
                Items = {
                    new CommandContextEntry("commands.memengine.OpenTaskSequencerCommand", "Task Sequencer"),
                    new CommandContextEntry("commands.memengine.ShowModulesCommand", "Module Viewer"),
                    new CommandContextEntry("commands.memengine.remote.ShowMemoryRegionsCommand", "Memory Region Viewer"),
                    new CommandContextEntry("commands.memengine.ShowDebuggerCommand", "Debugger"),
                    new CommandContextEntry("commands.memengine.PointerScanCommand", "Pointer Scanner"),
                    new CommandContextEntry("commands.memengine.ShowConsoleEventViewerCommand", "Event Viewer").
                        AddSimpleContextUpdate(MemoryEngine.EngineDataKey, (entry, engine) => {
                            // Maybe this should be shown via a popup instead of changing the actual menu entry
                            if (engine?.Connection != null && !(engine.Connection is IHaveSystemEvents)) {
                                entry.DisplayName = "Event Viewer (console unsupported)";
                            }
                            else {
                                entry.DisplayName = "Event Viewer";
                            }
                        }),
                }
            };

            entry.Items.Add(new ContextEntryGroup("Cool Utils") {
                Items = {
                    new CustomLambdaContextEntry("[BO1 SP] Find AI's X pos near camera", async (ctx) => {
                        if (!IEngineUI.DataKey.TryGetContext(ctx, out var engineUI))
                            return;

                        // new DynamicAddress(0x82000000, [0x1AD74, 0x1758, 0x18C4, 0x144, 0x118, 0x11C])
                        // new DynamicAddress(0x82000000, [0x1AD74, 0x1758, 0x18C4, 0x144, 0x1A4, 0x1EC8])
                        MemoryEngine engine = engineUI.MemoryEngine;
                        await engine.BeginBusyOperationActivityAsync(async (t, c) => {
                            if (engine.ScanningProcessor.IsScanning) {
                                await IMessageDialogService.Instance.ShowMessage("Currently scanning", "Cannot run. Engine is scanning for a value");
                                return;
                            }

                            SingleUserInputInfo info = new SingleUserInputInfo("Range", "Input maximum radius from you", "Radius", "100.0") {
                                Validate = args => {
                                    if (!float.TryParse(args.Input, out _))
                                        args.Errors.Add("Invalid float");
                                }
                            };

                            if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                                return;
                            }

                            float radius = float.Parse(info.Text);

                            // BO1 stores positions as X Z Y. Or maybe they treat Z as up/down.
                            const uint addr_p1_x = 0x82DC184C;
                            
                            float p1_x;
                            float p1_z;
                            float p1_y;

                            try {
                                p1_x = await c.ReadValue<float>(addr_p1_x);
                                p1_z = await c.ReadValue<float>(addr_p1_x + 0x4);
                                p1_y = await c.ReadValue<float>(addr_p1_x + 0x8);
                            }
                            catch (Exception e) when (e is TimeoutException || e is IOException) {
                                await IMessageDialogService.Instance.ShowMessage("Network error", "Error while reading data from console: " + e.Message);
                                return;
                            }

                            AddressRange range = new AddressRange(engine.ScanningProcessor.StartAddress, engine.ScanningProcessor.ScanLength);
                            List<(uint, float)> results = new List<(uint, float)>();
                            using CancellationTokenSource cts = new CancellationTokenSource();
                            ActivityTask task = ActivityManager.Instance.RunTask(async () => {
                                ActivityTask activity = ActivityManager.Instance.CurrentTask;
                                IActivityProgress prog = activity.Progress;

                                if (c is IHaveIceCubes)
                                    await ((IHaveIceCubes) c).DebugFreeze();

                                if (engine.ScanningProcessor.HasDoneFirstScan) {
                                    List<ScanResultViewModel> list = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => engine.ScanningProcessor.GetScanResultsAndQueued());
                                    using PopCompletionStateRangeToken token = prog.CompletionState.PushCompletionRange(0, 1.0 / list.Count);
                                    foreach (ScanResultViewModel result in list) {
                                        activity.CheckCancelled();
                                        prog.CompletionState.OnProgress(1);

                                        if (!(result.CurrentValue is DataValueFloat floatval)) {
                                            continue;
                                        }

                                        DataValueFloat currVal = (DataValueFloat) await MemoryEngine.ReadDataValue(c, result.Address, floatval);
                                        if (Math.Abs(currVal.Value - p1_x) <= radius) {
                                            results.Add((result.Address, currVal.Value));
                                        }
                                    }
                                }
                                else {
                                    using PopCompletionStateRangeToken token = prog.CompletionState.PushCompletionRange(0, 1.0 / range.Length);
                                    bool isLE = c.IsLittleEndian;
                                    byte[] buffer = new byte[0x10008]; // read 8 over for Z and Y axis
                                    int chunkIdx = 0;
                                    uint totalChunks = range.Length / 0x10000;
                                    for (uint addr = range.BaseAddress, end = range.EndAddress; addr < end; addr += 0x10000) {
                                        activity.CheckCancelled();
                                        prog.CompletionState.OnProgress(0x10000);
                                        prog.Text = $"Chunk {++chunkIdx}/{totalChunks} ({ValueScannerUtils.ByteFormatter.ToString(range.Length - (end - addr), false)}/{ValueScannerUtils.ByteFormatter.ToString(range.Length, false)})";
                                        await c.ReadBytes(addr, buffer, 0, buffer.Length);

                                        float x, z, y;
                                        for (int offset = 0; offset < (buffer.Length - 0x8) /* X10008-8=65535 */; offset += 4) {
                                            x = AsFloat(buffer, offset, isLE);
                                            z = AsFloat(buffer, offset + 4, isLE);
                                            y = AsFloat(buffer, offset + 8, isLE);
                                            if (Math.Abs(x - p1_x) <= radius && Math.Abs(z - p1_z) <= radius && Math.Abs(y - p1_y) <= radius) {
                                                if (!(Math.Abs(x - p1_x) < 0.001F) && !(Math.Abs(z - p1_z) < 0.001F) && !(Math.Abs(y - p1_y) < 0.001F)) {
                                                    if (addr + (uint) offset != addr_p1_x)
                                                        results.Add((addr + (uint) offset, x));
                                                }
                                            }
                                        }
                                    }
                                }


                                if (c is IHaveIceCubes)
                                    await ((IHaveIceCubes) c).DebugUnFreeze();
                                return;

                                static float AsFloat(byte[] buffer, int offset, bool isDataLittleEndian) {
                                    float value = Unsafe.ReadUnaligned<float>(ref buffer[offset]);
                                    if (BitConverter.IsLittleEndian != isDataLittleEndian) {
                                        MemoryMarshal.CreateSpan(ref Unsafe.As<float, byte>(ref value), sizeof(float)).Reverse();
                                    }

                                    return value;
                                }
                            }, cts);

                            await task;
                            if  (task.Exception is TimeoutException || task.Exception is IOException) {
                                await IMessageDialogService.Instance.ShowMessage("Network error", "Error while reading data from console: " + task.Exception.Message);
                            }

                            if (results.Count > 0) {
                                engine.ScanningProcessor.ResetScan();
                                engine.ScanningProcessor.DataType = DataType.Float;
                                engine.ScanningProcessor.FloatScanOption = FloatScanOption.RoundToQuery;
                                engine.ScanningProcessor.NumericScanType = NumericScanType.Between;
                                engine.ScanningProcessor.InputA = (p1_x - radius).ToString("F4");
                                engine.ScanningProcessor.InputB = (p1_x + radius).ToString("F4");
                                engine.ScanningProcessor.ScanResults.AddRange(results.Select(x => new ScanResultViewModel(engine.ScanningProcessor, x.Item1, DataType.Float, NumericDisplayType.Normal, StringType.ASCII, new DataValueFloat(x.Item2))));
                                engine.ScanningProcessor.HasDoneFirstScan = true;
                            }
                            else {
                                await IMessageDialogService.Instance.ShowMessage("No results!", "Did not find anything nearby");
                            }
                        });
                    }, (c) => c.ContainsKey(IEngineUI.DataKey.Id))
                }
            });

            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        this.themesSubList = new ContextEntryGroup("Themes");
        this.TopLevelMenuRegistry.Items.Add(this.themesSubList);

        {
            ContextEntryGroup entry = new ContextEntryGroup("About");
            entry.Items.Add(new CommandContextEntry("commands.application.AboutApplicationCommand", "About MemoryEngine360"));
            this.TopLevelMenuRegistry.Items.Add(entry);
        }

        this.PART_TopLevelMenu.TopLevelMenuRegistry = this.TopLevelMenuRegistry;

        this.MemoryEngine = new MemoryEngine();
        this.ScanResultSelectionManager = new DataGridSelectionManager<ScanResultViewModel>(this.PART_ScanListResults);
        // this.SavedAddressesSelectionManager = new DataGridSelectionManager<AddressTableEntry>(this.PART_SavedAddressList);
        this.AddressTableSelectionManager = new TreeViewSelectionManager<IAddressTableEntryUI>(this.PART_SavedAddressTree);

        this.PART_LatestActivity.Text = "Welcome to MemoryEngine360.";
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine.ScanningProcessor.ScanResults;
        this.PART_SavedAddressTree.AddressTableManager = this.MemoryEngine.AddressTableManager;

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

        this.PART_ScanOptionsControl.MemoryEngine = this.MemoryEngine;
        this.PART_ActivityListPanel.KeyDown += this.PART_ActivityListPanelOnKeyDown;

        this.PART_NotificationListBox.NotificationManager = new NotificationManager();
    }

    private void PART_ActivityListPanelOnKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            this.IsActivtyListVisible = false;
        }
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
        this.connectedHostNameBinder.Attach(this.PART_ConnectedHostName, this.MemoryEngine);
        this.isScanningBinder.Attach(this, this.MemoryEngine.ScanningProcessor);
        this.scanAddressBinder.Attach(this.PART_ScanOption_StartAddress, this.MemoryEngine.ScanningProcessor);
        this.scanLengthBinder.Attach(this.PART_ScanOption_Length, this.MemoryEngine.ScanningProcessor);
        this.alignmentBinder.Attach(this, this.MemoryEngine.ScanningProcessor);
        this.pauseXboxBinder.Attach(this.PART_ScanOption_PauseConsole, this.MemoryEngine.ScanningProcessor);
        // this.forceLEBinder.Attach(this.PART_ForcedEndianness, this.MemoryEngine);
        this.scanMemoryPagesBinder.Attach(this.PART_ScanOption_ScanMemoryPages, this.MemoryEngine.ScanningProcessor);
        this.MemoryEngine.ConnectionChanged += this.OnConnectionChanged;

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

        this.PART_ScanOptionsControl.MemoryEngine = null;
        this.PART_SavedAddressTree.AddressTableManager = null;
        this.PART_TopLevelMenu.TopLevelMenuRegistry = null;
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

                c.Notification?.Close();
            }) { ToolTip = "Disconnect from the connection" });

            notification.CanAutoHide = true;
            notification.Open(this.NotificationManager);
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

                notification.Commands.Clear();
                if (cause == ConnectionChangeCause.LostConnection || cause == ConnectionChangeCause.ConnectionError) {
                    notification.CanAutoHide = false;
                    notification.Commands.Add(this.connectionNotificationCommandReconnect ??= new LambdaNotificationCommand("Reconnect", static async (c) => {
                        // ContextData ensured non-null by LambdaNotificationCommand.requireContext
                        IEngineUI mem = IEngineUI.DataKey.GetContext(c.ContextData!)!;
                        MemoryEngine eng = mem.MemoryEngine;
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

    public IAddressTableEntryUI GetATEntryUI(BaseAddressTableEntry entry) {
        return this.PART_SavedAddressTree.ItemMap.GetControl(entry);
    }
}