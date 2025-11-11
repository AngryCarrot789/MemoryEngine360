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

using Lua;
using MemEngine360.ModTools.Gui;
using MemEngine360.Scripting.LuaFeatures;
using PFXToolKitUI;
using PFXToolKitUI.Configurations;
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.LuaFeatures;

public class LuaGuiFunctions {
    private static readonly LuaFunction DockPanel_Add = new LuaFunction("add", AddElementToDockPanel);
    private static readonly LuaFunction StackPanel_Add = new LuaFunction("add", AddElementToStackPanel);
    private static readonly LuaFunction GridPanel_Add = new LuaFunction("add", AddElementToGridPanel);
    private static readonly LuaFunction DockPanel_Remove = new LuaFunction("remove_at", RemoveElementInDockPanel);
    private static readonly LuaFunction StackPanel_Remove = new LuaFunction("remove_at", RemoveElementInStackPanel);
    private static readonly LuaFunction GridPanel_Remove = new LuaFunction("remove_at", RemoveElementInGridPanel);
    private static readonly LuaFunction DockPanel_IndexOf = new LuaFunction("index_of", IndexOfElementInDockPanel);
    private static readonly LuaFunction StackPanel_IndexOf = new LuaFunction("index_of", IndexOfElementInStackPanel);
    private static readonly LuaFunction GridPanel_IndexOf = new LuaFunction("index_of", IndexOfElementInGridPanel);
    private static readonly LuaFunction GridPanel_AddRow = new LuaFunction("add_row", AddGridPanelRow);
    private static readonly LuaFunction GridPanel_AddCol = new LuaFunction("add_column", AddGridPanelColumn);
    private static readonly LuaFunction GridPanel_RemoveRow = new LuaFunction("remove_row", RemoveGridPanelRow);
    private static readonly LuaFunction GridPanel_RemoveCol = new LuaFunction("remove_column", RemoveGridPanelColumn);
    private static readonly LuaFunction Button_SetPressFunction = new LuaFunction("set_press_function", SetButtonOnPressedFunction);
    private static readonly LuaFunction Button_SetHoldFunction = new LuaFunction("set_holding_function", SetButtonOnHoldingFunction);
    private static readonly LuaFunction Elem_SetHorzAlign = new LuaFunction("set_align_h", SetElementHorizontalAlignment);
    private static readonly LuaFunction Elem_SetVertAlign = new LuaFunction("set_align_v", SetElementVerticalAlignment);

    private readonly LuaFunction Button_SetTextFunction;
    private readonly LuaFunction TextBlock_SetTextFunction;
    private readonly LuaModToolMachine machine;
    private readonly LuaState state;
    private long lastGuiUpdateTicks;

    public LuaGuiFunctions(LuaModToolMachine machine, LuaState state) {
        this.machine = machine;
        this.state = state;

        this.Button_SetTextFunction = new LuaFunction("set_text", this.SetButtonText);
        this.TextBlock_SetTextFunction = new LuaFunction("set_text", this.SetTextBlockText);

        LuaTable luaTable = new LuaTable(0, 7);
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_dockpanel", CreateDockPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_stackpanel", CreateStackPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_gridpanel", CreateGridPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_button", this.CreateButton));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_text", this.CreateTextBlock));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("set_root_panel", this.SetWindowRootPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_timer", this.CreateTimer));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("destroy_timer", this.DestroyTimer));
        state.Environment[(LuaValue) "gui"] = (LuaValue) luaTable;
        state.LoadedModules[(LuaValue) "gui"] = (LuaValue) luaTable;
    }

    private static ValueTask<int> CreateDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        bool fillLast = false;
        if (ctx.ArgumentCount > 0)
            fillLast = ctx.GetArgument<bool>(0);

        MTDockPanel dockPanel = new MTDockPanel() {
            FillLast = fillLast
        };

        LuaTable table = new LuaTable(0, 4);
        table["_internal_CLR_object"] = new LuaValue(dockPanel);
        table[DockPanel_Add.Name] = DockPanel_Add;
        table[DockPanel_Remove.Name] = DockPanel_Remove;
        table[DockPanel_IndexOf.Name] = DockPanel_Add;
        table[Elem_SetHorzAlign.Name] = Elem_SetHorzAlign;
        table[Elem_SetVertAlign.Name] = Elem_SetVertAlign;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private static ValueTask<int> CreateStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        bool isVertical = false;
        if (ctx.ArgumentCount > 0)
            isVertical = ctx.GetArgument<bool>(0);

        MTStackPanel stackPanel = new MTStackPanel() {
            IsVertical = isVertical
        };

        LuaTable table = new LuaTable(0, 4);
        table["_internal_CLR_object"] = new LuaValue(stackPanel);
        table[StackPanel_Add.Name] = StackPanel_Add;
        table[StackPanel_Remove.Name] = StackPanel_Remove;
        table[StackPanel_IndexOf.Name] = StackPanel_Add;
        table[Elem_SetHorzAlign.Name] = Elem_SetHorzAlign;
        table[Elem_SetVertAlign.Name] = Elem_SetVertAlign;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private static ValueTask<int> CreateGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel stackPanel = new MTGridPanel();
        LuaTable table = new LuaTable(0, 6);
        table["_internal_CLR_object"] = new LuaValue(stackPanel);
        table[GridPanel_Add.Name] = GridPanel_Add;
        table[GridPanel_Remove.Name] = GridPanel_Remove;
        table[GridPanel_IndexOf.Name] = GridPanel_Add;
        table[GridPanel_AddRow.Name] = GridPanel_AddRow;
        table[GridPanel_AddCol.Name] = GridPanel_AddCol;
        table[GridPanel_RemoveRow.Name] = GridPanel_RemoveRow;
        table[GridPanel_RemoveCol.Name] = GridPanel_RemoveCol;
        table[Elem_SetHorzAlign.Name] = Elem_SetHorzAlign;
        table[Elem_SetVertAlign.Name] = Elem_SetVertAlign;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateButton(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        string text = ctx.GetArgument<string>(0);
        MTButton button = new MTButton() {
            Text = text,
        };

        LuaTable table = new LuaTable(0, 6);
        table["_internal_CLR_object"] = new LuaValue(button);
        table[Button_SetPressFunction.Name] = Button_SetPressFunction;
        table[Button_SetHoldFunction.Name] = Button_SetHoldFunction;
        table[this.Button_SetTextFunction.Name] = this.Button_SetTextFunction;
        table[Elem_SetHorzAlign.Name] = Elem_SetHorzAlign;
        table[Elem_SetVertAlign.Name] = Elem_SetVertAlign;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateTextBlock(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        string text = ctx.GetArgument<string>(0);
        MTTextBlock button = new MTTextBlock() {
            Text = text,
        };

        LuaTable table = new LuaTable(0, 4);
        table["_internal_CLR_object"] = new LuaValue(button);
        table[this.TextBlock_SetTextFunction.Name] = this.TextBlock_SetTextFunction;
        table[Elem_SetHorzAlign.Name] = Elem_SetHorzAlign;
        table[Elem_SetVertAlign.Name] = Elem_SetVertAlign;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private async ValueTask<int> SetWindowRootPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable panel = ctx.GetArgument<LuaTable>(0);
        BaseMTPanel element = GetElementFromTable<BaseMTPanel>(panel);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.machine.Owner.Gui.RootPanel = element, token: ct);
        return 0;
    }

    private static async ValueTask<int> AddElementToDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd;
        MTDockPanel.DockType? dockType;

        if (ctx.ArgumentCount == 2) {
            toAdd = ctx.GetArgument<LuaTable>(1);
            dockType = null;
        }
        else if (ctx.ArgumentCount == 3) {
            string type = ctx.GetArgument<string>(1);
            dockType = type switch {
                "left" => MTDockPanel.DockType.Left,
                "top" => MTDockPanel.DockType.Top,
                "right" => MTDockPanel.DockType.Right,
                "bottom" => MTDockPanel.DockType.Bottom,
                _ => throw LuaUtils.BadArgument(in ctx, 1, DockPanel_Add.Name, "left or top or right or bottom", type)
            };

            toAdd = ctx.GetArgument<LuaTable>(2);
        }
        else {
            throw LuaUtils.NotEnoughArgs(in ctx, DockPanel_Add.Name, "2 or 3");
        }

        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Add(element, dockType), token: ct);
        return 0;
    }

    private static async ValueTask<int> RemoveElementInDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, DockPanel_Remove.Name, "Negative index");

        int count = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.Count, token: ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, DockPanel_Remove.Name, "Index out of range");

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.RemoveAt(index), token: ct);
        return 0;
    }
    
    private static async ValueTask<int> IndexOfElementInDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        buffer.Span[1] = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.FindIndex(element, (e, s) => e.Item1 == s), token: ct);
        return 1;
    }

    private static async ValueTask<int> AddElementToStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd = ctx.GetArgument<LuaTable>(1);
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.Add(element), token: ct);
        return 0;
    }

    private static async ValueTask<int> RemoveElementInStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, StackPanel_Remove.Name, "Negative index");

        int count = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.Count, token: ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, StackPanel_Remove.Name, "Index out of range");

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.RemoveAt(index), token: ct);
        return 0;
    }
    
    private static async ValueTask<int> IndexOfElementInStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        buffer.Span[1] = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.IndexOf(element), token: ct);
        return 1;
    }
    
    private static async ValueTask<int> AddElementToGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd;
        int column = ctx.GetArgument<int>(1);
        if (column < 0)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_Add.Name, "Column must be greater than or equal to zero");

        int row = ctx.GetArgument<int>(2);
        if (row < 0)
            throw LuaUtils.BadArgument(in ctx, 2, GridPanel_Add.Name, "Row must be greater than or equal to zero");

        int colSpan = 1, rowSpan = 1;
        if (ctx.ArgumentCount >= 6) {
            colSpan = ctx.GetArgument<int>(3);
            if (colSpan < 1)
                throw LuaUtils.BadArgument(in ctx, 3, GridPanel_Add.Name, "Column span must be greater than zero");

            rowSpan = ctx.GetArgument<int>(4);
            if (rowSpan < 1)
                throw LuaUtils.BadArgument(in ctx, 4, GridPanel_Add.Name, "Row span must be greater than zero");

            toAdd = ctx.GetArgument<LuaTable>(5);
        }
        else {
            toAdd = ctx.GetArgument<LuaTable>(3);
        }

        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Add(element, new MTGridPanel.SlotIndex(row, column), new MTGridPanel.SpanInfo(rowSpan, colSpan)), token: ct);
        return 0;
    }

    private static async ValueTask<int> RemoveElementInGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_Remove.Name, "Negative index");

        int count = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.Count, token: ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_Remove.Name, "Index out of range");

        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.RemoveAt(index), token: ct);
        return 0;
    }
    
    private static async ValueTask<int> AddGridPanelRow(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, 1);
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Rows.Add(new MTGridPanel.RowDefinition(size)), token: ct);
        return 0;
    }  
    private static async ValueTask<int> RemoveGridPanelRow(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_RemoveRow.Name, "Negative index");

        int count = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Rows.Count, token: ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_RemoveRow.Name, "Index out of range");
        
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Rows.RemoveAt(index), token: ct);
        return 0;
    }

    private static async ValueTask<int> AddGridPanelColumn(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, 1);
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Columns.Add(new MTGridPanel.ColumnDefinition(size)), token: ct);
        return 0;
    }
    
    private static async ValueTask<int> RemoveGridPanelColumn(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_RemoveCol.Name, "Negative index");

        int count = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Columns.Count, token: ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, GridPanel_RemoveCol.Name, "Index out of range");
        
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Columns.RemoveAt(index), token: ct);
        return 0;
    }
    
    private static async ValueTask<int> IndexOfElementInGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        buffer.Span[1] = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.FindIndex(element, (e, s) => e.Element == s), token: ct);
        return 1;
    }

    public static MTGridPanel.GridDefinitionSize ParseDefinitionSize(in LuaFunctionExecutionContext ctx, int index) {
        string s = ctx.GetArgument<string>(index);

        double dval;
        s = s.ToUpperInvariant();
        if (s == "AUTO") {
            return new MTGridPanel.GridDefinitionSize(0, MTGridPanel.GridSizeType.Auto);
        }
        else if (s.EndsWith("*")) {
            ReadOnlySpan<char> valueString = s.AsSpan(0, s.Length - 1).Trim();
            if (valueString.Length > 0) {
                if (!double.TryParse(valueString, out dval))
                    throw LuaUtils.BadArgument(in ctx, index, ctx.Thread.GetCurrentFrame().Function.Name, "Invalid number: " + valueString.ToString());
            }
            else {
                dval = 1;
            }

            return new MTGridPanel.GridDefinitionSize(dval, MTGridPanel.GridSizeType.Star);
        }
        else {
            if (!double.TryParse(s, out dval))
                throw LuaUtils.BadArgument(in ctx, index, ctx.Thread.GetCurrentFrame().Function.Name, "Invalid number: " + s);
            return new MTGridPanel.GridDefinitionSize(dval, MTGridPanel.GridSizeType.Pixel);
        }
    }

    private static async ValueTask<int> SetButtonOnPressedFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction function = ctx.GetArgument<LuaFunction>(1);
        MTButton button = GetElementFromTable<MTButton>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => button.PressFunction = function, token: ct);
        return 0;
    }

    private static async ValueTask<int> SetButtonOnHoldingFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction function = ctx.GetArgument<LuaFunction>(1);
        MTButton button = GetElementFromTable<MTButton>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => button.HoldFunction = function, token: ct);
        return 0;
    }

    private async ValueTask<int> SetButtonText(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument(1).ToString();
        MTButton button = GetElementFromTable<MTButton>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => button.Text = text, token: ct, priority: this.GetSafePriorityForSetText());
        return 0;
    }

    private async ValueTask<int> SetTextBlockText(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument(1).ToString();
        MTTextBlock tb = GetElementFromTable<MTTextBlock>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => tb.Text = text, token: ct, priority: this.GetSafePriorityForSetText());
        return 0;
    }

    private DispatchPriority GetSafePriorityForSetText() {
        long timeNow = Time.GetSystemTicks();
        long last = Interlocked.Exchange(ref this.lastGuiUpdateTicks, timeNow);

        // If calling since < 1ms, then use a low priority to prevent the UI stalling
        if (last == 0 || (timeNow - last) < TimeSpan.TicksPerMillisecond) {
            return DispatchPriority.Background;
        }

        return DispatchPriority.Normal;
    }

    private static async ValueTask<int> SetElementHorizontalAlignment(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument<string>(1) ?? "";
        BaseMTElement.EnumHorizontalAlign align = text.ToUpperInvariant() switch {
            "STRETCH" or "S" => BaseMTElement.EnumHorizontalAlign.Stretch,
            "LEFT" or "L" => BaseMTElement.EnumHorizontalAlign.Left,
            "CENTER" or "C" => BaseMTElement.EnumHorizontalAlign.Center,
            "RIGHT" or "R" => BaseMTElement.EnumHorizontalAlign.Right,
            _ => throw LuaUtils.BadArgument(in ctx, 1, "set_align_h", "Unknown horizontal align: " + text)
        };

        MTButton button = GetElementFromTable<MTButton>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => button.HorizontalAlignment = align, token: ct);
        return 0;
    }

    private static async ValueTask<int> SetElementVerticalAlignment(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument<string>(1) ?? "";
        BaseMTElement.EnumVerticalAlign align = text.ToUpperInvariant() switch {
            "STRETCH" or "S" => BaseMTElement.EnumVerticalAlign.Stretch,
            "TOP" or "T" => BaseMTElement.EnumVerticalAlign.Top,
            "CENTER" or "C" => BaseMTElement.EnumVerticalAlign.Center,
            "BOTTOM" or "B" => BaseMTElement.EnumVerticalAlign.Bottom,
            _ => throw LuaUtils.BadArgument(in ctx, 1, "set_align_v", "Unknown vertical align: " + text)
        };

        MTButton button = GetElementFromTable<MTButton>(table);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => button.VerticalAlignment = align, token: ct);
        return 0;
    }

    private static BaseMTElement GetElementFromTable(LuaTable table) {
        return table["_internal_CLR_object"].Read<BaseMTElement>();
    }

    private static T GetElementFromTable<T>(LuaTable table) where T : BaseMTElement {
        return table["_internal_CLR_object"].Read<T>();
    }

    private ValueTask<int> CreateTimer(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        const int MaxSecondsInt = int.MaxValue / 1000;

        double seconds = ctx.GetArgument<double>(0);
        if (seconds <= 0.0 || seconds > MaxSecondsInt) {
            throw LuaUtils.BadArgument(in ctx, 0, "create_timer", $"Invalid interval: {seconds}. Must be > 0 and <= {MaxSecondsInt} seconds");
        }

        LuaFunction function = ctx.GetArgument<LuaFunction>(1);

        TimeSpan span = TimeSpan.FromMilliseconds(seconds * 1000.0);

        Timer timer = new Timer(static o => {
            Tuple<LuaGuiFunctions, LuaFunction> tuple = (Tuple<LuaGuiFunctions, LuaFunction>) o!;
            tuple.Item1.OnTimerCallback(tuple.Item2);
            ;
        }, new Tuple<LuaGuiFunctions, LuaFunction>(this, function), span, span);

        List<Timer> list = this.machine.UserTimers;
        lock (list) {
            list.Add(timer);
        }

        buffer.Span[0] = new LuaValue(timer);
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> DestroyTimer(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        object timerObject = ctx.GetArgument<object>(0);
        if (!(timerObject is Timer timer)) {
            throw LuaUtils.BadArgument(in ctx, 0, "destroy_timer", "Invalid timer object");
        }

        List<Timer> list = this.machine.UserTimers;
        lock (list) {
            if (!list.Remove(timer)) {
                return ValueTask.FromResult(0);
            }
        }

        timer.Dispose();
        return ValueTask.FromResult(0);
    }

    private void OnTimerCallback(LuaFunction function) {
        this.machine.PostMessage(async (ctx, o, ct) => {
            await ((LuaFunction) o!).InvokeAsync(ctx.State, Array.Empty<LuaValue>(), ct);
        }, function);
    }
}