namespace Blazor.Shared.Wizard.Models;

/// <inheritdoc cref="ISaveResult"/>
/// <param name="message">Error message</param>
public class SaveErrorResult(string message, int? errorCode = null) : ISaveResult
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Message => message;

    /// <summary>
    /// Optional error code
    /// </summary>
    public int? ErrorCode => errorCode;
}
