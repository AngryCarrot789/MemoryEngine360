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

/// <summary>
/// A class used to perform scanning operations. This class takes a snapshot of the options in <see cref="ScanningProcessor"/>
/// </summary>
public sealed class ScanningContext {
    private const uint ChunkSize = 0x10000; // 65536
    private readonly ScanningProcessor theProcessor;
    private readonly string inputA, inputB;
    private readonly uint startAddress, scanLength, scanEndAddress;
    private readonly uint alignment;
    private readonly bool scanMemoryPages, isIntInputHexadecimal;
    private readonly bool nextScanUsesFirstValue, nextScanUsesPreviousValue;
    private readonly FloatScanOption floatScanOption;
    private readonly StringType stringScanOption;
    private readonly DataType dataType;
    private readonly NumericScanType numericScanType;
    private readonly StringComparison stringComparison;
    private readonly bool isSecondInputRequired;

    // enough bytes to store all data types except string
    private ulong numericInputA, numericInputB;

    // number of bytes the data type takes up. for strings, calculates based on StringType and char count
    private int cbDataType;

    /// <summary>
    /// Fired when a result is found. When scanning for the next value, it fires with a pre-existing result
    /// </summary>
    public event ScanningContextResultEventHandler ResultFound;

    public ScanningContext(ScanningProcessor processor) {
        this.theProcessor = processor;
        this.inputA = processor.InputA;
        this.inputB = processor.InputB;
        this.startAddress = processor.StartAddress;
        this.scanLength = processor.ScanLength;
        this.scanEndAddress = this.startAddress + this.scanLength;
        this.alignment = processor.Alignment;
        this.scanMemoryPages = processor.ScanMemoryPages;
        this.isIntInputHexadecimal = processor.IsIntInputHexadecimal;
        this.nextScanUsesFirstValue = processor.UseFirstValueForNextScan;
        this.nextScanUsesPreviousValue = processor.UsePreviousValueForNextScan;
        this.floatScanOption = processor.FloatScanOption;
        this.stringScanOption = processor.StringScanOption;
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
            NumericDisplayType ndt = this.dataType.IsInteger() && this.isIntInputHexadecimal ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
            if (!TryParseNumeric(this.inputA, this.dataType, ndt, out this.numericInputA)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"{(this.isSecondInputRequired ? "'From' value" : "Input")} is invalid: {this.inputA}");
                return false;
            }

            if (this.isSecondInputRequired && !TryParseNumeric(this.inputB, this.dataType, ndt, out this.numericInputB)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", $"'To' value is invalid: {this.inputB}");
                return false;
            }
        }

        return true;
    }

    public async Task PerformFirstScan(IConsoleConnection connection) {
        uint align = this.alignment;
        ActivityTask task = ActivityManager.Instance.CurrentTask;
        if (this.scanMemoryPages && connection is IHaveMemoryRegions iHaveRegions) {
            List<MemoryRegion> allRegions = await iHaveRegions.GetMemoryRegions(true, false);
            List<MemoryRegion> regions = new List<MemoryRegion>();
            foreach (MemoryRegion region in allRegions) {
                if (this.scanEndAddress >= region.BaseAddress && this.startAddress < (region.BaseAddress + region.Size)) {
                    regions.Add(region);
                }
            }

            byte[] buffer = new byte[ChunkSize];
            for (int rgIdx = 0; rgIdx < regions.Count; rgIdx++) {
                task.CheckCancelled();
                MemoryRegion region = regions[rgIdx];

                task.Progress.IsIndeterminate = false;

                uint realStart = Math.Max(region.BaseAddress, this.startAddress);
                uint realEnd = Math.Min(region.EndAddress, this.scanEndAddress);
                uint offset = realStart - region.BaseAddress;
                uint count = realEnd - realStart;
                
                // The progress bar should show the true progress of the chunk scanning, so we set the
                // completion range as the actual range we're going to be reading.
                // The text will still show the absolute ranges though, which is fine
                using PopCompletionStateRangeToken token = task.Progress.CompletionState.PushCompletionRange(0, 1.0 / (count - offset));
                for (; offset < count; offset += ChunkSize) {
                    task.CheckCancelled();
                    task.Progress.Text = $"Region {rgIdx + 1}/{regions.Count} ({ValueScannerUtils.ByteFormatter.ToString(offset, false)}/{ValueScannerUtils.ByteFormatter.ToString(region.Size, false)})";
                    task.Progress.CompletionState.OnProgress(ChunkSize);

                    uint baseAddress = region.BaseAddress + offset;
                    uint cbTargetRead = Math.Min(ChunkSize, count - offset /* remaining */);
                    uint cbActualRead = await connection.ReadBytes(baseAddress, buffer, 0, cbTargetRead).ConfigureAwait(false);
                    if (cbActualRead > 0) {
                        this.ProcessMemoryBlockForFirstScan(baseAddress, buffer, cbActualRead, align);
                    }
                }
            }
        }
        else {
            uint addr = this.startAddress, scanLen = this.scanLength, range = scanLen;
            uint totalChunks = range / ChunkSize;
            byte[] buffer = new byte[ChunkSize];
            using PopCompletionStateRangeToken token = task.Progress.CompletionState.PushCompletionRange(0, 1.0 / scanLen);
            for (uint offset = 0, c = 0; offset < scanLen; offset += ChunkSize, c++) {
                task.CheckCancelled();
                task.Progress.Text = $"Chunk {c + 1}/{totalChunks} ({ValueScannerUtils.ByteFormatter.ToString(offset, false)}/{ValueScannerUtils.ByteFormatter.ToString(scanLen, false)})";
                task.Progress.CompletionState.OnProgress(ChunkSize);

                uint baseAddress = addr + offset;
                uint cbTargetRead = Math.Min(ChunkSize, Math.Max(scanLen - offset, 0));
                uint cbActualRead = await connection.ReadBytes(baseAddress, buffer, 0, cbTargetRead).ConfigureAwait(false);
                if (cbActualRead > 0) {
                    this.ProcessMemoryBlockForFirstScan(baseAddress, buffer, cbActualRead, align);
                }
            }
        }
    }

    private void ProcessMemoryBlockForFirstScan(uint baseAddress, byte[] buffer, uint count, uint align) {
        int cbData = this.cbDataType;
        bool checkBounds = align < cbData;
        if (this.dataType.IsNumeric()) {
            for (uint i = 0; i < count; i += align) {
                if (checkBounds && (buffer.Length - i) < cbData) {
                    break;
                }

                object? matchBoxed;
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, (int) i, cbData);
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
                    NumericDisplayType ndt = this.isIntInputHexadecimal && this.dataType.IsInteger() ? NumericDisplayType.Hexadecimal : NumericDisplayType.Normal;
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, baseAddress + i, this.dataType, ndt, ndt.AsString(this.dataType, matchBoxed)));
                }
            }
        }
        else if (this.dataType == DataType.String) {
            for (uint i = 0; i < count; i += align) {
                if (checkBounds && (buffer.Length - i) < cbData) {
                    break;
                }

                string readText;
                switch (this.stringScanOption) {
                    case StringType.ASCII: {
                        readText = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(buffer, (int) i, cbData));
                        break;
                    }
                    case StringType.UTF8: {
                        readText = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, (int) i, cbData));
                        break;
                    }
                    case StringType.UTF16: {
                        readText = Encoding.Unicode.GetString(new ReadOnlySpan<byte>(buffer, (int) i, cbData));
                        break;
                    }
                    case StringType.UTF32: {
                        readText = Encoding.UTF32.GetString(new ReadOnlySpan<byte>(buffer, (int) i, cbData));
                        break;
                    }
                    default: throw new ArgumentOutOfRangeException();
                }

                if (readText.Equals(this.inputA, this.stringComparison)) {
                    this.ResultFound?.Invoke(this, new ScanResultViewModel(this.theProcessor, baseAddress + i, this.dataType, NumericDisplayType.Normal, readText));
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

                    ulong searchA, searchB;
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

                    uint cbRead = await connection.ReadBytes(res.Address, buffer, 0, (uint) buffer.Length);
                    for (uint j = cbRead; j < this.cbDataType; j++) {
                        buffer[j] = 0;
                    }

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
                        res.PreviousValue = res.CurrentValue;
                        res.CurrentValue = res.NumericDisplayType.AsString(this.dataType, matchBoxed);
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
                        search = res.FirstValue;
                    else if (this.nextScanUsesPreviousValue)
                        search = res.PreviousValue;
                    else
                        search = this.inputA;

                    string readText = await connection.ReadString(res.Address, (uint) res.FirstValue.Length);
                    if (readText.Equals(search, this.stringComparison)) {
                        res.PreviousValue = res.CurrentValue;
                        res.CurrentValue = readText;
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

    private object? CompareInt<T>(ReadOnlySpan<byte> searchValueBytes, ulong theInputA, ulong theInputB) where T : unmanaged, IBinaryInteger<T> {
        T value = ValueScannerUtils.CreateNumberFromBytes<T>(searchValueBytes);
        T valA = Unsafe.As<ulong, T>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? value : null;
            case NumericScanType.NotEquals:           return value != valA ? value : null;
            case NumericScanType.LessThan:            return value < valA ? value : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? value : null;
            case NumericScanType.GreaterThan:         return value > valA ? value : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? value : null;
            case NumericScanType.Between: {
                T valB = Unsafe.As<ulong, T>(ref theInputB);
                return value >= valA && value <= valB ? value : null;
            }
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private object? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes) where T : unmanaged, IFloatingPoint<T> {
        return this.CompareFloat<T>(searchValueBytes, this.numericInputA, this.numericInputB);
    }

    private object? CompareFloat<T>(ReadOnlySpan<byte> searchValueBytes, ulong theInputA, ulong theInputB) where T : unmanaged, IFloatingPoint<T> {
        int idx;
        T value = ValueScannerUtils.CreateFloat<T>(searchValueBytes);
        if (this.numericScanType != NumericScanType.Between && this.floatScanOption != FloatScanOption.UseExactValue && (idx = this.inputA.IndexOf('.')) != -1) {
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

        T valA = Unsafe.As<ulong, T>(ref theInputA);
        switch (this.numericScanType) {
            case NumericScanType.Equals:              return value == valA ? value : null;
            case NumericScanType.NotEquals:           return value != valA ? value : null;
            case NumericScanType.LessThan:            return value < valA ? value : null;
            case NumericScanType.LessThanOrEquals:    return value <= valA ? value : null;
            case NumericScanType.GreaterThan:         return value > valA ? value : null;
            case NumericScanType.GreaterThanOrEquals: return value >= valA ? value : null;
            case NumericScanType.Between: {
                T valB = Unsafe.As<ulong, T>(ref theInputB);
                return value >= valA && value <= valB ? value : null;
            }
        }

        Debug.Fail("Unexpected exit");
        return null;
    }

    private static bool TryParseNumeric(string text, ScanResultViewModel scan, out ulong value) {
        return TryParseNumeric(text, scan.DataType, scan.NumericDisplayType, out value);
    }

    [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
    private static bool TryParseNumeric(string text, DataType dataType, NumericDisplayType ndt, out ulong value) {
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
                else if (result = float.TryParse(text, out float val)) {
                    value = Unsafe.As<float, uint>(ref val);
                }

                break;
            }
            case DataType.Double: {
                if (ndt == NumericDisplayType.Hexadecimal) {
                    if (result = ulong.TryParse(text, NumberStyles.HexNumber, null, out ulong val)) {
                        value = val;
                    }
                }
                else if (result = double.TryParse(text, out double val)) {
                    value = Unsafe.As<double, ulong>(ref val);
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