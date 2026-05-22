# Nalix.SDK.Native

## Triggers
- Exporting a new function to the native C ABI
- Integrating Nalix with Java (JNI/JNA), C/C++, Rust, Python, or Go
- Publishing the native shared library for a target RID
- Debugging crashes or errors at the managed/native boundary

---

## Rules

### C ABI Surface Constraints
- All exported functions must use `[UnmanagedCallersOnly]` — no exceptions
- Parameters and return types must be C-compatible: primitives, pointers, `delegate* unmanaged<...>` — **no managed types** (`string`, `object`, arrays, interfaces)
- Use `byte*` + `int length` for string parameters — never `string`
- Use `delegate* unmanaged<...>` for callbacks — never `Delegate`, `Action`, or `Func`

### Error Handling at the Boundary
- **No exceptions may cross the native boundary** — any unhandled managed exception in an `[UnmanagedCallersOnly]` method causes a process crash (not a catchable error)
- Every exported method must have a `try/catch (Exception)` block that captures the exception into `LastError` and returns an error code
- Callers check the return code; retrieve the message via the `GetLastError` export

### Memory Safety
- Managed buffers passed to native callers must be pinned/fixed for the duration of the call
- `BufferLease` supports detach for zero-copy handoffs — detached leases must be explicitly freed by the native caller via a corresponding `Free` export
- Never store managed object references in native memory — the GC can relocate them

### Build / Publish
```bash
dotnet publish -r linux-x64   -c Release   # produces libNalix.so
dotnet publish -r win-x64     -c Release   # produces Nalix.dll
dotnet publish -r osx-arm64   -c Release   # produces libNalix.dylib
```
Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `android-arm64`, `android-x64`

---

## Checklists

### Export a new function
1. Define the method as `public static <ReturnType> MyFunc(...)` in `Nalix.NativeMethods.cs`
2. Add `[UnmanagedCallersOnly(EntryPoint = "nalix_my_func")]`
3. Ensure all parameter and return types are C-compatible (no managed types)
4. Wrap entire body in `try { ... } catch (Exception ex) { NalixLastError.Set(ex); return ErrorCode.Failure; }`
5. If returning a buffer: use a `BufferLease` with a corresponding `nalix_free_buffer` export
6. Add the function signature to the C header file for native callers

### Debug a crash at the boundary
1. Check if `[UnmanagedCallersOnly]` method has an unhandled exception path — any uncaught exception crashes the process
2. Verify all pointer parameters are valid and within bounds before dereferencing
3. Check that pinned/fixed buffers remain pinned for the entire duration of the call
4. Use `NalixLastError.Get()` export from the calling language to retrieve the last error message

---

## Gotchas

- **Unhandled exception = process crash, not a catchable error**: In `[UnmanagedCallersOnly]` methods, a managed exception that escapes the try/catch does not propagate to the native caller — it aborts the process. Every code path must be covered by the catch block.

- **`string` parameters are not C-compatible**: The managed `string` type cannot be used in `[UnmanagedCallersOnly]` signatures. Always use `byte*` with a length parameter and convert inside the method body using `Encoding.UTF8.GetString(new ReadOnlySpan<byte>(ptr, length))`.

- **GC moves managed objects**: A managed `byte[]` passed to a native caller can be moved by the GC during the call if not pinned. Use `fixed (byte* p = array)` or `GCHandle.Alloc(array, GCHandleType.Pinned)` for the duration of the operation.

- **`IsPackable=false` means no NuGet distribution**: The native library is published as a platform-specific binary, not a NuGet package. Distribution is via direct file inclusion in consuming projects — reference documentation accordingly.

- **`StripSymbols=true` removes debug info from release builds**: Native crash dumps from release builds have no symbol names. For debugging native crashes in release, temporarily publish with `StripSymbols=false`.
