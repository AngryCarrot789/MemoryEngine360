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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Engine.Scanners;

public delegate void ScanningContextResultEventHandler(ScanningContext sender, ScanResultViewModel result);

/// <summary>
/// A class used to perform scanning operations. This class takes a snapshot of the options in <see cref="ScanningProcessor"/>
/// </summary>
public sealed class ScanningContext {
    internal const uint ChunkSize = 0x10000; // 65536
    internal readonly ScanningProcessor theProcessor;
    internal readonly string inputA, inputB;
    internal readonly uint startAddress, scanLength, scanEndAddress;
    internal readonly uint alignment;
    internal readonly bool pauseConsoleDuringScan;
    internal readonly bool scanMemoryPages, isIntInputHexadecimal;
    internal readonly bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    internal readonly FloatScanOption floatScanOption;
    internal readonly StringType stringType;
    internal readonly DataType dataType;
    internal readonly NumericScanType numericScanType;
    internal readonly StringComparison stringComparison;
    internal readonly bool isSecondInputRequired;

    // enough bytes to store all data types except string and byte array
    internal ulong numericInputA, numericInputB;
    internal MemoryPattern memoryPattern;

    // number of bytes the data type takes up. for strings, calculates based on StringType and char count
    internal int cbDataType;
    private readonly double epsilon = BasicApplicationConfiguration.Instance.FloatingPointEpsilon;

    // engine's forced LE state is not automatic, and it forces an endianness different from the connection.
    // internal bool reverseEndianness;

    public IOException? IOException { get; set; }

    /// <summary>
    /// Gets or sets if the scan encountered an IO error while reading data from the console
    /// </summary>
    public bool HasIOError => this.IOException != null;

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public event ScanningContextResultEventHandler? ResultFound;

    public ScanningContext(ScanningProcessor processor) {
        this.theProcessor = processor;
        this.inputA = processor.InputA.Trim();
        this.inputB = processor.InputB.Trim();
        this.startAddress = processor.StartAddress;
        this.scanLength = processor.ScanLength;
        this.scanEndAddress = this.startAddress + this.scanLength;
        this.alignment = processor.Alignment;
        this.pauseConsoleDuringScan = processor.PauseConsoleDuringScan;
        this.scanMemoryPages = processor.ScanMemoryPages;
        this.isIntInputHexadecimal = processor.IsIntInputHexadecimal;
        this.nextScanUsesFirstValue = processor.UseFirstValueForNextScan;
        this.nextScanUsesPreviousValue = processor.UsePreviousValueForNextScan;
        this.floatScanOption = processor.FloatScanOption;
        this.stringType = processor.StringScanOption;
        this.dataType = processor.DataType;
        this.numericScanType = processor.NumericScanType;
        this.stringComparison = processor.StringComparison;
        this.isSecondInputRequired = this.numericScanType == NumericScanType.Between
                                     && this.dataType.IsNumeric()
                                     && !this.nextScanUsesFirstValue
                                     && !this.nextScanUsesPreviousValue;
    }

    /// <summary>
    /// Sets up the internal data using what is currently present in the scanning processor (e.g. parse input(s) as the correct data type).
    /// Returns true when scanning and proceed.
    /// False when there's errors (e.g. non-integer when scanning for an integer, or min is greater than max when scanning in 'between' mode)
    /// </summary>
    public async Task<bool> Setup() {
        switch (this.dataType) {
            case DataType.Byte:   this.cbDataType = sizeof(byte); break;
            case DataType.Int16:  this.cbDataType = sizeof(short); break;
            case DataType.Int32:  this.cbDataType = sizeof(int); break;
            case DataType.Int64:  this.cbDataType = sizeof(long); break;
            case DataType.Float:  this.cbDataType = sizeof(float); break;
            case DataType.Double: this.cbDataType = sizeof(double); break;
            case DataType.String: {
                int cbInputA;
                switch (this.stringType) {
                    case StringType.ASCII:
                    case StringType.UTF8: {
                        cbInputA = this.inputA.Length;
                        break;
                    }
                    case StringType.UTF16: cbInputA = this.inputA.Length * 2; break;
                    case StringType.UTF32: cbInputA = this.inputA.Length * 4; break;
                    default:               throw new ArgumentOutOfRangeException();
                }

                if (cbInputA > ChunkSize) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", $"Input is too long. We read data in chunks of {ChunkSize / 1024}K, therefore, the string cannot contain more than that many bytes.");
                    return false;
                }

                this.cbDataType = cbInputA;
                break;
            }
            case DataType.ByteArray: {
                if (!MemoryPattern.TryCompile(this.inputA, out this.memoryPattern, false, out string? errorMessage)) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid memory pattern", errorMessage, "Example pattern: '11 88 FC ? EF ? FF'");
                    return false;
                }

                this.cbDataType = this.memoryPattern.Length;
                break;
            }
            default: {
                Debug.Fail("Missing data type");
                return false;
            }
        }

        IConsoleConnection connection = this.theProcessor.MemoryEngine360.Connection!;
        Debug.Assert(connection != null);
        // this.reverseEndianness = this.theProcessor.MemoryEngine360.IsForcedLittleEndian is bool forcedLittle && forcedLittle != connection.IsLittleEndian;

        if (this.theProcessor.HasDoneFirstScan && (this.nextScanUsesFirstValue || this.nextScanUsesPreviousValue)) {
            return true;
        }

        if (string.IsNullOrEmpty(this.inputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", this.isSecondInputRequired ? "FROM input is empty" : "Input is empty");
            return false;
        }

        if (this.isSecondInputRequired && string.IsNullOrEmpty(this.inputB)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", "TO input is empty");
            return false;
        }

        if (this.dataType.IsNumeric()) {
            NumericDisplayType ndt = this.dataType.IsInteger() && this.isIntInputHexadecimal ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            if (!TryParseNumeric(this.inputA, this.dataType, ndt, out this.numericInputA /*, this.reverseEndianness*/)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"{(this.isSecondInputRequired ? "FROM value" : "Input")} is invalid '{this.inputA}'. Cannot be parsed as {this.dataType}.");
                return false;
            }

            if (this.isSecondInputRequired) {
                if (!TryParseNumeric(this.inputB, this.dataType, ndt, out this.numericInputB /*, this.reverseEndianness*/)) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", $"TO value is invalid '{this.inputB}'. Cannot be parsed as {this.dataType}.");
                    return false;
                }

                // ensure FROM <= TO 
                bool isBackward = false;
                switch (this.dataType) {
                    case DataType.Byte:   isBackward = Unsafe.As<ulong, byte>(ref this.numericInputA) > Unsafe.As<ulong, byte>(ref this.numericInputB); break;
                    case DataType.Int16:  isBackward = Unsafe.As<ulong, short>(ref this.numericInputA) > Unsafe.As<ulong, short>(ref this.numericInputB); break;
                    case DataType.Int32:  isBackward = Unsafe.As<ulong, int>(ref this.numericInputA) > Unsafe.As<ulong, int>(ref this.numericInputB); break;
                    case DataType.Int64:  isBackward = Unsafe.As<ulong, long>(ref this.numericInputA) > Unsafe.As<ulong, long>(ref this.numericInputB); break;
                    case DataType.Float:  isBackward = Unsafe.As<ulong, float>(ref this.numericInputA) > Unsafe.As<ulong, float>(ref this.numericInputB); break;
                    case DataType.Double: isBackward = Unsafe.As<ulong, double>(ref this.numericInputA) > Unsafe.As<ulong, double>(ref this.numericInputB); break;
                    case DataType.ByteArray:
                    case DataType.String:
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }

                if (isBackward) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", $"You put them in the wrong way around!", $"FROM is greater than TO ({this.inputA} > {this.inputB})");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Scans the buffer for a value 
    /// </summary>
    /// <param name="address">The address that is relative to the 0th element in the buffer</param>
    /// <param name="buffer">The buffer containing data to scan</param>
    internal void ProcessMemoryBlockForFirstScan(uint address, ReadOnlySpan<byte> buffer) {
        // by default, align is set to cbDataType except for string where it's 1. So in most cases, only check bounds for strings
        // There's also another issue with values between chunks, which we don't process because I can't get it to work...
        bool checkBounds = this.alignment < this.cbDataType;
        if (this.dataType.IsNumeric()) {
            for (uint i = 0; i < buffer.Length; i += this.alignment) {
                if (checkBounds && (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                IDataValue? matchBoxed;
                ReadOnlySpan<byte> span = buffer.Slice((int) i, this.cbDataType);
                switch (this.dataType) {
                    case DataType.Byte:   matchBoxed = this.CompareInt<byte>(span); break;
                    case DataType.Int16:  matchBoxed = this.CompareInt<short>(span); break;
                    case DataType.Int32:  matchBoxed = this.CompareInt<int>(span); break;
                    case DataType.Int64:  matchBoxed = this.CompareInt<long>(span); break;
                    case DataType.Float:  matchBoxed = this.CompareFloat<float>(span); break;
                    case DataType.Double: matchBoxed = this.CompareFloat<double>(span); break;
                    default:
                        Debug.Fail("Invalid data type");
                        return;
                }

                if (matchBoxed != null) {
                    NumericDisplayType ndt = this.isIntInputHexadecimal && this.dataType.IsInteger() ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, address + i, this.dataType, ndt, this.stringType, matchBoxed));
                }
            }
        }
        else if (this.dataType == DataType.String || this.dataType == DataType.ByteArray) {
            bool isString = this.dataType == DataType.String;
            for (uint i = 0; i < buffer.Length; i += this.alignment) {
                if (checkBounds && (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                ReadOnlySpan<byte> data = buffer.Slice((int) i, this.cbDataType);
                if (isString) {
                    string readText = this.stringType.ToEncoding().GetString(data);
                    if (readText.Equals(this.inputA, this.stringComparison)) {
                        this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, address + i, this.dataType, NumericDisplayType.Normal, this.stringType, new DataValueString(readText, this.stringType)));
                    }
                }
                else {
                    Debug.Assert(this.memoryPattern.IsValid);
                    if (this.memoryPattern.Matches(data)) {
                        this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, address + i, this.dataType, NumericDisplayType.Normal, this.stringType, new DataValueByteArray(data.ToArray())));
                    }
                }
            }
        }
        else {
            Debug.Fail("Missing data type");
        }
    }

    /// <summary>
    /// Performs a next scan. Requirements: the src list's items are guaranteed to have the
    /// same data types, and it equals our internal scanning data type
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="srcList">The source list of items</param>
    public async Task PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList) {
        ActivityTask task = ActivityManager.Instance.CurrentTask;
        if (this.dataType.IsNumeric()) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                byte[] buffer = new byte[this.cbDataType];
                for (int i = 0; i < srcList.Count; i++) {
                    task.CheckCancelled();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];

                    ulong searchA = 0, searchB = 0;
                    if (this.nextScanUsesFirstValue) {
                        searchA = GetNumericDataValueAsULong(res.FirstValue);
                    }
                    else if (this.nextScanUsesPreviousValue) {
                        searchB = GetNumericDataValueAsULong(res.PreviousValue);
                    }
                    else {
                        searchA = this.numericInputA;
                        searchB = this.numericInputB;
                    }

                    uint cbRead = await connection.ReadBytes(res.Address, buffer, 0, (uint) buffer.Length);
                    for (uint j = cbRead; j < this.cbDataType; j++) {
                        buffer[j] = 0;
                    }

                    IDataValue? matchBoxed;
                    switch (this.dataType) {
                        case DataType.Byte:   matchBoxed = this.CompareInt<byte>(buffer, searchA, searchB); break;
                        case DataType.Int16:  matchBoxed = this.CompareInt<short>(buffer, searchA, searchB); break;
                        case DataType.Int32:  matchBoxed = this.CompareInt<int>(buffer, searchA, searchB); break;
                        case DataType.Int64:  matchBoxed = this.CompareInt<long>(buffer, searchA, searchB); break;
                        case DataType.Float:  matchBoxed = this.CompareFloat<float>(buffer, searchA, searchB); break;
                        case DataType.Double: matchBoxed = this.CompareFloat<double>(buffer, searchA, searchB); break;
                        default:              throw new ArgumentOutOfRangeException();
                    }

                    if (matchBoxed != null) {
                        res.PreviousValue = res.CurrentValue;
                        res.CurrentValue = matchBoxed;
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else if (this.dataType == DataType.String) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                for (int i = 0; i < srcList.Count; i++) {
                    task.CheckCancelled();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    string search;
                    if (this.nextScanUsesFirstValue)
                        search = ((DataValueString) res.FirstValue).Value;
                    else if (this.nextScanUsesPreviousValue)
                        search = ((DataValueString) res.PreviousValue).Value;
                    else
                        search = this.inputA;

                    string readText = await connection.ReadString(res.Address, (uint) search.Length);
                    if (readText.Equals(search, this.stringComparison)) {
                        res.PreviousValue = res.CurrentValue;
                        res.CurrentValue = new DataValueString(readText, this.stringType);
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else if (this.dataType == DataType.ByteArray) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                for (int i = 0; i < srcList.Count; i++) {
                    task.CheckCancelled();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    MemoryPattern search;
                    if (this.nextScanUsesFirstValue) // first/prev use pattern even though they contain no wildcards. Makes code cleaner ;)
                        search = MemoryPattern.Create(((DataValueByteArray) res.FirstValue).Value);
                    else if (this.nextScanUsesPreviousValue)
                        search = MemoryPattern.Create(((DataValueByteArray) res.PreviousValue).Value);
                    else {
                        search = this.memoryPattern;
                        Debug.Assert(search.IsValid);
                    }

                    byte[] bytes = await connection.ReadBytes(res.Address, (uint) search.Length);
                    if (search.Matches(bytes)) {
                        res.PreviousValue = res.CurrentValue;
                        res.CurrentValue = new DataValueByteArray(bytes);
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else {
            Debug.Fail("Missing data type");
        }
    }

    private BaseNumericDataValue<T>? CompareInt<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IBinaryInteger<T> {
        return this.CompareInt<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }

    private BaseNumericDataValue<T>? CompareInt<T>(ReadOnlySpan<byte> searchValueBytes, ulong theInputA, ulong theInputB) where T : unmanaged, IBinaryInteger<T> {
        T value = ValueScannerUtils.CreateNumberFromBytes<T>(searchValueBytes);
        T valA = Unsafe.As<ulong, T>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.NotEquals:           return value != valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.LessThan:            return value < valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.GreaterThan:         return value > valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.Between: {
                T valB = Unsafe.As<ulong, T>(ref theInputB);
                return value >= valA && value <= valB ? IDataValue.CreateNumeric(value) : null;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private BaseFloatDataValue<T>? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IFloatingPoint<T> {
        return this.CompareFloat<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }

    private BaseFloatDataValue<T>? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes, ulong theInputA, ulong theInputB) where T : unmanaged, IFloatingPoint<T> {
        bool isFloat = typeof(T) == typeof(float);
        T value = ValueScannerUtils.CreateFloat<T>(searchValueBytes);
        
        // We convert everything to doubles when comparing, for higher accuracy
        double dblVal = this.GetDoubleFromReadValue(value, this.inputA);
        double valA = isFloat ? Unsafe.As<ulong, float>(ref theInputA) : Unsafe.As<ulong, double>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return Math.Abs(dblVal - valA) < this.epsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.NotEquals:           return Math.Abs(dblVal - valA) >= this.epsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThan:            return dblVal < valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThanOrEquals:    return dblVal <= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThan:         return dblVal > valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThanOrEquals: return dblVal >= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.Between: {
                double valB = isFloat ? Unsafe.As<ulong, float>(ref theInputB) : Unsafe.As<ulong, double>(ref theInputB);
                return dblVal >= valA && dblVal <= valB ? IDataValue.CreateFloat(value) : null;
            }
        }

        Debug.Fail("Unexpected exit");
        return null;
    }

    private double GetDoubleFromReadValue<T>(T readValue /* value from console */, string inputText /* user input value */) where T : unmanaged, IFloatingPoint<T> {
        double value = typeof(T) == typeof(float) ? Unsafe.As<T, float>(ref readValue) : Unsafe.As<T, double>(ref readValue);
        
        int idx = inputText.IndexOf('.');
        if (idx == -1 || idx == (inputText.Length - 1) /* last char, assume trimmed start+end */) {
            // can't truncate or round to decimal places, so just clip the decimals off
            return this.floatScanOption == FloatScanOption.TruncateToQuery ? Math.Truncate(value) : Math.Round(value);
        }
        else {
            // Say user searches for "24.3245"
            //               idx = 2 -> ^
            // decimals = len(7) - (idx(2) + 1) = 4
            // therefore, if readValue is 24.3245735, it either
            // gets truncated to 24.3245 or rounded to 24.3246
            int decimals = inputText.Length - (idx + 1);
            value = this.floatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateDouble(value, decimals) : Math.Round(value, decimals);
            return value;
        }
    }

    private static ulong GetNumericDataValueAsULong(IDataValue data) {
        switch (data.DataType) {
            case DataType.Byte:   return ((DataValueByte) data).Value;
            case DataType.Int16:  return (ulong) ((DataValueInt16) data).Value;
            case DataType.Int32:  return (ulong) ((DataValueInt32) data).Value;
            case DataType.Int64:  return (ulong) ((DataValueInt64) data).Value;
            case DataType.Float:  return BitConverter.SingleToUInt32Bits(((DataValueFloat) data).Value);
            case DataType.Double: return BitConverter.DoubleToUInt64Bits(((DataValueDouble) data).Value);
            case DataType.String:
            case DataType.ByteArray:
            default:
                throw new Exception("Invalid data type: " + data.DataType);
        }
    }

    [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
    private static bool TryParseNumeric(string text, DataType dataType, NumericDisplayType ndt, out ulong value /*, bool reverseEndianness*/) {
        const NumberStyles floatNs = NumberStyles.Integer | NumberStyles.AllowDecimalPoint;
        
        bool result;
        value = 0;
        NumberStyles intNumStyle = ndt == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (dataType) {
            case DataType.Byte: {
                if (result = byte.TryParse(text, intNumStyle, null, out byte val))
                    value = val;
                break;
            }
            case DataType.Int16: {
                if (result = short.TryParse(text, intNumStyle, null, out short val))
                    value = (ulong) val;
                break;
            }
            case DataType.Int32: {
                if (result = int.TryParse(text, intNumStyle, null, out int val))
                    value = (ulong) val;
                break;
            }
            case DataType.Int64: {
                if (result = long.TryParse(text, intNumStyle, null, out long val))
                    value = (ulong) val;
                break;
            }
            case DataType.Float: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (result = uint.TryParse(text, NumberStyles.HexNumber, null, out uint val)) {
                        value = val;
                    }
                }
                else if (result = float.TryParse(text, floatNs, null, out float val)) {
                    uint val2 = Unsafe.As<float, uint>(ref val);
                    value = val2;
                }

                break;
            }
            case DataType.Double: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (result = ulong.TryParse(text, NumberStyles.HexNumber, null, out ulong val)) {
                        value = val;
                    }
                }
                else if (result = double.TryParse(text, floatNs, null, out double val)) {
                    ulong val2 = Unsafe.As<double, ulong>(ref val);
                    value = val2;
                }

                break;
            }
            case DataType.String:    throw new ArgumentOutOfRangeException();
            case DataType.ByteArray: throw new ArgumentOutOfRangeException();
            default:
                value = 0;
                Debug.Assert(false, "Missing data type");
                return false;
        }

        return result;
    }
}