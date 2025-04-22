using System.Numerics;
using MemEngine360.Connections.Impl;
using MemEngine360.Connections.Impl.Threads;
using ConsoleColor = MemEngine360.Connections.Impl.ConsoleColor;

namespace MemEngine360.Connections;

public delegate void ConsoleConnectionEventHandler(IConsoleConnection connection);

public interface IConsoleConnection : IDisposable {
    bool IsReallyConnected { get; }
    bool IsBusy { get; }

    Task<ConsoleResponse> SendCommand(string command);
    Task<List<string>> SendCommandAndReceiveLines(string Text);
    Task<List<KeyValuePair<string, string>>> SendCommandAndReceiveLines2(string Text);

    Task<List<int>> GetThreads();
    Task<List<ConsoleThread>> GetThreadDump();
    
    Task Eject();
    Task Reboot();
    Task ShutdownConsole();
    Task DebugFreeze();
    Task DebugUnFreeze();
    Task DeleteFile(string Path);
    Task LaunchFile(string Path);
    Task<string> GetConsoleID();
    Task<string> GetCPUKey();
    Task<string> GetDebugName();
    Task<ExecutionState> GetExecutionState();
    Task<HardwareInfo> GetHardwareInfo();
    Task<uint> GetProcessID();
    Task<uint> GetTitleAddress();
    Task<bool> ReadBool(uint Offset);
    Task<byte> ReadByte(uint Offset);
    Task<byte[]> ReadBytes(uint address, uint count);
    Task<byte[]> ReadBytesEx_BARELY_WORKS(uint address, uint count);
    Task<char> ReadChar(uint Offset);
    Task<double> ReadDouble(uint Offset);
    Task<float> ReadFloat(uint Offset);
    Task<short> ReadInt16(uint Offset);
    Task<int> ReadInt32(uint Offset);
    Task<long> ReadInt64(uint Offset);
    Task<sbyte> ReadSByte(uint Offset);
    Task<string> ReadString(uint Offset, uint Length);
    Task<ushort> ReadUInt16(uint Offset);
    Task<uint> ReadUInt32(uint Offset);
    Task<ulong> ReadUInt64(uint Offset);
    Task<Vector2> ReadVector2(uint Offset);
    Task<Vector3> ReadVector3(uint Offset);
    Task SetConsoleColor(ConsoleColor Color);
    Task SetDebugName(string DebugName);
    Task WriteBool(uint Offset, bool Bool);
    Task WriteByte(uint Offset, byte Byte);
    Task WriteByte(uint Offset, byte[] Bytes);
    Task WriteChar(uint Offset, char Char);
    Task WriteDouble(uint Offset, double Double);
    Task WriteFile(uint Offset, string Path);
    Task WriteFloat(uint Offset, float Float);
    Task WriteHook(uint Offset, uint Destination, bool Linked);
    Task WriteInt16(uint Offset, short Int16);
    Task WriteInt32(uint Offset, int Int32);
    Task WriteInt64(uint Offset, long Int64);
    Task WriteNOP(uint Offset);
    Task WriteString(uint Offset, string String);
    Task WriteSByte(uint Offset, sbyte SByte);
    Task WriteUInt16(uint Offset, ushort UInt16);
    Task WriteUInt32(uint Offset, uint UInt32);
    Task WriteUInt64(uint Offset, ulong UInt64);
    Task WriteVector2(uint Offset, Vector2 Vector2);
    Task WriteVector3(uint Offset, Vector3 Vector3);
}