# NALIX CORE RASPBERRY PI 5: LARGE PAYLOAD (>1.4KB MTU) STRESS TEST REPORT

> **Comprehensive Analysis of System Performance under Heavy Network Load with Large Packets (>1400 Bytes)**

---

## 1. Executive Summary & Test Context

This telemetry report details the performance of the **Nalix Core** framework on a physical Raspberry Pi 5 under a distinctly different stress profile compared to previous benchmarks. 

**Key Difference:** While prior tests utilized small ~32-byte payloads (the internal `Control` packet), this test forces the framework to handle payloads **exceeding 1.4 KB (1400+ bytes)**. This payload size approaches and exceeds the standard Ethernet Maximum Transmission Unit (MTU) of 1500 bytes. 

Processing large payloads shifts the bottleneck from sheer packet parsing speed to **memory bandwidth, buffer allocation, and kernel-level network interrupt handling**. Nalix internally fragments payloads larger than the configured frame size (~1400 bytes) into multiple application-layer frames before transport transmission. Additional segmentation may still occur at the TCP or kernel networking layers depending on MSS, socket buffering, and network stack behavior.

---

## 2. System Resource Telemetry (Pi 5 Host)

Resource data was gathered at 1-second intervals using `sysstat` (`sar`) during the active stress testing period. The larger payload size fundamentally altered the CPU profile.

### 2.1. CPU Utilization Profiling

```plaintext
Average CPU Metrics (Peak Load Window):
CPU     %user     %nice   %system   %iowait    %steal     %idle
all      0.00     46.60     48.87      0.00      0.00      4.53
all      0.00     45.71     48.23      0.00      0.00      6.06
```

```mermaid
pie title CPU Utilization Breakdown (Large Payload Stress)
    "OS Kernel / Networking (%system)" : 48.87
    "User-level Tasks (%nice)" : 46.60
    "Idle CPU (Available Headroom)" : 4.53
```

> [!WARNING]
> **Kernel Overload:** The `%system` CPU utilization spiked to nearly **49%** (compared to ~24% in the small-packet benchmark). This massive increase appears to be caused by the Linux kernel processing large Ethernet frames, requiring heavy memory copying (sk_buff) and TCP/IP stack overhead. Idle headroom collapsed to barely **4.5%**.

### 2.2. Physical Network Throughput (`eth0`)

```plaintext
Interface      rxpck/s      txpck/s       rxkB/s       txkB/s   %ifutil
eth0          51405.00     64033.00     45390.82     45556.51     37.32
eth0          51415.00     61001.00     41299.54     43905.28     35.97
```

* **Ingress Throughput:** ~45.3 MB/s (362 Mbps) at ~51,400 packets/sec.
* **Egress Throughput:** ~45.5 MB/s (364 Mbps) at ~64,000 packets/sec.
* **Observed behavior:** Despite processing fewer total packets per second than the small-packet test, the total bandwidth pushed through the Pi 5 remained extremely high (~90+ MB/s combined), maximizing the memory bus.

---

## 3. Nalix Core Memory & Buffer Pool Adjustments

Handling packets > 1.4 KB appears to bypass the smaller buffer pools.

### 3.1. Pinned Slab Allocator Shift

In the small packet test, the `256 B` and `1024 B` buffer pools handled almost all traffic. In this >1.4 KB test, the `BufferPoolManager` is forced to allocate from the **4,096 B Pool** (or larger) for every single network receive event.

```plaintext
Buffer Slabs Allocation Shift (Simulated for >1.4KB Payloads):
--------------------------------------------------------------------------------------
- 256 B Pool  : [ BYPASSED ] 
- 1,024 B Pool: [ BYPASSED ] 
- 4,096 B Pool: [ HEAVY HIT RATE ] -> Core buffer for 1.4KB+ payloads
- 16,384B Pool: [ MODERATE HIT RATE ] -> Used for application-layer frame reassembly
--------------------------------------------------------------------------------------
```

```mermaid
gantt
    title Buffer Utilization Profile Shift
    dateFormat  X
    axisFormat %s
    section Active Slabs (Small Packets)
    256B & 1024B Pools (High Hit Rate) : 0, 50
    section Active Slabs (>1.4K Packets)
    4096B Pool (Massive Hit Rate) : 50, 100
```

### 3.2. Object Pool Distress & Trimming Fallouts

**Observed behavior:** The dashbaord snapshots revealed severe degradation in Object Pool hit rates.

<details>
<summary><b>View Dashboard Snapshots</b></summary>

![Task Scheduler and Memory Utilization](./img/task_scheduler_memory_utilization.png)

![Pinned Buffer Pool (Slab Allocator)](./img/pinned_buffer_pool_allocator.png)

![TCP/WebSocket Connections and Throughput](./img/tcp_ws_connections_throughput.png)

![Lock-Free Object Pool Manager Statistics](./img/object_pool_manager_statistics.png)

</details>

> [!NOTE]
> **Dashboard Egress Discrepancy:** In the *TCP/WebSocket Connections and Throughput* snapshot above, the "Bytes Sent" metric appears anomalously low. This visual discrepancy was caused by a metrics-tracking logic bug in the framework (which has since been patched). The actual physical egress throughput was ~45.5 MB/s, as measured directly at the network interface layer via `sar` (see Section 2.2).

| Object Pool Type | Hit Rate | Status | Analysis |
| :--- | :---: | :---: | :--- |
| **PooledSocketAsyncEventArgs** | **50.4%** | `Fail (1)` | Massive drop. The system is struggling to recycle these fast enough. |
| **ConnectionEventArgs** | **0.7%** | `Fail (1)` | Almost 100% cache miss rate. |
| **PooledConnectEventContext** | **0.2%** | `Fail (1)` | Almost 100% cache miss rate. |
| **TimeoutTask** | **3.2%** | `Fail (1)` | Rapid creation and destruction bypassing pool. |

> [!CAUTION]
> **Why are pools failing?** 
> **Hypothesis:** The dashboard shows that Trimming Logic has not executed at all (0 trims), which is expected since the background trim job runs every 5 minutes by default and this was a short stress test. Therefore, the pools did not fail due to aggressive trimming. Instead, the extreme CPU contention (95%+ active load) and ~3x slower dispatch times caused worker threads to hold onto objects much longer than usual. Because objects were not returned to the pool fast enough to meet incoming demand, the pools were quickly exhausted, forcing the framework to allocate new objects continuously (cache misses).

---

## 4. Dispatch Pipeline & Middleware Latency

The large payload size dramatically increased execution latency across the entire pipeline.

### 4.1. Latency Tripling

> [!WARNING]
> **Middleware Execution Bypass:** In these tests, the benchmark packets did not attach the required middleware attributes, causing the dispatcher to skip the actual middleware logic. Therefore, the "middleware execution" times reported here are actually measuring the *bypass overhead* plus the severe queueing latency caused by thread contention.

**Observed behavior:** In prior tests, middleware bypass overhead was ~2.1 ms. With >1.4KB payloads, timings for skipping middleware increased to **6.4 - 6.9 ms**.

```mermaid
xychart-beta
    title "Middleware Latency (Skipped/Bypassed): Small (<100B) vs Large (>1.4KB)"
    x-axis ["Timeout", "PacketTag", "RateLimit", "Permission", "Concurrency"]
    y-axis "Latency (ms)" 0 --> 8
    line [2.1, 2.3, 2.2, 2.1, 2.3]
    line [6.4, 6.3, 6.8, 6.9, 6.4]
```

### 4.2. Root Cause Analysis

**Hypothesis:**
1. **Serialization / Deserialization Overhead:** Copying and parsing 1400+ bytes takes linearly longer than parsing 32 bytes.
2. **Thread Contention:** Because each packet takes ~3x longer to process, the worker threads (`net/tcp`) hold onto the CPU longer.
3. **Queue Starvation:** Packets spend significantly more time waiting in the `InlinePacketDispatcher` queues because the worker threads are tied up copying large byte arrays and waiting on the kernel (`%system` CPU).

---

## 5. Conclusion & Optimization Path

Handling payloads over the standard MTU (>1.4 KB) on a Raspberry Pi 5 appears to expose different bottlenecks than sheer packet volume tests:

**Observed behavior:**
1. **The workload appears to be dominated by kernel networking overhead and memory-copy pressure while also exposing framework-level bottlenecks such as object-pool trimming behavior.** The 49% `%system` CPU indicates the Linux kernel is working at maximum capacity to handle network interrupts and sk_buff copies for large frames.

**Hypothesis:**
2. **Object Pool Capacities are undersized for high-latency payloads.** The `Fail (1)` cascade across `PooledSocketAsyncEventArgs` and connection objects is not caused by trimming (which didn't run during the short test), but by the increased latency of processing 1.4KB payloads. Because objects are held in the pipeline longer, the pool's maximum capacity is exhausted faster. The default `MaxCapacity` for these critical pools must be increased when handling large MTU workloads.
3. **Buffer Pool Resizing.** The 4,096 B slab pool should be pre-allocated with a higher maximum capacity when the application expects MTU-sized payloads to prevent falling back to dynamic allocation.
