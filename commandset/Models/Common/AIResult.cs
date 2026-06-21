namespace RevitMCPCommandSet.Models.Common;

public class AIResult<T>
{
    /// <summary>
    ///     Is successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     Return data
    /// </summary>
    public T Response { get; set; }
}