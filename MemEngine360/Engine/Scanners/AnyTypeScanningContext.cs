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

public class AnyTypeScanningContext : ScanningContext {
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
    private double? in_float;
    private double? in_double;

    private bool canSearchForString;
    private int cbString;
    private int cbDataMax;
    private char[]? charBuffer;
    private Decoder? stringDecoder;
    private DataType[]? intOrdering;
    internal MemoryPattern memoryPattern;
    private uint myOverlap;

    public override uint Overlap => this.myOverlap;

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public override event EventHandler<ScanResultViewModel>? ResultFound;

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
    /// <param name="connection1"></param>
    internal override async Task<bool> SetupCore(IConsoleConnection connection) {
        if (string.IsNullOrEmpty(this.inputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", "Input is empty");
            return false;
        }

        NumberStyles intNs = this.isIntInputHexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        UnknownDataTypeOptions udto = this.Processor.UnknownDataTypeOptions;
        if (udto.CanSearchForByte && byte.TryParse(this.inputA, intNs, null, out byte b))
            this.in_byte = b;
        if (udto.CanSearchForShort && short.TryParse(this.inputA, intNs, null, out short s))
            this.in_short = s;
        if (udto.CanSearchForInt && int.TryParse(this.inputA, intNs, null, out int i))
            this.in_int = i;
        if (udto.CanSearchForLong && long.TryParse(this.inputA, intNs, null, out long l))
            this.in_long = l;
        if (udto.CanSearchForFloat && double.TryParse(this.inputA, out double f) && f >= float.MinValue && f <= float.MaxValue)
            this.in_float = f;
        if (udto.CanSearchForDouble && double.TryParse(this.inputA, out double d))
            this.in_double = d;
        if (udto.CanRunNextScanForByteArray && this.Processor.HasDoneFirstScan)
            MemoryPattern.TryCompile(this.inputA, out this.memoryPattern);

        this.intOrdering = udto.GetIntDataTypeOrdering();

        // ReSharper disable once AssignmentInConditionalExpression
        if (this.canSearchForString = udto.CanSearchForString) {
            Encoding encoding = this.stringType.ToEncoding(this.isConnectionLittleEndian);
            int cbInputA = encoding.GetMaxByteCount(this.inputA.Length);
            if (cbInputA > DataTypedScanningContext.ChunkSize) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"Input is too long. We read data in chunks of {DataTypedScanningContext.ChunkSize / 1024}K, therefore, the string cannot contain more than that many bytes.", icon: MessageBoxIcons.ErrorIcon);
                return false;
            }

            this.cbString = cbInputA;
            this.charBuffer = new char[this.inputA.Length];
            this.stringDecoder = encoding.GetDecoder();
        }

        int maxSizePrimitive = this.in_long.HasValue || this.in_double.HasValue ? 8 : this.in_int.HasValue || this.in_float.HasValue ? 4 : (this.in_short.HasValue ? 2 : this.in_byte.HasValue ? 1 : 0);
        int maxSizeMemPat = this.memoryPattern.IsValid ? this.memoryPattern.Length : 0;
        this.cbDataMax = Math.Max(maxSizePrimitive, Math.Max(this.cbString, maxSizeMemPat));
        this.myOverlap = (uint) Math.Max((long) this.cbDataMax - this.alignment, 0);
        return true;
    }

    private bool CanSearchType(int sizeOfType, int bufferSize, uint idx) {
        // chunk      = 32
        // cbMaxType  = 8 (byte, short, int)
        // align      = 1
        // overlap    = 7 (8-1)
        // bufferSize = 39 (32+7)

        // say sizeOfType == sizeof(byte) (1)
        //   when idx = 31 (last byte in non-overlap):
        //     39 - (7 - sizeof(byte)) - 31 = 2 -> 2 > sizeof(byte) == true
        //   when idx = 32 (the byte index is in the overlap region):
        //     39 - (7 - sizeof(byte)) - 32 = 1 -> 1 > sizeof(byte) == false
        // say sizeOfType == sizeof(short) (2)
        //   when idx = 30 (last ushort in non-overlap area):
        //     39 - (7 - sizeof(short)) - 30 = 4 -> 4 > sizeof(short) == true
        //   when idx = 31 (require 1 byte into overlap last ushort in non-overlap area):
        //     39 - (7 - sizeof(short)) - 31 = 3 -> 3 > sizeof(short) == true
        //   when idx = 32 (first byte of short is in overlap region, so no good):
        //     39 - (7 - sizeof(short)) - 32 = 2 -> 2 > sizeof(short) == false
        // say sizeOfType == sizeof(int) (4)
        //   when idx = 28 (last int in non-overlap area):
        //     39 - (7 - sizeof(int)) - 28 = 8 -> 8 > sizeof(int) == true
        //   when idx = 31 (3 bytes into overlap):
        //     39 - (7 - sizeof(int)) - 31 = 5 -> 5 > sizeof(int) == true
        //   when idx = 32 (integer is entirely in overlap):
        //     39 - (7 - sizeof(int)) - 32 = 4 -> 4 > sizeof(int) == false
        // say sizeOfType == sizeof(long) (8)
        //   when idx = 24 (last long in non-overlap area):
        //     39 - (7 - sizeof(long)) - 24 = 16 -> 16 > sizeof(long) == true
        //   when idx = 31 (7 bytes into overlap):
        //     39 - (7 - sizeof(long)) - 31 = 9 -> 9 > sizeof(long) == true
        //   when idx = 32 (long is entirely in overlap):
        //     39 - (7 - sizeof(long)) - 32 = 4 -> 4 > sizeof(long) == false
        return bufferSize - (this.myOverlap - sizeOfType) - idx > sizeOfType;
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
                // TODO: maybe increment i by the size of data type that was found? or maybe we just stick with += alignment
                // E.g. If a file is full of a ushort with a hex value of 0x1010, it will
                // find it 37 times, since alignment is set to 1 
                switch (this.intOrdering![j]) {
                    case DataType.Byte when this.in_byte.HasValue && this.CanSearchType(sizeof(byte), buffer.Length, i): {
                        byte val = this.in_byte.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<byte>(buffer.Slice((int) i, sizeof(byte)), this.isConnectionLittleEndian), Unsafe.As<byte, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Byte, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                    case DataType.Int16 when this.in_short.HasValue && this.CanSearchType(sizeof(short), buffer.Length, i): {
                        short val = this.in_short.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<short>(buffer.Slice((int) i, sizeof(short)), this.isConnectionLittleEndian), Unsafe.As<short, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int16, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                    case DataType.Int32 when this.in_int.HasValue && this.CanSearchType(sizeof(int), buffer.Length, i): {
                        int val = this.in_int.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<int>(buffer.Slice((int) i, sizeof(int)), this.isConnectionLittleEndian), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }
                        // else if ((buffer.Length - sizeof(float)) >= sizeof(float) && (value = this.CompareIntFromFloat<int>(BinaryPrimitives.ReadSingleBigEndian(buffer.Slice((int) i, sizeof(float))), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                        //     this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.StringType, value));
                        // }
                        // else if ((buffer.Length - sizeof(double)) >= sizeof(double) && (value = this.CompareIntFromDouble<int>(BinaryPrimitives.ReadDoubleBigEndian(buffer.Slice((int) i, sizeof(double))), Unsafe.As<int, ulong>(ref val), 0)) != null) {
                        //     this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int32, intNdt, this.StringType, value));
                        // }

                        break;
                    }
                    case DataType.Int64 when this.in_long.HasValue && this.CanSearchType(sizeof(long), buffer.Length, i): {
                        long val = this.in_long.Value;
                        if ((value = this.CompareInt(ValueScannerUtils.CreateNumberFromBytes<long>(buffer.Slice((int) i, sizeof(long)), this.isConnectionLittleEndian), Unsafe.As<long, ulong>(ref val), 0)) != null) {
                            this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Int64, intNdt, this.stringType, value));
                            goto LoopEnd;
                        }

                        break;
                    }
                }
            }

            if (this.in_float.HasValue && this.CanSearchType(sizeof(float), buffer.Length, i)) {
                double val = this.in_float.Value;
                float readVal = ValueScannerUtils.CreateFloat<float>(buffer.Slice((int) i, sizeof(float)), this.isConnectionLittleEndian);
                if ((value = this.CompareFloat(readVal, Unsafe.As<double, ulong>(ref val), 0)) != null) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Float, NumericDisplayType.Normal, this.stringType, value));
                    continue;
                }
            }

            if (this.in_double.HasValue && this.CanSearchType(sizeof(double), buffer.Length, i)) {
                double val = this.in_double.Value;
                double readVal = ValueScannerUtils.CreateFloat<double>(buffer.Slice((int) i, sizeof(double)), this.isConnectionLittleEndian);
                if ((value = this.CompareFloat(readVal, Unsafe.As<double, ulong>(ref val), 0)) != null) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.Processor, address + i, DataType.Double, NumericDisplayType.Normal, this.stringType, value));
                    continue;
                }
            }

            if (this.canSearchForString && this.CanSearchType(this.cbString, buffer.Length, i)) {
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

    private DataValueNumeric<T>? CompareInt<T>(T value, ulong theInputA, ulong theInputB) where T : unmanaged, IBinaryInteger<T> {
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

    private DataValueFloatingPoint<T>? CompareFloat<T>(T value, ulong theInputA, ulong theInputB) where T : unmanaged, IFloatingPoint<T> {
        // We convert everything to doubles when comparing, for higher accuracy
        double dblVal = DataTypedScanningContext.GetDoubleFromReadValue(value, this.inputA, this.floatScanOption);
        double valA = Unsafe.As<ulong, double>(ref theInputA);
        double valB;
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return Math.Abs(dblVal - valA) < this.floatEpsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.NotEquals:           return Math.Abs(dblVal - valA) >= this.floatEpsilon ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThan:            return dblVal < valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.LessThanOrEquals:    return dblVal <= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThan:         return dblVal > valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.GreaterThanOrEquals: return dblVal >= valA ? IDataValue.CreateFloat(value) : null;
            case NumericScanType.Between: {
                valB = Unsafe.As<ulong, double>(ref theInputB);
                return dblVal >= valA && dblVal <= valB ? IDataValue.CreateFloat(value) : null;
            }
            case NumericScanType.NotBetween: {
                valB = Unsafe.As<ulong, double>(ref theInputB);
                return dblVal < valA || dblVal > valB ? IDataValue.CreateFloat(value) : null;
            }
        }

        Debug.Fail("Unexpected exit");
        return null;
    }

    internal override async Task PerformFirstScan(IConsoleConnection connection, Reference<IBusyToken?> busyTokenRef) {
        await new FirstTypedScanTask(this, connection, busyTokenRef).RunWithCurrentActivity();
    }

    public override Task<bool> CanRunNextScan(List<ScanResultViewModel> srcList) {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Performs a next scan. Requirements: the src list's items are guaranteed to have the
    /// same data types, and it equals our internal scanning data type
    /// </summary>
    /// <param name="connection">The connection to read values from</param>
    /// <param name="srcList">The source list of items</param>
    /// <param name="busyTokenRef"></param>
    internal override async Task PerformNextScan(IConsoleConnection connection, List<ScanResultViewModel> srcList, Reference<IBusyToken?> busyTokenRef) {
        ActivityTask task = ActivityTask.Current;
        using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
            for (int i = 0; i < srcList.Count; i++) {
                ScanResultViewModel res = srcList[i];
                task.ThrowIfCancellationRequested();
                task.Progress.Text = $"Reading values {i + 1}/{srcList.Count}";
                task.Progress.CompletionState.OnProgress(1.0);

                if (res.DataType.IsNumeric()) {
                    IDataValue? match = null;
                    switch (res.DataType) {
                        case DataType.Byte when this.in_byte is byte val:       match = this.CompareInt(await connection.ReadByte(res.Address), Unsafe.As<byte, ulong>(ref val), 0); break;
                        case DataType.Int16 when this.in_short is short val:    match = this.CompareInt(await connection.ReadValue<short>(res.Address), Unsafe.As<short, ulong>(ref val), 0); break;
                        case DataType.Int32 when this.in_int is int val:        match = this.CompareInt(await connection.ReadValue<int>(res.Address), Unsafe.As<int, ulong>(ref val), 0); break;
                        case DataType.Int64 when this.in_long is long val:      match = this.CompareInt(await connection.ReadValue<long>(res.Address), Unsafe.As<long, ulong>(ref val), 0); break;
                        case DataType.Float when this.in_float is double val:   match = this.CompareFloat(await connection.ReadValue<float>(res.Address), Unsafe.As<double, ulong>(ref val), 0); break;
                        case DataType.Double when this.in_double is double val: match = this.CompareFloat(await connection.ReadValue<double>(res.Address), Unsafe.As<double, ulong>(ref val), 0); break;
                    }

                    if (match != null) {
                        res.CurrentValue = res.PreviousValue = match;
                        this.ResultFound?.Invoke(this, res);
                    }
                }
                else if (res.DataType == DataType.String) {
                    using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                        Encoding encoding = this.stringType.ToEncoding(this.isConnectionLittleEndian);
                        int cbSearchTerm = encoding.GetMaxByteCount(this.inputA.Length);
                        byte[] dstByteBuffer = new byte[cbSearchTerm];
                        char[] dstCharBuffer = new char[encoding.GetMaxCharCount(cbSearchTerm)];

                        await connection.ReadBytes(res.Address, dstByteBuffer, 0, cbSearchTerm);
                        if (encoding.TryGetChars(dstByteBuffer, dstCharBuffer, out int cchRead)) {
                            if (new ReadOnlySpan<char>(dstCharBuffer, 0, cchRead).Equals(this.inputA.AsSpan(), this.stringComparison)) {
                                string text = new string(dstCharBuffer, 0, cchRead);
                                res.CurrentValue = res.PreviousValue = new DataValueString(text, this.stringType);
                                this.ResultFound?.Invoke(this, res);
                            }
                        }
                    }
                }
                else if (res.DataType == DataType.ByteArray) {
                    using (task.Progress.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                        MemoryPattern search = this.memoryPattern;
                        byte[] bytes = await connection.ReadBytes(res.Address, search.Length);
                        if (search.Matches(bytes)) {
                            res.CurrentValue = res.PreviousValue = new DataValueByteArray(bytes);
                            this.ResultFound?.Invoke(this, res);
                        }
                    }
                }
            }
        }
    }
}