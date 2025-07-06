namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a contract for capturing a snapshot of an object's current state.
/// </summary>
/// <typeparam name="T">
/// The type representing the snapshot result. It must be a reference type.
/// </typeparam>
public interface ISnapshot<T> where T : class
{
    /// <summary>
    /// Captures and returns a snapshot of the current state of the object.
    /// </summary>
    /// <returns>
    /// An instance of <typeparamref name="T"/> representing the snapshot.
    /// </returns>
    T GetSnapshot();
}