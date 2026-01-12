using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Core.OS.Modules.Services;

internal sealed class ApplicationPartsLogger(ILogger<ApplicationPartsLogger> logger, ApplicationPartManager partManager, IWebHostEnvironment env) :
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
        logger.LogInformation("Use application parts: '{ApplicationParts}'", string.Join(", ", applicationParts));

        // Create a controller feature, and populate it from the application parts
        var controllerFeature = new ControllerFeature();
        partManager.PopulateFeature(controllerFeature);

        // Get the names of all controllers
        var controllers = controllerFeature.Controllers.Select(x => x.Name);
        logger.LogInformation("Use registered controllers: '{Controllers}'", string.Join(", ", controllers));

        return Task.CompletedTask;
    }

    // Required by the interface
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
