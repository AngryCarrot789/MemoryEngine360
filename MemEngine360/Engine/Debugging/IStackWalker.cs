using MemEngine360.Connections;

namespace MemEngine360.Engine.Debugging;

public interface IStackWalker {
    void Walk(IConsoleConnection connection, ThreadContext context);
}