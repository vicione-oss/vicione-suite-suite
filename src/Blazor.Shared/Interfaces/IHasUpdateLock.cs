namespace Blazor.Shared.Interfaces;

/// <summary>
/// Describes an instance with an update lock
/// </summary>
internal interface IHasUpdateLock
{
    /// <summary>
    /// Counts the number of times <see cref="BeginUpdate"/> was called without a corresponding call to <see cref="EndUpdate"/>.
    /// 
    /// The instance will not raise any event when <see cref="UpdateLock"/> is greater than 0.
    /// </summary>
    int UpdateLock { get; }

    /// <summary>
    /// Call this method to begin an update cycle.
    /// 
    /// This increments the <see cref="UpdateLock"/>.
    /// 
    /// Make sure to have a corresponding call to <see cref="EndUpdate"/> to end the update cycle.
    /// </summary>
    void BeginUpdate();

    /// <summary>
    /// Call this method to end an update cycle.
    /// 
    /// This decrements the <see cref="UpdateLock"/>.
    /// </summary>
    void EndUpdate();
}
