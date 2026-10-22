using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Blazor.Server.Backend.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class ErrorModel(ILogger<ErrorModel> logger) : PageModel
{
    private readonly ILogger<ErrorModel> _logger = logger;

    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
        LogGettingRequestId(_logger);
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Getting RequestId")]
    private static partial void LogGettingRequestId(ILogger logger);
}
