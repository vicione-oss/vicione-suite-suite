using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Sdk.Client.Contracts;
using Sdk.Client.Services;

namespace Blazor.Shared.Extensions;

public static partial class RenderTreeBuilderExtensions
{
    extension(RenderTreeBuilder builder)
    {
        public void AddStylesheetResources(IEnumerable<IClientModuleResourceProvider> providers, ILogger logger)
        {
            var resources = CollectResources(providers, logger);
            if (resources.Count == 0)
                return;

            foreach (var resource in resources.Where(r => r.ResourceType == ResourceType.Stylesheet))
            {
                builder.OpenElement(0, "link");
                builder.AddAttribute(1, "href", resource.Url);
                builder.AddAttribute(2, "rel", "stylesheet");
                builder.CloseElement();
            }
        }

        public void AddScriptResources(IEnumerable<IClientModuleResourceProvider> providers, ILogger logger)
        {
            var resources = CollectResources(providers, logger);
            if (resources.Count == 0)
                return;

            foreach (var resource in resources.Where(r => r.ResourceType == ResourceType.Script))
            {
                builder.OpenElement(1, "script");
                builder.AddAttribute(2, "src", resource.Url);
                builder.CloseElement();
            }
        }
    }

    private static List<Resource> CollectResources(IEnumerable<IClientModuleResourceProvider> providers, ILogger logger)
    {
        var resources = new List<Resource>();

        foreach (var provider in providers)
        {
            try
            {
                resources.AddRange(provider.GetResources());
            }
            catch (Exception ex)
            {
                GetClientModuleResourcesFailed(logger, provider.GetType().FullName, ex);
            }
        }

        return resources;
    }

    [LoggerMessage(LogLevel.Error, "Failed to get client module resources from provider {ProviderType}.")]
    private static partial void GetClientModuleResourcesFailed(ILogger logger, string? providerType, Exception exception);
}
