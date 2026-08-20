using System.IO.Abstractions;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Mappers;
using Core.OS.Modules;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Instance;

namespace Core.OS.Instance.Services;

internal sealed class LocalInstanceInformationProvider(IServiceProvider serviceProvider, IModuleHost moduleHost) :
    ILocalInstanceInformationProvider
{
    internal static bool RunningInRecoveryMode;

    private readonly Lock _infoLock = new();

    /// <summary>
    /// After suite has started the instance id file is not allowed to be modified -> once read it's static!
    /// </summary>
    private Guid? _localInstanceId;
    private InstanceInformation? _info;

    public IInstanceInformation Local
    {
        get
        {
            lock (_infoLock)
            {
                return _info ?? throw new InvalidOperationException("Local instance information is not yet initialized.");
            }
        }
    }
    public IReadOnlyCollection<string> LoadedModules => moduleHost.GetModules()
        .Select(m => m.ModuleKey.ModuleId)
        .OrderBy(id => id)
        .ToList()
        .AsReadOnly();

    public void UpdateLocal(IInstanceInformation info)
    {
        lock (_infoLock)
        {
            var localInstanceId = ReadLocalInstanceId();
            if (localInstanceId != info.Id)
                return;

            _info ??= new InstanceInformation
            {
                Id = info.Id
            };

            info.ApplyTo(_info);
            _info.InRecoveryMode = RunningInRecoveryMode;
        }
    }

    public Guid ReadLocalInstanceId()
    {
        if (_localInstanceId is null)
        {
            var fileSystem = serviceProvider.GetRequiredService<IFileSystem>();
            var instanceOptions = serviceProvider.GetRequiredService<IOptions<InstanceOptions>>().Value;

            var instanceId = fileSystem.ReadLocalInstanceId(instanceOptions)
                ?? throw new InvalidOperationException(
                    $"Instance id file '{fileSystem.GetLocalInstanceIdFilePath(instanceOptions)}' does not exist.");

            if (!Guid.TryParse(instanceId, out var parsedId))
                throw new InvalidOperationException($"Failed to parse instance id from file content: '{instanceId}'.");

            _localInstanceId = parsedId;
        }

        return _localInstanceId.Value;
    }
}
