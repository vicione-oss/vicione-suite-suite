using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Core.OS.Modules.Services;

internal sealed partial class ApplicationPartsLogger(ILogger<ApplicationPartsLogger> logger, ApplicationPartManager partManager, IWebHostEnvironment env) :
    IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        // Get the names of all the application parts. This is the short assembly name for AssemblyParts
        var applicationParts = partManager.ApplicationParts.Select(x => x.Name);
        LogApplicationParts(logger, string.Join(", ", applicationParts));

        // Create a controller feature, and populate it from the application parts
        var controllerFeature = new ControllerFeature();
        partManager.PopulateFeature(controllerFeature);

        // Get the names of all controllers
        var controllers = controllerFeature.Controllers.Select(x => x.Name);
        LogRegisteredControllers(logger, string.Join(", ", controllers));

        return Task.CompletedTask;
    }

    // Required by the interface
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Use application parts: '{ApplicationParts}'")]
    private static partial void LogApplicationParts(ILogger<ApplicationPartsLogger> logger, string applicationParts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Use registered controllers: '{Controllers}'")]
    private static partial void LogRegisteredControllers(ILogger<ApplicationPartsLogger> logger, string controllers);
}
