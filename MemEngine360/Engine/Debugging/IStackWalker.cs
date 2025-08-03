using MemEngine360.Connections;

namespace MemEngine360.Engine.Debugging;

public interface IStackWalker {
    void Walk_NotReadyYet(IConsoleConnection connection, ThreadContext context);
}