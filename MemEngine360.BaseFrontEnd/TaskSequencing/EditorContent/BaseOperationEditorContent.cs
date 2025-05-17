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

namespace MemEngine360.BaseFrontEnd.TaskSequencing.EditorContent;

/// <summary>
/// The base class for the content displayed in the editor panel below the operation list, which is used to modify specific or
/// additional details that wouldn't otherwise fit into a <see cref="MemEngine360.BaseFrontEnd.TaskSequencing.ListContent.BaseOperationListContent"/>
/// </summary>
public class BaseOperationEditorContent : UserControl {
    public static readonly ModelTypeControlRegistry<BaseOperationEditorContent> Registry = new ModelTypeControlRegistry<BaseOperationEditorContent>();
    public static readonly StyledProperty<BaseSequenceOperation?> OperationProperty = AvaloniaProperty.Register<BaseOperationEditorContent, BaseSequenceOperation?>(nameof(Operation));

    /// <summary>
    /// Gets or sets our operation
    /// </summary>
    public BaseSequenceOperation? Operation {
        get => this.GetValue(OperationProperty);
        set => this.SetValue(OperationProperty, value);
    }

    public BaseOperationEditorContent() {
    }
    
    static BaseOperationEditorContent() {
        OperationProperty.Changed.AddClassHandler<BaseOperationEditorContent, BaseSequenceOperation?>((o, e) => o.OnOperationChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        Registry.RegisterType(typeof(DelayOperation), () => new DelayOperationEditorContent());
        Registry.RegisterType(typeof(SetMemoryOperation), () => new SetMemoryOperationEditorContent());
    }
    
    protected virtual void OnOperationChanged(BaseSequenceOperation? oldOperation, BaseSequenceOperation? newOperation) {
        
    }
}