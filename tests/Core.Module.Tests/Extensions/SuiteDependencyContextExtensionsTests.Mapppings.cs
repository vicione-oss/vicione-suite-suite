using Core.Module.Extensions;

namespace Core.Module.Tests.Extensions;

public partial class SuiteDependencyContextExtensionsTests
{
    public sealed class UseAssemblyMapping : SuiteDependencyContextExtensionsTests
    {
        #region Assert Helpers
        private static void AssertOnlyMapsTo(List<ModuleAssemblyMapping> mappings, params string[] modules)
            => mappings.Should().OnlyContain(k => modules.Contains(k.MapTo.Module));

        private static void AssertOnlyMapsFrom(List<ModuleAssemblyMapping> mappings, params string[] modules)
            => mappings.Should().OnlyContain(k => k.MapFrom.All(m => modules.Contains(m.Module)));

        private static void AssertCoreNotContainedInMapFrom(List<ModuleAssemblyMapping> mappings)
            => mappings.Should().NotContain(k => k.MapFrom.Any(m => m.Module == TestDependencyContextBuilder.CoreOs), "core libraries not map to itself");

        private static void AssertMappingToCore(ModuleAssemblyMapping mapping, string fromModule, string fromVersion, string toVersion)
            => AssertMapping(mapping, fromModule, TestDependencyContextBuilder.CoreOs, fromVersion, toVersion);

        private static void AssertMappingToBlazorServer(ModuleAssemblyMapping mapping, string fromModule, string fromVersion, string toVersion)
            => AssertMapping(mapping, fromModule, TestDependencyContextBuilder.BlazorServer, fromVersion, toVersion);

        private static void AssertMapping(ModuleAssemblyMapping mapping, string fromModule, string toModule, string fromVersion, string toVersion)
        {
            mapping.MapFrom.Should().ContainSingle(k => k.Module == fromModule);
            mapping.MapTo.Module.Should().Be(toModule);

            mapping.MapFrom.Should().Contain(k => k.Version == fromVersion);
            mapping.MapTo.Version.Should().Be(toVersion);
        }
        #endregion

        [Fact]
        public void Core_assemblies_dont_map_to_core()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            mappings.Should().BeEmpty();
        }

        [Fact]
        public void Only_shared_uihost_assemblies_should_map_to_core()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            Assert.NotNull(suiteContext.UiHost);
            AssertCoreNotContainedInMapFrom(mappings);
            AssertOnlyMapsTo(mappings, TestDependencyContextBuilder.CoreOs);
            AssertOnlyMapsFrom(mappings, TestDependencyContextBuilder.BlazorServer); // "only libraries from uihost are mapped"
            mappings.Should().HaveCountLessThan(suiteContext.UiHost.RuntimeLibraries.Count, "ui libraries do not exist in core");
            mappings.Should().HaveCount(suiteContext.UiHost.RedundantLibraries.Count, "all mapped assemblies are redundant");
            mappings.Should().OnlyContain(k => k.MapFrom.First().Version == k.MapTo.Version, "all versions are match");
        }

        [Fact]
        public void Ping_backend_module_should_map_to_core_and_uihost()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(null, true, false)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertCoreNotContainedInMapFrom(mappings);
            AssertOnlyMapsTo(mappings, TestDependencyContextBuilder.CoreOs, TestDependencyContextBuilder.BlazorServer);
            AssertOnlyMapsFrom(mappings, TestDependencyContextBuilder.BlazorServer, TestDependencyContextBuilder.PingBackend);
        }

        [Fact]
        public void Ping_client_module_assemblies_should_map_to_core_and_uihost()
        {
            // Arrange
            var builder = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(null, false, true);

            var suiteContext = builder.Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertCoreNotContainedInMapFrom(mappings);
            AssertOnlyMapsTo(mappings, TestDependencyContextBuilder.CoreOs, TestDependencyContextBuilder.BlazorServer);

            List<string> assemblies =
            [
                TestDependencyContextBuilder.SassCompiler,
                TestDependencyContextBuilder.DevExpressBlazor,
                TestDependencyContextBuilder.SuiteSdkClient,
                TestDependencyContextBuilder.SuiteSdkClientComponents,
                TestDependencyContextBuilder.SuiteSdkLocalization,
            ];

            foreach (var module in assemblies)
            {
                mappings.First(k => k.AssemblyName == module).MapTo.Module.Should().Be(TestDependencyContextBuilder.BlazorServer);
            }
        }

        [Fact]
        public void Ping_client_module_assembly_versions_should_map_to_blazor_server()
        {
            // Arrange
            var coreVersions = new TestDependencyVersions();
            var pingVersions = new TestDependencyVersions
            {
                DevExpressBlazor = "23.2.3",
                SassCompiler = "1.72.2",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS(coreVersions)
                .SetupBlazorServer(coreVersions)
                .SetupPingModule(pingVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertMappingToBlazorServer(mappings.First(k => k.AssemblyName == TestDependencyContextBuilder.DevExpressBlazor),
                TestDependencyContextBuilder.PingClient,
                pingVersions.DevExpressBlazor,
                coreVersions.DevExpressBlazor);

            AssertMappingToBlazorServer(mappings.First(k => k.AssemblyName == TestDependencyContextBuilder.SassCompiler),
                TestDependencyContextBuilder.PingClient,
                pingVersions.SassCompiler,
                coreVersions.SassCompiler);
        }

        [Fact]
        public void Ping_client_module_assembly_versions_should_map()
        {
            // Arrange
            var coreVersions = new TestDependencyVersions();
            var pingVersions = new TestDependencyVersions();
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS(coreVersions)
                .SetupBlazorServer(coreVersions)
                .SetupPingModule(pingVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert - no version mappings
            mappings.Where(k => k.MapFrom.Any(m => m.Version != k.MapTo.Version))
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void Ping_module_different_assemblies_should_map_versions()
        {
            // Arrange
            var coreVersions = new TestDependencyVersions();
            var pingVersions = new TestDependencyVersions
            {
                SQLiteMicrosoft = "8.0.1",
                SQLitePCLRaw = "2.1.2",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS(coreVersions)
                .SetupPingModule(pingVersions, true, false)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert - no version mappings
            mappings.Where(k => k.MapFrom.Any(m => m.Version != k.MapTo.Version))
                .Should()
                .HaveCount(2);
        }

        [Fact]
        public void Cluster_management_backend_module_assembly_versions_should_map()
        {
            // Arrange
            var coreVersions = new TestDependencyVersions();
            var pingVersions = new TestDependencyVersions
            {
                SQLiteMicrosoft = "8.0.1",
                SQLitePCLRaw = "2.1.2",
            };
            var mgmtVersions = new TestDependencyVersions
            {
                SQLiteMicrosoft = "8.0.2",
                SQLitePCLRaw = "2.1.7",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS(coreVersions)
                .SetupPingModule(pingVersions, true, false)
                .SetupClusterManagement(mgmtVersions, true, false)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert        
            AssertMappingToCore(mappings.First(k => k.AssemblyName == "Microsoft.Data.Sqlite.Core"),
                TestDependencyContextBuilder.PingBackend,
                pingVersions.SQLiteMicrosoft,
                coreVersions.SQLiteMicrosoft);

            AssertMappingToCore(mappings.First(k => k.AssemblyName == "Microsoft.Data.Sqlite.Core"),
                TestDependencyContextBuilder.ClusterManagementBackend,
                mgmtVersions.SQLiteMicrosoft,
                coreVersions.SQLiteMicrosoft);

            AssertMappingToCore(mappings.First(k => k.AssemblyName == "SQLitePCLRaw.core"),
                TestDependencyContextBuilder.PingBackend,
                pingVersions.SQLitePCLRaw,
                coreVersions.SQLitePCLRaw);

            AssertMappingToCore(mappings.First(k => k.AssemblyName == "SQLitePCLRaw.core"),
                TestDependencyContextBuilder.ClusterManagementBackend,
                mgmtVersions.SQLitePCLRaw,
                coreVersions.SQLitePCLRaw);
        }

        [Fact]
        public void Cluster_management_client_module_assembly_versions_should_map()
        {
            // Arrange
            var coreVersions = new TestDependencyVersions();
            var pingVersions = new TestDependencyVersions
            {
                DevExpressBlazor = "23.2.6",
                SassCompiler = "1.72.4",
            };
            var mgmtVersions = new TestDependencyVersions
            {
                SassCompiler = "1.72.2",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS(coreVersions)
                .SetupBlazorServer(coreVersions)
                .SetupPingModule(pingVersions)
                .SetupClusterManagement(mgmtVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertMappingToBlazorServer(mappings.First(k => k is { AssemblyName: TestDependencyContextBuilder.SassCompiler }),
                TestDependencyContextBuilder.PingClient,
                pingVersions.SassCompiler,
                coreVersions.SassCompiler);

            AssertMappingToBlazorServer(mappings.First(k =>
                    k is { AssemblyName: TestDependencyContextBuilder.SassCompiler }),
                TestDependencyContextBuilder.ClusterManagementClient,
                mgmtVersions.SassCompiler,
                coreVersions.SassCompiler);
        }

        [Fact]
        public void Public_module_assembly_should_map_to_backend()
        {
            // Arrange
            var pingVersions = new TestDependencyVersions();
            var mgmtVersions = new TestDependencyVersions
            {
                PingModule = "0.7.0",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(pingVersions)
                .SetupClusterManagement(mgmtVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertMapping(mappings.First(k => k is { AssemblyName: TestDependencyContextBuilder.PingPublic }),
                TestDependencyContextBuilder.PingClient,
                TestDependencyContextBuilder.PingBackend,
                pingVersions.PingModule,
                pingVersions.PingModule);

            AssertMapping(mappings.First(k => k is { AssemblyName: TestDependencyContextBuilder.PingPublic }),
                TestDependencyContextBuilder.ClusterManagementClient,
                TestDependencyContextBuilder.PingBackend,
                mgmtVersions.PingModule,
                pingVersions.PingModule);
        }

        [Fact]
        public void Module_only_dependencies_should_map_to_backend_origin()
        {
            // Arrange - Ping.Backend|Ping.Client|ClusterManagement.Client depend on ThirdPartyPackage
            var pingVersions = new TestDependencyVersions
            {
                ThirdPartyPackage = "1.5.0",
            };
            var mgmtVersions = new TestDependencyVersions();
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(pingVersions, true, true)
                .SetupClusterManagement(mgmtVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            AssertMapping(mappings.First(k =>
                    k is { AssemblyName: TestDependencyContextBuilder.ThirdPartyPackage }),
                TestDependencyContextBuilder.ClusterManagementClient,
                TestDependencyContextBuilder.PingBackend,
                mgmtVersions.ThirdPartyPackage,
                pingVersions.ThirdPartyPackage);

            AssertMapping(mappings.First(k =>
                    k is { AssemblyName: TestDependencyContextBuilder.ThirdPartyPackage }),
                TestDependencyContextBuilder.PingClient,
                TestDependencyContextBuilder.PingBackend,
                pingVersions.ThirdPartyPackage,
                pingVersions.ThirdPartyPackage);
        }

        [Fact]
        public void Client_only_dependencies_should_map_to_one_module()
        {
            // Arrange - Ping.Client|ClusterManagement.Client depend on ThirdPartyPackage
            var pingVersions = new TestDependencyVersions
            {
                ThirdPartyPackage = "1.5.0",
            };
            var mgmtVersions = new TestDependencyVersions();
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(pingVersions, false, true)
                .SetupClusterManagement(mgmtVersions, false, true)
                .Build();

            // Act
            var mappings = suiteContext.UseAssemblyMapping();

            // Assert
            mappings.Should().ContainSingle(k => k.AssemblyName == TestDependencyContextBuilder.ThirdPartyPackage);
        }

        [Fact]
        public void Create_mapping_summary_should_contain_no_mismatch()
        {
            // Arrange
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule()
                .SetupClusterManagement()
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var summary = suiteContext.CreateMappingSummary();

            // Assert
            summary.Modules.Sum(k => k.MismatchCount).Should().Be(0, "all versions match");
        }

        [Fact]
        public void Create_mapping_summary_should_contain_mismatches_for_different_versions()
        {
            // Arrange
            var pingVersions = new TestDependencyVersions
            {
                DevExpressBlazor = "23.2.4",
                SassCompiler = "1.71.0",
                SQLiteMicrosoft = "7.0.21",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupBlazorServer()
                .SetupPingModule(pingVersions)
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var summary = suiteContext.CreateMappingSummary();

            // Assert
            summary.Modules.Sum(k => k.MismatchCount).Should().Be(3, "3 ping mismatches");

            summary.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.DevExpressBlazor)
                .BuildDiffs.Should().HaveCount(1);

            summary.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.SassCompiler)
                .MinorDiffs.Should().HaveCount(1);

            summary.Modules
                .First(k => k.AssemblyName == TestDependencyContextBuilder.SQLiteMicrosoft)
                .MajorDiffs.Should().HaveCount(1);
        }

        [Fact]
        public void Create_mapping_summary_should_contain_matches_for_sdk_pipeline_versions()
        {
            // Arrange
            var pingVersions = new TestDependencyVersions
            {
                SuiteSdk = "0.11.0.61017-ci",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupPingModule(pingVersions, client: false)
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var summary = suiteContext.CreateMappingSummary();

            var matches = summary.Modules.SelectMany(k => k.Matches);

            // Assert
            summary.Modules.Sum(k => k.MismatchCount).Should().Be(0, "0 sdk mismatches because .61017-ci gets ignored");
            summary.Modules.Where(k => k.AssemblyName.StartsWith(TestDependencyContextBuilder.SuiteSdk, StringComparison.Ordinal))
                .SelectMany(m => m.Matches).Should().HaveCount(2, "sdk mapping 0.11.0.61017-ci to 0.11.0");
        }

        [Fact]
        public void Create_mapping_summary_should_take_latest_shared_version()
        {
            // Arrange
            var mgmtVersions = new TestDependencyVersions
            {
                ThirdPartyPackage = "0.1.0.1183622-ci",
            };
            var pingVersions = new TestDependencyVersions
            {
                ThirdPartyPackage = "0.1.0.61017-ci",
            };
            var suiteContext = new TestDependencyContextBuilder()
                .SetupCoreOS()
                .SetupClusterManagement(mgmtVersions)
                .SetupPingModule(pingVersions)
                .Build();

            suiteContext.UseAssemblyMapping();

            // Act
            var summary = suiteContext.CreateMappingSummary();

            var matches = summary.Modules.SelectMany(k => k.Matches);

            // Assert
            summary.Modules.Sum(k => k.MismatchCount).Should().Be(0, "0 sdk mismatches because .61017-ci gets ignored");
            summary.Modules.Where(k => k.AssemblyName.StartsWith(TestDependencyContextBuilder.ThirdPartyPackage, StringComparison.Ordinal))
                .SelectMany(m => m.Matches).Should().HaveCount(3, "mgmt backend|client + ping client -> ping.backend");

            var mapping = suiteContext.Mappings.First(k => k.AssemblyName == TestDependencyContextBuilder.ThirdPartyPackage);
            mapping.MapTo.Version.Should().Be("0.1.0.1183622-ci", "higher version wins");
        }
    }
}
