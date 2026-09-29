namespace Core.OS.Persistence.Extensions;

internal static partial class IServiceProviderExtensions
{
    extension(IServiceProvider services)
    {
        /// <summary>
        /// Calls every registered <see cref="IReplicationObserver"/>; a failing observer is logged instead of failing the
        /// replication that notified it.
        /// </summary>
        internal void NotifyReplicationObservers(ILogger logger, Action<IReplicationObserver> notify)
        {
            foreach (var observer in services.GetServices<IReplicationObserver>())
            {
                try
                {
                    notify(observer);
                }
                catch (Exception exception)
                {
                    LogObserverFailed(logger, exception, observer.GetType().Name);
                }
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "Replication observer {Observer} failed")]
    private static partial void LogObserverFailed(ILogger logger, Exception exception, string observer);
}
