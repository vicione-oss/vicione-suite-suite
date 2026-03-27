using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record InstallSuiteVersionStarted(string? Message, bool WithWarnings) : ResponseEventBase;
