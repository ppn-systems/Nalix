# Project Setup Guide

This guide describes how to structure a production-grade Nalix solution using a multi-project architecture.

## Recommended Solution Structure

A typical Nalix solution consists of three main projects to ensure code sharing and clean separation.

```text
MySolution/
  ├── MySolution.sln
  ├── Directory.Build.props      <-- Shared versions & build rules
  ├── src/
  │   ├── MyProject.Contracts/  <-- Shared POCOs & Attributes
  │   ├── MyProject.Server/     <-- Server-side Logic & Hosting
  │   └── MyProject.Client/     <-- Client-side SDK & Integration
  └── tests/
      └── MyProject.Tests/      <-- Unit & Integration Tests
```

---

## 1. Directory.Build.props

Use a `Directory.Build.props` file in the root directory to manage package versions and build rules centrally. This prevents version drift between your server and client.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NalixVersion>$(VersionFromYourReleasePlan)</NalixVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- Common analyzers for high-performance code -->
    <PackageReference Include="Nalix.Analyzers" Version="$(NalixVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

---

## 2. Shared Contracts (`MyProject.Contracts`)

The contracts project must be a `Class Library` referenced by both the Server and the Client.

```bash
dotnet new classlib -n MyProject.Contracts
dotnet add MyProject.Contracts package Nalix.Abstractions --version $NalixVersion
dotnet add MyProject.Contracts package Nalix.Framework --version $NalixVersion
```

### Key Rules

- Annotate all packets with `[SerializePackable]`.
- Use `[SerializeOrder]` for explicit field layout.
- Define constants for OpCodes in the packet classes.

```csharp
[SerializePackable]
public sealed class JoinRequest : PacketBase<JoinRequest>
{
    public const ushort OpCodeValue = 0x3001;

    public string Username { get; set; } = string.Empty;

    public JoinRequest() => OpCode = OpCodeValue;
}
```

---

## 3. Server Setup (`MyProject.Server`)

The server project should be a `Console Application` or `Worker Service` that uses the `Nalix.Hosting` package.

```bash
dotnet new console -n MyProject.Server
dotnet add MyProject.Server reference MyProject.Contracts
dotnet add MyProject.Server package Nalix.Hosting --version $NalixVersion
```

### Best Practice: `NetworkApplication`

Use the fluent builder to assemble your server layers:

```csharp
using Nalix.Hosting;
using Nalix.Network.Options;

using var app = NetworkApplication.CreateBuilder()
    .Configure<NetworkSocketOptions>(options => options.Port = 57206)
    .ScanPackets<JoinRequest>() // Scans the marker assembly for contracts
    .ScanHandlers<MyHandlers>() // Scans the marker assembly for controllers
    .BindTcp<DefaultProtocol>().Bind()
    .Build();

await app.RunAsync();
```

### Builder Semantics

- `ScanPackets<TMarker>()` scans the assembly that contains `TMarker`.
- `ScanHandlers<TMarker>()` scans the assembly that contains `TMarker`.
- `AddHandler<THandler>()` registers one handler type directly.
- `ConfigureConnectionHub(...)` and `ConfigureBufferPoolManager(...)` are optional, but make host wiring explicit.
- `ConfigureBufferPoolManager(...)` is still worth calling explicitly for high-throughput servers, even though `NetworkApplicationBuilder` will create a default manager when omitted.
- If you only need the standard "forward to dispatch" behavior, use `DefaultProtocol` directly. `src/Nalix.Hosting/DefaultProtocol.cs` already sets `IsAccepting = true` and forwards `args.Lease` to `IPacketDispatch.HandlePacket(...)`.
- During activation, `src/Nalix.Hosting/NetworkApplicationBuilder.cs` applies `Configure<TOptions>(...)`, ensures `IConnectionHub` and `BufferPoolManager` exist, initializes `HandshakeHandlers`, then creates dispatch and listeners.

---

## 4. Client Setup (`MyProject.Client`)

The client project should use the `Nalix.SDK` package.

```bash
dotnet new console -n MyProject.Client
dotnet add MyProject.Client reference MyProject.Contracts
dotnet add MyProject.Client package Nalix.SDK --version $NalixVersion
```

---

## 5. Version Management

Nalix releases updates synchronized across all core packages. When upgrading:

1. Update `NalixVersion` in `Directory.Build.props`.
2. Clear the `obj` and `bin` folders if source-generated contracts have changed.
3. Run `dotnet build` to ensure all projects are aligned.

## Recommended Next Steps

- [Quickstart](../../quickstart.md)
- [Architecture Details](../../concepts/fundamentals/architecture.md)
- [Packet System Overview](../../concepts/fundamentals/packet-system.md)
