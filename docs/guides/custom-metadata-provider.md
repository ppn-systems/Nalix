# Custom Metadata Provider

This guide shows how to attach your own metadata to handler methods and read it later from middleware or handlers.

Use this when built-in attributes are not enough and you want your own convention.
It works with built-in packets and custom packet types alike.

## What a metadata provider does

An `IPacketMetadataProvider` is called while Nalix.Network builds metadata for handler methods.

It can:

- inspect the handler method
- look for your custom attributes
- add derived metadata into `PacketMetadataBuilder`

## Example goal

We want a custom attribute called `PacketTenantAttribute` so middleware can enforce tenant routing rules.

## Step 1. Create a custom attribute

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PacketTenantAttribute : Attribute
{
    public string Tenant { get; }

    public PacketTenantAttribute(string tenant) => Tenant = tenant;
}
```

## Step 2. Create a metadata provider

```csharp
using System.Reflection;
using Nalix.Runtime.Dispatching;

public sealed class SampleTenantMetadataProvider : IPacketMetadataProvider
{
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        PacketTenantAttribute? attr = method.GetCustomAttribute<PacketTenantAttribute>();
        if (attr is null)
            return;

        builder.Add(attr);
    }
}
```

## Step 3. Register the provider at startup

```csharp
PacketMetadataProviders.Register(new SampleTenantMetadataProvider());
```

Do this before building your handler catalog / dispatch configuration.

## Step 4. Use the attribute on handlers

```csharp
[PacketController("SampleInvoiceHandlers")]
public sealed class SampleInvoiceHandlers
{
    [PacketOpcode(0x2201)]
    [PacketTenant("billing")]
    public ValueTask<string> GetInvoice(PacketContext<InvoicePacket> request)
        => ValueTask.FromResult("invoice");
}
```

## Step 5. Read the custom metadata in middleware

```csharp
[MiddlewareOrder(-10)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class TenantGuardMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket
{
    public async ValueTask InvokeAsync(
        IPacketContext<TPacket> context,
        Func<CancellationToken, ValueTask> next)
    {
        PacketTenantAttribute? tenant =
            context.Attributes.GetCustomAttribute<PacketTenantAttribute>();

        if (tenant is null)
        {
            await next(context.CancellationToken);
            return;
        }

        // Replace with your own tenant resolution logic
        bool allowed = context.Connection.Level >= PermissionLevel.USER;

        if (!allowed)
        {
            await context.Connection.SendAsync(
                ControlType.FAIL,
                ProtocolReason.PERMISSION_DENIED,
                ProtocolAdvice.RETRY_LATER);
            return;
        }

        await next(context.CancellationToken);
    }
}
```

## Mental model

```mermaid
flowchart LR
    A["Handler method"] --> B["Custom attribute"]
    B --> C["IPacketMetadataProvider"]
    C --> D["PacketMetadataBuilder.Add(...)"]
    D --> E["PacketMetadata"]
    E --> F["PacketContext.Attributes"]
    F --> G["Middleware / handler logic"]
```

## Good use cases

Custom metadata providers are great for:

- tenant routing
- feature flags
- internal audit categories
- product/region policy tags
- controller naming conventions that should become runtime metadata

## Keep it simple

Best practice:

- add attributes, not mutable state
- keep provider logic deterministic
- avoid service lookups inside the provider unless absolutely necessary
- do policy enforcement later in middleware, not in metadata building

## Related pages

- [Packet Metadata](../api/runtime/routing/packet-metadata.md)
- [Custom Middleware End-to-End](./custom-middleware-end-to-end.md)
