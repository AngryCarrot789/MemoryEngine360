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

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public readonly record struct UploadTextBoxInfo(TextBox TextBox, DataType DataType, bool IsUnsigned);

public partial class DataInspectorControl : UserControl {
    public static readonly StyledProperty<bool> IsLittleEndianProperty = AvaloniaProperty.Register<DataInspectorControl, bool>(nameof(IsLittleEndian));
    public static readonly StyledProperty<bool> DisplayIntegersAsHexProperty = AvaloniaProperty.Register<DataInspectorControl, bool>(nameof(DisplayIntegersAsHex));
    public static readonly StyledProperty<Func<byte[], int>?> ReadDataProcedureProperty = AvaloniaProperty.Register<DataInspectorControl, Func<byte[], int>?>(nameof(ReadDataProcedure));
    public static readonly StyledProperty<Action<uint, uint>?> GoToAddressProcedureProperty = AvaloniaProperty.Register<DataInspectorControl, Action<uint, uint>?>(nameof(GoToAddressProcedure));
    public static readonly StyledProperty<Action<int>?> MoveCaretProcedureProperty = AvaloniaProperty.Register<DataInspectorControl, Action<int>?>(nameof(GoToAddressProcedure));
    public static readonly StyledProperty<Func<UploadTextBoxInfo, Task>?> UploadTextBoxTextProperty = AvaloniaProperty.Register<DataInspectorControl, Func<UploadTextBoxInfo, Task>?>(nameof(UploadTextBoxText));

    /// <summary>
    /// Gets or sets if we interpret the data source as little endian
    /// </summary>
    public bool IsLittleEndian {
        get => this.GetValue(IsLittleEndianProperty);
        set => this.SetValue(IsLittleEndianProperty, value);
    }

    /// <summary>
    /// Gets or sets if integers should be shown as hex
    /// </summary>
    public bool DisplayIntegersAsHex {
        get => this.GetValue(DisplayIntegersAsHexProperty);
        set => this.SetValue(DisplayIntegersAsHexProperty, value);
    }

    /// <summary>
    /// A function that reads data to present in the inspector. The function should return how many bytes in the array were actually written.
    /// </summary>
    public Func<byte[], int>? ReadDataProcedure {
        get => this.GetValue(ReadDataProcedureProperty);
        set => this.SetValue(ReadDataProcedureProperty, value);
    }

    /// <summary>
    /// Gets or sets the procedure that moves the hex editor to the given address
    /// </summary>
    public Action<uint /* address */, uint /* length */>? GoToAddressProcedure {
        get => this.GetValue(GoToAddressProcedureProperty);
        set => this.SetValue(GoToAddressProcedureProperty, value);
    }

    /// <summary>
    /// Gets or sets the procedure that moves the hex editor's caret by an amount 
    /// </summary>
    public Action<int>? MoveCaretProcedure {
        get => this.GetValue(MoveCaretProcedureProperty);
        set => this.SetValue(MoveCaretProcedureProperty, value);
    }

    /// <summary>
    /// Gets or sets the async procedure that uploads the text box info to the "source"
    /// </summary>
    public Func<UploadTextBoxInfo, Task>? UploadTextBoxText {
        get => this.GetValue(UploadTextBoxTextProperty);
        set => this.SetValue(UploadTextBoxTextProperty, value);
    }

    private bool isUpdatingRadioButtons;
    private AsyncRelayCommand<UploadTextBoxInfo>? parseTextBoxAndUploadCommand;

    public DataInspectorControl() {
        this.InitializeComponent();
        ReadDataProcedureProperty.Changed.AddClassHandler<DataInspectorControl>((o, args) => o.UpdateFields());
        DisplayIntegersAsHexProperty.Changed.AddClassHandler<DataInspectorControl>((o, args) => o.UpdateFields());
        IsLittleEndianProperty.Changed.AddClassHandler<DataInspectorControl>((o, args) => {
            o.UpdateEndiannessRadioButtons();
            o.UpdateFields();
        });

        UploadTextBoxTextProperty.Changed.AddClassHandler<DataInspectorControl, Func<UploadTextBoxInfo, Task>?>((o, args) => {
            Func<UploadTextBoxInfo, Task>? func = args.NewValue.GetValueOrDefault();
            o.parseTextBoxAndUploadCommand = func != null
                ? new AsyncRelayCommand<UploadTextBoxInfo>(func, isParamRequired: true)
                : null;
        });

        this.UpdateEndiannessRadioButtons();

        this.PART_LittleEndian.IsCheckedChanged += this.OnAnyRadioButtonIsCheckedChanged;
        this.PART_BigEndian.IsCheckedChanged += this.OnAnyRadioButtonIsCheckedChanged;

        this.PART_BtnFwdInt8.Click += (s, e) => this.MoveCaretProcedure?.Invoke(1);
        this.PART_BtnFwdInt16.Click += (s, e) => this.MoveCaretProcedure?.Invoke(2);
        this.PART_BtnFwdInt32.Click += (s, e) => this.MoveCaretProcedure?.Invoke(4);
        this.PART_BtnFwdInt64.Click += (s, e) => this.MoveCaretProcedure?.Invoke(8);
        this.PART_BtnBackInt8.Click += (s, e) => this.MoveCaretProcedure?.Invoke(-1);
        this.PART_BtnBackInt16.Click += (s, e) => this.MoveCaretProcedure?.Invoke(-2);
        this.PART_BtnBackInt32.Click += (s, e) => this.MoveCaretProcedure?.Invoke(-4);
        this.PART_BtnBackInt64.Click += (s, e) => this.MoveCaretProcedure?.Invoke(-8);

        EventHandler<KeyEventArgs> keyDownHandler = this.OnDataInspectorNumericTextBoxKeyDown;
        this.PART_Int8.KeyDown += keyDownHandler;
        this.PART_UInt8.KeyDown += keyDownHandler;
        this.PART_Int16.KeyDown += keyDownHandler;
        this.PART_UInt16.KeyDown += keyDownHandler;
        this.PART_Int32.KeyDown += keyDownHandler;
        this.PART_UInt32.KeyDown += keyDownHandler;
        this.PART_Int64.KeyDown += keyDownHandler;
        this.PART_UInt64.KeyDown += keyDownHandler;
        this.PART_Float.KeyDown += keyDownHandler;
        this.PART_Double.KeyDown += keyDownHandler;
    }

    /// <summary>
    /// Updates the inspector's data fields
    /// </summary>
    public void UpdateFields() {
        Func<byte[], int>? readProcedure = this.ReadDataProcedure;
        if (readProcedure == null) {
            return;
        }

        byte[] data = new byte[8];
        int cbData = readProcedure(data);

        // Read the data in system endianness
        byte val08 = cbData >= sizeof(byte) ? data[0] : default;
        ushort val16 = cbData >= sizeof(ushort) ? MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(data, 0, sizeof(ushort))) : default, u16 = val16;
        uint val32 = cbData >= sizeof(uint) ? MemoryMarshal.Read<uint>(new ReadOnlySpan<byte>(data, 0, sizeof(uint))) : 0, u32 = val32;
        ulong val64 = cbData >= sizeof(ulong) ? MemoryMarshal.Read<ulong>(new ReadOnlySpan<byte>(data, 0, sizeof(ulong))) : 0;

        // Reverse endianness if our inspector endianness does not match the system endianness
        if (this.IsLittleEndian != BitConverter.IsLittleEndian) {
            val16 = BinaryPrimitives.ReverseEndianness(val16);
            val32 = BinaryPrimitives.ReverseEndianness(val32);
            val64 = BinaryPrimitives.ReverseEndianness(val64);
        }

        bool displayAsHex = this.DisplayIntegersAsHex;
        this.PART_Binary8.Text = val08.ToString("B8");
        if (!this.PART_Int8.IsKeyboardFocusWithin) {
            this.PART_Int8.Text = displayAsHex
                ? (sbyte) val08 < 0
                    ? "-" + (-(sbyte) val08).ToString("X2")
                    : ((sbyte) val08).ToString("X2")
                : ((sbyte) val08).ToString();
        }

        if (!this.PART_UInt8.IsKeyboardFocusWithin) {
            this.PART_UInt8.Text = displayAsHex ? val08.ToString("X2") : val08.ToString();
        }

        if (!this.PART_Int16.IsKeyboardFocusWithin) {
            this.PART_Int16.Text = displayAsHex
                ? (short) val16 < 0
                    ? "-" + (-(short) val16).ToString("X4")
                    : ((short) val16).ToString("X4")
                : ((short) val16).ToString();
        }

        if (!this.PART_UInt16.IsKeyboardFocusWithin) {
            this.PART_UInt16.Text = displayAsHex ? val16.ToString("X4") : val16.ToString();
        }

        if (!this.PART_Int32.IsKeyboardFocusWithin) {
            this.PART_Int32.Text = displayAsHex
                ? (int) val32 < 0
                    ? "-" + (-(int) val32).ToString("X8")
                    : ((int) val32).ToString("X8")
                : ((int) val32).ToString();
        }

        if (!this.PART_UInt32.IsKeyboardFocusWithin) {
            this.PART_UInt32.Text = displayAsHex ? val32.ToString("X8") : val32.ToString();
        }

        if (!this.PART_Int64.IsKeyboardFocusWithin) {
            this.PART_Int64.Text = displayAsHex ? (long) val64 < 0 ? "-" + (-(long) val64).ToString("X16") : ((long) val64).ToString("X16") : ((long) val64).ToString();
        }

        if (!this.PART_UInt64.IsKeyboardFocusWithin) {
            this.PART_UInt64.Text = displayAsHex ? val64.ToString("X16") : val64.ToString();
        }

        if (!this.PART_Float.IsKeyboardFocusWithin) {
            this.PART_Float.Text = Unsafe.As<uint, float>(ref val32).ToString();
        }

        if (!this.PART_Double.IsKeyboardFocusWithin) {
            this.PART_Double.Text = Unsafe.As<ulong, double>(ref val64).ToString();
        }

        ReadOnlySpan<byte> rawSpan16 = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ushort, byte>(ref u16), sizeof(ushort));
        ReadOnlySpan<byte> rawSpan32 = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref u32), sizeof(uint));
        uint chInt32LE = BinaryPrimitives.ReadUInt32LittleEndian(rawSpan32);
        uint chInt32BE = BinaryPrimitives.ReadUInt32BigEndian(rawSpan32);

        this.PART_CharUTF8.Text = ((char) val08).ToString();
        this.PART_CharUTF16LE.Text = ((char) BinaryPrimitives.ReadInt16LittleEndian(rawSpan16)).ToString();
        this.PART_CharUTF16BE.Text = ((char) BinaryPrimitives.ReadInt16BigEndian(rawSpan16)).ToString();
        this.PART_CharUTF32LE.Text = Encoding.GetEncoding(12000).GetString(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref chInt32LE), sizeof(uint)));
        this.PART_CharUTF32BE.Text = Encoding.GetEncoding(12001).GetString(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref chInt32BE), sizeof(uint)));
    }

    private void OnDataInspectorNumericTextBoxKeyDown(object? sender, KeyEventArgs e) {
        TextBox tb = (TextBox) sender!;
        if (e.Key == Key.Escape) {
            VisualTreeUtils.TryMoveFocusUpwards(tb);
            return;
        }

        if (e.Key == Key.Enter && this.parseTextBoxAndUploadCommand != null) {
            if (TryGetTextBoxInfo(tb, out UploadTextBoxInfo info)) {
                this.parseTextBoxAndUploadCommand.Execute(info);
            }
        }
    }

    private static bool TryGetTextBoxInfo(TextBox tb, out UploadTextBoxInfo info) {
        switch (tb.Name) {
            case nameof(PART_Int8):   info = new UploadTextBoxInfo(tb, DataType.Byte, false); break;
            case nameof(PART_UInt8):  info = new UploadTextBoxInfo(tb, DataType.Byte, true); break;
            case nameof(PART_Int16):  info = new UploadTextBoxInfo(tb, DataType.Int16, false); break;
            case nameof(PART_UInt16): info = new UploadTextBoxInfo(tb, DataType.Int16, true); break;
            case nameof(PART_Int32):  info = new UploadTextBoxInfo(tb, DataType.Int32, false); break;
            case nameof(PART_UInt32): info = new UploadTextBoxInfo(tb, DataType.Int32, true); break;
            case nameof(PART_Int64):  info = new UploadTextBoxInfo(tb, DataType.Int64, false); break;
            case nameof(PART_UInt64): info = new UploadTextBoxInfo(tb, DataType.Int64, true); break;
            case nameof(PART_Float):  info = new UploadTextBoxInfo(tb, DataType.Float, false); break;
            case nameof(PART_Double): info = new UploadTextBoxInfo(tb, DataType.Double, false); break;
            default: {
                info = default;
                return false;
            }
        }

        return true;
    }

    private void OnAnyRadioButtonIsCheckedChanged(object? sender, RoutedEventArgs e) {
        if (!this.isUpdatingRadioButtons) {
            this.IsLittleEndian = ReferenceEquals(sender, this.PART_LittleEndian);
        }
    }

    private void UpdateEndiannessRadioButtons() {
        this.isUpdatingRadioButtons = true;

        bool isLE = this.IsLittleEndian;
        this.PART_LittleEndian.IsChecked = isLE;
        this.PART_BigEndian.IsChecked = !isLE;

        this.isUpdatingRadioButtons = false;
    }
}