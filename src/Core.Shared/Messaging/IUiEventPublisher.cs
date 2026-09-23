namespace Core.Shared.Messaging;

public interface IUiEventPublisher<in T>
{
    /// <summary>
    /// Called by the message pipeline when an event marked
    /// <see cref="Sdk.Messaging.ForwardToUIAttribute"/> is consumed.
    /// </summary>
    Task PublishUiEvent(T eventToPublish, Guid? correlationId, CancellationToken cancellationToken = default);
}
