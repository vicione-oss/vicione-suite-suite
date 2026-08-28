using System.Globalization;
using System.Text.RegularExpressions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Core.Shared.EnvironmentOverrides.Commands;
using Core.Shared.EnvironmentOverrides.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Instance;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

internal sealed partial class EnvironmentOverridesControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<EnvironmentOverridesControlPanelState>,
    IEventConsumer<EnvironmentOverridesChanged>, IEventConsumer<SetEnvironmentOverridesError>
{
    private readonly IInstanceInformationProvider _instanceInformationProvider;

    public EnvironmentOverridesControlPanelSaveHandler(IUiMediator mediator,
        IInstanceInformationProvider instanceInformationProvider) : base(mediator)
    {
        _instanceInformationProvider = instanceInformationProvider;

        Register<EnvironmentOverridesChanged>();
        Register<SetEnvironmentOverridesError>();
    }

    public override async Task<ISaveResult> Save(EnvironmentOverridesControlPanelState state,
        CancellationToken cancellationToken)
    {
        if (state.LoadError is not null)
            return new SaveErrorResult(Localization.EnvironmentOverridesControlPanel.SaveBlockedByLoadFailureError);

        var candidates = GetNonEmptyEntries(state);

        var validationError = Validate(candidates);
        if (validationError is not null)
            return new SaveErrorResult(validationError);

        state.Entries = candidates;

        var overrides = candidates.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        var command = new SetEnvironmentOverrides(overrides);

        var result = await SendAndWaitForCompletion(command, _instanceInformationProvider.Local.Id, cancellationToken);

        if (result is SaveSuccessResult)
            state.RestartRequired = true;

        return result;
    }

    private static List<EnvironmentOverrideEntry> GetNonEmptyEntries(EnvironmentOverridesControlPanelState state) =>
    [
        .. state.Entries
            .Select(e => e with { Name = e.Name.Trim() })
            .Where(e => e.Name.Length > 0 || !string.IsNullOrWhiteSpace(e.Value))
    ];

    public Task Consume(ClientContext<EnvironmentOverridesChanged> context, CancellationToken cancellationToken)
    {
        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<SetEnvironmentOverridesError> context, CancellationToken cancellationToken)
    {
        CompleteWithError(context.Message.CorrelationId, context.Message.Error);

        return Task.CompletedTask;
    }

    private static string? Validate(IReadOnlyList<EnvironmentOverrideEntry> candidates)
    {
        if (candidates.Any(e => e.Name.Length == 0))
            return Localization.EnvironmentOverridesControlPanel.ValueWithoutKeyError;

        var invalidKey = FindPotentialInvalidKey(candidates);
        if (invalidKey is not null)
            return string.Format(CultureInfo.CurrentCulture,
                Localization.EnvironmentOverridesControlPanel.InvalidKeyError,
                invalidKey);

        var duplicateKeys = FindPotentialDuplicatedKeys(candidates);
        if (duplicateKeys.Count > 0)
            return string.Format(CultureInfo.CurrentCulture,
                Localization.EnvironmentOverridesControlPanel.DuplicateKeyError,
                string.Join(", ", duplicateKeys));

        var keyWithInvalidValue = FindPotentialOverrideWithNullCharacter(candidates);
        if (keyWithInvalidValue is not null)
            return string.Format(CultureInfo.CurrentCulture,
                Localization.EnvironmentOverridesControlPanel.InvalidValueError,
                keyWithInvalidValue);

        return null;
    }

    private static string? FindPotentialInvalidKey(IReadOnlyList<EnvironmentOverrideEntry> candidates) =>
            candidates.Select(e => e.Name)
                .FirstOrDefault(n => !KeyRegex().IsMatch(n));
    private static List<string> FindPotentialDuplicatedKeys(IReadOnlyList<EnvironmentOverrideEntry> candidates) =>
    [.. candidates
        .GroupBy(e => e.Name, StringComparer.Ordinal)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)];

    private static string? FindPotentialOverrideWithNullCharacter(IReadOnlyList<EnvironmentOverrideEntry> candidates) =>
            candidates
                .Where(e => e.Value.Contains('\0'))
                .Select(e => e.Name)
                .FirstOrDefault();

    [GeneratedRegex(Core.Shared.EnvironmentOverrides.Constants.KeyPattern)]
    private static partial Regex KeyRegex();
}
