namespace MemEngine360.Engine.Modes;

public enum NumericScanType {
    /// <summary>
    /// Match when memory equals query
    /// </summary>
    Equals,
    /// <summary>
    /// Match when memory is not equal to query
    /// </summary>
    NotEquals,
    /// <summary>
    /// Match when memory is less than query
    /// </summary>
    LessThan,
    /// <summary>
    /// Match when memory is less than or equal to query
    /// </summary>
    LessThanOrEquals,
    /// <summary>
    /// Match when memory is greater than query
    /// </summary>
    GreaterThan,
    /// <summary>
    /// Match when memory is greater than or equal to query
    /// </summary>
    GreaterThanOrEquals,
    /// <summary>
    /// Match when memory is greater than or equal to query1 ("from") and
    /// less than or equal to query2 ("to"), therefore, making both query values inclusive
    /// </summary>
    Between
}