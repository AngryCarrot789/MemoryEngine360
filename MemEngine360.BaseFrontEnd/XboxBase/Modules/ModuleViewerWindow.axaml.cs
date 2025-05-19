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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.XboxBase;
using MemEngine360.XboxBase.Modules;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.XboxBase.Modules;

public partial class ModuleViewerWindow : DesktopWindow {
    public static readonly StyledProperty<XboxModuleManager?> XboxModuleManagerProperty = AvaloniaProperty.Register<ModuleViewerWindow, XboxModuleManager?>(nameof(XboxModuleManager));
    private readonly IBinder<XboxModule> shortNameBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.NameChanged), (b) => ((TextBox) b.Control).Text = b.Model.Name);
    private readonly IBinder<XboxModule> fullNameBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.FullNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.FullName);
    private readonly IBinder<XboxModule> peModuleNameBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.PEModuleNameChanged), (b) => ((TextBox) b.Control).Text = b.Model.PEModuleName);
    private readonly IBinder<XboxModule> baseAddressBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.BaseAddressChanged), (b) => ((TextBox) b.Control).Text = b.Model.BaseAddress.ToString("X8"));
    private readonly IBinder<XboxModule> entryPointBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.EntryPointChanged), (b) => ((TextBox) b.Control).Text = b.Model.EntryPoint.ToString("X8"));
    private readonly IBinder<XboxModule> moduleSizeBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.ModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.ModuleSize.ToString("X8"));
    private readonly IBinder<XboxModule> originalSizeBinder = new EventPropertyBinder<XboxModule>(nameof(XboxModule.OriginalModuleSizeChanged), (b) => ((TextBox) b.Control).Text = b.Model.OriginalModuleSize.ToString("X8"));
    
    public XboxModuleManager? XboxModuleManager {
        get => this.GetValue(XboxModuleManagerProperty);
        set => this.SetValue(XboxModuleManagerProperty, value);
    }

    private XboxModule? selectedModule;

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
            this.XboxModuleManager = new XboxModuleManager() {
                Modules = {
                    new XboxModule() {
                        Name = "redacted.xex",
                        FullName = @"HDD:\MyGames\DONOTGOINHERE\sexsimulator.xex",
                        BaseAddress = 0x2169420,
                        EntryPoint = 0x8008135,
                        ModuleSize = 0x4000,
                        PEModuleName = "Not really sure",
                        OriginalModuleSize = 0x2000,
                        Sections = {
                            new XboxModuleSection() { BaseAddress = 0x2169420, Index = 0, Size = 0x1000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new XboxModuleSection() { BaseAddress = 0x216A420, Index = 1, Size = 0x4000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new XboxModuleSection() { BaseAddress = 0x216D420, Index = 2, Size = 0x8000, Flags = XboxSectionInfoFlags.Executable },
                        }
                    },
                    new XboxModule() {
                        Name = "default.xex",
                        FullName = @"HDD:\MyGames\BlackOps1\default.xex",
                        BaseAddress = 0x82600000,
                        EntryPoint = 0x82600000,
                        ModuleSize = 0x100000,
                        PEModuleName = "Black ops 1 maybe",
                        OriginalModuleSize = 0x10000,
                        Sections = {
                            new XboxModuleSection() { BaseAddress = 0x8269420, Index = 0, Size = 0x1000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new XboxModuleSection() { BaseAddress = 0x826A420, Index = 1, Size = 0x4000, Flags = XboxSectionInfoFlags.Readable | XboxSectionInfoFlags.Writeable },
                            new XboxModuleSection() { BaseAddress = 0x826D420, Index = 2, Size = 0x8000, Flags = XboxSectionInfoFlags.Executable },
                        }
                    }
                }
            };
        }
    }

    static ModuleViewerWindow() {
        XboxModuleManagerProperty.Changed.AddClassHandler<ModuleViewerWindow, XboxModuleManager?>((o, e) => o.OnManagerChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        XboxModule? item = this.PART_ModuleListBox.SelectedModel;
        if (this.selectedModule != item) {
            this.OnSelectionChanged(this.selectedModule, item);
            this.selectedModule = item;
        }
    }

    private void OnSelectionChanged(XboxModule? oldModule, XboxModule? newModule) {
        // TODO: need to map between ObservableList and ObservableCollection
        this.PART_SectionsDataGrid.ItemsSource = newModule?.Sections;
        
        if (oldModule != null)
            Binders.DetachModels(this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder);
        if (newModule != null)
            Binders.AttachModels(newModule, this.shortNameBinder, this.fullNameBinder, this.peModuleNameBinder, this.baseAddressBinder, this.entryPointBinder, this.moduleSizeBinder, this.originalSizeBinder);
    }

    private void OnManagerChanged(XboxModuleManager? oldManager, XboxModuleManager? newManager) {
        this.PART_ModuleListBox.SetItemsSource(newManager?.Modules);
        if (newManager != null && newManager.Modules.Count > 0) {
            this.PART_ModuleListBox.SelectedModel = newManager.Modules[0];
        }
    }
}