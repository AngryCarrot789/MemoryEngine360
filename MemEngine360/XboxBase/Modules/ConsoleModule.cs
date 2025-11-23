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

using PFXToolKitUI.Utils.Collections.Observable;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.XboxBase.Modules;

public class ConsoleModule {
    /// <summary>
    /// Gets or sets the shorthand name, typically the file name (e.g. default.xex). When set to an empty value
    /// or a value consisting of only whitespaces, the value will become null.
    /// </summary>
    public string? Name {
        get => field;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.NameChanged);
        }
    }

    /// <summary>
    /// Gets or sets the long name. When set to an empty value or a value consisting of only whitespaces, the value will become null.
    /// </summary>
    public string? FullName {
        get => field;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            PropertyHelper.SetAndRaiseINE(ref field, value, this, this.FullNameChanged);
        }
    }

    /// <summary>
    /// Gets the base address of the module
    /// </summary>
    public uint BaseAddress {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.BaseAddressChanged);
    }

    /// <summary>
    /// Gets the size of the module, as in, the total amount of bytes it takes up in memory.
    /// </summary>
    public uint ModuleSize {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ModuleSizeChanged);
    }

    /// <summary>
    /// Gets the original size of this module. Not entirely sure what this is for, maybe the
    /// size of the xex in memory before it starts allocating its own shit on the heap
    /// </summary>
    public uint OriginalModuleSize {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.OriginalModuleSizeChanged);
    }

    public DateTime Timestamp {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.TimestampChanged);
    }

    /// <summary>
    /// Gets the memory address of the entry point
    /// </summary>
    public uint EntryPoint {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.EntryPointChanged);
    }

    /// <summary>
    /// No fucking clue lol
    /// </summary>
    public string PEModuleName {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.PEModuleNameChanged);
    } = "";
    
    /// <summary>
    /// Gets the manager this module exists in
    /// </summary>
    public ModuleViewer? Viewer { get; internal set; }

    public ObservableList<ConsoleModuleSection> Sections { get; }

    public event EventHandler? NameChanged;
    public event EventHandler? FullNameChanged;
    public event EventHandler? BaseAddressChanged;
    public event EventHandler? ModuleSizeChanged;
    public event EventHandler? OriginalModuleSizeChanged;
    public event EventHandler? TimestampChanged;
    public event EventHandler? EntryPointChanged;
    public event EventHandler? PEModuleNameChanged;

    public ConsoleModule() {
        this.Sections = new ObservableList<ConsoleModuleSection>();
        ObservableItemProcessor.MakeSimple(this.Sections, s => s.Module = this, s => s.Module = null);
    }
}