# Báo Cáo Kiểm Tra: Chuyển Đổi Structured Logging

> **Ngày tạo:** 2026-06-01  
> **Người thực hiện:** Dev Team (Step 1 — Report Phase)  
> **Trạng thái:** 🟡 CHỜ PM/SẾP REVIEW  
> **Phạm vi quét:** `src/` — toàn bộ dự án Nalix

---

## Tổng Quan

| Metric                        | Giá trị |
|-------------------------------|---------|
| **Tổng số vi phạm**          | **131** |
| **Số file bị ảnh hưởng**     | **41**  |
| **Dự án bị ảnh hưởng**       | `Nalix.Runtime`, `Nalix.Network`, `Nalix.Hosting` |
| **Pattern Regex sử dụng**     | `\.Log(Trace\|Debug\|Information\|Warning\|Error\|Critical)\s*\(\s*\$` |
| **Logger biến phổ biến**     | `_logger`, `s_logger`, `this.Logger`, `this.Logging`, `logger` |

### Phân Bố Theo Log Level (trong các vi phạm `$""`)

| Log Level        | Số lượng |
|------------------|----------|
| `LogTrace`       | ~22      |
| `LogDebug`       | ~48      |
| `LogInformation` | ~25      |
| `LogWarning`     | ~28      |
| `LogError`       | ~7       |
| `LogCritical`    | ~1       |

### Phân Bố Theo Dự Án

| Dự Án          | Số File | Số Vi Phạm |
|-----------------|---------|------------|
| `Nalix.Network` | 28      | ~97        |
| `Nalix.Runtime` | 11      | ~32        |
| `Nalix.Hosting` | 2       | 2          |

---

## Nhãn Đánh Dấu Trong Báo Cáo

| Nhãn                     | Ý nghĩa                                                                 |
|--------------------------|--------------------------------------------------------------------------|
| ✅ `SIMPLE`              | Chỉ dùng `{nameof(...)}` hoặc biến đơn giản — refactor dễ, không cần review |
| 🟡 `[REQUIRES_REVIEW]`  | Có logic phức tạp inline — cần PM/Sếp xem trước khi sửa               |

### Tiêu chí đánh dấu `[REQUIRES_REVIEW]`:

1. **Ternary/Conditional** trong chuỗi log: `{a ? b : c}`
2. **Method call** trong chuỗi log: `{obj.GetType().Name}`, `{scope.GetElapsedMilliseconds()}`
3. **Exception properties** vi phạm Rule 2: `{ex.Message}`, `{ex.SocketErrorCode}`, `{ex.ObjectName}`
4. **Null-coalescing** trong chuỗi log: `{a?.ToString() ?? "<null>"}`
5. **Multi-line concatenation** `$"" + $""`: cần gộp lại thành 1 template
6. **Format specifier** phức tạp: `{value:HH:mm:ss}`, `{value:0.#}`, `{value:F3}`
7. **LINQ/Computation** inline trong chuỗi log

---

## Chi Tiết Theo File

---

### 📁 Nalix.Runtime

---

#### `src/Nalix.Runtime/Timekeeping/TimeSynchronizer.cs` — 6 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 136  | `LogDebug` | `$"[RT.{nameof(TimeSynchronizer)}] initialized"` | ✅ SIMPLE |
| 2 | 208  | `LogWarning` | `$"[RT.{nameof(TimeSynchronizer)}] restart-timeout waiting for previous loop to stop"` | ✅ SIMPLE |
| 3 | 258  | `LogWarning` | `$"[RT.{nameof(TimeSynchronizer)}] dispose-timeout waiting for loop shutdown"` | ✅ SIMPLE |
| 4 | 273  | `LogDebug` | `$"[RT.{nameof(TimeSynchronizer)}] disposed"` | ✅ SIMPLE |
| 5 | 315  | `LogInformation` | `$"... started period={this.Period.TotalMilliseconds:0.#}ms"` | 🟡 `[REQUIRES_REVIEW]` — Format specifier `:0.#` |
| 6 | 400  | `LogInformation` | `$"[RT.{nameof(TimeSynchronizer)}] stopped"` | ✅ SIMPLE |

> **Logger:** `s_logger`

---

#### `src/Nalix.Runtime/Throttling/TokenBucketLimiter.cs` — 7 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 237  | `LogWarning` | `$"... endpoint-limit-reached-precheck count={_totalEndpointCount} limit={_options.MaxTrackedEndpoints}"` | ✅ SIMPLE |
| 2 | 273  | `LogDebug` | `$"... new-endpoint total={_totalEndpointCount}"` | ✅ SIMPLE |
| 3 | 285  | `LogWarning` | `$"... endpoint-limit-reached-precheck count={_totalEndpointCount} limit={_options.MaxTrackedEndpoints}"` | ✅ SIMPLE |
| 4 | 307  | `LogDebug` | `$"... new-endpoint total={_totalEndpointCount}"` | ✅ SIMPLE |
| 5 | 464  | `LogTrace` | `$"... hard-blocked retry_ms={retryMs}"` | ✅ SIMPLE |
| 6 | 517  | `LogTrace` | `$"... allow credit={credit}"` | ✅ SIMPLE |
| 7 | 941  | `LogDebug` | `$"[RT.{nameof(TokenBucketLimiter)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Runtime/Throttling/TokenBucketLimiter.Cleanup.cs` — 3 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 48   | `LogDebug` | `$"... " + $"Cleanup removed={removed}"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line concatenation `$"" + $""` |
| 2 | 57   | `LogWarning` | `$"... Cleanup was cancelled due to timeout"` | ✅ SIMPLE |
| 3 | 187  | `LogWarning` | `$"... " + $"Evicted {removed} endpoints to enforce MaxTrackedEndpoints limit"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line concatenation `$"" + $""` |

> **Logger:** `_logger`

---

#### `src/Nalix.Runtime/Throttling/PolicyRateLimiter.cs` — 3 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 197  | `LogWarning` | `$"... invalid-burst burst={rl.Burst}"` | ✅ SIMPLE |
| 2 | 253  | `LogInformation` | `$"[RT.{nameof(PolicyRateLimiter)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |
| 3 | 312  | `LogWarning` | `$"... missing-endpoint opCode={context.Packet.Header.OpCode}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Runtime/Throttling/ConcurrencyGate.Types.cs` — 3 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 154  | `LogError` | `$"... activeUsers overflow detected"` | ✅ SIMPLE |
| 2 | 174  | `LogError` | `$"... activeUsers underflow detected"` | ✅ SIMPLE |
| 3 | 219  | `LogError` | `$"... queueCount underflow detected"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Runtime/Throttling/ConcurrencyGate.Cleanup.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 47   | `LogInformation` | `$"... circuit breaker closed"` | ✅ SIMPLE |
| 2 | 205  | `LogDebug` | `$"... cleanup removed={removed} remaining={_table.Count}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Runtime/Middleware/Standard/RateLimitMiddleware.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 84   | `LogWarning` | `$"... rate-limiter-disposed request-denied"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs` — 6 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 77   | `LogDebug` | `$"... scan controller={controllerType.Name}"` | ✅ SIMPLE |
| 2 | 112-113 | `LogDebug` | `$"... found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : string.Empty)}]"` | 🟡 `[REQUIRES_REVIEW]` — Ternary inline + multi-line + LINQ pre-computation (`firstOps`) |
| 3 | 193  | `LogDebug` | `$"... no-method controller={x03.Name}"` | ✅ SIMPLE |
| 4 | 199  | `LogDebug` | `$"... compile count={methodInfos.Length} controller={x03.Name}"` | ✅ SIMPLE |
| 5 | 223  | `LogWarning` | `$"... dup-opcode {x01}"` | ✅ SIMPLE |
| 6 | 238  | `LogTrace` | `$"... compiled {x01}"` | ✅ SIMPLE |

> **Logger:** `logger`

---

#### `src/Nalix.Runtime/Dispatching/PacketSender.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 104  | `LogDebug` | `$"... Start SEND_CORE_ASYNC \| Packet={packet.GetType().Name}, Length={packetLength}, NeedEncrypt={needEncrypt}"` | 🟡 `[REQUIRES_REVIEW]` — `packet.GetType().Name` method call |

> **Logger:** `s_logger`  
> **Lưu ý:** Nằm trong `#if DEBUG`

---

#### `src/Nalix.Runtime/Dispatching/Options/PacketDispatchOptions.PublicMethods.cs` — 5 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 39   | `LogDebug` | `$"... logger-attached"` | ✅ SIMPLE |
| 2 | 62   | `LogDebug` | `$"... error-handler-set"` | ✅ SIMPLE |
| 3 | 85   | `LogDebug` | `$"... middleware-added type={middleware.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `GetType().Name` method call |
| 4 | 113  | `LogDebug` | `$"... loops={(loopCount.HasValue ? loopCount.Value.ToString(CultureInfo.InvariantCulture) : "auto")}"` | 🟡 `[REQUIRES_REVIEW]` — Ternary + `ToString(CultureInfo)` |
| 5 | 260-261 | `LogInformation` | `$"... reg-handlers count={compiledHandlers.Length} controller={controllerType.Name}"` | ✅ SIMPLE (multi-line nhưng logic đơn giản) |

> **Logger:** `this.Logging`

---

### 📁 Nalix.Network

---

#### `src/Nalix.Network/RateLimiting/Connection.Guard.cs` — 9 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 122-125 | `LogDebug` | `$"... init " + $"maxPerEndpoint={_maxPerEndpoint} " + $"inactivity={_inactivityThreshold.TotalSeconds:F0}s " + $"cleanup={_cleanupInterval.TotalSeconds:F0}s"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line concat + format specifiers `:F0` |
| 2 | 181  | `LogWarning` | `$"... manually-banned ip={address} until={banUntil:HH:mm:ss}"` | 🟡 `[REQUIRES_REVIEW]` — Format specifier `:HH:mm:ss` |
| 3 | 266  | `LogTrace` | `$"... allow endpoint={endPoint} current={result.CurrentConnections} limit={_maxPerEndpoint}"` | ✅ SIMPLE |
| 4 | 291  | `LogWarning` | `$"... received-null args/connection/endpoint"` | ✅ SIMPLE |
| 5 | 300  | `LogWarning` | `$"... received-empty-address"` | ✅ SIMPLE |
| 6 | 329  | `LogTrace` | `$"... closed endpoint={key.Address}{suffix}"` | 🟡 `[REQUIRES_REVIEW]` — `{suffix}` biến động, concat không có `=` |
| 7 | 433  | `LogWarning` | `$"... banned ip={key.Address} count={entry.BanCount} until={banUntil:HH:mm:ss}"` | 🟡 `[REQUIRES_REVIEW]` — Format specifier `:HH:mm:ss` |
| 8 | 572  | `LogDebug` | `$"... cleared-queue ip={key.Address} reason=oversized"` | ✅ SIMPLE |
| 9 | 615  | `LogDebug` | `$"[NW.{nameof(ConnectionGuard)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/RateLimiting/Connection.Guard.Cleanup.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 170  | `LogDebug` | `$"... cleanup scanned={scanned} removed={removed} remaining={_map.Count}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/RateLimiting/Datagram.Guard.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 300  | `LogDebug` | `$"... Evicted {removed} idle windows. IPv4={_ipv4Map.Count}, IPv6={_ipv6Map.Count}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Protocols/Protocol.PublicMethods.cs` — 6 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 64   | `LogTrace` | `$"... reject id={connection.ID} reason=not-accepting"` | ✅ SIMPLE |
| 2 | 82   | `LogTrace` | `$"... accepted id={connection.ID}"` | ✅ SIMPLE |
| 3 | 95   | `LogTrace` | `$"... reject id={connection.ID} reason=validation-failed"` | ✅ SIMPLE |
| 4 | 105  | `LogTrace` | `$"... accept-canceled id={connection.ID}"` | ✅ SIMPLE |
| 5 | 116  | `LogWarning` | `$"... accept-disposed id={connection.ID} target={ex.ObjectName}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.ObjectName` (Rule 2: exception property trong chuỗi) |
| 6 | 130  | `LogDebug` | `$"... accept-error id={connection.ID} ex={ex.Message}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.Message` (Rule 2: exception trong chuỗi, cần truyền `ex` là tham số đầu) |

> **Logger:** `s_logger`

---

#### `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 72   | `LogTrace` | `$"[NW.{nameof(Protocol)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |

> **Logger:** `s_logger`

---

#### `src/Nalix.Network/Protocols/Protocol.Core.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 75   | `LogTrace` | `$"... disconnect id={args.Connection?.ID}"` | 🟡 `[REQUIRES_REVIEW]` — Null-conditional `?.` trong interpolation |
| 2 | 107  | `LogInformation` | `$"... accepting={(isEnabled ? "enabled" : "disabled")}"` | 🟡 `[REQUIRES_REVIEW]` — Ternary operator |

> **Logger:** `s_logger`

---

#### `src/Nalix.Network/Listeners/WebListener/WebSocketListener.PublicMethods.cs` — 7 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 35   | `LogDebug` | `$"... activate-request port={_port}"` | ✅ SIMPLE |
| 2 | 57   | `LogWarning` | `$"... ignored-activate state={this.State}"` | ✅ SIMPLE |
| 3 | 87   | `LogInformation` | `$"... start protocol={this.Protocol} port={_port} path={_path}"` | ✅ SIMPLE |
| 4 | 126  | `LogInformation` | `$"... cancel port={_port}"` | ✅ SIMPLE |
| 5 | 157  | `LogDebug` | `$"... deactivate-request port={_port}"` | ✅ SIMPLE |
| 6 | 205  | `LogInformation` | `$"... stop protocol={this.Protocol} port={_port}"` | ✅ SIMPLE |
| 7 | 243  | `LogDebug` | `$"... bound to {prefix}"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/WebListener/WebSocketListener.Handle.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 88   | `LogTrace` | `$"... new={connection?.NetworkEndpoint}"` | 🟡 `[REQUIRES_REVIEW]` — Null-conditional `?.` |
| 2 | 131  | `LogWarning` | `$"... untrusted-proxy-rejected remote={remoteEp}"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/WebListener/WebSocketListener.Core.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 200  | `LogInformation` | `$"... stopped port={self._port}"` | ✅ SIMPLE |
| 2 | 293  | `LogDebug` | `$"[NW.{nameof(WebSocketListenerBase)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.PublicMethods.cs` — 7 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 53   | `LogDebug` | `$"... activate-request port={_port}"` | ✅ SIMPLE |
| 2 | 78   | `LogWarning` | `$"... ignored-activate state={this.State}"` | ✅ SIMPLE |
| 3 | 124  | `LogInformation` | `$"... start protocol={this.Protocol} port={_port}"` | ✅ SIMPLE |
| 4 | 160  | `LogInformation` | `$"... cancel port={_port}"` | ✅ SIMPLE |
| 5 | 207  | `LogDebug` | `$"... deactivate-request port={_port}"` | ✅ SIMPLE |
| 6 | 224  | `LogWarning` | `$"... ignored-deactivate state={this.State}"` | ✅ SIMPLE |
| 7 | 324  | `LogInformation` | `$"... stop protocol={this.Protocol} port={_port}"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.Handle.cs` — 7 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 273  | `LogTrace` | `$"... accept-error ex={ex.Message}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.Message` (Rule 2) |
| 2 | 331  | `LogWarning` | `$"... accept-failed={args.SocketError}"` | ✅ SIMPLE |
| 3 | 345  | `LogWarning` | `$"... accept-socket-null port={_port}"` | ✅ SIMPLE |
| 4 | 367  | `LogWarning` | `$"... channel-full port={_port} - dropped socket directly"` | ✅ SIMPLE |
| 5 | 380  | `LogWarning` | `$"... untrusted-proxy-rejected remote={remoteEp}"` | ✅ SIMPLE |
| 6 | 416  | `LogWarning` | `$"... disposed-during-accept remote={socket.RemoteEndPoint?.ToString() ?? \"<null>\"}"` | 🟡 `[REQUIRES_REVIEW]` — Null-conditional + null-coalescing `??` |
| 7 | 725  | `LogWarning` | `$"... transient-socket-error={ex.SocketErrorCode} port={_port}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.SocketErrorCode` (Rule 2) |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.SocketConfig.cs` — 6 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 68   | `LogDebug` | `$"... config-bind {epV6Any}.v6)"` | ✅ SIMPLE |
| 2 | 77   | `LogDebug` | `$"... config-listen {_listener.LocalEndPoint}.dual"` | ✅ SIMPLE |
| 3 | 88   | `LogWarning` | `$"... failed-bind ex={ex.Message}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.Message` (Rule 2) |
| 4 | 149  | `LogDebug` | `$"... config-bind {epV4Any}.v4"` | ✅ SIMPLE |
| 5 | 157  | `LogDebug` | `$"... config-listen {_listener.LocalEndPoint}"` | ✅ SIMPLE |
| 6 | 324  | `LogDebug` | `$"[{nameof(TcpListenerBase)}:InitializeOptions] SO_REUSEPORT not-supported platform/kernel"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.ProcessChannel.cs` — 3 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 135-136 | `LogWarning` | `$"... process-channel-unavailable remote={connection?.NetworkEndpoint.ToString() ?? \"<null>\"} port={_port}"` | 🟡 `[REQUIRES_REVIEW]` — Null-conditional + `.ToString()` + `??` |
| 2 | 166-167 | `LogWarning` | `$"... channel-full remote={connection?.NetworkEndpoint.ToString() ?? \"<null>\"} port={_port} - dropped"` | 🟡 `[REQUIRES_REVIEW]` — Null-conditional + `.ToString()` + `??` |
| 3 | 247  | `LogTrace` | `$"... worker-exited port={_port}"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.ProxyProtocol.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 265  | `LogTrace` | `$"... socket-disposed-during-init"` | ✅ SIMPLE |
| 2 | 276  | `LogTrace` | `$"... socket-error-during-init: {ex.SocketErrorCode}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.SocketErrorCode` (Rule 2) |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/TcpListener/TcpListener.Core.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 504  | `LogDebug` | `$"[NW.{nameof(TcpListenerBase)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Listeners/UdpListener/UdpListener.Core.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 150  | `LogDebug` | `$"... created port={_port} protocol={protocol.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `GetType().Name` method call |
| 2 | 258  | `LogDebug` | `$"... disposed port={_port}"` | ✅ SIMPLE |

> **Logger:** `this.Logger`

---

#### `src/Nalix.Network/Internal/Transport/SocketConnection.cs` — 8 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 205  | `LogDebug` | `$"... skip — already disposed ep={_endpointString}"` | ✅ SIMPLE |
| 2 | 217  | `LogDebug` | `$"... skip — already started ep={_endpointString}"` | ✅ SIMPLE |
| 3 | 234  | `LogDebug` | `$"... saea-receive-loop started ep={_endpointString} framing={_framing}"` | ✅ SIMPLE |
| 4 | 358  | `LogDebug` | `$"... invalid-size={size} ep={_endpointString}"` | ✅ SIMPLE |
| 5 | 592-593 | `LogWarning` | `$"... malformed-payload " + $"length={payloadLen} (too small for protocol header) ep={_endpointString}"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line concat `$"" + $""` |
| 6 | 612-613 | `LogWarning` | `$"... frame-dropped " + $"length={payloadLen} ep={_endpointString}"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line concat `$"" + $""` |
| 7 | 650  | `LogDebug` | `$"... fragment-limit open={openStreams} ep={_endpointString}"` | ✅ SIMPLE |
| 8 | 686  | `LogDebug` | `$"... assembled stream={header.StreamId} ep={_endpointString}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Internal/Transport/SocketConnection.Send.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 130-131 | `LogDebug` | `$"... stackalloc-benign-disconnect ep={_endpointString} ex={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — Multi-line + `ex.GetType().Name` + `#if DEBUG` |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Internal/Transport/SocketConnection.Send.VarInt.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 41   | `LogDebug` | `$"... stackalloc varint len={data.Length} ep={_socket.RemoteEndPoint}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Internal/Transport/SocketUdpTransport.cs` — 6 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 169  | `LogDebug` | `$"... dualmode-not-applied reason={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.GetType().Name` method call |
| 2 | 185  | `LogDebug` | `$"... dontfragment-not-applied reason={ex.SocketErrorCode}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.SocketErrorCode` (Rule 2) |
| 3 | 192  | `LogDebug` | `$"... dontfragment-not-supported reason={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.GetType().Name` |
| 4 | 199  | `LogDebug` | `$"... dontfragment-object-disposed reason={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.GetType().Name` |
| 5 | 206  | `LogDebug` | `$"... dontfragment-invalid-op reason={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.GetType().Name` |
| 6 | 221  | `LogDebug` | `$"... udp-connreset-ioctl-not-applied reason={ex.GetType().Name}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.GetType().Name` |

> **Logger:** `s_logger`

---

#### `src/Nalix.Network/Internal/Transport/AsyncCallback.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 159  | `LogTrace` | `$"... callback-null skipping"` | ✅ SIMPLE |

> **Logger:** `s_logger`

---

#### `src/Nalix.Network/Internal/Time/TimingWheel.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 425  | `LogInformation` | `$"[NW.{nameof(TimingWheel)}:{nameof(Deactivate)}] deactivated"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Internal/Security/NetworkBanRepository.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 74   | `LogInformation` | `$"... Loaded {records.Count} persisted bans."` | ✅ SIMPLE |
| 2 | 127  | `LogDebug` | `$"... Persisted {snapshot.Count} bans to disk."` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Internal/Security/ThrottledLogGate.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 81   | `LogWarning` | `$"... DDoS-detected ip={address}"` | ✅ SIMPLE |
| 2 | 101  | `LogTrace` | `$"... banned-reject ip={address} until={bannedUntil:HH:mm:ss}{suffix}"` | 🟡 `[REQUIRES_REVIEW]` — Format `:HH:mm:ss` + `{suffix}` concat không có `=` |

> **Logger:** `logger`

---

#### `src/Nalix.Network/Internal/Security/NetworkAccessList.cs` — 2 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 102  | `LogInformation` | `$"... Loaded {networks.Count} trusted proxies from disk."` | ✅ SIMPLE |
| 2 | 135  | `LogInformation` | `$"... Loaded {networks.Count} blacklisted IP/networks from disk (single IPs: {_blacklistedIps.Count}, CIDR networks: {_blacklistedNetworks.Count})."` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Connections/WebSocketConnection.cs` — 3 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 97   | `LogTrace` | `$"... created remote={this.NetworkEndpoint} id={this.ID}"` | ✅ SIMPLE |
| 2 | 242  | `LogWarning` | `$"... receive throttle triggered remote={this.NetworkEndpoint}"` | ✅ SIMPLE |
| 3 | 314  | `LogDebug` | `$"... disconnect request id={this.ID} remote={this.NetworkEndpoint} reason={reason}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

#### `src/Nalix.Network/Connections/Connection.Hub.cs` — 5 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 273  | `LogTrace` | `$"... broadcast-skip total=0"` | ✅ SIMPLE |
| 2 | 298  | `LogInformation` | `$"[PERF.NW.BroadcastAsync] total={connections.Length}, latency={scope.GetElapsedMilliseconds():F3} ms"` | 🟡 `[REQUIRES_REVIEW]` — `scope.GetElapsedMilliseconds()` method call + format `:F3` |
| 3 | 347  | `LogInformation` | `$"[NW.{nameof(ConnectionHub)}:{nameof(Dispose)}] disposed"` | ✅ SIMPLE |
| 4 | 675  | `LogInformation` | `$"[NW.{nameof(ConnectionHub)}:{operationName}] broadcast-cancel"` | ✅ SIMPLE |
| 5 | 808  | `LogInformation` | `$"[NW.{nameof(ConnectionHub)}:{operationName}] broadcast-cancel"` | ✅ SIMPLE |

> **Logger:** `_logger` / `logger` (line 298)

---

#### `src/Nalix.Network/Connections/Connection.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 127  | `LogTrace` | `$"[NW.{nameof(Connection)}] created remote={this.NetworkEndpoint} id={this.ID}"` | ✅ SIMPLE |

> **Logger:** `_logger`

---

### 📁 Nalix.Hosting

---

#### `src/Nalix.Hosting/Protocols/DefaultFrameProcessor.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 154  | `LogTrace` | `$"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.Message` chiếm toàn bộ message (Rule 2) |

> **Logger:** `_logger`

---

#### `src/Nalix.Hosting/Internal/WebSocketServerListener.cs` — 1 vi phạm

| # | Line | Level | Log Message (gốc) | Đánh giá |
|---|------|-------|--------------------|----------|
| 1 | 73   | `LogTrace` | `$"[NW.{nameof(WebSocketListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}"` | 🟡 `[REQUIRES_REVIEW]` — `ex.Message` chiếm toàn bộ message (Rule 2) |

> **Logger:** `this.Logger`

---

## Tổng Hợp Các Trường Hợp `[REQUIRES_REVIEW]`

Tổng cộng **~35 vi phạm** cần PM/Sếp review trước khi sửa:

### Nhóm 1: Exception Properties Trong Chuỗi (vi phạm Rule 2 — 15 cases)

| File | Line | Log Level | Vấn đề |
|------|------|-----------|--------|
| `Protocol.PublicMethods.cs` | 116 | Warning | `{ex.ObjectName}` |
| `Protocol.PublicMethods.cs` | 130 | Debug | `{ex.Message}` |
| `TcpListener.Handle.cs` | 273 | Trace | `{ex.Message}` |
| `TcpListener.Handle.cs` | 725 | Warning | `{ex.SocketErrorCode}` |
| `TcpListener.SocketConfig.cs` | 88 | Warning | `{ex.Message}` |
| `TcpListener.ProxyProtocol.cs` | 276 | Trace | `{ex.SocketErrorCode}` |
| `SocketUdpTransport.cs` | 185 | Debug | `{ex.SocketErrorCode}` |
| `SocketUdpTransport.cs` | 169, 192, 199, 206, 221 | Debug | `{ex.GetType().Name}` (5 cases) |
| `DefaultFrameProcessor.cs` | 154 | Trace | `{ex.Message}` |
| `WebSocketServerListener.cs` | 73 | Trace | `{ex.Message}` |

> **Đề xuất sửa (Rule 2):** Truyền `ex` làm tham số **đầu tiên** của hàm Log, không format exception vào chuỗi.
> Ví dụ: `logger.LogError(ex, "message {Param}", value);`

### Nhóm 2: Ternary / Conditional (6 cases)

| File | Line | Biểu thức |
|------|------|-----------|
| `PacketHandlerCompiler.cs` | 113 | `{(compiledMethods.Count > 6 ? ",..." : string.Empty)}` |
| `PacketDispatchOptions.PublicMethods.cs` | 113 | `{(loopCount.HasValue ? loopCount.Value.ToString(...) : "auto")}` |
| `Protocol.Core.cs` | 107 | `{(isEnabled ? "enabled" : "disabled")}` |
| `TcpListener.ProcessChannel.cs` | 135, 166 | `{connection?.NetworkEndpoint.ToString() ?? "<null>"}` |
| `TcpListener.Handle.cs` | 416 | `{socket.RemoteEndPoint?.ToString() ?? "<null>"}` |

### Nhóm 3: Method Calls Trong Chuỗi (9 cases)

| File | Line | Method |
|------|------|--------|
| `PacketSender.cs` | 104 | `packet.GetType().Name` |
| `PacketDispatchOptions.PublicMethods.cs` | 85 | `middleware.GetType().Name` |
| `UdpListener.Core.cs` | 150 | `protocol.GetType().Name` |
| `Connection.Hub.cs` | 298 | `scope.GetElapsedMilliseconds()` |
| `SocketUdpTransport.cs` | 169, 192, 199, 206, 221 | `ex.GetType().Name` (5 cases) |

### Nhóm 4: Multi-Line Concatenation `$"" + $""` (10 cases)

| File | Lines |
|------|-------|
| `TokenBucketLimiter.Cleanup.cs` | 48-49, 187-188 |
| `PacketHandlerCompiler.cs` | 112-113 |
| `PacketDispatchOptions.PublicMethods.cs` | 260-261 |
| `Connection.Guard.cs` | 122-125 |
| `SocketConnection.Send.cs` | 130-131 |
| `SocketConnection.cs` | 592-593, 612-613 |
| `TcpListener.ProcessChannel.cs` | 135-136, 166-167 |

### Nhóm 5: Format Specifiers (4 cases)

| File | Line | Specifier |
|------|------|-----------|
| `TimeSynchronizer.cs` | 315 | `{this.Period.TotalMilliseconds:0.#}` |
| `Connection.Guard.cs` | 122-125 | `{_inactivityThreshold.TotalSeconds:F0}`, `{_cleanupInterval.TotalSeconds:F0}` |
| `Connection.Guard.cs` | 181, 433 | `{banUntil:HH:mm:ss}` |
| `Connection.Hub.cs` | 298 | `{scope.GetElapsedMilliseconds():F3}` |

---

## Ghi Chú Kỹ Thuật

### Về `nameof()` trong chuỗi log
Nhiều log message sử dụng `{nameof(...)}` cho prefix (ví dụ: `[RT.{nameof(TimeSynchronizer)}]`). Đây là **compile-time constant**, không tạo chuỗi runtime. Tuy nhiên khi refactor sang Structured Logging, cần quyết định:
- **Giữ `nameof()` trong chuỗi template** (khuyến nghị): `_logger.LogDebug("[RT.{ClassName}] initialized", nameof(TimeSynchronizer));`
- **Hoặc hardcode string**: `_logger.LogDebug("[RT.TimeSynchronizer] initialized");`

### Về `#if DEBUG` guards
- File `PacketSender.cs:104` nằm trong `#if DEBUG` block — khi refactor vẫn cần tuân thủ Rule 3 (giữ nguyên `if (s_logger.IsEnabled(...))`)

### Về Logger variable names

| Pattern | Files |
|---------|-------|
| `s_logger` (static) | `TimeSynchronizer`, `Protocol.*`, `AsyncCallback`, `SocketUdpTransport`, `PacketSender` |
| `_logger` (instance) | `TokenBucketLimiter`, `PolicyRateLimiter`, `ConcurrencyGate.Cleanup`, `SocketConnection.*`, `Connection.*`, `ConnectionGuard`, `DatagramGuard`, `TimingWheel`, `NetworkBanRepository`, `NetworkAccessList` |
| `this.Logger` (property) | `TcpListener.*`, `WebSocketListener.*`, `UdpListener.*`, `ConcurrencyGate.Types` |
| `this.Logging` (property) | `PacketDispatchOptions.PublicMethods` |
| `logger` (parameter) | `PacketHandlerCompiler`, `Connection.Hub:298`, `ThrottledLogGate` |

---

## Checklist Cho Bước 2 (PM/Sếp Review)

- [ ] Duyệt tất cả các case ✅ SIMPLE → Bật đèn xanh cho Dev sửa
- [ ] Review 15 cases Exception (Nhóm 1) → Quyết định có cần giữ `ex.Message` trong chuỗi không
- [ ] Review 6 cases Ternary (Nhóm 2) → Quyết định format output
- [ ] Review 9 cases Method Calls (Nhóm 3) → Có nên extract ra biến trước khi log?
- [ ] Review 10 cases Multi-line (Nhóm 4) → Gộp thành 1 template hay giữ nguyên?
- [ ] Review 4 cases Format Specifier (Nhóm 5) → Structured logging không hỗ trợ format inline, cần xử lý riêng
- [ ] Quyết định về `nameof()` trong template — giữ hay hardcode?

---

*Báo cáo này được tạo tự động bằng `rg` (ripgrep). Dev không được phép sửa code cho đến khi có approval từ PM/Sếp.*