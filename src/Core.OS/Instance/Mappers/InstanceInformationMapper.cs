using Core.OS.Instance.Commands;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Contracts;
using Sdk.Instance;

namespace Core.OS.Instance.Mappers;

internal static class InstanceInformationMapper
{
    /// <summary>
    /// Add new properties here only if the field can be *seeded* from startup config on first boot. Follow
    /// the existing conditional pattern(`if (!string.IsNullOrEmpty(...)) info.X = ...`) —
    /// don't overwrite a good default with an empty config value unless it is required to change when the config is updated.
    /// </summary>
    /// <param name="options">The startup configuration to seed defaults from.</param>
    /// <param name="instanceId">The identifier to assign to the new instance.</param>
    /// <returns>A new <see cref="InstanceInformation"/> seeded from <paramref name="options"/>.</returns>
    public static InstanceInformation ToInstanceInformation(this InstanceOptions options, Guid instanceId)
    {
        var info = new InstanceInformation
        {
            Id = instanceId,
            Type = options.Type,
            SerialNumber = options.SerialNumber ?? instanceId.ToString("N")
        };

        if (!string.IsNullOrEmpty(options.NamePreload))
            info.Name = options.NamePreload;

        if (!string.IsNullOrEmpty(options.DescriptionPreload))
            info.Description = options.DescriptionPreload;

        if (!string.IsNullOrEmpty(options.FormattedName))
            info.FormattedName = options.FormattedName;

        return info;
    }

    /// <summary>
    /// Creates new <see cref="RegisterInstance"/> command with the specified parameters.
    /// Add new fields here too.
    /// </summary>
    /// <param name="info">The instance information to populate the command from.</param>
    /// <param name="installedModules">The modules currently loaded on this instance.</param>
    /// <param name="configuration">The configuration key/value pairs to include in the registration.</param>
    /// <param name="lastAppliedSequences">The last applied change-set sequence per context, used to resume sync.</param>
    /// <returns>A <see cref="RegisterInstance"/> command ready to be sent.</returns>
    public static RegisterInstance ToRegisterInstanceCommand(
        this IInstanceInformation info,
        IReadOnlyCollection<string> installedModules,
        List<KeyValuePair<string, string?>> configuration,
        Dictionary<string, long> lastAppliedSequences) =>
        new()
        {
            InstanceId = info.Id,
            Type = info.Type,
            Name = info.Name,
            FormattedName = info.FormattedName,
            Description = info.Description,
            SerialNumber = info.SerialNumber,
            SystemType = info.SystemType,
            SdkVersion = info.SdkVersion,
            Version = info.Version,
            BranchName = info.BranchName,
            InstalledModules = [.. installedModules],
            Configuration = configuration,
            LastAppliedSequences = lastAppliedSequences
        };

    /// <summary>
    /// Builds a brand-new DB row for a never-before-seen instance. Map the field from the command.
    /// </summary>
    /// <param name="command">The registration command received for a previously unknown instance.</param>
    /// <param name="registrationTime">The timestamp to record as both the first and last registration time.</param>
    /// <returns>A new <see cref="InstanceInformation"/> row ready to be added to the database.</returns>
    public static InstanceInformation ToInstanceInformation(this RegisterInstance command, DateTimeOffset registrationTime)
    {
        var info = new InstanceInformation
        {
            Id = command.InstanceId,
            Type = command.Type,
            InstalledModules = command.InstalledModules,
            Name = command.Name,
            Description = command.Description,
            SerialNumber = command.SerialNumber,
            SystemType = command.SystemType,
            SdkVersion = command.SdkVersion,
            FirstTimeRegistered = registrationTime,
            LastRegistered = registrationTime,
            Version = command.Version,
            BranchName = command.BranchName
        };

        if (command.FormattedName is not null)
            info.FormattedName = command.FormattedName;

        return info;
    }

    /// <summary>
    /// Updates an *existing* DB row on every re-registration. Map new fields
    /// here too — but decide deliberately whether it should be unconditionally overwritten
    /// (like `Type`, `Version`, `InstalledModules`) or only overwritten when present(like
    ///  `FormattedName`, which uses `if (command.FormattedName is not null)`).
    /// `FirstTimeRegistered` is intentionally <b>not</b> touched here — it must survive
    /// across re - registrations.Follow that pattern for any other "set-once" field.
    /// </summary>
    /// <param name="command">The registration command containing the latest values for the instance.</param>
    /// <param name="existing">The existing DB row to update in place.</param>
    /// <param name="registrationTime">The timestamp to record as the new <see cref="InstanceInformation.LastRegistered"/> value.</param>
    public static void ApplyTo(this RegisterInstance command, InstanceInformation existing, DateTimeOffset registrationTime)
    {
        existing.InstalledModules = command.InstalledModules;
        existing.Type = command.Type;
        existing.Name = command.Name;
        existing.Description = command.Description;
        existing.SerialNumber = command.SerialNumber;
        existing.SystemType = command.SystemType;
        existing.SdkVersion = command.SdkVersion;
        existing.LastRegistered = registrationTime;
        existing.Version = command.Version;
        existing.BranchName = command.BranchName;

        if (command.FormattedName is not null)
            existing.FormattedName = command.FormattedName;
    }

    /// <summary>
    /// Used to refresh the in-memory
    /// cache other modules read from(e.g.on <see cref="RegisterInstanceConsumer"/> completion, or at
    /// startup before the first registration completes). <b>Map new fields here as well.</b>
    /// Note `InRecoveryMode` is <b>deliberately excluded</b> from this method and set
    /// separately by the caller — if the new field is similarly "local-runtime-only" and
    /// must never be clobbered by a value copied from elsewhere, follow that pattern
    /// instead of adding it to `ApplyTo`.
    /// </summary>
    /// <param name="source">The instance information to copy from (e.g. the local in-memory cache or a DB row).</param>
    /// <param name="target">The instance information to update in place.</param>
    public static void ApplyTo(this IInstanceInformation source, InstanceInformation target)
    {
        target.LastRegistered = source.LastRegistered;
        target.FirstTimeRegistered = source.FirstTimeRegistered;
        target.Name = source.Name;
        target.Description = source.Description;
        target.FormattedName = source.FormattedName;
        target.SerialNumber = source.SerialNumber;
        target.SystemType = source.SystemType;
        target.InstalledModules = [.. source.InstalledModules];
        target.Type = source.Type;
        target.Version = source.Version;
        target.SdkVersion = source.SdkVersion;
        target.BranchName = source.BranchName;
    }
}
