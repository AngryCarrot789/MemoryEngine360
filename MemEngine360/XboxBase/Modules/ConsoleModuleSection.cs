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

public class ConsoleModuleSection : INotifyPropertyChanged {
    public static readonly DataKey<ConsoleModuleSection> DataKey = DataKeys.Create<ConsoleModuleSection>("XboxModuleSection");

    private XboxSectionInfoFlags flags;

    public string? Name {
        get => field;
        set {
            if (string.IsNullOrWhiteSpace(value))
                value = null;

            if (field != value) {
                field = value;
                this.NameChanged?.Invoke(this, EventArgs.Empty);
                this.OnPropertyChanged(nameof(this.Name));
            }
        }
    }

    public uint BaseAddress {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.BaseAddressChanged?.Invoke(this, EventArgs.Empty);
                this.OnPropertyChanged(nameof(this.BaseAddress));
            }
        }
    }

    public uint Size {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.SizeChanged?.Invoke(this, EventArgs.Empty);
                this.OnPropertyChanged(nameof(this.Size));
            }
        }
    }

    public uint Index {
        get => field;
        set {
            if (field != value) {
                field = value;
                this.IndexChanged?.Invoke(this, EventArgs.Empty);
                this.OnPropertyChanged(nameof(this.Index));
            }
        }
    }

    public XboxSectionInfoFlags Flags {
        get => this.flags;
        set {
            if (this.flags != value) {
                this.flags = value;
                this.FlagsChanged?.Invoke(this, EventArgs.Empty);
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

    public event EventHandler? NameChanged;
    public event EventHandler? BaseAddressChanged;
    public event EventHandler? SizeChanged;
    public event EventHandler? IndexChanged;
    public event EventHandler? FlagsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ConsoleModule? Module { get; internal set; }

    public ConsoleModuleSection() {
    }

    protected virtual void OnPropertyChanged(string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}