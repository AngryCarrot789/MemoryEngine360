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
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.LuaFeatures;

public class LuaGuiFunctions {
    private static readonly LuaFunction DockPanel_Add = new LuaFunction("add", AddElementToDockPanel);
    private static readonly LuaFunction StackPanel_Add = new LuaFunction("add", AddElementToStackPanel);
    private static readonly LuaFunction GridPanel_Add = new LuaFunction("add", AddElementToGridPanel);
    private static readonly LuaFunction GridPanel_AddRow = new LuaFunction("add_row", AddGridPanelRow);
    private static readonly LuaFunction GridPanel_AddCol = new LuaFunction("add_column", AddGridPanelColumn);
    private static readonly LuaFunction Button_SetPressFunction = new LuaFunction("set_press_function", SetButtonOnPressedFunction);
    private static readonly LuaFunction Button_SetHoldFunction = new LuaFunction("set_holding_function", SetButtonOnHoldingFunction);
    private static readonly LuaFunction Elem_SetHorzAlign = new LuaFunction("set_align_h", SetElementHorizontalAlignment);
    private static readonly LuaFunction Elem_SetVertAlign = new LuaFunction("set_align_v", SetElementVerticalAlignment);

    private readonly LuaFunction Button_SetTextFunction;
    private readonly LuaFunction TextBlock_SetTextFunction;
    private readonly LuaModToolMachine machine;
    private readonly LuaState state;
    private long lastSet;

    public LuaGuiFunctions(LuaModToolMachine machine, LuaState state) {
        this.machine = machine;
        this.state = state;

        this.Button_SetTextFunction = new LuaFunction("set_text", this.SetButtonText);
        this.TextBlock_SetTextFunction = new LuaFunction("set_text", this.SetTextBlockText);

        LuaTable luaTable = new LuaTable(0, 6);
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("create_dockpanel", CreateDockPanel));
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("create_stackpanel", CreateStackPanel));
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("create_gridpanel", CreateGridPanel));
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("create_button", this.CreateButton));
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("create_text", this.CreateTextBlock));
        LuaModToolMachine.AssignFunction(luaTable, new LuaFunction("set_root_panel", this.SetWindowRootPanel));
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
        table[GridPanel_AddRow.Name] = GridPanel_AddRow;
        table[GridPanel_AddCol.Name] = GridPanel_AddCol;
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
                _ => throw LuaArgUtils.InvalidOperation(in ctx, "Unknown dock type: " + type)
            };

            toAdd = ctx.GetArgument<LuaTable>(2);
        }
        else {
            throw LuaArgUtils.InvalidOperation(in ctx, "Invalid arg count");
        }

        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Add(element, dockType), token: ct);
        return 0;
    }

    private static async ValueTask<int> AddElementToStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd = ctx.GetArgument<LuaTable>(1);
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => targetPanel.Children.Add(element), token: ct);
        return 0;
    }

    private static async ValueTask<int> AddElementToGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd;
        int row = ctx.GetArgument<int>(1);
        if (row < 0)
            throw LuaArgUtils.InvalidOperation(in ctx, "Row span must be greater than or equal to zero");

        int column = ctx.GetArgument<int>(2);
        if (column < 0)
            throw LuaArgUtils.InvalidOperation(in ctx, "Row span must be greater than or equal to zero");

        int rowSpan = 1, colSpan = 1;
        if (ctx.ArgumentCount >= 6) {
            rowSpan = ctx.GetArgument<int>(3);
            if (rowSpan < 1)
                throw LuaArgUtils.InvalidOperation(in ctx, "Row span must be greater than zero");

            colSpan = ctx.GetArgument<int>(4);
            if (colSpan < 1)
                throw LuaArgUtils.InvalidOperation(in ctx, "Column span must be greater than zero");

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

    private static async ValueTask<int> AddGridPanelRow(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, ctx.GetArgument<string>(1));
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        targetPanel.Rows.Add(new MTGridPanel.RowDefinition(size));
        return 0;
    }

    private static async ValueTask<int> AddGridPanelColumn(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, ctx.GetArgument<string>(1));
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        targetPanel.Columns.Add(new MTGridPanel.ColumnDefinition(size));
        return 0;
    }

    public static MTGridPanel.GridDefinitionSize ParseDefinitionSize(in LuaFunctionExecutionContext ctx, string s) {
        double dval;
        s = s.ToUpperInvariant();
        if (s == "AUTO") {
            return new MTGridPanel.GridDefinitionSize(0, MTGridPanel.GridSizeType.Auto);
        }
        else if (s.EndsWith("*")) {
            string valueString = s.Substring(0, s.Length - 1).Trim();
            if (valueString.Length > 0) {
                if (!double.TryParse(valueString, out dval))
                    throw LuaArgUtils.InvalidOperation(ctx, "Invalid number: " + valueString);
            }
            else {
                dval = 1;
            }

            return new MTGridPanel.GridDefinitionSize(dval, MTGridPanel.GridSizeType.Star);
        }
        else {
            if (!double.TryParse(s, out dval))
                throw LuaArgUtils.InvalidOperation(ctx, "Invalid number: " + s);
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
        long last = Interlocked.Exchange(ref this.lastSet, timeNow);

        // If calling since < 1ms, then use a low priority to prevent the UI stalling
        if (last == 0 || (timeNow - last) < Time.TICK_PER_MILLIS) {
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
            _ => throw LuaArgUtils.InvalidOperation(in ctx, "Unknown horizontal align: " + text)
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
            _ => throw LuaArgUtils.InvalidOperation(in ctx, "Unknown vertical align: " + text)
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
}