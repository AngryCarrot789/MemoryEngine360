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
    public const string CLRElementObjectKey = "_internal_CLR_object";

    private readonly LuaFunction DockPanel_Add;
    private readonly LuaFunction StackPanel_Add;
    private readonly LuaFunction GridPanel_Add;
    private readonly LuaFunction DockPanel_Remove;
    private readonly LuaFunction StackPanel_Remove;
    private readonly LuaFunction GridPanel_Remove;
    private readonly LuaFunction DockPanel_IndexOf;
    private readonly LuaFunction StackPanel_IndexOf;
    private readonly LuaFunction GridPanel_IndexOf;
    private readonly LuaFunction GridPanel_AddRow;
    private readonly LuaFunction GridPanel_AddCol;
    private readonly LuaFunction GridPanel_RemoveRow;
    private readonly LuaFunction GridPanel_RemoveCol;
    private readonly LuaFunction Button_SetPressFunction;
    private readonly LuaFunction Button_SetHoldFunction;
    private readonly LuaFunction Elem_SetHorzAlign;
    private readonly LuaFunction Elem_SetVertAlign;
    private readonly LuaFunction Elem_SetWidth;
    private readonly LuaFunction Elem_SetHeight;
    private readonly LuaFunction Elem_SetMinWidth;
    private readonly LuaFunction Elem_SetMaxWidth;
    private readonly LuaFunction Elem_SetMinHeight;
    private readonly LuaFunction Elem_SetMaxHeight;
    private readonly LuaFunction Button_SetTextFunction;
    private readonly LuaFunction TextBlock_SetTextFunction;
    private readonly LuaFunction TextBox_SetTextFunction;
    private readonly LuaFunction TextBox_SetAddressFunction;
    private readonly LuaFunction TextBox_SetNumberFunction;
    private readonly LuaFunction TextBox_GetValueFunction;
    private readonly LuaFunction TextBox_SetValueChangeFunction;
    private readonly LuaFunction TextBox_SetKeyPressFunction;
    private readonly LuaModToolMachine machine;
    private readonly LuaState state;
    private long lastGuiUpdateTicks;

    public LuaGuiFunctions(LuaModToolMachine machine, LuaState state) {
        this.machine = machine;
        this.state = state;

        this.DockPanel_Add = new LuaFunction("add", this.AddElementToDockPanel);
        this.StackPanel_Add = new LuaFunction("add", this.AddElementToStackPanel);
        this.GridPanel_Add = new LuaFunction("add", this.AddElementToGridPanel);
        this.DockPanel_Remove = new LuaFunction("remove_at", this.RemoveElementInDockPanel);
        this.StackPanel_Remove = new LuaFunction("remove_at", this.RemoveElementInStackPanel);
        this.GridPanel_Remove = new LuaFunction("remove_at", this.RemoveElementInGridPanel);
        this.DockPanel_IndexOf = new LuaFunction("index_of", this.IndexOfElementInDockPanel);
        this.StackPanel_IndexOf = new LuaFunction("index_of", this.IndexOfElementInStackPanel);
        this.GridPanel_IndexOf = new LuaFunction("index_of", this.IndexOfElementInGridPanel);
        this.GridPanel_AddRow = new LuaFunction("add_row", this.AddGridPanelRow);
        this.GridPanel_AddCol = new LuaFunction("add_column", this.AddGridPanelColumn);
        this.GridPanel_RemoveRow = new LuaFunction("remove_row", this.RemoveGridPanelRow);
        this.GridPanel_RemoveCol = new LuaFunction("remove_column", this.RemoveGridPanelColumn);
        this.Button_SetPressFunction = new LuaFunction("set_press_function", this.SetButtonOnPressedFunction);
        this.Button_SetHoldFunction = new LuaFunction("set_holding_function", this.SetButtonOnHoldingFunction);
        this.Elem_SetHorzAlign = new LuaFunction("set_align_h", this.SetElementHorizontalAlignment);
        this.Elem_SetVertAlign = new LuaFunction("set_align_v", this.SetElementVerticalAlignment);
        this.Elem_SetWidth = new LuaFunction("set_width", this.SetElementWidth);
        this.Elem_SetHeight = new LuaFunction("set_height", this.SetElementHeight);
        this.Elem_SetMinWidth = new LuaFunction("set_minwidth", this.SetElementMinWidth);
        this.Elem_SetMaxWidth = new LuaFunction("set_maxwidth", this.SetElementMaxWidth);
        this.Elem_SetMinHeight = new LuaFunction("set_minheight", this.SetElementMinHeight);
        this.Elem_SetMaxHeight = new LuaFunction("set_maxheight", this.SetElementMaxHeight);
        this.Button_SetTextFunction = new LuaFunction("set_text", this.SetButtonText);
        this.TextBlock_SetTextFunction = new LuaFunction("set_text", this.SetTextBlockText);
        this.TextBox_SetTextFunction = new LuaFunction("set_text", this.SetTextBoxText);
        this.TextBox_SetAddressFunction = new LuaFunction("set_address", this.SetTextBoxAddress);
        this.TextBox_SetNumberFunction = new LuaFunction("set_number", this.SetTextBoxNumber);
        this.TextBox_GetValueFunction = new LuaFunction("get_value", this.GetTextBoxValue);
        this.TextBox_SetValueChangeFunction = new LuaFunction("set_value_change_function", this.SetTextBoxValueChangedFunction);
        this.TextBox_SetKeyPressFunction = new LuaFunction("set_keypress_function", this.SetTextBoxKeyPressFunction);

        LuaTable luaTable = new LuaTable(0, 9);
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_dockpanel", this.CreateDockPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_stackpanel", this.CreateStackPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_gridpanel", this.CreateGridPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_button", this.CreateButton));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_text", this.CreateTextBlock));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_textbox", this.CreateTextBox));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("set_root_panel", this.SetWindowRootPanel));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("create_timer", this.CreateTimer));
        LuaUtils.AssignFunction(luaTable, new LuaFunction("destroy_timer", this.DestroyTimer));
        state.Environment[(LuaValue) "gui"] = (LuaValue) luaTable;
        state.LoadedModules[(LuaValue) "gui"] = (LuaValue) luaTable;
    }

    private DispatchPriority GetPriorityForAntiStall() {
        long timeNow = Time.GetSystemTicks();
        long last = Interlocked.Exchange(ref this.lastGuiUpdateTicks, timeNow);

        // If calling since < 1ms, then use a low priority to prevent the UI stalling
        if (last == 0 || (timeNow - last) < TimeSpan.TicksPerMillisecond) {
            return DispatchPriority.Background;
        }

        return DispatchPriority.Normal;
    }

    private Task InvokeOnMainThreadAsync(Action action, CancellationToken cancellation) {
        return ApplicationPFX.Instance.Dispatcher.InvokeAsync(action, token: cancellation, priority: this.GetPriorityForAntiStall());
    }

    private Task<T> InvokeOnMainThreadAsync<T>(Func<T> function, CancellationToken cancellation) {
        return ApplicationPFX.Instance.Dispatcher.InvokeAsync(function, token: cancellation, priority: this.GetPriorityForAntiStall());
    }

    private ValueTask<int> CreateDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        bool fillLast = false;
        if (ctx.ArgumentCount > 0)
            fillLast = ctx.GetArgument<bool>(0);

        LuaTable table = new LuaTable(0, 12);
        MTDockPanel dockPanel = new MTDockPanel() {
            FillLast = fillLast, ownerTable = table
        };

        table[CLRElementObjectKey] = new LuaValue(dockPanel);
        table[this.DockPanel_Add.Name] = this.DockPanel_Add;
        table[this.DockPanel_Remove.Name] = this.DockPanel_Remove;
        table[this.DockPanel_IndexOf.Name] = this.DockPanel_Add;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;

        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        bool isVertical = false;
        if (ctx.ArgumentCount > 0)
            isVertical = ctx.GetArgument<bool>(0);

        LuaTable table = new LuaTable(0, 12);
        MTStackPanel stackPanel = new MTStackPanel() {
            IsVertical = isVertical, ownerTable = table
        };

        table[CLRElementObjectKey] = new LuaValue(stackPanel);
        table[this.StackPanel_Add.Name] = this.StackPanel_Add;
        table[this.StackPanel_Remove.Name] = this.StackPanel_Remove;
        table[this.StackPanel_IndexOf.Name] = this.StackPanel_Add;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;


        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = new LuaTable(0, 16);
        MTGridPanel stackPanel = new MTGridPanel() {
            ownerTable = table
        };

        table[CLRElementObjectKey] = new LuaValue(stackPanel);
        table[this.GridPanel_Add.Name] = this.GridPanel_Add;
        table[this.GridPanel_Remove.Name] = this.GridPanel_Remove;
        table[this.GridPanel_IndexOf.Name] = this.GridPanel_Add;
        table[this.GridPanel_AddRow.Name] = this.GridPanel_AddRow;
        table[this.GridPanel_AddCol.Name] = this.GridPanel_AddCol;
        table[this.GridPanel_RemoveRow.Name] = this.GridPanel_RemoveRow;
        table[this.GridPanel_RemoveCol.Name] = this.GridPanel_RemoveCol;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;


        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateButton(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        string text = ctx.GetArgument<string>(0);
        LuaTable table = new LuaTable(0, 12);
        MTButton button = new MTButton() {
            Text = text, ownerTable = table
        };

        table[CLRElementObjectKey] = new LuaValue(button);
        table[this.Button_SetPressFunction.Name] = this.Button_SetPressFunction;
        table[this.Button_SetHoldFunction.Name] = this.Button_SetHoldFunction;
        table[this.Button_SetTextFunction.Name] = this.Button_SetTextFunction;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;


        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateTextBlock(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        string text = ctx.GetArgument<string>(0);
        LuaTable table = new LuaTable(0, 10);
        MTTextBlock button = new MTTextBlock() {
            Text = text, ownerTable = table
        };

        table[CLRElementObjectKey] = new LuaValue(button);
        table[this.TextBlock_SetTextFunction.Name] = this.TextBlock_SetTextFunction;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;


        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private ValueTask<int> CreateTextBox(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        string type = ctx.GetArgument<string>(0);
        MTTextBox.EnumContentType contentType = type switch {
            "text" => MTTextBox.EnumContentType.Text,
            "address" => MTTextBox.EnumContentType.UInt32,
            "number" => MTTextBox.EnumContentType.Number,
            _ => throw LuaUtils.BadArgument(in ctx, 1, "create_textbox", "text or address or number", type)
        };

        LuaTable table = new LuaTable(0, 14);
        MTTextBox textBox = new MTTextBox() {
            ownerTable = table
        };

        textBox.SetDefaultContent(contentType);

        table[CLRElementObjectKey] = new LuaValue(textBox);
        table[this.TextBox_SetTextFunction.Name] = this.TextBox_SetTextFunction;
        table[this.TextBox_SetAddressFunction.Name] = this.TextBox_SetAddressFunction;
        table[this.TextBox_SetNumberFunction.Name] = this.TextBox_SetNumberFunction;
        table[this.TextBox_GetValueFunction.Name] = this.TextBox_GetValueFunction;
        table[this.TextBox_SetValueChangeFunction.Name] = this.TextBox_SetValueChangeFunction;
        table[this.TextBox_SetKeyPressFunction.Name] = this.TextBox_SetKeyPressFunction;
        table[this.Elem_SetHorzAlign.Name] = this.Elem_SetHorzAlign;
        table[this.Elem_SetVertAlign.Name] = this.Elem_SetVertAlign;
        table[this.Elem_SetWidth.Name] = this.Elem_SetWidth;
        table[this.Elem_SetHeight.Name] = this.Elem_SetHeight;
        table[this.Elem_SetMinWidth.Name] = this.Elem_SetMinWidth;
        table[this.Elem_SetMaxWidth.Name] = this.Elem_SetMaxWidth;
        table[this.Elem_SetMinHeight.Name] = this.Elem_SetMinHeight;
        table[this.Elem_SetMaxHeight.Name] = this.Elem_SetMaxHeight;


        buffer.Span[0] = table;
        return ValueTask.FromResult(1);
    }

    private async ValueTask<int> SetWindowRootPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable panel = ctx.GetArgument<LuaTable>(0);
        BaseMTPanel element = GetElementFromTable<BaseMTPanel>(panel);
        await this.InvokeOnMainThreadAsync(() => this.machine.Owner.Gui.RootPanel = element, ct);
        return 0;
    }

    private async ValueTask<int> AddElementToDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
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
                _ => throw LuaUtils.BadArgument(in ctx, 1, this.DockPanel_Add.Name, "left or top or right or bottom", type)
            };

            toAdd = ctx.GetArgument<LuaTable>(2);
        }
        else {
            throw LuaUtils.NotEnoughArgs(in ctx, this.DockPanel_Add.Name, "expected 2 or 3, got " + ctx.ArgumentCount);
        }

        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await this.InvokeOnMainThreadAsync(() => targetPanel.Add(element, dockType), ct);
        return 0;
    }

    private async ValueTask<int> RemoveElementInDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.DockPanel_Remove.Name, "Negative index");

        int count = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.Count, ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, this.DockPanel_Remove.Name, "Index out of range");

        await this.InvokeOnMainThreadAsync(() => targetPanel.Children.RemoveAt(index), ct);
        return 0;
    }

    private async ValueTask<int> IndexOfElementInDockPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTDockPanel targetPanel = GetElementFromTable<MTDockPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        int whyDoesThisHaveToBeAVariable = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.FindIndex(element, (e, s) => e.Item1 == s), ct);
        buffer.Span[1] = whyDoesThisHaveToBeAVariable;
        return 1;
    }

    private async ValueTask<int> AddElementToStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd = ctx.GetArgument<LuaTable>(1);
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await this.InvokeOnMainThreadAsync(() => targetPanel.Children.Add(element), ct);
        return 0;
    }

    private async ValueTask<int> RemoveElementInStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.StackPanel_Remove.Name, "Negative index");

        int count = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.Count, ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, this.StackPanel_Remove.Name, "Index out of range");

        await this.InvokeOnMainThreadAsync(() => targetPanel.Children.RemoveAt(index), ct);
        return 0;
    }

    private async ValueTask<int> IndexOfElementInStackPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTStackPanel targetPanel = GetElementFromTable<MTStackPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        int whyDoesThisHaveToBeAVariable = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.IndexOf(element), ct);
        buffer.Span[1] = whyDoesThisHaveToBeAVariable;
        return 1;
    }

    private async ValueTask<int> AddElementToGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaTable toAdd;
        int column = ctx.GetArgument<int>(1);
        if (column < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_Add.Name, "Column must be greater than or equal to zero");

        int row = ctx.GetArgument<int>(2);
        if (row < 0)
            throw LuaUtils.BadArgument(in ctx, 2, this.GridPanel_Add.Name, "Row must be greater than or equal to zero");

        int colSpan = 1, rowSpan = 1;
        if (ctx.ArgumentCount >= 6) {
            colSpan = ctx.GetArgument<int>(3);
            if (colSpan < 1)
                throw LuaUtils.BadArgument(in ctx, 3, this.GridPanel_Add.Name, "Column span must be greater than zero");

            rowSpan = ctx.GetArgument<int>(4);
            if (rowSpan < 1)
                throw LuaUtils.BadArgument(in ctx, 4, this.GridPanel_Add.Name, "Row span must be greater than zero");

            toAdd = ctx.GetArgument<LuaTable>(5);
        }
        else {
            toAdd = ctx.GetArgument<LuaTable>(3);
        }

        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        BaseMTElement element = GetElementFromTable(toAdd);
        await this.InvokeOnMainThreadAsync(() => targetPanel.Add(element, new MTGridPanel.SlotIndex(row, column), new MTGridPanel.SpanInfo(rowSpan, colSpan)), ct);
        return 0;
    }

    private async ValueTask<int> RemoveElementInGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_Remove.Name, "Negative index");

        int count = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.Count, ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_Remove.Name, "Index out of range");

        await this.InvokeOnMainThreadAsync(() => targetPanel.Children.RemoveAt(index), ct);
        return 0;
    }

    private async ValueTask<int> AddGridPanelRow(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, 1);
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        await this.InvokeOnMainThreadAsync(() => targetPanel.Rows.Add(new MTGridPanel.RowDefinition(size)), ct);
        return 0;
    }

    private async ValueTask<int> RemoveGridPanelRow(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_RemoveRow.Name, "Negative index");

        int count = await this.InvokeOnMainThreadAsync(() => targetPanel.Rows.Count, ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_RemoveRow.Name, "Index out of range");

        await this.InvokeOnMainThreadAsync(() => targetPanel.Rows.RemoveAt(index), ct);
        return 0;
    }

    private async ValueTask<int> AddGridPanelColumn(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTGridPanel.GridDefinitionSize size = ParseDefinitionSize(in ctx, 1);
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(table);
        await this.InvokeOnMainThreadAsync(() => targetPanel.Columns.Add(new MTGridPanel.ColumnDefinition(size)), ct);
        return 0;
    }

    private async ValueTask<int> RemoveGridPanelColumn(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        int index = ctx.GetArgument<int>(1);
        if (index < 0)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_RemoveCol.Name, "Negative index");

        int count = await this.InvokeOnMainThreadAsync(() => targetPanel.Columns.Count, ct);
        if (index >= count)
            throw LuaUtils.BadArgument(in ctx, 1, this.GridPanel_RemoveCol.Name, "Index out of range");

        await this.InvokeOnMainThreadAsync(() => targetPanel.Columns.RemoveAt(index), ct);
        return 0;
    }

    private async ValueTask<int> IndexOfElementInGridPanel(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        MTGridPanel targetPanel = GetElementFromTable<MTGridPanel>(ctx.GetArgument<LuaTable>(0));
        BaseMTElement element = GetElementFromTable(ctx.GetArgument<LuaTable>(1));
        int whyDoesThisHaveToBeAVariable = await this.InvokeOnMainThreadAsync(() => targetPanel.Children.FindIndex(element, (e, s) => e.Element == s), ct);
        buffer.Span[1] = whyDoesThisHaveToBeAVariable;
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

    private async ValueTask<int> SetButtonOnPressedFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction? function = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<LuaFunction>(1);
        MTButton button = GetElementFromTable<MTButton>(table);
        await this.InvokeOnMainThreadAsync(() => button.PressFunction = function, ct);
        return 0;
    }

    private async ValueTask<int> SetButtonOnHoldingFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction? function = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<LuaFunction>(1);
        MTButton button = GetElementFromTable<MTButton>(table);
        await this.InvokeOnMainThreadAsync(() => button.HoldFunction = function, ct);
        return 0;
    }

    private async ValueTask<int> SetButtonText(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument(1).ToString();
        MTButton button = GetElementFromTable<MTButton>(table);
        await this.InvokeOnMainThreadAsync(() => button.Text = text, ct);
        return 0;
    }

    private async ValueTask<int> SetTextBlockText(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument(1).ToString();
        MTTextBlock tb = GetElementFromTable<MTTextBlock>(table);
        await this.InvokeOnMainThreadAsync(() => tb.Text = text, ct);
        return 0;
    }

    private async ValueTask<int> SetTextBoxText(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument(1).ToString();
        MTTextBox tb = GetElementFromTable<MTTextBox>(table);
        await this.InvokeOnMainThreadAsync(() => tb.SetText(text), ct);
        return 0;
    }

    private async ValueTask<int> SetTextBoxAddress(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        uint address = LuaUtils.GetUIntFromValue(in ctx, 1);
        MTTextBox tb = GetElementFromTable<MTTextBox>(table);
        await this.InvokeOnMainThreadAsync(() => tb.SetUInt32(address), ct);
        return 0;
    }

    private async ValueTask<int> SetTextBoxNumber(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        double number = ctx.GetArgument<double>(1);
        MTTextBox tb = GetElementFromTable<MTTextBox>(table);
        await this.InvokeOnMainThreadAsync(() => tb.SetNumber(number), ct);
        return 0;
    }

    private async ValueTask<int> GetTextBoxValue(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTTextBox tb = GetElementFromTable<MTTextBox>(table);
        LuaValue value = await this.InvokeOnMainThreadAsync(() => tb.GetLuaValue(), ct);
        buffer.Span[0] = value;
        return 1;
    }

    private async ValueTask<int> SetTextBoxValueChangedFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction? function = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<LuaFunction>(1);
        MTTextBox button = GetElementFromTable<MTTextBox>(table);
        await this.InvokeOnMainThreadAsync(() => button.ValueChangeFunction = function, ct);
        return 0;
    }
    

    private async ValueTask<int> SetTextBoxKeyPressFunction(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        LuaFunction? function = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<LuaFunction>(1);
        MTTextBox button = GetElementFromTable<MTTextBox>(table);
        await this.InvokeOnMainThreadAsync(() => button.KeyPressFunction = function, ct);
        return 0;
    }

    private async ValueTask<int> SetElementHorizontalAlignment(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument<string>(1) ?? "";
        EnumHorizontalAlign align = text.ToUpperInvariant() switch {
            "STRETCH" or "S" => EnumHorizontalAlign.Stretch,
            "LEFT" or "L" => EnumHorizontalAlign.Left,
            "CENTER" or "C" => EnumHorizontalAlign.Center,
            "RIGHT" or "R" => EnumHorizontalAlign.Right,
            _ => throw LuaUtils.BadArgument(in ctx, 1, "set_align_h", "Unknown horizontal align: " + text)
        };

        MTButton button = GetElementFromTable<MTButton>(table);
        await this.InvokeOnMainThreadAsync(() => button.HorizontalAlignment = align, ct);
        return 0;
    }

    private async ValueTask<int> SetElementVerticalAlignment(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        string text = ctx.GetArgument<string>(1) ?? "";
        EnumVerticalAlign align = text.ToUpperInvariant() switch {
            "STRETCH" or "S" => EnumVerticalAlign.Stretch,
            "TOP" or "T" => EnumVerticalAlign.Top,
            "CENTER" or "C" => EnumVerticalAlign.Center,
            "BOTTOM" or "B" => EnumVerticalAlign.Bottom,
            _ => throw LuaUtils.BadArgument(in ctx, 1, "set_align_v", "Unknown vertical align: " + text)
        };

        MTButton button = GetElementFromTable<MTButton>(table);
        await this.InvokeOnMainThreadAsync(() => button.VerticalAlignment = align, ct);
        return 0;
    }

    private async ValueTask<int> SetElementWidth(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.Width = value, ct);
        return 0;
    }

    private async ValueTask<int> SetElementHeight(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.Height = value, ct);
        return 0;
    }

    private async ValueTask<int> SetElementMinWidth(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.MinWidth = value, ct);
        return 0;
    }

    private async ValueTask<int> SetElementMinHeight(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.MinHeight = value, ct);
        return 0;
    }

    private async ValueTask<int> SetElementMaxWidth(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.MaxWidth = value, ct);
        return 0;
    }

    private async ValueTask<int> SetElementMaxHeight(LuaFunctionExecutionContext ctx, Memory<LuaValue> buffer, CancellationToken ct) {
        LuaTable table = ctx.GetArgument<LuaTable>(0);
        MTButton button = GetElementFromTable<MTButton>(table);
        double? value = ctx.GetArgument(1).Type == LuaValueType.Nil ? null : ctx.GetArgument<double>(1);
        await this.InvokeOnMainThreadAsync(() => button.MaxHeight = value, ct);
        return 0;
    }

    private static BaseMTElement GetElementFromTable(LuaTable table) {
        return table[CLRElementObjectKey].Read<BaseMTElement>();
    }

    private static T GetElementFromTable<T>(LuaTable table) where T : BaseMTElement {
        return table[CLRElementObjectKey].Read<T>();
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