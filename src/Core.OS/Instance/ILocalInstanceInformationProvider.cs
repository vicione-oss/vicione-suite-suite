using Sdk.Instance;

namespace Core.OS.Instance;

public interface ILocalInstanceInformationProvider
{
    /// <summary>
    /// Returns local instance information created on first access
    /// </summary>
    IInstanceInformation Local { get; }

    IReadOnlyCollection<string> LoadedModules { get; }

    /// <summary>
    /// Read instance id from file <see cref="Extensions.IFileSystemExtensions.InstanceIdFileName"/>
    /// Id gets cached reading the id successfully the first time
    /// </summary>
    Guid ReadLocalInstanceId();

    /// <summary>
    /// Update local instance information cache
    /// </summary>
    void UpdateLocal(IInstanceInformation info);
}
