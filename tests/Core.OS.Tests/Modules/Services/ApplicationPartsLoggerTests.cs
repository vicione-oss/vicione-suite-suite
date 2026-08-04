using Core.OS.Modules.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Logging;

namespace Core.OS.Tests.Modules.Services;

public class ApplicationPartsLoggerTests
{
    [Fact]
    public async Task Should_start_without_error()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var loggerMock = Substitute.For<ILogger<ApplicationPartsLogger>>();
        var appManager = new ApplicationPartManager();
        var envMock = Substitute.For<IWebHostEnvironment>();
        var appLogger = new ApplicationPartsLogger(loggerMock, appManager, envMock);

        envMock.EnvironmentName
            .Returns("production");

        // Act + Assert
        await appLogger.StartAsync(cancellationToken);
    }
}
