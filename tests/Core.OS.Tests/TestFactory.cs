using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Module;
using Core.Module.Utils;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Tests.Tools;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using TestSystem.Backend;

namespace Core.OS.Tests;

internal static class TestFactory
{
    public const string ModuleResourceNamespace = "Core.OS.Tests.Modules.Resources";
    public const string ClusterManagementMetadataResource = "0.22.0-win-x64_0.18.0.json";
    public const string ClusterManagementCiMetadataResource = "ci-1399909-win-x64_0.18.0.json";
    public const string DataCollectionWizardMetadataResource = "0.5.0-win-x64_0.18.0.json";

    public static UserManager<SuiteUser> CreateUserManager()
        => Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),                          // store
            Substitute.For<IOptions<IdentityOptions>>(),                      // optionsAccessor
            Substitute.For<IPasswordHasher<SuiteUser>>(),                     // passwordHasher
            Array.Empty<IUserValidator<SuiteUser>>(),                         // userValidators
            Array.Empty<IPasswordValidator<SuiteUser>>(),                     // passwordValidators
            Substitute.For<ILookupNormalizer>(),                              // keyNormalizer
            new IdentityErrorDescriber(),                                     // errors
            Substitute.For<IServiceProvider>(),                               // services
            Substitute.For<ILogger<UserManager<SuiteUser>>>()
            );

    /// <summary>
    /// Create a suite context with Core, optional TestUiHostBackend, optional TestBackendModule, optional TestClientModule
    /// </summary>
    internal static SuiteDependencyContext CreateSuiteContext(bool enableUiHost = true, bool enableBackendModules = true, bool enableUiModules = true)
    {
        var manifest = new ModulePackageManifest();

        var manifestProvider = Substitute.For<IModulePackageManifestStore>();
        manifestProvider.Load(Arg.Any<CancellationToken>()).Returns(manifest);

        var setup = new TestConfig()
            .ConfigureModuleLoader()
            .AddTestUiHost(enableUiHost)
            .AddTestBackendClientModule(enableBackendModules || enableUiModules);

        var config = setup.BuildConfiguration();
        var loaderOptions = config.GetModuleLoaderOptions();
        var uiHostOptions = config.CreateUiHostOptions(loaderOptions);
        var moduleOptions = config.CreateModuleOptions(manifest, TestBackendModule.Id, TestClientModule.Id);
        if (uiHostOptions is not null)
            moduleOptions[loaderOptions.UiHost!] = uiHostOptions;

        var builder = new SuiteDependencyContextBuilder()
            .WithCore(typeof(TestSystemModule).Assembly)
            .WithUiHost(loaderOptions, moduleOptions)
            .WithBackendModules(loaderOptions, moduleOptions);

        if (enableUiModules)
            builder.WithClientModules(loaderOptions, moduleOptions);

        return builder.Build();
    }

    public static SuiteDependencyContext CreateEmptySuiteContext()
    {
        var core = new ModuleDependencyContext(ModuleType.Backend, "emtpy", true);
        return new SuiteDependencyContext(core, null, []);
    }

    public static ModuleDependencyContext CreateBackendModuleContext(Type mainAssemblyType, bool isDebugSource = false)
        => CreateModuleContext(mainAssemblyType, ModuleType.Backend, isDebugSource);

    public static ModuleDependencyContext CreateModuleContext(Type mainAssemblyType, ModuleType moduleType, bool isDebugSource = false)
    {
        var assembly = Assembly.GetAssembly(mainAssemblyType);
        Assert.NotNull(assembly);

        var assemblyName = assembly.GetName();
        Assert.NotNull(assemblyName);
        Assert.NotNull(assemblyName.Name);
        Assert.NotNull(assemblyName.Version);

        var path = ModuleHelpers.DllToDepsJson(assembly.Location);
        var module = new ModuleDependencyContext(moduleType, path, isDebugSource);

        // A fake main library.
        module.RuntimeLibraries.Add(new RuntimeLibrary(mainAssemblyType.ToString(), assemblyName.Name, assemblyName.Version.ToString(), null, [], [], [], [], false));

        // A fake sdk library.
        var sdkAssembly = Assembly.GetAssembly(typeof(ModuleMetadata));
        var sdkAssemblyName = sdkAssembly!.GetName();

        Assert.NotNull(sdkAssemblyName);
        Assert.NotNull(sdkAssemblyName.Name);
        Assert.NotNull(sdkAssemblyName.Version);

        module.RuntimeLibraries.Add(new RuntimeLibrary(sdkAssembly.GetType().ToString(), sdkAssemblyName.Name, sdkAssemblyName.Version.ToString(), null, [], [], [], [], false));

        return module;
    }

    public static async Task<List<Artifact>> GetEmbeddedModuleArtifacts()
    {
        var queryResult = await GetEmbeddedModuleArtifactQueryResult();

        return queryResult.Results;
    }

    public static async Task<JFrogQueryResult> GetEmbeddedModuleArtifactQueryResult()
    {
        var resource = "jfrog-module-artifacts.json";
        var assembly = Assembly.GetAssembly(typeof(TestFactory));
        await using var stream = assembly!.GetManifestResourceStream($"{ModuleResourceNamespace}.{resource}");
        var queryResult = await JsonSerializer.DeserializeAsync<JFrogQueryResult>(stream!, options: ModuleSerializerOptions.GetOptions());

        return queryResult ?? throw new InvalidOperationException($"Failed to load embedded resource {resource}");
    }

    public static Task<List<ModuleMetadata>> GetEmbeddedModuleMetadata(bool includePreReleases = true)
    {
        var resources = new List<string> {
            ClusterManagementMetadataResource,  // ClusterManagement            
            DataCollectionWizardMetadataResource    // DataCollectionWizard
        };

        if (includePreReleases)
            resources.Add(ClusterManagementCiMetadataResource); // ClusterManagement CI

        return DeserializeFromEmbeddedResources<ModuleMetadata>(resources, ModuleSerializerOptions.GetOptions());
    }

    private static async Task<List<T>> DeserializeFromEmbeddedResources<T>(IEnumerable<string> resourceNames, JsonSerializerOptions? options = null)
    {
        var result = new List<T>();
        var assembly = Assembly.GetAssembly(typeof(TestFactory));

        foreach (var resourceName in resourceNames)
        {
            await using var stream = assembly!.GetManifestResourceStream($"{ModuleResourceNamespace}.{resourceName}");
            var response = await JsonSerializer.DeserializeAsync<T>(stream!, options: options);
            Assert.NotNull(response);
            result.Add(response);
        }

        return result;
    }

    internal sealed class ArtifactQueryResult : IArtifactQueryResult
    {
        [JsonIgnore]
        public IReadOnlyCollection<IArtifact> Artifacts => WrapResults;

        [JsonIgnore]
        public IReadOnlyCollection<IArtifactQueryRange>? Ranges => WrapRanges;

        [JsonIgnore]
        public IReadOnlyCollection<IArtifactQueryError>? Errors => WrapErrors;

        public List<Artifact> WrapResults { get; set; } = [];

        public List<ArtifactQueryRange>? WrapRanges { get; set; }

        public List<ArtifactQueryError>? WrapErrors { get; set; }
    }

    internal sealed class ArtifactQueryError : IArtifactQueryError
    {
        public required string Source { get; set; }

        public required ErrorInfo Error { get; set; }
    }

    internal sealed class JFrogQueryResult
    {
        [JsonPropertyName("results")]
        public List<Artifact> Results { get; set; } = [];

        [JsonPropertyName("range")]
        public ArtifactQueryRange? Range { get; set; }
    }

    [DebuggerDisplay("Repo = {Repo,nq}, Path = {Path,nq}, Name = {Name,nq}")]
    internal sealed class Artifact : IArtifact
    {
        /// <inheritdoc />
        public IArtifactChecksum? Checksum { get; set; }

        [JsonPropertyName("modified")]
        /// <inheritdoc />
        public DateTimeOffset? Modified { get; set; }

        [JsonPropertyName("name")]
        /// <inheritdoc />
        public required string Name { get; set; }

        [JsonPropertyName("path")]
        /// <inheritdoc />
        public required string Path { get; set; }

        [JsonPropertyName("repo")]
        /// <inheritdoc />
        public string Repository { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        /// <inheritdoc />
        public long? Size { get; set; }

        [JsonPropertyName("type")]
        /// <inheritdoc />
        public ArtifactKind Kind { get; set; } = ArtifactKind.File;

        public string SourceKey { get; set; } = string.Empty;
    }

    [DebuggerDisplay("Start = {StartPosition,nq}, EndPosition = {EndPosition,nq}, Total = {Total,nq}")]
    internal sealed class ArtifactQueryRange : IArtifactQueryRange
    {
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public int Total { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
