using Blazor.Wasm.Client.Infrastructure.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.HealthChecks;

public sealed class InstanceHealthServiceTests : IDisposable
{
    private readonly IUiMediator _clientMediatorMock = Substitute.For<IUiMediator>();
    private readonly ILogger<InstanceHealthService> _loggerMock = Substitute.For<ILogger<InstanceHealthService>>();
    private readonly InstanceHealthService _service;

    public InstanceHealthServiceTests()
    {
        _service = new InstanceHealthService(_clientMediatorMock, _loggerMock);
    }

    public void Dispose() => _service.Dispose();

    [Fact(Skip = "To be implemented...")]
    public void Test_the_rest()
    {
        // Arrange

        // Act

        // Assert
    }
}
