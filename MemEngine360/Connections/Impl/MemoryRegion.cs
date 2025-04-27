namespace MemEngine360.Connections.Impl;

public readonly struct MemoryRegion {
    public readonly uint BaseAddress;
    public readonly uint Size;
    public readonly uint Protection;
    public readonly uint PhysicalAddress;

    public MemoryRegion(uint baseAddress, uint size, uint protection, uint physicalAddress) {
        this.BaseAddress = baseAddress;
        this.Size = size;
        this.Protection = protection;
        this.PhysicalAddress = physicalAddress;
    }
}