using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.Shared.Instance.Contracts;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class LocalInstanceInformationProviderTests
{
    private readonly IModuleHost _moduleHost = Substitute.For<IModuleHost>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    private ServiceProvider SetupServiceProvider(Guid? instanceId = null)
    {
        var options = Substitute.For<IOptions<InstanceOptions>>();

        options.Value.Returns(new InstanceOptions
        {
            HomeDirectory = "app",
            CacheDirectory = "cache",
            BackupDirectory = "backup",
            Type = InstanceType.Standalone
        });

        _fileSystem.SetupInstanceIdFile(instanceId ?? Guid.NewGuid(), options.Value.HomeDirectory);

        return new ServiceCollection()
            .AddSingleton(_fileSystem)
            .AddSingleton(options)
            .AddSingleton(_moduleHost)
            .AddSingleton<ILocalInstanceInformationProvider, LocalInstanceInformationProvider>()
            .BuildServiceProvider();
    }

    public class Local : LocalInstanceInformationProviderTests
    {
        [Fact]
        public void Should_throw_if_local_instance_is_not_initialized()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider(Guid.NewGuid());
            var provider = serviceProvider.GetRequiredService<ILocalInstanceInformationProvider>();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => provider.Local);
        }

        [Fact]
        public void Should_return_instance_information_once_initialized()
        {
            // Arrange
            var instanceInfo = new InstanceInformation
            {
                Id = Guid.NewGuid(),
            };
            using var serviceProvider = SetupServiceProvider(instanceInfo.Id);
            var provider = serviceProvider.GetRequiredService<ILocalInstanceInformationProvider>();
            provider.UpdateLocal(instanceInfo);

            // Act +Assert
            provider.Local.Should().BeEquivalentTo(instanceInfo);
        }
    }

    public class ReadLocalInstanceId : LocalInstanceInformationProviderTests
    {
        [Fact]
        public void Should_return_local_instance_id_from_file()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            using var serviceProvider = SetupServiceProvider(instanceId);
            var provider = serviceProvider.GetRequiredService<ILocalInstanceInformationProvider>();

            // Act
            var result = provider.ReadLocalInstanceId();

            // Assert
            Assert.Equal(instanceId, result);
        }

        [Fact]
        public void Should_read_instance_id_only_once_from_file()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var provider = serviceProvider.GetRequiredService<ILocalInstanceInformationProvider>();

            // Act
            _ = provider.ReadLocalInstanceId();
            _ = provider.ReadLocalInstanceId();
            _ = provider.ReadLocalInstanceId();

            // Assert
            _fileSystem.File.Received(1).ReadAllText(Arg.Any<string>());
        }
    }
}
