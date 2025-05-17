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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.OperationControls;

public class BaseOperationControl : UserControl {
    public static readonly ModelTypeControlRegistry<BaseOperationControl> Registry = new ModelTypeControlRegistry<BaseOperationControl>();
    public static readonly StyledProperty<BaseSequenceOperation?> OperationProperty = AvaloniaProperty.Register<BaseOperationControl, BaseSequenceOperation?>(nameof(Operation));

    /// <summary>
    /// Gets or sets our operation
    /// </summary>
    public BaseSequenceOperation? Operation {
        get => this.GetValue(OperationProperty);
        set => this.SetValue(OperationProperty, value);
    }

    public BaseOperationControl() {
    }
    
    static BaseOperationControl() {
        OperationProperty.Changed.AddClassHandler<BaseOperationControl, BaseSequenceOperation?>((o, e) => o.OnOperationChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        Registry.RegisterType(typeof(DelayOperation), () => new DelayOperationControl());
        Registry.RegisterType(typeof(SetMemoryOperation), () => new SetMemoryOperationControl());
    }
    
    protected virtual void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        
    }
}