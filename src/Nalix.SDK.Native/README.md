# Nalix.SDK.Native

> Native AOT interop layer for cross-language Nalix integration.

**Nalix.SDK.Native** exports the Nalix networking stack as a shared native library (`.dll`, `.so`, `.dylib`) with a stable C ABI. This enables high-performance Nalix integration for non-.NET languages including C/C++, Rust, Python, Go, and Java.

## Key Features

- **C ABI Surface:** All methods use `[UnmanagedCallersOnly]` for universal compatibility.
- **Native AOT:** Fully compiled to machine code for low latency and zero runtime overhead.
- **Cross-Platform:** Supports Windows, Linux, macOS (x64 and ARM64), as well as Android.
- **Async Interop:** Provides function-pointer based callbacks for message handling and events.

## Build Requirements

To publish the native library, you need the [.NET SDK](https://dotnet.microsoft.com/download) and the native build toolchain for your target OS (MSVC, GCC, or Clang).

```bash
# Publish for the current platform
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r osx-arm64
```

## Anti-Patterns

- **No Managed Types:** The public API uses only primitive types and `byte*` pointers.
- **Error Codes:** Uses a thread-local `LastError` pattern instead of managed exceptions.
- **Pinned Memory:** Memory passed across the boundary is handled carefully to avoid GC issues.
