using Notio.Http.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Notio.Http.Interfaces;

/// <summary>
/// A common interface for Flurl.Http objects that contain event handlers.
/// </summary>
public interface IEventHandlerContainer
{
    /// <summary>
    /// A collection of Flurl event handlers.
    /// </summary>
    IList<(HttpEventType EventType, INotioEventHandler Handler)> EventHandlers { get; }
}

/// <summary>
/// Fluent extension methods for tweaking HttpSettings
/// </summary>
public static class EventHandlerContainerExtensions
{
    /// <summary>
    /// Adds an event handler that is invoked when a BeforeCall event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T BeforeCall<T>(this T obj, Action<NotioCall> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.BeforeCall, act);

    /// <summary>
    /// Adds an asynchronous event handler that is invoked when a BeforeCall event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T BeforeCall<T>(this T obj, Func<NotioCall, Task> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.BeforeCall, act);

    /// <summary>
    /// Adds an event handler that is invoked when an AfterCall event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T AfterCall<T>(this T obj, Action<NotioCall> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.AfterCall, act);

    /// <summary>
    /// Adds an asynchronous event handler that is invoked when an AfterCall event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T AfterCall<T>(this T obj, Func<NotioCall, Task> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.AfterCall, act);

    /// <summary>
    /// Adds an event handler that is invoked when an OnError event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T OnError<T>(this T obj, Action<NotioCall> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.OnError, act);

    /// <summary>
    /// Adds an asynchronous event handler that is invoked when an OnError event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T OnError<T>(this T obj, Func<NotioCall, Task> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.OnError, act);

    /// <summary>
    /// Adds an event handler that is invoked when an OnRedirect event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T OnRedirect<T>(this T obj, Action<NotioCall> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.OnRedirect, act);

    /// <summary>
    /// Adds an asynchronous event handler that is invoked when an OnRedirect event is fired.
    /// </summary>
    /// <returns>This event handler container.</returns>
    public static T OnRedirect<T>(this T obj, Func<NotioCall, Task> act) where T : IEventHandlerContainer => AddHandler(obj, HttpEventType.OnRedirect, act);

    private static T AddHandler<T>(T obj, HttpEventType eventType, Action<NotioCall> act) where T : IEventHandlerContainer
    {
        obj.EventHandlers.Add((eventType, new NotioEventHandler.FromAction(act)));
        return obj;
    }

    private static T AddHandler<T>(T obj, HttpEventType eventType, Func<NotioCall, Task> act) where T : IEventHandlerContainer
    {
        obj.EventHandlers.Add((eventType, new NotioEventHandler.FromAsyncAction(act)));
        return obj;
    }
}
