namespace Blazor.Shared.Wizard.Models;

/// <inheritdoc cref="ISaveResult"/>
/// <param name="message">Success message</param>
public class SaveSuccessResult(string? message = null) : ISaveResult
{
    /// <summary>
    /// Success message
    /// </summary>
    public string? Message => message;
}
