using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Net;
using System.Text;
using Core.Module.JFrog;
using Core.Module.Options;
using Core.Module.Tests;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.ArtifactApi;
using Sdk.Testing;
using Xunit;
using NSubstitute.ExceptionExtensions;

/// <summary>
/// https://jfrog.com/help/r/jfrog-rest-apis/artifactory-query-language
/// </summary>
public class JFrogArtifactQueryApiTests
{
    private const string TestApiAddress = "https://ifm.jfrog.io/artifactory";

    public sealed class CreateQueryBuilder : JFrogArtifactQueryApiTests
    {
        [Fact]
        public void Returns_a_jfrog_artifact_query_builder()
        {
            // Arrange
            using var client = new HttpClient();
            var api = CreateApi(client: client);

            // Act
            var builder = api.CreateQueryBuilder();

            // Assert
            builder.Should().BeOfType<JFrogArtifactQueryBuilder>();
        }
    }

    public sealed class CreateDownloadUri : JFrogArtifactQueryApiTests
    {
        [Fact]
        public void Constructs_uri_using_artifact_path_and_repo()
        {
            // Arrange
            using var client = new HttpClient();
            var artifact = Substitute.For<IArtifactItem>();
            artifact.Path.Returns("path/to");
            artifact.Name.Returns("file.txt");
            artifact.Repo.Returns("custom-repo");

            var api = CreateApi(client: client);

            // Act
            var result = api.CreateDownloadUri(artifact);

            // Assert
            result.Should().Be(new Uri($"{TestApiAddress}/custom-repo/path/to/file.txt"));
        }

        [Fact]
        public void Uses_default_repo_if_not_set_on_artifact()
        {
            // Arrange
            using var client = new HttpClient();
            var artifact = Substitute.For<IArtifactItem>();
            artifact.Path.Returns("x/y");
            artifact.Name.Returns("z.txt");
            artifact.Repo.Returns(string.Empty);

            var api = CreateApi(client: client);

            // Act
            var result = api.CreateDownloadUri(artifact);

            // Assert
            result.Should().Be(new Uri($"{TestApiAddress}/vicione-suite/x/y/z.txt"));
        }
    }

    public sealed class ExecuteQuery : JFrogArtifactQueryApiTests
    {
        [Fact]
        public async Task Returns_result_when_http_response_is_successful()
        {
            // Arrange
            using var handler = new FakeHttpHandler(HttpStatusCode.OK, """{ "results": [] }""", "application/json");
            using var client = new HttpClient(handler) { BaseAddress = new Uri(TestApiAddress) };

            var api = CreateApi(client: client);

            // Act
            var result = await api.ExecuteQuery("items.find({})", CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void Throws_exception_on_http_errors()
        {
            // Arrange
            using var handler = new FakeHttpHandler(throwOnSend: true);
            using var client = new HttpClient(handler) { BaseAddress = new Uri(TestApiAddress) };

            var api = CreateApi(client: client);

            // Act
            var action = () => api.ExecuteQuery("items.find({})", CancellationToken.None);

            // Assert
            action().ThrowsAsync<HttpRequestException>();
        }
    }

    public sealed class DownloadToFile : JFrogArtifactQueryApiTests
    {
        [Fact]
        public async Task Writes_stream_to_file_system()
        {
            // Arrange
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello world"));
            var artifact = Substitute.For<IArtifactItem>();
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("target");

            using var fileStream = new MemoryStream();

            using var handler = new FakeHttpHandler(contentStream: stream);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://host/") };
            var api = CreateApi(client, fileSystem);

            // Act
            await api.DownloadToFile(artifact, "target/path.txt", CancellationToken.None);

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

            var artifact = Substitute.For<IArtifactItem>();
            using var tempDirectory = new TemporaryDirectory();
            var fileSystem = new FileSystem();

            using var handler = new FakeHttpHandler(contentStream: zipStream);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://host/") };

            var api = CreateApi(client, fileSystem);

            // Act
            await api.DownloadAndExtract(artifact, tempDirectory.Path, CancellationToken.None);

            // Assert
            fileSystem.Directory.Exists(tempDirectory.Path).Should().BeTrue();
            fileSystem.File.Exists(fileSystem.Path.Combine(tempDirectory.Path, "test.txt")).Should().BeTrue();
        }
    }

    public sealed class ExecuteQueryRaw : JFrogArtifactQueryApiTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Returns_raw_string_when_successful()
        {
            // Arrange
            using var client = new HttpClient();
            var api = CreateApi(client: client, fs: new FileSystem());

            var builder = api.CreateQueryBuilder();
            builder.AndPathMatches("fbs/*");

            // Act
            var result = await api.ExecuteQueryRaw(builder.BuildQueryString());

            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Throws_when_http_fails()
        {
            // Arrange
            using var handler = new FakeHttpHandler(HttpStatusCode.BadRequest, "error", "text/plain");
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://host/") };
            var api = CreateApi(client: client);

            // Act
            var act = async () => await api.ExecuteQueryRaw("invalid query");

            // Assert
            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }

    private static JFrogArtifactQueryApi CreateApi(
        HttpClient client,
        IFileSystem? fs = null,
        ModuleApiOptions? opts = null)
    {
        fs ??= Substitute.For<IFileSystem>();
        opts ??= SystemTestSettings.ModuleApiOptions;

        var options = Substitute.For<IOptions<ModuleApiOptions>>();
        options.Value.Returns(opts);

        return new JFrogArtifactQueryApi(fs, client, options);
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
