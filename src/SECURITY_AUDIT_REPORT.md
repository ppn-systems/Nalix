# Bao Cao Ra Soat Bug & Bao Mat - Thu Muc `src`

- Du an: `Nalix`
- Pham vi: toan bo ma nguon trong `src` (bo qua `bin/obj`)
- Ngay thuc hien: 2026-04-15
- Hinh thuc: static code review (doc code, truy vet luong xu ly, doi chieu rui ro bao mat)

## 1. Tong Quan

Qua quet ma nguon, phat hien **5 van de bao mat**. Rui ro tap trung o cac luong: **session resume**, **xac thuc UDP datagram**, va **secure-by-default cua SDK client**.

- Critical: 1
- High: 3
- Medium: 1
- Low: 0

## 2. Phuong Phap

- Rasoat thu cong cac module nhay cam: handshake, session, transport, crypto, frame processing.
- Tim mau code co rui ro: auth/authz, token handling, replay resistance, secure defaults, trust boundary endpoint/IP:port.
- Doi chieu luong client-server de xac dinh kha nang tan cong thuc te.

## 3. Danh Sach Phat Hien

| ID | Muc do | Tieu de | Trang thai |
|---|---|---|---|
| SEC-01 | Critical | Session resume khong xac thuc so huu secret (chi can token) | ✅ FIXED |
| SEC-02 | High | Session token duoc tao bang Snowflake co tinh du doan | ✅ FIXED |
| SEC-03 | High | Session token khong one-time, co the replay nhieu lan den het TTL | ✅ FIXED |
| SEC-04 | High | UDP fast-path trust theo endpoint cache, khong kiem tra lai token moi datagram | ✅ FIXED |
| SEC-05 | Medium | SDK de mac dinh `EncryptionEnabled=false`, co the gui plaintext truoc handshake | ✅ FIXED |

---

## 4. Chi Tiet Tung Phat Hien

### SEC-01 (Critical): Session resume khong xac thuc so huu secret (chi can token) — ✅ FIXED

**Mo ta**

Luong resume hien tai chap nhan phuc hoi session neu `SessionToken` ton tai trong store. Khong co proof/challenge-response dua tren secret hien co cua session.

**Bang chung code**

- Client gui resume packet voi `encrypt: false`:
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:48`
- Server endpoint cho phep packet khong ma hoa:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:34`
- Server chi lookup token va ap session vao ket noi:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:59`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:67`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:89`

**Tac dong**

Ke tan cong chi can co token hop le (lo, sniff, doan duoc) co the chiem session va nhan quyen cua nguoi dung do. Day la lo hong authN/authZ nghiem trong.

**Tham chieu CWE**

- CWE-306: Missing Authentication for Critical Function
- CWE-287: Improper Authentication

---

### SEC-02 (High): Session token duoc tao bang Snowflake co tinh du doan — ✅ FIXED

**Mo ta**

Session token duoc tao boi `Snowflake.NewId(SnowflakeType.Session)`. Cau truc Snowflake trong code cho thay token duoc dong goi tu time + sequence + machine id, khong phai random cryptographic token.

**Bang chung code**

- Tao session token bang Snowflake:
  - `src/Nalix.Network/Sessions/SessionStoreBase.cs:35`
- Snowflake su dung `now`, `sequence`, `machineId` de tao value:
  - `src/Nalix.Framework/Identifiers/Snowflake.cs:225`
  - `src/Nalix.Framework/Identifiers/Snowflake.cs:240`
  - `src/Nalix.Framework/Identifiers/Snowflake.cs:269`
  - `src/Nalix.Framework/Identifiers/Snowflake.cs:272`

**Tac dong**

Khong gian token bi giam entropy thuc te va co cau truc doan duoc theo thoi gian/phat sinh. Khi ket hop voi SEC-01, rui ro takeover tang manh.

**Tham chieu CWE**

- CWE-330: Use of Insufficiently Random Values
- CWE-331: Insufficient Entropy

---

### SEC-03 (High): Session token khong one-time, co the replay nhieu lan den het TTL — ✅ FIXED

**Mo ta**

Khi resume thanh cong, token khong bi rotate hoac revoke. `RetrieveAsync` chi doc session va tra ve; khong co co che consume-once. Nhu vay mot token da lo co the duoc dung lap lai nhieu lan trong suot TTL.

**Bang chung code**

- Resume thanh cong nhung khong remove/rotate token:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:59`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:67`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:73`
- Session store retrieve khong consume token:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:50`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:70`

**Tac dong**

Token replay hop le trong khoang thoi gian session con han, mo rong cua so tan cong va kho thu hep sau su co lo token.

**Tham chieu CWE**

- CWE-294: Authentication Bypass by Capture-replay

---

### SEC-04 (High): UDP fast-path trust theo endpoint cache, khong kiem tra lai token moi datagram — ✅ FIXED

**Mo ta**

Sau khi datagram dau tien resolve thanh cong, he thong cache `EndPoint -> Connection` va fast-path bo qua buoc parse/validate token cho cac datagram sau. Neu endpoint bi spoof/chiem dung (dac biet trong mang noi bo, NAT edge-case, hoac compromised host), datagram co the duoc day vao connection chi dua tren `remoteEndPoint`.

**Bang chung code**

- Co endpoint cache va mo ta skip hub/token lookup:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Core.cs:58`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Core.cs:62`
- Fast-path su dung cache va xu ly tiep ma khong parse token lai:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:161`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:178`
- Token chi duoc parse o slow-path:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:208`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:222`

**Tac dong**

Mat rang buoc xac thuc theo moi datagram tren UDP channel, tang rui ro injection/impersonation theo endpoint trong cac dieu kien tan cong phu hop.

**Tham chieu CWE**

- CWE-290: Authentication Bypass by Spoofing

---

### SEC-05 (Medium): SDK de mac dinh `EncryptionEnabled=false`, co the gui plaintext truoc handshake — ✅ FIXED

**Mo ta**

`TransportOptions` dat mac dinh tat ma hoa. `TcpSession.SendAsync(...)` khong enforce "must-handshake-before-send", nen neu ung dung su dung sai thu tu goi API, du lieu co the duoc gui plaintext.

**Bang chung code**

- Mac dinh tat ma hoa:
  - `src/Nalix.SDK/Options/TransportOptions.cs:132`
- SendAsync khong co guard handshake/encryption state:
  - `src/Nalix.SDK/Transport/TcpSession.cs:167`
  - `src/Nalix.SDK/Transport/TcpSession.cs:176`

**Tac dong**

Rui ro misconfiguration/unsafe usage o phia client, dan den lo du lieu tren kenh TCP truoc khi handshake duoc thuc hien.

**Tham chieu CWE**

- CWE-319: Cleartext Transmission of Sensitive Information

---

## 5. Danh Gia Rui Ro Tong The

He thong hien tai co be mat tan cong cao tai flow resume va UDP trust boundary. Rui ro thuc te la **session hijacking**, **replay takeover**, va **udp packet injection trong mot so dieu kien mang**. Nen uu tien xu ly nhom auth/resume truoc.

## 6. Luu Y Gioi Han Danh Gia

- Day la static review, chua bao gom pentest/traffic simulation.
- `UdpListenerBase.IsAuthenticated(...)` la abstract; danh gia SEC-04 duoc dua tren trust model hien tai cua fast-path va luong token parsing.
- Chua xac nhan co WAF/mTLS/network segmentation o tang ha tang de giam nhe tac dong.

## 7. Kien Nghi Uu Tien (khong sua code trong bao cao nay)

1. Bo sung proof-of-possession cho resume (challenge-response bang secret session).
2. Thay session token bang CSPRNG token (>=128-bit entropy).
3. Ap dung rotate/consume-once token khi resume thanh cong.
4. Buoc kiem tra token/cryptographic tag theo tung UDP datagram, khong chi dua vao endpoint cache.
5. Chuyen SDK sang secure-by-default: encryption on by default va chan send neu chua handshake (hoac canh bao hard-fail).

---

## 8. Cap Nhat Bo Sung (Bug/Reliability) - 2026-04-15

### 8.1 Tong ket bo sung

Trong dot quet bo sung tap trung vao `Configuration`, phat hien them **2 van de bug/reliability** co the gay hanh vi khong on dinh o runtime:

- BUG-01 (High): Su dung read-lock nhung lai mutate state trong `IniConfig.Load()`. ✅ FIXED
- BUG-02 (Medium): `ReloadAll()` nuot exception va van log `reload-ok`, gay false-positive ve trang thai reload. ✅ FIXED

### BUG-01 (High): `IniConfig.Load()` mutate state duoi read-lock — ✅ FIXED

**Mo ta**

`Load()` vao `EnterReadLock()` nhung lai thuc hien thao tac ghi (`_iniData.Clear()`, `_valueCache.Clear()`, them/sua dictionary section/key). Day la vi pham mo hinh dong bo va co nguy co race-condition/InvalidOperation trong truong hop truy cap dong thoi.

**Bang chung code**

- Read lock trong `Load()`:
  - `src/Nalix.Framework/Configuration/Internal/IniConfig.cs:1049`
- Mutate du lieu duoi read lock:
  - `src/Nalix.Framework/Configuration/Internal/IniConfig.cs:1058`
  - `src/Nalix.Framework/Configuration/Internal/IniConfig.cs:1059`

**Tac dong**

- Co the gay state khong nhat quan cua INI cache trong truong hop da luong.
- Tang rui ro loi runtime kho tai hien (intermittent), dac biet khi file watcher trigger `Reload()` cung luc voi luong doc config.

---

### BUG-02 (Medium): `ConfigurationManager.ReloadAll()` nuot loi nhung van bao `reload-ok` — ✅ FIXED

**Mo ta**

Trong `ReloadAll()`, exception duoc bat vao bien `reloadException` nhung khong duoc throw, khong duoc log error, va luong xu ly van di tiep den log `reload-ok`. Dieu nay lam he thong va van hanh tin rang reload thanh cong du thuc te co the da that bai.

**Bang chung code**

- Bat loi vao bien tam:
  - `src/Nalix.Framework/Configuration/ConfigurationManager.cs:400`
  - `src/Nalix.Framework/Configuration/ConfigurationManager.cs:424`
- Van log thanh cong:
  - `src/Nalix.Framework/Configuration/ConfigurationManager.cs:433`
  - `src/Nalix.Framework/Configuration/ConfigurationManager.cs:434`

**Tac dong**

- Mat kha nang observability/chuan doan su co config.
- Co the dan den he thong chay voi config cu/khong day du nhung van bao "ok", lam cham phat hien su co production.

---

## 9. Cap Nhat Bo- SEC-13 (High): Handshake replay attack — ✅ FIXED
- SEC-14 (High): ConnectAsync Race condition gay ra orphaned sockets — ✅ FIXED (Verified locking)
- SEC-15 (High): ChaCha20 block counter overflow — ✅ FIXED

**Mo ta**

Trong `ConnectWithResumeAsync`, SDK tam thoi set `session.Options.EncryptionEnabled = false` truoc khi goi `HandshakeAsync`. `FrameSender` su dung gia tri nay de quyet dinh co ma hoa outbound hay khong. Neu co luong khac cung luc goi `SendAsync(...)`, packet co the bi gui plaintext trong cua so race-condition.

**Bang chung code**

- Tam tat ma hoa toan cuc tren session options:
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:118`
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:119`
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:130`
- Luong send phu thuoc truc tiep vao `EncryptionEnabled`:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:57`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:61`

**Tac dong**

Rui ro lo du lieu plaintext theo race-condition khi ung dung co nhieu luong gui dong thoi (dac biet trong reconnect/resume flow).

**Tham chieu CWE**

- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-319: Cleartext Transmission of Sensitive Information

---

### SEC-07 (Medium): Session secret khong duoc zeroize khi session bi thu hoi/het han — ✅ FIXED

**Mo ta**

`SessionSnapshot` luu `Secret` de phuc hoi session, nhung khi `Return()` duoc goi (remove/expire), chi tra `Attributes` ve pool, khong xoa mang byte secret. Du lieu nhay cam co the ton tai trong bo nho den khi GC thu hoi va co nguy co bi lo qua memory dump/forensic.

**Bang chung code**

- Secret duoc luu trong snapshot:
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:33`
- Return khong zeroize secret:
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:53`
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:55`
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:56`
- Session het han/bi xoa goi `Return()`:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:42`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:64`

**Tac dong**

Tang rui ro ton luu secret sau vong doi session, anh huong den hygiene cua du lieu nhay cam trong memory.

**Tham chieu CWE**

- CWE-226: Sensitive Information in Resource Not Removed Before Reuse

---

## 10. Cap Nhat Bo Sung (Serialization/DoS) - 2026-04-15

### SEC-08 (High): Gioi han kich thuoc deserialize qua lon, co nguy co memory-exhaustion DoS — ✅ FIXED

**Mo ta**

Nhe serializer cho phep do dai collection/string toi da gan `int.Max` (`MaxArray`, `MaxString`), trong khi day la gia tri rat cao voi du lieu den tu mang. Nhieu formatter deserialize se cap phat bo nho dua tren length nay (`new List<T>(count)`, `GC.AllocateUninitializedArray<T>(length)`), dan den nguy co OOM/GC pressure nghiem trong neu payload bi craft co do dai lon.

**Bang chung code**

- Gioi han toi da rat cao:
  - `src/Nalix.Common/Serialization/SerializerBounds.cs:20`
  - `src/Nalix.Common/Serialization/SerializerBounds.cs:25`
- Deserialize cap phat theo length:
  - `src/Nalix.Framework/Serialization/Formatters/Collections/ListFormatter.cs:88`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumListFormatter.cs:100`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/ArrayFormatter.cs:94`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumArrayFormatter.cs:107`

**Tac dong**

Co the bi khai thac de gay mat on dinh he thong (OOM, GC storm, latency spike) thong qua payload co length field bat thuong lon.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption

---

### SEC-09 (Medium): Nhanh `LiteSerializer` deserialize unmanaged array thieu rang buoc length/chong overflow day du — ✅ FIXED

**Mo ta**

Trong nhanh `TypeKind.UnmanagedSZArray` cua `LiteSerializer.Deserialize(...)`, `length` duoc doc truc tiep tu input va tinh `dataSize = size * length` ma khong ap gioi han nghiep vu (nhu `SerializerBounds.MaxArray`) va khong co guard overflow theo ngữ cảnh an toan. Viec nay tao be mat loi cho payload xau (overflow, invalid length, cap phat lon), dan den exception va DoS.

**Bang chung code**

- Doc length truc tiep:
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:574`
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:679`
- Nhan kich thuoc va cap phat mang tu length:
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:577`
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:586`
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:682`
  - `src/Nalix.Framework/Serialization/LiteSerializer.cs:691`

**Tac dong**

Tang rui ro crash/DoS khi gap payload khong tin cay co length field bat thuong trong luong deserialize.

**Tham chieu CWE**

- CWE-190: Integer Overflow or Wraparound
- CWE-400: Uncontrolled Resource Consumption

---

## 11. Cap Nhat Bo Sung (Unsafe Memory Path) - 2026-04-15

### SEC-10 (High): Integer overflow + sizeHint am trong DataReader mo duong unsafe copy kich thuoc lon bat thuong — ✅ FIXED

**Mo ta**

Nhieu formatter collection tinh `totalBytes = length * s_elementSize` bang `int`. Gia tri nay co the overflow thanh so am voi `length` lon. Sau do `reader.GetSpanReference(totalBytes)` duoc goi; ham nay chi kiem tra `sizeHint > BytesRemaining`, khong kiem tra `sizeHint < 0`. Ket qua la size am co the vuot check, roi di vao `Unsafe.CopyBlockUnaligned(..., (uint)totalBytes)` voi kich thuoc cast sang `uint` rat lon.

**Bang chung code**

- `GetSpanReference` khong chan `sizeHint` am:
  - `src/Nalix.Framework/Memory/Buffers/DataReader.cs:154`
  - `src/Nalix.Framework/Memory/Buffers/DataReader.cs:156`
  - `src/Nalix.Framework/Memory/Buffers/DataReader.cs:162`
- Tinh `totalBytes` bang phep nhan `int`:
  - `src/Nalix.Framework/Serialization/Formatters/Collections/ListFormatter.cs:91`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumListFormatter.cs:106`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumArrayFormatter.cs:97`
- Unsafe copy voi kich thuoc cast `uint`:
  - `src/Nalix.Framework/Serialization/Formatters/Collections/ListFormatter.cs:96`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumListFormatter.cs:111`
  - `src/Nalix.Framework/Serialization/Formatters/Collections/EnumArrayFormatter.cs:111`

**Tac dong**

Payload crafted co the kich hoat luong copy bat thuong (kich thuoc cuc lon), gay crash process/DoS nghiem trong trong duong deserialize unsafe.

**Tham chieu CWE**

- CWE-190: Integer Overflow or Wraparound
- CWE-787: Out-of-bounds Write
- CWE-400: Uncontrolled Resource Consumption

---

## 12. Cap Nhat Bo Sung (Protocol Error Handling) - 2026-04-15

### SEC-11 (Medium): `Protocol.ProcessFrame` nuot loi parse/decrypt va tiep tuc giu ket noi (fail-open ve mat tai nguyen) — ✅ FIXED

**Mo ta**

`ProcessFrame` bat exception voi filter `when (this.TryHandleProcessError(ex))`, trong khi `TryHandleProcessError(...)` luon `return true` cho ca nhanh known-error va unknown-error. Kết quả: loi duoc log nhung bi nuot, khong co hanh dong dong ket noi o lop base cho phan lon loi frame parsing/decrypt/decompress. Ke tan cong co the tiep tuc bam malformed frame tren cung ket noi de tao pressure log/CPU.

**Bang chung code**

- Catch + swallow trong `ProcessFrame`:
  - `src/Nalix.Network/Protocols/Protocol.Core.cs:43`
- Ham filter luon tra `true`:
  - `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs:81`
  - `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs:86`
  - `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs:90`

**Tac dong**

Tang be mat DoS theo ket noi (malformed packet flood), do ket noi khong bi cat dut sau cac loi frame nghiem trong o pipeline xu ly.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-400: Uncontrolled Resource Consumption

## 13. Cap Nhat Bo Sung (SDK Runtime Reliability) - 2026-04-15

### BUG-03 (High): `FrameSender.Dispose()` co the lam treo `SendAsync()` dang cho — ✅ FIXED

**Mo ta**

`FrameSender` dua ket qua gui frame ve caller qua `TaskCompletionSource<bool>` (moi frame mot `tcs`). Tuy nhien, khi `Dispose()` duoc goi, code chi `Cancel()` token + `TryComplete()` writer ma khong drain queue va khong complete/fail cac `tcs` dang cho. Neu co frame da enqueue nhung chua duoc drain, caller co the bi treo vo han tai `await tcs.Task`.

**Bang chung code**

- Tao `TaskCompletionSource` va await ket qua:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:75`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:77`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:78`
- Ket qua chi duoc set trong drain path:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:117`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:121`
- `Dispose()` khong complete cac item dang pending:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:211`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:213`

**Tac dong**

- Treo request/send task trong tinh huong shutdown/reconnect race.
- Co the gay tich lu y/c dang cho, timeout day chuyen va suy giam do on dinh runtime.

**Tham chieu CWE**

- CWE-667: Improper Locking (nhom concurrency/lifecycle issue)
- CWE-703: Improper Check or Handling of Exceptional Conditions

---

### BUG-04 (High): `TcpSession.SendAsync(...)` bo qua ket qua gui that bai (false-return) tu `FrameSender` — ✅ FIXED

**Mo ta**

`FrameSender.SendAsync(...)` tra ve `Task<bool>` de bieu thi ket qua gui frame. Khi loi socket xay ra trong drain loop, `FrameSender` set `tcs = false` va khong nem lai exception tren task cua caller. Tuy nhien `TcpSession.SendAsync(...)` chi `await` roi bo ket qua (`_ = await ...`), vi vay caller co the xem nhu gui thanh cong du frame thuc te da that bai.

**Bang chung code**

- `FrameSender` tra ket qua bool:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:54`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:79`
- Nhanh loi set `false`:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:121`
- `TcpSession` bo qua ket qua:
  - `src/Nalix.SDK/Transport/TcpSession.cs:176`
  - `src/Nalix.SDK/Transport/TcpSession.cs:181`

**Tac dong**

- Mat semantic "send guarantee": caller khong phan biet duoc frame da gui hay da roi.
- Co the gay sai lech nghiep vu (ack timeout, duplicate retry, state divergence) trong production.

**Tham chieu CWE**

- CWE-252: Unchecked Return Value
- CWE-391: Unchecked Error Condition

---

### BUG-05 (High): `UdpSession.SendAsyncInternal(...)` nuot loi gui, API co the bao thanh cong gia — ✅ FIXED

**Mo ta**

Trong `UdpSession.SendAsyncInternal`, neu `_socket.SendAsync(...)` nem exception, code chi raise `OnError`, goi `DisconnectAsync()`, sau do **khong rethrow**. Hai API public `SendAsync(...)` deu `await SendAsyncInternal(...)` nen caller co the nhan completion binh thuong du datagram thuc te khong duoc gui.

**Bang chung code**

- Catch nuot loi o send internal:
  - `src/Nalix.SDK/Transport/UdpSession.cs:268`
  - `src/Nalix.SDK/Transport/UdpSession.cs:274`
  - `src/Nalix.SDK/Transport/UdpSession.cs:276`
- Public send phu thuoc vao internal nay:
  - `src/Nalix.SDK/Transport/UdpSession.cs:206`
  - `src/Nalix.SDK/Transport/UdpSession.cs:244`

**Tac dong**

- Caller nhan false-success, kho phat hien mat goi/that bai gui.
- Lam sai co che retry/telemetry o tang ung dung vi khong co exception tuong ung.

**Tham chieu CWE**

- CWE-391: Unchecked Error Condition
- CWE-388: Error Handling

---

## 14. Cap Nhat Bo Sung (Callback Fairness/DoS) - 2026-04-15

### SEC-13 (High): Gioi han "per-IP" callback co the bi bypass do key thuc te gom ca port — ✅ FIXED

**Mo ta**

`AsyncCallback` mo ta co che fairness theo "remote IP", nhung implementation su dung dictionary key la `INetworkEndpoint`. Trong he thong hien tai, `SocketEndpoint` hash/equality co tinh den `Port` khi `HasPort=true`. Nghia la cung mot IP nhung khac source port se duoc tinh thanh nhieu key khac nhau, lam vo hieu hoa muc gioi han per-IP va mo rong be mat DoS.

**Bang chung code**

- `AsyncCallback` luu counter theo `INetworkEndpoint`:
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:91`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:186`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:297`
- `SocketEndpoint` dua `Port` vao equality/hash:
  - `src/Nalix.Network/Internal/Transport/SocketEndpoint.cs:140`
  - `src/Nalix.Network/Internal/Transport/SocketEndpoint.cs:168`
  - `src/Nalix.Network/Internal/Transport/SocketEndpoint.cs:171`
- `Connection` tao endpoint co port tu socket remote endpoint:
  - `src/Nalix.Network/Connections/Connection.cs:65`

**Tac dong**

- Ke tan cong co the tao nhieu ket noi cung IP nhung source port khac nhau de lan gioi han `MaxPendingPerIp`.
- Lam giam hieu qua fairness control va tang rui ro flood callback/CPU pressure.

**Tham chieu CWE**

- CWE-770: Allocation of Resources Without Limits or Throttling
- CWE-307: Improper Restriction of Excessive Authentication Attempts (ap dung theo nghia rate-limit bypass)

---

### BUG-06 (Medium): `ConnectionHub` co the spin-cho vo han khi full capacity o che do `DropPolicy.Block` — ✅ FIXED

**Mo ta**

Trong `TryReserveCapacitySlot`, khi dat nguong `MaxConnections` va `DropPolicy.Block`, code vao vong `while (true)` + `SpinWait/Yield` lien tuc cho den khi co slot trong. Nhanh nay khong co timeout, khong nhan cancellation token, va co the giu CPU dai neu he thong bi flood ket noi.

**Bang chung code**

- Vong lap vo han cho reserve slot:
  - `src/Nalix.Network/Connections/Connection.Hub.cs:781`
- Nhanh `DropPolicy.Block` chi spin/yield:
  - `src/Nalix.Network/Connections/Connection.Hub.cs:812`
  - `src/Nalix.Network/Connections/Connection.Hub.cs:819`

**Tac dong**

- Trong cau hinh `DropPolicy.Block`, attacker co the duy tri full-capacity de day server vao trang thai CPU burn/throughput giam.
- Tang latency accept path va co the tao starvation cho luong xu ly hop le.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption

---

## 15. Cap Nhat Bo Sung (Concurrency/Logic/Hardening) - 2026-04-15

### SEC-14 (High): Race condition trong `TcpSession.ConnectAsync` dan den ro ri socket (orphaned sockets) — ✅ FIXED

**Mo ta**

`ConnectAsync` thuc hien dọn dẹp ket noi cu (line 105) truoc khi ket noi moi, nhung khong co lock thuc su de ngan chan viec nhieu luong cung goi `ConnectAsync` dong thoi tren cung mot instance. Ket qua: nhieu socket co the duoc tao va ket noi vao server, nhung chi socket cuoi cung duoc gan vao `_socket`, cac socket khac tro thanh "orphaned" (chay ngam nhung khong the dispose qua session).

**Bang chung code**

- `src/Nalix.SDK/Transport/TcpSession.cs:105` (Goi DisconnectAsync nhung khong co lock toan cuc cho Connect flow).

**Tac dong**

- Gay ro ri resource (socket, memory) va file descriptors.
- Co the gay hanh vi bat thuong cho server khi nhan nhieu ket noi tu cung mot client instance ma khong biet.

---

### SEC-15 (Medium): `ChaCha20` khong compliant RFC 8439 ve viec tang nonce khi counter overflow — ✅ FIXED

**Mo ta**

Implementation cua `ChaCha20` hien tai gia dinh counter 32-bit se quay vong (wrap-around) ma khong lam gi ca. Tuy nhien, theo RFC 8439, mot so construction yeu cau phai dung lai hoac tang nonce de tranh trung lap keystream (keystream reuse vulnerability).

**Bang chung code**

- `src/Nalix.Framework/Security/Symmetric/ChaCha20.cs:340` (Thieu logic IncrementNonce hoac Stop khi counter dat max).

---

### SEC-16 (Critical): Mechansim proof-of-possession bi loi khien Session Resume khong kha thi — ✅ FIXED

**Mo ta**

Logic kiem tra secret trong `SessionHandlers` yeu cau secret tren doi tuong `Connection` phai khop voi secret trong session snapshot. Tuy nhien, khi mot ket noi moi den va muon resume, `connection.Secret` luon la `null`. Vi check bi loi, moi request resume deu bi tu choi, lam tinh nang nay tro nen vo dung va danh lua nguoi dung ve tinh bao mat cua flow.

**Bang chung code**

- `src/Nalix.Runtime/Handlers/SessionHandlers.cs:71-76`

---

### SEC-17 (Medium): Vi pham Constant-time trong cac primitives bao mat — ✅ FIXED

**Mo ta**

`FixedTimeEquals` thoat som khi do dai khac nhau. `IsZero` (unsafe path) cung thoat som ngay khi gap byte khac 0. Dieu nay cho phep tan cong timing (Timing Attack) de thong ke do dai secret hoac noi dung byte.

**Bang chung code**

- `src/Nalix.Framework/Security/Primitives/BitwiseOperations.cs:80-82`
- `src/Nalix.Framework/Security/Primitives/BitwiseOperations.cs:114-132`

---

### SEC-18 (Medium): Quyền hạn thư mục quá rộng trên Windows — ✅ FIXED

**Mo ta**

Hàm `HARDEN_PERMISSIONS` cấp quyền `Modify` cho nhóm `BuiltinUsers` trên Windows. Trong môi trường Share Hosting hoặc Multi-tenant, điều này cho phép các user khác có thể can thiệp vào file dữ liệu hoặc config.

**Bang chung code**

- `src/Nalix.Framework/Environment/Directories.UnixDirPerms.cs:141-158`

---

### BUG-07 (High): `InstanceManager` ro ri Disposeable objects do race condition — ✅ FIXED

**Mo ta**

Khi hai thread cung goi `Get<T>` cho mot Singleton dang chua ton tai, ca hai deu co the tao ra instance moi. Mac du `ConcurrentDictionary.GetOrAdd` chi luu mot cai, nhung instance "thua" se bi bo roi ma khong duoc dispose, gay memory/resource leak neu instance do implement `IDisposable`.

**Bang chung code**

- `src/Nalix.Framework/Injection/InstanceManager.cs:330-350` (Logic slow path thieu double-check lock truoc khi factory factory).

---

### BUG-08 (Medium): `DataReader` rò rỉ GCHandle nếu không được Dispose — ✅ FIXED

**Mo ta**

`DataReader` su dung `GCHandle` de pin mang byte khi duoc khoi tao tu mang. Neu user khong goi `Dispose()` (hoac khong dung `using`), mang byte se bi pin vao memory vo han, gay hien tuong fragmentation va memory leak.

**Bang chung code**

- `src/Nalix.Framework/Memory/Buffers/DataReader.cs:42-55`

---

### BUG-09 (Medium): Race condition trong `TimingWheel` tick-alignment — ✅ FIXED

**Mo ta**

Loop cua `TimingWheel` tinh `dueTime` du tren `DateTime.UtcNow` lien tuc. Trong moi truong amo-multi-thread, viec nay co the dan den viec tick bi cham hoac bo lo cac connection het han neu loop bi delay (CPU pressure).

**Bang chung code**

- `src/Nalix.Network/Internal/Time/TimingWheel.cs:220-250`

---

### BUG-10 (Medium): Packet re-ordering trong `TcpSession` do su dung `Task.Run` khong kiem soat — ✅ FIXED

**Mo ta**

Trong `OnMessageAsync` (hoac tuong duong), neu cac handler duoc thuc thi qua `Task.Run` ma khong co co che giu thu tu (Sequence ID), cac packet den sau co the duoc xu ly truoc packet den truoc neu handler dau tien bi delay.

**Bang chung code**

- `src/Nalix.SDK/Transport/TcpSession.cs:210-230`

---

### BUG-11 (Medium): Integer overflow trong formatting collections — ✅ FIXED

**Mo ta**

Cac formatter mảng/danh sách tính toán kích thước buffer bằng `length * elementSize` mà không kiểm tra tràn số.

**Bang chung code**

- `src/Nalix.Framework/Serialization/Formatters/Collections/ArrayFormatter.cs:50-70`

---

### BUG-12 (Medium): Data race trên `_configFilePath` trong `ConfigurationManager` — ✅ FIXED

**Mo ta**

Trường `_configFilePath` được truy cập từ nhiều thread (trong đó có `FileSystemWatcher` callback) mà không được bảo vệ bằng volatile hoặc lock đồng nhất, dẫn đến nguy cơ đọc dữ liệu cũ/không chính xác khi đường dẫn đang thay đổi.

**Bang chung code**

- `src/Nalix.Framework/Configuration/ConfigurationManager.cs:683`

---

### BUG-13 (Low): Potential 5s hang khi shutdown ConfigurationManager — ✅ FIXED

**Mo ta**

`DisposeManaged` đợi `_reloadGate` trong 5 giây. Nếu reload đang bị treo, quá trình shutdown sẽ bị trì hoãn.

**Bang chung code**

- `src/Nalix.Framework/Configuration/ConfigurationManager.cs:564`

---

### BUG-14 (Medium): Buffer lifecycle race trong `UdpSession.OnMessageReceived` — ✅ FIXED

**Mo ta**

`UdpSession` dispose datagram lease ngay sau khi invoke event `OnMessageReceived` đồng bộ. Nếu người dùng thực hiện xử lý bất đồng bộ (async) bên trong handler mà không Copy dữ liệu, họ sẽ truy cập vào buffer đã bị trả lại pool (Use-After-Free).

**Bang chung code**

- `src/Nalix.SDK/Transport/UdpSession.cs:325-330`

---

### BUG-15 (Medium): Integer overflow trong `LiteSerializer` cho unmanaged arrays — ✅ FIXED

**Mo ta**

Logic tính `dataSize + 4` có thể tràn số nếu mảng cực lớn, dẫn đến cấp phát buffer sai kích thước và gây lỗi khi copy dữ liệu.

**Bang chung code**

- `src/Nalix.Framework/Serialization/LiteSerializer.cs:88-89`

---

## 16. Cap Nhat Bo Sung (Deep Audit Phase 2) - 2026-04-15

### SEC-19 (High): INI Injection trong `IniConfig.WriteValue` — ✅ FIXED

**Mo ta**

Hàm `FormatValue` chuyển đổi object sang string để ghi vào file INI mà không thực hiện khử độc (sanitization) các ký tự xuống dòng (`\n`, `\r`). Nếu một giá trị chứa ký tự xuống dòng, nó có thể phá vỡ cấu trúc file INI và chèn thêm các section hoặc key độc hại.

**Bang chung code**

- `src/Nalix.Framework/Configuration/Internal/IniConfig.cs:985-1001`

**Tac dong**

- Kẻ tấn công có quyền thay đổi config (qua UI hoặc API) có thể leo thang đặc quyền hoặc thay đổi hành vi hệ thống bằng cách chèn thêm các cấu hình ẩn.

---

### SEC-20 (Medium): Nguy cơ DI Hijacking trong `InstanceManager` — ✅ FIXED

**Mo ta**

`InstanceManager.Register` cho phép ghi đè (replace) các instance đã tồn tại mà không có cơ chế "lockdown" sau khi khởi tạo xong. Một plugin hoặc module độc hại có thể thay thế các dịch vụ cốt lõi (như `IProtocol`, `ILogger`) bằng logic của riêng mình.

**Bang chung code**

- `src/Nalix.Framework/Injection/InstanceManager.cs:251`

---

### SEC-21 (Low): Mutex Name không hợp lệ và tiềm ẩn Crash — ✅ FIXED

**Mo ta**

`ApplicationMutexName` sử dụng `EntryAssembly.FullName` và tiền tố `"LOW\\"`. `FullName` của Assembly thường chứa các ký tự không hợp lệ cho Mutex name (như dấu phẩy, khoảng trắng). Ngoài ra, tiền tố `"LOW\\"` thường dùng cho Low Integrity Level trên Windows, có thể gây lỗi `AccessDenied` nếu process chạy ở mức integrity khác.

**Bang chung code**

- `src/Nalix.Framework/Injection/InstanceManager.cs:44`

---

### SEC-22 (Low): DDoS/Resource Exhaustion qua Task creation trong TcpListener — ✅ FIXED

**Mo ta**

Trong `TcpListener.ProcessChannel`, khi queue đầy và policy là `Block`, hệ thống tạo một `Task.Delay` và `await`. Trong kịch bản DDoS với hàng triệu kết nối, hàng triệu Task objects sẽ được tạo ra, gây áp lực cực lớn lên Heap và GC.

**Bang chung code**

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.ProcessChannel.cs:180-200`

---

### SEC-23 (High): Định danh Packet yếu trong `PacketBase<T>.Deserialize` — ✅ FIXED

**Mo ta**

`Deserialize` dựa vào `LiteSerializer.Deserialize<T>` nhưng không kiểm tra xem schema của `T` có thực sự khớp với dữ liệu nhận được hay không (ngoài Opcode). Nếu kẻ tấn công gửi một loại packet khác nhưng cùng Opcode (hoặc ép kiểu sai qua IL), hệ thống có thể hoạt động trên dữ liệu rác.

**Bang chung code**

- `src/Nalix.Framework/DataFrames/PacketBase.cs:450-480`

---

### SEC-24 (Medium): Timing Leak trong `BitwiseOperations.IsZero` — ✅ FIXED

**Mo ta**

Hàm `IsZero` sử dụng vòng lặp kiểm tra từng block `ulong` và `return false` ngay khi gặp giá trị khác 0. Điều này làm lộ thông tin về nội dung của buffer (ví dụ: byte đầu tiên khác 0 hay byte cuối cùng khác 0) qua thời gian xử lý.

**Bang chung code**

- `src/Nalix.Framework/Security/Primitives/BitwiseOperations.cs:114-132`

---

### BUG-16 (High): Race condition trong `TcpListenerBase.SCHEDULE_STOP` — ✅ FIXED

**Mo ta**

`SCHEDULE_STOP` đặt `_isActive = false` nhưng không có lock bảo vệ so với `Activate`. Nếu `Activate` được gọi ngay lúc `SCHEDULE_STOP` đang chạy, listener có thể bị rơi vào trạng thái "nửa sống nửa chết" (Stop gọi nửa chừng nhưng isActive vẫn bị set lại).

**Bang chung code**

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Core.cs:220`

---

### BUG-17 (Medium): Nuốt exception trong `TcpListener.INVOKE_PROCESS` — ✅ FIXED

**Mo ta**

Khi `INVOKE_PROCESS` gặp lỗi thực thi handler, nó log lỗi và đóng connection nhưng không ném lại (rethrow) exception hoặc thông báo cho Dispatcher. Điều này làm mất dấu vết các lỗi logic nghiêm trọng trong business handlers.

**Bang chung code**

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Handle.cs:550`

---

### BUG-18 (Low): Cleanup `_processThread` không triệt để khi shutdown — ✅ FIXED

**Mo ta**

`DisposeManaged` chỉ đợi `_processThread.Join(5000)`. Nếu thread bị treo (do I/O nghẽn), process sẽ thoát mà vẫn còn thread chạy ngầm, có thể gây lỗi `AccessViolation` khi các tài nguyên dùng chung đã bị giải phóng.

**Bang chung code**

- `src/Nalix.Network/Listeners/TcpListener/TcpListener.Core.cs:188`

---

### BUG-19 (Medium): `PacketBase<T>.Deserialize` trả về đối tượng khởi tạo một phần — ✅ FIXED

**Mo ta**

Nếu `LiteSerializer.Deserialize` chỉ đọc được một phần dữ liệu (do stream bị cắt hoặc lỗi format) mà không ném exception, `Deserialize` sẽ trả về đối tượng với các trường mang giá trị mặc định, dẫn đến logic nghiệp vụ sai lầm.

---

### BUG-20 (High): Double-registration `TimingWheel` trong `TcpListenerBase` — ✅ FIXED

**Mo ta**

Nếu `Activate` được gọi nhiều lần trên cùng một instance listener mà không stop, listener sẽ được đăng ký nhiều lần vào `TimingWheel`, dẫn đến việc quét dọn connection bị lặp lại vô ích và tốn hiệu năng.

---

### BUG-21 (Medium): Unsafe `Span` usage trong `DataReader.CheckSize` — ✅ FIXED

**Mo ta**

`CheckSize` thực hiện so sánh `sizeHint` nhưng không bảo vệ chống lại việc buffer bên dưới bị thay đổi bởi thread khác (mặc dù là `ReadOnlySpan`, nhưng nếu span đó view vào memory được map hoặc pool-shared buffer, nội dung vẫn có thể bị thay đổi).

---

### BUG-22 (Medium): Logic flush lỗi thời trong `IniConfig.WriteValue` — ✅ FIXED

**Mo ta**

`WriteValue` gọi `WriteFile()` (line 226) trong khi `Flush()` (line 959) cũng kiểm tra `_isDirty`. Việc `WriteValue` tự động flush mọi lúc làm giảm hiệu năng ghi đáng kể khi cần cập nhật nhiều cấu hình cùng lúc. Nên để người dùng chủ động gọi `Flush()` hoặc dùng timer.

---

## 18. Cap Nhat Bo Sung (Phase 4 - UDP & Randomness) - 2026-04-15

### SEC-27 (Critical): Thieu co che Replay Protection trong UDP Transport

**Mo ta**

Cả `UdpSession` (phía SDK) và `UdpListener` (phía Server) đều không sử dụng Sequence Number, Timestamp hoặc Nonce trong cấu trúc gói tin UDP. 
Cấu trúc hiện tại: `[SessionToken (7 bytes) | Payload ...]`.
Kẻ tấn công có thể bắt gói tin UDP hợp lệ (đã mã hóa) và gửi lại (replay) nhiều lần. Vì token vẫn hợp lệ và việc giải mã (nếu có) là tĩnh đối với cùng một nội dung, Server sẽ chấp nhận và xử lý lại lệnh (ví dụ: lệnh di chuyển, thực hiện hành động).

**Bang chung code**

- `src/Nalix.SDK/Transport/UdpSession.cs:204-207` (Outbound layout)
- `src/Nalix.Network.Pipeline/Throttling/UdpListener.Receive.cs:170-181` (Inbound handling)

**Tac dong**

- Người dùng có thể bị "teleport" hoặc thực hiện hành động lặp lại ngoài kiểm soát.
- Kẻ tấn công có thể spam các packet "nặng" để gây DoS business logic mà không cần bẻ khóa mã hóa.

---

### SEC-28 (High): Leak bo nho (Dictionary Exhaustion) trong `_endpointCache` cua UDP Listener

**Mo ta**

`UdpListenerBase` sử dụng một `ConcurrentDictionary` để cache `EndPoint -> Connection` nhằm tối ưu hóa xử lý (fast-path). Tuy nhiên, code hiện tại không có giới hạn kích thước (Bounded) và không có cơ chế dọn dẹp (Eviction) cho dictionary này. Kẻ tấn công có thể gửi các gói tin từ hàng triệu source port khác nhau (spoofed IP/Port) để làm đầy bộ nhớ Server.

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/UdpListener.Receive.cs:161` (Lookup)
- `src/Nalix.Network.Pipeline/Throttling/UdpListener.Receive.cs:266` (Add không giới hạn)

---

### SEC-30 (Critical): Nguy co chiem quyen dieu khien UDP Session (Session Hijacking)

**Mo ta**

Cơ chế phân giải Connection trên UDP hoàn toàn dựa vào ID 7-byte (`Snowflake`). Nếu ứng dụng không triển khai kiểm tra phụ (proof-of-possession) trong hàm `IsAuthenticated` (vốn là abstract), kẻ tấn công chỉ cần biết ID của một người dùng (thường là dữ liệu công khai trong game hoặc app) là có thể gửi UDP packet mạo danh người dùng đó.

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/UdpListener.Receive.cs:226-240`
- `src/Nalix.Network.Pipeline/Throttling/UdpListener.Receive.cs:314-318`

---

### SEC-32 (Critical): Su dung PRNG khong an toan cho muc dich mat ma (Xoshiro256++)

**Mo ta**

Lớp `Csprng` được quảng cáo là "cryptographically strong" nhưng tài liệu và fallback logic lại dựa trên thuật toán **Xoshiro256++**. Thuật toán này rất nhanh nhưng **KHÔNG** an toàn về mặt mật mã (không kháng được tấn công dự đoán trạng thái). Nếu hệ thống sử dụng nó để tạo Khóa (Key), IV, hoặc Nonce, tính bảo mật của toàn bộ các lớp mã hóa phía trên sẽ bị phá vỡ.

**Bang chung code**

- `src/Nalix.Framework/Random/Csprng.cs:13-15` (Tài liệu xác nhận dùng Xoshiro++)
- `src/Nalix.Framework/Random/Csprng.cs:43-47` (Fallback logic)

---

### BUG-27 (Medium): Race condition va giai phong tai nguyen sai cach trong `UdpSession`

**Mo ta**

Hàm `Dispose()` của `UdpSession` gọi `DisconnectAsync()` theo kiểu fire-and-forget (`_ = this.DisconnectAsync()`). Điều này gây ra race condition: socket có thể bị dispose trong khi luồng `ReceiveLoopAsync` vẫn đang cố gắng đọc từ nó, hoặc CTS bị dispose trước khi các task hoàn thành, dẫn đến các exception không mong muốn (`ObjectDisposedException`) không được handle sạch sẽ.

**Bang chung code**

- `src/Nalix.SDK/Transport/UdpSession.cs:149-168`
- `src/Nalix.SDK/Transport/UdpSession.cs:377-386`

---

### BUG-31 (Medium): Static Constructor cua `Csprng` co the gay treo toan bo App (DoS)

**Mo ta**

`Csprng` sử dụng static constructor để khởi tạo hàm random. Nếu tiến trình khởi tạo thất bại (ví dụ: không truy cập được OS entropy), nó sẽ throw `InvalidOperationException`. Điều này dẫn đến `TypeInitializationException` cho bất kỳ module nào cố gắng truy cập `Csprng`, khiến hệ thống không thể khởi động hoặc crash hàng loạt module quan trọng.

**Bang chung code**

- `src/Nalix.Framework/Random/Csprng.cs:33-51`

---

## 17. Cap Nhat Bo Sung (Phase 3 - High-Performance & Throttling) - 2026-04-15

### SEC-25 (Critical): Integer overflow trong `LiteSerializer.Serialize<T>` gay Out-of-bounds Write

**Mo ta**

Trong nhanh xử lý `TypeKind.UnmanagedSZArray` của hàm `Serialize<T>(in T value)`, biến `dataSize` được tính bằng phép nhân 32-bit: `int dataSize = size * length`. Với các mảng cực lớn, kết quả có thể overflow thành số âm. Khi đó:
1. `GC.AllocateUninitializedArray<byte>(dataSize + 4)` sẽ cấp phát một mảng rất nhỏ hoặc rỗng (do overflow tiếp).
2. `Unsafe.CopyBlockUnaligned` sử dụng `(uint)dataSize`. Nếu `dataSize` âm (ví dụ -4), nó sẽ cast sang `uint` cực lớn (~4GB) và thực hiện copy đè lên vùng nhớ ngoài bounds của buffer.

**Bang chung code**

- `src/Nalix.Framework/Serialization/LiteSerializer.cs:88-89`
- `src/Nalix.Framework/Serialization/LiteSerializer.cs:93-95`

**Tac dong**

- Gây crash process ngay lập tức (Access Violation).
- Tiềm ẩn nguy cơ khai thác Remote Code Execution (RCE) nếu kẻ tấn công kiểm soát được nội dung mảng bị serialize.

---

### SEC-26 (High): Rate Limit Bypass qua viec thay doi Source Port trong `PolicyRateLimiter`

**Mo ta**

`PolicyRateLimiter` sử dụng `RateLimitSubject` làm key để định danh đối tượng giới hạn. Key này bao gồm `INetworkEndpoint`. Tuy nhiên, `SocketEndpoint` (mặc định) thực hiện so sánh và hash bao gồm cả `Port`. Kẻ tấn công có thể dễ dàng vượt qua giới hạn rate limit bằng cách xoay vòng source port (đặc biệt dễ với UDP) để được tính là một "subject" mới.

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/PolicyRateLimiter.cs:206-208`
- `src/Nalix.Network.Pipeline/Throttling/PolicyRateLimiter.cs:576-577`

**Tac dong**

- Làm vô hiệu hóa khả năng chống DoS/Flood theo IP.
- Kẻ tấn công có thể spam hàng triệu request mà không bị chặn bởi policy định sẵn.

---

### BUG-23 (High): CPU DoS va Lock Contention trong luong bao cao cua `TokenBucketLimiter`

**Mo ta**

Các hàm diagnostic (`GenerateReport`, `GetReportData`) thực hiện thu thập snapshot của *tất cả* endpoint đang theo dõi. Quá trình này không chỉ iterate qua toàn bộ shards mà còn thực hiện **Sort** snapshot dựa trên áp lực (pressure). Trong lúc compare để sort, mã nguồn thực hiện `lock(state.Gate)` trên từng endpoint. 

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/TokenBucketLimiter.cs:905` (Giai đoạn Sort)
- `src/Nalix.Network.Pipeline/Throttling/TokenBucketLimiter.cs:925-935` (Lock trong lúc Sort)

**Tac dong**

- Nếu số lượng endpoint lớn (Max=50,000), việc gọi báo cáo sẽ gây "hứng" CPU (O(N log N) kèm lock).
- Gây nghẽn (starvation) cho luồng xử lý packet thực tế vì tất cả các lệnh `Evaluate` đều phải đợi lock từ luồng báo cáo.

---

### BUG-24 (Medium): Race condition trong `PolicyRateLimiter` sweep gay ra loi deny oan

**Mo ta**

Luồng background sweep (`EVICT_STALE_POLICIES`) thực hiện remove và `Dispose()` các limiter hết hạn. Có một cửa sổ race-condition: thread xử lý packet lấy được limiter (`TryGetValue`), nhưng ngay sau đó sweep xóa và dispose nó trước khi thread kia kịp gọi `TryAcquire`. Kết quả là `TryAcquire` trả về `false` và người dùng nhận lỗi `SoftThrottle` dù thực tế họ không vi phạm giới hạn.

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/PolicyRateLimiter.cs:594-598` vs `761-768`

---

### BUG-25 (Medium): Don dep stale endpoints khong deu giua cac Shards (Shard Starvation)

**Mo ta**

Hàm `CLEANUP_STALE_ENDPOINTS` có timeout 5 giây. Vòng lặp dọn dẹp luôn bắt đầu từ Shard đầu tiên. Trong kịch bản hệ thống bị flood lượng endpoint cực lớn, quá trình dọn dẹp có thể luôn bị timeout ở những shard đầu tiên, khiến các shard phía sau không bao giờ được quét tới.

**Bang chung code**

- `src/Nalix.Network.Pipeline/Throttling/TokenBucketLimiter.cs:1155`
- `src/Nalix.Network.Pipeline/Throttling/TokenBucketLimiter.cs:1194`

---

### BUG-26 (Medium): Memory Exhaustion qua viec cap phat upfront capacity trong Serializer

**Mo ta**

Các formatter cho `HashSet`, `Stack`, và `List` thực hiện khởi tạo collection với capacity bằng đúng `count` đọc từ payload ngay từ đầu (`new HashSet<T>(count)`, `new T[count]`). Dù `count` đã được giới hạn bởi `MaxArray` (1MB), nhưng một kẻ tấn công gửi nhiều request đồng thời với `count` lớn và các phần tử phức tạp (struct lớn) có thể nhanh chóng làm cạn kiệt bộ nhớ Server trước khi bắt đầu đọc dữ liệu thực sự.

**Bang chung code**

- `src/Nalix.Framework/Serialization/Formatters/Collections/HashSetFormatter.cs:176`
- `src/Nalix.Framework/Serialization/Formatters/Collections/StackFormatter.cs:173`
- `src/Nalix.Framework/Serialization/Formatters/Collections/StackFormatter.cs:182`

## 19. Cap Nhat Bo Sung (Session Resume Race) - 2026-04-15

### SEC-33 (High): Race-condition cho phep parallel replay voi session token one-time (TOCTOU)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Luong resume hien tai thuc hien theo thu tu: `RetrieveAsync(token)` -> verify proof -> `RemoveAsync(token)` -> `ApplySession(...)`.
Voi 2 request dong thoi cung token, ca hai co the cung `RetrieveAsync` thanh cong truoc khi token bi remove. Request den sau van co doi tuong `SessionEntry` hop le trong tay, va do code khong kiem tra ket qua consume mot-cach-atomically, ca hai request deu co the `ApplySession(...)` thanh cong.

**Bang chung code**

- Resume doc token roi moi remove (khong atomically consume):
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:61`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:96`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:98`
- Session store retrieve chi `TryGetValue`, khong consume-on-read:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:54`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:70`
- `RemoveAsync` khong tra ket qua de handler biet token da bi consume boi request khac:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:36`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:45`

**Tac dong**

- Vi pham tinh chat one-time cua resume token trong dieu kien race.
- Co the dan den session takeover song song (parallel replay), dac biet khi ke tan cong da co token + proof hop le va ban nhieu request cung luc.

**Tham chieu CWE**

- CWE-367: Time-of-check Time-of-use (TOCTOU) Race Condition
- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization

## 20. Cap Nhat Bo Sung (Fragmentation + Compression DoS) - 2026-04-15

### SEC-34 (High): Fragment reassembly khong gioi han so stream dang mo (memory exhaustion per connection)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`FragmentAssembler` gioi han kich thuoc moi stream qua `MaxStreamBytes`, nhung khong co hard-limit cho tong so stream dang mo (`_streams.Count`). Ke tan cong co the mo nhieu stream khac nhau (streamId khac nhau), moi stream gui chunk dau (index=0) de buoc cap phat `BufferLease` ngay lap tuc, roi khong hoan tat stream. Bo nho bi giu den khi timeout, tao cua so DoS theo memory.

**Bang chung code**

- Luu stream theo dictionary va khong co max-open-stream guard:
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:76`
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:86`
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:143`
- Tao stream moi va cap phat buffer ngay o chunk dau:
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:154`
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:155`
- Stream chi duoc thu hoi theo timeout/sweep:
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:92`
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:136`
  - `src/Nalix.Framework/DataFrames/Chunks/FragmentAssembler.cs:234`

**Tac dong**

- Tang nhanh memory footprint trong cua so timeout.
- Co the gay OOM/GC pressure cao khi bi fragment-flood co chu dich.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption

---

### SEC-35 (High): Pre-allocation theo LZ4 header chua validate day du (memory allocation DoS)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`PacketCompression.DecompressFrame(...)` cap phat destination buffer dua truc tiep vao `FrameTransformer.GetDecompressedLength(...)` (doc `header.OriginalLength`) truoc khi decode/validate day du khung LZ4. `GetDecompressedLength` hien chi check duoc do dai toi thieu cua header, khong rang buoc `OriginalLength` (am/qua lon) hay tinh hop le `CompressedLength` tai thoi diem cap phat.

Trong luong inbound, du lieu khong tin cay co co `COMPRESSED` flag se di vao nhanh nay, tao be mat allocation-based DoS.

**Bang chung code**

- Cap phat theo `GetDecompressedLength(...)` truoc validate decode:
  - `src/Nalix.Framework/DataFrames/Transforms/PacketCompression.cs:29`
  - `src/Nalix.Framework/DataFrames/Transforms/PacketCompression.cs:30`
- `GetDecompressedLength` tra thang `header.OriginalLength` sau check header toi thieu:
  - `src/Nalix.Framework/DataFrames/Transforms/FrameTransformer.cs:83`
  - `src/Nalix.Framework/DataFrames/Transforms/FrameTransformer.cs:90`
  - `src/Nalix.Framework/DataFrames/Transforms/FrameTransformer.cs:92`
- Duong vao inbound protocol/SDK:
  - `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs:171`
  - `src/Nalix.SDK/Transport/Internal/PacketFrameTransforms.cs:36`

**Tac dong**

- Payload compressed gia mao co the kich hoat cap phat bo nho rat lon ngay tu dau pipeline.
- Dan den pressure bo nho/GC, mat on dinh hoac OOM trong kich ban flood.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling

## 21. Cap Nhat Bo Sung (Dispatch & Authorization Defaults) - 2026-04-15

### SEC-36 (High): Dispatch queue mac dinh unbounded (MaxPerConnectionQueue=0) dan den memory DoS

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`DispatchOptions.MaxPerConnectionQueue` mac dinh la `0` (unlimited). Khi gia tri <= 0, `DispatchChannel` chuyen sang che do unbounded cho queue theo priority (`Channel.CreateUnbounded`). Ke tan cong co the flood packet nhanh hon toc do xu ly handler, lam so packet pending tang vo han theo ket noi -> memory tang khong gioi han.

**Bang chung code**

- Mac dinh unlimited:
  - `src/Nalix.Runtime/Options/DispatchOptions.cs:19`
  - `src/Nalix.Runtime/Options/DispatchOptions.cs:21`
- Chuyen sang unbounded mode khi queue <= 0:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:137`
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:140`
- Unbounded queue implementation:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:672`
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:881`
- Packet inbound duoc enqueue truc tiep vao dispatch channel:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:198`

**Tac dong**

- Tan cong flood co the day heap/GC pressure den OOM.
- Suy giam nghiem trong throughput/latency va co the lam chet process.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling

---

### SEC-37 (Medium): Authorization fail-open khi handler thieu `PacketPermissionAttribute`

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`PermissionMiddleware` cho phep packet di tiep neu `context.Attributes.Permission is null`. Metadata permission hien duoc lay tu attribute tren method; neu handler bi thieu annotation (hoac metadata provider khong gan), request se duoc xem nhu hop le va bo qua check quyen. Day la mo hinh fail-open ve authorization.

**Bang chung code**

- Nhanh cho phep khi Permission null:
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:45`
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:46`
- Permission metadata chi lay tu attribute, khong thay default-deny:
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:866`
  - `src/Nalix.Runtime/Dispatching/PacketMetadataBuilder.cs:35`

**Tac dong**

- Chi can sai sot cau hinh/annotation tren handler nhay cam la co the mo quyen truy cap ngoai y muon.
- Tang rui ro privilege bypass do human error trong qua trinh mo rong he thong.

**Tham chieu CWE**

- CWE-862: Missing Authorization
- CWE-284: Improper Access Control

## 22. Cap Nhat Bo Sung (Crypto Downgrade / Integrity) - 2026-04-15

### SEC-38 (Critical): Decrypt fail-open theo algorithm trong envelope header (co the downgrade AEAD -> non-AEAD)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Luong decrypt hien tai khong rang buoc cipher suite voi trang thai phien (`connection.Algorithm`/`options.Algorithm`) ma tin truc tiep `TYPE` trong envelope header do peer gui len. Vi vay attacker co the sua truong `TYPE` trong goi da ma hoa de ep nhanh decrypt sang `Chacha20`/`Salsa20` (khong co authentication tag), bien check toan ven tu bat buoc thanh tuy chon.

Noi cach khac, quyet dinh co verify tag hay khong dang bi dieu khien boi du lieu khong tin cay tren wire.

**Bang chung code**

- Server decrypt khong truyen expected algorithm, chi truyen key:
  - `src/Nalix.Network/Protocols/Protocol.Lifecycle.cs:129`
- SDK inbound decrypt cung khong rang buoc expected algorithm:
  - `src/Nalix.SDK/Transport/Internal/PacketFrameTransforms.cs:28`
- `PacketCipher.DecryptFrame` goi decrypt generic theo header:
  - `src/Nalix.Framework/DataFrames/Transforms/PacketCipher.cs:42`
- `EnvelopeCipher.Decrypt` parse envelope roi switch theo `env.AeadType` (tu header):
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:277`
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:279`
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:283`
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:288`
- Trong khi session van luu algorithm ky vong (nhung khong duoc enforce o decrypt path):
  - `src/Nalix.Network/Connections/Connection.cs:112`

**Tac dong**

- Mo duong downgrade tu AEAD sang stream cipher khong xac thuc.
- Mat bao dam integrity/anti-tamper cua payload tren kenh ma hoa neu attacker co kha nang sua du lieu tren duong truyen.

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-327: Use of a Broken or Risky Cryptographic Algorithm
- CWE-693: Protection Mechanism Failure

## 23. Cap Nhat Bo Sung (Control-Plane Crypto Policy) - 2026-04-15

### SEC-39 (Critical): Client co the doi cipher suite cua ket noi qua `ControlType.CIPHER_UPDATE` (crypto policy tampering)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`SystemControlHandlers.HandleCipherUpdate(...)` chap nhan `SYSTEM_CONTROL/CIPHER_UPDATE` tu peer va gan truc tiep `connection.Algorithm = ...` dua tren truong `packet.Reason`. Khong co rang buoc voi handshake state, khong co kiem tra quyen dac biet, va khong co verify tinh hop le cua transition cipher policy.

Dieu nay cho phep ben gui frame control thay doi thuat toan ma hoa o runtime cua ket noi, mo duong cho downgrade/algorithm-confusion va pha vo ky vong bao mat cua phien.

**Bang chung code**

- Handler SYSTEM_CONTROL cho phep packet khong ma hoa + Permission NONE:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:27`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:28`
- Nhanh CIPHER_UPDATE doi thang algorithm cua connection:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:37`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:73`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:78`
- `Connection.Algorithm` la state duoc decrypt path su dung de xu ly frame:
  - `src/Nalix.Network/Connections/Connection.cs:112`

**Tac dong**

- Peer/attacker co the can thiep crypto policy cua session trong luc dang hoat dong.
- Tang rui ro downgrade, algorithm confusion, va mat dong nhat security properties giua 2 dau ket noi.

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-284: Improper Access Control
- CWE-693: Protection Mechanism Failure

### SEC-40 (High): `CIPHER_UPDATE` khong validate gia tri `CipherSuiteType`, co the gay protocol DoS

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Trong `HandleCipherUpdate`, server ep kieu truc tiep `(CipherSuiteType)(byte)packet.Reason` va gan vao `connection.Algorithm` ma khong kiem tra `Enum.IsDefined` hoac whitelist suite duoc phep. Peer co the gui gia tri bat ky de dat connection vao state algorithm khong hop le/khong ho tro.

Sau do cac luong encrypt/decrypt tiep theo co the nem exception "Unsupported cipher type" va lam ket noi bi fail/disconnect, tao DoS co chu dich tren tung session.

**Bang chung code**

- Gan truc tiep algorithm tu input khong tin cay:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:83`
- Khong thay validate enum trong handler:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:73`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:87`
- Encrypt/decrypt se throw neu cipher khong ho tro:
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:232`
  - `src/Nalix.Framework/Security/EnvelopeCipher.cs:292`

**Tac dong**

- Gay mat dong bo crypto state va ngat ket noi do loi cipher.
- Co the bi khai thac de DoS phien/nguoi dung thong qua control frame crafted.

**Tham chieu CWE**

- CWE-20: Improper Input Validation
- CWE-754: Improper Check for Unusual or Exceptional Conditions
- CWE-400: Uncontrolled Resource Consumption

## 24. Cap Nhat Bo Sung (Object Pool Data Hygiene) - 2026-04-15

### SEC-41 (High): `SessionResume` pooled object khong reset truong `Proof`, co nguy co ro ri proof/du lieu nhay cam qua response

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`SessionResume` co truong `Proof` (32 bytes) duoc serialize trong packet. Tuy nhien, ca `Initialize(...)` va `ResetForPool()` deu khong reset `Proof`. Khi object duoc lay lai tu pool de tao response/ack, `Proof` co the giu gia tri cu tu request truoc do, va bi serialize gui ra mang ngoai y muon.

Day la data-leak cross-request qua object reuse, dac biet nguy hiem vi `Proof` lien quan den HMAC possession proof cua session.

**Bang chung code**

- `Proof` nam trong payload serialized:
  - `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs:73`
- `Initialize(...)` khong set lai `Proof`:
  - `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs:84`
  - `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs:92`
- `ResetForPool()` khong clear `Proof`:
  - `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs:95`
  - `src/Nalix.Framework/DataFrames/SignalFrames/SessionResume.cs:104`
- Server rent packet tu pool va gui ack ma khong gan proof:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:110`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:112`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:118`

**Tac dong**

- Lo du lieu proof cua request truoc sang response khac (cross-request information leak).
- Lam yeu hygiene cua du lieu nhay cam trong pooled objects, tao be mat tan cong replay/phan tich.

**Tham chieu CWE**

- CWE-226: Sensitive Information in Resource Not Removed Before Reuse
- CWE-200: Exposure of Sensitive Information to an Unauthorized Actor

### BUG-42 (Medium): `Directive` pooled packet thieu reset lifecycle, co nguy co reuse header state cu (OpCode)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`Directive` duoc lay/tra qua `ObjectPoolManager`, nhung class khong override `ResetForPool()`. Dong thoi overload `Initialize(...)` khong co tham so `opCode` cung khong gan lai `OpCode`. Trong object reuse scenario, header field co the giu state cu tu lan su dung truoc (neu truoc do packet da bi doi `OpCode`), dan den protocol metadata khong nhat quan.

**Bang chung code**

- `Directive` duoc pool-hoi dung:
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:53`
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:84`
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:85`
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:105`
- `Directive` khong co `ResetForPool()`:
  - `src/Nalix.Framework/DataFrames/SignalFrames/Directive.cs:15`
  - `src/Nalix.Framework/DataFrames/SignalFrames/Directive.cs:137`
- Overload `Initialize(...)` (khong opCode) khong set `OpCode`:
  - `src/Nalix.Framework/DataFrames/SignalFrames/Directive.cs:81`
  - `src/Nalix.Framework/DataFrames/SignalFrames/Directive.cs:100`

**Tac dong**

- Gay bat dinh protocol header (opcode metadata co the khong dung ky vong).
- Tang rui ro loi kho tai hien trong moi truong tai cao do object pooling reuse state.

**Tham chieu CWE**

- CWE-665: Improper Initialization
- CWE-664: Improper Control of a Resource Through its Lifetime

## 25. Cap Nhat Bo Sung (Async Dispatch Lease Lifecycle) - 2026-04-15

### BUG-43 (High): Rò rỉ `BufferLease` khi schedule async handler that bai/cancel (TCP + UDP SDK)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Co 2 duong loi lifecycle tuong tu:

1) `TcpSession.HandleReceiveMessage(...)` goi `lease.Retain()` truoc khi enqueue async work, nhung bo qua ket qua `writer.TryWrite(...)`. Neu queue da dong/khong nhan them, work khong duoc enqueue, `finally { lease.Dispose(); }` trong delegate khong bao gio chay => ref retain bi leak.

2) `UdpSession.ReceiveLoopAsync(...)` goi `datagram.Retain()` roi `Task.Run(..., ct)`. Neu `ct` da bi cancel tai thoi diem schedule, delegate co the khong chay, `finally { datagram.Dispose(); }` khong duoc thuc thi => ref retain bi leak.

Ca hai truong hop deu gay ro ri pooled buffer theo thoi gian trong kịch bản reconnect/disconnect race hoac shutdown.

**Bang chung code**

- TCP retain + bo qua ket qua enqueue:
  - `src/Nalix.SDK/Transport/TcpSession.cs:277`
  - `src/Nalix.SDK/Transport/TcpSession.cs:278`
  - `src/Nalix.SDK/Transport/TcpSession.cs:282`
- UDP retain + Task.Run voi cancellation token:
  - `src/Nalix.SDK/Transport/UdpSession.cs:359`
  - `src/Nalix.SDK/Transport/UdpSession.cs:360`
  - `src/Nalix.SDK/Transport/UdpSession.cs:364`
  - `src/Nalix.SDK/Transport/UdpSession.cs:365`

**Tac dong**

- Tich luy memory leak tren duong nhan goi (pooled buffer khong duoc tra lai day du).
- Tang GC/memory pressure, co the dan den suy giam hieu nang hoac OOM khi gap nhieu disconnect/reconnect/cancel.

**Tham chieu CWE**

- CWE-772: Missing Release of Resource after Effective Lifetime
- CWE-404: Improper Resource Shutdown or Release

## 26. Cap Nhat Bo Sung (Deserialize Consistency) - 2026-04-15

### BUG-44 (Medium): `PacketBase.Deserialize(ReadOnlyMemory<byte>, ref TSelf)` thieu check consume-to-end (parser inconsistency)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Overload `PacketBase.Deserialize(ReadOnlyMemory<byte>, ref TSelf)` chi kiem tra `bytesRead == 0`, nhung khong kiem tra truong hop `bytesRead < buffer.Length`. Nghia la payload co trailing bytes van duoc chap nhan.

Trong khi overload `PacketBase.Deserialize(ReadOnlySpan<byte>)` lai co check nghiem ngat va throw neu con du lieu thua. Su bat nhat giua hai API deserialize co the gay parser differential behavior (mot ben reject, mot ben accept), mo duong cho logic bug va bypass cac rule duoc xay tren ky vong "parse het frame".

**Bang chung code**

- Overload span co check consume-to-end:
  - `src/Nalix.Framework/DataFrames/PacketBase.cs:180`
  - `src/Nalix.Framework/DataFrames/PacketBase.cs:185`
- Overload memory+ref thieu check trailing bytes:
  - `src/Nalix.Framework/DataFrames/PacketBase.cs:204`
  - `src/Nalix.Framework/DataFrames/PacketBase.cs:214`

**Tac dong**

- Co the dan den hanh vi parse khong nhat quan giua cac call-site.
- Tang rui ro bug logic/validation bypass khi call-site dung overload `ref` de tai su dung object.

**Tham chieu CWE**

- CWE-20: Improper Input Validation
- CWE-444: Inconsistent Interpretation of Input

## 27. Cap Nhat Bo Sung (ConnectionHub Disposal Race) - 2026-04-15

### BUG-45 (High): Race trong `ConnectionHub.Dispose()` cho phep dang ky connection moi trong luc dang shutdown

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`Dispose()` goi `CloseAllConnections("disposed")` truoc, sau do moi set `_disposed = true`. Trong cua so nay, `RegisterConnection/TryRegisterCore` van co the chay vi check `_disposed` dau vao chua bi bat. Ket qua: ket noi moi co the duoc them vao hub trong luc hub dang dong.

Vi `CloseAllConnections` snapshot danh sach ket noi tai mot thoi diem, cac ket noi dang ky muon co the khong nam trong snapshot va khong bi disconnect dung quy trinh shutdown, gay state khong nhat quan va race lifecycle.

**Bang chung code**

- `Dispose()` set `_disposed` sau khi close-all:
  - `src/Nalix.Network/Connections/Connection.Hub.cs:643`
  - `src/Nalix.Network/Connections/Connection.Hub.cs:644`
- Register path chi chan theo `_disposed` check dau vao:
  - `src/Nalix.Network/Connections/Connection.Hub.cs:670`
  - `src/Nalix.Network/Connections/Connection.Hub.cs:672`
- `CloseAllConnections` dua tren snapshot, khong khoa chan dang ky moi trong qua trinh xu ly:
  - `src/Nalix.Network/Connections/Connection.Hub.cs:401`
  - `src/Nalix.Network/Connections/Connection.Hub.cs:424`
  - `src/Nalix.Network/Connections/Connection.Hub.cs:430`

**Tac dong**

- Hub co the roi vao trang thai disposal race (dang dispose nhung van nhan connection moi).
- Gay leak state, lifecycle khong nhat quan, va loi ngau nhien trong pha shutdown/restart.

**Tham chieu CWE**

- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-664: Improper Control of a Resource Through its Lifetime
## 28. Cap Nhat Bo Sung (Session Lifecycle Hygiene) - 2026-04-15

### BUG-46 (High): `SessionHandlers` consume session nhung khong `Return()`, gay leak resource va keo dai du lieu nhay cam trong bo nho

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Trong luong resume, `SessionHandlers.HandleAsync(...)` goi `Hub.SessionStore.ConsumeAsync(...)` de lay va xoa atomically `SessionEntry` khoi store. Sau khi verify proof thanh cong, code chi `ApplySession(...)` va tao token moi, nhung khong goi `session.Return()` cho entry cu.

`SessionEntry.Return()` moi la noi giai phong `SessionSnapshot` noi bo: zeroize `Secret` va tra `Attributes` object-map ve pool. Viec bo sot buoc nay gay 2 he qua: (1) object-map attribute bi giu lai (resource leak) va (2) key/session secret cua snapshot cu khong duoc xoa som theo lifecycle mong doi.

**Bang chung code**

- Consume session entry:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:64`
- Duong thanh cong khong goi `session.Return()`:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:99`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:114`
- Co `session.Return()` o nhanh loi (chung to can lifecycle release) nhung khong co o nhanh thanh cong:
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:77`
  - `src/Nalix.Runtime/Handlers/SessionHandlers.cs:92`
- `SessionEntry.Return()` dan toi `SessionSnapshot.Return()` (zero secret + return attributes):
  - `src/Nalix.Common/Networking/Sessions/SessionEntry.cs:37`
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:55`
  - `src/Nalix.Common/Networking/Sessions/SessionSnapshot.cs:62`

**Tac dong**

- Ro ri tai nguyen theo thoi gian (object-map/attribute snapshot khong duoc tra pool) trong he thong co nhieu lan resume.
- Du lieu nhay cam (`Secret`) cua snapshot cu ton tai lau hon vong doi can thiet, lam giam memory hygiene va tang be mat forensic-memory exposure.

**Tham chieu CWE**

- CWE-772: Missing Release of Resource after Effective Lifetime
- CWE-226: Sensitive Information in Resource Not Removed Before Reuse
- CWE-459: Incomplete Cleanup

## 29. Cap Nhat Bo Sung (Session Store Retention DoS) - 2026-04-15

### SEC-47 (High): `InMemorySessionStore` khong cleanup TTL chu dong, cho phep tich luy session het han vo han (memory DoS)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`InMemorySessionStore` luu session trong `ConcurrentDictionary` va chi kiem tra het han TTL khi co thao tac `RetrieveAsync(...)` hoac `ConsumeAsync(...)` tren chinh token do. Khong co background sweeper/scavenger de quet va xoa token het han theo thoi gian.

Dong thoi, moi handshake thanh cong deu tao session moi va `StoreAsync(...)` ngay lap tuc. Mot attacker co the tao nhieu ket noi, hoan tat handshake, sau do ngat ket noi ma khong bao gio resume token cu. Cac session het han khong duoc truy cap lai se tiep tuc nam trong dictionary, dan den tang bo nho lien tuc.

**Bang chung code**

- Store chi add/overwrite, khong cleanup:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:25`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:31`
- Het han chi duoc xu ly lazy khi Retrieve/Consume token cu the:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:55`
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:82`
- Khong co worker/background task cleanup trong implementation session store:
  - `src/Nalix.Network/Sessions/InMemorySessionStore.cs:17`
  - `src/Nalix.Network/Sessions/SessionStoreBase.cs:22`
- Handshake thanh cong luon tao + luu session moi:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:157`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:160`

**Tac dong**

- Co the khai thac de lam phinh dictionary session bang luong handshake/disconnect lap lai, gay memory pressure va OOM.
- He thong don node (default in-memory store) dac biet de bi anh huong trong kịch ban tan cong low-cost.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling

## 30. Cap Nhat Bo Sung (Global Clock Trust Boundary) - 2026-04-15

### SEC-48 (Medium): `SyncTimeAsync` cho phep mot session don le chinh sua `Clock` global cua process (cross-session trust bleed)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`TimeSyncExtensions.SyncTimeAsync(...)` nhan `TIMESYNCRESPONSE` tu session hien tai roi goi truc tiep `Clock.SynchronizeUnixMilliseconds(...)`. `Clock` la static global state (offset/drift dung chung cho toan process), nen mot session co the tac dong den nhan thoi gian cua toan bo thanh phan khac trong cung process.

Hien tai khong co co che rang buoc trust-domain (vi du: pin theo endpoint trusted, mode opt-in theo service, hay scope clock theo session). Neu session bi MITM/peer khong dang tin, clock global co the bi skew, gay sai lech logic timeout/TTL/scheduling o noi khac.

**Bang chung code**

- `SyncTimeAsync` chap nhan timestamp response va dong bo clock global:
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:45`
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:53`
- `Clock` luu state static toan cuc (`s_timeOffset`, `s_driftCorrection`, `IsSynchronized`):
  - `src/Nalix.Framework/Time/Clock.cs:24`
  - `src/Nalix.Framework/Time/Clock.cs:27`
  - `src/Nalix.Framework/Time/Clock.cs:44`
- Dong bo thay doi state static bang `Volatile.Write`:
  - `src/Nalix.Framework/Time/Clock.cs:113`
  - `src/Nalix.Framework/Time/Clock.cs:127`

**Tac dong**

- Mot kenh ket noi khong tin cay co the anh huong hanh vi thoi gian cua cac module khac trong cung process.
- Tang rui ro logic bug lien quan timeout/TTL/rate-window neu ung dung dua vao `Clock.NowUtc()` thay vi `DateTime.UtcNow`.

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-1220: Insufficient Granularity of Access Control
- CWE-840: Business Logic Errors

## 31. Cap Nhat Bo Sung (Handshake State Machine) - 2026-04-15

### SEC-49 (High): Handshake co the bi kich hoat lai tren ket noi da establish (state machine bypass / session churn DoS)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`HandshakeHandlers.HandleAsync(...)` chap nhan `CLIENT_HELLO` va `CLIENT_FINISH` ma khong kiem tra connection da handshake thanh cong truoc do hay chua. Handler handshake duoc danh dau `PacketEncryption(false)` + `PermissionLevel.NONE`, nen control flow cho phep xu ly handshake packet o moi thoi diem cua vong doi ket noi.

He qua: peer co the lien tuc kich hoat lai handshake tren ket noi da hoat dong, gay churn crypto state (`connection.Secret`, `connection.Algorithm`) va tao them session token moi lien tuc. Day la loi state-machine (thieu transition guard), co the bi dung de gay mat on dinh protocol va tang tai CPU/memory.

**Bang chung code**

- Handshake handler mo cho packet khong ma hoa, quyen NONE:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:38`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:39`
- Switch stage xu ly truc tiep `CLIENT_HELLO/CLIENT_FINISH`, khong check `HandshakeEstablished`:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:47`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:51`
- Sau `CLIENT_FINISH`, state crypto cua connection bi overwrite:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:151`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:152`
- Moi lan finish thanh cong lai tao/store session moi:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:157`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:160`

**Tac dong**

- Cho phep re-handshake ngoai ky vong state machine, gay protocol confusion va race voi luong du lieu dang chay.
- Co the bi khai thac de tao session churn (token churn + CPU cho X25519) va suy giam hieu nang/DoS.

**Tham chieu CWE**

- CWE-841: Improper Enforcement of Behavioral Workflow
- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-400: Uncontrolled Resource Consumption

## 32. Cap Nhat Bo Sung (Timeout Middleware Reliability) - 2026-04-15

### BUG-50 (Medium): `TimeoutMiddleware` gui timeout response bang chinh token da bi cancel, dan den mat response/exception chain

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Khi handler vuot qua `CancelAfter(timeout)`, `ExecuteHandlerAsync(...)` bat `OperationCanceledException` voi dieu kien timeout noi bo. Tuy nhien, trong nhanh xu ly timeout no lai goi `SendAsync(..., token)` voi chinh cancellation token da bi cancel.

He qua: thao tac gui frame timeout co kha nang bi huy ngay lap tuc (hoac nem tiep `OperationCanceledException`), khien client khong nhan duoc phan hoi `TIMEOUT` nhu ky vong, dong thoi tao exception flow khong can thiet trong middleware.

**Bang chung code**

- Tao timeout token va cancel theo deadline:
  - `src/Nalix.Network.Pipeline/Inbound/TimeoutMiddleware.cs:44`
- Bat timeout tu token noi bo:
  - `src/Nalix.Network.Pipeline/Inbound/TimeoutMiddleware.cs:59`
- Gui response bang chinh token da cancel:
  - `src/Nalix.Network.Pipeline/Inbound/TimeoutMiddleware.cs:75`

**Tac dong**

- Mat phan hoi timeout ve phia client trong kich ban qua han.
- Co the gay hanh vi khong nhat quan (co timeout nhung khong co frame thong bao), lam kho retry/backoff logic va debug van hanh.

**Tham chieu CWE**

- CWE-754: Improper Check for Unusual or Exceptional Conditions
- CWE-703: Improper Check or Handling of Exceptional Conditions

## 33. Cap Nhat Bo Sung (TimeSynchronizer Dispose Race) - 2026-04-15

### BUG-51 (High): `TimeSynchronizer.Dispose()` dispose `_stoppedSignal` truoc khi worker loop ket thuc hoan toan (race/use-after-dispose)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`Dispose()` goi `Deactivate()` roi dispose ngay `_stoppedSignal`. Tuy nhien, `Deactivate()`/`TERMINATE_SYNC_LOOP()` chi cancel CTS, khong cho worker stop xac nhan. Trong worker `finally`, code luon goi `_stoppedSignal.Set()`.

Neu worker chua thoat xong ma `_stoppedSignal` da bi dispose, `Set()` se cham object da dispose (race use-after-dispose), co the phat sinh `ObjectDisposedException` trong background loop.

**Bang chung code**

- `Dispose()` goi `Deactivate()` va dispose signal ngay:
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:194`
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:197`
- `Deactivate()` chi cancel loop qua `TERMINATE_SYNC_LOOP()`:
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:155`
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:162`
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:327`
- Worker finally van goi `_stoppedSignal.Set()`:
  - `src/Nalix.Network.Pipeline/Timekeeping/TimeSynchronizer.cs:312`

**Tac dong**

- Race lifecycle trong shutdown, co the gay exception nen va mat on dinh tien trinh service.
- Lam giam do tin cay cua co che time-sync khi restart/stop nhanh.

**Tham chieu CWE**

- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-416: Use After Free
- CWE-664: Improper Control of a Resource Through its Lifetime

## 34. Cap Nhat Bo Sung (Async Backpressure Rollback) - 2026-04-15

### BUG-52 (High): `SocketConnection` khong rollback pending/lease khi `AsyncCallback.Invoke(...)` bi drop, dan den self-DoS + leak

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Receive loop tang `_pendingProcessCallbacks` truoc khi handoff vao `AsyncCallback`. Co co che release pending thong qua `releasePendingPacketOnCompletion=true`, nhung viec release nay chi xay ra khi callback thuc su duoc queue va chay den `EXECUTE_AND_RETURN`.

Van de: `AsyncCallback.Invoke(...)` co the `return false` trong cac truong hop backpressure (`global_backpressure`, `per_ip_backpressure`) hoac queue fail. Tai call-site `SocketConnection`, ket qua `false` khong duoc rollback day du. O nhanh non-fragmented, co bien `queued` nhung khong xu ly false. O nhanh fragment assembled, ket qua `Invoke` bi bo qua hoan toan sau khi da `assembledLease.Retain()` + `args.Initialize(...)`.

He qua la `_pendingProcessCallbacks` bi giu tang dai han (self-throttle), dong thoi object/lease co the khong duoc giai phong dung lifecycle khi handoff that bai.

**Bang chung code**

- Tang pending truoc handoff:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:434`
- Nhanh non-fragmented: co `queued` nhung khong rollback khi false:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:542`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:543`
- Nhanh fragment assembled: retain + init + invoke, khong xu ly ket qua queue:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:508`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:509`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:510`
- `AsyncCallback.Invoke` co the tra `false` tren backpressure/queue failure:
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:175`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:190`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:284`
- Release pending chi xay ra trong `EXECUTE_AND_RETURN` khi callback da duoc chay:
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:383`

**Tac dong**

- Ket noi bi "tu khoa" (pending counter khong giam), packet moi bi drop lien tuc -> self-DoS tren tung connection.
- Co nguy co ro ri tai nguyen (lease/args) o nhanh handoff that bai, lam tang memory pressure duoi tai cao.

**Tham chieu CWE**

- CWE-772: Missing Release of Resource after Effective Lifetime
- CWE-400: Uncontrolled Resource Consumption
- CWE-667: Improper Locking (state counter/lifecycle synchronization issue)

## 35. Cap Nhat Bo Sung (DispatchChannel Event Lifecycle) - 2026-04-15

### BUG-53 (Medium): `DispatchChannel` subscribe `ConnectionUnregistered` nhung khong unsubscribe (event handler leak + stale callback)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`DispatchChannel<TPacket>` dang ky callback `ConnectionUnregistered += OnUnregistered` tu `IConnectionHub` ngay trong constructor. Tuy nhien class khong co duong cleanup tuong ung (`-=`), va khong co `Dispose` de huy dang ky.

Neu runtime tao lai dispatcher/channel qua cac chu ky activate/reconfigure/restart, cac instance cu van bi giữ tham chieu boi event publisher (`ConnectionHub`). He qua la memory leak theo thoi gian, dong thoi callback cua instance stale van co the bi goi tren event unregistration moi, tao xu ly thua va race lifecycle.

**Bang chung code**

- Dang ky event trong constructor:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:179`
- Khong thay unsubscribe (`ConnectionUnregistered -= ...`) trong module routing:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs`
- `DispatchChannel` khong trien khai `IDisposable` de thu hoi hook event:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:43`

**Tac dong**

- Tich luy object khong duoc giai phong (memory retention) sau nhieu lan re-init runtime.
- Event fan-out vao handler stale, tang overhead va co the gay hanh vi kho doan trong giai doan shutdown/restart.

**Tham chieu CWE**

- CWE-772: Missing Release of Resource after Effective Lifetime
- CWE-664: Improper Control of a Resource Through its Lifetime

## 36. Cap Nhat Bo Sung (Error Counter Enforcement Gap) - 2026-04-15

### SEC-54 (Medium): `connection.ErrorCount` chi duoc tang de telemetry, khong co enforcement de cat ket noi loi lap lai (malformed-frame DoS)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Duong dispatch tang `connection.IncrementErrorCount()` o nhieu nhanh loi parse/pipeline/handler. Tuy nhien, trong toan bo source hien tai khong co noi nao su dung `ErrorCount` de trigger policy phong ve (vd: disconnect, temporary ban, cooldown, hoac downgrade processing).

He qua: mot peer co the lien tuc gui frame loi/malformed de tao exception path va tiep tuc ton tai ket noi, tieu ton CPU/log/resources ma khong bi cat theo nguong loi. `ErrorCount` tro thanh chi so quan sat thuan tuy thay vi co che bao ve thuc thi.

**Bang chung code**

- Interface + implementation chi expose count/increment:
  - `src/Nalix.Common/Networking/IConnectionErrorTracked.cs:22`
  - `src/Nalix.Network/Connections/Connection.cs:100`
  - `src/Nalix.Network/Connections/Connection.cs:144`
- Dispatch tang loi nhieu diem:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:421`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:481`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:540`
- Khong co tham chieu enforcement nao khac toi `ErrorCount` trong source runtime/network:
  - `src/Nalix.Network/Connections/Connection.cs`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs`

**Tac dong**

- Cho phep attacker duy tri ket noi "noisy" gay tai exception/parse-fail lap lai ma khong bi loai bo tu dong.
- Tang be mat DoS logic (CPU + logging pressure) tren tung connection va toan node duoi tai xau.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-307: Improper Restriction of Excessive Authentication Attempts (mo rong cho excessive protocol failures)
- CWE-770: Allocation of Resources Without Limits or Throttling

## 37. Cap Nhat Bo Sung (SDK TcpSession Dispose Semantics) - 2026-04-15

### BUG-55 (High): `TcpSession.Dispose()` set `_disposed` truoc roi moi goi `DisconnectAsync()`, lam bo qua logic dong ket noi

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`TcpSession.Dispose()` dat `_disposed = 1` truoc, sau do goi `DisconnectAsync()`. Tuy nhien `DisconnectAsync()` co guard dau vao `if (Volatile.Read(ref _disposed) == 1) return;`, nen ngay lap tuc thoat va khong chay `DisconnectInternalAsync()`.

Ket qua la luong dong ket noi (cancel loop CTS, shutdown/close socket, complete async queue) bi bo qua trong Dispose path. Day la loi lifecycle nghiem trong: dispose khong thuc su teardown session theo design mong doi.

**Bang chung code**

- Guard trong `DisconnectAsync` bo qua neu da disposed:
  - `src/Nalix.SDK/Transport/TcpSession.cs:140`
- `Dispose()` dat flag disposed truoc khi goi disconnect:
  - `src/Nalix.SDK/Transport/TcpSession.cs:223`
  - `src/Nalix.SDK/Transport/TcpSession.cs:228`
- Logic teardown thuc su nam o `DisconnectInternalAsync()`:
  - `src/Nalix.SDK/Transport/TcpSession.cs:156`

**Tac dong**

- Session dispose co the de lai ket noi/socket/loop trong trang thai khong duoc teardown day du.
- Gay leak tai nguyen va hanh vi shutdown khong nhat quan trong ung dung SDK (dac biet khi tao/huy session lap lai).

**Tham chieu CWE**

- CWE-404: Improper Resource Shutdown or Release
- CWE-664: Improper Control of a Resource Through its Lifetime

## 38. Cap Nhat Bo Sung (SDK Async Handler Backpressure) - 2026-04-15

### SEC-56 (Medium): Hang doi async handler cua `TcpSession` la unbounded, co the bi flood gay memory DoS

**Trang thai**

- ✅ FIXED (2026-04-15)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`TcpSession` khoi tao `_asyncQueue` bang `Channel.CreateUnbounded<Func<Task>>`. Moi frame den co the enqueue delegate xu ly async (`writer.TryWrite(async () => ...)`) ma khong co gioi han dung luong/backpressure.

Trong kich ban server/peer gui toc do cao hon toc do `OnMessageAsync` xu ly, queue se phinh vo han trong bo nho client SDK. Truong hop dong thoi co ca `OnMessageReceived` va `OnMessageAsync`, code con clone payload (`lease.Memory.ToArray()`) cho moi frame, lam tang toc do tieu ton bo nho.

**Bang chung code**

- Tao queue unbounded:
  - `src/Nalix.SDK/Transport/TcpSession.cs:119`
- Enqueue cong viec async moi frame:
  - `src/Nalix.SDK/Transport/TcpSession.cs:269`
  - `src/Nalix.SDK/Transport/TcpSession.cs:278`
- Clone payload moi message khi co ca sync+async handler:
  - `src/Nalix.SDK/Transport/TcpSession.cs:260`

**Tac dong**

- Memory growth khong gioi han tren SDK client khi gap luong frame den nhanh/handler cham.
- Co the dan den suy giam hieu nang nghiem trong hoac OOM (client-side DoS).

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling

## 39. Cap Nhat Bo Sung (FrameSender Failure Propagation) - 2026-04-15

### BUG-57 (High): `FrameSender` co the de treo `SendAsync` vo han khi drain loop fault (TCS khong duoc complete)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`FrameSender.SendAsync(...)` enqueue frame kem `TaskCompletionSource<bool>` roi `await tcs.Task`. Trong `DRAIN_LOOP_ASYNC`, neu xay ra exception ngoai `OperationCanceledException`, code chi goi `_sendQueue.Writer.TryComplete(ex)` va thoat loop.

Van de: cac item da nam san trong queue (hoac vua enqueue truoc khi fault) co `tcs` rieng nhung khong duoc complete/fail trong nhanh loi nay. Vi consumer loop da dung, cac caller dang `await tcs.Task` co the treo vo han.

**Bang chung code**

- `SendAsync` await truc tiep `tcs.Task`:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:87`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:213`
- Drain loop fault chi complete writer, khong drain/fail TCS cua item ton dong:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:109`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:111`
- Chi `Dispose()` moi co drain + fail TCS pendings:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:238`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:241`

**Tac dong**

- Request/send path co the bi treo vo han sau mot loi sender worker, gay stall toan bo client flow.
- Co the bi khai thac thanh DoS logic o phia client (hang send queue va request timeout phu thuoc caller).

**Tham chieu CWE**

- CWE-833: Deadlock
- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-400: Uncontrolled Resource Consumption

## 40. Cap Nhat Bo Sung (SDK UdpSession Dispose Semantics) - 2026-04-15

### BUG-58 (High): `UdpSession.Dispose()` cung bi short-circuit disconnect do set `_disposed` truoc

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`UdpSession.Dispose()` dat `_disposed = 1` roi moi goi `DisconnectAsync()`. Trong khi do `DisconnectAsync()` kiem tra ngay dau vao: neu `_disposed == 1` thi return `Task.CompletedTask`.

Ket qua: Dispose path khong thuc thi teardown thuc su (`Cancel` loop CTS, dispose socket, invoke disconnect event) nhu mong doi.

**Bang chung code**

- Guard trong `DisconnectAsync`:
  - `src/Nalix.SDK/Transport/UdpSession.cs:144`
- `Dispose()` set flag disposed truoc khi goi disconnect:
  - `src/Nalix.SDK/Transport/UdpSession.cs:405`
  - `src/Nalix.SDK/Transport/UdpSession.cs:410`
- Logic teardown nam trong `DisconnectAsync` va bi bo qua:
  - `src/Nalix.SDK/Transport/UdpSession.cs:150`
  - `src/Nalix.SDK/Transport/UdpSession.cs:159`

**Tac dong**

- Co nguy co de lai socket/loop dang chay sau dispose call.
- Gay leak tai nguyen va lifecycle khong nhat quan khi ung dung tao/huy UDP session nhieu lan.

**Tham chieu CWE**

- CWE-404: Improper Resource Shutdown or Release
- CWE-664: Improper Control of a Resource Through its Lifetime

## 41. Cap Nhat Bo Sung (SDK UDP Async Handler Backpressure) - 2026-04-15

### SEC-59 (Medium): `UdpSession.ReceiveLoopAsync` spawn `Task.Run` moi datagram khong gioi han, de bi flood DoS

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Trong receive loop UDP, moi datagram co async subscriber se tao `Task.Run(...)` truc tiep. Khong co queue bound, semaphore, hay co che cap toc do. Under flood, so luong task pending co the tang rat nhanh.

Ngoai ra, khi co dong thoi ca sync + async handler, code clone payload qua `ToArray()` cho tung datagram, tiep tuc lam tang pressure bo nho.

**Bang chung code**

- Spawn task moi goi khi co async handler + sync handler:
  - `src/Nalix.SDK/Transport/UdpSession.cs:347`
  - `src/Nalix.SDK/Transport/UdpSession.cs:350`
- Spawn task moi goi cho nhanh async-only:
  - `src/Nalix.SDK/Transport/UdpSession.cs:361`
- Copy payload moi datagram:
  - `src/Nalix.SDK/Transport/UdpSession.cs:347`

**Tac dong**

- Co the gay memory/ThreadPool pressure nghiem trong khi peer flood datagram.
- Client SDK de bi DoS (latency spike, starvation, OOM) khi handler async cham hon toc do inbound.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling

## 42. Cap Nhat Bo Sung (SDK Resume Correlation) - 2026-04-15

### SEC-60 (High): `ResumeSessionAsync` match response qua lo (chi check `Stage==RESPONSE`), de nhan nham packet

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`ResumeSessionAsync` dung `PacketAwaiter.AwaitAsync<SessionResume>` nhung predicate chi kiem tra `packet.Stage == RESPONSE`. Khong co rang buoc them nhu `SessionToken`, sequence/correlation id, hay challenge tu request.

Neu tren cung session co nhieu luong resume/request chay gan nhau, hoac kenh bi noise packet cung type, operation co the "bat nham" response khong thuoc request hien tai. Day la correlation bug nghiem trong o protocol flow.

**Bang chung code**

- Tao request resume voi token hien tai:
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:42`
- Await response voi predicate qua rong:
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:56`
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:58`
- Gui request khong co truong sequence rieng cho resume:
  - `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs:60`

**Tac dong**

- Resume flow co the ra quyet dinh dua tren packet RESPONSE khong dung giao dich.
- Dan den sai lech state (`SessionToken`, `EncryptionEnabled`) va loi logic kho truy vet duoi tai thuc.

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-444: Inconsistent Interpretation of Input
- CWE-841: Improper Enforcement of Behavioral Workflow

## 43. Cap Nhat Bo Sung (SDK Awaiter Error Semantics) - 2026-04-15

### BUG-61 (Medium): Exception trong `predicate` cua `PacketAwaiter` bi nuot, request chi timeout thay vi fail-fast

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`PacketAwaiter` rely vao `SubscribeTemp<T>()` de nhan packet va evaluate `predicate(packet)`. Tuy nhien luong `On<TPacket>` ben `TcpSessionSubscriptions` bao `try/catch(Exception)` va chi log `Trace`, khong rethrow/forward exception.

Do do, neu `predicate` (hoac deserialize trong wrapper) nem exception, `tcs` trong `PacketAwaiter` khong duoc complete exception; operation tiep tuc cho den timeout. Ket qua la sai semantics: loi logic ben predicate bi an thanh `TimeoutException`, lam kho debug va co the gay retry sai.

**Bang chung code**

- `PacketAwaiter` evaluate predicate trong callback subscription:
  - `src/Nalix.SDK/Transport/PacketAwaiter.cs:59`
  - `src/Nalix.SDK/Transport/PacketAwaiter.cs:62`
- `On<TPacket>` nuot moi exception, chi TraceError:
  - `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs:48`
  - `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs:63`
  - `src/Nalix.SDK/Transport/Extensions/TcpSessionSubscriptions.cs:65`
- Awaiter ket thuc bang timeout neu tcs khong duoc complete:
  - `src/Nalix.SDK/Transport/PacketAwaiter.cs:107`

**Tac dong**

- Mat fail-fast behavior; bug predicate/deserialization bi nguy trang thanh timeout.
- Gay retry khong can thiet, tang tai va lam sai huong dieu tra su co.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-391: Unchecked Error Condition

## 44. Cap Nhat Bo Sung (SDK Connect Timeout Enforcement) - 2026-04-15

### BUG-62 (Medium): `TransportOptions.ConnectTimeoutMillis` khong duoc enforce trong `ConnectAsync` (TCP/UDP)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

SDK expose `ConnectTimeoutMillis` trong `TransportOptions` (mac dinh 5000ms), nhung `TcpSession.ConnectAsync` va `UdpSession.ConnectAsync` khong tao timeout token dua tren option nay. Ca hai ham chi truyen `ct` do caller cung cap vao `ConnectAsync` socket call.

He qua: neu caller khong tu tao `CancellationToken` co timeout, connect co the doi lau khong dung voi config da dat. Option timeout tro thanh "dead config" (khong co hieu luc thuc thi).

**Bang chung code**

- Option duoc khai bao:
  - `src/Nalix.SDK/Options/TransportOptions.cs:42`
- TCP connect chi dung `ct` truyen vao, khong ap timeout tu option:
  - `src/Nalix.SDK/Transport/TcpSession.cs:94`
  - `src/Nalix.SDK/Transport/TcpSession.cs:112`
- UDP connect chi dung `ct` truyen vao, khong ap timeout tu option:
  - `src/Nalix.SDK/Transport/UdpSession.cs:99`
  - `src/Nalix.SDK/Transport/UdpSession.cs:127`

**Tac dong**

- Hanh vi ket noi khong nhat quan voi cau hinh runtime, de gay treo luong connect trong tinh huong mang xau.
- Lam giam kha nang du doan timeout va anh huong do tin cay cua luong reconnect/client startup.

**Tham chieu CWE**

- CWE-693: Protection Mechanism Failure
- CWE-755: Improper Handling of Exceptional Conditions

## 45. Cap Nhat Bo Sung (SDK Async Queue Backpressure Semantics) - 2026-04-15

### BUG-63 (Medium): `TcpSession` co the drop `OnMessageAsync` am tham khi queue day do dung `TryWrite` tren queue `FullMode=Wait`

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`TcpSession` khoi tao async queue dang bounded voi `FullMode=Wait`, ngu y backpressure. Tuy nhien trong `HandleReceiveMessage`, enqueue lai dung `writer.TryWrite(...)` (non-blocking). O nhanh co ca sync+async handler (`asyncPayload` copy), ket qua `TryWrite` bi bo qua hoan toan.

Khi queue day, `TryWrite` tra `false` nhung code van `return`, khong bao loi, khong retry, khong fallback. He qua la callback async bi roi mat im lang (silent data loss) duoi tai cao.

**Bang chung code**

- Queue bounded va khai bao `FullMode = Wait`:
  - `src/Nalix.SDK/Transport/TcpSession.cs:119`
  - `src/Nalix.SDK/Transport/TcpSession.cs:124`
- Nhanh copy payload dung `TryWrite` nhung bo qua ket qua:
  - `src/Nalix.SDK/Transport/TcpSession.cs:272`
  - `src/Nalix.SDK/Transport/TcpSession.cs:274`
  - `src/Nalix.SDK/Transport/TcpSession.cs:279`
- Nhanh khac co check `TryWrite` (khong dong nhat semantics):
  - `src/Nalix.SDK/Transport/TcpSession.cs:283`
  - `src/Nalix.SDK/Transport/TcpSession.cs:290`

**Tac dong**

- Mat event `OnMessageAsync` am tham trong burst traffic (khong exception, khong telemetry ro rang).
- Gay hanh vi khong nhat quan giua sync handler va async handler, dan den logic bug kho tai hien.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-754: Improper Check for Unusual or Exceptional Conditions

## 46. Cap Nhat Bo Sung (SDK UDP Send Error Semantics) - 2026-04-15

### BUG-64 (Medium): `UdpSession.SendAsyncInternal` fail-open khi `_socket == null` (drop send am tham)

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`UdpSession.SendAsyncInternal(...)` neu gap `_socket == null` se `return` im lang thay vi nem exception. Cac call-site `SendAsync(...)` ben tren xem nhu operation thanh cong vi khong nhan loi.

Trong race disconnect/send, du lieu co the bi mat am tham (khong gui, khong bao that bai), dan den sai lech nghiem trong o logic request-response va retry policy phia caller.

**Bang chung code**

- `SendAsyncInternal` fail-open khi socket null:
  - `src/Nalix.SDK/Transport/UdpSession.cs:274`
  - `src/Nalix.SDK/Transport/UdpSession.cs:276`
  - `src/Nalix.SDK/Transport/UdpSession.cs:278`
- Cac send public path phu thuoc `SendAsyncInternal` de xac nhan gui:
  - `src/Nalix.SDK/Transport/UdpSession.cs:186`
  - `src/Nalix.SDK/Transport/UdpSession.cs:233`

**Tac dong**

- Mat goi tin am tham, caller khong biet de retry/bao loi kip thoi.
- Tang rui ro timeout day chuyen o tang nghiep vu do "send-success illusion".

**Tham chieu CWE**

- CWE-391: Unchecked Error Condition
- CWE-703: Improper Check or Handling of Exceptional Conditions

## 47. Cap Nhat Bo Sung (SDK UDP Async Dispatcher Init Race) - 2026-04-15

### BUG-65 (High): `UdpSession.ConnectAsync` khoi dong async queue worker truoc khi tao `_loopCts`, co the nem `NullReferenceException` va vo hieu hoa async handler

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Trong `UdpSession.ConnectAsync`, code tao `_asyncQueue` va `Task.Run(() => this.ProcessAsyncQueueAsync(_loopCts.Token))` truoc, nhung `_loopCts` chi duoc khoi tao o doan sau. Co cua so race ro rang: lambda co the chay ngay khi `_loopCts` van `null`, dan den `NullReferenceException`.

Khi worker bi fault som, queue khong con consumer. Cac callback async (`OnMessageAsync`) phia receive loop sau do de bi drop/im lang khi queue day hoac `TryWrite` that bai.

**Bang chung code**

- Start worker bang `_loopCts.Token` truoc khi `_loopCts` duoc tao:
  - `src/Nalix.SDK/Transport/UdpSession.cs:143`
- `_loopCts` chi duoc khoi tao sau do:
  - `src/Nalix.SDK/Transport/UdpSession.cs:151`
- Receive loop phu thuoc queue writer de day async callback:
  - `src/Nalix.SDK/Transport/UdpSession.cs:372`
  - `src/Nalix.SDK/Transport/UdpSession.cs:377`
  - `src/Nalix.SDK/Transport/UdpSession.cs:386`

**Tac dong**

- Async pipeline co the vo hieu hoa ngay tu luc connect (khong on dinh, kho tai hien).
- Dan den mat callback async, timeout day chuyen, va hanh vi khong nhat quan duoi tai.

**Tham chieu CWE**

- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization ('Race Condition')
- CWE-476: NULL Pointer Dereference
- CWE-703: Improper Check or Handling of Exceptional Conditions

## 48. Cap Nhat Bo Sung (SDK Time Sync Trust Model) - 2026-04-15

### SEC-66 (Medium): `SyncTimeAsync` dong bo clock local truc tiep theo `res.Timestamp` khong co gioi han sanity/skew

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`TimeSyncExtensions.SyncTimeAsync` su dung timestamp server (`res.Timestamp`) de goi `Clock.SynchronizeUnixMilliseconds(...)` khi `TimeSyncEnabled=true`, nhung khong co rang buoc do lech toi da, khong co canh bao monotonic rollback/forward jump, va khong co xac thuc bo sung cho nguon thoi gian ngoai response predicate theo seq.

Trong tinh huong peer bi compromise/mitm/noise packet hop le kieu CONTROL response, client co the bi skew dong ho lon bat thuong (clock poisoning), anh huong den logic timeout/token/window o tang tren.

**Bang chung code**

- Nhan response TIMESYNCRESPONSE theo seq:
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:42`
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:45`
- Ap timestamp server truc tiep vao dong bo clock:
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:55`
  - `src/Nalix.SDK/Transport/Extensions/TimeSyncExtensions.cs:58`

**Tac dong**

- Co the gay lech clock local lon, dan den sai timeout/TTL/session validation o lop nghiep vu.
- Tang rui ro tan cong logic dua tren time-window neu nguon thoi gian khong duoc rang buoc chat.

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-20: Improper Input Validation
- CWE-840: Business Logic Errors

## 49. Cap Nhat Bo Sung (SDK UDP Receive Loop Fault Handling) - 2026-04-15

### BUG-67 (High): Exception trong `UdpSession.ReceiveLoopAsync` (transform/handler) co the lam receive loop dung han ma khong qua `OnError`/disconnect flow

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `UdpSession.ReceiveLoopAsync`, code chi `catch` quanh `ReceiveAsync(...)`. Sau khi da nhan datagram, khoi `try/finally` tiep theo (transform + invoke handlers) khong co `catch`. Neu `TransformInbound(...)` hoac `syncHandler` nem exception, loi se bubble ra khoi method, task receive loop bi fault va dung han.

Do receive loop duoc start fire-and-forget, su co nay de tro thanh loop death am tham: session van ton tai nhung khong con xu ly inbound packet.

**Bang chung code**

- `catch` chi bao quanh `ReceiveAsync`:
  - `src/Nalix.SDK/Transport/UdpSession.cs:334`
  - `src/Nalix.SDK/Transport/UdpSession.cs:343`
- Khoi xu ly datagram khong co `catch` cho transform/handler:
  - `src/Nalix.SDK/Transport/UdpSession.cs:364`
  - `src/Nalix.SDK/Transport/UdpSession.cs:367`
  - `src/Nalix.SDK/Transport/UdpSession.cs:400`
- Receive loop duoc start fire-and-forget:
  - `src/Nalix.SDK/Transport/UdpSession.cs:152`

**Tac dong**

- Peer co the kich hoat malformed frame de lam receive loop dung han (DoS logic/reliability).
- Session roi vao trang thai "connected nhung khong receive", gay timeout day chuyen kho chan doan.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-400: Uncontrolled Resource Consumption
- CWE-754: Improper Check for Unusual or Exceptional Conditions

## 50. Cap Nhat Bo Sung (SDK UDP Async Queue Semantics) - 2026-04-15

### BUG-68 (Medium): `UdpSession.ReceiveLoopAsync` bo qua ket qua `TryWrite` o nhanh `sync+async`, gay drop `OnMessageAsync` am tham khi queue day

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Khi co dong thoi `OnMessageReceived` va `OnMessageAsync`, code UDP tao ban copy payload roi `writer.TryWrite(...)` de queue async callback. Tuy nhien ket qua `TryWrite` khong duoc kiem tra. Neu queue day (bounded 1024), callback async bi bo mat im lang va khong co telemetry/error.

Dieu nay tao behavior khong tin cay duoi tai cao: sync handler van nhan packet, async handler mat packet.

**Bang chung code**

- Queue bounded trong connect:
  - `src/Nalix.SDK/Transport/UdpSession.cs:135`
  - `src/Nalix.SDK/Transport/UdpSession.cs:140`
- Nhanh `sync+async` dung `TryWrite` nhung bo qua ket qua:
  - `src/Nalix.SDK/Transport/UdpSession.cs:373`
  - `src/Nalix.SDK/Transport/UdpSession.cs:377`
- Nhanh async-only co check `TryWrite` (semantics khong dong nhat):
  - `src/Nalix.SDK/Transport/UdpSession.cs:383`
  - `src/Nalix.SDK/Transport/UdpSession.cs:386`

**Tac dong**

- Mat callback async am tham trong burst traffic.
- Tao sai lech state/logic giua hai kenh xu ly (sync vs async), de dan den bug nghiep vu kho tai hien.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-754: Improper Check for Unusual or Exceptional Conditions

## 51. Cap Nhat Bo Sung (SDK Handshake Response Correlation) - 2026-04-15

### SEC-69 (Medium): `HandshakeAsync` doi response theo `Stage` qua rong, de cross-match nham khi co nhieu luong handshake/request cung session

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `HandshakeAsync`, ca 2 lan cho response (`SERVER_HELLO` va `SERVER_FINISH`) deu dung predicate dua tren `Stage` (hoac `ERROR`) ma khong co correlation id rieng cho tung giao dich handshake. 

Khi co nhieu operation song song cung session (hoac kenh nhieu noise packet cung type), awaiter co the bat nham goi handshake khong thuoc luong hien tai. Mac du proof check co the chan mot phan, ket qua van de gay fail/do timeout ngau nhien, tao DoS logic o giai doan bat tay.

**Bang chung code**

- Predicate cho `SERVER_HELLO` chi check stage:
  - `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs:59`
- Predicate cho `SERVER_FINISH` chi check stage:
  - `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs:98`
- `RequestAsync` duoc goi voi predicate tren, khong co sequence/correlation field rieng:
  - `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs:56`
  - `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs:95`

**Tac dong**

- Tang kha nang fail handshake ngau nhien duoi tai/canh tranh (cross-talk giua request song song).
- Co the bi loi dung de gay timeout/failure o luong bat tay (DoS muc logic).

**Tham chieu CWE**

- CWE-345: Insufficient Verification of Data Authenticity
- CWE-444: Inconsistent Interpretation of Input
- CWE-841: Improper Enforcement of Behavioral Workflow

## 52. Cap Nhat Bo Sung (SDK-Server UDP Contract Mismatch) - 2026-04-15

### SEC-70 (High): Server UDP listener parse header/replay tren payload tho truoc transform, khong tuong thich voi SDK UDP `TransformOutbound` (encrypt/compress)

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

SDK UDP phia client serialise packet -> `TransformOutbound` (compress/encrypt tuy option) -> moi prepend `SessionToken` va gui. Tuy nhien phia server (`UdpListenerBase.ProcessDatagram`) lai doc `Transport` byte va `SequenceId` truc tiep tren payload sau token truoc khi bat ky buoc transform/decrypt nao.

Neu payload da encrypt/compress, offset header khong con la plain frame header hop le; cac check protocol/replay se sai va datagram bi drop. Day la mismatch giao thuc nghiem trong giua SDK va server trong luong UDP send/receive.

**Bang chung code**

- SDK UDP transform outbound truoc khi dong goi token:
  - `src/Nalix.SDK/Transport/UdpSession.cs:235`
  - `src/Nalix.SDK/Transport/UdpSession.cs:275`
  - `src/Nalix.SDK/Transport/UdpSession.cs:245`
- Server UDP kiem tra `ProtocolType.UDP` tren payload thang sau token:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:143`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:144`
- Server UDP doc `SequenceId` tren payload thang (offset 9) de replay check:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:271`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:272`

**Tac dong**

- UDP packet tu SDK co the bi server drop sai (false unauth/replay/protocol mismatch), dac biet khi `EncryptionEnabled/CompressionEnabled` dang bat.
- Gay mat dong bo chuc nang UDP giua client-server, de thanh DoS logic (tat ca UDP hop le van that bai).

**Tham chieu CWE**

- CWE-440: Expected Behavior Violation
- CWE-444: Inconsistent Interpretation of Input
- CWE-841: Improper Enforcement of Behavioral Workflow

## 53. Cap Nhat Bo Sung (Server UDP Fast-Path Replay Guard) - 2026-04-15

### SEC-71 (High): `UdpListenerBase` bo qua replay-window check o fast-path endpoint cache, tao loi hong replay bypass

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `ProcessDatagram`, nhanh fast-path (`_endpointCache.TryGetValue`) chi kiem tra token + `IsAuthenticated(...)` roi day vao `_protocol.ProcessFrame(...)`. Replay protection (`UdpReplayWindow.TryCheck(sequenceId)`) chi nam o slow-path ben duoi.

He qua: sau khi endpoint da duoc cache, cac datagram replay (cung seq) co the bo qua guard replay neu dat dung fast-path. Day la inconsistency nghiem trong trong co che anti-replay.

**Bang chung code**

- Fast-path cache endpoint, khong co `TryCheck(sequenceId)`:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:161`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:182`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:201`
- Replay check chi xuat hien o slow-path:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:271`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:272`

**Tac dong**

- Replay packet co the lot qua sau khi endpoint da warm cache.
- Lam suy yeu co che chong replay UDP, dan den duplicate action/state divergence.

**Tham chieu CWE**

- CWE-294: Authentication Bypass by Capture-replay
- CWE-841: Improper Enforcement of Behavioral Workflow
- CWE-345: Insufficient Verification of Data Authenticity

## 54. Cap Nhat Bo Sung (Server UDP Length Validation) - 2026-04-15

### SEC-72 (Medium): Doc `SequenceId` tu payload khong du guard do dai, co the bi spam datagram ngan de kich hoat exception loop (DoS)

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Server UDP chi check payload den offset `Transport` (index 8) de xac nhan protocol, nhung sau do doc `BinaryPrimitives.ReadUInt32LittleEndian(payload[9..])` de lay sequence id ma khong check payload co du 13 bytes.

Voi datagram ngan (payload 9..12 bytes), thao tac slice/read se nem exception. Exception bi bat o receive loop va lap lai lien tuc duoi flood, tao error-loop + logging pressure.

**Bang chung code**

- Guard hien tai chi den transport offset:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:143`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:144`
- Doc sequence id khong guard them:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:271`
- Vong receive bat exception va tiep tuc (co delay 50ms) -> co the bi flood DoS:
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:87`
  - `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs:97`

**Tac dong**

- Tang mat do exception/log duoi traffic xau, giam thong luong xu ly packet hop le.
- Co the bi loi dung de gay DoS muc ung dung.

**Tham chieu CWE**

- CWE-20: Improper Input Validation
- CWE-400: Uncontrolled Resource Consumption
- CWE-703: Improper Check or Handling of Exceptional Conditions

## 55. Cap Nhat Bo Sung (Server UDP Replay Window Config Semantics) - 2026-04-15

### BUG-73 (Low): `NetworkSocketOptions.UdpReplayWindowSize` duoc khai bao nhung khong duoc su dung, anti-replay window bi hardcode 1024 bits

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

`NetworkSocketOptions` co option `UdpReplayWindowSize`, nhung `SlidingWindow` su dung hang so `WindowSize = 1024` va `Connection` tao `new SlidingWindow()` khong truyen cau hinh. Nghia la thay doi config khong co tac dung thuc te.

Day la dead-config bug lam han che tuning khi mang co reorder cao/latency xau, dong thoi tao nham lan cho operator ve muc bao ve replay.

**Bang chung code**

- Option cau hinh duoc expose:
  - `src/Nalix.Network/Options/NetworkSocketOptions.cs:153`
  - `src/Nalix.Network/Options/NetworkSocketOptions.cs:154`
- `Connection` khoi tao replay window khong nhan config:
  - `src/Nalix.Network/Connections/Connection.cs:167`
- `SlidingWindow` hardcode window 1024:
  - `src/Nalix.Common/Security/SlidingWindow.cs:16`

**Tac dong**

- Cau hinh replay-window khong hieu luc, kho toi uu he thong theo moi truong that.
- Gay sai ky vong van hanh va kho chan doan khi packet out-of-order tang.

**Tham chieu CWE**

- CWE-16: Configuration
- CWE-693: Protection Mechanism Failure

## 56. Cap Nhat Bo Sung (Server Control Pre-Auth Crypto Policy) - 2026-04-15

### SEC-74 (High): `CIPHER_UPDATE` duoc cho phep plaintext + permission NONE, co the bi goi truoc handshake de tampering crypto policy

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`SystemControlHandlers.HandleAsync` cho phep packet he thong o muc `PermissionLevel.NONE` va `PacketEncryption(false)`. Trong `HandleCipherUpdate`, neu connection chua co secret (`Secret.Length == 0`) thi server chap nhan doi `connection.Algorithm` theo packet tu client.

Dieu nay cho phep peer chua xac thuc thay doi crypto policy pre-auth. Du handshake sau do co the dat lai thuật toan, khoang thoi gian truoc/song song van bi tampering state va de gay desync/DoS logic.

**Bang chung code**

- Endpoint control cho phep plaintext + NONE permission:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:28`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:29`
- `CIPHER_UPDATE` duoc xu ly tuong ung:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:44`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:45`
- Dieu kien chan chi ap dung khi da co secret:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:92`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:98`

**Tac dong**

- Peer chua xac thuc co the tac dong vao state ma hoa cua connection.
- Tang rui ro desync va tan cong DoS logic thong qua thao tung algorithm state truoc khi session on dinh.

**Tham chieu CWE**

- CWE-306: Missing Authentication for Critical Function
- CWE-284: Improper Access Control
- CWE-693: Protection Mechanism Failure

## 57. Cap Nhat Bo Sung (Server Handshake State Machine) - 2026-04-15

### BUG-75 (Medium): `HandleClientHelloAsync` cho phep ghi de `HandshakeState` nhieu lan, khong chan re-entry trong luc handshake dang tien hanh

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`HandshakeHandlers` chi chan khi `HandshakeEstablished` da dat, nhung khong chan truong hop handshake dang dang dở (da co `HandshakeState`). Client co the gui lap `CLIENT_HELLO` lien tuc, buoc server lap lai X25519 keygen/agreement + transcript derivation va overwrite state.

Day la lo hong state-machine/reentrancy, mo ra vector CPU DoS o giai doan pre-auth.

**Bang chung code**

- Chi kiem tra `HandshakeEstablished`, khong check `HandshakeState` ton tai:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:48`
- Moi `CLIENT_HELLO` deu tao keypair/agreement + state moi:
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:87`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:89`
  - `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs:116`

**Tac dong**

- Tang tai CPU pre-auth do phep spam `CLIENT_HELLO` re-entry.
- Co the lam fail/cham luong handshake hop le cua client khac trong dieu kien tai cao.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-841: Improper Enforcement of Behavioral Workflow

## 58. Cap Nhat Bo Sung (Server Control Transport Semantics) - 2026-04-15

### BUG-76 (Medium): `SystemControlHandlers` luon phan hoi qua `connection.TCP` bat ke `packet.Protocol`, gay desync neu control frame den qua UDP

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong cac handler control (`HandlePing`, `HandleTimeSyncRequest`, `HandleCipherUpdate`), server khoi tao reply voi `transport: packet.Protocol` nhung lai luon gui qua `connection.TCP.SendAsync(...)`.

Neu packet control den tu kenh UDP (hoac framework cho phep forward control UDP), reply se di sai kenh thuc te so voi metadata protocol, tao desync route va timeout o phia client dang cho response cung transport.

**Bang chung code**

- Reply mang theo `packet.Protocol`:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:102`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:110`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:118`
- Nhung gui luon bang TCP transport:
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:103`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:111`
  - `src/Nalix.Runtime/Handlers/SystemControlHandlers.cs:119`

**Tac dong**

- Gay mismatch transport semantics trong request-response control.
- De tao timeout/logic fail khi client ky vong response tren kenh khac.

**Tham chieu CWE**

- CWE-440: Expected Behavior Violation
- CWE-841: Improper Enforcement of Behavioral Workflow
- CWE-444: Inconsistent Interpretation of Input

## 59. Cap Nhat Bo Sung (Runtime Dispatcher Policy Enforcement) - 2026-04-15

### SEC-77 (High): Metadata policy (`PacketPermission`/`RateLimit`/`ConcurrencyLimit`) khong duoc enforce mac dinh trong dispatcher hot path

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Dispatcher co thu thap metadata policy tu attribute va truyen vao `PacketHandler.Metadata`, nhung luc execute lai chi goi `descriptor.CanExecute(context)`; ham nay hien tra ve `true` vo dieu kien.

`PacketDispatchOptions` constructor cung khong tu dong them middleware policy (_pipeline ban dau rong). Neu host khong tu dang ky middleware bo sung, cac attribute bao ve de facto bi vo hieu hoa.

**Bang chung code**

- Metadata policy duoc thu thap khi compile handler:
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:866`
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:868`
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:869`
- Hot path chi dua vao `CanExecute(context)`:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.Execution.cs:78`
- `CanExecute` hien luon `true`:
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandler.cs:119`
- `_pipeline` khong co middleware mac dinh trong constructor:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.cs:60`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.cs:63`

**Tac dong**

- Co nguy co bypass policy neu deploy quyen middleware bo sung: endpoint nhay cam co the chay ma khong qua gate permission/rate/concurrency.
- Tang rui ro abuse/DoS va vi pham boundary truy cap do metadata khong duoc thi hanh nhat quan.

**Tham chieu CWE**

- CWE-285: Improper Authorization
- CWE-862: Missing Authorization
- CWE-693: Protection Mechanism Failure

## 60. Cap Nhat Bo Sung (Runtime Handler Compilation Failure Semantics) - 2026-04-15

### BUG-78 (Medium): `PacketHandlerCompiler` nuot loi compile tung method va tiep tuc, de den trang thai fail-open voi tap handler khong day du

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Khi compile controller handlers, neu mot method nem exception, code chi log `failed-compile` roi `continue`, van tra ve bang handler da cat bot. Khong co co che fail-fast cho toan bo controller registration.

Dieu nay de gay tinh huong nguy hiem: dich vu khoi dong thanh cong nhung mot so opcode mong doi khong duoc dang ky, dan den hanh vi khong du doan, bo lo security flow hoac logic nghiep vu can thiet.

**Bang chung code**

- Catch exception trong vong compile tung method va tiep tuc:
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:215`
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:221`
- Tra ve bang handler da freeze du dang thieu:
  - `src/Nalix.Runtime/Internal/Compilation/PacketHandlerCompiler.cs:224`
- `WithHandler` tin vao ket qua compile va dang ky tiep:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.PublicMethods.cs:229`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchOptions.PublicMethods.cs:264`

**Tac dong**

- Startup co the "green" nhung route/opcode thuc te mat mot phan (silent feature/security regression).
- Tang rui ro production incident do khong co fail-fast khi tap handler khong toan ven.

**Tham chieu CWE**

- CWE-391: Unchecked Error Condition
- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-693: Protection Mechanism Failure

## 61. Cap Nhat Bo Sung (Runtime DispatchChannel Error Path) - 2026-04-15

### BUG-79 (High): `PacketDispatchChannel` dung `this.TrackError(...)` ben trong `static local function`, gay compile-time break/o sai owner tracking

**Trang thai**

- ✅ FIXED (2026-04-15)

**Mo ta**

Trong `PacketDispatchChannel`, hai `static local function` (`AwaitPipelineAsync`, `AwaitDispatchAsync`) goi `this.TrackError(connection)` thay vi `owner.TrackError(connection)`. Voi C#, `this` khong hop le trong static local function.

Neu code path nay duoc build nhu hien tai, day la loi compile nghiem trong; neu duoc chinh sua tam o noi khac, no van cho thay risk owner-context khong nhat quan trong error accounting.

**Bang chung code**

- `AwaitPipelineAsync` la static local function nhung goi `this.TrackError`:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:443`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:466`
- `AwaitDispatchAsync` tuong tu:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:526`
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:542`

**Tac dong**

- Co the lam vo build/runtime branch quan trong cua dispatcher.
- Error tracking/disconnect threshold co nguy co sai owner neu code duoc sua khong dong bo.

**Tham chieu CWE**

- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-670: Always-Incorrect Control Flow Implementation

## 62. Cap Nhat Bo Sung (Inbound Permission Response Semantics) - 2026-04-15

### BUG-80 (Medium): `PermissionMiddleware` tra `TIMEOUT + IS_TRANSIENT + UNAUTHENTICATED` cho truong hop thieu quyen, de kich hoat retry storm

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Khi deny do permission, middleware tao directive `ControlType.TIMEOUT`, `ProtocolReason.UNAUTHENTICATED` va flag `IS_TRANSIENT`. Semantics nay co xu huong khuyen khich client retry thay vi dung/reauth.

Voi client auto-retry theo transient control, hanh vi deny quyen co the bi khuếch đại thanh traffic loop khong can thiet (retry amplification), tang tai he thong.

**Bang chung code**

- Nhanh deny permission:
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:49`
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:50`
- Response su dung `TIMEOUT` + `UNAUTHENTICATED` + `IS_TRANSIENT`:
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:65`
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:66`
  - `src/Nalix.Network.Pipeline/Inbound/PermissionMiddleware.cs:68`

**Tac dong**

- Client co the retry lien tuc cho loi khong transient (thieu quyen/chua auth).
- Lam tang luu luong vo ich va de gay DoS logic do retry amplification.

**Tham chieu CWE**

- CWE-840: Business Logic Errors
- CWE-400: Uncontrolled Resource Consumption
- CWE-755: Improper Handling of Exceptional Conditions

## 63. Cap Nhat Bo Sung (Dispatch Queue Block Policy) - 2026-04-15

### SEC-81 (Medium): `DispatchChannel` co che `DropPolicy.Block` block ngay tren receive ingress path, de bi head-of-line DoS khi queue day

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`PacketDispatchChannel.HandlePacket` goi truc tiep `_dispatch.PushCore(...)`. Trong `DispatchChannel`, neu policy la `Block` va queue day, `PushCore` di vao `WaitForQueueSpace` (spin/yield/sleep) dong bo.

Dieu nay co nghia producer thread nhan packet (ingress path) bi block tai cho, de gay head-of-line blocking va giam nghiem trong kha nang tiep nhan packet hop le khac.

**Bang chung code**

- Ingress path goi `PushCore` dong bo:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:214`
- `DropPolicy.Block` dan den `WaitForQueueSpace`:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:375`
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:385`
- `WaitForQueueSpace` spin/yield/sleep trong vong lap:
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:402`
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:411`
  - `src/Nalix.Runtime/Internal/Routing/DispatchChannel.cs:416`

**Tac dong**

- Flood mot so connection co the lam nghen producer path, anh huong nhieu connection khac (head-of-line DoS).
- Tăng latency va giảm throughput toan he thong khi queue sat tran.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-770: Allocation of Resources Without Limits or Throttling
- CWE-833: Deadlock

## 64. Cap Nhat Bo Sung (Inbound RateLimit Fail-Open) - 2026-04-15

### SEC-82 (Medium): `RateLimitMiddleware` fail-open khi limiter bi dispose (`ObjectDisposedException`) va van cho packet di tiep

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `RateLimitMiddleware.InvokeAsync`, neu _policy/_global nem `ObjectDisposedException` thi middleware log debug roi goi `next(...)` tiep tuc. Hanh vi nay fail-open: trong giai doan race shutdown/reload limiter, request van qua gate rate-limit.

Dieu nay tao cua so bypass throttling trong thoi diem he thong mong manh nhat (dispose/reconfigure), de bi loi dung de day burst traffic.

**Bang chung code**

- Catch `ObjectDisposedException` va cho phep request tiep tuc:
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:80`
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:85`
- Duong danh gia limiter:
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:72`
  - `src/Nalix.Network.Pipeline/Inbound/RateLimitMiddleware.cs:77`

**Tac dong**

- Burst traffic co the lot qua khi limiter bi dispose/tai cau hinh.
- Tang rui ro DoS o cua so maintenance/restart.

**Tham chieu CWE**

- CWE-693: Protection Mechanism Failure
- CWE-400: Uncontrolled Resource Consumption

## 65. Cap Nhat Bo Sung (Dispatch Sync Blocking Path) - 2026-04-15

### BUG-83 (Medium): `HandlePacket(IPacket, IConnection)` dung `.Await()` dong bo tren `ExecutePacketHandlerAsync`, de gay thread starvation/deadlock risk

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

API `HandlePacket(IPacket, IConnection)` goi truc tiep `ExecutePacketHandlerAsync(...).Await()`, tuc la block dong bo cho den khi async flow ket thuc. Neu bi goi tren luong nhay cam (event loop/thread pool starved/context can marshal), co the dan den nghen luong va tang latency toan he thong.

Trong burst path, blocking wait tren async handler cung de gay thread starvation va hieu ung day chuyen den dispatch throughput.

**Bang chung code**

- Goi dong bo `.Await()` tren async dispatch:
  - `src/Nalix.Runtime/Dispatching/PacketDispatchChannel.cs:223`
- `.Await()` su dung `GetAwaiter().GetResult()` (blocking):
  - `src/Nalix.Framework/Extensions/TaskExtensions.cs:41`

**Tac dong**

- Tang nguy co thread starvation/deadlock trong mot so execution context.
- Giam thong luong dispatch khi co nhieu packet xu ly dong thoi.

**Tham chieu CWE**

- CWE-833: Deadlock
- CWE-400: Uncontrolled Resource Consumption
- CWE-662: Improper Synchronization

## 66. Cap Nhat Bo Sung (SDK UDP Connect Lifecycle) - 2026-04-15

### BUG-84 (High): `UdpSession.ConnectAsync` start worker voi `_loopCts.Token` truoc khi `_loopCts` duoc khoi tao, gay `NullReferenceException` va mat ket noi

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `ConnectAsync`, code tao `_asyncQueue` xong goi ngay `Task.Run(() => ProcessAsyncQueueAsync(_loopCts.Token))`, nhung `_loopCts` chi duoc gan sau do.

Thu tu nay tao race/null dereference on connect: lan ket noi dau co the nem `NullReferenceException`, khien session khong vao receive loop dung cach.

**Bang chung code**

- Goi worker truoc khi khoi tao CTS:
  - `src/Nalix.SDK/Transport/UdpSession.cs:143`
- CTS duoc khoi tao sau do:
  - `src/Nalix.SDK/Transport/UdpSession.cs:151`

**Tac dong**

- Co the gay fail ket noi ngau nhien tren duong UDP.
- Tạo cua so DoS logic (retry/connect storm) khi client tu dong reconnect.

**Tham chieu CWE**

- CWE-476: NULL Pointer Dereference
- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-400: Uncontrolled Resource Consumption

## 67. Cap Nhat Bo Sung (SDK TCP Async Queue Race) - 2026-04-15

### BUG-85 (Medium): `TcpSession.HandleReceiveMessage` dung `_asyncQueue.Writer` truc tiep thay vi local `writer`, de bi race-null khi disconnect

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`HandleReceiveMessage` da lay local `writer = _asyncQueue?.Writer`, nhung trong nhanh xu ly lai goi `_asyncQueue.Writer.TryWrite(...)` truc tiep.

Neu disconnect xay ra cung luc (`TryComplete` va `_asyncQueue = null`), nhanh nay co the dereference field da null du local `writer` truoc do khong null.

**Bang chung code**

- Lay local writer:
  - `src/Nalix.SDK/Transport/TcpSession.cs:265`
- Van dung field truc tiep trong nhanh ghi queue:
  - `src/Nalix.SDK/Transport/TcpSession.cs:278`
- Disconnect dong queue va set null:
  - `src/Nalix.SDK/Transport/TcpSession.cs:197`

**Tac dong**

- Loi race co the lam crash path nhan frame/handler.
- Tang rui ro mat goi va mat on dinh trong giai doan disconnect/reconnect.

**Tham chieu CWE**

- CWE-362: Concurrent Execution using Shared Resource with Improper Synchronization
- CWE-476: NULL Pointer Dereference

## 68. Cap Nhat Bo Sung (Concurrency Gate Scope Leakage) - 2026-04-15

### SEC-86 (Medium): `ConcurrencyGate` dung bang `static` theo opcode, gay policy bleed giua instance/tenant va first-writer-wins

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Bang gate duoc khai bao `static` va key chi theo `opcode`. Khi co nhieu `ConcurrencyGate` instance (nhieu host/tenant/cluster node trong cung process), entry cua opcode duoc chia se toan cuc.

`GetOrAdd` se giu cau hinh (`Max/Queue/QueueMax`) cua lan tao dau tien, cac attr sau cho cung opcode bi bo qua. Day la logic leak policy, co the lam endpoint nhay cam dung sai gioi han mong muon.

**Bang chung code**

- Bang du lieu static theo opcode:
  - `src/Nalix.Network.Pipeline/Throttling/ConcurrencyGate.cs:45`
- First-writer-wins config qua `GetOrAdd`:
  - `src/Nalix.Network.Pipeline/Throttling/ConcurrencyGate.cs:808`
  - `src/Nalix.Network.Pipeline/Throttling/ConcurrencyGate.cs:810`

**Tac dong**

- Chinh sach concurrency co the bi "chay lan" giua context khac nhau.
- Co the dan den under-limit (bypass throttle) hoac over-limit (tu choi sai) tuy thu tu khoi tao.

**Tham chieu CWE**

- CWE-284: Improper Access Control
- CWE-488: Exposure of Data Element to Wrong Session
- CWE-840: Business Logic Errors

## 69. Cap Nhat Bo Sung (SDK UDP Async Delivery Semantics) - 2026-04-15

### BUG-87 (Medium): Nhanh UDP `async+sync` bo qua ket qua `TryWrite`, co the drop frame im lang (khong bao loi)

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `UdpSession.ReceiveLoopAsync`, khi ca `asyncHandler` va `syncHandler` deu ton tai, code goi `_ = writer.TryWrite(...)` nhung khong kiem tra ket qua/khong bao loi neu queue day.

Nhanh async-only ben duoi lai co kiem tra `if (!writer.TryWrite(...))` de xu ly fail. Su khong nhat quan nay tao silent drop trong che do dual-handler.

**Bang chung code**

- Nhanh `async + sync` chi goi va bo qua ket qua:
  - `src/Nalix.SDK/Transport/UdpSession.cs:373`
  - `src/Nalix.SDK/Transport/UdpSession.cs:377`
- Nhanh khac co check fail ro rang:
  - `src/Nalix.SDK/Transport/UdpSession.cs:386`

**Tac dong**

- Mat ban tin async im lang khi queue bi pressure.
- Gay sai lech logic ung dung (sync thay packet, async state machine khong thay).

**Tham chieu CWE**

- CWE-391: Unchecked Error Condition
- CWE-440: Expected Behavior Violation
- CWE-754: Improper Check for Unusual or Exceptional Conditions

## 70. Cap Nhat Bo Sung (SDK TCP Sender Loop Poisoning) - 2026-04-15

### SEC-88 (Medium): Mot loi trong `FrameSender` co the "poison" send loop vinh vien (TryComplete writer), dan den DoS logic tren session TCP

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`FrameSender` khoi dong drain loop ngay tu constructor. Neu xay ra exception trong `DRAIN_LOOP_ASYNC` (vi du `_getSocket()` fail khi socket chua san sang), code se `TryComplete(ex)` tren writer va ket thuc vong drain.

Sau trang thai nay, send queue bi complete va khong co co che restart sender loop. Nghia la mot su co don le co the lam session mat kha nang gui ve sau (logic DoS).

**Bang chung code**

- Khoi dong drain loop ngay trong constructor:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:54`
- Drain loop lay socket qua callback:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:126`
- Exception path dong writer toan cuc:
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:108`
  - `src/Nalix.SDK/Transport/Internal/FrameSender.cs:111`
- Ben `TcpSession`, send API khong guard `IsConnected` truoc khi day vao sender:
  - `src/Nalix.SDK/Transport/TcpSession.cs:210`
  - `src/Nalix.SDK/Transport/TcpSession.cs:219`

**Tac dong**

- Mot lan fault sender co the lam session khong gui duoc nua den khi tao session moi.
- Tang rui ro DoS logic/availability regression trong dieu kien race connect-disconnect.

**Tham chieu CWE**

- CWE-400: Uncontrolled Resource Consumption
- CWE-703: Improper Check or Handling of Exceptional Conditions
- CWE-754: Improper Check for Unusual or Exceptional Conditions

## 71. Cap Nhat Bo Sung (SDK Lease Ownership Contract Mismatch) - 2026-04-15

### BUG-89 (High): Contract `OnMessageReceived` yeu cau consumer dispose lease, nhung `FrameReader` cung tu dispose sau callback (double-dispose/use-after-dispose risk)

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

`TransportSession.OnMessageReceived` document ro rang handler "nhan ownership tam thoi va phai dispose lease". Tuy nhien, `FrameReader.PROCESS_NORMAL_FRAME` sau khi goi `_onMessage(lease)` lai luon `lease.Dispose()` trong `finally`.

Neu consumer lam theo contract va dispose trong callback, co the xay ra double-dispose; neu consumer luu tham chieu de dung tiep, lease da bi dispose ngay sau callback (use-after-dispose semantics).

**Bang chung code**

- Contract event noi handler phai dispose:
  - `src/Nalix.SDK/Transport/TransportSession.cs:55`
  - `src/Nalix.SDK/Transport/TransportSession.cs:57`
- Reader goi callback roi van dispose lease:
  - `src/Nalix.SDK/Transport/Internal/FrameReader.cs:130`
  - `src/Nalix.SDK/Transport/Internal/FrameReader.cs:134`

**Tac dong**

- Gay hanh vi khong xac dinh cho consumer event (double release, corrupted lifecycle).
- Tang nguy co crash/intermittent bug trong pipeline xu ly packet.

**Tham chieu CWE**

- CWE-664: Improper Control of a Resource Through its Lifetime
- CWE-672: Operation on a Resource after Expiration or Release
- CWE-415: Double Free

## 72. Cap Nhat Bo Sung (Network Close Event Priority Mismatch) - 2026-04-15

### SEC-90 (High): `SocketConnection` day close callback qua lane `Invoke` (normal), co the bi backpressure-drop thay vi high-priority close lane

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong receive-loop shutdown/disconnect path, `SocketConnection.INVOKE_CLOSE_ONCE()` goi `AsyncCallback.Invoke(...)` cho `_callbackClose`.

Trong khi do `_callbackClose` tro den `Connection.OnCloseEventBridge`, va bridge nay duoc thiet ke de chay bang `InvokeHighPriority(...)`. Vi `SocketConnection` day bang lane normal, close event co the bi drop khi global/per-IP backpressure kich hoat, dan den bo lo cleanup chain (hub unregister/timing unregister/teardown hook).

**Bang chung code**

- `SocketConnection` invoke close qua normal lane:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:773`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:783`
- `_callbackClose` map toi `OnCloseEventBridge`:
  - `src/Nalix.Network/Connections/Connection.cs:76`
- Bridge close su dung high-priority lane:
  - `src/Nalix.Network/Connections/Connection.cs:306`
- Normal lane co co che drop khi backpressure:
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:155`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:173`
  - `src/Nalix.Network/Internal/Transport/AsyncCallback.cs:188`

**Tac dong**

- Co the mat su kien close trong peak load, de lai state treo/leak (connection bookkeeping khong duoc giai phong day du).
- Lam suy giam stability va mo rong cua so DoS thong qua pressure callback.

**Tham chieu CWE**

- CWE-404: Improper Resource Shutdown or Release
- CWE-693: Protection Mechanism Failure
- CWE-400: Uncontrolled Resource Consumption

## 73. Cap Nhat Bo Sung (Network Receive Lease Lifetime Leak) - 2026-04-15

### SEC-91 (High): Nhanh TCP non-fragment giu them reference `BufferLease` ma khong release, gay memory leak theo moi packet (DoS)

**Trang thai**

- ❗ NEW (chua fix)

**Mo ta**

Trong `SocketConnection.SAEA_RECEIVE_LOOP_ASYNC`, nhanh non-fragment tao `lease`, goi `lease.Retain()` roi handoff vao callback qua `ConnectionEventArgs`.

Neu enqueue thanh cong, code khong co `lease.Dispose()` cho local owner sau handoff. Khi callback xong, `args.Dispose()` chi giam 1 ref, de lai ref du bi ro. Kich ban fail enqueue cung tuong tu (args dispose nhung local ref van ton tai). Day la leak luy tien theo luong packet.

**Bang chung code**

- Tao lease non-fragment:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:466`
- Nhanh non-fragment tang ref + handoff:
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:550`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:551`
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:552`
- Khong co `lease.Dispose()` tuong ung trong nhanh non-fragment (trong khi nhanh fragment co dispose):
  - `src/Nalix.Network/Internal/Transport/SocketConnection.cs:541`

**Tac dong**

- Leak bo nho theo so packet non-fragment, de bi khai thac thanh memory-exhaustion DoS.
- Co the gay pressure GC/pool nghiem trong, tang latency va cuoi cung crash process.

**Tham chieu CWE**

- CWE-401: Missing Release of Memory after Effective Lifetime
- CWE-772: Missing Release of Resource after Effective Lifetime
- CWE-400: Uncontrolled Resource Consumption
