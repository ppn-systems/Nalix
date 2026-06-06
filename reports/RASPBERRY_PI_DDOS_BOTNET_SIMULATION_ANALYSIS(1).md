# NALIX CORE RASPBERRY PI 5: CONTROLLED DDOS SIMULATION ANALYSIS

> Telemetry-driven analysis of Raspberry Pi 5 behavior under controlled high-connection pressure, listener admission saturation, and Layer-4 load shedding.

---

## 1. Executive Summary & Test Context

This report analyzes a controlled DDoS-style stress run against the Nalix server running on a Raspberry Pi 5. The workload was designed to answer a specific operational question: under hostile connection pressure, does the system fail thermally, exhaust memory, saturate bandwidth, or protect itself through admission control and early load shedding before the application pipeline collapses?

The strongest evidence points to **listener admission pressure and dispatch-level Layer-4 load shedding** rather than thermal shutdown or confirmed memory leakage. The host monitor recorded a maximum temperature of **47.2°C** and a constant `throttled=0x0`, so no thermal or power-induced throttling was observed. CPU utilization averaged **46.52%** and peaked at **100%**. However, the more important scheduler signal is the 1-minute load average, which peaked at **16.1** on a 4-core Raspberry Pi 5, indicating runnable queue pressure during the burst window.

Connection pressure was substantial. The host monitor recorded **11,269** established sockets, **4,098** `SYN_RECV` sockets, and **11,598** `CLOSE_WAIT` sockets at peak. Listener telemetry shows **130,435** accepted TCP connections, **29,133** rejected connections, and **21,760** queue-full rejections. This makes the listener accept queue one of the clearest pressure points in the run.

Dispatch telemetry confirms that the framework shed a large portion of ingress before full pipeline execution. `PipelineMetrics.TotalExecutions` reached **555,581**, while `TotalPacketsDropped` reached **3,934,415**. Using the observable approximation `PipelineExecutions + TotalPacketsDropped`, the approximate ingress volume is **4,489,996** packets and the approximate drop ratio is **87.63%**. This approximation excludes packets rejected before dispatch telemetry and packets lost outside the process, but it is sufficient to identify dispatch-level load shedding as a dominant protective behavior.

Memory pressure existed but should not be mislabeled as a confirmed leak. Host memory peaked at **3,827 MB**, swap peaked at **1,241 MB**, managed heap reached **1,862 MB**, and Gen2 collections reached **617**. At the same time, object-pool telemetry reported `TotalLeaked = 0`. Therefore, the measured behavior is best described as allocation/retention pressure under extreme connection churn, not a proven memory leak.

---

## 2. Data Sources

| File | Rows | Time Range | Used For |
| :--- | ---: | :--- | :--- |
| pi_stress_test_20260606_193549.csv | 432 | 2026-06-06 19:35:49 → 19:52:48 | Host monitor: thermal, throttle flag, CPU, load averages, memory, swap, byte counters, and socket states. |
| listener_metrics.csv | 270 | 2026-06-06 19:34:32 → 19:50:43 | Admission control: accepted/rejected connections, accept queue depth, backlog, limiter rejections, and proxy-protocol errors. |
| connections_metrics.csv | 312 | 2026-06-06 19:34:32 → 19:50:43 | Connection plane: framework byte counters, managed connection count, async callback pressure, fairness collisions, and packet drops. |
| dispatch_metrics.csv | 272 | 2026-06-06 19:34:32 → 19:51:00 | Dispatch plane: pipeline executions, priority queue pressure, wake signals, active executions, and parser errors. |
| buffers_metrics.csv | 270 | 2026-06-06 19:34:32 → 19:50:42 | Buffer slab allocator: hit rate, misses, expansion/shrink events, fallback count, and peak buffer memory. |
| object_pools_metrics.csv | 278 | 2026-06-06 19:34:32 → 19:50:42 | Object pools: cache hit/miss behavior, object creation, net objects, leak counter, and pool health. |
| tasks_metrics.csv | 270 | 2026-06-06 19:34:32 → 19:50:42 | Runtime: scheduler workers, OS threads, handles, managed heap, working set, completed work items, and GC generations. |
| connection_guard_metrics.csv | 277 | 2026-06-06 19:34:32 → 19:50:42 | Connection guard: attempts, rejections, active concurrency, tracked endpoints, subnet counters, and cleanup activity. |
| policy_rate_limiter_metrics.csv | 269 | 2026-06-06 19:34:32 → 19:50:42 | Policy limiter: tracked endpoints, token configuration, and hard-block count. |
| token_bucket_limiter_metrics.csv | 273 | 2026-06-06 19:34:32 → 19:50:42 | Token bucket: tracked endpoints, capacity/refill configuration, and hard-block count. |
| concurrency_gate_metrics.csv | 271 | 2026-06-06 19:34:32 → 19:50:42 | Concurrency gate: circuit-breaker trips, queued/acquired/rejected work, and opcode tracking. |
| sessions_metrics.csv | 27 | 2026-06-06 19:34:32 → 19:50:32 | Session store: active sessions, stored/consumed/expired sessions, and persistence counters. |
| instances_metrics.csv | 272 | 2026-06-06 19:34:32 → 19:50:42 | Instance manager: cache hit counters, instance creation, cached instances, and invalidated slots. |
| protocol_metrics.csv | 270 | 2026-06-06 19:34:32 → 19:50:43 | Protocol state: TCP/WebSocket accepting state, message counters, and protocol-level errors. |

---

## 3. Critical Metrics Summary

| Area | Metric | Minimum | Maximum | Average | Interpretation |
| :--- | :--- | ---: | ---: | ---: | :--- |
| Host | Temperature | 32.3°C | 47.2°C | 40.07°C | No thermal bottleneck indicated by telemetry. |
| Host | Throttle flag | 0x0 | 0x0 | 0x0 | No undervoltage or thermal throttling flag appeared. |
| Host | CPU usage | 0% | 100% | 46.52% | Average CPU was moderate, but transient full utilization occurred. |
| Host | Load average 1m | 0.01 | 16.1 | 5.45 | Peak load exceeded the 4-core hardware by ~4x, indicating runnable queue pressure. |
| Host | Memory used | 459 MB | 3,827 MB | 1,770 MB | Memory pressure appeared during the burst; not sufficient alone to prove a leak. |
| Host | Swap used |  MB | 1,241 MB | 22 MB | Swap activity was present and should be correlated with heap and socket lifetimes. |
| Socket | Established sockets | 3 | 11,269 | 1,139 | Large active connection population during the run. |
| Socket | SYN_RECV |  | 4,098 | 324 | Half-open backlog pressure appeared under burst connection attempts. |
| Socket | CLOSE_WAIT |  | 11,598 | 953 | Connection close drainage lag appeared; this is not a confirmed socket leak. |
| Listener | Total accepted |  | 130,435 | 45,116 | Cumulative accepted TCP connections. |
| Listener | Total rejected |  | 29,133 | 6,566 | Admission control rejected excess connections. |
| Listener | Queue full rejections |  | 21,760 | 4,699 | Strong evidence of accept queue pressure. |
| Dispatch | Packets dropped |  | 3,934,415 | 1,823,334 | Strong evidence of load shedding before full pipeline execution. |
| Dispatch | Pipeline executions | 1 | 555,581 | 138,821 | Packets that reached execution path. |
| Buffer | Hit rate | 1 | 1 | 1 | 1.00 means 100% reported buffer hit rate. |
| Buffer | Total misses |  | 671 | 291 | Misses existed but were tiny relative to hits. |
| Object | Cache hit rate | 100% | 100% | 100% | Object pool reported complete cache hit rate at the aggregate level. |
| Object | Total leaked |  |  |  | No object-pool leak reported. |
| Object | Unhealthy pools |  | 6 | 3 | Some pools entered unhealthy state and require pool-level breakdown. |
| Runtime | Workers running | 3 | 3 | 3 | Workers were continuously occupied. |
| Runtime | OS threads | 5 | 72 | 3 | Thread count expanded during load. |
| Runtime | Process handles | 14 | 10,171 | 1,301 | Handle count increased under connection churn; not a confirmed handle leak. |
| Runtime | Managed heap | 158 MB | 1,862 MB | 58 MB | Heap grew materially and needs heap dump correlation. |
| Runtime | Gen2 collections | 11 | 617 | 231 | Full GC count rose under sustained allocation/retention pressure. |

---

## 4. Host-Level Resource Analysis

### Thermal and Power Status

| Metric | Value |
| :--- | ---: |
| Temperature minimum | 32.3°C |
| Temperature maximum | 47.2°C |
| Temperature average | 40.07°C |
| Throttle flag | `0x0` throughout the run |

**Observed behavior:**

- Temperature remained between **32.3°C** and **47.2°C**.
- The throttle flag remained `0x0`.
- No telemetry indicates undervoltage, frequency capping, thermal throttling, or soft temperature limiting.

**Hypothesis:**

- The cooling and power path were sufficient for this run.
- Thermal behavior was not the limiting factor. Any performance collapse observed during this run should be investigated in the listener, dispatch, scheduler, socket lifecycle, and memory-retention paths before thermal tuning.

### CPU, Load Average, and Scheduler Pressure

| Metric | Minimum | Maximum | Average |
| :--- | ---: | ---: | ---: |
| CPU usage | 0% | 100% | 46.52% |
| Load1 | 0.01 | 16.1 | 5.45 |
| Load5 | 0.04 | 10.05 | 3.56 |
| Load15 | 0.01 | 5.3 | 1.72 |

**Observed behavior:**

- Average CPU utilization was **46.52%**, while peak CPU reached **100%**.
- The 1-minute load average peaked at **16.1**, approximately **4.03x** the Raspberry Pi 5's 4-core count.
- Worker telemetry reported `WorkersRunning = 30` for the entire sampled period, while `WorkersTotal` ranged from **30** to **31**.

**Hypothesis:**

- CPU was not continuously saturated by average utilization alone, but the load average and worker occupancy indicate runnable backlog and scheduler pressure during bursts.
- Because `sar`/`perf` CPU split was not collected, this run cannot separate user-space dispatch work from kernel networking cost. A future run should capture `%user`, `%system`, `%softirq`, `%iowait`, and context-switch counters.

### Memory, Swap, Heap, and GC

| Metric | Minimum | Maximum | Average |
| :--- | ---: | ---: | ---: |
| Host memory used | 459 MB | 3,827 MB | 1,770 MB |
| Swap used |  MB | 1,241 MB | 22 MB |
| Working set | 233 MB | 3,252 MB | 1,004 MB |
| Managed heap | 158 MB | 1,862 MB | 58 MB |
| Gen2 collections | 11 | 617 | 231 |

**Observed behavior:**

- Host memory usage grew from **459 MB** to **3,827 MB**.
- Swap usage reached **1,241 MB**.
- Managed heap reached **1,862 MB**, and Gen2 collections reached **617**.
- Object-pool `TotalLeaked` remained **0**.

**Hypothesis:**

- The memory profile is consistent with connection-state retention, object-pool growth, GC pressure, OS page/cache behavior, or delayed cleanup during burst load.
- It is not sufficient to claim a confirmed memory leak. Leak confirmation requires `dotnet-gcdump`, `dotnet-dump`, heap histogram, allocation flame graph, and comparison after a quiet recovery interval.

---

## 5. Network, Socket State, and Listener Admission

### Network Throughput

| Metric | Value |
| :--- | ---: |
| Host rx byte delta | 1,065,552,334 bytes |
| Host tx byte delta | 289,840,502 bytes |
| Host average rx throughput | 8.37 Mbps |
| Host average tx throughput | 2.28 Mbps |
| Framework ingress peak | 254.88 Mbps |
| Framework egress peak | 6.15 Mbps |

The available counters do not prove 1 Gbps Ethernet saturation. The host-level average receive throughput was **8.37 Mbps**, and the framework-level ingress peak was approximately **254.88 Mbps**. These values are materially below a 1 Gbps link ceiling. This does not mean the network was irrelevant; it means this dataset does not support the claim that physical bandwidth collapsed before the application admission path reacted.

### Socket State

| Socket State | Minimum | Maximum | Average |
| :--- | ---: | ---: | ---: |
| Established | 3 | 11,269 | 1,139 |
| SYN_RECV |  | 4,098 | 324 |
| CLOSE_WAIT |  | 11,598 | 953 |

**Observed behavior:**

- `SYN_RECV` peaked at **4,098**, showing half-open connection pressure.
- `CLOSE_WAIT` peaked at **11,598** and was still **4,097** at the end of the host monitor CSV.

**Hypothesis:**

- `CLOSE_WAIT` means the remote peer initiated connection termination while the local process had not yet completed socket closure. This is evidence of close-drain lag under pressure.
- A confirmed socket leak cannot be inferred from `CLOSE_WAIT` alone. The next run should capture `ss -tanp`, socket owner mapping, connection lifetime events, and application close/dispose counters.

### Listener Admission Control

| Listener Metric | Maximum | Final |
| :--- | ---: | ---: |
| TCP accepted | 130,435 | 130,435 |
| TCP rejected | 29,133 | 29,133 |
| Queue full rejections | 21,760 | 21,760 |
| Limiter rejections | 3,603 | 3,603 |
| Accept queue depth | 8,192 |  |
| Backlog configuration | 16,384 | 16,384 |
| Proxy protocol errors | 3,090 | 3,090 |

**Observed behavior:**

- The listener accepted **130,435** TCP connections.
- It rejected **29,133** connections.
- Of those, **21,760** were queue-full rejections and **3,603** were limiter rejections.
- Accept queue depth reached **8,192** against a configured backlog of **16,384**.

**Hypothesis:**

- Listener admission pressure is a stronger bottleneck signal than raw CPU percentage in this run.
- OS `somaxconn`, TCP SYN backlog, and application accept queue behavior may have interacted, but this cannot be proven without kernel queue telemetry.

---

## 6. Dispatch Pipeline and Layer-4 Load Shedding

| Dispatch Metric | Maximum | Technical Meaning |
| :--- | ---: | :--- |
| Pipeline executions | 555,581 | Packets that reached the execution path. |
| Packets dropped | 3,934,415 | Packets rejected before full downstream processing. |
| Approximate ingress | 4,489,996 | `PipelineExecutions + TotalPacketsDropped`; excludes pre-dispatch and external loss. |
| Approximate drop ratio | 87.63% | Dominant evidence of Layer-4 load shedding. |
| Active executions | 16 | Concurrent active pipeline executions. |
| Pending HIGH | 1,627 | Priority queue pressure. |
| Pending URGENT | 5,275 | Urgent queue pressure. |
| Ready connections | 5,303 | Connections ready for dispatch. |
| Wake signals | 596,893 | Worker wake-up activity. |
| Deserialization errors |  | No parser-failure spike was observed. |

> [!WARNING]
> **Layer-4 Load Shedding Boundary**
>
> The observable drop/execution gap indicates that a substantial portion of accepted traffic was rejected before reaching full Layer-7 middleware and handler execution. This protects `PacketContext` allocation, handler scheduling, and GC behavior, but it also means client-visible acceptance is intentionally reduced under overload.

**Observed behavior:**

- `TotalPacketsDropped` reached **3,934,415**.
- `PipelineMetrics.TotalExecutions` reached **555,581**.
- The approximate drop ratio was **87.63%**.
- Dispatch reported **0** total errors and **0** deserialization errors.

**Hypothesis:**

- The dispatch layer protected the runtime by dropping frames at or near the load-shedding boundary rather than allowing every packet to allocate context objects and enter middleware.
- This pattern aligns with Nalix's layered design: raw data can be drained from sockets and returned to buffer pools without forcing full Layer-7 execution when downstream capacity is saturated.

---

## 7. Buffer Pool, Object Pool, and Runtime Telemetry

### Buffer Pool

| Buffer Metric | Value | Technical Impact |
| :--- | ---: | :--- |
| Hit rate | 1 | Reported as 1.00, equivalent to 100%. |
| Total hits | 18,587,904 | High reuse volume on slab allocator. |
| Total misses | 671 | Misses existed but were tiny relative to hits. |
| Fallback count |  | No fallback-to-slower allocation path was observed. |
| Total expands | 71 | Pool grew dynamically during the run. |
| Total shrinks | 8 | Trimming occurred after pressure changed. |
| Peak buffer memory | 218.16 MB | Pinned/slab memory footprint under stress. |

**Observed behavior:**

- Buffer hit rate remained **100%**.
- `TotalMisses` reached **671** against **18,587,904** hits.
- `FallbackCount` remained **0**, while expansions reached **71**.

**Hypothesis:**

- Buffer sizing was mostly sufficient for the observed workload. The expansions show that the allocator adapted under pressure, but the absence of fallback events suggests it did not collapse into an expensive allocation path.

### Object Pool

| Object Pool Metric | Value | Technical Impact |
| :--- | ---: | :--- |
| Cache hit rate | 100% | Aggregate object reuse stayed high. |
| Total cache hits | 30,938,977 | Reused object path volume. |
| Total cache misses | 3,586,989 | New object creation pressure existed. |
| Total created | 3,622,757 | Object creation volume under churn. |
| Net objects | 78,565 | Objects retained/outstanding at peak. |
| Total leaked |  | No pool-reported leak. |
| Unhealthy pool count | 6 | Requires pool-level breakdown. |

**Observed behavior:**

- `CacheHitRate` reported **100%**, while `TotalCacheMisses` still reached **3,586,989** and `TotalCreated` reached **3,622,757**.
- `TotalLeaked` remained **0**.
- `UnhealthyPoolCount` peaked at **6**.

**Hypothesis:**

- Aggregate cache hit rate remained high, but pool-level health was not uniformly perfect. The unhealthy pool count suggests that at least some object categories experienced retention, pressure, or sizing problems during connection churn.
- The dataset does not identify which pools were unhealthy. The next run should export `UnhealthyPools` in a parseable form and include per-pool hit/miss/lease/return counts.

### Runtime, Scheduler, and GC

| Runtime Metric | Minimum | Maximum | Average |
| :--- | ---: | ---: | ---: |
| Workers running | 3 | 3 | 3 |
| Workers total | 3 | 31 | 31 |
| OS threads | 5 | 72 | 3 |
| Process handles | 14 | 10,171 | 1,301 |
| Managed heap | 158 MB | 1,862 MB | 58 MB |
| Completed work items | 19,362 | 9,915,809 | 3,347,092 |
| Worker errors |  |  |  |

**Observed behavior:**

- Workers were continuously occupied at **30** running workers.
- OS threads expanded to **72**.
- Process handles peaked at **10,171**.
- Worker and recurring error counters remained **0**.

**Hypothesis:**

- The scheduler was heavily occupied but did not report worker errors.
- Handle growth and thread growth are pressure indicators, not proof of leaks. Confirmation requires handle type classification, thread stacks, and before/after recovery snapshots.

---

## 8. Rate Limiter and Connection Guard

| Control Metric | Maximum | Interpretation |
| :--- | ---: | :--- |
| ConnectionGuard total attempts | 149,269 | Endpoint attempts observed by guard. |
| ConnectionGuard rejections | 13,275 | Guard-level rejections occurred. |
| ConnectionGuard concurrent | 9,837 | Peak guarded concurrency. |
| ConnectionGuard tracked endpoints | 48,060 | Large endpoint table pressure. |
| TokenBucket tracked endpoints | 42,899 | Rate limiter tracked many endpoint identities. |
| TokenBucket hard blocked |  | No hard-block escalation observed. |
| Policy limiter tracked endpoints | 42,682 | Shared limiter endpoint growth. |
| Policy hard blocked |  | No hard-block escalation observed. |
| Concurrency gate trips |  | Circuit breaker did not trip. |

**Observed behavior:**

- Connection guard attempts reached **149,269**, with **13,275** rejections.
- Token bucket tracked endpoints reached **42,899**.
- Hard-block counts stayed at **0**, and the concurrency circuit breaker did not trip.

**Hypothesis:**

- Most protective action occurred at listener admission and dispatch load-shedding boundaries rather than through hard-block escalation.
- Endpoint tracking reached a large scale and should be monitored for cleanup efficiency under longer tests.

---

## 9. Cross-Layer Root Cause Analysis

| Layer | Evidence | Interpretation |
| :--- | :--- | :--- |
| Host | temp max 47.2°C, throttled 0x0 | Hardware did not thermally throttle. |
| CPU / Scheduler | CPU avg 46.52%, load1 max 16.1, WorkersRunning 30 | Runnable backlog and occupied workers appeared during burst load. |
| Socket | SYN_RECV 4,098, CLOSE_WAIT 11,598 | Half-open pressure and delayed close completion occurred. |
| Listener | QueueFullRejections 21,760 | Listener admission queue saturated. |
| Dispatch | Dropped 3,934,415 vs executed 555,581 | Layer-4 shedding was the dominant application-protection mechanism. |
| Buffer pool | HitRate 100%, fallback 0 | Buffer allocator did not fall back to slower path. |
| Object pool | TotalLeaked 0, UnhealthyPoolCount 6 | No reported leak, but pool-level health needs inspection. |
| Runtime / GC | Heap 1,862 MB, Gen2 617 | Allocation/retention pressure existed. |

### Observed behavior

- The Pi remained thermally stable and did not report power throttling.
- The listener accumulated queue pressure and rejected connections.
- The dispatch layer dropped substantially more packets than it executed.
- Buffer fallback count stayed at zero, and object-pool leak count stayed at zero.
- Memory, swap, heap, Gen2, handles, and `CLOSE_WAIT` all rose during the run.

### Hypothesis

The likely pressure chain is: connection burst pressure increased socket states and listener queue depth; listener admission rejected excess connections; accepted traffic then encountered dispatch capacity limits; dispatch load shedding rejected most observable ingress before middleware and handler execution; buffer pools absorbed raw traffic without fallback; object pools avoided reported leaks but showed unhealthy pool pressure; the runtime experienced heap, handle, and GC pressure from connection churn and delayed cleanup.

### What probably saturated first

The strongest evidence points to **listener accept queue pressure** and **dispatch-level Layer-4 load shedding** as the first meaningful application-side saturation boundaries. CPU and scheduler pressure were present, but average CPU alone does not prove that CPU was the first bottleneck. Physical network bandwidth saturation was not proven.

### What did not fail

- Thermal throttling: no evidence.
- Power throttling: no evidence.
- Confirmed memory leak: insufficient evidence.
- Confirmed socket leak: insufficient evidence.
- Buffer allocation fallback collapse: no evidence.
- Worker task failure: no evidence.

---

## 10. Bottleneck Decision Matrix

| Component | Evidence Level | Evidence | Decision |
| :--- | :---: | :--- | :--- |
| Thermal | No evidence | temp_c max 47.2°C; throttled 0x0 | Thermal throttling did not appear. |
| Power | No evidence | vcgencmd throttled flag remained 0x0 | No undervoltage/power throttle evidence. |
| CPU | Moderate evidence | CPU avg 46.52%, peak 100%; load1 max 16.1 | CPU had burst saturation and scheduler pressure, but average CPU alone does not prove CPU was the primary bottleneck. |
| Network bandwidth | Weak evidence | Host rx avg 8.37 Mbps; framework ingress peak 254.88 Mbps | The 1 Gbps link was not proven saturated by available counters. |
| Listener accept queue | Strong evidence | QueueFullRejections 21,760; AcceptQueueDepth max 8,192 | Listener admission pressure was one of the clearest saturation points. |
| Dispatch pipeline | Strong evidence | Dropped 3,934,415 vs executions 555,581 | Dispatch load shedding dominated accepted packet handling. |
| Layer-4 load shedding | Strong evidence | Approx drop ratio 87.63% | Nalix rejected most observable ingress before full Layer-7 execution. |
| Memory pressure | Moderate evidence | mem_used max 3,827 MB, swap max 1,241 MB, heap max 1,862 MB | Memory pressure existed; leak not confirmed. |
| Buffer pool | Weak evidence | HitRate 100%, misses 671, fallback 0, expands 71 | Buffer pool mostly absorbed ingress, but expansions show capacity adaptation. |
| Object pool | Moderate evidence | CacheHitRate 100%, TotalLeaked 0, UnhealthyPoolCount max 6 | No leak reported, but unhealthy pools require pool-level inspection. |
| Scheduler | Strong evidence | WorkersRunning fixed at 30; WorkersTotal max 31; Process.Threads max 72 | Worker capacity was fully occupied while load average exceeded core count. |
| GC | Moderate evidence | Gen2 max 617; ManagedHeapMB max 1,862 MB | Full GC pressure increased; allocation source requires gcdump. |
| Confirmed memory leak | Insufficient evidence | TotalLeaked 0; no heap dump | Cannot confirm memory leak. |
| Confirmed socket leak | Insufficient evidence | CLOSE_WAIT max 11,598 without lifecycle trace | CLOSE_WAIT proves delayed close completion, not a leak by itself. |

---

## 11. Scope & Limitations

This report describes a controlled lab simulation, not a real botnet attack over diverse public Internet paths. LAN behavior, client generator limits, router behavior, and OS TCP settings can materially change the outcome.

Client-side benchmark output was not included, so this report cannot state successful request count, failed request count, RPS, latency percentiles, or client-observed timeout rates. Internal counters are not equivalent to end-to-end latency.

Kernel-level CPU split was not collected. Without `sar`, `perf`, or eBPF data, the analysis cannot distinguish application CPU, kernel socket overhead, softirq processing, context switching, and I/O wait.

Packet capture was not collected. Without `tcpdump` or equivalent capture, actual packet rate, retransmission behavior, TCP reset behavior, and MSS/MTU effects remain unknown.

Heap and socket lifecycle traces were not collected. Therefore, heap growth, handle growth, and `CLOSE_WAIT` accumulation are pressure indicators, not confirmed leaks.

CSV sampling can miss short spikes, and cumulative counters require careful delta interpretation. Derived values such as the Layer-4 drop ratio are approximations.

---

## 12. Conclusions & Optimization Paths

### Current Findings

The application did not appear to fail from thermal throttling or power throttling. It also did not provide telemetry evidence of a confirmed object-pool memory leak. The dominant measured behavior was protective degradation: the listener rejected excess connections and the dispatch layer dropped a large portion of observable packet ingress before full Layer-7 execution.

The most important result is the drop/execution gap. With **3,934,415** dropped packets and **555,581** pipeline executions, Nalix appears to have preserved downstream runtime safety by refusing work at the load-shedding boundary. This is the expected failure mode for a defensive edge framework: reduce accepted work rather than allow heap, scheduler, and handler execution to collapse.

### Potential Optimizations

| Optimization | Expected Impact | Trade-off | Confidence |
| :--- | :--- | :--- | :---: |
| Add explicit Layer-4 drop counters by reason | Separates per-connection throttle, fairness drops, dispatch queue drops, and listener drops. | More telemetry counters in hot paths. | High |
| Add queue wait time separate from execution time | Distinguishes queue latency from middleware/handler execution latency. | Requires timestamping and careful allocation-free implementation. | High |
| Capture `sar` / `perf` during peak load | Separates user CPU, system CPU, softirq, context switches, and I/O wait. | Profiling overhead and extra log volume. | High |
| Capture `tcpdump` during burst windows | Confirms packet rate, resets, retransmissions, and client close behavior. | High disk/network capture overhead. | High |
| Add socket lifecycle tracing for `CLOSE_WAIT` | Determines whether close lag is expected under burst disconnects or a lifecycle bug. | Requires event correlation per connection. | High |
| Capture `dotnet-gcdump` near peak heap | Identifies retained object categories and validates memory-pressure hypotheses. | Can pause or perturb the process. | Medium |
| Export per-pool object health metrics | Identifies which object pools caused `UnhealthyPoolCount = 6`. | Larger telemetry output. | High |
| Add handle type classification | Distinguishes socket handles, file handles, timers, and runtime handles. | Requires platform-specific tooling. | Medium |
| Tune backlog / `somaxconn` only after kernel queue capture | May reduce SYN/accept drops if OS queue is limiting. | Larger queues can hide overload and increase memory pressure. | Medium |
| Tune pool capacities only after per-pool evidence | May reduce object misses and unhealthy pool events. | Higher steady-state memory footprint. | Medium |
