using Sdk.Instance;
using Sdk.Modules;

namespace TestUiHost.Services;

/// <summary>
/// To test what happens if 2 Backend-Modules register implementations of the same interface 
/// </summary>
internal class TestInstanceInformationProvider : IInstanceInformationProvider
{
    public IInstanceInformation Local { get; } = new TestInstanceInformation
    {
        Id = Guid.NewGuid(),
        Type = InstanceType.Standalone,
        Name = ModuleIdResolver.ResolveId<TestUiHostBackend>(),
        InstalledModules = []
    };

    public Task<IReadOnlyCollection<ModuleMetadata>> GetInstalledModules(CancellationToken cancellationToken = default)
    {
        var x = Local.InstalledModules
            .Select(k => new ModuleMetadata { Name = k, Version = "0.0.1", MinSuiteSdkVersion = "1.0.0" })
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyCollection<ModuleMetadata>>(x);
    }

    public Task<List<IInstanceInformation>> GetInstancesInCluster(CancellationToken token)
        => Task.FromResult(new List<IInstanceInformation> { Local });

    private class TestInstanceInformation : IInstanceInformation
    {
        public Guid Id { get; init; }
        public InstanceType Type { get; init; }
        public string? Name { get; init; }
        public string FormattedName { get; set; } = "{ViciOne} Suite";
        public string? Description { get; set; }
        public string SerialNumber => Id.ToString("N");
        public string SystemType => "TestSystemType";
        public required IReadOnlyCollection<string> InstalledModules { get; init; }
        public DateTimeOffset? FirstTimeRegistered { get; set; }
        public DateTimeOffset? LastRegistered { get; set; }
        public string Version { get; set; } = "undefined";
        public string? BranchName { get; set; }
        public string SdkVersion { get; set; } = "undefined";

        public bool InRecoveryMode { get; set; }
    }
}
