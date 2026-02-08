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

using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MemEngine360.Commands;
using MemEngine360.Engine;
using MemEngine360.Engine.HexEditing.Commands;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.XboxBase.Modules;

public partial class ModuleViewerView : UserControl {
    public static readonly StyledProperty<ModuleViewer?> XboxModuleManagerProperty = AvaloniaProperty.Register<ModuleViewerView, ModuleViewer?>(nameof(XboxModuleManager));
    public static readonly StyledProperty<MemoryEngine?> MemoryEngineProperty = AvaloniaProperty.Register<ModuleViewerView, MemoryEngine?>(nameof(MemoryEngine));
    private readonly IBinder<ConsoleModule> shortNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.NameChanged), (b) => ((TextBox) b.Control).Text = b.Model.Name);
    private readonly IBinder<ConsoleModule> fullNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.FullNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.FullName);
    private readonly IBinder<ConsoleModule> peModuleNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.PEModuleNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.PEModuleName);
    private readonly IBinder<ConsoleModule> baseAddressBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.BaseAddressChanged), (b) => ((TextBox) b.Control).Text = b.Model.BaseAddress.ToString("X8"));
    private readonly IBinder<ConsoleModule> entryPointBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.EntryPointChanged), (b) => ((TextBox) b.Control).Text = b.Model.EntryPoint.ToString("X8"));
    private readonly IBinder<ConsoleModule> moduleSizeBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.ModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.ModuleSize.ToString("X8"));
    private readonly IBinder<ConsoleModule> originalSizeBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.OriginalModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.OriginalModuleSize.ToString("X8"));
    private readonly IBinder<ConsoleModule> timestampBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.TimestampChanged), (b) => ((TextBox) b.Control).Text = b.Model.Timestamp.ToString("g"));

    public ModuleViewer? XboxModuleManager {
        get => this.GetValue(XboxModuleManagerProperty);
        set => this.SetValue(XboxModuleManagerProperty, value);
    }

    /// <summary>
    /// Gets or sets the engine reference. This is used to allow memory dumping within this GUI
    /// </summary>
    public MemoryEngine? MemoryEngine {
        get => this.GetValue(MemoryEngineProperty);
        set => this.SetValue(MemoryEngineProperty, value);
    }

    private ConsoleModule? selectedModule;
    private readonly AsyncRelayCommand dumpModuleMemoryCommand;
    internal IDesktopWindow InternalWindow;

    public ModuleViewerView() {
        this.InitializeComponent();
        this.PART_ModuleListBox.SelectionChanged += this.OnSelectionChanged;
        this.PART_CopyStuffToScanner.Click += this.PART_CopyStuffToScannerOnClick;
        this.shortNameBinder.AttachControl(this.PART_TB_ShortName);
        this.fullNameBinder.AttachControl(this.PART_TB_FullName);
        this.peModuleNameBinder.AttachControl(this.PART_TB_PEModuleName);
        this.baseAddressBinder.AttachControl(this.PART_TB_BaseAddress);
        this.entryPointBinder.AttachControl(this.PART_TB_EntryPoint);
        this.moduleSizeBinder.AttachControl(this.PART_TB_ModuleSize);
        this.originalSizeBinder.AttachControl(this.PART_TB_OriginalSize);
        this.timestampBinder.AttachControl(this.PART_TB_Timestamp);
        this.PART_DumpModuleMemory.Command = this.dumpModuleMemoryCommand = new AsyncRelayCommand(async () => {
            if (this.selectedModule == null) {
                return;
            }

            MemoryEngine? engine = this.MemoryEngine;
            if (engine == null || engine.Connection == null) {
                return;
            }

            using IBusyToken? token = await engine.BeginBusyOperationUsingActivityAsync("Dump memory");
            if (token == null) {
                return;
            }

            MessageBoxResult freezeResult = await IMessageDialogService.Instance.ShowMessage(
                "Freeze console",
                "Freeze console during memory dump?",
                "Freezing the console massively increases how quickly we can download memory from the console.",
                MessageBoxButtons.YesNo, MessageBoxResult.Yes);
            if (!freezeResult.IsValidResultOf(MessageBoxButtons.YesNo)) {
                return;
            }

            string? filePath = await IFilePickDialogService.Instance.SaveFile("Save binary data", SaveSelectionAsFileCommand.BinaryTypeAndAll);
            if (filePath == null) {
                return;
            }

            uint start = this.selectedModule.BaseAddress;
            uint length = this.selectedModule.ModuleSize;

            DumpMemoryCommand.DumpMemoryTask task = new DumpMemoryCommand.DumpMemoryTask(engine, filePath, start, length, freezeResult == MessageBoxResult.Yes, token);
            ActivityTask activity = task.Run();
            if (this.InternalWindow != null && IForegroundActivityService.TryGetInstance(out IForegroundActivityService? service)) {
                await service.WaitForActivity(new WaitForActivityOptions(this.InternalWindow, activity, CancellationToken.None) {
                   CanMinimizeIntoBackgroundActivity = true,
                   CancelActivityOnCloseRequest = true
                });
            }

            await activity; // in case user specified to run activity as a background activity or no foreground service available
            if (task.FileException != null || task.ConnectionException != null) {
                StringBuilder sb = new StringBuilder();
                if (task.ConnectionException != null) {
                    sb.Append("Download IO error: ").Append(task.ConnectionException.Message);
                }

                if (task.FileException != null) {
                    if (sb.Length > 0)
                        sb.AppendLine();
                    sb.Append("File IO error: ").Append(task.FileException.Message);
                }

                await IMessageDialogService.Instance.ShowMessage("Errors", "One or more errors occurred during memory dump", sb.ToString(), defaultButton: MessageBoxResult.OK);
            }
        }, () => this.selectedModule != null && this.MemoryEngine?.Connection != null);

        if (Design.IsDesignMode) {
            this.XboxModuleManager = new ModuleViewer() {
                Modules = {
                    new ConsoleModule() {
                        Name = "redacted.xex",
                        FullName = @"HDD:\MyGames\DONOTGOINHERE\sexsimulator.xex",
                        BaseAddress = 0x2169420,
                        EntryPoint = 0x8008135,
                        ModuleSize = 0x4000,
                        PEModuleName = "Not really sure",
                        OriginalModuleSize = 0x2000,
                        Sections = {
                            new ConsoleModuleSection() { BaseAddress = 0x2169420, Index = 0, Size = 0x1000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new ConsoleModuleSection() { BaseAddress = 0x216A420, Index = 1, Size = 0x4000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new ConsoleModuleSection() { BaseAddress = 0x216D420, Index = 2, Size = 0x8000, Flags = XboxSectionInfoFlags.Executable },
                        }
                    },
                    new ConsoleModule() {
                        Name = "default.xex",
                        FullName = @"HDD:\MyGames\BlackOps1\default.xex",
                        BaseAddress = 0x82600000,
                        EntryPoint = 0x82600000,
                        ModuleSize = 0x100000,
                        PEModuleName = "Black ops 1 maybe",
                        OriginalModuleSize = 0x10000,
                        Sections = {
                            new ConsoleModuleSection() { BaseAddress = 0x8269420, Index = 0, Size = 0x1000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new ConsoleModuleSection() { BaseAddress = 0x826A420, Index = 1, Size = 0x4000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new ConsoleModuleSection() { BaseAddress = 0x826D420, Index = 2, Size = 0x8000, Flags = XboxSectionInfoFlags.Executable },
                        }
                    }
                }
            };
        }
    }

    private void PART_CopyStuffToScannerOnClick(object? sender, RoutedEventArgs e) {
        ConsoleModule? selection = this.selectedModule;
        MemoryEngine? engine = this.MemoryEngine;
        ModuleViewer? manager = this.XboxModuleManager;
        if (selection != null && manager != null && engine != null && !engine.ScanningProcessor.IsScanning) {
            if ((selection.BaseAddress + selection.ModuleSize) < selection.BaseAddress) {
                return;
            }
            
            engine.ScanningProcessor.SetScanRange(selection.BaseAddress, selection.ModuleSize);
        }
    }

    static ModuleViewerView() {
        XboxModuleManagerProperty.Changed.AddClassHandler<ModuleViewerView, ModuleViewer?>((o, e) => o.OnManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        MemoryEngineProperty.Changed.AddClassHandler<ModuleViewerView, MemoryEngine?>((s, e) => s.OnMemoryEngineChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        ConsoleModule? item = this.PART_ModuleListBox.SelectedModel;
        if (this.selectedModule != item) {
            ConsoleModule? oldSelection = this.selectedModule;
            this.selectedModule = item;
            this.OnSelectionChanged(oldSelection, item);
            this.dumpModuleMemoryCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnSelectionChanged(ConsoleModule? oldModule, ConsoleModule? newModule) {
        this.PART_SectionsDataGrid.ItemsSource = newModule?.Sections;

        if (oldModule != null)
            Binders.DetachModels(this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder, this.timestampBinder);
        if (newModule != null)
            Binders.AttachModels(newModule, this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder, this.timestampBinder);
    }

    private void OnManagerChanged(ModuleViewer? oldManager, ModuleViewer? newManager) {
        this.PART_ModuleListBox.SetItemsSource(newManager?.Modules);
        if (newManager != null && newManager.Modules.Count > 0) {
            this.PART_ModuleListBox.SelectedModel = newManager.Modules[0];
        }
    }

    private void OnMemoryEngineChanged(MemoryEngine? oldValue, MemoryEngine? newValue) {
        this.dumpModuleMemoryCommand.RaiseCanExecuteChanged();
        if (oldValue != null)
            oldValue.ConnectionChanged -= this.OnEngineConnectionChanged;
        if (newValue != null)
            newValue.ConnectionChanged += this.OnEngineConnectionChanged;
    }

    private void OnEngineConnectionChanged(object? o, ConnectionChangedEventArgs args) {
        this.dumpModuleMemoryCommand.RaiseCanExecuteChanged();
    }
}