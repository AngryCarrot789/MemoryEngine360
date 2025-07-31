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
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd;

public class DataGridTextColumnEx : DataGridTextColumn {
    public static readonly StyledProperty<string?> DoubleTapCommandIdProperty = AvaloniaProperty.Register<DataGridTextColumnEx, string?>(nameof(DoubleTapCommandId));
    public static readonly DirectProperty<DataGridTextColumnEx, DataKey?> CellDataKeyForDCProperty = AvaloniaProperty.RegisterDirect<DataGridTextColumnEx, DataKey?>(nameof(CellDataKeyForDC), o => o.CellDataKeyForDC, (o, v) => o.CellDataKeyForDC = v);
    private static readonly AttachedProperty<ActiveCommandInfo> ActiveCommandInfoProperty = AvaloniaProperty.RegisterAttached<DataGridTextColumnEx, TextBlock, ActiveCommandInfo>("ActiveCommandInfo");

    public string? DoubleTapCommandId {
        get => this.GetValue(DoubleTapCommandIdProperty);
        set => this.SetValue(DoubleTapCommandIdProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the data key that keys the cell's data context in the <see cref="IContextData"/> when executing the command.
    /// </summary>
    public DataKey? CellDataKeyForDC {
        get => this.cellDataKeyForDC;
        set => this.SetAndRaise(CellDataKeyForDCProperty, ref this.cellDataKeyForDC, value);
    }

    private readonly Lazy<ControlTheme?> cellTextBlockTheme;
    private DataKey? cellDataKeyForDC;

    public DataGridTextColumnEx() {
        this.cellTextBlockTheme = new Lazy<ControlTheme?>((Func<ControlTheme?>) (() => this.OwningGrid.TryFindResource("DataGridCellTextBlockTheme", out object? obj2) ? (ControlTheme?) obj2 : null));
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem) {
        cell.DataContextChanged += this.CellOnDataContextChanged;
        cell.DoubleTapped += this.CellOnDoubleTapped;

        TextBlock textBlock = new TextBlock {
            Name = "CellTextBlock"
        };
        
        ControlTheme? controlTheme = this.cellTextBlockTheme.Value;
        if (controlTheme != null)
            textBlock.Theme = controlTheme;

        this.SyncProperties(textBlock);
        if (this.Binding != null)
            textBlock.Bind(TextBlock.TextProperty, this.Binding);
        return textBlock;
    }

    private void CellOnDoubleTapped(object? sender, TappedEventArgs e) {
        if (this.DoubleTapCommandId is string cmdId && !string.IsNullOrWhiteSpace(cmdId)) {
            DataGridCell cell = (DataGridCell) sender!;
            ActiveCommandInfo info = cell.GetValue(ActiveCommandInfoProperty); 
            if (info.task != null && !info.task.IsCompleted) {
                return;
            }

            if (CommandManager.Instance.TryFindCommandById(cmdId, out Command? command)) {
                Task task = CommandManager.Instance.Execute(command, DataManager.GetFullContextData(cell));
                if (!task.IsCompleted) {
                    cell.SetValue(ActiveCommandInfoProperty, new ActiveCommandInfo(command, task));
                }
            }
        }
    }

    private void CellOnDataContextChanged(object? sender, EventArgs e) {
        if (this.cellDataKeyForDC != null) {
            DataGridCell cell = (DataGridCell) sender!;
            DataManager.GetContextData(cell).SetUnsafe(this.cellDataKeyForDC.Id, cell.DataContext);
        }
    }

    private void SyncProperties(AvaloniaObject content) {
        DataGridHelper.SyncColumnProperty(this, content, FontFamilyProperty);
        DataGridHelper.SyncColumnProperty(this, content, FontSizeProperty);
        DataGridHelper.SyncColumnProperty(this, content, FontStyleProperty);
        DataGridHelper.SyncColumnProperty(this, content, FontWeightProperty);
        DataGridHelper.SyncColumnProperty<IBrush>(this, content, ForegroundProperty);
    }

    private static class DataGridHelper {
        public static void SyncColumnProperty<T>(AvaloniaObject column, AvaloniaObject content, AvaloniaProperty<T> property) {
            SyncColumnProperty(column, content, property, property);
        }

        public static void SyncColumnProperty<T>(AvaloniaObject column, AvaloniaObject content, AvaloniaProperty<T> contentProperty, AvaloniaProperty<T> columnProperty) {
            if (!column.IsSet(columnProperty))
                content.ClearValue(contentProperty);
            else
                content.SetValue(contentProperty, column.GetValue(columnProperty));
        }
    }

    private readonly struct ActiveCommandInfo {
        public readonly Command? command;
        public readonly Task? task;

        public ActiveCommandInfo(Command command, Task task) {
            this.command = command;
            this.task = task;
        }
    }
}