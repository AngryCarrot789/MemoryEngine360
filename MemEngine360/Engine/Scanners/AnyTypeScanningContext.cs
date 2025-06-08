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
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using MemEngine360.Configs;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils;

namespace MemEngine360.Engine.Scanners;

public class AnyTypeScanningContext : ScanningContext {
    internal const int ChunkSize = 0x10000; // 65536
    internal readonly double floatEpsilon = BasicApplicationConfiguration.Instance.FloatingPointEpsilon;
    internal readonly string inputA, inputB;
    internal readonly bool isIntInputHexadecimal;
    internal readonly FloatScanOption floatScanOption;
    internal readonly StringType stringType;
    internal readonly NumericScanType numericScanType;
    internal readonly StringComparison stringComparison;

    // input values parsed as the given data types. null when the scanning
    // options do not allow the specific data type to be scanned
    private byte? in_byte;
    private short? in_short;
    private int? in_int;
    private long? in_long;
    private float? in_float;
    private double? in_double;
    private RoundingMode f2int;

    private bool canSearchForString;
    private int cbString;
    private char[]? charBuffer;
    private Decoder? stringDecoder;
    private DataType[] intOrdering;

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public override event ScanningContextResultEventHandler? ResultFound;

    public AnyTypeScanningContext(ScanningProcessor processor) : base(processor) {
        Debug.Assert(processor.ScanForAnyDataType);

        this.inputA = processor.InputA.Trim();
        this.inputB = processor.InputB.Trim();
        this.isIntInputHexadecimal = processor.IsIntInputHexadecimal;
        this.floatScanOption = processor.FloatScanOption;
        this.stringType = processor.StringScanOption;
        this.numericScanType = processor.NumericScanType;
        this.stringComparison = processor.StringComparison;
    }

    /// <summary>
    /// Sets up the internal data using what is currently present in the scanning processor (e.g. parse input(s) as the correct data type).
    /// Returns true when scanning and proceed.
    /// False when there's errors (e.g. non-integer when scanning for an integer, or min is greater than max when scanning in 'between' mode)
    /// </summary>
    internal override async Task<bool> Setup() {
        IConsoleConnection connection = this.Processor.MemoryEngine360.Connection!;
        Debug.Assert(connection != null);

        if (string.IsNullOrEmpty(this.inputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", "Input is empty");
            return false;
        }

        NumberStyles intNs = this.isIntInputHexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        {
            UnknownDataTypeOptions udto = this.Processor.UnknownDataTypeOptions;
            this.f2int = udto.FloatToIntRounding;
            if (udto.CanSearchForByte && byte.TryParse(this.inputA, intNs, null, out byte b))
                this.in_byte = b;
            if (udto.CanSearchForShort && short.TryParse(this.inputA, intNs, null, out short s))
                this.in_short = s;
            if (udto.CanSearchForInt && int.TryParse(this.inputA, intNs, null, out int i))
                this.in_int = i;
            if (udto.CanSearchForLong && long.TryParse(this.inputA, intNs, null, out long l))
                this.in_long = l;
            if (udto.CanSearchForFloat && float.TryParse(this.inputA, out float f))
                this.in_float = f;
            if (udto.CanSearchForDouble && double.TryParse(this.inputA, out double d))
                this.in_double = d;

            this.intOrdering = udto.IntDataTypeOrdering.CloneArrayUnsafe()!;
            Debug.Assert(this.intOrdering.Length == 4);
            foreach (DataType dt in this.intOrdering) {
                Debug.Assert(dt.IsInteger());
            }

            // ReSharper disable once AssignmentInConditionalExpression
            if (this.canSearchForString = udto.CanSearchForString) {
                Encoding encoding = this.stringType.ToEncoding();
                int cbInputA = encoding.GetMaxByteCount(this.inputA.Length);
                if (cbInputA > ChunkSize) {
                    await IMessageDialogService.Instance.ShowMessage("Invalid input", $"Input is too long. We read data in chunks of {ChunkSize / 1024}K, therefore, the string cannot contain more than that many bytes.");
                    return false;
                }

                this.cbString = cbInputA;
                this.charBuffer = new char[this.inputA.Length];
                this.stringDecoder = encoding.GetDecoder();
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
        IDataValue? value;
        NumericDisplayType intNdt = this.isIntInputHexadecimal ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
        for (uint i = 0; i < buffer.Length; i += this.alignment) {
            for (int j = 0; j < 4; j++) {
                switch (this.intOrdering[j]) {
                    case DataType.Byte when this.in_byte.HasValue && (buffer.Length - i) >= sizeof(byte): {
                        byte val = this.in_byte.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<byte>(buffer.Slice((int) i, sizeof(byte))), Unsafe.As<byte, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Byte, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                    case DataType.Int16 when this.in_short.HasValue && (buffer.Length - i) >= sizeof(short): {
                        short val = this.in_short.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<short>(buffer.Slice((int) i, sizeof(short))), Unsafe.As<short, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int16, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                    case DataType.Int32 when this.in_int.HasValue && (buffer.Length - i) >= sizeof(int): {
                        int val = this.in_int.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<int>(buffer.Slice((int) i, sizeof(int))), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }
                        // else if ((buffer.Length - sizeof(float)) >= sizeof(float) && (value = this.CompareIntFromFloat<int>(BinaryPrimitives.ReadSingleBigEndian(buffer.Slice((int) i, sizeof(float))), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                        //     this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.stringType, value));
                        // }
                        // else if ((buffer.Length - sizeof(double)) >= sizeof(double) && (value = this.CompareIntFromDouble<int>(BinaryPrimitives.ReadDoubleBigEndian(buffer.Slice((int) i, sizeof(double))), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                        //     this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.stringType, value));
                        // }

                        break;
                    }
                    case DataType.Int64 when this.in_long.HasValue && (buffer.Length - i) >= sizeof(long): {
                        long val = this.in_long.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<long>(buffer.Slice((int) i, sizeof(long))), Unsafe.As<long, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int64, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                    default: Debug.Fail("Memory Corruption of int ordering array"); break;
                }
            }

            if (this.in_float.HasValue && (buffer.Length - i) >= sizeof(float)) {
                float val = this.in_float.Value;
                float readVal = ValueScannerUtils.CreateFloat<float>(buffer.Slice((int) i, sizeof(float)));
                if ((value = this.CompareFloat(readVal, Unsafe.As<float, ulong>(ref val), 0)) != null) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Float, NumericDisplayType.Normal, this.stringType, value));
                    continue;
                }
            }

            if (this.in_double.HasValue && (buffer.Length - i) >= sizeof(double)) {
                double val = this.in_double.Value;
                double readVal = ValueScannerUtils.CreateFloat<double>(buffer.Slice((int) i, sizeof(double)));
                if ((value = this.CompareFloat(readVal, Unsafe.As<double, ulong>(ref val), 0)) != null) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Double, NumericDisplayType.Normal, this.stringType, value));
                    continue;
                }
            }

            if (this.canSearchForString && (buffer.Length - i) >= this.cbString) {
                ReadOnlySpan<byte> memory = buffer.Slice((int) i, this.cbString);
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
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.String, NumericDisplayType.Normal, this.stringType, new DataValueString(new string(chars), this.stringType)));
                    continue;
                }
            }

            LoopEnd: ;
        }
    }

    private BaseNumericDataValue<T>? CompareInt<T>(T value, ulong theInputA, ulong theInputB) where T : unmanaged, IBinaryInteger<T> {
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

    // private BaseNumericDataValue<T>? CompareIntFromFloat<T>(float floatValue, ulong theInputA, ulong theInputB) where T : unmanaged, IBinaryInteger<T> {
    //     T valA = Unsafe.As<ulong, T>(ref theInputA), valB;
    //     switch (this.numericScanType) {
    //         case NumericScanType.Equals:              return value == valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.NotEquals:           return value != valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.LessThan:            return value < valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.LessThanOrEquals:    return value <= valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.GreaterThan:         return value > valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.GreaterThanOrEquals: return value >= valA ? IDataValue.CreateNumeric(value) : null;
    //         case NumericScanType.Between: {
    //             valB = Unsafe.As<ulong, T>(ref theInputB);
    //             return value >= valA && value <= valB ? IDataValue.CreateNumeric(value) : null;
    //         }
    //         case NumericScanType.NotBetween: {
    //             valB = Unsafe.As<ulong, T>(ref theInputB);
    //             return value < valA || value > valB ? IDataValue.CreateNumeric(value) : null;
    //         }
    //         default: throw new ArgumentOutOfRangeException();
    //     }
    // }

    private BaseFloatDataValue<T>? CompareFloat<T>(T value, ulong theInputA, ulong theInputB) where T : unmanaged, IFloatingPoint<T> {
        bool isFloat = typeof(T) == typeof(float);
        // We convert everything to doubles when comparing, for higher accuracy
        double dblVal = this.GetDoubleFromReadValue(value, this.inputA);
        double valA = isFloat ? Unsafe.As<ulong, float>(ref theInputA) : Unsafe.As<ulong, double>(ref theInputA), valB;
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return Math.Abs(dblVal - valA) < this.floatEpsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.NotEquals:           return Math.Abs(dblVal - valA) >= this.floatEpsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThan:            return dblVal < valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThanOrEquals:    return dblVal <= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThan:         return dblVal > valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThanOrEquals: return dblVal >= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.Between: {
                valB = isFloat ? Unsafe.As<ulong, float>(ref theInputB) : Unsafe.As<ulong, double>(ref theInputB);
                return dblVal < valA || dblVal > valB ? IDataValue.CreateFloat(value) : null;
            }
            case NumericScanType.NotBetween: {
                valB = isFloat ? Unsafe.As<ulong, float>(ref theInputB) : Unsafe.As<ulong, double>(ref theInputB);
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
            // just clip the decimals off
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

    internal override async Task<IDisposable?> PerformFirstScan(IConsoleConnection connection, IDisposable busyToken) {
        FirstTypedScanTask task = new FirstTypedScanTask(this, connection, busyToken);
        await task.RunWithCurrentActivity();
        return task.BusyToken;
    }

    /// <summary>
    /// Performs a next scan. Requirements: the src list's items are guaranteed to have the
    /// same data types, and it equals our internal scanning data type
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="srcList">The source list of items</param>
    /// <param name="busyToken"></param>
    internal override async Task<IDisposable?> PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList, IDisposable busyToken) {
        await IMessageDialogService.Instance.ShowMessage("Unsupported", "Next Scan not supported for any data type mode yet");
        
        // ActivityTask task = ActivityManager.Instance.CurrentTask;
        // if (this.dataType.IsNumeric()) {
        //     using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
        //         byte[] buffer = new byte[this.cbDataType];
        //         for (int i = 0; i < srcList.Count; i++) {
        //             task.CheckCancelled();
        //             task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
        //             task.Progress.CompletionState.OnProgress(1.0);
        //
        //             ScanResultViewModel res = srcList[i];
        //
        //             ulong searchA = 0, searchB = 0;
        //             if (this.nextScanUsesFirstValue) {
        //                 searchA = GetNumericDataValueAsULong(res.FirstValue);
        //             }
        //             else if (this.nextScanUsesPreviousValue) {
        //                 searchB = GetNumericDataValueAsULong(res.PreviousValue);
        //             }
        //             else {
        //                 searchA = this.numericInputA;
        //                 searchB = this.numericInputB;
        //             }
        //
        //             await connection.ReadBytes(res.Address, buffer, 0, buffer.Length);
        //
        //             IDataValue? match;
        //             switch (this.dataType) {
        //                 case DataType.Byte:   match = this.CompareInt<byte>(buffer, searchA, searchB); break;
        //                 case DataType.Int16:  match = this.CompareInt<short>(buffer, searchA, searchB); break;
        //                 case DataType.Int32:  match = this.CompareInt<int>(buffer, searchA, searchB); break;
        //                 case DataType.Int64:  match = this.CompareInt<long>(buffer, searchA, searchB); break;
        //                 case DataType.Float:  match = this.CompareFloat<float>(buffer, searchA, searchB); break;
        //                 case DataType.Double: match = this.CompareFloat<double>(buffer, searchA, searchB); break;
        //                 default:              throw new ArgumentOutOfRangeException();
        //             }
        //
        //             if (match != null) {
        //                 res.CurrentValue = res.PreviousValue = match;
        //                 this.ResultFound?.Invoke(this, res);
        //             }
        //         }
        //     }
        // }
        // else if (this.dataType == DataType.String) {
        //     using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
        //         Encoding encoding = this.stringType.ToEncoding();
        //         bool useInputValue = !this.nextScanUsesFirstValue && !this.nextScanUsesPreviousValue;
        //         int cbInputValue = useInputValue ? encoding.GetMaxByteCount(this.inputA.Length) : 0;
        //         byte[]? inputByteBuffer = useInputValue ? new byte[cbInputValue] : null;
        //         char[]? inputCharBuffer = useInputValue ? new char[encoding.GetMaxCharCount(cbInputValue)] : null;
        //         for (int i = 0; i < srcList.Count; i++) {
        //             task.CheckCancelled();
        //             task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
        //             task.Progress.CompletionState.OnProgress(1.0);
        //
        //             ScanResultViewModel res = srcList[i];
        //             string search;
        //             int cbSearchTerm;
        //             byte[] dstByteBuffer;
        //             char[] dstCharBuffer;
        //             if (useInputValue) {
        //                 search = this.inputA;
        //                 cbSearchTerm = cbInputValue;
        //                 dstByteBuffer = inputByteBuffer!;
        //                 dstCharBuffer = inputCharBuffer!;
        //             }
        //             else {
        //                 if (this.nextScanUsesFirstValue) {
        //                     search = ((DataValueString) res.FirstValue).Value;
        //                 }
        //                 else {
        //                     Debug.Assert(this.nextScanUsesPreviousValue);
        //                     search = ((DataValueString) res.PreviousValue).Value;
        //                 }
        //
        //                 cbSearchTerm = encoding.GetMaxByteCount(search.Length);
        //                 dstByteBuffer = new byte[cbSearchTerm];
        //                 dstCharBuffer = new char[encoding.GetMaxCharCount(cbSearchTerm)];
        //             }
        //
        //             // int cchBuffer = this.stringType.ToEncoding().GetChars(memory, this.charBuffer.AsSpan());
        //
        //             await connection.ReadBytes(res.Address, dstByteBuffer, 0, cbSearchTerm);
        //             if (encoding.TryGetChars(dstByteBuffer, dstCharBuffer, out int cchRead)) {
        //                 if (new ReadOnlySpan<char>(dstCharBuffer, 0, cchRead).Equals(search.AsSpan(), this.stringComparison)) {
        //                     string text = new string(dstCharBuffer, 0, cchRead);
        //                     res.CurrentValue = res.PreviousValue = new DataValueString(text, this.stringType);
        //                     this.ResultFound?.Invoke(this, res);
        //                 }
        //             }
        //         }
        //     }
        // }
        // else if (this.dataType == DataType.ByteArray) {
        //     using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
        //         for (int i = 0; i < srcList.Count; i++) {
        //             task.CheckCancelled();
        //             task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
        //             task.Progress.CompletionState.OnProgress(1.0);
        //
        //             ScanResultViewModel res = srcList[i];
        //             MemoryPattern search;
        //             if (this.nextScanUsesFirstValue)
        //                 search = MemoryPattern.Create(((DataValueByteArray) res.FirstValue).Value);
        //             else if (this.nextScanUsesPreviousValue)
        //                 search = MemoryPattern.Create(((DataValueByteArray) res.PreviousValue).Value);
        //             else {
        //                 search = this.memoryPattern;
        //                 Debug.Assert(search.IsValid);
        //             }
        //
        //             byte[] bytes = await connection.ReadBytes(res.Address, search.Length);
        //             if (search.Matches(bytes)) {
        //                 res.CurrentValue = res.PreviousValue = new DataValueByteArray(bytes);
        //                 this.ResultFound?.Invoke(this, res);
        //             }
        //         }
        //     }
        // }
        // else {
        //     Debug.Fail("Missing data type");
        // }

        return busyToken;
    }
}