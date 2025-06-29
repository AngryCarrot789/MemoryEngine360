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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using MemEngine360.PointerScanning;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Commands;
using Pointer = MemEngine360.PointerScanning.Pointer;

namespace MemEngine360.BaseFrontEnd.PointerScanning;

public partial class PointerScanWindow : DesktopWindow {
    public static readonly StyledProperty<PointerScanner?> PointerScannerProperty = AvaloniaProperty.Register<PointerScanWindow, PointerScanner?>(nameof(PointerScanner));
    
    private readonly IBinder<PointerScanner> binder_AddressableBase = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.AddressableBaseChanged), b => b.Model.AddressableBase.ToString("X8"), (b, t) => ParseUIntHelper(b, t, "Invalid Base Address", (string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, null, out value), (m, v) => m.AddressableBase = v));
    private readonly IBinder<PointerScanner> binder_AddressableLength = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.AddressableLengthChanged), b => b.Model.AddressableLength.ToString("X8"), (b, t) => ParseUIntHelper(b, t, "Invalid Length", (string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, null, out value), (m, v) => m.AddressableLength = v));
    private readonly IBinder<PointerScanner> binder_MaxDepth = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.MaxDepthChanged), b => b.Model.MaxDepth.ToString(), (b, t) => ParseUIntHelper(b, t, "Invalid Max Depth", (string s, out byte value) => byte.TryParse(s, out value), (m, v) => m.MaxDepth = v));
    private readonly IBinder<PointerScanner> binder_MaximumOffset = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.MaximumOffsetChanged), b => b.Model.MaximumOffset.ToString("X8"), (b, t) => ParseUIntHelper(b, t, "Invalid Maximum Offset", (string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, null, out value), (m, v) => m.MaximumOffset = v));
    private readonly IBinder<PointerScanner> binder_SearchAddress = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.SearchAddressChanged), b => b.Model.SearchAddress.ToString("X8"), (b, t) => ParseUIntHelper(b, t, "Invalid Search Address", (string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, null, out value), (m, v) => m.SearchAddress = v));
    private readonly IBinder<PointerScanner> binder_Alignment = new TextBoxToEventPropertyBinder<PointerScanner>(nameof(PointerScanner.AlignmentChanged), b => b.Model.Alignment.ToString(), (b, t) => ParseUIntHelper<uint>(b, t, "Invalid Alignment", uint.TryParse, (m, v) => m.Alignment = v));
    private readonly IBinder<PointerScanner> binder_StatusBar = new EventUpdateBinder<PointerScanner>(nameof(PointerScanner.HasPointerMapChanged), b => ((TextBlock) b.Control).Text = b.Model.HasPointerMap ? $"Pointer map loaded with {b.Model.PointerMap.Count}" : "No pointer map loaded");
    private ObservableItemProcessorIndexing<ImmutableArray<Pointer>>? listObservable;

    private delegate bool TryParseDelegate<T>(string input, [NotNullWhen(true)] out T? value);
    
    private static async Task<bool> ParseUIntHelper<T>(IBinder<PointerScanner> binder, string input, string errorMessage, TryParseDelegate<T> tryParse, Action<PointerScanner, T> func) {
        if (!tryParse(input, out T? value)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid input", errorMessage);
            return false;
        }

        func(binder.Model, value);
        return true;
    }
    
    public PointerScanner? PointerScanner {
        get => this.GetValue(PointerScannerProperty);
        set => this.SetValue(PointerScannerProperty, value);
    }
    
    public PointerScanWindow() {
        this.InitializeComponent();

        this.binder_AddressableBase.AttachControl(this.PART_AddressableBase);
        this.binder_AddressableLength.AttachControl(this.PART_AddressableLength);
        this.binder_MaxDepth.AttachControl(this.PART_MaxDepth);
        this.binder_MaximumOffset.AttachControl(this.PART_MaximumOffset);
        this.binder_SearchAddress.AttachControl(this.PART_SearchAddress);
        this.binder_Alignment.AttachControl(this.PART_Alignment);
        this.binder_StatusBar.AttachControl(this.PART_StatusBar);

        this.PART_OpenFile.Command = new AsyncRelayCommand(async () => {
            PointerScanner? scanner = this.PointerScanner;
            if (scanner == null) {
                return;
            }
            
            string? file = await IFilePickDialogService.Instance.OpenFile("Open binary file", [Filters.All]);
            if (file != null) {
                MessageBoxResult resultIsLE = await IMessageDialogService.Instance.ShowMessage("Endianness", "Is the data little endian? (For xbox, select No)", MessageBoxButton.YesNoCancel, MessageBoxResult.No);
                if (resultIsLE != MessageBoxResult.Yes && resultIsLE != MessageBoxResult.No) {
                    return;
                }

                SingleUserInputInfo info = new SingleUserInputInfo("Base Address", "What is the base-address of the data? (If you ran a memory dump at 0x82600000, then specify that as the base address)", "00000000") {
                    Validate = (a) => {
                        if (!uint.TryParse(a.Input, NumberStyles.HexNumber, null, out _))
                            a.Errors.Add("Invalid address");
                    }
                };

                if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info) != true) {
                    return;
                }

                uint baseAddress = uint.Parse(info.Text, NumberStyles.HexNumber);
                scanner.DisposeMemoryDump();
                Task loadDumpTask = scanner.LoadMemoryDump(file, baseAddress, resultIsLE == MessageBoxResult.Yes);
                DefaultProgressTracker progressTracker = new DefaultProgressTracker();
                
                await ActivityManager.Instance.RunTask(async () => {
                    IActivityProgress prog = ActivityManager.Instance.GetCurrentProgressOrEmpty();
                    prog.IsIndeterminate = true;
                    prog.Caption = "Load Memory Dump";
                    prog.Text = "Loading memory...";
                    await loadDumpTask;
                }, progressTracker);

                using CancellationTokenSource cancellation = new CancellationTokenSource();
                Task task = this.PointerScanner!.GenerateBasePointerMap(progressTracker, cancellation.Token);
                
                await ActivityManager.Instance.RunTask(() => task, progressTracker);
            }
        });
        
        this.PART_RunScan.Command = new AsyncRelayCommand(() => {
            PointerScanner? scanner = this.PointerScanner;
            if (scanner == null) {
                return Task.CompletedTask;
            }

            return scanner.Run();
        });
    }

    static PointerScanWindow() {
        PointerScannerProperty.Changed.AddClassHandler<PointerScanWindow, PointerScanner?>((s, e) => s.OnPointerScannerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnPointerScannerChanged(PointerScanner? oldValue, PointerScanner? newValue) {
        this.binder_AddressableBase.SwitchModel(newValue);
        this.binder_AddressableLength.SwitchModel(newValue);
        this.binder_MaxDepth.SwitchModel(newValue);
        this.binder_MaximumOffset.SwitchModel(newValue);
        this.binder_SearchAddress.SwitchModel(newValue);
        this.binder_Alignment.SwitchModel(newValue);
        this.binder_StatusBar.SwitchModel(newValue);
        this.listObservable?.Dispose();
        this.listObservable = null;

        if (newValue != null)
            this.listObservable = ObservableItemProcessor.MakeIndexable(newValue.PointerChain, this.OnItemAdded, this.OnItemRemoved, this.OnItemMoved);
    }

    private static string GetPointerText(ImmutableArray<Pointer> pointers) {
        StringBuilder sb = new StringBuilder();
        sb.Append(pointers[0].Address.ToString("X8")).Append('+').Append(pointers[0].Offset.ToString("X"));
        for (int i = 1; i < pointers.Length; i++) {
            sb.Append("->[").Append(pointers[i].Offset.ToString("X")).Append(']');
        }

        return sb.ToString();
    }
    
    private void OnItemAdded(object sender, int index, ImmutableArray<Pointer> item) {
        this.PART_ScanResults.Items.Add(new ListBoxItem() { Content = GetPointerText(item) });
    }
    
    private void OnItemRemoved(object sender, int index, ImmutableArray<Pointer> item) {
        this.PART_ScanResults.Items.RemoveAt(index);
    }
    
    private void OnItemMoved(object sender, int oldindex, int newindex, ImmutableArray<Pointer> item) {
        if (newindex < 0 || newindex >= this.PART_ScanResults.Items.Count)
            throw new IndexOutOfRangeException($"{nameof(newindex)} is not within range: {(newindex < 0 ? "less than zero" : "greater than list length")} ({newindex})");
        
        object? removedItem = this.PART_ScanResults.Items[oldindex];
        this.PART_ScanResults.Items.RemoveAt(oldindex);
        this.PART_ScanResults.Items.Insert(newindex, removedItem);
    }
}
