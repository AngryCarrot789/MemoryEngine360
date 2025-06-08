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

using System.ComponentModel;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.XboxBase.Modules;

public delegate void XboxModuleSectionEventHandler(XboxModuleSection sender);

public class XboxModuleSection : INotifyPropertyChanged {
    public static readonly DataKey<XboxModuleSection> DataKey = DataKey<XboxModuleSection>.Create("XboxModuleSection");

    private string? name;
    private uint baseAddress;
    private uint size;
    private uint index;
    private XboxSectionInfoFlags flags;

    public string? Name {
        get => this.name;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            if (this.name != value) {
                this.name = value;
                this.NameChanged?.Invoke(this);
                this.OnPropertyChanged(nameof(this.Name));
            }
        }
    }

    public uint BaseAddress {
        get => this.baseAddress;
        set {
            if (this.baseAddress != value) {
                this.baseAddress = value;
                this.BaseAddressChanged?.Invoke(this);
                this.OnPropertyChanged(nameof(this.BaseAddress));
            }
        }
    }

    public uint Size {
        get => this.size;
        set {
            if (this.size != value) {
                this.size = value;
                this.SizeChanged?.Invoke(this);
                this.OnPropertyChanged(nameof(this.Size));
            }
        }
    }

    public uint Index {
        get => this.index;
        set {
            if (this.index != value) {
                this.index = value;
                this.IndexChanged?.Invoke(this);
                this.OnPropertyChanged(nameof(this.Index));
            }
        }
    }

    public XboxSectionInfoFlags Flags {
        get => this.flags;
        set {
            if (this.flags != value) {
                this.flags = value;
                this.FlagsChanged?.Invoke(this);
                this.OnPropertyChanged(nameof(this.Flags));
                this.OnPropertyChanged(nameof(this.IsLoaded));
                this.OnPropertyChanged(nameof(this.IsReadable));
                this.OnPropertyChanged(nameof(this.IsWriteable));
                this.OnPropertyChanged(nameof(this.IsExecutable));
                this.OnPropertyChanged(nameof(this.IsUninitialized));
            }
        }
    }

    public bool IsLoaded => (this.flags & XboxSectionInfoFlags.Loaded) != 0;
    public bool IsReadable => (this.flags & XboxSectionInfoFlags.Readable) != 0;
    public bool IsWriteable => (this.flags & XboxSectionInfoFlags.Writeable) != 0;
    public bool IsExecutable => (this.flags & XboxSectionInfoFlags.Executable) != 0;
    public bool IsUninitialized => (this.flags & XboxSectionInfoFlags.Uninitialized) != 0;

    public event XboxModuleSectionEventHandler? NameChanged;
    public event XboxModuleSectionEventHandler? BaseAddressChanged;
    public event XboxModuleSectionEventHandler? SizeChanged;
    public event XboxModuleSectionEventHandler? IndexChanged;
    public event XboxModuleSectionEventHandler? FlagsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public XboxModule? Module { get; internal set; }

    public XboxModuleSection() {
    }

    protected virtual void OnPropertyChanged(string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}