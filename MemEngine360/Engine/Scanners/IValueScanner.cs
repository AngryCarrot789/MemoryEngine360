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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Impl;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Interactivity.Formatting;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.Scanners;

public interface IValueScanner {
    // This is only really supposed to be used for the number dragger but we can get away with it ;)
    internal static readonly AutoMemoryValueFormatter ByteFormatter = new AutoMemoryValueFormatter() {
        SourceFormat = MemoryFormatType.Byte,
        NonEditingRoundedPlaces = 1,
        AllowedFormats = [MemoryFormatType.Byte, MemoryFormatType.KiloByte1000, MemoryFormatType.MegaByte1000, MemoryFormatType.GigaByte1000, MemoryFormatType.TeraByte1000]
    };

    public static IValueScanner ByteScanner { get; } = new ByteValueScanner();
    public static IValueScanner Int16Scanner { get; } = new Int16ValueScanner();
    public static IValueScanner Int32Scanner { get; } = new Int32ValueScanner();
    public static IValueScanner Int64Scanner { get; } = new Int64ValueScanner();
    public static IValueScanner FloatScanner { get; } = new FloatValueScanner();
    public static IValueScanner DoubleScanner { get; } = new DoubleValueScanner();
    public static IValueScanner StringScanner { get; } = new StringValueScanner();

    public static IReadOnlyDictionary<DataType, IValueScanner> Scanners { get; } = new Dictionary<DataType, IValueScanner>() {
        { DataType.Byte, ByteScanner },
        { DataType.Int16, Int16Scanner },
        { DataType.Int32, Int32Scanner },
        { DataType.Int64, Int64Scanner },
        { DataType.Float, FloatScanner },
        { DataType.Double, DoubleScanner },
        { DataType.String, StringScanner },
    }.AsReadOnly();

    /// <summary>
    /// Scans for an input value
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="results"></param>
    /// <param name="activity"></param>
    /// <returns></returns>
    Task<bool> PerformFirstScan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity);

    /// <summary>
    /// Scans for an input in our current results
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="results"></param>
    /// <param name="activity"></param>
    /// <returns></returns>
    Task<bool> PerformNextScan(ScanningProcessor processor, List<ScanResultViewModel> srcList, ObservableList<ScanResultViewModel> dstList, IActivityProgress activity);
}

public abstract class BaseNumericValueScanner<T> : IValueScanner where T : unmanaged, INumber<T> {
    private static async Task<(T inputA, T inputB, bool performFirstScan)> GetInputs(ScanningProcessor processor) {
        T inputA, inputB = default;
        NumberStyles numberStyles;
        if (processor.DataType.IsInteger()) {
            numberStyles = processor.IsIntInputHexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        }
        else if (processor.DataType.IsFloat()) {
            numberStyles = NumberStyles.Float | NumberStyles.AllowThousands;
        }
        else {
            throw new Exception("Invalid processor data type for this scanner");
        }

        if (!T.TryParse(processor.InputA, numberStyles, null, out inputA)) {
            await IMessageDialogService.Instance.ShowMessage("Invalid input", "Input is not valid for this search type: " + processor.InputA);
            return (inputA, inputB, false);
        }

        if (processor.NumericScanType == NumericScanType.Between) {
            if (!T.TryParse(processor.InputB, numberStyles, null, out inputB)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", "Second input is not valid for this search type: " + processor.InputB);
                return (inputA, inputB, false);
            }
        }

        return (inputA, inputB, true);
    }

    public async Task<bool> PerformFirstScan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        (T inputA, T inputB, bool performFirstScan) = await GetInputs(processor);
        if (!performFirstScan) {
            return false;
        }

        IConsoleConnection connection = processor.MemoryEngine360.Connection!;
        if (processor.ScanMemoryPages) {
            uint addrStart = processor.StartAddress, addrEnd = addrStart + processor.ScanLength;
            List<MemoryRegion> safeRegions = new List<MemoryRegion>();
            List<MemoryRegion> consoleMemoryRegions = await connection.GetMemoryRegions();
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
            
            byte[] buffer = new byte[65536];
            for (int rgIdx = 0; rgIdx < safeRegions.Count; rgIdx++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                MemoryRegion region = safeRegions[rgIdx];

                // We still stick to the start/length fields even when scanning pages, because
                // the user may only want to scan a specific address region
                if (region.BaseAddress < addrStart || (region.BaseAddress + region.Size) > addrEnd) {
                    continue;
                }

                int align = (int) processor.Alignment;
                int sizeOfT = Unsafe.SizeOf<T>();
                activity.IsIndeterminate = false;
                using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / region.Size);
                for (int j = 0; j < region.Size; j += 65536) {
                    ActivityManager.Instance.CurrentTask.CheckCancelled();
                    activity.Text = $"Region {rgIdx + 1}/{safeRegions.Count} ({IValueScanner.ByteFormatter.ToString(j, false)}/{IValueScanner.ByteFormatter.ToString(region.Size, false)})";
                    activity.CompletionState.OnProgress(65536);

                    // should we be using BaseAddress or PhysicalAddress???
                    uint baseAddress = (uint) (region.BaseAddress + j);
                    int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(65536, (uint) Math.Max((int) region.Size - j, 0)));
                    if (cbRead != 65536 && cbRead != Math.Min(65536, (uint) Math.Max((int) region.Size - j, 0)))
                        Debugger.Break();

                    this.ProcessMemoryBlockForFirstScan(inputA, inputB, processor, results, Math.Max(cbRead - sizeOfT, 0), align, buffer, sizeOfT, baseAddress);
                }
            }
        }
        else {
            uint addr = processor.StartAddress, scanLen = processor.ScanLength, range = scanLen;
            int align = (int) processor.Alignment;
            int totalChunks = (int) (range / 65536);
            byte[] buffer = new byte[65536];
            int sizeOfT = Unsafe.SizeOf<T>();
            using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / scanLen);
            for (int j = 0, c = 0; j < scanLen; j += 65536, c++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.Text = $"Chunk {c + 1}/{totalChunks} ({IValueScanner.ByteFormatter.ToString(j, false)}/{IValueScanner.ByteFormatter.ToString(scanLen, false)})";
                activity.CompletionState.OnProgress(65536);
                
                uint baseAddress = (uint) (addr + j);
                int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(65536, (uint) Math.Max((int) scanLen - j, 0)));
                if (cbRead != 65536 && cbRead != Math.Min(65536, (uint) Math.Max((int) scanLen - j, 0)))
                    Debugger.Break();

                this.ProcessMemoryBlockForFirstScan(inputA, inputB, processor, results, Math.Max(cbRead - sizeOfT, 0), align, buffer, sizeOfT, baseAddress);
            }
        }

        return true;
    }

    public async Task<bool> PerformNextScan(ScanningProcessor processor, List<ScanResultViewModel> srcList, ObservableList<ScanResultViewModel> dstList, IActivityProgress activity) {
        if (srcList[0].DataType != processor.DataType) {
            await IMessageDialogService.Instance.ShowMessage("Data Type Changed", "The results contains a different data type from the scan data type. Please change the scan type to " + srcList[0].DataType);
            return false;
        }

        (T inputA, T inputB, bool performFirstScan) = await GetInputs(processor);
        if (!performFirstScan) {
            return false;
        }

        IConsoleConnection connection = processor.MemoryEngine360.Connection!;
        using (activity.CompletionState.PushCompletionRange(0.0, 256.0 / srcList.Count)) {
            for (int i = 0; i < srcList.Count; i++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.Text = $"Reading values {i + 1}/{srcList.Count}";
                activity.CompletionState.OnProgress(1.0);

                ScanResultViewModel res = srcList[i];
                res.PreviousValue = res.CurrentValue;
                res.CurrentValue = await MemoryEngine360.ReadAsText(connection, res.Address, res.DataType, res.NumericDisplayType, (uint) res.FirstValue.Length);
            }
        }

        using (activity.CompletionState.PushCompletionRange(0.0, 256.0 / srcList.Count)) {
            activity.Text = $"Processing results...";
            for (int i = 0; i < srcList.Count; i++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.CompletionState.OnProgress(1.0);
                if (this.CanKeepResultForNextScan(srcList[i], inputA, inputB)) {
                    dstList.Add(srcList[i]);
                }
            }
        }

        return true;
    }

    protected abstract void ProcessMemoryBlockForFirstScan(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, int blockEnd, int align, byte[] buffer, int sizeOfT, uint baseAddress);

    protected abstract bool CanKeepResultForNextScan(ScanResultViewModel result, T inputA, T inputB);
}

public class BaseIntegerValueScanner<T> : BaseNumericValueScanner<T> where T : unmanaged, IBinaryInteger<T> {
    protected override void ProcessMemoryBlockForFirstScan(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, int blockEnd, int align, byte[] buffer, int sizeOfT, uint baseAddress) {
        for (int i = 0; i < blockEnd; i += align) {
            if (i >= buffer.Length || (buffer.Length - i) < sizeOfT) {
                break;
            }

            T value = ValueScannerUtils.CreateNumberFromBytes<T>(new ReadOnlySpan<byte>(buffer, i, sizeOfT));
            bool matched;
            switch (processor.NumericScanType) {
                case NumericScanType.Equals:              matched = value == inputA; break;
                case NumericScanType.NotEquals:           matched = value != inputA; break;
                case NumericScanType.LessThan:            matched = value < inputA; break;
                case NumericScanType.LessThanOrEquals:    matched = value <= inputA; break;
                case NumericScanType.GreaterThan:         matched = value > inputA; break;
                case NumericScanType.GreaterThanOrEquals: matched = value >= inputA; break;
                case NumericScanType.Between:             matched = value >= inputA && value <= inputB; break;
                default:                                  throw new ArgumentOutOfRangeException();
            }

            if (matched) {
                results.Add(new ScanResultViewModel(processor, baseAddress + (uint) i, processor.DataType, NumericDisplayType.Normal, value.ToString() ?? ""));
            }
        }
    }

    protected override bool CanKeepResultForNextScan(ScanResultViewModel result, T inputA, T inputB) {
        NumberStyles style = result.NumericDisplayType == NumericDisplayType.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
        ulong parsedValue;
        switch (result.DataType) {
            case DataType.Byte:  parsedValue = byte.Parse(result.CurrentValue, style); break;
            case DataType.Int16: parsedValue = result.NumericDisplayType == NumericDisplayType.Unsigned ? ushort.Parse(result.CurrentValue, style) : (ulong) short.Parse(result.CurrentValue, style); break;
            case DataType.Int32: parsedValue = result.NumericDisplayType == NumericDisplayType.Unsigned ? uint.Parse(result.CurrentValue, style) : (ulong) int.Parse(result.CurrentValue, style); break;
            case DataType.Int64: parsedValue = result.NumericDisplayType == NumericDisplayType.Unsigned ? ulong.Parse(result.CurrentValue, style) : (ulong) long.Parse(result.CurrentValue, style); break;
            default:             return false;
        }

        bool matched;
        T value = ValueScannerUtils.CreateNumberFromRawLong<T>(parsedValue);
        switch (result.ScanningProcessor.NumericScanType) {
            case NumericScanType.Equals:              matched = value == inputA; break;
            case NumericScanType.NotEquals:           matched = value != inputA; break;
            case NumericScanType.LessThan:            matched = value < inputA; break;
            case NumericScanType.LessThanOrEquals:    matched = value <= inputA; break;
            case NumericScanType.GreaterThan:         matched = value > inputA; break;
            case NumericScanType.GreaterThanOrEquals: matched = value >= inputA; break;
            case NumericScanType.Between:             matched = value >= inputA && value <= inputB; break;
            default:                                  throw new ArgumentOutOfRangeException();
        }

        return matched;
    }
}

public class BaseFloatValueScanner<T> : BaseNumericValueScanner<T> where T : unmanaged, IFloatingPoint<T> {
    protected override void ProcessMemoryBlockForFirstScan(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, int blockEnd, int align, byte[] buffer, int sizeOfT, uint baseAddress) {
        for (int i = 0; i < blockEnd; i += align) {
            if (i >= buffer.Length || (buffer.Length - i) < sizeOfT) {
                break;
            }

            T value = ValueScannerUtils.CreateNumberFromBytes<T>(new ReadOnlySpan<byte>(buffer, i, sizeOfT));
            switch (processor.FloatScanOption) {
                case FloatScanOption.UseExactValue: break;
                case FloatScanOption.TruncateToQuery:
                case FloatScanOption.RoundToQuery: {
                    if (processor.NumericScanType == NumericScanType.Between) {
                        break;
                    }

                    int idx = processor.InputA?.IndexOf('.') ?? -1;
                    if (idx != -1) {
                        int decimals = processor.InputA!.Length - idx + 1;
                        if (typeof(T) == typeof(float)) {
                            float value_f = Unsafe.As<T, float>(ref value);
                            value_f = processor.FloatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateFloat(value_f, decimals) : (float) Math.Round(value_f, decimals);
                            value = Unsafe.As<float, T>(ref value_f);
                        }
                        else {
                            double value_d = Unsafe.As<T, double>(ref value);
                            value_d = processor.FloatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateDouble(value_d, decimals) : Math.Round(value_d, decimals);
                            value = Unsafe.As<double, T>(ref value_d);
                        }
                    }

                    break;
                }
            }

            bool matched;
            switch (processor.NumericScanType) {
                case NumericScanType.Equals:              matched = value == inputA; break;
                case NumericScanType.NotEquals:           matched = value != inputA; break;
                case NumericScanType.LessThan:            matched = value < inputA; break;
                case NumericScanType.LessThanOrEquals:    matched = value <= inputA; break;
                case NumericScanType.GreaterThan:         matched = value > inputA; break;
                case NumericScanType.GreaterThanOrEquals: matched = value >= inputA; break;
                case NumericScanType.Between:             matched = value >= inputA && value <= inputB; break;
                default:                                  throw new ArgumentOutOfRangeException();
            }

            if (matched) {
                results.Add(new ScanResultViewModel(processor, baseAddress + (uint) i, processor.DataType, NumericDisplayType.Normal, value.ToString() ?? ""));
            }
        }
    }

    protected override bool CanKeepResultForNextScan(ScanResultViewModel result, T inputA, T inputB) {
        T value;
        switch (result.DataType) {
            case DataType.Float: {
                float f = result.NumericDisplayType == NumericDisplayType.Hexadecimal ? BitConverter.Int32BitsToSingle(int.Parse(result.CurrentValue, NumberStyles.HexNumber, null)) : float.Parse(result.CurrentValue);
                value = Unsafe.As<float, T>(ref f);
                break;
            }
            case DataType.Double: {
                double d = result.NumericDisplayType == NumericDisplayType.Hexadecimal ? BitConverter.Int64BitsToDouble(long.Parse(result.CurrentValue, NumberStyles.HexNumber, null)) : double.Parse(result.CurrentValue);
                value = Unsafe.As<double, T>(ref d);
                break;
            }
            default: return false;
        }

        switch (result.ScanningProcessor.FloatScanOption) {
            case FloatScanOption.UseExactValue: break;
            case FloatScanOption.TruncateToQuery:
            case FloatScanOption.RoundToQuery: {
                if (result.ScanningProcessor.NumericScanType == NumericScanType.Between) {
                    break;
                }

                int idx = result.ScanningProcessor.InputA?.IndexOf('.') ?? -1;
                if (idx != -1) {
                    int decimals = result.ScanningProcessor.InputA!.Length - idx + 1;
                    if (typeof(T) == typeof(float)) {
                        float value_f = Unsafe.As<T, float>(ref value);
                        value_f = result.ScanningProcessor.FloatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateFloat(value_f, decimals) : (float) Math.Round(value_f, decimals);
                        value = Unsafe.As<float, T>(ref value_f);
                    }
                    else {
                        double value_d = Unsafe.As<T, double>(ref value);
                        value_d = result.ScanningProcessor.FloatScanOption == FloatScanOption.TruncateToQuery ? ValueScannerUtils.TruncateDouble(value_d, decimals) : Math.Round(value_d, decimals);
                        value = Unsafe.As<double, T>(ref value_d);
                    }
                }

                break;
            }
        }

        bool matched;
        switch (result.ScanningProcessor.NumericScanType) {
            case NumericScanType.Equals:              matched = value == inputA; break;
            case NumericScanType.NotEquals:           matched = value != inputA; break;
            case NumericScanType.LessThan:            matched = value < inputA; break;
            case NumericScanType.LessThanOrEquals:    matched = value <= inputA; break;
            case NumericScanType.GreaterThan:         matched = value > inputA; break;
            case NumericScanType.GreaterThanOrEquals: matched = value >= inputA; break;
            case NumericScanType.Between:             matched = value >= inputA && value <= inputB; break;
            default:                                  throw new ArgumentOutOfRangeException();
        }

        return matched;
    }
}

public class ByteValueScanner : BaseIntegerValueScanner<byte>;

public class Int16ValueScanner : BaseIntegerValueScanner<short>;

public class Int32ValueScanner : BaseIntegerValueScanner<int>;

public class Int64ValueScanner : BaseIntegerValueScanner<long>;

public class FloatValueScanner : BaseFloatValueScanner<float>;

public class DoubleValueScanner : BaseFloatValueScanner<double>;

public class StringValueScanner : IValueScanner {
    public async Task<bool> PerformFirstScan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        int cbInputString;
        switch (processor.StringScanOption) {
            case StringType.ASCII:
            case StringType.UTF8:
                cbInputString = processor.InputA.Length;
            break;
            case StringType.UTF16: cbInputString = processor.InputA.Length * 2; break;
            case StringType.UTF32: cbInputString = processor.InputA.Length * 4; break;
            default:               throw new ArgumentOutOfRangeException();
        }

        if (cbInputString > 65536) {
            throw new Exception("Input string is too long. Console memory is read in chunks of 64KB, therefore, the string cannot contain more than that many bytes");
        }

        IConsoleConnection connection = processor.MemoryEngine360.Connection!;
        uint chunkSize = (uint) Maths.Ceil(65536, cbInputString);
        if (processor.ScanMemoryPages) {
            uint addrStart = processor.StartAddress, addrEnd = addrStart + processor.ScanLength;
            List<MemoryRegion> safeRegions = new List<MemoryRegion>();
            List<MemoryRegion> consoleMemoryRegions = await connection.GetMemoryRegions();
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
            
            byte[] buffer = new byte[chunkSize];
            for (int rgIdx = 0; rgIdx < safeRegions.Count; rgIdx++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                MemoryRegion region = safeRegions[rgIdx];
                if (region.Protection == 0x00000240) {
                    // It might not be specifically this protection value, but I noticed that around the memory regions
                    // with this region, if you attempt to read them, it freezes the console even after debug unfreeze command.
                    continue;
                }

                // We still stick to the start/length fields even when scanning pages, because
                // the user may only want to scan a specific address region
                if (region.BaseAddress < addrStart || (region.BaseAddress + region.Size) >= addrEnd) {
                    continue;
                }

                int align = (int) processor.Alignment;
                activity.IsIndeterminate = false;
                using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / region.Size);
                for (int j = 0; j < region.Size; j += (int) chunkSize) {
                    ActivityManager.Instance.CurrentTask.CheckCancelled();
                    activity.Text = $"Region {rgIdx + 1}/{safeRegions.Count}... ({IValueScanner.ByteFormatter.ToString(j, false)}/{IValueScanner.ByteFormatter.ToString(region.Size, false)})";
                    activity.CompletionState.OnProgress(chunkSize);

                    // should we be using BaseAddress or PhysicalAddress???
                    uint baseAddress = (uint) (region.BaseAddress + j);
                    int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(chunkSize, (uint) Math.Max((int) region.Size - j, 0)));
                    if (cbRead != chunkSize && cbRead != Math.Min(chunkSize, (uint) Math.Max((int) region.Size - j, 0)))
                        Debugger.Break();

                    ProcessStringBlock(processor, results, activity, Math.Max(cbRead - cbInputString, 0), align, buffer, cbInputString, baseAddress);
                }
            }
        }
        else {
            uint addr = processor.StartAddress, scanLen = processor.ScanLength, range = scanLen;
            int align = (int) processor.Alignment;
            int totalChunks = (int) (range / chunkSize);
            byte[] buffer = new byte[chunkSize];
            using var token = activity.CompletionState.PushCompletionRange(0, 1.0 / scanLen);
            for (int j = 0, c = 1; j < scanLen; j += (int) chunkSize, c++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.Text = $"Chunk {c + 1}/{totalChunks} ({IValueScanner.ByteFormatter.ToString(j, false)}/{IValueScanner.ByteFormatter.ToString(scanLen, false)})";
                activity.CompletionState.OnProgress(chunkSize);

                uint baseAddress = (uint) (addr + j);
                int cbRead = await connection.ReadBytes(baseAddress, buffer, 0, Math.Min(chunkSize, (uint) Math.Max((int) scanLen - j, 0)));
                if (cbRead != chunkSize && cbRead != Math.Min(chunkSize, (uint) Math.Max((int) scanLen - j, 0)))
                    Debugger.Break();

                int blockEnd = Math.Max(cbRead - cbInputString, 0);
                using var _ = activity.CompletionState.PushCompletionRange(0.0, 1 / (blockEnd / 256.0));
                ProcessStringBlock(processor, results, activity, blockEnd, align, buffer, cbInputString, baseAddress);
            }
        }

        return true;
    }

    private static void ProcessStringBlock(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity, int blockEnd, int align, byte[] buffer, int cbInputString, uint baseAddress) {
        for (int i = 0; i < blockEnd; i += align) {
            if (i >= buffer.Length || (buffer.Length - i) < cbInputString) {
                break;
            }

            if (i % 256 == 0) {
                activity.CompletionState.OnProgress(1);
            }

            string bufferString;
            switch (processor.StringScanOption) {
                case StringType.ASCII: {
                    bufferString = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(buffer, i, cbInputString));
                    break;
                }
                case StringType.UTF8: {
                    bufferString = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, i, cbInputString));
                    break;
                }
                case StringType.UTF16: {
                    bufferString = Encoding.Unicode.GetString(new ReadOnlySpan<byte>(buffer, i, cbInputString));
                    break;
                }
                case StringType.UTF32: {
                    bufferString = Encoding.UTF32.GetString(new ReadOnlySpan<byte>(buffer, i, cbInputString));
                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }

            if (bufferString.Equals(processor.InputA)) {
                results.Add(new ScanResultViewModel(processor, baseAddress + (uint) i, processor.DataType, NumericDisplayType.Normal, bufferString));
            }
        }
    }

    public async Task<bool> PerformNextScan(ScanningProcessor processor, List<ScanResultViewModel> srcList, ObservableList<ScanResultViewModel> dstList, IActivityProgress activity) {
        if (srcList[0].DataType != processor.DataType) {
            await IMessageDialogService.Instance.ShowMessage("Data Type Changed", "The results contains a different data type from the scan data type. Please change the scan type to " + srcList[0].DataType);
            return false;
        }

        IConsoleConnection connection = processor.MemoryEngine360.Connection!;
        using (activity.CompletionState.PushCompletionRange(0.0, 256.0 / srcList.Count)) {
            for (int i = 0; i < srcList.Count; i++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.Text = $"Reading values {i + 1}/{srcList.Count}";
                activity.CompletionState.OnProgress(1.0);

                ScanResultViewModel res = srcList[i];
                res.PreviousValue = res.CurrentValue;
                res.CurrentValue = await MemoryEngine360.ReadAsText(connection, res.Address, res.DataType, res.NumericDisplayType, (uint) res.FirstValue.Length);
            }
        }

        using (activity.CompletionState.PushCompletionRange(0.0, 256.0 / srcList.Count)) {
            activity.Text = $"Processing results...";
            for (int i = 0; i < srcList.Count; i++) {
                ActivityManager.Instance.CurrentTask.CheckCancelled();
                activity.CompletionState.OnProgress(1.0);
                if (srcList[i].CurrentValue.Equals(processor.InputA)) {
                    dstList.Add(srcList[i]);
                }
            }
        }

        return true;
    }
}

internal static class ValueScannerUtils {
    public static float TruncateFloat(float value, int decimals) {
        float factor = (float) Math.Pow(10, decimals);
        return (float) (Math.Truncate(value * factor) / factor);
    }

    public static double TruncateDouble(double value, int decimals) {
        double factor = Math.Pow(10, decimals);
        return Math.Truncate(value * factor) / factor;
    }

    public static T CreateNumberFromBytes<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        if (typeof(T) == typeof(sbyte))
            return CreateGeneric_sbyte<T>(bytes);
        if (typeof(T) == typeof(byte))
            return CreateGeneric_byte<T>(bytes);
        if (typeof(T) == typeof(short))
            return CreateGeneric_short<T>(bytes);
        if (typeof(T) == typeof(ushort))
            return CreateGeneric_ushort<T>(bytes);
        if (typeof(T) == typeof(int))
            return CreateGeneric_int<T>(bytes);
        if (typeof(T) == typeof(uint))
            return CreateGeneric_uint<T>(bytes);
        if (typeof(T) == typeof(long))
            return CreateGeneric_long<T>(bytes);
        if (typeof(T) == typeof(ulong))
            return CreateGeneric_ulong<T>(bytes);
        if (typeof(T) == typeof(float))
            return CreateGeneric_float<T>(bytes);
        if (typeof(T) == typeof(double))
            return CreateGeneric_double<T>(bytes);
        throw new NotSupportedException();
    }

    public static TOut CreateNumberFromRawLong<TOut>(ulong src) where TOut : INumber<TOut> {
        return Unsafe.As<ulong, TOut>(ref src);
    }

    private static T CreateGeneric_sbyte<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        sbyte value = (sbyte) bytes[0];
        return Unsafe.As<sbyte, T>(ref value);
    }

    private static T CreateGeneric_byte<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        byte value = bytes[0];
        return Unsafe.As<byte, T>(ref value);
    }

    private static T CreateGeneric_short<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        short value = BinaryPrimitives.ReadInt16BigEndian(bytes);
        return Unsafe.As<short, T>(ref value);
    }

    private static T CreateGeneric_ushort<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        return Unsafe.As<ushort, T>(ref value);
    }

    private static T CreateGeneric_int<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        int value = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return Unsafe.As<int, T>(ref value);
    }

    private static T CreateGeneric_uint<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return Unsafe.As<uint, T>(ref value);
    }

    private static T CreateGeneric_long<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        long value = BinaryPrimitives.ReadInt64BigEndian(bytes);
        return Unsafe.As<long, T>(ref value);
    }

    private static T CreateGeneric_ulong<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return Unsafe.As<ulong, T>(ref value);
    }

    private static T CreateGeneric_float<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        float value = BinaryPrimitives.ReadSingleBigEndian(bytes);
        return Unsafe.As<float, T>(ref value);
    }

    private static T CreateGeneric_double<T>(ReadOnlySpan<byte> bytes) where T : INumber<T> {
        double value = BinaryPrimitives.ReadDoubleBigEndian(bytes);
        return Unsafe.As<double, T>(ref value);
    }
}