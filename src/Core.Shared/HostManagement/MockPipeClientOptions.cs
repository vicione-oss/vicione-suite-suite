using HostManagement.Shared.Contracts;

namespace Core.Shared.HostManagement;

public sealed class MockPipeClientOptions
{
    public bool Enabled { get; set; }
    public MockPipeClientDataSource DataSource { get; set; }
    public SystemConfiguration? SystemConfiguration { get; set; }
    public string? SystemConfigurationJsonFile { get; set; }
}
