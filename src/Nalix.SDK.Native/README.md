# Nalix.SDK.Native

> Native AOT interop layer for cross-language Nalix integration.

**Nalix.SDK.Native** exports the Nalix networking stack as a shared native library (`.dll`, `.so`, `.dylib`) with a stable C ABI. This enables high-performance Nalix integration for non-.NET languages including C/C++, Rust, Python, Go, and Java via Foreign Function Interface (FFI).

## Key Features

| Feature | Description | C ABI Exported Symbols |
| :--- | :--- | :--- |
| 🔗 **TCP Session Lifecycle** | Allocation, connection, data transmission, and resource disposal of managed TCP streams. | `nalix_tcp_create`, `nalix_tcp_connect`, `nalix_tcp_send`, `nalix_tcp_disconnect`, `nalix_tcp_free` |
| 📡 **Event Callback Bindings** | Real-time unmanaged callback registration for transport state changes and incoming data. | `nalix_tcp_on_connected`, `nalix_tcp_on_message`, `nalix_tcp_on_error`, `nalix_tcp_on_disconnected` |
| 🔄 **Protocol Extensions** | Sub-millisecond ping, NTP-like time synchronization, and cryptographic handshakes. | `nalix_tcp_handshake`, `nalix_tcp_connect_with_resume`, `nalix_tcp_resume_session`, `nalix_tcp_ping`, `nalix_tcp_sync_time` |
| 🔐 **Cipher Updates & Control** | Dynamic symmetric key rotation at runtime and manual control/disconnect frame dispatch. | `nalix_tcp_update_cipher`, `nalix_tcp_send_control`, `nalix_tcp_disconnect_graceful` |
| 🛡️ **Thread-Safe Diagnostics** | Retrieve unmanaged error messages per-thread upon failure of any API function. | `nalix_get_last_error`, `nalix_free_error` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.SDK.Native` | Root namespace containing C ABI symbols, method mappings, error codes, and diagnostics | `Nalix`, `NativeMethods`, `ErrorCode`, `LastError` |
| `Nalix.SDK.Native.Wrappers` | Managed GC wrappers mapping TCP session events to unmanaged function pointers | `NativeTcpSession` |
| `Nalix.SDK.Native.Results` | Sequential, blittable struct layouts returning operation details across FFI boundary | `TcpPingResult`, `TcpTimeSyncResult`, `TcpResumeResult` |

## Installation & Compilation

To compile Nalix into a native shared library, use the [.NET SDK](https://dotnet.microsoft.com/download) along with the native toolchain of your operating system (MSVC on Windows, GCC/Clang on Linux, Xcode on macOS):

```bash
# Publish native binary for target platform
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r osx-arm64
```

This generates a standalone `Nalix.dll` (Windows), `libNalix.so` (Linux), or `libNalix.dylib` (macOS) in the publish output directory.

## FFI Consumption Example (C++)

The following code illustrates how to load the shared library, connect to a server, and subscribe to messages using C++:

```cpp
#include <iostream>
#include <string>

// Blittable result structure mapping TcpPingResult
struct TcpPingResult {
    double rtt_ms;
    int error_code;
};

// Function pointer declarations
typedef void* (*CreateSessionFn)(void* options);
typedef int (*ConnectFn)(void* handle, const char* host, unsigned short port);
typedef int (*SendFn)(void* handle, const unsigned char* data, int length, unsigned char encrypt);
typedef void (*OnMessageFn)(void* handle, void (*callback)(void*, const unsigned char*, int));
typedef void (*FreeSessionFn)(void* handle);

void on_message_received(void* handle, const unsigned char* data, int length) {
    std::cout << "Received message of length " << length << std::endl;
}

int main() {
    // 1. Load the shared library and resolve symbols (platform-specific loading omitted)
    CreateSessionFn tcp_create = /* resolve "nalix_tcp_create" */;
    ConnectFn tcp_connect = /* resolve "nalix_tcp_connect" */;
    OnMessageFn tcp_on_message = /* resolve "nalix_tcp_on_message" */;
    SendFn tcp_send = /* resolve "nalix_tcp_send" */;
    FreeSessionFn tcp_free = /* resolve "nalix_tcp_free" */;

    // 2. Allocate TCP transport session
    void* session = tcp_create(nullptr);
    if (!session) return -1;

    // 3. Bind callbacks
    tcp_on_message(session, [](void* h, const unsigned char* d, int len) {
        on_message_received(h, d, len);
    });

    // 4. Connect to remote host
    int connect_result = tcp_connect(session, "127.0.0.1", 8080);
    if (connect_result == 0) {
        std::cout << "Connected successfully!" << std::endl;

        // 5. Send binary frame
        std::string payload = "Hello, Nalix!";
        tcp_send(session, (const unsigned char*)payload.data(), payload.size(), 1);
    }

    // 6. Graceful cleanup
    tcp_free(session);
    return 0;
}
```

## Anti-Patterns

- **Direct Object Reference:** Unmanaged callers must never hold direct references to managed C# objects. Instead, they reference allocations using opaque session handles (`IntPtr`) tracked through pinned GC handles.
- **Unchecked Exceptions:** Managed exceptions are caught safely at the native boundary to prevent process crashes. Callers must inspect standard integer error codes and retrieve detailed exception logs via `nalix_get_last_error`.
- **Heap Allocation Overhead:** Callback buffers are exposed to the caller as raw pointers (`byte*`) referencing pinned stack/GC allocations, minimizing marshalling heap overhead. Callers must not store these pointers beyond the callback lifetime.
