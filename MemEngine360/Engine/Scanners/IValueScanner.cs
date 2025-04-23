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
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using MemEngine360.Engine.Modes;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Tasks;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Engine.Scanners;

public interface IValueScanner {
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
    Task<bool> Scan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity);
}

public abstract class BaseNumericValueScanner<T> : IValueScanner where T : unmanaged, INumber<T> {
    public async Task<bool> Scan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        T inputA, inputB;
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
            return false;
        }

        if (processor.NumericScanType == NumericScanType.Between) {
            if (!T.TryParse(processor.InputB, numberStyles, null, out inputB)) {
                await IMessageDialogService.Instance.ShowMessage("Invalid input", "Second input is not valid for this search type: " + processor.InputB);
                return false;
            }
        }
        else {
            inputB = default;
        }

        return await this.ScanCore(inputA, inputB, processor, results, activity);
    }

    /// <summary>
    /// Scans memory
    /// </summary>
    /// <param name="inputA">Primary input, or "From", when <see cref="ScanningProcessor.NumericScanType"/> is <see cref="NumericScanType.Between"/></param>
    /// <param name="inputB">"To" when <see cref="ScanningProcessor.NumericScanType"/> is <see cref="NumericScanType.Between"/>, otherwise default(T)</param>
    /// <param name="processor"></param>
    /// <param name="results"></param>
    /// <param name="activity"></param>
    /// <returns></returns>
    protected abstract Task<bool> ScanCore(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity);
}

public abstract class BaseIntegerValueScanner<T> : BaseNumericValueScanner<T> where T : unmanaged, IBinaryInteger<T> {
    protected override async Task<bool> ScanCore(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        await ValueScannerCore.ScanInteger(inputA, inputB, processor, results, activity);
        return true;
    }
}

public abstract class BaseFloatValueScanner<T> : BaseNumericValueScanner<T> where T : unmanaged, IFloatingPoint<T> {
    protected override async Task<bool> ScanCore(T inputA, T inputB, ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        await ValueScannerCore.ScanFloat(inputA, inputB, processor, results, activity);
        return true;
    }
}

public class ByteValueScanner : BaseIntegerValueScanner<byte>;
public class Int16ValueScanner : BaseIntegerValueScanner<short>;
public class Int32ValueScanner : BaseIntegerValueScanner<int>;
public class Int64ValueScanner : BaseIntegerValueScanner<long>;
public class FloatValueScanner : BaseFloatValueScanner<float>;
public class DoubleValueScanner : BaseFloatValueScanner<double>;

public class StringValueScanner : IValueScanner {
    public async Task<bool> Scan(ScanningProcessor processor, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        await ValueScannerCore.ScanString(processor.InputA, processor, results, activity);
        return true;
    }
}

public static class ValueScannerCore {
    public static async Task ScanInteger<T>(T inputA, T inputB, ScanningProcessor p, ObservableList<ScanResultViewModel> results, IActivityProgress activity) where T : IBinaryNumber<T> {
        DataType dt = p.DataType;
        uint addr = p.StartAddress, scanLen = p.ScanLength, range = scanLen;
        int totalChunks = (int) (range / 65535) + 1;
        for (int j = 0, c = 1; j < scanLen; j += 65535, c++) {
            ActivityManager.Instance.CurrentTask.CheckCancelled();
            activity.Text = $"Reading chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = true;
            byte[] bytes = await p.MemoryEngine360.Connection!.ReadBytes((uint) (addr + j), Math.Min(65535, (uint) Math.Max((int) scanLen - j, 0)));

            activity.Text = $"Scanning chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = false;

            int sizeOfT = Unsafe.SizeOf<T>();
            int blockEnd = 65535 - sizeOfT;
            using var _ = activity.CompletionState.PushCompletionRange(0.0, 256.0 / blockEnd);
            for (int i = 0; i < blockEnd; i++) {
                if (i >= bytes.Length || (bytes.Length - i) < sizeOfT) {
                    break;
                }

                if (i % 256 == 0) {
                    activity.CompletionState.OnProgress(1);
                }

                T value = CreateNumberFromBytes<T>(new ReadOnlySpan<byte>(bytes, i, sizeOfT));
                bool matched;
                switch (p.NumericScanType) {
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
                    results.Add(new ScanResultViewModel(p, addr + (uint) i, dt, MemoryEngine360.NumericDisplayType.Normal, value.ToString() ?? ""));
                }
            }
        }
    }

    public static async Task ScanFloat<T>(T inputA, T inputB, ScanningProcessor p, ObservableList<ScanResultViewModel> results, IActivityProgress activity) where T : IFloatingPoint<T> {
        DataType dt = p.DataType;
        uint addr = p.StartAddress, scanLen = p.ScanLength, range = scanLen;
        int totalChunks = (int) (range / 65535) + 1;
        for (int j = 0, c = 1; j < scanLen; j += 65535, c++) {
            ActivityManager.Instance.CurrentTask.CheckCancelled();
            activity.Text = $"Reading chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = true;
            byte[] bytes = await p.MemoryEngine360.Connection!.ReadBytes((uint) (addr + j), Math.Min(65535, (uint) Math.Max((int) scanLen - j, 0)));

            activity.Text = $"Scanning chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = false;

            int sizeOfT = Unsafe.SizeOf<T>();
            int blockEnd = 65535 - sizeOfT;
            using var _ = activity.CompletionState.PushCompletionRange(0.0, 256.0 / blockEnd);
            for (int i = 0; i < blockEnd; i++) {
                if (i >= bytes.Length || (bytes.Length - i) < sizeOfT) {
                    break;
                }

                if (i % 256 == 0) {
                    activity.CompletionState.OnProgress(1);
                }

                T value = CreateNumberFromBytes<T>(new ReadOnlySpan<byte>(bytes, i, sizeOfT));
                switch (p.FloatScanOption) {
                    case FloatScanOption.UseExactValue: break;
                    case FloatScanOption.TruncateToQuery:
                    case FloatScanOption.RoundToQuery: {
                        if (p.NumericScanType == NumericScanType.Between) {
                            break;
                        }

                        int idx = p.InputA?.IndexOf('.') ?? -1;
                        if (idx != -1) {
                            int decimals = p.InputA!.Length - idx + 1;
                            if (typeof(T) == typeof(float)) {
                                float value_f = Unsafe.As<T, float>(ref value);
                                value_f = p.FloatScanOption == FloatScanOption.TruncateToQuery ? TruncateFloat(value_f, decimals) : (float) Math.Round(value_f, decimals);
                                value = Unsafe.As<float, T>(ref value_f);
                            }
                            else {
                                double value_d = Unsafe.As<T, double>(ref value);
                                value_d = p.FloatScanOption == FloatScanOption.TruncateToQuery ? TruncateDouble(value_d, decimals) : Math.Round(value_d, decimals);
                                value = Unsafe.As<double, T>(ref value_d);
                            }
                        }

                        break;
                    }
                }

                bool matched;
                switch (p.NumericScanType) {
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
                    results.Add(new ScanResultViewModel(p, addr + (uint) i, dt, MemoryEngine360.NumericDisplayType.Normal, value.ToString() ?? ""));
                }
            }
        }
    }

    public static async Task ScanString(string input, ScanningProcessor p, ObservableList<ScanResultViewModel> results, IActivityProgress activity) {
        DataType dt = p.DataType;
        int cbInputString;
        switch (p.StringScanOption) {
            case StringType.ASCII:
            case StringType.UTF8:
                cbInputString = input.Length;
            break;
            case StringType.UTF16: cbInputString = input.Length * 2; break;
            case StringType.UTF32: cbInputString = input.Length * 4; break;
            default:                     throw new ArgumentOutOfRangeException();
        }

        if (cbInputString > 65535) {
            throw new Exception("Input string is too long. Console memory is read in chunks of 64KB, therefore, the string cannot contain more than that many bytes");
        }

        uint addr = p.StartAddress, scanLen = p.ScanLength, range = scanLen;
        int totalChunks = (int) (range / 65535) + 1;
        for (int j = 0, c = 1; j < scanLen; j += 65535, c++) {
            ActivityManager.Instance.CurrentTask.CheckCancelled();
            activity.Text = $"Reading chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = true;
            byte[] bytes = await p.MemoryEngine360.Connection!.ReadBytes((uint) (addr + j), Math.Min(65535, (uint) Math.Max((int) scanLen - j, 0)));

            activity.Text = $"Scanning chunk {c}/{totalChunks}...";
            activity.IsIndeterminate = false;

            int blockEnd = 65535 - cbInputString;
            using var _ = activity.CompletionState.PushCompletionRange(0.0, 1 / (blockEnd / 256.0));
            for (int i = 0; i < blockEnd; i++) {
                if (i >= bytes.Length || (bytes.Length - i) < cbInputString) {
                    break;
                }

                if (i % 256 == 0) {
                    activity.CompletionState.OnProgress(1);
                }

                string bufferString;
                switch (p.StringScanOption) {
                    case StringType.ASCII: {
                        bufferString = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(bytes, i, cbInputString));
                        break;
                    }
                    case StringType.UTF8: {
                        bufferString = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(bytes, i, cbInputString));
                        break;
                    }
                    case StringType.UTF16: {
                        bufferString = Encoding.Unicode.GetString(new ReadOnlySpan<byte>(bytes, i, cbInputString));
                        break;
                    }
                    case StringType.UTF32: {
                        bufferString = Encoding.UTF32.GetString(new ReadOnlySpan<byte>(bytes, i, cbInputString));
                        break;
                    }
                    default: throw new ArgumentOutOfRangeException();
                }

                if (bufferString.Equals(input)) {
                    results.Add(new ScanResultViewModel(p, addr + (uint) i, dt, MemoryEngine360.NumericDisplayType.Normal, bufferString));
                }
            }
        }
    }

    private static float TruncateFloat(float value, int decimals) {
        float factor = (float) Math.Pow(10, decimals);
        return (float) (Math.Truncate(value * factor) / factor);
    }

    private static double TruncateDouble(double value, int decimals) {
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