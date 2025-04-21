namespace MemEngine360.Engine.Modes;

public enum FloatScanOption : byte {
    /// <summary>
    /// Uses the floating point number exactly how it was read from the console
    /// </summary>
    UseExactValue,
    /// <summary>
    /// Truncates decimal places off of the value from the console, according to how many places
    /// there are in the query. Effectively becomes integer comparison if the user specifies no decimal places
    /// </summary>
    TruncateToQuery,
    /// <summary>
    /// Rounds the console value to the number of decimal places in the query. Effectively
    /// becomes integer comparison if the user specifies no decimal places
    /// </summary>
    RoundToQuery
}