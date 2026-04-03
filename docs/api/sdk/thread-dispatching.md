# Thread Dispatching

This page covers the small thread-dispatching surface in `Nalix.SDK`.

## Source mapping

- `src/Nalix.SDK/IThreadDispatcher.cs`
- `src/Nalix.SDK/InlineDispatcher.cs`

## Main types

- `IThreadDispatcher`
- `InlineDispatcher`

## Public members at a glance

| Type | Public members |
|---|---|
| `IThreadDispatcher` | `Post(Action)` |
| `InlineDispatcher` | `Post(Action)` |

## IThreadDispatcher

`IThreadDispatcher` is the minimal abstraction used when SDK code needs to marshal work onto a UI or main thread.

The interface stays intentionally small so it works well across different app models.

Common uses:

- UI updates from background receive loops
- marshaling state changes back to a main thread
- keeping platform-specific dispatch logic outside core networking code

## Basic usage

```csharp
IThreadDispatcher dispatcher = new InlineDispatcher();

dispatcher.Post(() =>
{
    Console.WriteLine("dispatched");
});
```

## Platform examples

### .NET MAUI

```csharp
public sealed class MauiThreadDispatcher : IThreadDispatcher
{
    public void Post(Action action)
        => MainThread.BeginInvokeOnMainThread(action);
}
```

Use this when you need to update UI-bound state from a background callback.

### Unity

```csharp
public sealed class UnityThreadDispatcher : IThreadDispatcher
{
    public void Post(Action action)
        => UnityMainThreadDispatcher.Enqueue(action);
}
```

Use this when your networking code runs off the Unity main thread and must touch scene state safely.

### WinForms or WPF

```csharp
public sealed class UiThreadDispatcher : IThreadDispatcher
{
    private readonly SynchronizationContext _context;

    public UiThreadDispatcher(SynchronizationContext context) => _context = context;

    public void Post(Action action) => _context.Post(_ => action(), null);
}
```

Use this when you want to marshal work back to the UI thread without coupling SDK code to a specific framework.

## Common pitfalls

- touching UI objects directly from receive callbacks instead of marshaling through `Post(...)`
- assuming `InlineDispatcher` will switch threads for you; it does not
- keeping platform-specific UI code inside the networking layer instead of behind the dispatcher abstraction

## InlineDispatcher

`InlineDispatcher` is the default no-switch implementation. It runs the action immediately on the current thread.

Use it when:

- you are not in a UI app
- you are testing
- you do not need thread marshalling

## Related APIs

- [SDK Overview](./index.md)
- [TCP Session](./tcp-session.md)
- [Protocol String Extensions](./protocol-string-extensions.md)
