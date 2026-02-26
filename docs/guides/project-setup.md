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
    <NalixVersion>11.8.0</NalixVersion>
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
dotnet add MyProject.Contracts package Nalix.Common --version 11.8.0
dotnet add MyProject.Contracts package Nalix.Framework --version 11.8.0
```

### Key Rules:
- Annotate all packets with `[SerializePackable]`.
- Use `[SerializeOrder]` for explicit field layout.
- Define constants for OpCodes in the packet classes.

```csharp
[SerializePackable(SerializeLayout.Explicit)]
public sealed class JoinRequest : PacketBase<JoinRequest>
{
    public const ushort OpCodeValue = 0x3001;

    [SerializeOrder(0)]
    public string Username { get; set; } = string.Empty;

    public JoinRequest() => OpCode = OpCodeValue;
}
```

---

## 3. Server Setup (`MyProject.Server`)

The server project should be a `Console Application` or `Worker Service` that uses the `Nalix.Network.Hosting` package.

```bash
dotnet new console -n MyProject.Server
dotnet add MyProject.Server reference MyProject.Contracts
dotnet add MyProject.Server package Nalix.Network.Hosting --version 11.8.0
```

### Best Practice: `NetworkApplication`
Use the fluent builder to assemble your server layers:

```csharp
using var app = NetworkApplication.CreateBuilder()
    .AddPacket<JoinRequest>() // Scans assembly for contracts
    .AddHandlers<MyHandlers>() // Scans assembly for controllers
    .AddTcp<MyProtocol>()
    .Build();

await app.RunAsync();
```

---

## 4. Client Setup (`MyProject.Client`)

The client project should use the `Nalix.SDK` package.

```bash
dotnet new console -n MyProject.Client
dotnet add MyProject.Client reference MyProject.Contracts
dotnet add MyProject.Client package Nalix.SDK --version 11.8.0
```

---

## 5. Version Management

Nalix releases updates synchronized across all core packages. When upgrading:
1. Update `NalixVersion` in `Directory.Build.props`.
2. Clear the `obj` and `bin` folders if source-generated contracts have changed.
3. Run `dotnet build` to ensure all projects are aligned.

## Recommended Next Steps

- [Quickstart](../quickstart.md)
- [Architecture Details](../concepts/architecture.md)
- [Packet System Overview](../concepts/packet-system.md)
