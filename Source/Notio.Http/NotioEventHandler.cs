using Notio.Http.Enums;
using Notio.Http.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Http;

/// <summary>
/// Default implementation of INotioEventHandler. Typically, you should override Handle or HandleAsync.
/// Both are noops by default.
/// </summary>
public class NotioEventHandler : INotioEventHandler
{
    /// <summary>
    /// Override to define an action to take when a Flurl event fires. Prefer HandleAsync if async calls need to be made.
    /// </summary>
    public virtual void Handle(HttpEventType eventType, NotioCall call) { }

    /// <summary>
    /// Override to define an asynchronous action to take when a Flurl event fires.
    /// </summary>
    public virtual Task HandleAsync(HttpEventType eventType, NotioCall call) => Task.CompletedTask;

    internal class FromAction : NotioEventHandler
    {
        private readonly Action<NotioCall> _act;
        public FromAction(Action<NotioCall> act) => _act = act;
        public override void Handle(HttpEventType eventType, NotioCall call) => _act(call);
    }

    internal class FromAsyncAction : NotioEventHandler
    {
        private readonly Func<NotioCall, Task> _act;
        public FromAsyncAction(Func<NotioCall, Task> act) => _act = act;
        public override Task HandleAsync(HttpEventType eventType, NotioCall call) => _act(call);
    }
}
