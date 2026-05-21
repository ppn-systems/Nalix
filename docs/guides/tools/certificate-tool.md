# Identity Certificate Tool

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner
    - :fontawesome-solid-clock: **Time**: 5 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Security Architecture](../../concepts/security/security-architecture.md)

The `Nalix.Certificate` tool is the identity bootstrap utility currently shipped in the repository. It generates the X25519 key material used by the server handshake and the client-side public-key pinning flow.

---

## Overview

In the current source tree, the tool writes a paired identity into `Directories.ConfigurationDirectory`:

- `certificate.private`: the server-side private identity loaded by `HandshakeHandlers`
- `certificate.public`: the public key that clients pin through `TransportOptions.ServerPublicKey`

Those files support:

- server identity verification during the handshake
- client-side pinning against MitM attacks
- fresh session-key derivation on every handshake

---

## Key Generation

Run it from the repo root or point `dotnet run` at the project explicitly:

```powershell
dotnet run --project tools/Nalix.Certificate/Nalix.Certificate.csproj
```

### Output Files

By default, the tool saves two files into the shared Nalix configuration directory:

1. **`certificate.private`**: Contains the private key. **KEEP THIS SECRET.**
2. **`certificate.public`**: Contains the X25519 public key in hex. This is what clients pin.

!!! tip "Standard Paths (Framework Directories API)"
    Nalix uses a standardized path resolution strategy based on the `Directories` API:

    - **Windows**: `%LOCALAPPDATA%\Nalix\Config\`
    - **Linux/macOS**: `~/.local/share/Nalix/Config/`

### Force Overwrite

If certificates already exist, the tool will ask for confirmation and create automatic timestamped backups before proceeding. To skip confirmation:

```powershell
dotnet run --project tools/Nalix.Certificate/Nalix.Certificate.csproj -- --force
```

---

## Security Specifications

| Feature | Specification |
| :--- | :--- |
| **Algorithm** | X25519 (Curve25519) |
| **Key Length** | 32 bytes (256 bits) |
| **Entropy** | High (System-provided Cryptographic RNG) |
| **Clamping** | Fully RFC 7748 compliant |

---

## Server Configuration

If you do nothing, `NetworkApplicationBuilder.Build()` initializes `HandshakeHandlers` with:

- `Directories.ConfigurationDirectory/certificate.private`

If your private identity lives somewhere else, configure the host builder explicitly:

```csharp
builder.ConfigureCertificate("/custom/path/certificate.private");
```

!!! info "Security Enforcement"
    If the private identity file is missing or malformed, handshake initialization throws during host startup. Anonymous server handshakes are not supported.

---

## 💡 Best Practices

!!! warning "Security Risk"
    Never commit `certificate.private` to version control (Git). Use environment variables or secure secret managers in production environments.

- **Rotation**: Rotate your keys regularly if you suspect a compromise.
- **Backups**: The tool automatically creates backups with `.bak` extensions. Keep these secure or delete them if no longer needed.
- **Client Pinning**: Load the value from `certificate.public` into `TransportOptions.ServerPublicKey` on the client.

