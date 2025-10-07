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

// This file contains code adapted from JRPC by XboxChef, licenced under GPL-3.0.
// See LICENCE file for the full terms.
// https://github.com/XboxChef/JRPC/blob/master/JRPC_Client/JRPC.cs

namespace MemEngine360.Connections.Features;

/// <summary>
/// A feature for xbox 360 connections that provide JRPC2 functionality, such as custom notifications, changing the LEDs, etc.
/// <para>
/// Uses version 2 of JRPC
/// </para>
/// </summary>
public interface IFeatureXboxJRPC2 : IConsoleFeature, IFeatureXboxNotifications {
    /// <summary>
    /// Gets the console's CPU key
    /// </summary>
    Task<string> GetCPUKey();

    /// <summary>
    /// Gets the xbox's dashboard version, or possibly kernel version
    /// </summary>
    Task<uint> GetDashboardVersion();

    /// <summary>
    /// Gets the temperature from a specific sensor
    /// </summary>
    /// <param name="sensorType">The type of sensor</param>
    Task<uint> GetTemperature(SensorType sensorType);

    /// <summary>
    /// Gets the current title ID
    /// </summary>
    Task<uint> GetCurrentTitleId();

    /// <summary>
    /// Gets the name of the motherboard, e.g. Trinity
    /// </summary>
    Task<string> GetMotherboardType();

    /// <summary>
    /// Sets the LEDs on the console. This will force the LEDs to remain the
    /// same until the console is restarted
    /// </summary>
    /// <param name="p1">Player 1 LED</param>
    /// <param name="p2">Player 2 LED</param>
    /// <param name="p3">Player 3 LED</param>
    /// <param name="p4">Player 4 LED</param>
    Task SetLEDs(bool p1, bool p2, bool p3, bool p4);

    Task<uint> ResolveFunction(string moduleName, uint ordinal);
    Task<T> Call<T>(uint address, params object[] Arguments) where T : struct;
    Task<T> Call<T>(string module, int ordinal, params object[] args) where T : struct;
    Task<T> Call<T>(ThreadType thread, uint address, params object[] args) where T : struct;
    Task<T> Call<T>(ThreadType thread, string module, int ordinal, params object[] args) where T : struct;
    Task CallVoid(uint address, params object[] args);
    Task CallVoid(string module, int ordinal, params object[] args);
    Task CallVoid(ThreadType thread, uint address, params object[] args);
    Task CallVoid(ThreadType thread, string module, int ordinal, params object[] args);
    Task<T[]> CallArray<T>(uint address, uint arraySize, params object[] args) where T : struct;
    Task<T[]> CallArray<T>(string module, int ordinal, uint arraySize, params object[] args) where T : struct;
    Task<T[]> CallArray<T>(ThreadType thread, uint address, uint arraySize, params object[] args) where T : struct;
    Task<T[]> CallArray<T>(ThreadType thread, string module, int ordinal, uint arraySize, params object[] args) where T : struct;
    Task<string> CallString(uint address, params object[] args);
    Task<string> CallString(string module, int ordinal, params object[] args);
    Task<string> CallString(ThreadType thread, uint address, params object[] args);
    Task<string> CallString(ThreadType thread, string module, int ordinal, params object[] args);
    Task<T> CallVM<T>(uint Address, params object[] Arguments) where T : struct;
    Task<T> CallVM<T>(string module, int ordinal, params object[] Arguments) where T : struct;
    Task<T> CallVM<T>(ThreadType Type, uint Address, params object[] Arguments) where T : struct;
    Task<T> CallVM<T>(ThreadType Type, string module, int ordinal, params object[] Arguments) where T : struct;
    Task CallVMVoid(uint Address, params object[] Arguments);
    Task CallVMVoid(string module, int ordinal, params object[] Arguments);
    Task CallVMVoid(ThreadType Type, uint Address, params object[] Arguments);
    Task CallVMVoid(ThreadType Type, string module, int ordinal, params object[] Arguments);
    Task<T[]> CallVMArray<T>(uint Address, uint ArraySize, params object[] Arguments) where T : struct;
    Task<T[]> CallVMArray<T>(string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct;
    Task<T[]> CallVMArray<T>(ThreadType Type, uint Address, uint ArraySize, params object[] Arguments) where T : struct;
    Task<T[]> CallVMArray<T>(ThreadType Type, string module, int ordinal, uint ArraySize, params object[] Arguments) where T : struct;
    Task<string> CallVMString(uint Address, params object[] Arguments);
    Task<string> CallVMString(string module, int ordinal, params object[] Arguments);
    Task<string> CallVMString(ThreadType Type, uint Address, params object[] Arguments);
    Task<string> CallVMString(ThreadType Type, string module, int ordinal, params object[] Arguments);
}

public enum SensorType {
    CPU,
    GPU,
    EDRAM,
    MotherBoard
}

public enum RPCDataType : uint {
    Void = 0,
    Int = 1,
    String = 2,
    Float = 3,
    Byte = 4,
    IntArray = 5,
    FloatArray = 6,
    ByteArray = 7,
    Uint64 = 8,
    Uint64Array = 9
}

public enum ThreadType {
    System,
    Title
}