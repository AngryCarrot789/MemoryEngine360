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

using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Xbox360XDevkit.Modules.Models;

public delegate void XboxModuleEventHandler(XboxModule sender);

public class XboxModule {
    private string shortName = "";
    private string longName = "";
    private uint baseAddress;
    private uint moduleSize;
    private uint originalModuleSize;
    private uint entryPoint;
    private string peModuleName;

    /// <summary>
    /// Gets or sets the shorthand name, typically the file name (e.g. default.xex)
    /// </summary>
    public string ShortName {
        get => this.shortName;
        set {
            if (this.shortName == value)
                return;

            this.shortName = value;
            this.ShortNameChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets the long name, typically the full path of the file (e.g. hdd:\Games\Shit\default.xex)
    /// </summary>
    public string LongName {
        get => this.longName;
        set {
            if (this.longName == value)
                return;

            this.longName = value;
            this.LongNameChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the base address of the module
    /// </summary>
    public uint BaseAddress {
        get => this.baseAddress;
        set {
            if (this.baseAddress == value)
                return;

            this.baseAddress = value;
            this.BaseAddressChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the size of the module, as in, the total amount of bytes it takes up in memory.
    /// </summary>
    public uint ModuleSize {
        get => this.moduleSize;
        set {
            if (this.moduleSize == value)
                return;

            this.moduleSize = value;
            this.ModuleSizeChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the original size of this module. Not entirely sure what this is for, maybe the
    /// size of the xex in memory before it starts allocating its own shit on the heap
    /// </summary>
    public uint OriginalModuleSize {
        get => this.originalModuleSize;
        set {
            if (this.originalModuleSize == value)
                return;

            this.originalModuleSize = value;
            this.OriginalModuleSizeChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the memory address of the entry point
    /// </summary>
    public uint EntryPoint {
        get => this.entryPoint;
        set {
            if (this.entryPoint == value)
                return;

            this.entryPoint = value;
            this.EntryPointChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// No fucking clue lol
    /// </summary>
    public string PEModuleName {
        get => this.peModuleName;
        set {
            if (this.peModuleName == value)
                return;

            this.peModuleName = value;
            this.PEModuleNameChanged?.Invoke(this);
        }
    }

    public ObservableList<XboxModuleSection> Sections { get; }

    /// <summary>
    /// Gets the manager this module exists in
    /// </summary>
    public XboxModuleManager? Manager { get; internal set; }

    public event XboxModuleEventHandler? ShortNameChanged;
    public event XboxModuleEventHandler? LongNameChanged;
    public event XboxModuleEventHandler? BaseAddressChanged;
    public event XboxModuleEventHandler? ModuleSizeChanged;
    public event XboxModuleEventHandler? OriginalModuleSizeChanged;
    public event XboxModuleEventHandler? EntryPointChanged;
    public event XboxModuleEventHandler? PEModuleNameChanged;

    public XboxModule() {
        this.Sections = new ObservableList<XboxModuleSection>();
        ObservableItemProcessor.MakeSimple(this.Sections, s => s.Module = this, s => s.Module = null);
    }
}