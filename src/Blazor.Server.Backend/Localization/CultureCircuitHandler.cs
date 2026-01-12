using System.Globalization;
using Blazor.Shared;
using Core.Shared.Instance.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;

namespace Blazor.Server.Backend.Localization;

public sealed class CultureCircuitHandler(IServiceProvider serviceProvider) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope(); // new Scope is needed sometimes
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true)
            return;

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
        var userName = userManager.GetUserName(httpContext.User);
        if (userName is null)
            return;

        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var cacheKey = Constants.GetUserCultureCacheKey(userName);
        if (!cache.TryGetValue<string>(cacheKey, out var cultureString)) // check for cache entry
        {
            var user = await userManager.FindByNameAsync(userName);
            if (!string.IsNullOrEmpty(user?.Language)
                && Constants.SupportedCultures.Any(k => k.Name == user.Language))
            {
                cultureString = user.Language;
                cache.Set(cacheKey, cultureString);
            }
        }

        if (cultureString is null) // no user-override
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
            var crossInstanceConfiguration = await mediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
                new GetCrossInstanceConfiguration(), cancellationToken);
            cultureString = crossInstanceConfiguration.CrossInstanceConfiguration.CultureName;
        }

        var culture = new CultureInfo(cultureString);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}

