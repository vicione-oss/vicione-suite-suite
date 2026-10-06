using AutoFixture;
using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Core.Shared.Instance.Contracts;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances.Models;

public sealed class InstanceInformationModelTests
{
    [Fact]
    public void Should_copy_all_instance_information_properties()
    {
        // Arrange
        // AutoFixture fills properties added later too; its first enum value is the default.
        var info = new Fixture().Build<InstanceInformation>()
            .With(i => i.Type, InstanceType.Master)
            .With(i => i.InRecoveryMode, true)
            .Create();

        // Act
        var model = new InstanceInformationModel(info);

        // Assert
        // The model is the expectation, so its members are read through the interface, explicit implementations included.
        info.Should().BeEquivalentTo<IInstanceInformation>(model);
    }
}
