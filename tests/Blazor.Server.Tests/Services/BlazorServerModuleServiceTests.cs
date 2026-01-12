using System.Reflection;
using System.Security.Claims;
using AwesomeAssertions;
using Blazor.Server.Backend;
using Blazor.Server.Backend.Services;
using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Core.Shared.Instance.Services;
using Core.UiHosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.Infrastructure;
using Sdk.Client.Modules;
using Sdk.Instance;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class BlazorServerModuleServiceTests
{
    private readonly IUiHostEnvironment _uiHostEnvironment = Substitute.For<IUiHostEnvironment>();
    private readonly IUiModuleManager _uiModuleManager = Substitute.For<IUiModuleManager>();
    private readonly IUiMediator _uiMediator = Substitute.For<IUiMediator>();
    private readonly IClientTimeProvider _timeProvider = Substitute.For<IClientTimeProvider>();
    private readonly IOnboardingStateStore _onboardingStore = Substitute.For<IOnboardingStateStore>();
    private readonly IInstanceInformationProvider _instanceInformationProviderMock = Substitute.For<IInstanceInformationProvider>();
    private readonly Assembly _blazorServerAssembly = typeof(BlazorServerBackendModule).Assembly;

    private ServiceProvider SetupServiceProvider(Dictionary<string, string?>? settings = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        _instanceInformationProviderMock.SetupGetInstanceInformation(InstanceType.Standalone);

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
            typeof(TestOtherEditorClientModule).Assembly
        };

        _uiModuleManager.UiModules.Returns(modules);
        _uiModuleManager.UiModuleAssemblies.Returns(assemblies);

        var services = new ServiceCollection()
            .AddBlazorShared()
            .AddSingleton(_uiMediator)
            .AddSingleton(_onboardingStore)
            .AddSingleton(_timeProvider)
            .AddSingleton(_instanceInformationProviderMock)
            .AddSingleton(_uiModuleManager)
            .AddSingleton(_uiHostEnvironment)
            .AddSingleton(config)
            .AddLogging()
            .AddSingleton<BlazorServerModuleService>();

        return services.BuildServiceProvider();
    }

    public sealed class GetModules : BlazorServerModuleServiceTests
    {
        [Fact]
        public void Should_returns_modules()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();

            // Act
            var actualModules = blazorServerModuleService.GetModules();

            // Assert
            _uiModuleManager.UiModules.Count().Should().Be(actualModules.Count);
        }
    }

    public sealed class GetModuleByType : BlazorServerModuleServiceTests
    {
        [Fact]
        public void Should_return_client_module_if_type_is_known()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();

            // Act
            var actualModule = blazorServerModuleService.GetModuleByType(typeof(TestSomeEditorClientModule));

            // Assert
            actualModule.Should().BeOfType<TestSomeEditorClientModule>();
            actualModule!.ModuleId.Should().Be(TestSomeEditorClientModule.Id);
        }

        [Fact]
        public void Should_return_null_if_type_is_unknown()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();

            // Act
            var actualModule = blazorServerModuleService.GetModuleByType(typeof(TestBackendModule));

            // Assert
            actualModule.Should().BeNull();
        }
    }

    public sealed class GetModuleAssemblies : BlazorServerModuleServiceTests
    {
        [Fact]
        public void Should_returns_module_assemblies()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();

            // Act
            var moduleAssemblies = blazorServerModuleService.GetModuleAssemblies();

            // Assert
            moduleAssemblies.Count().Should().Be(0);
        }

        [Fact]
        public void Should_returns_module_assemblies_provided_by_ui_manager()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();
            _uiModuleManager.GetAdditionalAssemblies().Returns([
                typeof(TestClientModule).Assembly,
                typeof(BlazorServerBackendModule).Assembly
                ]);

            // Act
            var moduleAssemblies = blazorServerModuleService.GetModuleAssemblies();

            // Assert
            moduleAssemblies.Count().Should().Be(2);
        }
    }

    public sealed class GetAllModuleStylesheets : BlazorServerModuleServiceTests
    {
        [Fact]
        public void Should_returns_all_module_stylesheets()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();

            // Act
            var moduleStylesheets = blazorServerModuleService.GetAllModuleStylesheets().ToArray();

            // Assert
            moduleStylesheets.Should().NotContain(s => s.Contains(_blazorServerAssembly.GetName().Name!));
        }
    }

    public sealed class InitializeServices : BlazorServerModuleServiceTests
    {
        [Fact]
        public async Task Should_call_initialize_service_for_client_module()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var blazorServerModuleService = serviceProvider.GetRequiredService<BlazorServerModuleService>();
            var admin = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimsIdentity.DefaultRoleClaimType, AccessLevel.Full.ToString())
            ]));

            // Act
            Assert.NotNull(serviceProvider);
            await blazorServerModuleService.InitializeServices(serviceProvider);
            await blazorServerModuleService.OnUserAuthenticated(serviceProvider, admin);

            // Assert
            var clientModule = (TestClientModule)_uiModuleManager.UiModules.First(module => module is TestClientModule);
            var user = clientModule.Users.FirstOrDefault();
            user.Should().Be(admin);
        }
    }
}
