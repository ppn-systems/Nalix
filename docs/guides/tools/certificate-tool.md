# Identity Certificate Tool

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Beginner
    - :fontawesome-solid-clock: **Time**: 5 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Security Architecture](../../concepts/security/security-architecture.md)

The `Nalix.Certificate` tool is a high-performance CLI utility designed to generate and manage asymmetric identity keys for the Nalix Framework. It exclusively uses the **X25519** (Curve25519) algorithm for all operations.

---

## 🔑 Overview

In the Nalix security model, each server and client identity is represented by a 256-bit X25519 key pair. These keys are fundamental for:

- **Server Identity**: Proving the authenticity of the server to clients.
- **Perfect Forward Secrecy**: Deriving session keys during the protocol handshake.
- **Client Pinning**: Preventing Man-in-the-Middle (MitM) attacks by hardcoding public keys in clients.

---

## 🏗️ Key Generation

To generate a new identity, run the tool from the `tools/Nalix.Certificate` directory:

```powershell
dotnet run --project Nalix.Certificate.csproj
```

### Output Files

By default, the tool saves two files to your application's identity directory:

1.  **`certificate.private`**: Contains the private key. **KEEP THIS SECRET.**
2.  **`certificate.public`**: Contains the public key hash. This is used for server identity validation.

!!! tip "Standard Paths (Framework Directories API)"
    Nalix uses a standardized path resolution strategy based on the `Directories` API:
    - **Windows**: `%LOCALAPPDATA%\Nalix\Config\`
    - **Linux/macOS**: `~/.local/share/Nalix/Config/`

### Force Overwrite

If certificates already exist, the tool will ask for confirmation and create automatic timestamped backups before proceeding. To skip confirmation:

```powershell
dotnet run --project Nalix.Certificate.csproj -- --force
```

---

## 🛡️ Security Specifications

| Feature | Specification |
|:---|:---|
| **Algorithm** | X25519 (Curve25519) |
| **Key Length** | 32 bytes (256 bits) |
| **Entropy** | High (System-provided Cryptographic RNG) |
| **Clamping** | Fully RFC 7748 compliant |

---

## ⚙️ Server Configuration

Once generated, the Nalix server's `HandshakeHandlers` will automatically attempt to load the identity from the standard path. You do not typically need to configure this manually unless using a custom path:

```csharp
builder.ConfigureHandshake(options => {
    // Optional: Overwrite default identity path
    options.IdentityPath = "/custom/path/certificate.private";
});
```

!!! info "Security Enforcement"
    If no identity is found, the server will throw a `NetworkException` at startup. Anonymous handshakes are not permitted.

---

## 💡 Best Practices

!!! warning "Security Risk"
    Never commit `certificate.private` to version control (Git). Use environment variables or secure secret managers in production environments.

- **Rotation**: Rotate your keys regularly if you suspect a compromise.
- **Backups**: The tool automatically creates backups with `.bak` extensions. Keep these secure or delete them if no longer needed.
- **Client Pinning**: Hardcode the value from `certificate.public` directly in your client’s `ConnectionOptions.ServerPublicKey` for maximum security.
