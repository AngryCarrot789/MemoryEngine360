namespace MemEngine360.Connections.Impl;

public struct HardwareInfo {
    public uint Flags;
    public byte NumberOfProcessors, PCIBridgeRevisionID;
    public byte[] ReservedBytes;
    public ushort BldrMagic, BldrFlags;
}