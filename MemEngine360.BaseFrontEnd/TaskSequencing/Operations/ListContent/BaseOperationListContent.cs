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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Operations;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Operations.ListContent;

/// <summary>
/// The base class for the content of a <see cref="OperationListBoxItem"/>
/// </summary>
public class BaseOperationListContent : UserControl {
    public static readonly ModelTypeControlRegistry<BaseOperationListContent> Registry = new ModelTypeControlRegistry<BaseOperationListContent>();
    public static readonly StyledProperty<BaseSequenceOperation?> OperationProperty = AvaloniaProperty.Register<BaseOperationListContent, BaseSequenceOperation?>(nameof(Operation));

    /// <summary>
    /// Gets or sets our operation
    /// </summary>
    public BaseSequenceOperation? Operation {
        get => this.GetValue(OperationProperty);
        set => this.SetValue(OperationProperty, value);
    }

    public BaseOperationListContent() {
    }

    static BaseOperationListContent() {
        OperationProperty.Changed.AddClassHandler<BaseOperationListContent, BaseSequenceOperation?>((o, e) => o.OnOperationChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        Registry.RegisterType(typeof(DelayOperation), () => new DelayOperationListContent());
        Registry.RegisterType(typeof(SetMemoryOperation), () => new SetMemoryOperationListContent());
        Registry.RegisterType(typeof(LabelOperation), () => new LabelOperationListContent());
        Registry.RegisterType(typeof(JumpToLabelOperation), () => new JumpToOperationListContent());
        Registry.RegisterType(typeof(StopSequenceOperation), () => new StopSequenceOperationListContent());
    }

    protected virtual void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        if (oldOperation != null)
            oldOperation.IsEnabledChanged -= this.OnIsEnabledChanged;
        if (newOperation != null)
            newOperation.IsEnabledChanged += this.OnIsEnabledChanged;
        this.UpdateOpacity();
    }

    private void OnIsEnabledChanged(BaseSequenceOperation sender) {
        this.UpdateOpacity();
    }

    private void UpdateOpacity() {
        BaseSequenceOperation? operation = this.Operation;
        this.Opacity = operation == null || operation is LabelOperation /* disabled label means nothing */ || operation.IsEnabled
            ? 1.0
            : 0.5;
    }
}