using System.Globalization;
using System.Reflection;
using Blazor.Wasm.Client.Infrastructure.Modules;
using Blazor.Wasm.Client.Services;
using Core.Shared.Modules;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TestModule.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Services;

public class ClientModuleLoaderTests
{
    [Fact]
    public async Task Load_assemblies_should_do_nothing_if_http_response_is_empty()
    {
        // Arrange
        var httpMock = Substitute.For<IBackendModuleHttpClient>();
        httpMock.LoadClientModulesArchive(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await ClientModuleLoader.LoadClientAssemblies(httpMock);

        // Assert      
        Assert.Null(result.Error);
        Assert.Equal(0, result.ZipLength);
        Assert.Empty(result.LoadedDlls);
        Assert.Empty(result.LoadErrors);
    }

    [Fact]
    public async Task Load_assemblies_should_load_extracted_dlls_into_context()
    {
        // Arrange
        var moduleAssembly = Assembly.GetAssembly(typeof(TestClientModule));
        var moduleFiles = new Dictionary<string, string> { { $"{moduleAssembly!.GetName().Name}X.dll", moduleAssembly.Location } };
        var moduleArchive = await ModuleZip.CreateArchive(moduleFiles);

        var httpMock = Substitute.For<IBackendModuleHttpClient>();
        httpMock.LoadClientModulesArchive(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(moduleArchive);

        // Act
        var result = await ClientModuleLoader.LoadClientAssemblies(httpMock);

        // Assert      
        Assert.Null(result.Error);
        Assert.Empty(result.LoadErrors);
        Assert.Single(result.LoadedDlls);
        Assert.Equal(moduleArchive.Length, result.ZipLength);
    }


    [Fact]
    public async Task Load_assembly_should_return_error_info_on_failure()
    {
        // Arrange
        var httpMock = Substitute.For<IBackendModuleHttpClient>();
        httpMock.LoadClientModuleResourcesArchive(Arg.Any<CultureInfo>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());

        // Act
        var result = await ClientModuleLoader.LoadClientResourceAssemblies(httpMock, CultureInfo.CurrentCulture);

        // Assert      
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Load_assembly_resources_should_do_nothing_if_http_response_is_empty()
    {
        // Arrange
        var httpMock = Substitute.For<IBackendModuleHttpClient>();
        httpMock.LoadClientModuleResourcesArchive(Arg.Any<CultureInfo>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await ClientModuleLoader.LoadClientResourceAssemblies(httpMock, CultureInfo.CurrentCulture);

        // Assert      
        Assert.Null(result.Error);
        Assert.Equal(0, result.ZipLength);
        Assert.Empty(result.LoadedDlls);
        Assert.Empty(result.LoadErrors);
    }
}
