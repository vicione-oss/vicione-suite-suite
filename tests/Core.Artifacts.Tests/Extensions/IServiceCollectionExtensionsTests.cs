using AwesomeAssertions;
using Core.Artifacts.Extensions;
using Core.Artifacts.JFrog;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Artifacts;
using Xunit;

namespace Core.Artifacts.Tests.Extensions;

public class IServiceCollectionExtensionsTests
{
    private sealed class StubOptionsProvider : IArtifactRepositoryOptionsProvider
    {
        public ArtifactRepositoryOptions GetOptions() => new();
    }

    public sealed class AddArtifactRepositoryWithType
    {
        [Fact]
        public void Should_register_IArtifactRepository()
        {
            var services = new ServiceCollection();

            services.AddArtifactRepository<StubOptionsProvider>();

            services.Should().ContainSingle(d => d.ServiceType == typeof(IArtifactRepository));
        }

        [Fact]
        public void Should_register_IArtifactRepository_as_JFrogArtifactRepository()
        {
            var services = new ServiceCollection();

            services.AddArtifactRepository<StubOptionsProvider>();

            services.Should().ContainSingle(d =>
                d.ServiceType == typeof(IArtifactRepository) &&
                d.ImplementationType == typeof(JFrogArtifactRepository));
        }

        [Fact]
        public void Should_register_IArtifactRepositoryOptionsProvider()
        {
            var services = new ServiceCollection();

            services.AddArtifactRepository<StubOptionsProvider>();

            services.Should().ContainSingle(d =>
                d.ServiceType == typeof(IArtifactRepositoryOptionsProvider) &&
                d.ImplementationType == typeof(StubOptionsProvider));
        }

        [Fact]
        public void Should_return_the_service_collection()
        {
            var services = new ServiceCollection();

            var result = services.AddArtifactRepository<StubOptionsProvider>();

            result.Should().BeSameAs(services);
        }
    }

    public sealed class AddArtifactRepositoryWithFactory
    {
        [Fact]
        public void Should_register_IArtifactRepository()
        {
            var services = new ServiceCollection();

            services.AddArtifactRepository(_ => new StubOptionsProvider());

            services.Should().ContainSingle(d => d.ServiceType == typeof(IArtifactRepository));
        }

        [Fact]
        public void Should_register_IArtifactRepository_as_JFrogArtifactRepository()
        {
            var services = new ServiceCollection();

            services.AddArtifactRepository(_ => new StubOptionsProvider());

            services.Should().ContainSingle(d =>
                d.ServiceType == typeof(IArtifactRepository) &&
                d.ImplementationType == typeof(JFrogArtifactRepository));
        }

        [Fact]
        public void Should_register_IArtifactRepositoryOptionsProvider_using_factory()
        {
            var services = new ServiceCollection();
            var instance = new StubOptionsProvider();

            services.AddArtifactRepository(_ => instance);
            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IArtifactRepositoryOptionsProvider>().Should().BeSameAs(instance);
        }

        [Fact]
        public void Should_return_the_service_collection()
        {
            var services = new ServiceCollection();

            var result = services.AddArtifactRepository(_ => new StubOptionsProvider());

            result.Should().BeSameAs(services);
        }
    }
}
