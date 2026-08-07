using System.IO.Abstractions.TestingHelpers;
using Core.OS.Diagnostics;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Sdk.Instance;

namespace Core.OS.Tests.Diagnostics;

public class SuiteOtelResourceTests
{
    private static readonly Guid _instanceId = Guid.Parse("2f0f1a3c-6f5c-4c4a-9c4e-1f2b3a4d5e6f");

    private static InstanceOptions CreateInstanceOptions()
        => new()
        {
            HomeDirectory = "AppData",
            CacheDirectory = "Cache",
            BackupDirectory = "Backup",
            Type = InstanceType.Standalone,
            IdPreload = _instanceId,
        };

    public sealed class GetResourceAttributes : SuiteOtelResourceTests
    {
        /// <summary>
        /// Logs and traces must report the same <c>service.instance.id</c>, so the id file has to be
        /// written before the logger is created - which is what <c>Program.cs</c> guarantees by calling
        /// <see cref="IFileSystemExtensions.EnsureInstanceIdFile"/> ahead of <c>ConfigureLogging</c>.
        /// </summary>
        [Fact]
        public void Should_report_the_instance_id_ensured_at_startup()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var instanceOptions = CreateInstanceOptions();
            fileSystem.EnsureInstanceIdFile(instanceOptions);

            // Act
            var attributes = SuiteOtelResource.GetResourceAttributes(new OtelExporterOptions(), fileSystem, instanceOptions);

            // Assert
            attributes.Should().ContainKey("service.instance.id")
                .WhoseValue.Should().Be(_instanceId.ToString());
        }

        [Fact]
        public void Should_omit_the_instance_id_when_the_id_file_does_not_exist()
        {
            // Arrange
            var instanceOptions = CreateInstanceOptions();

            // Act
            var attributes = SuiteOtelResource.GetResourceAttributes(new OtelExporterOptions(), new MockFileSystem(), instanceOptions);

            // Assert
            attributes.Should().NotContainKey("service.instance.id");
            attributes.Should().ContainKey("service.name");
        }
    }
}
