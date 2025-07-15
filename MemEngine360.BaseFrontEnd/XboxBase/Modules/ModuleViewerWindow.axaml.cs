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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.XboxBase.Modules;

public partial class ModuleViewerWindow : DesktopWindow {
    public static readonly StyledProperty<ModuleViewer?> XboxModuleManagerProperty = AvaloniaProperty.Register<ModuleViewerWindow, ModuleViewer?>(nameof(XboxModuleManager));
    private readonly IBinder<ConsoleModule> shortNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.NameChanged), (b) => ((TextBox) b.Control).Text = b.Model.Name);
    private readonly IBinder<ConsoleModule> fullNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.FullNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.FullName);
    private readonly IBinder<ConsoleModule> peModuleNameBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.PEModuleNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.PEModuleName);
    private readonly IBinder<ConsoleModule> baseAddressBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.BaseAddressChanged), (b) => ((TextBox) b.Control).Text = b.Model.BaseAddress.ToString("X8"));
    private readonly IBinder<ConsoleModule> entryPointBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.EntryPointChanged), (b) => ((TextBox) b.Control).Text = b.Model.EntryPoint.ToString("X8"));
    private readonly IBinder<ConsoleModule> moduleSizeBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.ModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.ModuleSize.ToString("X8"));
    private readonly IBinder<ConsoleModule> originalSizeBinder = new EventUpdateBinder<ConsoleModule>(nameof(ConsoleModule.OriginalModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.OriginalModuleSize.ToString("X8"));
    
    public ModuleViewer? XboxModuleManager {
        get => this.GetValue(XboxModuleManagerProperty);
        set => this.SetValue(XboxModuleManagerProperty, value);
    }

    private ConsoleModule? selectedModule;

    public ModuleViewerWindow() {
        this.InitializeComponent();
        this.PART_ModuleListBox.SelectionChanged += this.OnSelectionChanged;

        this.shortNameBinder.AttachControl(this.PART_TB_ShortName);
        this.fullNameBinder.AttachControl(this.PART_TB_FullName);
        this.peModuleNameBinder.AttachControl(this.PART_TB_PEModuleName);
        this.baseAddressBinder.AttachControl(this.PART_TB_BaseAddress);
        this.entryPointBinder.AttachControl(this.PART_TB_EntryPoint);
        this.moduleSizeBinder.AttachControl(this.PART_TB_ModuleSize);
        this.originalSizeBinder.AttachControl(this.PART_TB_OriginalSize);

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

    static ModuleViewerWindow() {
        XboxModuleManagerProperty.Changed.AddClassHandler<ModuleViewerWindow, ModuleViewer?>((o, e) => o.OnManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        ConsoleModule? item = this.PART_ModuleListBox.SelectedModel;
        if (this.selectedModule != item) {
            this.OnSelectionChanged(this.selectedModule, item);
            this.selectedModule = item;
        }
    }

    private void OnSelectionChanged(ConsoleModule? oldModule, ConsoleModule? newModule) {
        this.PART_SectionsDataGrid.ItemsSource = newModule?.Sections;
        
        if (oldModule != null)
            Binders.DetachModels(this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder);
        if (newModule != null)
            Binders.AttachModels(newModule, this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder);
    }

    private void OnManagerChanged(ModuleViewer? oldManager, ModuleViewer? newManager) {
        this.PART_ModuleListBox.SetItemsSource(newManager?.Modules);
        if (newManager != null && newManager.Modules.Count > 0) {
            this.PART_ModuleListBox.SelectedModel = newManager.Modules[0];
        }
    }
}