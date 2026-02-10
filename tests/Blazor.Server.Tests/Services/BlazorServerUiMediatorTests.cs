using System.Security.Claims;
using AwesomeAssertions;
using Blazor.Server.Backend.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Server.Tests.Services;

public abstract class BlazorServerUiMediatorTests
{
    private readonly ISuiteMediator _suiteMediator = Substitute.For<ISuiteMediator>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly IUiEventSubscriptionHolder<FooEvent> _publisher = Substitute.For<IUiEventSubscriptionHolder<FooEvent>>();

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_publisher)
            .AddSingleton(_suiteMediator)
            .AddSingleton(Substitute.For<ILogger<BlazorServerUiMediator>>())
            .AddScoped(s => _httpContextAccessor)
            .AddSingleton<BlazorServerUiMediator>()
            .BuildServiceProvider();

    public sealed class Send : BlazorServerUiMediatorTests
    {
        [Fact]
        public async Task Should_send_command()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();
            var command = new BarTestCommand(Guid.NewGuid())
            {
                InstanceId = Guid.NewGuid(),
            };
            // Act
            await uiMediator.Send(command);

            // Assert
            await _suiteMediator.Received().Send(command, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_send_instance_dependent_command()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();
            var command = new FooTestCommand(Guid.NewGuid());
            // Act
            await uiMediator.Send(command, command.InstanceId);

            // Assert
            await _suiteMediator.Received().Send(command, command.InstanceId, Arg.Any<CancellationToken>());
        }
    }

    public sealed class Request : BlazorServerUiMediatorTests
    {
        [Fact]
        public async Task Should_return_response()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();
            var fooTestRequest = new FooTestRequest(Guid.NewGuid());
            var fooTestResponse = new FooTestResponse()
            {
                ClusterJson = @" [ {""name"": ""John Doe"", ""occupation"": ""gardener""}, 
                               {""name"": ""Peter Novak"", ""occupation"": ""driver""} ]"
            };

            _suiteMediator.SetupRequest<FooTestRequest, FooTestResponse>(null, fooTestResponse);

            // Act
            var responseMessage = await uiMediator.Request<FooTestRequest, FooTestResponse>(fooTestRequest);

            // Assert
            responseMessage.ClusterJson.Should().Be(fooTestResponse.ClusterJson);
        }
    }

    public sealed class Register : BlazorServerUiMediatorTests
    {
        [Fact]
        public void Should_connect_event_consumer()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();

            var fooHandler = new FooService();

            // Act
            _ = uiMediator.Register(fooHandler);

            // Assert
            _publisher.Received().Connect(fooHandler);
        }

        [Fact]
        public void Should_connect_event_consumer_with_identity()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();

            _httpContextAccessor.HttpContext = BuildHttpContext(true, "/some");

            var fooHandler = new FooService();

            // Act
            _ = uiMediator.Register(fooHandler);

            // Assert
            _publisher.Received().Connect(fooHandler, _httpContextAccessor.HttpContext.User.Identity);
        }

        [Fact]
        public void Should_init_scoped_identity_only_once()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var uiMediator = serviceProvider.GetRequiredService<BlazorServerUiMediator>();

            _httpContextAccessor.HttpContext = BuildHttpContext(true, "/some");

            var fooHandler = new FooService();

            // Act
            _ = uiMediator.Register(fooHandler);
            _ = uiMediator.Register(fooHandler);

            // Assert
            _ = _httpContextAccessor.Received(1).HttpContext;
        }

        private static DefaultHttpContext BuildHttpContext(bool authenticated, string requestPath)
        {
            var context = new DefaultHttpContext();

            if (authenticated)
            {
                var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], authenticationType: "TestAuth");
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
            }

            context.Request.Path = requestPath;
            context.Response.Body = new MemoryStream();

            var authFeature = Substitute.For<IHttpAuthenticationFeature>();
            authFeature.User = context.User;
            context.Features.Set(authFeature);

            return context;
        }
    }

    public record FooTestCommand(Guid InstanceId) : IInstanceDependentCommand
    {
        public Guid CorrelationId { get; init; } = InstanceId;
    }

    public record BarTestCommand(Guid BarId) : ICommand
    {
        public Guid InstanceId { get; set; }

        public Guid CorrelationId { get; init; } = BarId;
    }

    public record FooTestRequest(Guid RequestId) : IRequest<FooTestResponse>;

    public record FooTestResponse : IResponse
    {
        public ErrorInfo? RequestError { get; init; }
        public string? ClusterJson { get; init; }
    }

    public sealed record FooEvent : IEvent;

    public class FooService : IEventConsumer<FooEvent>
    {
        public Task Consume(ClientContext<FooEvent> context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
