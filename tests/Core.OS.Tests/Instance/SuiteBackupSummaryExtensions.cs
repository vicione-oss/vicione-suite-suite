using Core.Shared.Persistence.Contracts;
using AwesomeAssertions;
using Sdk.Instance;

namespace Core.OS.Tests.Instance;

public static class SuiteBackupSummaryExtensions
{
    public static void ShouldContainInstanceInfos(this BackupSummary summary, IInstanceInformation localInstance)
    {
        summary.InstanceId.Should().Be(localInstance.Id);
        summary.Name.Should().Be(localInstance.Name);
        summary.SuiteVersion.Should().Be(localInstance.Version);
        summary.SdkVersion.Should().Be(localInstance.SdkVersion);
        summary.InstanceType.Should().Be(localInstance.Type.ToString());
    }
}
