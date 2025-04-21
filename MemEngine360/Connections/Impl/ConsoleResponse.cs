namespace MemEngine360.Connections.Impl;

public readonly struct ConsoleResponse {
    /// <summary>
    /// Gets the first response line as raw
    /// </summary>
    public readonly string RawMessage;
    
    /// <summary>
    /// Gets the first line's response message
    /// </summary>
    public readonly string Message;
    
    /// <summary>
    /// Gets the response type
    /// </summary>
    public readonly ResponseType ResponseType;

    private ConsoleResponse(string raw, string message, ResponseType responseType) {
        this.RawMessage = raw;
        this.Message = message;
        this.ResponseType = responseType;
    }

    public static ConsoleResponse FromFirstLine(string line) {
        return new ConsoleResponse(line, line.Substring(5), (ResponseType) int.Parse(line.AsSpan(0, 3)));
    }
}