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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Utils;

namespace MemEngine360.Xbox360XBDM.Consoles.Xbdm;

// This file contains code adapted from JRPC by XboxChef, licenced under GPL-3.0.
// See LICENCE file for the full terms.
// https://github.com/XboxChef/JRPC/blob/master/JRPC_Client/JRPC.cs

public class Jrpc2FeaturesImpl : IFeatureXboxJRPC2 {
    private readonly XbdmConsoleConnection connection;

    public IConsoleConnection Connection => this.connection;

    public Jrpc2FeaturesImpl(XbdmConsoleConnection connection) {
        this.connection = connection;
    }

    public Task ShowNotification(XNotifyLogo logo, string? message) {
        string msgHex = message != null ? NumberUtils.ConvertStringToHex(message, Encoding.ASCII) : "";
        string command = $"consolefeatures ver=2 type=12 params=\"A\\0\\A\\2\\2/{message?.Length ?? 0}\\{msgHex}\\{(uint) RPCDataType.Int}\\{(int) logo}\\\"";
        return this.connection.SendCommand(command);
    }

    public async Task<string> GetCPUKey() {
        const string command = "consolefeatures ver=2 type=10 params=\"A\\0\\A\\0\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType != XbdmResponseType.SingleResponse || response.Message.Length != 32) {
            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }

        return response.Message;
    }

    public async Task<uint> GetDashboardVersion() {
        const string command = "consolefeatures ver=2 type=13 params=\"A\\0\\A\\4\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType != XbdmResponseType.SingleResponse || !uint.TryParse(response.Message, out uint version)) {
            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }

        return version;
    }

    public async Task<uint> GetTemperature(SensorType sensorType) {
        string command = $"consolefeatures ver=2 type=15 params=\"A\\0\\A\\1\\{(uint) RPCDataType.Int}\\{(int) sensorType}\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType == XbdmResponseType.SingleResponse) {
            if (uint.TryParse(response.Message, NumberStyles.HexNumber, null, out uint version)) {
                return version;
            }
        }

        this.connection.Close();
        throw new IOException("JRPC2 did not respond correctly");
    }

    public async Task<uint> GetCurrentTitleId() {
        const string command = "consolefeatures ver=2 type=16 params=\"A\\0\\A\\0\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType == XbdmResponseType.SingleResponse) {
            if (uint.TryParse(response.Message, NumberStyles.HexNumber, null, out uint version)) {
                return version;
            }
        }

        this.connection.Close();
        throw new IOException("JRPC2 did not respond correctly");
    }

    public async Task<string> GetMotherboardType() {
        const string command = "consolefeatures ver=2 type=17 params=\"A\\0\\A\\0\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType == XbdmResponseType.SingleResponse) {
            return response.Message;
        }

        this.connection.Close();
        throw new IOException("JRPC2 did not respond correctly");
    }

    public async Task SetLEDs(bool p1, bool p2, bool p3, bool p4) {
        int bits = ((p4 ? 1 : 0) << 3) | ((p3 ? 1 : 0) << 2) | ((p2 ? 1 : 0) << 1) | (p1 ? 1 : 0);
        string command = $"consolefeatures ver=2 type=14 params=\"A\\0\\A\\4\\1\\0\\1\\0\\1\\0\\1\\{bits * 16}\\\"";
        XbdmResponse response = await this.connection.SendCommand(command);
        if (response.ResponseType != XbdmResponseType.SingleResponse || response.Message != "S_OK") {
            this.connection.Close();
            throw new IOException("JRPC2 did not respond correctly");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RPCDataType TypeToRPCType<T>(bool isArray) where T : struct {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(char))
            return isArray ? RPCDataType.ByteArray : RPCDataType.Byte;
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort) || typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            return isArray ? RPCDataType.IntArray : RPCDataType.Int;
        else if (typeof(T) == typeof(ulong) || typeof(T) == typeof(long))
            return isArray ? RPCDataType.Uint64Array : RPCDataType.Uint64;
        else if (typeof(T) == typeof(float) || typeof(T) == typeof(double))
            return isArray ? RPCDataType.FloatArray : RPCDataType.Float;
        else if (typeof(T) == typeof(string) || typeof(T) == typeof(char[]))
            return RPCDataType.String;
        else // CLR object
            return RPCDataType.Uint64;
    }

    public async Task<uint> ResolveFunction(string moduleName, uint ordinal) {
        string? txt = await this.SendCommand($"consolefeatures ver=2 type=9 params=\"A\\0\\A\\2\\" +
                                             $"{(int) RPCDataType.String}/{moduleName.Length}\\" +
                                             $"{NumberUtils.ConvertStringToHex(moduleName, Encoding.ASCII)}\\" +
                                             $"{(int) RPCDataType.Int}\\{ordinal}\\\"");
        int index;
        if (txt == null || (index = txt.IndexOf(' ')) == -1)
            return 0;
        if (!uint.TryParse(txt.AsSpan(index + 1), NumberStyles.HexNumber, null, out uint address))
            return 0;
        return address;
    }

    public async Task<T> Call<T>(uint address, params object[] Arguments) where T : struct {
        return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), null, 0, address, 0, false, Arguments);
    }

    public async Task<T> Call<T>(string module, int ordinal, params object[] args) where T : struct {
        return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, false, args);
    }

    public async Task<T> Call<T>(ThreadType thread, uint address, params object[] args) where T : struct {
        return (T) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(false), typeof(T), null, 0, address, 0, false, args);
    }

    public async Task<T> Call<T>(ThreadType thread, string module, int ordinal, params object[] args) where T : struct {
        return (T) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, false, args);
    }

    public Task CallVoid(uint address, params object[] args) {
        return this.CallArgs(true, RPCDataType.Void, typeof(void), null, 0, address, 0, false, args);
    }

    public Task CallVoid(string module, int ordinal, params object[] args) {
        return this.CallArgs(true, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, false, args);
    }

    public Task CallVoid(ThreadType thread, uint address, params object[] args) {
        return this.CallArgs(thread == ThreadType.System, RPCDataType.Void, typeof(void), null, 0, address, 0, false, args);
    }

    public Task CallVoid(ThreadType thread, string module, int ordinal, params object[] args) {
        return this.CallArgs(thread == ThreadType.System, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, false, args);
    }

    public async Task<T[]> CallArray<T>(uint address, uint arraySize, params object[] args) where T : struct {
        if (arraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), null, 0, address, arraySize, false, args);
    }

    public async Task<T[]> CallArray<T>(string module, int ordinal, uint arraySize, params object[] args) where T : struct {
        if (arraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, arraySize, false, args);
    }

    public async Task<T[]> CallArray<T>(ThreadType thread, uint address, uint arraySize, params object[] args) where T : struct {
        if (arraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(true), typeof(T), null, 0, address, arraySize, false, args);
    }

    public async Task<T[]> CallArray<T>(ThreadType thread, string module, int ordinal, uint arraySize, params object[] args) where T : struct {
        if (arraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(thread == ThreadType.System, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, arraySize, false, args);
    }

    public async Task<string> CallString(uint address, params object[] args) {
        return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), null, 0, address, 0, false, args);
    }

    public async Task<string> CallString(string module, int ordinal, params object[] args) {
        return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), module, ordinal, 0, 0, false, args);
    }

    public async Task<string> CallString(ThreadType thread, uint address, params object[] args) {
        return (string) await this.CallArgs(thread == ThreadType.System, RPCDataType.String, typeof(string), null, 0, address, 0, false, args);
    }

    public async Task<string> CallString(ThreadType thread, string module, int ordinal, params object[] args) {
        return (string) await this.CallArgs(thread == ThreadType.System, RPCDataType.String, typeof(string), module, ordinal, 0, 0, false, args);
    }

    public async Task<T> CallVM<T>(uint Address, params object[] Arguments) where T : struct {
        return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, true, Arguments);
    }

    public async Task<T> CallVM<T>(string module, int ordinal, params object[] Arguments) where T : struct {
        return (T) await this.CallArgs(true, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, true, Arguments);
    }

    public async Task<T> CallVM<T>(ThreadType Type, uint Address, params object[] Arguments) where T : struct {
        return (T) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(false), typeof(T), null, 0, Address, 0, true, Arguments);
    }

    public async Task<T> CallVM<T>(ThreadType Type, string module, int ordinal, params object[] Arguments) where T : struct {
        return (T) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(false), typeof(T), module, ordinal, 0, 0, true, Arguments);
    }

    public Task CallVMVoid(uint Address, params object[] Arguments) {
        return this.CallArgs(true, RPCDataType.Void, typeof(void), null, 0, Address, 0, true, Arguments);
    }

    public Task CallVMVoid(string module, int ordinal, params object[] Arguments) {
        return this.CallArgs(true, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, true, Arguments);
    }

    public Task CallVMVoid(ThreadType Type, uint Address, params object[] Arguments) {
        return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), null, 0, Address, 0, true, Arguments);
    }

    public Task CallVMVoid(ThreadType Type, string module, int ordinal, params object[] Arguments) {
        return this.CallArgs(Type == ThreadType.System, RPCDataType.Void, typeof(void), module, ordinal, 0, 0, true, Arguments);
    }

    public async Task<T[]> CallVMArray<T>(uint Address, uint ArraySize, params object[] Arguments) where T : struct {
        if (ArraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, true, Arguments);
    }

    public async Task<T[]> CallVMArray<T>(string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
        if (ArraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(true, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, true, Arguments);
    }

    public async Task<T[]> CallVMArray<T>(ThreadType Type, uint Address, uint ArraySize, params object[] Arguments) where T : struct {
        if (ArraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), null, 0, Address, ArraySize, true, Arguments);
    }

    public async Task<T[]> CallVMArray<T>(ThreadType Type, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct {
        if (ArraySize == 0)
            return new T[1];
        return (T[]) await this.CallArgs(Type == ThreadType.System, TypeToRPCType<T>(true), typeof(T), module, ordinal, 0, ArraySize, true, Arguments);
    }

    public async Task<string> CallVMString(uint Address, params object[] Arguments) {
        return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), null, 0, Address, 0, true, Arguments);
    }

    public async Task<string> CallVMString(string module, int ordinal, params object[] Arguments) {
        return (string) await this.CallArgs(true, RPCDataType.String, typeof(string), module, ordinal, 0, 0, true, Arguments);
    }

    public async Task<string> CallVMString(ThreadType Type, uint Address, params object[] Arguments) {
        return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), null, 0, Address, 0, true, Arguments);
    }

    public async Task<string> CallVMString(ThreadType Type, string module, int ordinal, params object[] Arguments) {
        return (string) await this.CallArgs(Type == ThreadType.System, RPCDataType.String, typeof(string), module, ordinal, 0, 0, true, Arguments);
    }

    private async Task<string?> SendCommand(string Command) {
        XbdmResponse response = await this.connection.SendCommand(Command);
        if (response.RawMessage.Contains("error="))
            throw new IOException(response.RawMessage.Substring(11));
        if (response.RawMessage.Contains("DEBUG"))
            throw new IOException("JRPC is not installed on the current console");
        if (response.ResponseType == XbdmResponseType.InvalidArgument)
            return null;
        return response.RawMessage;
    }

    private async Task<object> CallArgs(bool onSystemThread, RPCDataType dataType, Type t, string? module, int ordinal, uint addr, uint arraySize, bool vm, params object[] arguments) {
        if (!IsValidReturnType(t))
            throw new IOException("Invalid type " + (object) t.Name + Environment.NewLine + "JRPC only supports: " + "bool, byte, short, int, long, ushort, uint, ulong, float, double");

        uint argc = 0;
        string cmdParams = CreateParams(vm, arguments, ref argc);
        if (argc > 37)
            throw new IOException("Can not use more than 37 paramaters in a call");

        string startSendCMD = $"consolefeatures ver=2 " +
                              $"type={(uint) dataType}" +
                              $"{(onSystemThread ? " system" : "")}" +
                              $"{(module != null ? (" module=\"" + module + "\" ord=" + ordinal) : "")}" +
                              $"{(vm ? " VM" : "")} " +
                              $"as={arraySize} " +
                              $"params=\"A\\{addr:X}\\A\\{argc}\\{cmdParams}";

        const string findText = "buf_addr=";
        string? response = await this.SendCommand(startSendCMD);
        if (response == null)
            throw new IOException("Invalid argument");

        while (response.Contains(findText)) {
            await Task.Delay(100);
            uint address = uint.Parse(response.AsSpan(response.IndexOf(findText) + findText.Length), NumberStyles.HexNumber);
            response = await this.SendCommand("consolefeatures " + findText + "0x" + address.ToString("X"));
            if (response == null)
                throw new IOException("Invalid argument");
        }

        switch (dataType) {
            case RPCDataType.Int:
                uint uVal = uint.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                if (t == typeof(uint))
                    return uVal;
                if (t == typeof(int))
                    return (int) uVal;
                if (t == typeof(short))
                    return short.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                if (t == typeof(ushort))
                    return ushort.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                break;
            case RPCDataType.String:
                string sString = response.Substring(response.IndexOf(' ') + 1);
                if (t == typeof(string))
                    return sString;
                if (t == typeof(char[]))
                    return sString.ToCharArray();
                break;
            case RPCDataType.Float:
                if (t == typeof(double))
                    return double.Parse(response.AsSpan(response.IndexOf(' ') + 1));
                if (t == typeof(float))
                    return float.Parse(response.AsSpan(response.IndexOf(' ') + 1));
                break;
            case RPCDataType.Byte:
                byte bByte = byte.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                if (t == typeof(byte))
                    return bByte;
                if (t == typeof(char))
                    return (char) bByte;
                break;
            case RPCDataType.Uint64:
                if (t == typeof(long))
                    return long.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                if (t == typeof(ulong))
                    return ulong.Parse(response.AsSpan(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
                break;
            case RPCDataType.IntArray: {
                string String = response.Substring(response.IndexOf(' ') + 1);
                int Tmp = 0;
                string Temp = "";
                uint[] Uarray = new uint[8];
                foreach (char Char1 in String) {
                    if (Char1 != ',' && Char1 != ';')
                        Temp += Char1.ToString();
                    else {
                        Uarray[Tmp] = uint.Parse(Temp, NumberStyles.HexNumber);
                        Tmp += 1;
                        Temp = "";
                    }

                    if (Char1 == ';')
                        break;
                }

                return Uarray;
            }
            case RPCDataType.FloatArray: {
                string String = response.Substring(response.IndexOf(' ') + 1);
                int Tmp = 0;
                string Temp = "";
                float[] Farray = new float[arraySize];
                foreach (char Char1 in String) {
                    if (Char1 != ',' && Char1 != ';')
                        Temp += Char1.ToString();
                    else {
                        Farray[Tmp] = float.Parse(Temp);
                        Tmp += 1;
                        Temp = "";
                    }

                    if (Char1 == ';')
                        break;
                }

                return Farray;
            }
            case RPCDataType.ByteArray: {
                string String = response.Substring(response.IndexOf(' ') + 1);
                int Tmp = 0;
                string Temp = "";
                byte[] Barray = new byte[arraySize];
                foreach (char Char1 in String) {
                    if (Char1 != ',' && Char1 != ';')
                        Temp += Char1.ToString();
                    else {
                        Barray[Tmp] = byte.Parse(Temp);
                        Tmp += 1;
                        Temp = "";
                    }

                    if (Char1 == ';')
                        break;
                }

                return Barray;
            }
            case RPCDataType.Uint64Array: {
                string str = response.Substring(response.IndexOf(' ') + 1);
                int Tmp = 0;
                string Temp = "";
                ulong[] ulongArray = new ulong[arraySize];
                foreach (char ch in str) {
                    if (ch != ',' && ch != ';')
                        Temp += ch.ToString();
                    else {
                        ulongArray[Tmp] = ulong.Parse(Temp);
                        Tmp += 1;
                        Temp = "";
                    }

                    if (ch == ';')
                        break;
                }

                if (t == typeof(ulong)) {
                    return ulongArray;
                }
                else if (t == typeof(long)) {
                    long[] longArray = new long[arraySize];
                    for (int i = 0; i < arraySize; i++)
                        longArray[i] = (long) ulongArray[i];
                    return longArray;
                }

                break;
            }
            case RPCDataType.Void: return 0;
        }

        return ulong.Parse(response.Substring(response.IndexOf(' ') + 1), NumberStyles.HexNumber);
    }

    private static string CreateParams(bool vm, object[] arguments, ref uint argc) {
        Span<byte> bytes8 = stackalloc byte[8];

        StringBuilder sb = new StringBuilder(128);
        foreach (object obj in arguments) {
            switch (obj) {
                case uint u:
                    sb.Append((uint) RPCDataType.Int).Append("\\" + (int) u).Append('\\');
                    argc += 1;
                    break;
                case int:
                case bool:
                case byte: {
                    sb.Append((uint) RPCDataType.Int).Append('\\' + (obj is bool b ? (b ? "1" : "0") : obj.ToString())).Append('\\');
                    argc += 1;
                    break;
                }
                case int[]:
                case uint[]: {
                    if (!vm) {
                        byte[] bytes = IntArrayToByte((int[]) obj);
                        sb.Append((uint) RPCDataType.ByteArray).Append("/" + bytes.Length).Append('\\');
                        foreach (byte b in bytes)
                            sb.Append(b.ToString("X2"));
                        sb.Append('\\');
                        argc += 1;
                    }
                    else {
                        int[] dstArray;
                        if (obj is int[] srcArray) {
                            dstArray = srcArray;
                        }
                        else {
                            uint[] uintArray = (uint[]) obj;
                            dstArray = new int[uintArray.Length];
                            for (int i = 0; i < uintArray.Length; i++) {
                                dstArray[i] = (int) uintArray[i];
                            }
                        }

                        foreach (int value in dstArray) {
                            sb.Append((uint) RPCDataType.Int).Append('\\' + value).Append('\\');
                            argc += 1;
                        }
                    }

                    break;
                }
                case string str: {
                    sb.Append((uint) RPCDataType.ByteArray).Append("/" + str.Length).Append('\\' + NumberUtils.ConvertStringToHex(str, Encoding.ASCII)).Append('\\');
                    argc += 1;
                    break;
                }
                case double d: {
                    sb.Append((uint) RPCDataType.Float).Append('\\' + d.ToString()).Append('\\');
                    argc += 1;
                    break;
                }
                case float f: {
                    sb.Append((uint) RPCDataType.Float).Append('\\' + f.ToString()).Append('\\');
                    argc += 1;
                    break;
                }
                case float[] floats: {
                    if (!vm) {
                        sb.Append((uint) RPCDataType.FloatArray).Append("/" + (floats.Length * 4)).Append('\\');
                        Span<byte> bytes4 = bytes8.Slice(0, sizeof(float));
                        foreach (float f in floats) {
                            Unsafe.As<byte, double>(ref bytes4[0]) = f;
                            bytes4.Reverse();
                            for (int q = 0; q < 4; q++)
                                sb.Append(bytes4[q].ToString("X2"));
                        }

                        sb.Append('\\');
                        argc += 1;
                    }
                    else {
                        foreach (float f in floats) {
                            sb.Append((uint) RPCDataType.Float).Append('\\' + f.ToString()).Append('\\');
                            argc += 1;
                        }
                    }

                    break;
                }
                case double[] doubles: {
                    if (!vm) {
                        sb.Append((uint) RPCDataType.FloatArray).Append("/" + (doubles.Length * 4)).Append('\\');
                        foreach (double d in doubles) {
                            Unsafe.As<byte, double>(ref bytes8[0]) = d;
                            bytes8.Reverse();
                            for (int q = 0; q < 4; q++)
                                sb.Append(bytes8[q].ToString("X2"));
                        }

                        sb.Append('\\');
                        argc += 1;
                    }
                    else {
                        foreach (double d in doubles) {
                            sb.Append((uint) RPCDataType.Float).Append('\\' + d.ToString()).Append('\\');
                            argc += 1;
                        }
                    }

                    break;
                }
                case byte[] bytes: {
                    sb.Append((uint) RPCDataType.ByteArray).Append("/" + bytes.Length).Append('\\');
                    foreach (byte b in bytes)
                        sb.Append(b.ToString("X2"));

                    sb.Append('\\');
                    argc += 1;
                    break;
                }
                default: {
                    sb.Append((uint) RPCDataType.Uint64).Append('\\').Append(ConvertToUInt64(obj)).Append('\\');
                    argc += 1;
                    break;
                }
            }
        }

        sb.Append('\"');
        return sb.ToString();
    }

    private static readonly HashSet<Type> ValidReturnTypes = new HashSet<Type>() {
        typeof(void),
        typeof(bool),
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(ushort),
        typeof(uint),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(string),
        typeof(bool[]),
        typeof(byte[]),
        typeof(short[]),
        typeof(int[]),
        typeof(long[]),
        typeof(ushort[]),
        typeof(uint[]),
        typeof(ulong[]),
        typeof(float[]),
        typeof(double[]),
        typeof(string[])
    };

    public static bool IsValidReturnType(Type t) {
        return ValidReturnTypes.Contains(t);
    }

    private static ulong ConvertToUInt64(object o) {
        return o switch {
            bool bo => !bo ? 0UL : 1UL,
            byte b => b,
            short s => (ulong) s,
            int i => (ulong) i,
            long l => (ulong) l,
            ushort us => us,
            uint u => u,
            ulong ul => ul,
            float f => (ulong) BitConverter.DoubleToInt64Bits(f),
            double d => (ulong) BitConverter.DoubleToInt64Bits(d),
            _ => 0UL
        };
    }

    private static byte[] IntArrayToByte(int[] array) {
        byte[] output = new byte[array.Length * sizeof(int)];
        for (int i = 0; i < array.Length; i++) {
            Unsafe.As<byte, int>(ref output[i * sizeof(int)]) = array[i];
        }

        return output;
    }
}