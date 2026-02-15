# Thread Dispatching

This page covers the public thread-dispatching surface in `Nalix.SDK`.

## Source mapping

- `src/Nalix.SDK/IThreadDispatcher.cs`
- `src/Nalix.SDK/InlineDispatcher.cs`

## Main types

- `IThreadDispatcher`
- `InlineDispatcher`

## IThreadDispatcher

`IThreadDispatcher` is the minimal abstraction used when SDK code needs to marshal work onto a UI or main thread.

## Basic usage

```csharp
IThreadDispatcher dispatcher = new InlineDispatcher();

dispatcher.Post(() =>
{
    Console.WriteLine("dispatched");
});
```

## InlineDispatcher

`InlineDispatcher` is the default no-switch implementation. It runs the action immediately on the current thread.

Use it when:

- you are not in a UI app
- you are testing
- you do not need thread marshalling

## Related APIs

- [SDK Overview](./index.md)
- [TCP Session](./tcp-session.md)
