# SecureMultiTransportHelloWorld

A Nalix sample demonstrating a server with **TCP**, **UDP**, and **WebSocket** listeners,
all protected by the secure transport model (X25519 handshake + AEAD encryption).

## What This Sample Demonstrates

- Building a multi-transport Nalix server using the canonical Hosting API.
- Enabling `UseSecureConnections()` to require X25519 key exchange before any
  application packet is processed.
- Establishing a secure TCP session first, then sending authenticated UDP
  datagrams that share the same session state (session token + HMAC).
- UDP packets carry an 8-byte session token (negotiated during TCP handshake)
  and a 4-byte XxHash32 MAC signed with the shared secret. The server verifies
  both before processing the datagram.
- WebSocket server binding (requires elevated privileges on Windows).

## Why TCP Must Connect Before UDP

Nalix's UDP security model relies on a session token issued during the TCP
X25519 handshake. Each UDP datagram is prefixed with this 8-byte token and
appended with an XxHash32 HMAC derived from the shared secret. The server
resolves the connection by token and verifies the MAC before processing.

Without a prior TCP handshake, there is no session token and no shared
secret, so the server has no way to authenticate or route the datagram.

## Why `UseSecureConnections()` Is Enabled

`UseSecureConnections()` registers the server-side handshake handlers
(`HandshakeHandlers`, `ProofOfWorkHandlers`) and initializes the X25519
certificate. Without it, clients cannot perform the key exchange that
establishes the session token and shared secret used by UDP authentication.

## Folder Structure

```text
SecureMultiTransportHelloWorld/
  SecureMultiTransportHelloWorld.Contracts/
    HelloRequestPacket.cs
    HelloResponsePacket.cs
    SecureMultiTransportHelloWorld.Contracts.csproj

  SecureMultiTransportHelloWorld.Server/
    HelloHandlers.cs
    Program.cs
    SecureMultiTransportHelloWorld.Server.csproj

  SecureMultiTransportHelloWorld.Client/
    Program.cs
    SecureMultiTransportHelloWorld.Client.csproj

  SecureMultiTransportHelloWorld.sln
  README.md
```

## How to Build

```bash
dotnet build samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.sln
```

## How to Run the Server

```bash
dotnet run --project samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Server
```

The server listens on:

| Transport   | Address                    |
|-------------|----------------------------|
| TCP + UDP   | `127.0.0.1:57210`         |
| WebSocket   | `ws://127.0.0.1:57212/ws` |

> **Note:** TCP and UDP share the same port because Nalix's endpoint-pinning
> security (SEC-30) requires the UDP datagram source IP:port to match the TCP
> connection's endpoint.

## How to Run the Client

In a separate terminal:

```bash
dotnet run --project samples/SecureMultiTransportHelloWorld/SecureMultiTransportHelloWorld.Client
```

## Expected Output

**Server:**

```text
SecureMultiTransport server is running.
  TCP + UDP  : 127.0.0.1:57210
  WebSocket  : ws://127.0.0.1:57212/ws
Press Ctrl+C to stop.
```

**Client:**

```text
Connected over TCP.
TCP handshake completed. Secure state is ready.
  SessionToken : 822530074371686401
  Encryption   : True
TCP replied: Hello from Nalix!

Sending UDP hello (send-only)...
UDP packet sent (authenticated with session token + HMAC).
TCP connection is still alive: True

Connecting WebSocket...
WebSocket test skipped: WebSocket Connection failed: Unable to connect to the remote server

Done.
```

## Known Limitations

1. **UDP request/response is not demonstrated.** The SDK's `UdpSession`
   supports sending authenticated datagrams, but awaiting a typed response
   over UDP (`RequestAsync`) may not work in all server configurations due
   to server-side response routing. This sample uses `SendAsync` (fire-and-forget)
   to demonstrate the authenticated datagram flow.

2. **WebSocket requires elevated privileges on Windows.** The server's
   `HttpListener`-based WebSocket listener requires administrator rights.
   The client gracefully handles this by catching the connection error.
   On Linux, no special privileges are needed.

3. **UDP and TCP must share the same server port.** Nalix's endpoint-pinning
   security (SEC-30) verifies that the UDP datagram's source IP:port matches
   the TCP connection's endpoint. Using different server ports for TCP and UDP
   does not affect this check (the client's source port is what matters),
   but sharing a port is the tested and recommended configuration.

4. **First connection generates server certificate.** On first run,
   `UseSecureConnections()` auto-generates an X25519 key pair if no
   certificate file exists. The client uses Trust-On-First-Use (TOFU) to
   pin the server's public key.
