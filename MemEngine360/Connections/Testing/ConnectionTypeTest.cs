namespace MemEngine360.Connections.Testing;

public class ConnectionTypeTest : RegisteredConnectionType {
    public const string TheID = "console.test-donotuse";
    public static readonly RegisteredConnectionType Instance = new ConnectionTypeTest();
    
    public override string DisplayName => "Test";
    
    public override string LongDescription => "No features implemented, just throws errors, in hopes that the program can handle them.";
    
    public override Task<IConsoleConnection?> OpenConnection(UserConnectionInfo? _info, CancellationTokenSource cancellation) {
        return Task.FromResult<IConsoleConnection?>(new TestConsoleConnection());
    }
}