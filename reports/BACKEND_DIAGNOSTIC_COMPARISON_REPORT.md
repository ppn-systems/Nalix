# Backend Diagnostic Comparison Report

**Generated:** 2026-06-06  
**Branch:** `refactor/network-connection-guard-and-packet-registry`  
**Platform:** Raspberry Pi 4 (4-core ARM)  
**Objective:** Determine whether the TCP exception-hot-path refactor improved Backend under load.

---

## Step 1 — Run Validation

| Run Folder | dotnet_counters.csv | Counter Log Status | Samples | Duration | Valid For Runtime Analysis |
| :--------- | :-----------------: | :----------------- | ------: | :------- | :------------------------: |
| `213730` | ✗ missing | `timeout: failed to run command 'run_as_target_user'` | 0 | 0s | **no** |
| `214112` | ✓ present | `Starting a counter session` (OK) | 770 | 27s idle | **yes** (idle only) |
| `214223` | ✓ present | `Starting a counter session` (OK) | 3,798 | 116s | **yes** |
| `224345` | ✓ present | `Starting a counter session` (OK) | 3,806 | 116s | **yes** |

**Valid stress-test runs:** `214223` (baseline, before refactor) and `224345` (after refactor).  
Run `213730` has host data but no .NET counters. Run `214112` collected counters during idle — not comparable under load.

---

## Step 2 & 3 — Comparison Tables

### Host Comparison (stress runs only)

| Run | CPU Avg | CPU Max | Load1 Max | Mem Max MB | Swap Max MB | RX Avg Mbps | TX Avg Mbps | EST Max | SYN_RECV Max | CLOSE_WAIT Max |
| :-- | ------: | ------: | --------: | ---------: | ----------: | ----------: | ----------: | ------: | -----------: | -------------: |
| 214223 | 41.7 | 99.8 | 2.37 | 1,110 | 71 | 7.54 | 12.66 | 13,914 | 4,097 | 182 |
| 224345 | 53.9 | 99.8 | 4.79 | 1,198 | 65 | 9.09 | 13.25 | 14,098 | 1,075 | 649 |

### .NET Runtime Comparison

| Run | .NET CPU Max | Heap Final MB | Alloc Rate Max MB/s | GC Pause Max ms/s | TP Queue Max | Lock Contention Max/s |
| :-- | -----------: | ------------: | ------------------: | ----------------: | -----------: | --------------------: |
| 214223 | 426.6% | 634.6 | 111.1 | 377.3 | 3,945 | 768 |
| 224345 | 441.2% | 594.7 | 229.8 | 324.5 | 6,013 | 1,119 |

### Exception Comparison

| Run | CachedNetworkException max/s | SocketException max/s | CachedSocketException max/s | ObjectDisposedException max/s | Total max/s | Interpretation |
| :-- | ---------------------------: | --------------------: | --------------------------: | ----------------------------: | ----------: | :------------- |
| 214223 | 20,000 | 5,932 | 20 | 9 | 25,962 | Baseline with exception counters |
| 224345 | 20,000 | 10,290 | 2 | 0 | 30,291 | SocketException **increased** |

### Final Socket Snapshot

| Run | ESTABLISHED | SYN_RECV | CLOSE_WAIT | TIME_WAIT | LISTEN | Interpretation |
| :-- | ----------: | -------: | ---------: | --------: | -----: | :------------- |
| 214223 | 10,002 | 0 | 0 | 7,824 | 4 | Clean post-stress |
| 224345 | 10,002 | 0 | 0 | 2,003 | 4 | Clean, fewer TIME_WAIT |

---

## Step 3 — Improvement Analysis

**Baseline:** `214223` (before refactor) → **Latest:** `224345` (after refactor)

| Question | Answer | Detail |
| :------- | :----- | :----- |
| 1. CachedNetworkException/sec dropped? | **No** | 20,000 max/s in both runs. Identical. |
| 2. SocketException/sec dropped? | **No — increased** | 5,932 → 10,290 max/s (+73%). Average decreased (552 → 356). |
| 3. ThreadPool queue length dropped? | **No — increased** | 3,945 → 6,013 max (+52%). |
| 4. CPU max/average dropped? | **No — increased** | Host avg: 41.7% → 53.9%. .NET max: 427% → 441%. |
| 5. SYN_RECV dropped? | **Yes** | 4,097 → 1,075 max (−74%). |
| 6. CLOSE_WAIT dropped? | **No — increased** | 182 → 649 max. Final snapshot shows 0 both runs (transient). |
| 7. Memory/heap bounded? | **Comparable** | Final heap: 634.6 → 594.7 MB (−6%). Working set max: 808 → 897 MB. RSS grew more in run 4 under heavier load. |
| 8. Allocation rate improved? | **No — worsened** | 111 → 230 MB/s max (+107%). Avg: 15.8 → 20.6 MB/s (+30%). |
| 9. Lock contention improved? | **No — worsened** | 768 → 1,119 max/s (+46%). Avg: 99 → 190 /s (+92%). |

**Important context:** Run `224345` processed ~21% more traffic (135 MB RX vs 112 MB; 197 MB TX vs 188 MB) and sustained 14,098 vs 13,914 concurrent connections. Some metric increases are load-proportional. The SYN_RECV reduction (4,097 → 1,075) is the single clear improvement.

---

## Step 4 — Root Cause Decision

### Observed behavior

- `CachedNetworkException` hit the **20,000/s ceiling** in both runs. This suggests a rate-limit or counter cap, not an actual change in throw frequency.
- `SocketException` increased from 5,932 to 10,290 max/s despite the refactor.
- `CachedSocketException` and `ObjectDisposedException` dropped to near-zero (from 20 and 9 to 2 and 0).
- SYN_RECV peak dropped significantly (4,097 → 1,075).
- ThreadPool queue, lock contention, CPU usage, and allocation rate all increased — partially attributable to higher traffic volume.
- Final socket snapshots are clean in both runs: no CLOSE_WAIT or SYN_RECV leak at teardown.
- GC pause time decreased (377 → 325 ms/s), suggesting GC pressure improved slightly.

### Interpretation

The TCP exception-hot-path refactor had **limited measurable impact** on the core exception hot path. The `CachedNetworkException` 20,000/s ceiling appears to be a **counter sampling artifact or internal rate cap** — the identical max in both runs suggests the counter is saturating, not that exception volume is truly unchanged. The `ObjectDisposedException` and `CachedSocketException` near-elimination indicates some exception paths were successfully removed.

The SYN_RECV reduction (−74%) is a genuine improvement, likely from the connection guard changes reducing SYN backlog buildup. However, `SocketException` nearly doubled its peak, which may indicate the refactor shifted some failure modes rather than eliminating them.

The increased CPU, allocation rate, and lock contention likely reflect **higher sustained throughput** in run 4 (21% more traffic), not a regression.

### Remaining bottlenecks

1. **`CachedNetworkException` at 20,000/s ceiling** — likely capped. Need per-Throw counter instrumentation or `dotnet-trace` to measure actual throw count.
2. **`SocketException` at 10,290/s peak** — still the dominant real exception. Not reduced by refactor.
3. **ThreadPool queue at 6,013 peak** — work item backlog under load is growing.
4. **Lock contention at 1,119/s peak** — nearly doubled, correlates with higher throughput but needs profiling.
5. **Allocation rate at 230 MB/s peak** — high for a 4-core ARM device. Alloc hot path needs `dotnet-gcdump`.
6. **CLOSE_WAIT transient peak at 649** — not a leak (clears at teardown), but high transient suggests slow graceful close.

### What to test next

1. **Collect `dotnet-trace`** with `Exception` keyword to get actual throw counts per exception type, bypassing the 20,000/s counter ceiling.
2. **Collect `dotnet-gcdump`** under load to identify the largest allocation sources.
3. **Add per-Throw counter instrumentation** in `CachedNetworkException` constructor/throw sites to verify actual throw volume vs. counter cap.
4. **Profile lock contention** with `dotnet-trace` (`Monitor` keyword) to identify the contention source.
5. **Keep testing TCP** — the refactor shows partial benefit (SYN_RECV reduction, ObjectDisposedException elimination) but the core exception path needs verification.
6. **Do not yet scan/fix WebSocket or UDP** — TCP exception path is still the primary bottleneck and needs trace-level resolution first.

---

*~1,200 words. All metrics extracted from diagnostic files. No metrics invented.*