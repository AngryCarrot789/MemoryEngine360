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
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Engine.Scanners;

public delegate void ScanningContextResultEventHandler(ScanningContext sender, ScanResultViewModel result);

public class ScanningContext {
    private const int ChunkSize = 0x10000; // 65536
    private readonly ScanningProcessor theProcessor;
    private readonly string inputA, inputB;
    private readonly uint startAddress, scanLength;
    private readonly uint alignment;
    private readonly bool pauseConsoleDuringScan, scanMemoryPages, isIntInputHexadecimal;
    private readonly bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    private readonly FloatScanOption floatScanOption;
    private readonly StringType stringScanOption;
    private readonly DataType dataType;
    private readonly NumericScanType numericScanType;
    private readonly bool isSecondInputRequired;

    // enough bytes to store all data types except string
    private long numericInputA, numericInputB;
    private int cbDataType;
    private NumberStyles numberStyles;

    /// <summary>
    /// Fired when a result is found
    /// </summary>
    public event ScanningContextResultEventHandler ResultFound;

    public ScanningContext(ScanningProcessor processor) {
        this.theProcessor = processor;
        this.inputA = processor.InputA;
        this.inputB = processor.InputB;
        this.startAddress = processor.StartAddress;
        this.scanLength = processor.ScanLength;
        this.alignment = processor.Alignment;
        this.pauseConsoleDuringScan = processor.PauseConsoleDuringScan;
        this.scanMemoryPages = processor.ScanMemoryPages;
        this.isIntInputHexadecimal = processor.IsIntInputHexadecimal;
        this.nextScanUsesFirstValue = processor.UseFirstValueForNextScan;
        this.nextScanUsesPreviousValue = processor.UsePreviousValueForNextScan;
        this.floatScanOption = processor.FloatScanOption;
        this.stringScanOption = processor.StringScanOption;
        this.dataType = processor.DataType;
        this.numericScanType = processor.NumericScanType;
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
        if (this.dataType.IsInteger()) {
            this.numberStyles = this.isIntInputHexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        }
        else if (this.dataType.IsFloat()) {
            this.numberStyles = NumberStyles.Float | NumberStyles.AllowThousands;
        }
        else if (this.dataType.IsNumeric()) {
            Debug.Fail("Missing data type");
            return false;
        }
        
        switch (this.dataType) {
            case DataType.Byte:   this.cbDataType = sizeof(byte); break;
            case DataType.Int16:  this.cbDataType = sizeof(short); break;
            case DataType.Int32:  this.cbDataType = sizeof(int); break;
            case DataType.Int64:  this.cbDataType = sizeof(long); break;
            case DataType.Float:  this.cbDataType = sizeof(float); break;
            case DataType.Double: this.cbDataType = sizeof(double); break;
            case DataType.String: {
                int cbInputA;
                switch (this.stringScanOption) {
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
            default: {
                Debug.Fail("Missing data type");
                return false;
            }
        }
        
        if (this.theProcessor.HasDoneFirstScan && (this.nextScanUsesFirstValue || this.nextScanUsesPreviousValue)) {
            return true;
        }

        if (string.IsNullOrEmpty(this.inputA)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", this.isSecondInputRequired ? "'From' input is empty" : "Input is empty");
            return false;
        }

        if (this.isSecondInputRequired && string.IsNullOrEmpty(this.inputB)) {
            await IMessageDialogService.Instance.ShowMessage("Input format", "'To' input is empty");
            return false;
        }

        if (this.dataType.IsNumeric()) {
            if (!TryParseNumeric(this.inputA, this.numberStyles, this.dataType, out this.numericInputA)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"{(this.isSecondInputRequired ? "'From' value" : "Input")} is invalid: {this.inputA}");
                return false;
            }

            if (this.isSecondInputRequired && !TryParseNumeric(this.inputB, this.numberStyles, this.dataType, out this.numericInputB)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"'To' value is invalid: {this.inputB}");
                return false;
            }
        }

        return true;
    }

    public async Task PerformFirstScan(IConsoleConnection connection) {
        int align = (int) this.alignment;
        IActivityProgress activity = ActivityManager.Instance.GetCurrentProgressOrEmpty();
        if (this.scanMemoryPages && connection is IHaveMemoryRegions regions) {
            uint addrStart = this.startAddress, addrEnd = addrStart + this.scanLength;
            List<MemoryRegion> safeRegions = new List<MemoryRegion>();
            List<MemoryRegion> consoleMemoryRegions = await regions.GetMemoryRegions();
            foreach (MemoryRegion region in consoleMemoryRegions) {
                if (region.Protection == 0x00000240) {
                    // It might not be specifically this protection value, but I noticed that around the memory regions
                    // with this region, if you attempt to read them, it freeze the console even after debug unfreeze command.
                    continue;
                }

                if (region.BaseAddress < addrStart || (region.BaseAddress + region.Size) > addrEnd) {
                    continue;
                }

                safeRegions.Add(region);
            }

            byte[] buffer = new byte[ChunkSize];
            for (int rgIdx = 0; rgIdx < safeRegions.Count; rgIdx++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                MemoryRegion region = safeRegions[rgIdx];

                // We still stick to the start/length fields even when scanning pages, because
                // the user may only want to scan a specific address region
                if (region.BaseAddress < addrStart || (region.BaseAddress + region.Size) > addrEnd) {
                    continue;
                }

                activity.IsIndeterminate = false;
                using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / region.Size);
                for (int j = 0; j < region.Size; j += ChunkSize) {
                    ActivityManager.Instance.CurrentTask.CheckCancelled();
                    activity.Text = $"Region {rgIdx + 1}/{safeRegions.Count} ({ValueScannerUtils.ByteFormatter.ToString(j, false)}/{ValueScannerUtils.ByteFormatter.ToString(region.Size, false)})";
                    activity.CompletionState.OnProgress(ChunkSize);

                    // should we be using BaseAddress or PhysicalAddress???
                    uint baseAddress = (uint) (region.BaseAddress + j);
                    int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(ChunkSize, (uint) Math.Max((int) region.Size - j, 0)));
                    if (cbRead > 0) {
                        this.ProcessMemoryBlockForFirstScan(Math.Max(cbRead - this.cbDataType, 0), align, buffer, baseAddress);
                    }
                }
            }
        }
        else {
            uint addr = this.startAddress, scanLen = this.scanLength, range = scanLen;
            int totalChunks = (int) (range / ChunkSize);
            byte[] buffer = new byte[ChunkSize];
            using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / scanLen);
            for (int j = 0, c = 0; j < scanLen; j += ChunkSize, c++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.Text = $"Chunk {c + 1}/{totalChunks} ({ValueScannerUtils.ByteFormatter.ToString(j, false)}/{ValueScannerUtils.ByteFormatter.ToString(scanLen, false)})";
                activity.CompletionState.OnProgress(ChunkSize);

                uint baseAddress = (uint) (addr + j);
                int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(ChunkSize, (uint) Math.Max((int) scanLen - j, 0)));
                if (cbRead > 0) {
                    this.ProcessMemoryBlockForFirstScan(Math.Max(cbRead - this.cbDataType, 0), align, buffer, baseAddress);
                }
            }
        }
    }

    private void ProcessMemoryBlockForFirstScan(int blockEnd, int align, byte[] buffer, uint baseAddress) {
        if (this.dataType.IsNumeric()) {
            for (int i = 0; i < blockEnd; i += align) {
                if (i >= buffer.Length || (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                object? matchBoxed;
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, i, this.cbDataType);
                switch (this.dataType) {
                    case DataType.Byte:   matchBoxed = this.CompareInt<byte>(span); break;
                    case DataType.Int16:  matchBoxed = this.CompareInt<short>(span); break;
                    case DataType.Int32:  matchBoxed = this.CompareInt<int>(span); break;
                    case DataType.Int64:  matchBoxed = this.CompareInt<long>(span); break;
                    case DataType.Float:  matchBoxed = this.CompareFloat<float>(span); break;
                    case DataType.Double: matchBoxed = this.CompareFloat<double>(span); break;
                    default:              throw new ArgumentOutOfRangeException();
                }

                if (matchBoxed != null) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, baseAddress + (uint) i, this.dataType, NumericDisplayType.Normal, NumericDisplayType.Normal.AsString(this.dataType, matchBoxed)));
                }
            }
        }
        else if (this.dataType == DataType.String) {
            for (int i = 0; i < blockEnd; i += align) {
                if (i >= buffer.Length || (buffer.Length - i) < this.cbDataType) {
                    break;
                }

                string readText;
                switch (this.stringScanOption) {
                    case StringType.ASCII: {
                        readText = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(buffer, i, this.cbDataType));
                        break;
                    }
                    case StringType.UTF8: {
                        readText = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, i, this.cbDataType));
                        break;
                    }
                    case StringType.UTF16: {
                        readText = Encoding.Unicode.GetString(new ReadOnlySpan<byte>(buffer, i, this.cbDataType));
                        break;
                    }
                    case StringType.UTF32: {
                        readText = Encoding.UTF32.GetString(new ReadOnlySpan<byte>(buffer, i, this.cbDataType));
                        break;
                    }
                    default: throw new ArgumentOutOfRangeException();
                }

                // TODO: string comparison customisation
                if (readText.Equals(this.inputA)) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, baseAddress + (uint) i, this.dataType, NumericDisplayType.Normal, readText));
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
        IActivityProgress activity = ActivityManager.Instance.GetCurrentProgressOrEmpty();
        if (this.dataType.IsNumeric()) {
            using (activity.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                for (int i = 0; i < srcList.Count; i++) {
                    ActivityManager.Instance.CurrentTask.CheckCancelled();
                    activity.Text = $"Reading values {i + 1}/{srcList.Count}";
                    activity.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    
                    long searchA, searchB;
                    if (this.nextScanUsesFirstValue) {
                        searchB = 0;
                        if (!TryParseNumeric(res.FirstValue, res, out searchA)) {
                            throw new Exception("Failed to reparse first value");
                        }
                    }
                    else if (this.nextScanUsesPreviousValue) {
                        searchB = 0;
                        if (!TryParseNumeric(res.PreviousValue, res, out searchA)) {
                            throw new Exception("Failed to reparse previous value");
                        }
                    }
                    else {
                        searchA = this.numericInputA;
                        searchB = this.numericInputB;
                    }
                    
                    res.PreviousValue = res.CurrentValue;

                    byte[] buffer = new byte[this.cbDataType];
                    await connection.ReadBytes(res.Address, buffer, 0, (uint) buffer.Length);

                    object? matchBoxed;
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
                        res.CurrentValue = res.NumericDisplayType.AsString(res.DataType, matchBoxed);
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else if (this.dataType == DataType.String) {
            using (activity.CompletionState.PushCompletionRange(0.0, 1.0 / srcList.Count)) {
                for (int i = 0; i < srcList.Count; i++) {
                    ActivityManager.Instance.CurrentTask.CheckCancelled();
                    activity.Text = $"Reading values {i + 1}/{srcList.Count}";
                    activity.CompletionState.OnProgress(1.0);

                    ScanResultViewModel res = srcList[i];
                    string search;
                    if (this.nextScanUsesFirstValue)
                        search = res.FirstValue;
                    else if (this.nextScanUsesPreviousValue)
                        search = res.PreviousValue;
                    else
                        search = this.inputA;

                    res.PreviousValue = res.CurrentValue;
                    res.CurrentValue = await MemoryEngine360.ReadAsText(connection, res.Address, res.DataType, res.NumericDisplayType, (uint) res.FirstValue.Length);
                    if (res.CurrentValue.Equals(search)) {
                        this.ResultFound?.Invoke(this, res);
                    }
                }
            }
        }
        else {
            Debug.Fail("Missing data type");
        }
    }

    private object? CompareInt<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IBinaryInteger<T> {
        return this.CompareInt<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }
    
    private object? CompareInt<T>(ReadOnlySpan<byte> searchValueBytes, long theInputA, long theInputB) where T : unmanaged, IBinaryInteger<T> {
        T value = ValueScannerUtils.CreateNumberFromBytes<T>(searchValueBytes);
        T valA = Unsafe.As<long, T>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? value : null;
            case NumericScanType.NotEquals:           return value != valA ? value : null;
            case NumericScanType.LessThan:            return value < valA ? value : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? value : null;
            case NumericScanType.GreaterThan:         return value > valA ? value : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? value : null;
            case NumericScanType.Between: {
                T valB = Unsafe.As<long, T>(ref theInputB);
                return value >= valA && value <= valB ? value : null;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private object? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IFloatingPoint<T> {
        return this.CompareFloat<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }
    
    private object? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes, long theInputA, long theInputB) where T : unmanaged, IFloatingPoint<T> {
        T value = ValueScannerUtils.CreateNumberFromBytes<T>(searchValueBytes);
        switch (this.floatScanOption) {
            case FloatScanOption.UseExactValue: break;
            case FloatScanOption.TruncateToQuery:
            case FloatScanOption.RoundToQuery: {
                if (this.numericScanType == NumericScanType.Between) {
                    break;
                }

                int idx = this.inputA.IndexOf('.');
                if (idx != -1) {
                    int decimals = this.inputA.Length - idx + 1;
                    if (typeof(T) == typeof(float)) {
                        float value_f = Unsafe.As<T, float>(ref value);
                        value_f = this.floatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateFloat(value_f, decimals) : (float) Math.Round(value_f, decimals);
                        value = Unsafe.As<float, T>(ref value_f);
                    }
                    else {
                        double value_d = Unsafe.As<T, double>(ref value);
                        value_d = this.floatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateDouble(value_d, decimals) : Math.Round(value_d, decimals);
                        value = Unsafe.As<double, T>(ref value_d);
                    }
                }

                break;
            }
        }

        T valA = Unsafe.As<long, T>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? value : null;
            case NumericScanType.NotEquals:           return value != valA ? value : null;
            case NumericScanType.LessThan:            return value < valA ? value : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? value : null;
            case NumericScanType.GreaterThan:         return value > valA ? value : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? value : null;
            case NumericScanType.Between: {
                T valB = Unsafe.As<long, T>(ref theInputB);
                return value >= valA && value <= valB ? value : null;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }
    
    [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
    private static bool TryParseNumeric(string text, NumberStyles style, DataType type, out long value) {
        bool result;
        value = 0;
        switch (type) {
            case DataType.Byte: {
                if (result = byte.TryParse(text, style, null, out byte val))
                    value = val;
                break;
            }
            case DataType.Int16: {
                if (result = short.TryParse(text, style, null, out short val))
                    value = val;
                break;
            }
            case DataType.Int32: {
                if (result = int.TryParse(text, style, null, out int val))
                    value = val;
                break;
            }
            case DataType.Int64: {
                if (result = long.TryParse(text, style, null, out long val))
                    value = val;
                break;
            }
            case DataType.Float: {
                if (result = float.TryParse(text, style, null, out float val))
                    value = Unsafe.As<float, long>(ref val);
                break;
            }
            case DataType.Double: {
                if (result = double.TryParse(text, style, null, out double val))
                    value = Unsafe.As<double, long>(ref val);
                break;
            }
            case DataType.String: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            default:
                value = 0;
                Debug.Assert(false, "Missing data type");
                return false;
        }

        return result;
    }
    
    [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
    private static bool TryParseNumeric(string text, ScanResultViewModel scan, out long value) {
        bool result;
        value = 0;
        NumberStyles intNumStyle = scan.NumericDisplayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        switch (scan.DataType) {
            case DataType.Byte: {
                if (result = byte.TryParse(text, intNumStyle, null, out byte val))
                    value = val;
                break;
            }
            case DataType.Int16: {
                if (result = short.TryParse(text, intNumStyle, null, out short val))
                    value = val;
                break;
            }
            case DataType.Int32: {
                if (result = int.TryParse(text, intNumStyle, null, out int val))
                    value = val;
                break;
            }
            case DataType.Int64: {
                if (result = long.TryParse(text, intNumStyle, null, out long val))
                    value = val;
                break;
            }
            case DataType.Float: {
                if (scan.NumericDisplayType == NumericDisplayType.Hexadecimal) {
                    if (result = float.TryParse(text, out float val))
                        value = Unsafe.As<float, long>(ref val);
                }
                else {
                    if (result = uint.TryParse(text, NumberStyles.HexNumber, null, out uint val))
                        value = val;
                }

                break;
            }
            case DataType.Double: {
                if (scan.NumericDisplayType == NumericDisplayType.Hexadecimal) {
                    if (result = float.TryParse(text, out float val))
                        value = Unsafe.As<float, long>(ref val);
                }
                else {
                    if (result = ulong.TryParse(text, NumberStyles.HexNumber, null, out ulong val))
                        value = (long) val;
                }
                break;
            }
            case DataType.String: throw new ArgumentOutOfRangeException();
            default:
                value = 0;
                Debug.Assert(false, "Missing data type");
                return false;
        }

        return result;
    }
}