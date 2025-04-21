namespace MemEngine360.Connections.Impl.Threads;

public struct ConsoleThread {
    public int id;
    public int suspendCount;      // "suspend"
    public int priority;          // "priority"
    public int tlsBaseAddress;    // "tlsbase"
    public int baseAddress;       // "base"
    public int limit;             // "limit"
    public int slack;             // "slack"
    public ulong creationTime;    // createhi | createlo
    public int nameAddress;       // "nameaddr";
    public int nameLength;        // "namelen";
    public int currentProcessor;  // "proc";
    public int lastError;         // "proc";
    public string readableName;

    public override string ToString() {
        return $"Thread '{this.readableName}': {{ {nameof(this.id)}: {this.id}, {nameof(this.suspendCount)}: {this.suspendCount}, {nameof(this.priority)}: {this.priority}, {nameof(this.tlsBaseAddress)}: {this.tlsBaseAddress:X8}, {nameof(this.baseAddress)}: {this.baseAddress:X8}, {nameof(this.limit)}: {this.limit:X8}, {nameof(this.slack)}: {this.slack}, {nameof(this.creationTime)}: {this.creationTime}, {nameof(this.nameAddress)}: {this.nameAddress:X8}, {nameof(this.nameLength)}: {this.nameLength}, {nameof(this.currentProcessor)}: {this.currentProcessor}, {nameof(this.lastError)}: {this.lastError} }}";
    }
}