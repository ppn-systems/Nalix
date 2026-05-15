# Nalix.SDK.Native

## Role

Native AOT interop layer. Exports the Nalix networking stack as a shared native library (`.dll`/`.so`/`.dylib`) with a C ABI (`UnmanagedCallersOnly`), enabling Java (JNI/JNA), C/C++, Rust, Python, Go, and other languages to call into Nalix.

**Dependencies:** `Nalix.SDK`

## Directory Structure

```
Nalix.SDK.Native/
├── Nalix.cs                    # Main entry point / session factory
├── Nalix.NativeMethods.cs      # UnmanagedCallersOnly exported methods
├── Nalix.Extensions.cs         # Native-friendly extension methods
├── Nalix.PrivateMethods.cs     # Internal helper methods
├── Nalix.ErrorCode.cs          # Error code enum for C ABI
├── Nalix.LastError.cs          # Thread-local last error tracking
├── Results/                    # Result wrapper types for interop
└── Wrappers/                   # Managed-to-native wrapper types
```

## Key Design

### C ABI Surface

All exported functions use `[UnmanagedCallersOnly]`:
- Pure C-compatible signatures (no managed types in parameters/return).
- Error handling via error codes + `LastError` thread-local pattern.
- Callbacks via function pointers (`delegate* unmanaged<...>`).

### Supported Features

- TcpSession: Connect, Send, Handshake, RequestAsync (via callback)
- Callbacks: OnMessageReceived, OnDisconnected, OnError
- Session resume, cipher update, ping, time sync
- Zero-copy where possible via `BufferLease`

### Build Configuration

- `PublishAot=true`, `NativeLib=Shared`, `SelfContained=true`
- `NativeLibraryName=Nalix` → produces `Nalix.dll` / `libNalix.so` / `libNalix.dylib`
- Multi-RID: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `android-arm64`, `android-x64`
- `IlcOptimizationPreference=Speed`, `StripSymbols=true`
- `IsPackable=false` — not distributed as NuGet package

## Publishing

```bash
dotnet publish -r linux-x64 -c Release
dotnet publish -r win-x64 -c Release
dotnet publish -r osx-arm64 -c Release
```

## Anti-Patterns

- Do NOT expose managed types in the C ABI surface.
- Do NOT use `string` parameters in `[UnmanagedCallersOnly]` — use `byte*` + length.
- Do NOT throw exceptions across the native boundary — use error codes.
- Do NOT forget to pin/fix managed buffers when passing to unmanaged code.
