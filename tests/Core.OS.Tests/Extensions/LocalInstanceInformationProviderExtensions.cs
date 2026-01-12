using Core.OS.Instance;
using Core.Shared;
using NSubstitute;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;

namespace Core.OS.Tests.Extensions;

internal static class LocalInstanceInformationProviderExtensions
{
    public static void SetupLocalInstanceInformation(this ILocalInstanceInformationProvider providerMock, InstanceType instanceType = InstanceType.Standalone, Guid? guid = null)
    {
        var info = new TestInstanceInformation
        {
            Id = instanceType == InstanceType.Master ? Constants.MasterInstanceGuid : (guid ?? Guid.NewGuid()),
            Type = instanceType,
            Name = "TestInstance",
            Description = "Description of instance"
        };

        providerMock.Local
            .Returns(info);

        providerMock.LoadedModules
            .Returns([ModuleIdResolver.ResolveId<TestBackendModule>()]);
    }
}
