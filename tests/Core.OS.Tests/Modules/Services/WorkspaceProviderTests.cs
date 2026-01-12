using Core.OS.Modules;
using Core.OS.Modules.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class WorkspaceProviderTests
{
    private readonly IWorkspaceManagement _workspaceManagement = Substitute.For<IWorkspaceManagement>();

    private ServiceProvider CreateServiceProvider() => new ServiceCollection()
        .AddSingleton<WorkspaceProvider<TestBackendModule>>()
        .AddSingleton(_workspaceManagement)
        .AddSingleton(Substitute.For<ILogger<WorkspaceProvider<TestBackendModule>>>())
        .BuildServiceProvider();

    [Fact]
    public void Should_provide_module_home_directory()
    {
        // Arrange
        var moduleHome = "path/to/home/module.id";
        _workspaceManagement.GetHomeDirectory(TestBackendModule.Id).Returns(moduleHome);

        // Act
        var wsProvider = CreateServiceProvider().GetRequiredService<WorkspaceProvider<TestBackendModule>>();

        // Assert
        wsProvider.Home.Should().Be(moduleHome);
    }

    [Fact]
    public void Should_provide_module_cache_directory()
    {
        // Arrange
        var moduleCache = "path/to/cache/module.id";
        _workspaceManagement.GetCacheDirectory(TestBackendModule.Id).Returns(moduleCache);

        // Act
        var wsProvider = CreateServiceProvider().GetRequiredService<WorkspaceProvider<TestBackendModule>>();

        // Assert
        wsProvider.Cache.Should().Be(moduleCache);
    }

    [Fact]
    public void Should_throw_exception_for_unregistered_module()
    {
        // Act + Assert
        Assert.Throws<InvalidOperationException>(()
            => CreateServiceProvider().GetRequiredService<WorkspaceProvider<UnregisteredModule>>());
    }


    // ReSharper disable once MemberCanBePrivate.Global
    public sealed class UnregisteredModule : BackendModule;
}
