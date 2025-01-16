using Notio.Http.Enums;
using System.Threading.Tasks;

namespace Notio.Http.Interfaces;

/// <summary>
/// Defines a handler for Flurl events such as BeforeCall, AfterCall, and OnError
/// </summary>
public interface INotioEventHandler
{
    /// <summary>
    /// Action to take when a Flurl event fires. Prefer HandleAsync if async calls need to be made.
    /// </summary>
    void Handle(HttpEventType eventType, NotioCall call);

    /// <summary>
    /// Asynchronous action to take when a Flurl event fires.
    /// </summary>
    Task HandleAsync(HttpEventType eventType, NotioCall call);
}
