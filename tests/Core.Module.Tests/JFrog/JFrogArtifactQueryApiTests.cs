using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Core.Module.JFrog;
using Core.Module.Options;
using Core.Module.Tests;
using Core.Tests.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Testing;
using Xunit;

/// <summary>
/// https://jfrog.com/help/r/jfrog-rest-apis/artifactory-query-language
/// </summary>
public class JFrogArtifactQueryApiTests
{
    private const string TestApiAddress = "https://ifm.jfrog.io/artifactory";
    private readonly TestHttpClientFactory _httpClientFactory = new();

    public sealed class CreateQueryBuilder : JFrogArtifactQueryApiTests
    {
        [Fact]
        public void Returns_a_jfrog_artifact_query_builder()
        {
            // Arrange
            var repository = CreateRepository(_httpClientFactory);

            // Act
            var builder = repository.CreateQueryBuilder();

            // Assert
            builder.Should().BeOfType<JFrogArtifactQueryBuilder>();
        }
    }

    public sealed class GetDownloadUri : JFrogArtifactQueryApiTests
    {
        [Fact]
        public void Constructs_uri_using_artifact_path_and_repo()
        {
            // Arrange
            var artifact = Substitute.For<IArtifact>();
            artifact.Path.Returns("path/to");
            artifact.Name.Returns("file.txt");
            artifact.Repository.Returns("custom-repo");

            var repository = CreateRepository(_httpClientFactory);

            // Act
            var result = repository.GetDownloadUri(artifact);

            // Assert
            result.Should().Be(new Uri($"{TestApiAddress}/custom-repo/path/to/file.txt"));
        }

        [Fact]
        public void Uses_default_repo_if_not_set_on_artifact()
        {
            // Arrange
            var artifact = Substitute.For<IArtifact>();
            artifact.Path.Returns("x/y");
            artifact.Name.Returns("z.txt");
            artifact.Repository.Returns(string.Empty);

            var repository = CreateRepository(_httpClientFactory);

            // Act
            var result = repository.GetDownloadUri(artifact);

            // Assert
            result.Should().Be(new Uri($"{TestApiAddress}/vicione-suite/x/y/z.txt"));
        }
    }

    public sealed class Query : JFrogArtifactQueryApiTests
    {
        [Fact]
        public async Task Returns_result_when_http_response_is_successful()
        {
            // Arrange
            _httpClientFactory.MessageHandlerSetup = () => new FakeHttpHandler(HttpStatusCode.OK, """{ "results": [] }""", "application/json");
            _httpClientFactory.BaseAddress = new Uri(TestApiAddress);

            var repository = CreateRepository(_httpClientFactory);

            // Act
            var result = await repository.Query("items.find({})", CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_catch_exception_on_http_errors()
        {
            // Arrange
            _httpClientFactory.MessageHandlerSetup = () => new FakeHttpHandler(throwOnSend: true);
            _httpClientFactory.BaseAddress = new Uri(TestApiAddress);

            var repository = CreateRepository(_httpClientFactory);

            // Act
            var result = await repository.Query("items.find({})", CancellationToken.None);

            // Assert
            result.Errors.Should().NotBeEmpty();
        }
    }

    public sealed class DownloadToFile : JFrogArtifactQueryApiTests
    {
        [Fact]
        public async Task Writes_stream_to_file_system()
        {
            // Arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello world"));
            _httpClientFactory.MessageHandlerSetup = () => new FakeHttpHandler(contentStream: stream);

            var artifact = Substitute.For<IArtifact>();
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("target");

            var repository = CreateRepository(_httpClientFactory, fileSystem);

            // Act
            await repository.DownloadToFile(artifact, "target/path.txt", CancellationToken.None);

            // Assert
            fileSystem.File.Exists("target/path.txt").Should().BeTrue();
            (await fileSystem.File.ReadAllTextAsync("target/path.txt")).Should().Be("Hello world");
        }
    }

    public sealed class DownloadAndExtract : JFrogArtifactQueryApiTests
    {
        [Fact]
        public async Task Extracts_zip_file_into_target_folder()
        {
            // Arrange
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("test.txt");
                using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("content");
            }
            zipStream.Position = 0;

            var artifact = Substitute.For<IArtifact>();
            using var tempDirectory = new TemporaryDirectory();
            var fileSystem = new FileSystem();

            _httpClientFactory.MessageHandlerSetup = () => new FakeHttpHandler(contentStream: zipStream);
            _httpClientFactory.BaseAddress = new Uri("https://host/");

            var repository = CreateRepository(_httpClientFactory, fileSystem);

            // Act
            await repository.DownloadAndExtract(artifact, tempDirectory.Path, CancellationToken.None);

            // Assert
            fileSystem.Directory.Exists(tempDirectory.Path).Should().BeTrue();
            fileSystem.File.Exists(fileSystem.Path.Combine(tempDirectory.Path, "test.txt")).Should().BeTrue();
        }
    }

    public sealed class QueryRaw : JFrogArtifactQueryApiTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Returns_raw_string_when_successful()
        {
            // Arrange
            var repository = CreateRepository(_httpClientFactory, fs: new FileSystem());

            var builder = repository.CreateQueryBuilder();
            builder.AndPathMatches("modules/*");

            // Act
            var result = await repository.QueryRaw(builder.Build());

            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Should_catch_errors_on_source_http_failures()
        {
            // Arrange
            _httpClientFactory.MessageHandlerSetup = () => new FakeHttpHandler(HttpStatusCode.BadRequest, "error", "text/plain");
            _httpClientFactory.BaseAddress = new Uri("https://host/");

            var repository = CreateRepository(_httpClientFactory);

            // Act
            var result = await repository.QueryRaw("invalid query");

            // Assert
            result.Should().BeEmpty();
        }
    }

    public sealed class Limit : JFrogArtifactQueryApiTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_limit_the_result_count()
        {
            // Arrange            
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository
                .CreateQueryBuilder()
                .AndPathMatches("enginehost")
                .Limit(2)
                .Build();

            // Act
            var result = await artifactRepository.Query(query);

            // Assert
            result.Artifacts.Should().HaveCount(2);
            result.Ranges.Should().NotBeEmpty();
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_use_offset_for_result_start()
        {
            // Arrange
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository.CreateQueryBuilder()
                .AndPathMatches("enginehost")
                .Limit(2, 5)
                .Build();

            // Act
            var result = await artifactRepository.Query(query);

            // Assert
            result.Artifacts.Should().HaveCount(2);
            result.Ranges.Should().NotBeEmpty();
            result.Ranges.First().StartPosition.Should().Be(5);
        }
    }

    public sealed class FilterBy : JFrogArtifactQueryApiTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_filter_by_folder_returns_only_folders()
        {
            // Arrange
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository
                .CreateQueryBuilder()
                .AndPathMatches("fbs")
                .FilterBy(ArtifactKind.Folder)
                .Build();

            // Act
            var result = await artifactRepository.Query(query);

            // Assert
            result.Artifacts.Should().AllSatisfy(k => k.Kind.Should().Be(ArtifactKind.Folder));
        }
    }

    public sealed class QuerySuiteArtifacts : JFrogArtifactQueryApiTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_return_all_suite_package_artifacts()
        {
            // Arrange
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository
                .CreateQueryBuilder()
                .AndPathMatches("suites")
                .AndNameMatches("vicione-suite_*")
                .Build();

            // Act
            var result = await artifactRepository.Query(query);

            // Assert
            result.Artifacts.Should().AllSatisfy(k => k.Name.Should().StartWith("vicione-suite_"));
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_return_all_suite_artifacts_raw()
        {
            // Arrange            
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository
                .CreateQueryBuilder()
                .AndPathMatches("suites")
                .Build();

            // Act
            var result = await artifactRepository.QueryRaw(query);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_return_all_suite_signature_artifacts()
        {
            // Arrange            
            var artifactRepository = CreateRepository(_httpClientFactory);
            var query = artifactRepository
                .CreateQueryBuilder()
                .AndPathMatches("suites")
                .AndNameMatches("*.minisig")
                .Build();

            // Act
            var result = await artifactRepository.Query(query);

            // Assert
            result.Artifacts.Should().AllSatisfy(k => k.Name.Should().StartWith("vicione-suite_"));
        }


        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_download_suite_package()
        {
            // Arrange
            var suiteVersion = "vicione-suite_1.1.0~1942353_arm64.deb";
            var repository = CreateRepository(_httpClientFactory, fs: new FileSystem());

            // Act
            var query = repository
                .CreateQueryBuilder()
                .AndPathMatches("suites")
                .AndNameMatches(suiteVersion)
                .Build();

            var artifact = (await repository.Query(query)).Artifacts.First();

            await repository.DownloadToFile(artifact, $"C:\\Users\\deneuhjo\\Downloads\\{suiteVersion}", CancellationToken.None);
        }
    }

    private static JFrogArtifactRepository CreateRepository(
        IHttpClientFactory httpClientFactory,
        IFileSystem? fs = null,
        ArtifactRepositoryOptions? opts = null)
    {
        fs ??= Substitute.For<IFileSystem>();
        opts ??= SystemTestSettings.ArtifactApiOptions;

        var options = Substitute.For<IOptions<ArtifactRepositoryOptions>>();
        options.Value.Returns(opts);

        return new JFrogArtifactRepository(fs, httpClientFactory, options, Substitute.For<ILogger<JFrogArtifactRepository>>());
    }

    /// <summary>
    /// We need this implementation to support using statements when creating
    /// client for multiple sources
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        // Message handler get's disposed when client get's disposed
        public Func<HttpMessageHandler>? MessageHandlerSetup { get; set; }

        public Uri? BaseAddress { get; set; }

        public HttpClient CreateClient(string name)
        {
            var client = MessageHandlerSetup is not null ? new HttpClient(MessageHandlerSetup()) : new HttpClient();

            if (BaseAddress is not null)
            {
                client.BaseAddress = BaseAddress;
            }

            return client;
        }
    }

    private class FakeHttpHandler(HttpStatusCode code = HttpStatusCode.OK, string? content = null, string mediaType = "application/json", bool throwOnSend = false, Stream? contentStream = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throwOnSend)
                throw new HttpRequestException("Simulated network failure");

            var response = new HttpResponseMessage(code);

            if (contentStream != null)
            {
                response.Content = new StreamContent(contentStream);
            }
            else if (content != null)
            {
                response.Content = new StringContent(content, Encoding.UTF8, mediaType);
            }

            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                contentStream?.Dispose();

            base.Dispose(disposing);
        }
    }
}
