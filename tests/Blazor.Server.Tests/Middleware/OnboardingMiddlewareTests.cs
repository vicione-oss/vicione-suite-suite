using System.Security.Claims;
using AwesomeAssertions;
using Blazor.Server.Backend.Middleware;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Blazor.Server.Tests.Middleware;

public sealed class OnboardingMiddlewareTests
{
    private readonly Guid _instanceId = Guid.NewGuid();

    private static ServiceProvider SetupServiceProvider(HttpContext context, OnboardingState state)
    {
        var services = new ServiceCollection();

        var instanceInfoProvider = Substitute.For<IInstanceInformationProvider>();
        instanceInfoProvider.Local.Returns(new InstanceInformation { Id = Guid.NewGuid() });

        var onboardingStore = Substitute.For<IOnboardingStateStore>();
        onboardingStore.GetOnboardingStateAsync(Arg.Any<Guid>()).Returns(ci => state);

        services.AddSingleton(instanceInfoProvider);
        services.AddSingleton(onboardingStore);

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
