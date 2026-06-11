using Core.OS.DbContext;
using Core.Shared.Extensions;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Core.OS.UserManagement.Configuration;

public sealed class DynamicExternalIdProviderOptions(
    IServiceProvider serviceProvider,
    IConfiguration configuration) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public const string OptionsName = OpenIdConnectDefaults.AuthenticationScheme;

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name != OptionsName) return;

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var externalIdProvider = dbContext.ExternalIdProviders.FirstOrDefault()
                                 ?? ReadFromConfiguration();

        if (externalIdProvider != null)
            ApplyOptions(options, externalIdProvider);
        else
            MarkAsUnconfigured(options);
    }

    /// <summary>
    /// When using <see cref="Microsoft.Extensions.DependencyInjection.OpenIdConnectExtensions.AddOpenIdConnect"/>,
    /// it strictly validates the <see cref="OpenIdConnectOptions"/> instance and requires, for example, a client id.
    /// As our configuration of the external id provider is optional, we cannot provide that information if none is
    /// configured. Hence, we use sentinel information to mark that unconfigured case.
    /// </summary>
    private static void MarkAsUnconfigured(OpenIdConnectOptions options)
    {
        options.Authority = "https://unconfigured.local";
        options.ClientId = Constants.UnconfiguredClient;

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            // context.HandleResponse() tells the framework we handled it and to stop processing
            context.HandleResponse();

            // Return a clean error instead of attempting a network request
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("OIDC is not configured on this instance.");
        };
    }

    private ExternalIdProvider? ReadFromConfiguration()
    {
        var fromConfig = configuration.GetExternalIdProviderOptions()?.Providers.FirstOrDefault();
        if (fromConfig is null)
            return null;

        return new()
        {
            Authority = fromConfig.Authority,
            ClientId = fromConfig.ClientId,
            ClientSecret = fromConfig.ClientSecret,
            Name = fromConfig.Name,
            Id = Guid.Empty
        };
    }

    private static void ApplyOptions(OpenIdConnectOptions connectOptions, ExternalIdProvider externalIdProvider)
    {
        connectOptions.Authority = externalIdProvider.Authority;
        connectOptions.ClientId = externalIdProvider.ClientId;
        connectOptions.ClientSecret = externalIdProvider.ClientSecret;
        connectOptions.UsePkce = true;

        connectOptions.ResponseType = OpenIdConnectResponseType.Code;
        connectOptions.SaveTokens = false;

        // IMPORTANT: Set to false ONLY for local HTTP development. MUST be true in production.
        connectOptions.RequireHttpsMetadata = true;

        connectOptions.Scope.Clear();
        connectOptions.Scope.Add(OpenIdConnectScope.OpenId); // Required for OIDC
        connectOptions.Scope.Add(OpenIdConnectScope.Profile); // Request basic user profile claims
        connectOptions.Scope.Add(OpenIdConnectScope.Email); // Request email claim

        connectOptions.CallbackPath = "/signin-oidc";
        connectOptions.SignedOutCallbackPath = "/signout-callback-oidc";

        // try to read extended user information as not every IdP will send it per default.
        connectOptions.GetClaimsFromUserInfoEndpoint = true;
        connectOptions.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");

        // sticking closer to the actual OpenID Connect and JWT specifications
        connectOptions.MapInboundClaims = false;
    }
}
