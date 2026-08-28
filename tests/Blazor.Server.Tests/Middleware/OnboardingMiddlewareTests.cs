using System.Security.Claims;
using Blazor.Server.Backend.Middleware;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Sdk.Instance;

namespace Blazor.Server.Tests.Middleware;

public sealed class OnboardingMiddlewareTests
{
    private readonly Guid _instanceId = Guid.NewGuid();

    private static ServiceProvider SetupServiceProvider(HttpContext context, OnboardingState state, bool firstRunWizardEnabled = true)
    {
        var services = new ServiceCollection();

        var instanceInfoProvider = Substitute.For<IInstanceInformationProvider>();
        instanceInfoProvider.Local.Returns(new InstanceInformation { Id = Guid.NewGuid() });

        var onboardingStore = Substitute.For<IOnboardingStateStore>();
        onboardingStore.GetOnboardingStateAsync(Arg.Any<Guid>()).Returns(ci => state);

        var featureManager = Substitute.For<IFeatureManager>();
        featureManager.IsEnabledAsync(Core.Shared.Features.Constants.FirstRunWizardFeatureName).Returns(firstRunWizardEnabled);

        services.AddSingleton(instanceInfoProvider);
        services.AddSingleton(onboardingStore);
        services.AddSingleton(featureManager);

        var serviceProvider = services.BuildServiceProvider();
        context.RequestServices = serviceProvider;

        return serviceProvider;
    }

    [Fact]
    public async Task Should_redirect_when_authenticated_not_completed_flag_set_and_not_on_onboarding_route()
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath: "/");
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };
        await using var _ = SetupServiceProvider(context, state);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        context.Response.Headers.Location.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_call_next_when_user_is_not_authenticated()
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: false, requestPath: "/");
        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("/_framework")]
    [InlineData("/_blazor")]
    [InlineData("/.well-known")]
    public async Task Should_call_next_for_framework_internal_paths(string internalPath)
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath: internalPath);
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };
        await using var _ = SetupServiceProvider(context, state);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Should_call_next_when_onboarding_is_already_completed()
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath: "/");
        var state = new OnboardingState { InstanceId = _instanceId, Completed = true, ShowWizardWhenNotCompleted = true };
        await using var _ = SetupServiceProvider(context, state);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Should_call_next_when_request_is_already_on_onboarding_route()
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath: Shared.Onboarding.Constants.Route);
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };
        await using var _ = SetupServiceProvider(context, state);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// The way through the wizard for a user who has the full access it requires: the first request is
    /// caught, the wizard itself is served, and exiting it clears the way for everything afterwards.
    /// </summary>
    [Fact]
    public async Task Should_let_a_user_with_full_access_through_the_wizard_and_out_of_it()
    {
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };

        var landing = await Invoke("/", state);
        landing.NextCalled.Should().BeFalse();
        landing.Context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        landing.Context.Response.Headers.Location.ToString().Should().Be(Shared.Onboarding.Constants.Route);

        var wizard = await Invoke(Shared.Onboarding.Constants.Route, state);
        wizard.NextCalled.Should().BeTrue();

        // What the user's click on the wizard's exit button leaves behind.
        state.ShowWizardWhenNotCompleted = false;

        var (context, nextCalled) = await Invoke("/", state);
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// A user without the full access the wizard page requires has no way through it and no way out.
    /// These two tests describe that as it is today, so they turn red once it is fixed.
    /// <para>
    /// When the redirect learns to spare such a user, invert them: next is called and the response
    /// stays 200, the same shape as <see cref="Should_call_next_for_framework_internal_paths"/>.
    /// </para>
    /// <para>
    /// They hold the instance state that ships — the <c>FirstRunWizard</c> feature on. An instance
    /// that turns it off never reaches this, see
    /// <see cref="Should_call_next_when_the_first_run_wizard_feature_is_off"/>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Should_redirect_the_access_denied_page_and_so_close_a_loop()
    {
        // Arrange: where a user without full access lands, because the onboarding page requires it.
        // Redirecting it back to the wizard is what makes the browser give up:
        // /onboarding -> /Account/AccessDenied -> /onboarding -> ...
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };

        // Act
        var (context, nextCalled) = await Invoke(CookieAuthenticationDefaults.AccessDeniedPath, state);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().Should().Be(Shared.Onboarding.Constants.Route);
    }

    /// <inheritdoc cref="Should_redirect_the_access_denied_page_and_so_close_a_loop"/>
    [Fact]
    public async Task Should_redirect_the_logout_endpoint_and_so_swallow_the_sign_out()
    {
        // Arrange: the only way out for a user the wizard rejects (mapped in
        // IdentityComponentsEndpointRouteBuilderExtensions.MapAdditionalIdentityEndpoints). Redirected,
        // the sign-out never runs, so clearing the cookie by hand is all that is left.
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };

        // Act
        var (context, nextCalled) = await Invoke("/account/logout", state);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().Should().Be(Shared.Onboarding.Constants.Route);
    }

    /// <summary>
    /// With the <c>FirstRunWizard</c> feature off the middleware lets everything through, whatever the
    /// instance's onboarding state says — including the two paths that close the loop above.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/account/logout")]
    public async Task Should_call_next_when_the_first_run_wizard_feature_is_off(string requestPath)
    {
        // Arrange
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = true };

        // Act
        var (context, nextCalled) = await Invoke(requestPath, state, firstRunWizardEnabled: false);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Should_call_next_when_show_flag_is_false_even_if_not_completed()
    {
        // Arrange
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath: "/");
        var state = new OnboardingState { InstanceId = _instanceId, Completed = false, ShowWizardWhenNotCompleted = false };
        await using var _ = SetupServiceProvider(context, state);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Value.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// Runs one authenticated request against the middleware. Takes the state as an instance so a test
    /// can span several requests and change it in between, the way a user's click does.
    /// </summary>
    private static async Task<(DefaultHttpContext Context, bool NextCalled)> Invoke(string requestPath, OnboardingState state,
        bool firstRunWizardEnabled = true)
    {
        var (context, nextCalled) = BuildHttpContext(authenticated: true, requestPath);
        await using var _ = SetupServiceProvider(context, state, firstRunWizardEnabled);

        var middleware = new OnboardingMiddleware(_ => { nextCalled.Value = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context);

        return (context, nextCalled.Value);
    }

    private static (DefaultHttpContext Context, Flag NextCalled) BuildHttpContext(bool authenticated, string requestPath)
    {
        var context = new DefaultHttpContext();

        if (authenticated)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "user") }, authenticationType: "TestAuth");
            context.User = new ClaimsPrincipal(identity);
        }
        else
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        }

        context.Request.Path = requestPath;
        context.Response.Body = new MemoryStream();

        return (context, new Flag());
    }

    private sealed class Flag
    {
        public bool Value { get; set; }
    }
}
