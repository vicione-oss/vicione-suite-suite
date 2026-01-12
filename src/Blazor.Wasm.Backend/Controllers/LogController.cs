using System.IO.Abstractions;
using Core.Shared.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blazor.Wasm.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LogController : ControllerBase
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogLevelSwitch _logLevelSwitch;
    private readonly ILogOptions _logOptions;
    private readonly ILogger<LogController> _logger;
    private readonly string _logDirectory;

    public LogController(IFileSystem fileSystem,
        ILogLevelSwitch logLevelSwitch,
        ILogOptions logOptions,
        ILogger<LogController> logger)
    {
        _fileSystem = fileSystem;
        _logLevelSwitch = logLevelSwitch;
        _logOptions = logOptions;
        _logger = logger;
        var logPath = logOptions.LogPath ?? string.Empty;
        _logDirectory = _fileSystem.Path.IsPathFullyQualified(logPath)
            ? logPath
            : _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), logPath);
    }

    [HttpGet("level")]
    public ActionResult<string> GetLevel() => new JsonResult(Enum.GetName(_logLevelSwitch.LogLevel));

    [HttpGet("level/{level}")]
    public IActionResult SetLevel(LogLevel level)
    {
        _logLevelSwitch.LogLevel = level;
        return Ok();
    }

    [HttpGet("paths")]
    public ActionResult<IEnumerable<string>> GetLogsPaths()
    {
        if (!Directory.Exists(_logDirectory))
            return new JsonResult(Enumerable.Empty<string>());
        return new JsonResult(_fileSystem.Directory.GetFiles(_logDirectory, "*.*", SearchOption.AllDirectories)
            .Select(p => _fileSystem.Path.GetRelativePath(_logDirectory, p)));
    }

    [HttpGet("paths/{logPath}")]
    public async Task<IActionResult> GetLog(string? logPath)
    {
        logPath ??= _logOptions.LogFileNameTemplate;
        if (logPath is null)
            return NotFound();

        var fullPath = _fileSystem.Path.Combine(_logDirectory, Uri.UnescapeDataString(logPath));
        try
        {
            var memory = new MemoryStream();
            await using var stream = new FileStream(fullPath, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

            await stream.CopyToAsync(memory);
            memory.Position = 0;
            return File(memory, "text/plain", _fileSystem.Path.GetFileName(fullPath));
        }
        catch (FileNotFoundException)
        {
            _logger.LogError("Could not find log {Path}", fullPath);
            return NotFound();
        }
    }
}
