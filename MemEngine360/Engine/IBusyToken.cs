namespace MemEngine360.Engine;

/// <summary>
/// Represents a busy token
/// </summary>
public interface IBusyToken : IDisposable {
    /// <summary>
    /// Gets the busy lock associated with this token. Null when disposed
    /// </summary>
    BusyLock? BusyLock { get; }
}