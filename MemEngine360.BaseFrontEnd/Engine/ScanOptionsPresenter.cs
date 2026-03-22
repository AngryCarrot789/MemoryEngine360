// 
// Copyright (c) 2026-2026 REghZy
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

using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;
using PFXToolKitUI.Avalonia.Bindings.TextBoxes;
using PFXToolKitUI.PropertyEditing.DataTransfer.Enums;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Engine;

public class ScanOptionsPresenter {
    private readonly IBinder<ScanningProcessor> isScanningBinder =
        new EventUpdateBinder<ScanningProcessor>(
            nameof(ScanningProcessor.IsScanningChanged),
            (b) => {
                EngineView w = (EngineView) b.Control;
                w.PART_Grid_ScanInput.IsEnabled = !b.Model.IsScanning;
                w.PART_Grid_ScanOptions.IsEnabled = !b.Model.IsScanning;
                w.UpdateScanResultCounterText();
            });

    private readonly IBinder<ScanningProcessor> scanAddressBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.StartAddress:X8}", async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        if (value == b.Model.StartAddress) {
            return true;
        }

        if (value + b.Model.ScanLength < value) {
            return await OnAddressOrLengthOutOfRange(b.Model, value, b.Model.ScanLength);
        }
        else {
            b.Model.SetScanRange(value, b.Model.ScanLength);
            return true;
        }
    });

    private readonly IBinder<ScanningProcessor> scanLengthBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.ScanRangeChanged), (b) => $"{b.Model.ScanLength:X8}", async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        if (value == b.Model.ScanLength) {
            return true;
        }
        else if (b.Model.StartAddress + value < value) {
            return await OnAddressOrLengthOutOfRange(b.Model, b.Model.StartAddress, value);
        }
        else {
            b.Model.SetScanRange(b.Model.StartAddress, value);
            return true;
        }
    });

    private readonly IBinder<ScanningProcessor> nextScanOverReadBinder = new TextBoxToEventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.NextScanOverReadChanged), (b) => $"{b.Model.NextScanOverRead}", async (b, x) => {
        if (!AddressParsing.TryParse32(x, out uint value, out string? error, canParseAsExpression: true, hexByDefault: false)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid value", error, defaultButton: MessageBoxResult.OK, icon: MessageBoxIcons.ErrorIcon);
            return false;
        }

        b.Model.NextScanOverRead = value;
        return true;
    });

    private readonly IBinder<ScanningProcessor> alignmentBinder = new EventUpdateBinder<ScanningProcessor>(nameof(ScanningProcessor.AlignmentChanged), (b) => ((EngineView) b.Control).PART_ScanOption_Alignment.Content = b.Model.Alignment.ToString());
    private readonly IBinder<ScanningProcessor> pauseXboxBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.PauseConsoleDuringScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.PauseConsoleDuringScan, (b) => b.Model.PauseConsoleDuringScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> scanMemoryPagesBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanMemoryPagesChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanMemoryPages, (b) => b.Model.ScanMemoryPages = ((ToggleButton) b.Control).IsChecked == true);

    private readonly EventPropertyEnumBinder<FloatScanOption> floatScanModeBinder = new EventPropertyEnumBinder<FloatScanOption>(typeof(ScanningProcessor), nameof(ScanningProcessor.FloatScanModeChanged), (x) => ((ScanningProcessor) x).FloatScanOption, (x, v) => ((ScanningProcessor) x).FloatScanOption = v);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(ScanningProcessor), nameof(ScanningProcessor.StringScanModeChanged), (x) => ((ScanningProcessor) x).StringScanOption, (x, v) => ((ScanningProcessor) x).StringScanOption = v);
    private readonly IBinder<ScanningProcessor> int_isHexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.IsIntInputHexadecimalChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.IsIntInputHexadecimal, (b) => b.Model.IsIntInputHexadecimal = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> int_isUnsignedBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.IsIntInputUnsignedChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.IsIntInputUnsigned, (b) => b.Model.IsIntInputUnsigned = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> useFirstValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UseFirstValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UseFirstValueForNextScan, (b) => b.Model.UseFirstValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> usePrevValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UsePreviousValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UsePreviousValueForNextScan, (b) => b.Model.UsePreviousValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(ScanningProcessor), nameof(ScanningProcessor.DataTypeChanged), (x) => ((ScanningProcessor) x).DataType, (x, y) => ((ScanningProcessor) x).DataType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder1 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder2 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);

    private readonly IBinder<ScanningProcessor> selectedTabIndexBinder;
    private readonly IBinder<ScanningProcessor> scanForAnyBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.ScanForAnyDataTypeChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.ScanForAnyDataType, (b) => b.Model.ScanForAnyDataType = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> useExpressionBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UseExpressionParsingChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UseExpressionParsing, (b) => b.Model.UseExpressionParsing = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> inputValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenABinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenBBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputBChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputB, (b) => b.Model.InputB = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> stringIgnoreCaseBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.StringIgnoreCaseChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.StringIgnoreCase, (b) => b.Model.StringIgnoreCase = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<UnknownDataTypeOptions> canScanFloatBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForFloatChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForFloat, (b) => b.Model.CanSearchForFloat = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<UnknownDataTypeOptions> canScanDoubleBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForDoubleChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForDouble, (b) => b.Model.CanSearchForDouble = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<UnknownDataTypeOptions> canScanStringBinder = new AvaloniaPropertyToEventPropertyBinder<UnknownDataTypeOptions>(ToggleButton.IsCheckedProperty, nameof(UnknownDataTypeOptions.CanSearchForStringChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.CanSearchForString, (b) => b.Model.CanSearchForString = ((ToggleButton) b.Control).IsChecked == true);

    private readonly IBinder<ScanningProcessor> updatedEnabledControlsBinder = new EventUpdateBinder<ScanningProcessor>(
        [
            nameof(ScanningProcessor.ScanForAnyDataTypeChanged),
            nameof(ScanningProcessor.HasFirstScanChanged),
            nameof(ScanningProcessor.UseExpressionParsingChanged)
        ],
        b => {
            EngineView view = (EngineView) b.Control;
            bool scanAny = b.Model.ScanForAnyDataType;
            view.PART_DataTypeCombo.IsEnabled = !scanAny && !b.Model.HasDoneFirstScan;
            view.PART_TabItemInteger.IsEnabled = !scanAny;
            view.PART_TabItemFloat.IsEnabled = !scanAny;
            view.PART_TabItemString.IsEnabled = !scanAny && !b.Model.UseExpressionParsing;
            view.PART_TabItemUnknown.IsEnabled = !b.Model.UseExpressionParsing;
            view.PART_UseFirstValue.IsEnabled = !scanAny && b.Model.HasDoneFirstScan;
            view.PART_UsePreviousValue.IsEnabled = !scanAny && b.Model.HasDoneFirstScan;
            view.PART_ToggleUnknownDataType.IsEnabled = !b.Model.HasDoneFirstScan || scanAny;
            view.PART_ExpressionNamingHint.IsVisible = b.Model.UseExpressionParsing;
        });
    
    private static readonly DataParameterEnumInfo<NumericScanType> NumericScanTypeEnumInfo = DataParameterEnumInfo<NumericScanType>.All(new Dictionary<NumericScanType, string>() {
        { NumericScanType.Equals, "Equals" },
        { NumericScanType.NotEquals, "Not Equals" },
        { NumericScanType.LessThan, "Less Than" },
        { NumericScanType.LessThanOrEquals, "Less Than Or Equal" },
        { NumericScanType.GreaterThan, "Greater Than" },
        { NumericScanType.GreaterThanOrEquals, "Greater Than Or Equal" },
        { NumericScanType.Between, "Between" },
        { NumericScanType.NotBetween, "Not Between" }
    });

    private DataType lastIntegerDataType = DataType.Int32, lastFloatDataType = DataType.Float;
    private readonly EngineView view;

    public AsyncRelayCommand EditAlignmentCommand { get; }

    public ScanOptionsPresenter(EngineView view) {
        this.view = view;

        this.EditAlignmentCommand = new AsyncRelayCommand(this.EditAlignmentAsync);
        this.stringIgnoreCaseBinder.AttachControl(this.view.PART_IgnoreCases);
        this.floatScanModeBinder.Assign(this.view.PART_DTFloat_Truncate, FloatScanOption.TruncateToQuery);
        this.floatScanModeBinder.Assign(this.view.PART_DTFloat_RoundToQuery, FloatScanOption.RoundToQuery);
        this.stringScanModeBinder.Assign(this.view.PART_DTString_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.view.PART_DTString_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.view.PART_DTString_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.view.PART_DTString_UTF32, StringType.UTF32);

        this.selectedTabIndexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(
            SelectingItemsControl.SelectedIndexProperty,
            [nameof(ScanningProcessor.DataTypeChanged), nameof(ScanningProcessor.ScanForAnyDataTypeChanged)],
            this.UpdateTabControlSelectedIndex, this.UpdateModeForTabControlSelectedIndex);
    }

    public void OnViewLoaded() {
        ScanningProcessor processor = this.view.MemoryEngine.ScanningProcessor;

        this.isScanningBinder.Attach(this.view, processor);
        this.scanAddressBinder.Attach(this.view.PART_ScanOption_StartAddress, processor);
        this.scanLengthBinder.Attach(this.view.PART_ScanOption_Length, processor);
        this.nextScanOverReadBinder.Attach(this.view.PART_NextScanOverRead, processor);
        this.alignmentBinder.Attach(this.view, processor);
        this.pauseXboxBinder.Attach(this.view.PART_ScanOption_PauseConsole, processor);
        // this.forceLEBinder.Attach(this.PART_ForcedEndianness, this.MemoryEngine);
        this.scanMemoryPagesBinder.Attach(this.view.PART_ScanOption_ScanMemoryPages, processor);

        this.stringIgnoreCaseBinder.AttachModel(processor);
        this.floatScanModeBinder.Attach(processor);
        this.stringScanModeBinder.Attach(processor);
        this.int_isHexBinder.Attach(this.view.PART_DTInt_IsHex, processor);
        this.int_isUnsignedBinder.Attach(this.view.PART_DTInt_IsUnsigned, processor);
        this.useFirstValueBinder.Attach(this.view.PART_UseFirstValue, processor);
        this.usePrevValueBinder.Attach(this.view.PART_UsePreviousValue, processor);
        this.dataTypeBinder.Attach(this.view.PART_DataTypeCombo, processor);
        this.scanTypeBinder1.Attach(this.view.PART_ScanTypeCombo1, processor, NumericScanTypeEnumInfo);
        this.scanTypeBinder2.Attach(this.view.PART_ScanTypeCombo2, processor, NumericScanTypeEnumInfo);
        this.selectedTabIndexBinder.Attach(this.view.PART_ScanSettingsTabControl, processor);
        this.scanForAnyBinder.Attach(this.view.PART_ToggleUnknownDataType, processor);
        this.useExpressionBinder.Attach(this.view.PART_UseExpressions, processor);
        this.updatedEnabledControlsBinder.Attach(this.view, processor);

        this.canScanFloatBinder.Attach(this.view.PART_Toggle_Float, processor.UnknownDataTypeOptions);
        this.canScanDoubleBinder.Attach(this.view.PART_Toggle_Double, processor.UnknownDataTypeOptions);
        this.canScanStringBinder.Attach(this.view.PART_Toggle_String, processor.UnknownDataTypeOptions);

        processor.NumericScanTypeChanged += this.ScanningProcessorOnNumericScanTypeChanged;
        processor.DataTypeChanged += this.OnScanningProcessorOnDataTypeChanged;
        processor.UseFirstValueForNextScanChanged += this.UpdateNonBetweenInput;
        processor.UsePreviousValueForNextScanChanged += this.UpdateNonBetweenInput;
        processor.ScanForAnyDataTypeChanged += this.UpdateNonBetweenInput;
        processor.UseExpressionParsingChanged += this.OnUseExpressionParsingChanged;
        this.OnUseExpressionParsingChanged(processor, EventArgs.Empty);
        this.view.PART_OrderListBox.SetScanningProcessor(processor);
    }

    public void OnViewUnloaded() {
        this.isScanningBinder.Detach();
        this.scanAddressBinder.Detach();
        this.scanLengthBinder.Detach();
        this.nextScanOverReadBinder.Detach();
        this.alignmentBinder.Detach();
        this.pauseXboxBinder.Detach();
        // this.forceLEBinder.Detach();
        this.scanMemoryPagesBinder.Detach();

        if (this.inputValueBinder.IsFullyAttached)
            this.inputValueBinder.Detach();
        if (this.inputBetweenABinder.IsFullyAttached)
            this.inputBetweenABinder.Detach();
        if (this.inputBetweenBBinder.IsFullyAttached)
            this.inputBetweenBBinder.Detach();

        this.stringIgnoreCaseBinder.DetachModel();
        this.floatScanModeBinder.Detach();
        this.stringScanModeBinder.Detach();
        this.int_isHexBinder.Detach();
        this.int_isUnsignedBinder.Detach();
        this.useFirstValueBinder.Detach();
        this.usePrevValueBinder.Detach();
        this.dataTypeBinder.Detach();
        this.scanTypeBinder1.Detach();
        this.scanTypeBinder2.Detach();
        this.selectedTabIndexBinder.Detach();
        this.scanForAnyBinder.Detach();
        this.useExpressionBinder.Detach();
        this.updatedEnabledControlsBinder.Detach();

        this.canScanFloatBinder.Detach();
        this.canScanDoubleBinder.Detach();
        this.canScanStringBinder.Detach();

        ScanningProcessor processor = this.view.MemoryEngine.ScanningProcessor;

        processor.NumericScanTypeChanged -= this.ScanningProcessorOnNumericScanTypeChanged;
        processor.DataTypeChanged -= this.OnScanningProcessorOnDataTypeChanged;
        processor.UseFirstValueForNextScanChanged -= this.UpdateNonBetweenInput;
        processor.UsePreviousValueForNextScanChanged -= this.UpdateNonBetweenInput;
        processor.ScanForAnyDataTypeChanged -= this.UpdateNonBetweenInput;
        processor.UseExpressionParsingChanged -= this.OnUseExpressionParsingChanged;

        this.view.PART_OrderListBox.SetScanningProcessor(null);
    }

    private async Task EditAlignmentAsync() {
        ScanningProcessor p = this.view.MemoryEngine.ScanningProcessor;
        SingleUserInputInfo info = new SingleUserInputInfo(p.Alignment.ToString("X")) {
            Caption = "Edit alignment",
            Message = "Alignment is the offset added to each memory address",
            Label = "Alignment (prefix with '0x' to parse as hex)",
            Validate = (e) => {
                if (!AddressParsing.TryParse32(e.Input, out uint number, out string? error, canParseAsExpression: true, hexByDefault: false)) {
                    e.Errors.Add(error);
                }
                else if (number == 0) {
                    e.Errors.Add("Alignment cannot be zero!");
                }
            },
            DebounceErrorsDelay = 300
        };

        using IDisposable _ = SingleUserInputInfo.DebounceElapsedObservable.Subscribe(info, null, static (s, _) => {
            if (s.TextErrors != null) {
                s.Footer = "Cannot show examples: invalid value";
            }
            else {
                int align = (int) AddressParsing.Parse32(s.Text, canParseAsExpression: true, hexByDefault: false);
                StringBuilder sb = new StringBuilder().Append(0);
                for (int i = 0, j = align; i < 4; i++, j += align)
                    sb.Append(", ").Append(j);
                s.Footer = "We will scan " + sb.Append(", etc.");
            }
        });

        if (await IUserInputDialogService.Instance.ShowInputDialogAsync(info, this.view.myWindow) == true) {
            p.Alignment = AddressParsing.Parse32(info.Text, canParseAsExpression: true, hexByDefault: false);
        }
    }

    private void ScanningProcessorOnNumericScanTypeChanged(object? o, EventArgs e) {
        this.UpdateUIForScanTypeAndDataType();
    }

    private void OnScanningProcessorOnDataTypeChanged(object? sender, EventArgs e) {
        ScanningProcessor p = (ScanningProcessor) sender!;
        if (p.DataType.IsFloatingPoint()) {
            this.lastFloatDataType = p.DataType;
        }
        else if (p.DataType.IsInteger()) {
            this.lastIntegerDataType = p.DataType;
        }
    }

    private void UpdateNonBetweenInput(object? sender, EventArgs e) {
        this.UpdateSingleInputField();
    }

    private void OnUseExpressionParsingChanged(object? o, EventArgs e) {
        ScanningProcessor processor = this.view.MemoryEngine.ScanningProcessor;
        this.UpdateUIForScanTypeAndDataType();
        this.UpdateSingleInputField();

        this.dataTypeBinder.SetIsEnabled(DataType.String, !processor.UseExpressionParsing);
        this.dataTypeBinder.SetIsEnabled(DataType.ByteArray, !processor.UseExpressionParsing);
    }

    private void UpdateTabControlSelectedIndex(IBinder<ScanningProcessor> b) {
        if (b.Model.ScanForAnyDataType) {
            ((TabControl) b.Control).SelectedIndex = 3;
        }
        else {
            switch (b.Model.DataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32:
                case DataType.Int64:
                    ((TabControl) b.Control).SelectedIndex = 0;
                    break;
                case DataType.Float:
                case DataType.Double:
                    ((TabControl) b.Control).SelectedIndex = 1;
                    break;
                case DataType.String:    ((TabControl) b.Control).SelectedIndex = 2; break;
                case DataType.ByteArray: break;
                default:                 throw new ArgumentOutOfRangeException();
            }
        }

        this.UpdateUIForScanTypeAndDataType();
    }

    private void UpdateModeForTabControlSelectedIndex(IBinder<ScanningProcessor> b) {
        if (!b.Model.HasDoneFirstScan) {
            int idx = ((TabControl) b.Control).SelectedIndex;
            if (idx == 3) {
                b.Model.ScanForAnyDataType = true;
                b.Model.Alignment = 1;
            }
            else {
                b.Model.ScanForAnyDataType = false;
                switch (idx) {
                    case 0: b.Model.DataType = this.lastIntegerDataType; break;
                    case 1: b.Model.DataType = this.lastFloatDataType; break;
                    case 2: b.Model.DataType = DataType.String; break;
                }

                // update anyway just in case old DT equals new DT
                b.Model.Alignment = b.Model.DataType.GetAlignmentFromDataType();
            }
        }
    }

    public void UpdateUIForScanTypeAndDataType() {
        ScanningProcessor sp = this.view.MemoryEngine.ScanningProcessor;
        bool isExpr = sp.UseExpressionParsing;
        bool isNumeric = sp.DataType.IsNumeric();
        bool isBetween = !isExpr && sp.NumericScanType.IsBetween();
        bool isAny = sp.ScanForAnyDataType;

        if (isBetween && isNumeric && !isAny) {
            this.view.PART_Input_Value1.IsVisible = false;
            this.view.PART_Grid_Input_Between.IsVisible = true;
            this.view.PART_ValueFieldLabel.Text = "Between";
            this.view.PART_UseFirstOrPrevButtonGrid.IsVisible = false;

            if (this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Detach();
            if (!this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Attach(this.view.PART_Input_BetweenA, sp);
            if (!this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Attach(this.view.PART_Input_BetweenB, sp);
        }
        else {
            this.view.PART_Input_Value1.IsVisible = true;
            this.view.PART_Grid_Input_Between.IsVisible = false;
            this.view.PART_ValueFieldLabel.Text = isExpr ? "Expression" : "Value";
            this.view.PART_UseFirstOrPrevButtonGrid.IsVisible = !isExpr && !isAny && isNumeric;
            this.view.PART_CompareModePanelInteger.IsVisible = !isExpr;
            this.view.PART_CompareModePanelFloating.IsVisible = !isExpr;
            this.view.PART_DTFloat_Truncate.IsVisible = !isExpr;
            this.view.PART_DTFloat_RoundToQuery.IsVisible = !isExpr;

            if (this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Detach();
            if (this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Detach();
            if (!this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Attach(this.view.PART_Input_Value1, sp);
        }

        this.UpdateSingleInputField();
    }

    public void UpdateSingleInputField() {
        ScanningProcessor p = this.view.MemoryEngine.ScanningProcessor;
        bool isEnabled = p.ScanForAnyDataType
                         || p.UseExpressionParsing
                         || p.DataType == DataType.ByteArray
                         || p.DataType == DataType.String
                         || (!p.UseFirstValueForNextScan && !p.UsePreviousValueForNextScan);

        this.view.PART_Input_Value1.IsEnabled = isEnabled;
    }

    private static async Task<bool> OnAddressOrLengthOutOfRange(ScanningProcessor processor, uint start, uint length) {
        bool didChangeStart = processor.StartAddress != start;
        Debug.Assert(didChangeStart || processor.ScanLength != length);
        ulong overflowAmount = (ulong) start + (ulong) length - uint.MaxValue;
        MessageBoxInfo info = new MessageBoxInfo() {
            Caption = $"Invalid {(didChangeStart ? "start address" : "scan length")}",
            Message = $"{(didChangeStart ? "Start Address" : "Scan Length")} causes scan to exceed 32 bit address space by 0x{overflowAmount:X8}." +
                      Environment.NewLine +
                      Environment.NewLine +
                      $"Auto-adjust the {(didChangeStart ? "scan length" : "start address")} to fit?",
            Buttons = MessageBoxButtons.OKCancel, DefaultButton = MessageBoxResult.OK,
        };

        if (await IMessageDialogService.Instance.ShowMessage(info) == MessageBoxResult.OK) {
            if (didChangeStart) {
                processor.SetScanRange(start, uint.MaxValue - start);
            }
            else {
                processor.SetScanRange((uint) (start - overflowAmount), length);
            }

            return true;
        }

        return false;
    }
}