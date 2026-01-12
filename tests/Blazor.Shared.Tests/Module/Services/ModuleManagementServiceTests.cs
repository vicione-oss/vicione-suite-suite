using Blazor.Shared.Module;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.Services;

public class ModuleManagementServiceTests
{
    private const string MinSuiteVersion = "0.17.0";

    private const string TestClientModuleName = "ViciOne.Suite.TestClientModule";
    private const string TestClientModuleVersion = "1.0.0";
    private const string TestClientModuleUpdateVersion = "1.2.1";

    private const string AnotherModuleName = "ViciOne.Suite.AnotherModule";
    private const string AnotherModuleVersion = "1.3.2";

    private const string AvailableModulePackageName = "ViciOne.Suite.AvailableModule";
    private const string AvailableModuleVersion = "1.0.1";

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IInstanceInformationProvider _informationProvider = Substitute.For<IInstanceInformationProvider>();

    private readonly List<ModuleMetadataBundle> _moduleBundles = [
        new ModuleMetadataBundle {
            ModuleId = TestClientModuleName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = TestClientModuleName,
                Version = TestClientModuleVersion,
            },
            Installed = true,
            AvailableVersions = [
                TestClientModuleUpdateVersion,
                "0.7.0"
            ],
        },
        new ModuleMetadataBundle {
            ModuleId = AnotherModuleName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = AnotherModuleName,
                Version = AnotherModuleVersion
            },
            Installed = true,
            AvailableVersions = [
                AnotherModuleVersion
            ]
        },
        new ModuleMetadataBundle {
            ModuleId = AvailableModulePackageName,
            Metadata = new ModuleMetadata {
                MinSuiteSdkVersion = MinSuiteVersion,
                Name = AvailableModulePackageName,
                Version = AvailableModuleVersion,
                Options = [
                    new ModuleOptionDeclaration
                    {
                        Key = "Option1",
                        DefaultValue = "test1",
                        OptionType = ModuleOptionType.Text,
                    },
                    new ModuleOptionDeclaration
                    {
                        Key = "Option2",
                        DefaultValue = "2",
                        OptionType = ModuleOptionType.Number,
                    },
                    new ModuleOptionDeclaration
                    {
                        Key = "Option3",
                        OptionType = ModuleOptionType.Text,
                    },
                ]
            },
            Installed = false,
            AvailableVersions = [
                AnotherModuleVersion,
                "ci-234234"
            ]
        }];

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_mediator)
            .AddSingleton(_informationProvider)
            .AddSingleton<ModuleManagementService>()
            .BuildServiceProvider();

    private void SetupModuleMetadata(List<ModuleMetadataBundle>? bundles = null)
        => _mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(
            Arg.Any<GetModuleMetadataBundlesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetModuleMetadataBundlesResponse(bundles ?? []));

    public sealed class GetModuleMetadata : ModuleManagementServiceTests
    {
        [Fact]
        public async Task Should_preset_available_modules_option_values_with_defaults()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata(_moduleBundles);

            // Act
            var results = await service.GetModuleMetadata();

            // Assert
            var available = ModuleMetadataModelFactory.CreateModel(results.Bundles.First(k => k.ModuleId == AvailableModulePackageName));
            foreach (var option in available.EditOptions.Values)
            {
                if (string.IsNullOrWhiteSpace(option.DefaultValue))
                    continue;

                option.Value.Should().Be(option.DefaultValue);
            }

            available.EditOptions.Values.Should().ContainSingle(k => k.Value == null, "the other 2 options have their default values");
        }

        [Fact]
        public async Task Should_return_empty_result_if_no_modules_are_installed_or_available()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata();

            // Act
            var results = await service.GetModuleMetadata(true);

            // Assert
            results.Bundles.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_installed_modules()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            SetupModuleMetadata(_moduleBundles);

            // Act
            var results = await service.GetModuleMetadata();

            // Assert
            results.Bundles.Select(k => k).Should().BeEquivalentTo(_moduleBundles);
        }
    }

    public sealed class SetModuleVersions : ModuleManagementServiceTests
    {
        [Fact]
        public async Task Should_send_command()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            var models = ModuleMetadataModelFactory.CreateModels(_moduleBundles);

            // Act
            await service.UpdateModulePackageVersions(models);

            // Assert
            await _mediator.Received().Send(Arg.Any<UpdateModulePackageManifest>());
        }

        [Fact]
        public async Task Should_keep_installed_modules()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            var models = ModuleMetadataModelFactory.CreateModels(_moduleBundles);

            // Act
            await service.UpdateModulePackageVersions(models);

            // Assert
            await _mediator.Received().Send(Arg.Is<UpdateModulePackageManifest>(k
                => k.Manifest.Packages.Count == 2
                && k.Manifest.Packages.First(m => m.Name == TestClientModuleName).Version.ToString() == TestClientModuleVersion
                && k.Manifest.Packages.First(m => m.Name == AnotherModuleName).Version.ToString() == AnotherModuleVersion));
        }

        [Fact]
        public async Task Should_update_installed_modules()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            var models = ModuleMetadataModelFactory.CreateModels(_moduleBundles);

            models.First(k => k.Name == TestClientModuleName).UpdateVersion = TestClientModuleUpdateVersion;

            // Act
            await service.UpdateModulePackageVersions(models);

            // Assert
            await _mediator.Received().Send(Arg.Is<UpdateModulePackageManifest>(k
                => k.Manifest.Packages.Count == 2
                && k.Manifest.Packages.First(m => m.Name == TestClientModuleName).Version.ToString() == TestClientModuleUpdateVersion
                && k.Manifest.Packages.First(m => m.Name == AnotherModuleName).Version.ToString() == AnotherModuleVersion));
        }

        [Fact]
        public async Task Should_send_available_modules_to_be_installed()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var service = serviceProvider.GetRequiredService<ModuleManagementService>();
            var models = ModuleMetadataModelFactory.CreateModels(_moduleBundles);

            var package = models.First(k => !k.Installed && k.Name == AvailableModulePackageName);
            package.SelectedVersion = AvailableModuleVersion;
            package.ToBeInstalled = true;

            // Act
            await service.UpdateModulePackageVersions(models);

            // Assert
            await _mediator.Received().Send(Arg.Is<UpdateModulePackageManifest>(k
                => k.Manifest.Packages.Count == 3
                && k.Manifest.Packages.First(m => m.Name == TestClientModuleName).Version.ToString() == TestClientModuleVersion
                && k.Manifest.Packages.First(m => m.Name == AnotherModuleName).Version.ToString() == AnotherModuleVersion
                && k.Manifest.Packages.First(m => m.Name == AvailableModulePackageName).Version.ToString() == AvailableModuleVersion));
        }
    }
}
