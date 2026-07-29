using System.IO.Abstractions;
using System.Reflection;
using Core.UiHosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Client.Contracts;
using Sdk.Modules;

namespace Blazor.Server.Backend.Services;

public class StreamUploadHandlerFactory(IUiHostEnvironment uiHostEnvironment, IServiceProvider serviceProvider) : IStreamUploadHandlerFactory
{
    public IStreamUploadHandler<TMarker> CreateStreamUploadHandler<TMarker, TModule>(StreamUploadHandlerOptions options) where TModule : IModule
    {
        var moduleType = typeof(TModule);

        if (!moduleType.IsAssignableTo(typeof(BackendModule)))
        {
            throw new ArgumentException($"{moduleType} is not assignable to {typeof(BackendModule)}.", nameof(TMarker));
        }

        return CreateStreamUploadHandler<TMarker>(options, moduleType);
    }

    private IStreamUploadHandler<TMarker> CreateStreamUploadHandler<TMarker>(StreamUploadHandlerOptions options, Type moduleType)
    {
        var result = GetType().GetMethod(nameof(CreateStreamUploadHandlerBackendModule), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(typeof(TMarker), moduleType)
                    .Invoke(this, [options]);

        if (result is null)
        {
            throw new InvalidOperationException("Failed to create stream upload handler.");
        }

        return (IStreamUploadHandler<TMarker>)result;
    }

    public IStreamUploadHandler<TMarker> CreateStreamUploadHandlerFromModuleId<TMarker>(StreamUploadHandlerOptions options, string moduleId)
    {
        var module = uiHostEnvironment.GetBackendModule(moduleId);

        if (module is null)
        {
            throw new ArgumentException($"No BackendModule with id {moduleId} has been found.", nameof(moduleId));
        }

        return CreateStreamUploadHandler<TMarker>(options, module.GetType());
    }

    private IStreamUploadHandler<TMarker> CreateStreamUploadHandlerBackendModule<TMarker, TModule>(StreamUploadHandlerOptions options) where TModule : BackendModule
        => new StreamUploadHandler<TModule, TMarker>(serviceProvider.GetRequiredService<IWorkspaceProvider<TModule>>(),
                                                     serviceProvider.GetRequiredService<IFileSystem>(),
                                                     options,
                                                     serviceProvider.GetRequiredService<ILogger<StreamUploadHandler<TModule, TMarker>>>());
}
