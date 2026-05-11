using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance.Extensions;

internal static class BackupSummaryExtensions
{
    public static IEnumerable<string> GetErrorMessages(this BackupSummary summary)
    {
        var errors = new List<string>();

        if (!string.IsNullOrEmpty(summary.Error))
            errors.Add(summary.Error);

        if (summary.SystemModule is not null)
        {
            if (!string.IsNullOrEmpty(summary.SystemModule.Error))
                errors.Add(summary.SystemModule.Error);

            if (summary.SystemModule.Databases is not null)
            {
                errors.AddRange(summary.SystemModule.Databases
                    .Where(db => !string.IsNullOrEmpty(db.Error))
                    .Select(k => k.Error!));
            }
        }

        if (summary.SystemConfiguration is not null && !string.IsNullOrEmpty(summary.SystemConfiguration.Error))
            errors.Add(summary.SystemConfiguration.Error);

        // errors occurred on module backup
        errors.AddRange(summary.Modules.Where(k => !string.IsNullOrEmpty(k.Error)).Select(k => k.Error!));

        errors.AddRange(summary.Modules.SelectMany(k => k.Databases ?? [])
            .Where(db => !string.IsNullOrEmpty(db.Error))
            .Select(k => k.Error!));

        return errors;
    }
}
