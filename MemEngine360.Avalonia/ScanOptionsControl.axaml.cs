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

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MemEngine360.Engine;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Avalonia.Bindings;
using PFXToolKitUI.Avalonia.Bindings.ComboBoxes;
using PFXToolKitUI.Avalonia.Bindings.Enums;

namespace MemEngine360.Avalonia;

public partial class ScanOptionsControl : UserControl {
    public static readonly StyledProperty<MemoryEngine360?> MemoryEngine360Property = AvaloniaProperty.Register<ScanOptionsControl, MemoryEngine360?>(nameof(MemoryEngine360));

    public MemoryEngine360? MemoryEngine360 {
        get => this.GetValue(MemoryEngine360Property);
        set => this.SetValue(MemoryEngine360Property, value);
    }

    private readonly EventPropertyBinder<ScanningProcessor> hasDoneFirstScanBinder = new EventPropertyBinder<ScanningProcessor>(nameof(ScanningProcessor.HasFirstScanChanged), (b) => {
        ScanOptionsControl view = (ScanOptionsControl) b.Control;
        view.PART_DataTypeCombo.IsEnabled = !b.Model.HasDoneFirstScan;
        view.PART_UseFirstValue.IsEnabled = b.Model.HasDoneFirstScan;
        view.PART_UsePreviousValue.IsEnabled = b.Model.HasDoneFirstScan;

        // view.PART_ScanSettingsTabControl.IsEnabled = !b.Model.HasDoneFirstScan; 
    });

    private readonly EventPropertyEnumBinder<FloatScanOption> floatScanModeBinder = new EventPropertyEnumBinder<FloatScanOption>(typeof(ScanningProcessor), nameof(ScanningProcessor.FloatScanModeChanged), (x) => ((ScanningProcessor) x).FloatScanOption, (x, v) => ((ScanningProcessor) x).FloatScanOption = v);
    private readonly EventPropertyEnumBinder<StringType> stringScanModeBinder = new EventPropertyEnumBinder<StringType>(typeof(ScanningProcessor), nameof(ScanningProcessor.StringScanModeChanged), (x) => ((ScanningProcessor) x).StringScanOption, (x, v) => ((ScanningProcessor) x).StringScanOption = v);
    private readonly IBinder<ScanningProcessor> int_isHexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.IsIntInputHexadecimalChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.IsIntInputHexadecimal, (b) => b.Model.IsIntInputHexadecimal = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> useFirstValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UseFirstValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UseFirstValueForNextScan, (b) => b.Model.UseFirstValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly IBinder<ScanningProcessor> usePrevValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.UsePreviousValueForNextScanChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.UsePreviousValueForNextScan, (b) => b.Model.UsePreviousValueForNextScan = ((ToggleButton) b.Control).IsChecked == true);
    private readonly ComboBoxToEventPropertyEnumBinder<DataType> dataTypeBinder = new ComboBoxToEventPropertyEnumBinder<DataType>(typeof(ScanningProcessor), nameof(ScanningProcessor.DataTypeChanged), (x) => ((ScanningProcessor) x).DataType, (x, y) => ((ScanningProcessor) x).DataType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder1 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly ComboBoxToEventPropertyEnumBinder<NumericScanType> scanTypeBinder2 = new ComboBoxToEventPropertyEnumBinder<NumericScanType>(typeof(ScanningProcessor), nameof(ScanningProcessor.NumericScanTypeChanged), (x) => ((ScanningProcessor) x).NumericScanType, (x, y) => ((ScanningProcessor) x).NumericScanType = y);
    private readonly AvaloniaPropertyToEventPropertyBinder<ScanningProcessor> selectedTabIndexBinder;
    private readonly IBinder<ScanningProcessor> inputValueBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenABinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputA, (b) => b.Model.InputA = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> inputBetweenBBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TextBox.TextProperty, nameof(ScanningProcessor.InputBChanged), (b) => ((TextBox) b.Control).Text = b.Model.InputB, (b) => b.Model.InputB = ((TextBox) b.Control).Text ?? "");
    private readonly IBinder<ScanningProcessor> stringIgnoreCaseBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(ToggleButton.IsCheckedProperty, nameof(ScanningProcessor.StringIgnoreCaseChanged), (b) => ((ToggleButton) b.Control).IsChecked = b.Model.StringIgnoreCase, (b) => b.Model.StringIgnoreCase = ((ToggleButton) b.Control).IsChecked == true);

    static ScanOptionsControl() {
        // AVPToEventPropertyBinder.Bind<ScanOptionsControl, TextBox, ScanningProcessor, string?>(nameof(PART_Input_Value1), TextBox.TextProperty, nameof(ScanningProcessor.InputAChanged), (c, m) => c.Text = m.InputA, (c, m) => m.InputA = c.Text ?? "");

        MemoryEngine360Property.Changed.AddClassHandler<ScanOptionsControl, MemoryEngine360?>(OnMemEngineChanged);
    }

    private DataType lastIntegerDataType = DataType.Int32, lastFloatDataType = DataType.Float;

    public ScanOptionsControl() {
        this.InitializeComponent();
        // AVPToEventPropertyBinder.Attach<ScanOptionsControl>(this.FindNameScope()!, new ScanningProcessor(null));

        this.stringIgnoreCaseBinder.AttachControl(this.PART_IgnoreCases);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_UseExactValue, FloatScanOption.UseExactValue);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_Truncate, FloatScanOption.TruncateToQuery);
        this.floatScanModeBinder.Assign(this.PART_DTFloat_RoundToQuery, FloatScanOption.RoundToQuery);
        this.stringScanModeBinder.Assign(this.PART_DTString_ASCII, StringType.ASCII);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF8, StringType.UTF8);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF16, StringType.UTF16);
        this.stringScanModeBinder.Assign(this.PART_DTString_UTF32, StringType.UTF32);
        // Bind between tab control and data type
        this.selectedTabIndexBinder = new AvaloniaPropertyToEventPropertyBinder<ScanningProcessor>(TabControl.SelectedIndexProperty, nameof(ScanningProcessor.DataTypeChanged), (b) => {
            switch (b.Model.DataType) {
                case DataType.Byte:
                case DataType.Int16:
                case DataType.Int32:
                case DataType.Int64: {
                    ((TabControl) b.Control).SelectedIndex = 0;
                    break;
                }
                case DataType.Float:
                case DataType.Double: {
                    ((TabControl) b.Control).SelectedIndex = 1;
                    break;
                }
                case DataType.String: {
                    ((TabControl) b.Control).SelectedIndex = 2;
                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }

            this.UpdateUIForScanTypeAndDataType();
        }, (b) => {
            if (b.Model.HasDoneFirstScan)
                return;

            switch (((TabControl) b.Control).SelectedIndex) {
                case 0: b.Model.DataType = this.lastIntegerDataType; break;
                case 1: b.Model.DataType = this.lastFloatDataType; break;
                case 2: b.Model.DataType = DataType.String; break;
            }
        });
    }


    private static void OnMemEngineChanged(ScanOptionsControl c, AvaloniaPropertyChangedEventArgs<MemoryEngine360?> e) {
        if (e.OldValue.GetValueOrDefault() is MemoryEngine360 oldEngine) {
            c.stringIgnoreCaseBinder.Detach();
            c.floatScanModeBinder.Detach();
            c.stringScanModeBinder.Detach();
            c.hasDoneFirstScanBinder.Detach();
            c.int_isHexBinder.Detach();
            c.useFirstValueBinder.Detach();
            c.usePrevValueBinder.Detach();
            c.dataTypeBinder.Detach();
            c.scanTypeBinder1.Detach();
            c.scanTypeBinder2.Detach();
            c.selectedTabIndexBinder.Detach();

            oldEngine.ScanningProcessor.NumericScanTypeChanged -= c.ScanningProcessorOnNumericScanTypeChanged;
            oldEngine.ScanningProcessor.DataTypeChanged -= c.OnScanningProcessorOnDataTypeChanged;
            oldEngine.ScanningProcessor.UseFirstValueForNextScanChanged -= c.UpdateOtherShit;
            oldEngine.ScanningProcessor.UsePreviousValueForNextScanChanged -= c.UpdateOtherShit;
        }

        if (e.NewValue.GetValueOrDefault() is MemoryEngine360 newEngine) {
            c.stringIgnoreCaseBinder.AttachModel(newEngine.ScanningProcessor);
            c.floatScanModeBinder.Attach(newEngine.ScanningProcessor);
            c.stringScanModeBinder.Attach(newEngine.ScanningProcessor);
            c.hasDoneFirstScanBinder.Attach(c, newEngine.ScanningProcessor);
            c.int_isHexBinder.Attach(c.PART_DTInt_IsHex, newEngine.ScanningProcessor);
            c.useFirstValueBinder.Attach(c.PART_UseFirstValue, newEngine.ScanningProcessor);
            c.usePrevValueBinder.Attach(c.PART_UsePreviousValue, newEngine.ScanningProcessor);
            c.dataTypeBinder.Attach(c.PART_DataTypeCombo, newEngine.ScanningProcessor);
            c.scanTypeBinder1.Attach(c.PART_ScanTypeCombo1, newEngine.ScanningProcessor);
            c.scanTypeBinder2.Attach(c.PART_ScanTypeCombo2, newEngine.ScanningProcessor);
            c.selectedTabIndexBinder.Attach(c.PART_ScanSettingsTabControl, newEngine.ScanningProcessor);

            newEngine.ScanningProcessor.NumericScanTypeChanged += c.ScanningProcessorOnNumericScanTypeChanged;
            newEngine.ScanningProcessor.DataTypeChanged += c.OnScanningProcessorOnDataTypeChanged;
            newEngine.ScanningProcessor.UseFirstValueForNextScanChanged += c.UpdateOtherShit;
            newEngine.ScanningProcessor.UsePreviousValueForNextScanChanged += c.UpdateOtherShit;

            c.UpdateUIForScanTypeAndDataType();
        }
    }

    private void ScanningProcessorOnNumericScanTypeChanged(ScanningProcessor sender) {
        this.UpdateUIForScanTypeAndDataType();
    }

    private void OnScanningProcessorOnDataTypeChanged(ScanningProcessor p) {
        if (p.DataType.IsFloat()) {
            this.lastFloatDataType = p.DataType;
        }
        else if (p.DataType.IsInteger()) {
            this.lastIntegerDataType = p.DataType;
        }
    }

    private void UpdateOtherShit(ScanningProcessor p) {
        this.PART_Input_Value1.IsEnabled = p.DataType == DataType.String || !p.UseFirstValueForNextScan && !p.UsePreviousValueForNextScan;
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);

        if (this.inputValueBinder.IsFullyAttached)
            this.inputValueBinder.Detach();
        if (this.inputBetweenABinder.IsFullyAttached)
            this.inputBetweenABinder.Detach();
        if (this.inputBetweenBBinder.IsFullyAttached)
            this.inputBetweenBBinder.Detach();
    }

    private void UpdateUIForScanTypeAndDataType() {
        MemoryEngine360? engine = this.MemoryEngine360;
        if (engine == null) {
            return;
        }

        ScanningProcessor sp = engine.ScanningProcessor;
        bool isNumeric = sp.DataType.IsNumeric();
        bool isBetween = sp.NumericScanType == NumericScanType.Between;

        if (isBetween && isNumeric) {
            this.PART_Input_Value1.IsVisible = false;
            this.PART_Grid_Input_Between.IsVisible = true;
            this.PART_ValueOrBetweenTextBlock.Text = "Between";
            this.PART_UseFirstOrPrevButtonGrid.IsVisible = false;

            if (this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Detach();
            if (!this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Attach(this.PART_Input_BetweenA, sp);
            if (!this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Attach(this.PART_Input_BetweenB, sp);
        }
        else {
            this.PART_Input_Value1.IsVisible = true;
            this.PART_Grid_Input_Between.IsVisible = false;
            this.PART_ValueOrBetweenTextBlock.Text = "Value";
            this.PART_UseFirstOrPrevButtonGrid.IsVisible = isNumeric;

            if (this.inputBetweenABinder.IsFullyAttached)
                this.inputBetweenABinder.Detach();
            if (this.inputBetweenBBinder.IsFullyAttached)
                this.inputBetweenBBinder.Detach();
            if (!this.inputValueBinder.IsFullyAttached)
                this.inputValueBinder.Attach(this.PART_Input_Value1, engine.ScanningProcessor);
        }

        this.UpdateOtherShit(sp);
    }
}