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

using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.XboxBase.Modules;

public delegate void ConsoleModuleEventHandler(ConsoleModule sender);

public class ConsoleModule {
    private string? name = null;
    private string? fullName = null;
    private uint baseAddress;
    private uint moduleSize;
    private uint originalModuleSize;
    private DateTime timestamp;
    private uint entryPoint;
    private string peModuleName = "";

    /// <summary>
    /// Gets or sets the shorthand name, typically the file name (e.g. default.xex). When set to an empty value
    /// or a value consisting of only whitespaces, the value will become null.
    /// </summary>
    public string? Name {
        get => this.name;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            PropertyHelper.SetAndRaiseINE(ref this.name, value, this, static t => t.NameChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets or sets the long name. When set to an empty value or a value consisting of only whitespaces, the value will become null.
    /// </summary>
    public string? FullName {
        get => this.fullName;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            PropertyHelper.SetAndRaiseINE(ref this.fullName, value, this, static t => t.FullNameChanged?.Invoke(t));
        }
    }

    /// <summary>
    /// Gets the base address of the module
    /// </summary>
    public uint BaseAddress {
        get => this.baseAddress;
        set => PropertyHelper.SetAndRaiseINE(ref this.baseAddress, value, this, static t => t.BaseAddressChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the size of the module, as in, the total amount of bytes it takes up in memory.
    /// </summary>
    public uint ModuleSize {
        get => this.moduleSize;
        set => PropertyHelper.SetAndRaiseINE(ref this.moduleSize, value, this, static t => t.ModuleSizeChanged?.Invoke(t));
    }

    /// <summary>
    /// Gets the original size of this module. Not entirely sure what this is for, maybe the
    /// size of the xex in memory before it starts allocating its own shit on the heap
    /// </summary>
    public uint OriginalModuleSize {
        get => this.originalModuleSize;
        set => PropertyHelper.SetAndRaiseINE(ref this.originalModuleSize, value, this, static t => t.OriginalModuleSizeChanged?.Invoke(t));
    }

    public DateTime Timestamp {
        get => this.timestamp;
        set => PropertyHelper.SetAndRaiseINE(ref this.timestamp, value, this, static t => t.TimestampChanged?.Invoke(t));
    }
    
    /// <summary>
    /// Gets the memory address of the entry point
    /// </summary>
    public uint EntryPoint {
        get => this.entryPoint;
        set => PropertyHelper.SetAndRaiseINE(ref this.entryPoint, value, this, static t => t.EntryPointChanged?.Invoke(t));
    }

    /// <summary>
    /// No fucking clue lol
    /// </summary>
    public string PEModuleName {
        get => this.peModuleName;
        set => PropertyHelper.SetAndRaiseINE(ref this.peModuleName, value, this, static t => t.PEModuleNameChanged?.Invoke(t));
    }
    
    public ObservableList<ConsoleModuleSection> Sections { get; }

    /// <summary>
    /// Gets the manager this module exists in
    /// </summary>
    public ModuleViewer? Viewer { get; internal set; }

    public event ConsoleModuleEventHandler? NameChanged;
    public event ConsoleModuleEventHandler? FullNameChanged;
    public event ConsoleModuleEventHandler? BaseAddressChanged;
    public event ConsoleModuleEventHandler? ModuleSizeChanged;
    public event ConsoleModuleEventHandler? OriginalModuleSizeChanged;
    public event ConsoleModuleEventHandler? TimestampChanged;
    public event ConsoleModuleEventHandler? EntryPointChanged;
    public event ConsoleModuleEventHandler? PEModuleNameChanged;

    public ConsoleModule() {
        this.Sections = new ObservableList<ConsoleModuleSection>();
        ObservableItemProcessor.MakeSimple(this.Sections, s => s.Module = this, s => s.Module = null);
    }
}