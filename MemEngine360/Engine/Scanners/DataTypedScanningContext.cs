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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

public delegate void ScanningContextResultEventHandler(ScanningContext sender, ScanResultViewModel result);

/// <summary>
/// A class used to perform scanning operations. This class takes a snapshot of the options in <see cref="ScanningProcessor"/>
/// </summary>
public sealed class DataTypedScanningContext : ScanningContext {
    internal const int ChunkSize = 0x10000; // 65536
    internal readonly double floatEpsilon = BasicApplicationConfiguration.Instance.FloatingPointEpsilon;
    internal readonly string inputA, inputB;
    internal readonly bool isIntInputHexadecimal;
    internal readonly bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    internal readonly FloatScanOption floatScanOption;
    internal readonly StringType stringType;
    internal readonly DataType dataType;
    internal readonly NumericScanType numericScanType;
    internal readonly StringComparison stringComparison;
    internal readonly bool isSecondInputRequired;

    // enough bytes to store all data types except string and byte array
    // note: DataType.Float is parsed as double for extra precision
    internal ulong numericInputA, numericInputB;
    internal MemoryPattern memoryPattern;
    internal Decoder? stringDecoder;

    // number of bytes the data type takes up. for strings, calculates based on StringType and char count
    internal int cbDataType;

    // a char buffer for decoding chars from bytes
    private char[]? charBuffer;

    // engine's forced LE state is not automatic, and it forces an endianness different from the connection.
    // internal bool reverseEndianness;

    public override uint Overlap => (uint) Math.Max((long) this.cbDataType - this.alignment, 0);

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public override event ScanningContextResultEventHandler? ResultFound;

    public DataTypedScanningContext(ScanningProcessor processor) : base(processor) {
        Debug.Assert(!processor.ScanForAnyDataType);
        this.inputA = processor.InputA.Trim();
        this.inputB = processor.InputB.Trim();
        this.isIntInputHexadecimal = processor.IsIntInputHexadecimal;
        this.nextScanUsesFirstValue = processor.UseFirstValueForNextScan;
        this.nextScanUsesPreviousValue = processor.UsePreviousValueForNextScan;
        this.floatScanOption = processor.FloatScanOption;
        this.stringType = processor.StringScanOption;
        this.dataType = processor.DataType;
        this.numericScanType = processor.NumericScanType;
        this.stringComparison = processor.StringComparison;
        this.isSecondInputRequired = this.numericScanType.IsBetween()
                                     && this.dataType.IsNumeric()
                                     && !this.nextScanUsesFirstValue
                                     && !this.nextScanUsesPreviousValue;
    }

    /// <summary>
    /// Sets up the internal data using what is currently present in the scanning processor (e.g. parse input(s) as the correct data type).
    /// Returns true when scanning and proceed.
    /// False when there's errors (e.g. non-integer when scanning for an integer, or min is greater than max when scanning in 'between' mode)
    /// </summary>
    /// <param name="connection1"></param>
    internal override async Task<bool> SetupCore(IConsoleConnection connection1) {
        switch (this.dataType) {
            case DataType.Byte:   this.cbDataType = sizeof(byte); break;
            case DataType.Int16:  this.cbDataType = sizeof(short); break;
            case DataType.Int32:  this.cbDataType = sizeof(int); break;
            case DataType.Int64:  this.cbDataType = sizeof(long); break;
            case DataType.Float:  this.cbDataType = sizeof(float); break;
            case DataType.Double: this.cbDataType = sizeof(double); break;
            case DataType.String: {
                Encoding encoding = this.stringType.ToEncoding(this.isConnectionLittleEndian);
                int cbInputA = encoding.GetMaxByteCount(this.inputA.Length);
                if (cbInputA > ChunkSize) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", $"Input is too long. We read data in chunks of {ChunkSize / 1024}K, therefore, the string cannot contain more than that many bytes.");
                    return false;
                }

                this.cbDataType = cbInputA;
                this.charBuffer = new char[this.inputA.Length];
                this.stringDecoder = encoding.GetDecoder();
                break;
            }
            case DataType.ByteArray: {
                if (!MemoryPattern.TryCompile(this.inputA, out this.memoryPattern, true, out string? errorMessage)) {
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

        Debug.Assert(this.cbDataType > 0);

        IConsoleConnection connection = this.Processor.MemoryEngine.Connection!;
        Debug.Assert(connection != null);
        // this.reverseEndianness = this.theProcessor.MemoryEngine360.IsForcedLittleEndian is bool forcedLittle && forcedLittle != connection.IsLittleEndian;

        if (this.Processor.HasDoneFirstScan && (this.nextScanUsesFirstValue || this.nextScanUsesPreviousValue)) {
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
                    case DataType.Float:  isBackward = Unsafe.As<ulong, double>(ref this.numericInputA) > Unsafe.As<ulong, double>(ref this.numericInputB); break;
                    case DataType.Double: isBackward = Unsafe.As<ulong, double>(ref this.numericInputA) > Unsafe.As<ulong, double>(ref this.numericInputB); break;
                    case DataType.ByteArray:
                    case DataType.String:
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }

                if (isBackward) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", "You put them in the wrong way around!", $"FROM is greater than TO ({this.inputA} > {this.inputB})");
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
    internal override void ProcessMemoryBlockForFirstScan(uint address, ReadOnlySpan<byte> buffer) {
        // by default, align is set to cbDataType except for string where it's 1. So in most cases, only check bounds for strings
        // There's also another issue with values between chunks, which we don't process because I can't get it to work...

        bool checkBounds = this.alignment < this.cbDataType, isString;
        ReadOnlySpan<byte> memory;
        if (this.dataType.IsNumeric()) {
            for (uint i = 0; i < buffer.Length; i += this.alignment) {
                if (checkBounds && (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                memory = buffer.Slice((int) i, this.cbDataType);

                IDataValue? matchBoxed;
                switch (this.dataType) {
                    case DataType.Byte:   matchBoxed = this.CompareInt<byte>(memory); break;
                    case DataType.Int16:  matchBoxed = this.CompareInt<short>(memory); break;
                    case DataType.Int32:  matchBoxed = this.CompareInt<int>(memory); break;
                    case DataType.Int64:  matchBoxed = this.CompareInt<long>(memory); break;
                    case DataType.Float:  matchBoxed = this.CompareFloat<float>(memory); break;
                    case DataType.Double: matchBoxed = this.CompareFloat<double>(memory); break;
                    default:
                        Debug.Fail("Invalid data type");
                        return;
                }

                if (matchBoxed != null) {
                    NumericDisplayType ndt = this.isIntInputHexadecimal && this.dataType.IsInteger() ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, this.dataType, ndt, this.stringType, matchBoxed));
                }
            }
        }
        else if ((isString = this.dataType == DataType.String) || this.dataType == DataType.ByteArray) {
            for (uint i = 0; i < buffer.Length; i += this.alignment) {
                if (checkBounds && (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                memory = buffer.Slice((int) i, this.cbDataType);
                if (isString) {
                    int cchUsed;
                    try {
                        this.stringDecoder!.Convert(memory, this.charBuffer.AsSpan(), true, out _, out cchUsed, out _);
                    }
                    catch {
                        // failed to decode chars so skip
                        continue;
                    }

                    ReadOnlySpan<char> chars = new ReadOnlySpan<char>(this.charBuffer, 0, cchUsed);
                    if (chars.Equals(this.inputA.AsSpan(), this.stringComparison)) {
                        this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, this.dataType, NumericDisplayType.Normal, this.stringType, new DataValueString(new string(chars), this.stringType)));
                    }
                }
                else {
                    Debug.Assert(this.memoryPattern.IsValid);
                    if (this.memoryPattern.Matches(memory)) {
                        this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, this.dataType, NumericDisplayType.Normal, this.stringType, new DataValueByteArray(memory.ToArray())));
                    }
                }
            }
        }
        else {
            Debug.Fail("Missing data type");
        }
    }

    internal override async Task PerformFirstScan(IConsoleConnection connection, Reference<IDisposable?> busyTokenRef) {
        await new FirstTypedScanTask(this, connection, busyTokenRef).RunWithCurrentActivity();
    }

    public override async Task<bool> CanRunNextScan(List<ScanResultViewModel> srcList) {
        bool hasDifferentDataTypes = false;
        DataType firstDataType = this.dataType;
        if (srcList.Count > 0) {
            firstDataType = srcList[0].DataType;
            for (int i = 1; i < srcList.Count; i++) {
                if (srcList[i].DataType != firstDataType) {
                    hasDifferentDataTypes = true;
                    break;
                }
            }
        }

        if (hasDifferentDataTypes) {
            await IMessageDialogService.Instance.ShowMessage("Error", "Result list contains results with different data types. Toggle the \"Any\" (unknown data type) button to search using these results");
            return false;
        }
        else if (firstDataType != this.dataType) {
            await IMessageDialogService.Instance.ShowMessage("Error", $"Search data type is different to the search results. You're searching for {this.dataType}, but the results contain {firstDataType}");
            return false;
        }

        return true;
    }

    internal override async Task PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList, Reference<IDisposable?> busyTokenRef) {
        ActivityTask task = ActivityManager.Instance.CurrentTask;
        if (this.dataType.IsNumeric()) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                byte[] buffer = new byte[this.cbDataType];
                for (int i = 0; i < srcList.Count; i++) {
                    task.ThrowIfCancellationRequested();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];

                    ulong searchA, searchB = 0;
                    if (this.nextScanUsesFirstValue) {
                        searchA = GetNumericDataValueAsULong(res.FirstValue);
                    }
                    else if (this.nextScanUsesPreviousValue) {
                        searchA = GetNumericDataValueAsULong(res.PreviousValue);
                    }
                    else {
                        searchA = this.numericInputA;
                        searchB = this.numericInputB;
                    }

                    await connection.ReadBytes(res.Address, buffer, 0, buffer.Length);

                    IDataValue? match;
                    switch (this.dataType) {
                        case DataType.Byte:   match = this.CompareInt<byte>(buffer, searchA, searchB); break;
                        case DataType.Int16:  match = this.CompareInt<short>(buffer, searchA, searchB); break;
                        case DataType.Int32:  match = this.CompareInt<int>(buffer, searchA, searchB); break;
                        case DataType.Int64:  match = this.CompareInt<long>(buffer, searchA, searchB); break;
                        case DataType.Float:  match = this.CompareFloat<float>(buffer, searchA, searchB); break;
                        case DataType.Double: match = this.CompareFloat<double>(buffer, searchA, searchB); break;
                        default:              throw new ArgumentOutOfRangeException();
                    }

                    if (match != null) {
                        res.CurrentValue = res.PreviousValue = match;
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else if (this.dataType == DataType.String) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                Encoding encoding = this.stringType.ToEncoding(this.isConnectionLittleEndian);
                bool useInputValue = !this.nextScanUsesFirstValue && !this.nextScanUsesPreviousValue;
                int cbInputValue = useInputValue ? encoding.GetMaxByteCount(this.inputA.Length) : 0;
                byte[]? inputByteBuffer = useInputValue ? new byte[cbInputValue] : null;
                char[]? inputCharBuffer = useInputValue ? new char[encoding.GetMaxCharCount(cbInputValue)] : null;
                for (int i = 0; i < srcList.Count; i++) {
                    task.ThrowIfCancellationRequested();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    string search;
                    int cbSearchTerm;
                    byte[] dstByteBuffer;
                    char[] dstCharBuffer;
                    if (useInputValue) {
                        search = this.inputA;
                        cbSearchTerm = cbInputValue;
                        dstByteBuffer = inputByteBuffer!;
                        dstCharBuffer = inputCharBuffer!;
                    }
                    else {
                        if (this.nextScanUsesFirstValue) {
                            search = ((DataValueString) res.FirstValue).Value;
                        }
                        else {
                            Debug.Assert(this.nextScanUsesPreviousValue);
                            search = ((DataValueString) res.PreviousValue).Value;
                        }

                        cbSearchTerm = encoding.GetMaxByteCount(search.Length);
                        dstByteBuffer = new byte[cbSearchTerm];
                        dstCharBuffer = new char[encoding.GetMaxCharCount(cbSearchTerm)];
                    }

                    // int cchBuffer = this.StringType.ToEncoding().GetChars(memory, this.charBuffer.AsSpan());

                    await connection.ReadBytes(res.Address, dstByteBuffer, 0, cbSearchTerm);
                    if (encoding.TryGetChars(dstByteBuffer, dstCharBuffer, out int cchRead)) {
                        if (new ReadOnlySpan<char>(dstCharBuffer, 0, cchRead).Equals(search.AsSpan(), this.stringComparison)) {
                            string text = new string(dstCharBuffer, 0, cchRead);
                            res.CurrentValue = res.PreviousValue = new DataValueString(text, this.stringType);
                            this.ResultFound?.Invoke(this, res);
                        }
                    }
                }
            }
        }
        else if (this.dataType == DataType.ByteArray) {
            using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                for (int i = 0; i < srcList.Count; i++) {
                    task.ThrowIfCancellationRequested();
                    task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                    task.Progress.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    MemoryPattern search;
                    if (this.nextScanUsesFirstValue)
                        search = MemoryPattern.Create(((DataValueByteArray) res.FirstValue).Value);
                    else if (this.nextScanUsesPreviousValue)
                        search = MemoryPattern.Create(((DataValueByteArray) res.PreviousValue).Value);
                    else {
                        search = this.memoryPattern;
                        Debug.Assert(search.IsValid);
                    }

                    byte[] bytes = await connection.ReadBytes(res.Address, search.Length);
                    if (search.Matches(bytes)) {
                        res.CurrentValue = res.PreviousValue = new DataValueByteArray(bytes);
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
        T value = ValueScannerUtils.CreateNumberFromBytes<T>(searchValueBytes, this.isConnectionLittleEndian);
        T valA = Unsafe.As<ulong, T>(ref theInputA), valB;
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.NotEquals:           return value != valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.LessThan:            return value < valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.GreaterThan:         return value > valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? IDataValue.CreateNumeric(value) : null;
            case NumericScanType.Between: {
                valB = Unsafe.As<ulong, T>(ref theInputB);
                return value >= valA && value <= valB ? IDataValue.CreateNumeric(value) : null;
            }
            case NumericScanType.NotBetween: {
                valB = Unsafe.As<ulong, T>(ref theInputB);
                return value < valA || value > valB ? IDataValue.CreateNumeric(value) : null;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private BaseFloatDataValue<T>? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IFloatingPoint<T> {
        return this.CompareFloat<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }

    private BaseFloatDataValue<T>? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes, ulong theInputA, ulong theInputB) where T : unmanaged, IFloatingPoint<T> {
        T preProcessedValue = ValueScannerUtils.CreateFloat<T>(searchValueBytes, this.isConnectionLittleEndian);

        // We convert everything to doubles when comparing, for higher accuracy.
        // InputA and InputB are parsed as doubles, even when the DataType is Float

        string str = this.nextScanUsesFirstValue || this.nextScanUsesPreviousValue ? "0.000000" : this.inputA;
        double valToCmp = GetDoubleFromReadValue(preProcessedValue, str, this.floatScanOption);
        double valA = Unsafe.As<ulong, double>(ref theInputA);
        double valB;
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return Math.Abs(valToCmp - valA) < this.floatEpsilon ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.NotEquals:           return !(Math.Abs(valToCmp - valA) < this.floatEpsilon) ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.LessThan:            return valToCmp < valA ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.LessThanOrEquals:    return valToCmp <= valA ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.GreaterThan:         return valToCmp > valA ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.GreaterThanOrEquals: return valToCmp >= valA ? IDataValue.CreateFloat(preProcessedValue) : null;
            case NumericScanType.Between: {
                valB = Unsafe.As<ulong, double>(ref theInputB);
                return valToCmp >= valA && valToCmp <= valB ? IDataValue.CreateFloat(preProcessedValue) : null;
            }
            case NumericScanType.NotBetween: {
                valB = Unsafe.As<ulong, double>(ref theInputB);
                return valToCmp < valA || valToCmp > valB ? IDataValue.CreateFloat(preProcessedValue) : null;
            }
        }

        Debug.Fail("Unexpected exit");
        return null;
    }

    public static double GetDoubleFromReadValue<T>(T readValue /* value from console */, string inputText /* user input value */, FloatScanOption scanOption) where T : unmanaged, IFloatingPoint<T> {
        double value = typeof(T) == typeof(float) ? Unsafe.As<T, float>(ref readValue) : Unsafe.As<T, double>(ref readValue);

        int idx = inputText.IndexOf('.');
        if (idx == -1 || idx == (inputText.Length - 1) /* last char, assume trimmed start+end */) {
            // just clip the decimals off
            return scanOption == FloatScanOption.TruncateToQuery ? Math.Truncate(value) : Math.Round(value);
        }
        else {
            // Say user searches for "24.3245"
            //               idx = 2 -> ^
            // decimals = len(7) - (idx(2) + 1) = 4
            // therefore, if readValue is 24.3245735, it either
            // gets truncated to 24.3245 or rounded to 24.3246
            int decimals = inputText.Length - (idx + 1);
            value = scanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateDouble(value, decimals) : Math.Round(value, decimals);
            return value;
        }
    }

    private static ulong GetNumericDataValueAsULong(IDataValue data) {
        switch (data.DataType) {
            case DataType.Byte:  return ((DataValueByte) data).Value;
            case DataType.Int16: return (ulong) ((DataValueInt16) data).Value;
            case DataType.Int32: return (ulong) ((DataValueInt32) data).Value;
            case DataType.Int64: return (ulong) ((DataValueInt64) data).Value;
            case DataType.Float: {
                double v = ((DataValueFloat) data).Value;
                return Unsafe.As<double, ulong>(ref v);
            }
            case DataType.Double: {
                double v = ((DataValueDouble) data).Value;
                return Unsafe.As<double, ulong>(ref v);
            }
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
                // we parse float inputs as double for extra precision.
                // E.g. parsing '-2234.6187' as a float results in -2234.61865
                else if (result = double.TryParse(text, floatNs, null, out double val)) {
                    if (val >= float.MinValue && val <= float.MaxValue) {
                        value = Unsafe.As<double, ulong>(ref val);
                    }
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
                    value = Unsafe.As<double, ulong>(ref val);
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