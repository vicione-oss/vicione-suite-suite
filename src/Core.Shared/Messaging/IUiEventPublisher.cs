namespace Core.Shared.Messaging;

public interface IUiEventPublisher<in T>
{
    /// <summary>
    /// This method will be called by the message pipeline when an event with the <see cref="Sdk.Messaging.ForwardToUIAttribute"/> is consumed
    /// Implementation depends on the type of UI
    /// </summary>
    Task PublishUiEvent(T eventToPublish, Guid? correlationId, CancellationToken cancellationToken = default);
}
