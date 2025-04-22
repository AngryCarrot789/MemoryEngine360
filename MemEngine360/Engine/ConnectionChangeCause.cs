namespace MemEngine360.Engine;

public enum ConnectionChangeCause {
    /// <summary>
    /// The user connected to or disconnected from the console via in the standard ways
    /// </summary>
    User,
    /// <summary>
    /// The background worker notices the console connection was no longer
    /// actually connected, so it was automatically changed to null
    /// </summary>
    LostConnection,
    /// <summary>
    /// The user closed the window which automatically disconnects the connection
    /// </summary>
    ClosingWindow,
    /// <summary>
    /// The connection changed for an unknown reason
    /// </summary>
    Custom
}