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

namespace MemEngine360.Sequencing.Operations;

public readonly struct TestClassIsCoolChangedEventArgs(long oldIsCool, long newIsCool);
public delegate void TestClassCustomEventsChangedEventHandler(TestClass sender);

public class TestClass {
    private long isCool;
    private int counter;
    private bool customEvents;

    public long IsCool {
        get => this.isCool;
        set => SetAndRaiseINE(ref this.isCool, value, this, this.IsCoolChanged, new TestClassIsCoolChangedEventArgs(this.isCool, value));
    }

    public int Counter {
        get => this.counter;
        set => SetAndRaiseINE(ref this.counter, value, this, this.CounterChanged);
    }

    public bool CustomEvents {
        get => this.customEvents;
        set => PropertyHelper.SetAndRaiseINE(ref this.customEvents, value, this, static t => t.CustomEventsChanged?.Invoke(t));
    }

    public event EventHandler<TestClassIsCoolChangedEventArgs>? IsCoolChanged;
    public event EventHandler? CounterChanged;
    public event TestClassCustomEventsChangedEventHandler? CustomEventsChanged;
    
    public static void SetAndRaiseINE<T, TEventArgs>(ref T field, T newValue, object instance, EventHandler<TEventArgs>? onValueChanged, TEventArgs eventArgs) {
        if (!EqualityComparer<T>.Default.Equals(field, newValue)) {
            field = newValue;
            onValueChanged?.Invoke(instance, eventArgs);
        }
    }
    
    public static void SetAndRaiseINE<T>(ref T field, T newValue, object instance, EventHandler? onValueChanged) {
        if (!EqualityComparer<T>.Default.Equals(field, newValue)) {
            field = newValue;
            onValueChanged?.Invoke(instance, EventArgs.Empty);
        }
    }
}