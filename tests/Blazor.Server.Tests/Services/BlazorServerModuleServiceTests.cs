using System.Reflection;
using System.Security.Claims;
using Blazor.Server.Backend;
using Blazor.Server.Backend.Services;
using Core.UiHosting;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.Modules;
using Sdk.Instance;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using Xunit;

namespace Blazor.Server.Tests.Services;

public sealed class BlazorServerModuleServiceTests
{
    private readonly IUiHostEnvironment _uiHostEnvironment = Substitute.For<IUiHostEnvironment>();
    private readonly IUiModuleManager _uiModuleManager = Substitute.For<IUiModuleManager>();
    private readonly IInstanceInformationProvider _instanceInformationProviderMock = Substitute.For<IInstanceInformationProvider>();
    private IServiceProvider? _serviceProvider;
    private readonly Assembly _blazorServerAssembly = typeof(BlazorServerClientModule).Assembly;

    private ServiceProvider CreateServiceProvider(Dictionary<string, string?>? settings = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        _instanceInformationProviderMock.SetupGetInstanceInformation(InstanceType.Standalone);

        var services = new ServiceCollection()
            .AddSingleton(_instanceInformationProviderMock)
            .AddSingleton(config)
            .AddLogging()
            .AddSingleton(Substitute.For<ILogger<BlazorServerModuleService>>);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Returns_modules()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();

        // Act
        var actualModules = blazorServerModuleService.GetModules();

        // Assert
        _uiModuleManager.UiModules.Count().Should().Be(actualModules.Count);
    }

    [Fact]
    public void Returns_client_module_if_type_common()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();

        // Act
        var actualModule = blazorServerModuleService.GetModuleByType(typeof(TestSomeEditorClientModule));

        // Assert
        actualModule.Should().NotBeNull();
        actualModule!.ModuleId.Should().Be(TestBackendModule.Id);
    }

    [Fact]
    public void Returns_null_if_type_unknown()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();

        // Act
        var actualModule = blazorServerModuleService.GetModuleByType(typeof(TestBackendModule));

        // Assert
        actualModule.Should().BeNull();
    }

    [Fact]
    public void Returns_module_assemblies()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();

        // Act
        var moduleAssemblies = blazorServerModuleService.GetModuleAssemblies();

        // Assert
        moduleAssemblies.Count().Should().Be(_uiModuleManager.UiModuleAssemblies.Count());
    }

    [Fact]
    public void Returns_all_module_stylesheets()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();

        // Act
        var moduleStylesheets = blazorServerModuleService.GetAllModuleStylesheets().ToArray();

        // Assert
        moduleStylesheets.Should().NotContain(s => s.Contains(_blazorServerAssembly.GetName().Name!));
    }

    [Fact]
    public async Task Check_call_initialize_service_for_client_module()
    {
        // Arrange
        var blazorServerModuleService = SetupTest();
        var admin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimsIdentity.DefaultRoleClaimType, AccessLevel.Full.ToString())
        ]));

        // Act
        Assert.NotNull(_serviceProvider);
        await blazorServerModuleService.InitializeServices(_serviceProvider);
        await blazorServerModuleService.OnUserAuthenticated(_serviceProvider, admin);

        // Assert
        var clientModule = (TestClientModule)_uiModuleManager.UiModules.First(module => module is TestClientModule);
        var user = clientModule.Users.FirstOrDefault();
        user.Should().Be(admin);
    }

    private BlazorServerModuleService SetupTest()
    {
        _serviceProvider = CreateServiceProvider();
        var modules = new List<ClientModule>()
        {
            new TestClientModule(),
            new TestSomeEditorClientModule(),
            new TestOtherEditorClientModule(),
            new TestClientModuleWithEntryPoint()
        };
        var assemblies = new List<Assembly>
        {
            typeof(TestClientModule).Assembly,
            _blazorServerAssembly,
            typeof(TestOtherEditorClientModule).Assembly
        };

        _uiModuleManager.UiModules.Returns(modules);
        _uiModuleManager.UiModuleAssemblies.Returns(assemblies);

        return new BlazorServerModuleService(_uiModuleManager, _uiHostEnvironment);
    }
}
