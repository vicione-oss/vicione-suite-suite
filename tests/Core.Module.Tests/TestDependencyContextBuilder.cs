using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Core.Module.Tests;

/// <summary>
/// Builder class to create a tests dependency context for suite.
/// Use the SetupXXX calls to simulate modules with runtime libraries
///
/// improvements:
///  - case that modules a,b reference a public lib from c
///  - case for module ref public lib and it's public lib gets referenced
///  - case that module has only a client module and its public part gets referenced
/// </summary>
internal sealed class TestDependencyContextBuilder
{
    public const string SassCompiler = "AspNetCore.SassCompiler";
    public const string DevExpressBlazor = "DevExpress.Blazor";
    public const string BlazorServer = "ViciOne.Suite.Blazor.Server.Backend";
    public const string CoreOs = "ViciOne.Suite.Core.OS";

    public const string ClusterManagementModuleId = "ViciOne.Suite.ClusterManagement";
    public const string ClusterManagementBackend = ClusterManagementModuleId + Constants.ModuleSuffixBackend;
    public const string ClusterManagementClient = ClusterManagementModuleId + Constants.ModuleSuffixClient;
    public const string ClusterManagementPublic = ClusterManagementModuleId + Constants.ModuleSuffixPublic;


    public const string DataCollectionWizardModuleId = "ViciOne.Suite.DataCollectionWizard";
    public const string DataCollectionWizardBackend = DataCollectionWizardModuleId + Constants.ModuleSuffixBackend;
    public const string DataCollectionWizardClient = DataCollectionWizardModuleId + Constants.ModuleSuffixClient;

    public const string PingModuleId = "ViciOne.Suite.Ping";
    public const string PingBackend = PingModuleId + Constants.ModuleSuffixBackend;
    public const string PingClient = PingModuleId + Constants.ModuleSuffixClient;
    public const string PingPublic = PingModuleId + Constants.ModuleSuffixPublic;

    public const string UiOnlyId = "ViciOne.Suite.UiOnly";
    public const string UiOnlyClient = UiOnlyId + Constants.ModuleSuffixClient;
    public const string UiOnlyPublic = UiOnlyId + Constants.ModuleSuffixPublic;
    private const string UiOnlyVersion = "0.24.0";

    public const string UiOnlyDependentModuleId = "ViciOne.Suite.UiOnlyDependent";
    private const string UiOnlyDependentClient = UiOnlyDependentModuleId + Constants.ModuleSuffixClient;

    public const string MassTransit = "MassTransit";
    public const string SQLiteMicrosoft = "Microsoft.Data.Sqlite.Core";

    public const string SuiteSdk = "ViciOne.Suite.Sdk";
    public const string SuiteSdkBackend = SuiteSdk + Constants.ModuleSuffixBackend;
    public const string SuiteSdkClient = SuiteSdk + Constants.ModuleSuffixClient;
    public const string SuiteSdkClientComponents = SuiteSdkClient + ".Components";
    public const string SuiteSdkLocalization = SuiteSdk + ".Localization";

    public const string UiSharedDx = "ViciOne.Ui.Shared.Dx";
    public const string VoCodeAnalysis = "ViciOne.CodeStyle";
    public const string ThirdPartyPackage = "ViciOne.Test.ThirdPartyPackage";

    private sealed class Options
    {
        public ModuleDependencyContext? CoreContext { get; set; }

        public ModuleDependencyContext? UiHostContext { get; set; }

        public List<ModuleDependencyContext> ModuleContexts { get; set; } = [];
    }

    private readonly Options _options = new();

    public TestDependencyContextBuilder SetupCoreOS(TestDependencyVersions? versions = null)
    {
        var options = versions ?? new TestDependencyVersions();
        var libraries = CreateMicrosoftRuntimeLibraries(options)
            .Union(CreateMassTransitRuntimeLibraries(options, false))
            .Union(CreateSdkRuntimeLibraries(options, true, false))
            .Union(CreateSqliteRuntimeLibraries(options));

        var dependencyContext = CreateDependencyContext(libraries);
        _options.CoreContext = new ModuleDependencyContext(ModuleType.Backend, $"/src/Core.OS/bin/Debug/net10.0/{CoreOs}.deps.json", false);
        _options.CoreContext.AddRuntimeLibraries(dependencyContext);

        return this;
    }

    public TestDependencyContextBuilder SetupBlazorServer(TestDependencyVersions? versions = null)
    {
        if (_options.CoreContext is null)
            SetupCoreOS(versions);

        var options = versions ?? new TestDependencyVersions();
        var libraries = CreateMicrosoftRuntimeLibraries(options)
            .Union(CreateSdkRuntimeLibraries(options))
            .Union(CreateMassTransitRuntimeLibraries(options))
            .Union(CreateSassCompilerRuntimeLibraries(options))
            .Union(CreateBlazorServerRuntimeLibraries(options));

        var dependencyContext = CreateDependencyContext(libraries);
        var context = new ModuleDependencyContext(ModuleType.Backend, $"/src/Blazor.Server.Backend/bin/Debug/net10.0/{BlazorServer}.deps.json", false);
        context.AddRuntimeLibraries(dependencyContext);

        _options.UiHostContext = context;

        return this;
    }

    /// <summary>
    /// Fake assembly context for Ping.Client|Backend to test the mappings
    /// Client|Backend depends on ThirdPartyPackage
    /// </summary>
    public TestDependencyContextBuilder SetupPingModule(TestDependencyVersions? versions = null, bool backend = true, bool client = true)
        => SetupModule(versions, backend ? CreatePingBackendContext : null, client ? CreatePingClientContext : null);

    /// <summary>
    /// Fake assembly context for ClusterManagement.Client|Backend to test the mappings
    /// Client|Backend depends on Ping.Public and ThirdPartyPackage
    /// </summary>
    public TestDependencyContextBuilder SetupClusterManagement(TestDependencyVersions? versions = null, bool backend = true, bool client = true)
        => SetupModule(versions, backend ? CreateClusterManagementBackendContext : null, client ? CreateClusterManagementClientContext : null);

    /// <summary>
    /// DataCollectionWizard depends on ClusterManagement.Public
    /// </summary>
    public TestDependencyContextBuilder SetupDataCollectionWizard(TestDependencyVersions? versions = null, bool backend = true, bool client = true)
        => SetupModule(versions, backend ? CreateDataCollectionWizardBackendContext : null, client ? CreateDataCollectionWizardClientContext : null);

    /// <summary>
    /// A client module without backend
    /// </summary>
    public TestDependencyContextBuilder SetupUiOnlyClientModule(TestDependencyVersions? versions = null)
        => SetupModule(versions, null, CreateUiOnlyClientContext);

    /// <summary>
    /// Another client module without backend with dependency to SomeClient.Public
    /// </summary>
    public TestDependencyContextBuilder SetupUiOnlyDependentClientModule(TestDependencyVersions? versions = null)
        => SetupModule(versions, null, CreateUiOnlyDependentClientContext);

    /// <summary>
    /// Simulates two unrelated backend modules (no dependency relation between them and no other
    /// module depending on either) that both ship the same shared package unknown to core|host,
    /// but at different versions. Used to characterize the "highest compatible version wins"
    /// arbitration in <see cref="Core.Module.Extensions.SuiteDependencyContextExtensions"/>.
    /// </summary>
    /// <param name="moduleAVersion">Version of the shared package shipped by module A.</param>
    /// <param name="moduleBVersion">Version of the shared package shipped by module B.</param>
    public TestDependencyContextBuilder SetupUnrelatedModulesWithSharedPackage(string moduleAVersion, string moduleBVersion)
    {
        if (_options.CoreContext is null)
            SetupCoreOS();

        var options = new TestDependencyVersions();
        _options.ModuleContexts.Add(CreateSharedPackageBackendContext(options, "ViciOne.Suite.SharedA", moduleAVersion));
        _options.ModuleContexts.Add(CreateSharedPackageBackendContext(options, "ViciOne.Suite.SharedB", moduleBVersion));

        return this;
    }

    private static ModuleDependencyContext CreateSharedPackageBackendContext(TestDependencyVersions versions, string moduleId, string sharedPackageVersion)
    {
        var moduleBackend = moduleId + Constants.ModuleSuffixBackend;

        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateSdkRuntimeLibraries(versions, client: false))
            .Union(
            [
                new RuntimeLibrary("package",
                    moduleBackend,
                    versions.SuiteSdk,
                    null,
                    [],
                    [],
                    [],
                    [
                        new Dependency(SuiteSdkBackend, versions.SuiteSdk),
                        new Dependency(ThirdPartyPackage, sharedPackageVersion),
                    ],
                    true),
                new RuntimeLibrary("package",
                    ThirdPartyPackage,
                    sharedPackageVersion,
                    null,
                    [new RuntimeAssetGroup(null, $"lib/net9.0/{ThirdPartyPackage}.dll")],
                    [],
                    [],
                    [
                        new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
                    ],
                    true),
            ]);

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Backend, moduleId, moduleBackend);
    }

    private TestDependencyContextBuilder SetupModule(TestDependencyVersions? versions,
        Func<TestDependencyVersions, ModuleDependencyContext>? backendCall = null,
        Func<TestDependencyVersions, ModuleDependencyContext>? clientCall = null)
    {
        if (_options.CoreContext is null)
            SetupCoreOS(versions);

        var options = versions ?? new TestDependencyVersions();
        if (backendCall is not null)
            _options.ModuleContexts.Add(backendCall(options));

        if (clientCall is not null)
            _options.ModuleContexts.Add(clientCall(options));

        return this;
    }

    public SuiteDependencyContext Build()
        => new(_options.CoreContext ?? throw new InvalidOperationException("You need to setup either Core or UiHost"),
            _options.UiHostContext,
            _options.ModuleContexts);

    private static ModuleDependencyContext CreateModuleDependencyContext(DependencyContext dependencyContext, ModuleType moduleType, string moduleId, string assemblyName)
    {
        var context = new ModuleDependencyContext(moduleType, $"/src/{moduleId}/bin/Debug/net10.0/{assemblyName}.deps.json", false);
        context.AddRuntimeLibraries(dependencyContext);
        return context;
    }

    private static ModuleDependencyContext CreateClusterManagementBackendContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateMassTransitRuntimeLibraries(versions))
            .Union(CreateSdkRuntimeLibraries(versions, client: false))
            .Union(CreateSqliteRuntimeLibraries(versions))
            .Union(CreateClusterManagementBackendRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Backend, ClusterManagementModuleId, ClusterManagementBackend);
    }

    private static ModuleDependencyContext CreateClusterManagementClientContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateMassTransitRuntimeLibraries(versions))
            .Union(CreateSdkRuntimeLibraries(versions, backend: false))
            .Union(CreateClusterManagementClientRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Client, ClusterManagementModuleId, ClusterManagementClient);
    }

    private static ModuleDependencyContext CreatePingBackendContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateMassTransitRuntimeLibraries(versions))
            .Union(CreateSdkRuntimeLibraries(versions, client: false))
            .Union(CreateSqliteRuntimeLibraries(versions))
            .Union(CreatePingBackendRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Backend, PingModuleId, PingBackend);
    }

    private static ModuleDependencyContext CreatePingClientContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateMassTransitRuntimeLibraries(versions))
            .Union(CreateSdkRuntimeLibraries(versions, backend: false))
            .Union(CreatePingClientRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Client, PingModuleId, PingClient);
    }

    private ModuleDependencyContext CreateDataCollectionWizardBackendContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateSdkRuntimeLibraries(versions, client: false))
            .Union(CreateDataCollectionWizardBackendRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Backend, DataCollectionWizardModuleId, DataCollectionWizardBackend);
    }

    private ModuleDependencyContext CreateDataCollectionWizardClientContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateSdkRuntimeLibraries(versions, backend: false))
            .Union(CreateDataCollectionWizardClientRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Client, DataCollectionWizardModuleId, DataCollectionWizardClient);
    }

    private ModuleDependencyContext CreateUiOnlyClientContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateSdkRuntimeLibraries(versions, backend: false))
            .Union(CreateUiOnlyClientRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Client, UiOnlyId, UiOnlyClient);
    }

    private ModuleDependencyContext CreateUiOnlyDependentClientContext(TestDependencyVersions versions)
    {
        var libraries = CreateMicrosoftRuntimeLibraries(versions)
            .Union(CreateSdkRuntimeLibraries(versions, backend: false))
            .Union(CreateUiOnlyDependentClientRuntimeLibraries(versions));

        var dependencyContext = CreateDependencyContext(libraries);

        return CreateModuleDependencyContext(dependencyContext, ModuleType.Client, UiOnlyDependentModuleId, UiOnlyDependentClient);
    }

    private static DependencyContext CreateDependencyContext(IEnumerable<RuntimeLibrary> runtimeLibraries)
        => new(new TargetInfo(".NET", null, null, false), CompilationOptions.Default, [], runtimeLibraries, []);

    private static IEnumerable<RuntimeLibrary> CreateBlazorServerRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            BlazorServer,
            versions.SuiteSdk,
            null,
            [new RuntimeAssetGroup(null, $"{BlazorServer}.dll")],
            [],
            [],
            [
                new Dependency(SassCompiler, versions.SassCompiler),
                new Dependency(VoCodeAnalysis, "1.0.0"),
                new Dependency("ViciOne.Suite.Blazor.Shared", versions.SuiteSdk),
                new Dependency("ViciOne.Suite.Blazor.DevAssets", versions.SuiteSdk),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "ViciOne.Suite.Blazor.Shared",
            versions.SuiteSdk,
            null,
            [new RuntimeAssetGroup(null, "ViciOne.Suite.Blazor.Shared.dll")],
            [],
            [],
            [
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency("Microsoft.AspNetCore.Components.WebAssembly", versions.DotNetVersion),
                new Dependency("Microsoft.AspNetCore.Components.Web", versions.DotNetVersion),
                new Dependency(SuiteSdkClientComponents, versions.SuiteSdk),
                new Dependency(SuiteSdkLocalization, versions.SuiteSdk),
                new Dependency(UiSharedDx, versions.UiSharedDx),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "ViciOne.Ui.Shared.Dx",
            versions.UiSharedDx,
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/ViciOne.Ui.Shared.Dx.dll")],
            [],
            [],
            [
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency("Microsoft.AspNetCore.Components.Web", versions.DotNetVersion),
                new Dependency(SassCompiler, versions.SassCompiler),
            ],
            true);

        // DevExpress
        yield return new RuntimeLibrary("package",
            DevExpressBlazor,
            versions.DevExpressBlazor,
            null,
            [new RuntimeAssetGroup(null, "lib/net6.0/DevExpress.Blazor.v23.2.dll")],
            [],
            [],
            [],
            true);

        // Microsoft
        yield return new RuntimeLibrary("package",
            "Microsoft.AspNetCore.Components.WebAssembly",
            versions.DotNetVersion,
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/Microsoft.AspNetCore.Components.WebAssembly.dll")],
            [],
            [],
            [],
            true);

        yield return new RuntimeLibrary("package",
            "Microsoft.AspNetCore.Components.Web",
            versions.DotNetVersion,
            "sha512-8wvWv4uX4tu9PmPd9f84MY0ZqiQnkvUbpLzaUo6xxbg0oq7g73jzPt9aKypkiMr7g2PwOihfyftfSoj6LOJ6WQ==",
            [new RuntimeAssetGroup(null, "lib/net10.0/Microsoft.AspNetCore.Components.Web.dll")],
            [],
            [],
            [],
            true);
    }

    private static IEnumerable<RuntimeLibrary> CreateSqliteRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            SQLiteMicrosoft,
            versions.SQLiteMicrosoft,
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/Microsoft.Data.Sqlite.dll")],
            [],
            [],
            [
                new Dependency("SQLitePCLRaw.core", versions.SQLitePCLRaw),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "SQLitePCLRaw.core",
            versions.SQLitePCLRaw,
            null,
            [new RuntimeAssetGroup(null, "lib/netstandard2.0/SQLitePCLRaw.core.dll")],
            [],
            [],
            [
                new Dependency("System.Memory", "4.5.5"),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "System.Memory",
            "4.5.5",
            null,
            [],
            [],
            [],
            [],
            true);
    }

    private static IEnumerable<RuntimeLibrary> CreateMassTransitRuntimeLibraries(TestDependencyVersions versions, bool abstractionsOnly = true)
    {
        yield return new RuntimeLibrary("package",
            "MassTransit.Abstractions",
            versions.MassTransit,
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/MassTransit.Abstractions.dll")],
            [],
            [],
            [],
            true);

        if (!abstractionsOnly)
        {
            yield return new RuntimeLibrary("package",
                MassTransit,
                versions.MassTransit,
                null,
                [new RuntimeAssetGroup(null, $"lib/net10.0/{MassTransit}.dll")],
                [],
                [],
                [
                    new Dependency("MassTransit.Abstractions", versions.MassTransit),
                    new Dependency("Microsoft.Extensions.DependencyInjection.Abstractions", versions.MsDependencyInjectionAbstractions),// 8.0.1 before
                    new Dependency("Microsoft.Extensions.Hosting.Abstractions", versions.DotNetVersion),
                    new Dependency("Microsoft.Extensions.Logging.Abstractions", "9.0.0"),
                ],
                true);

            yield return new RuntimeLibrary("package",
                "MassTransit.RabbitMQ",
                versions.MassTransit,
                null,
                [new RuntimeAssetGroup(null, "lib/net10.0/MassTransit.RabbitMqTransport.dll")],
                [],
                [],
                [
                    new Dependency("MassTransit", versions.MassTransit),
                    new Dependency("RabbitMQ.Client", "6.8.1"),
                ],
                true);
        }
    }

    private static IEnumerable<RuntimeLibrary> CreateSassCompilerRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            SassCompiler,
            versions.SassCompiler,
            null,
            [],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Configuration.Binder", "3.1.0"),
                new Dependency("Microsoft.Extensions.DependencyInjection.Abstractions", "8.0.1"),
                new Dependency("Microsoft.Extensions.Hosting.Abstractions", "3.1.0"),
                new Dependency("Microsoft.Extensions.Options", "8.0.2"),
            ],
            true);
    }

    private static IEnumerable<RuntimeLibrary> CreateMicrosoftRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            "Microsoft.Extensions.DependencyInjection",
            versions.MsDependencyInjection,
            null,
            [],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.DependencyInjection.Abstractions", versions.MsDependencyInjectionAbstractions),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            versions.MsDependencyInjectionAbstractions,
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/Microsoft.Extensions.DependencyInjection.Abstractions.dll")],
            [],
            [],
            [],
            true);
    }

    private static IEnumerable<RuntimeLibrary> CreateSdkRuntimeLibraries(TestDependencyVersions versions, bool backend = true, bool client = true)
    {
        yield return new RuntimeLibrary("package",
            SuiteSdk,
            versions.SuiteSdk,
            null,
            [new RuntimeAssetGroup(null, $"{SuiteSdk}.dll")],
            [],
            [],
            [],
            false);

        if (backend)
        {
            yield return new RuntimeLibrary("package",
                SuiteSdkBackend,
                versions.SuiteSdk,
                null,
                [new RuntimeAssetGroup(null, $"{SuiteSdkBackend}.dll")],
                [],
                [],
                [
                    new Dependency("Microsoft.EntityFrameworkCore.Sqlite", "8.0.4"),
                    new Dependency("Npgsql.EntityFrameworkCore.PostgreSQL", "8.0.2"),
                    new Dependency(SuiteSdk, versions.SuiteSdk),
                ],
                true);
        }

        if (client)
        {
            yield return new RuntimeLibrary("package",
                SuiteSdkClient,
                versions.SuiteSdk,
                null,
                [new RuntimeAssetGroup(null, $"{SuiteSdkClient}.dll")],
                [],
                [],
                [
                    new Dependency("Microsoft.AspNetCore.Components.Web", versions.DotNetVersion),
                    new Dependency("Microsoft.Extensions.Localization", versions.DotNetVersion),
                    new Dependency(SuiteSdk, versions.SuiteSdk),
                ],
                true);


            yield return new RuntimeLibrary("package",
                SuiteSdkClientComponents,
                versions.SuiteSdk,
                null,
                [new RuntimeAssetGroup(null, $"{SuiteSdkClientComponents}.dll")],
                [],
                [],
                [
                    new Dependency(SuiteSdkClient, versions.SuiteSdk),
                    new Dependency(SuiteSdkLocalization, versions.SuiteSdk),
                    new Dependency("ViciOne.Ui.MonochromeIcons.Components", "1.3.1"),
                ],
                true);

            yield return new RuntimeLibrary("package",
                SuiteSdkLocalization,
                versions.SuiteSdk,
                null,
                [new RuntimeAssetGroup(null, $"{SuiteSdkLocalization}.dll")],
                [],
                [],
                [],
                true);
        }
    }

    private static IEnumerable<RuntimeLibrary> CreateClusterManagementBackendRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            "ViciOne.Suite.ClusterManagement.Backend",
            versions.ClusterManagement,
            null,
            [new RuntimeAssetGroup(null, "ViciOne.Suite.ClusterManagement.Backend.dll")],
            [],
            [],
            [
                new Dependency(ClusterManagementPublic, versions.ClusterManagement),
                new Dependency(PingPublic, versions.PingModule),
                new Dependency("MassTransit.EntityFrameworkCore", versions.MassTransit),
                new Dependency(SuiteSdkBackend, versions.SuiteSdk),
                new Dependency(ThirdPartyPackage, versions.ThirdPartyPackage),
                new Dependency("ViciOne.Core.Dataflow.DataModel.Generation", "0.42.0"),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            false);

        yield return new RuntimeLibrary("package",
            "ViciOne.ManagedEngine.Contracts",
            "0.50.0",
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/ViciOne.ManagedEngine.Contracts.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.DependencyInjection.Abstractions", versions.DotNetVersion),
                new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
            ],
            true);

        yield return CreateClusterManagementPublicRuntimeLibrary(versions);

        yield return CreatePingPublicRuntimeLibrary(versions);

        yield return CreateThirdPartyLibRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreateClusterManagementClientRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            "ViciOne.Suite.ClusterManagement.Client",
            versions.ClusterManagement,
            null,
            [new RuntimeAssetGroup(null, "ViciOne.Suite.ClusterManagement.Client.dll")],
            [],
            [],
            [
                new Dependency(ClusterManagementPublic, versions.ClusterManagement),
                new Dependency(PingPublic, versions.PingModule),
                new Dependency("MassTransit.EntityFrameworkCore", versions.MassTransit),
                new Dependency(SassCompiler, versions.SassCompiler),
                new Dependency(SuiteSdkClientComponents, versions.SuiteSdk),
                new Dependency(ThirdPartyPackage, versions.ThirdPartyPackage),
                new Dependency("ViciOne.Ui.ClusterEditor", "0.1.0.62682"),
                new Dependency("ViciOne.Core.Dataflow.DataModel.Generation", "0.42.0"),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            false);

        yield return new RuntimeLibrary("package",
            "ViciOne.Ui.ClusterEditor",
            "0.1.0.62682",
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/ViciOne.Ui.ClusterEditor.dll", "lib/net9.0/ViciOne.Ui.ColorableIcons.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Localization", versions.DotNetVersion),
                new Dependency("ViciOne.Cluster.Builder", "0.1.0.62370-ci"),
                new Dependency("ViciOne.TreeBuilder", "0.1.0.62476-ci"),
                new Dependency(UiSharedDx, versions.UiSharedDx),
                new Dependency("ViciOne.Ui.TreeEditor", "0.3.0.60341"),
                new Dependency("ViciOne.Ui.TreeEditor.Interface", "0.3.0.60341"),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "ViciOne.Ui.TreeEditor",
            "0.3.0.60341",
            null,
            [new RuntimeAssetGroup(null, "lib/net9.0/ViciOne.Ui.TreeEditor.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.AspNetCore.Components.Web", versions.DotNetVersion),
                new Dependency("ViciOne.Ui.TreeEditor.Interface", "0.3.0.60341"),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "ViciOne.Ui.TreeEditor.Interface",
            "0.3.0.60341",
            null,
            [new RuntimeAssetGroup(null, "lib/net9.0/ViciOne.Ui.TreeEditor.Interface.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.AspNetCore.Components.Web", versions.DotNetVersion),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "ViciOne.TreeBuilder",
            "0.1.0.62476-ci",
            null,
            [new RuntimeAssetGroup(null, "lib/net9.0/ViciOne.TreeBuilder.dll")],
            [],
            [],
            [
                new Dependency("YamlDotNet", "15.1.2"),
            ],
            true);

        yield return new RuntimeLibrary("package",
            "YamlDotNet",
            "15.1.2",
            null,
            [new RuntimeAssetGroup(null, "lib/net10.0/YamlDotNet.dll")],
            [],
            [],
            [],
            true);

        yield return CreateClusterManagementPublicRuntimeLibrary(versions);

        yield return CreatePingPublicRuntimeLibrary(versions);

        yield return CreateThirdPartyLibRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreatePingBackendRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            PingBackend,
            versions.SuiteSdk,
            null,
            [],
            [],
            [],
            [
                new Dependency(PingPublic, versions.PingModule),
                new Dependency("Microsoft.EntityFrameworkCore.Design", versions.DotNetVersion),
                new Dependency(SuiteSdkBackend, versions.SuiteSdk),
                new Dependency(ThirdPartyPackage, versions.ThirdPartyPackage),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreatePingPublicRuntimeLibrary(versions);

        yield return CreateThirdPartyLibRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreatePingClientRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            PingClient,
            versions.SuiteSdk,
            null,
            [new RuntimeAssetGroup(null, $"{PingClient}.dll")],
            [],
            [],
            [
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency(PingPublic, versions.PingModule),
                new Dependency(SassCompiler, versions.SassCompiler),
                new Dependency(SuiteSdkClient, versions.SuiteSdk),
                new Dependency(SuiteSdkClientComponents, versions.SuiteSdk),
                new Dependency(ThirdPartyPackage, versions.ThirdPartyPackage),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreatePingPublicRuntimeLibrary(versions);

        yield return CreateThirdPartyLibRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreateDataCollectionWizardBackendRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            DataCollectionWizardBackend,
            versions.DataCollectionWizard,
            null,
            [],
            [],
            [],
            [
                new Dependency(ClusterManagementPublic, versions.ClusterManagement),
                new Dependency("Microsoft.EntityFrameworkCore.Design", versions.DotNetVersion),
                new Dependency(SuiteSdkBackend, versions.SuiteSdk),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreateClusterManagementPublicRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreateDataCollectionWizardClientRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            DataCollectionWizardClient,
            versions.DataCollectionWizard,
            null,
            [],
            [],
            [],
            [
                new Dependency(ClusterManagementPublic, versions.ClusterManagement),
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency(SuiteSdkClient, versions.SuiteSdk),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreateClusterManagementPublicRuntimeLibrary(versions);
    }


    private static IEnumerable<RuntimeLibrary> CreateUiOnlyClientRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            UiOnlyClient,
            UiOnlyVersion,
            null,
            [],
            [],
            [],
            [
                new Dependency(UiOnlyPublic, UiOnlyVersion),
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency(SuiteSdkClient, versions.SuiteSdk),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreateUiOnlyPublicRuntimeLibrary(versions);
    }

    private static IEnumerable<RuntimeLibrary> CreateUiOnlyDependentClientRuntimeLibraries(TestDependencyVersions versions)
    {
        yield return new RuntimeLibrary("package",
            UiOnlyDependentClient,
            "0.28.5",
            null,
            [],
            [],
            [],
            [
                new Dependency(UiOnlyPublic, UiOnlyVersion),
                new Dependency(DevExpressBlazor, versions.DevExpressBlazor),
                new Dependency(SuiteSdkClient, versions.SuiteSdk),
                new Dependency(VoCodeAnalysis, versions.CodeAnalysis),
            ],
            true);

        yield return CreateUiOnlyPublicRuntimeLibrary(versions);
    }

    private static RuntimeLibrary CreatePingPublicRuntimeLibrary(TestDependencyVersions versions)
        => new("package",
            PingPublic,
            versions.PingModule,
            null,
            [new RuntimeAssetGroup(null, $"{PingPublic}.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
            ],
            true);

    private static RuntimeLibrary CreateClusterManagementPublicRuntimeLibrary(TestDependencyVersions versions)
        => new("package",
            ClusterManagementPublic,
            versions.ClusterManagement,
            null,
            [new RuntimeAssetGroup(null, $"{ClusterManagementPublic}.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
            ],
            true);

    private static RuntimeLibrary CreateUiOnlyPublicRuntimeLibrary(TestDependencyVersions versions)
        => new("package",
            UiOnlyPublic,
            UiOnlyVersion,
            null,
            [new RuntimeAssetGroup(null, $"{UiOnlyPublic}.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
            ],
            true);

    /// <summary>
    /// a fake package to simulate package not provided by core|host
    /// </summary>
    private static RuntimeLibrary CreateThirdPartyLibRuntimeLibrary(TestDependencyVersions versions)
        => new("package",
            ThirdPartyPackage,
            versions.ThirdPartyPackage,
            null,
            [new RuntimeAssetGroup(null, $"lib/net9.0/{ThirdPartyPackage}.dll")],
            [],
            [],
            [
                new Dependency("Microsoft.Extensions.Logging.Abstractions", versions.DotNetVersion),
            ],
            true);
}
