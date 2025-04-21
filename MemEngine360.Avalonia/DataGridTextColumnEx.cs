using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using PFXToolKitUI.Avalonia.Interactivity;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.Avalonia;

public class DataGridTextColumnEx : DataGridTextColumn {
    public static readonly StyledProperty<string?> DoubleTapCommandIdProperty = AvaloniaProperty.Register<DataGridTextColumnEx, string?>(nameof(DoubleTapCommandId));
    private static readonly AttachedProperty<ActiveCommandInfo?> ActiveCommandInfoProperty = AvaloniaProperty.RegisterAttached<DataGridTextColumnEx, TextBlock, ActiveCommandInfo?>("ActiveCommandInfo");
    public static readonly DirectProperty<DataGridTextColumnEx, DataKey?> TextBlockDataKeyForDCProperty = AvaloniaProperty.RegisterDirect<DataGridTextColumnEx, DataKey?>(nameof(TextBlockDataKeyForDC), o => o.TextBlockDataKeyForDC, (o, v) => o.TextBlockDataKeyForDC = v);

    public DataKey? TextBlockDataKeyForDC {
        get => this.textBlockDataKeyForDC;
        set => this.SetAndRaise(TextBlockDataKeyForDCProperty, ref this.textBlockDataKeyForDC, value);
    }

    private readonly Lazy<ControlTheme?> cellTextBlockTheme;
    private DataKey? textBlockDataKeyForDC;

    public string? DoubleTapCommandId {
        get => this.GetValue(DoubleTapCommandIdProperty);
        set => this.SetValue(DoubleTapCommandIdProperty, value);
    }

    public DataGridTextColumnEx() {
        this.cellTextBlockTheme = new Lazy<ControlTheme?>((Func<ControlTheme?>) (() => this.OwningGrid.TryFindResource("DataGridCellTextBlockTheme", out object? obj2) ? (ControlTheme?) obj2 : null));
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem) {
        TextBlock textBlock = new TextBlock();
        textBlock.DoubleTapped += this.OnTextBlockDoubleTapped;
        textBlock.DataContextChanged += this.OnTextBlockDataContextChanged;
        textBlock.Name = "CellTextBlock";

        ControlTheme? controlTheme = this.cellTextBlockTheme.Value;
        if (controlTheme != null)
            textBlock.Theme = controlTheme;

        this.SyncProperties(textBlock);
        if (this.Binding != null)
            textBlock.Bind(TextBlock.TextProperty, this.Binding);
        return textBlock;
    }

    private void OnTextBlockDataContextChanged(object? sender, EventArgs e) {
        if (this.textBlockDataKeyForDC != null)
            DataManager.GetContextData((TextBlock) sender!).SetUnsafe(this.textBlockDataKeyForDC.Id, ((StyledElement) sender!).DataContext);
    }

    private void OnTextBlockDoubleTapped(object? sender, TappedEventArgs e) {
        if (this.DoubleTapCommandId is string cmdId && !string.IsNullOrWhiteSpace(cmdId)) {
            TextBlock tb = (TextBlock) sender!;
            if (tb.GetValue(ActiveCommandInfoProperty) is ActiveCommandInfo info && !info.task.IsCompleted) {
                return;
            }

            if (CommandManager.Instance.TryFindCommandById(cmdId, out Command? command)) {
                Task task = CommandManager.Instance.Execute(cmdId, command, DataManager.GetFullContextData(tb));
                if (!task.IsCompleted) {
                    ActiveCommandInfo? value = new ActiveCommandInfo(command, task);
                    tb.SetValue(ActiveCommandInfoProperty, value);
                }
            }
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
        public readonly Command command;
        public readonly Task task;

        public ActiveCommandInfo(Command command, Task task) {
            this.command = command;
            this.task = task;
        }
    }
}