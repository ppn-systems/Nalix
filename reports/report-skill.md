# Deep Technical Report Skill

## Purpose

This skill defines mandatory rules for generating highly detailed engineering,
benchmarking, telemetry, and performance analysis reports.

Target audience:

- Software engineers
- Performance engineers
- System architects
- Infrastructure engineers

Depth is preferred over brevity.

---

## Writing Rules

### Style

- Use formal engineering language.
- Write in a research-oriented style.
- Prioritize accuracy over readability.
- Avoid marketing language.
- Avoid exaggerated claims.
- Never use vague wording.

Forbidden examples:

- "Performance improved significantly"
- "Memory usage became better"
- "System behaved efficiently"

Preferred:

- "Average throughput increased from 30,284 RPS to 33,915 RPS."
- "Managed heap remained stable at 156 MB."

---

## Evidence Rules

Every statement must be categorized as either:

### Observed behavior

Contains:

- Direct measurements
- Logged data
- Dashboard metrics
- Captured telemetry
- Benchmark output

Example:

```text
Observed behavior:

- CPU %system increased from 24.2% to 48.9%.
- Average latency increased from 2.3 ms to 6.8 ms.
```

### Hypothesis

Contains:

- Possible explanations
- Root-cause assumptions
- Inferred causes

Example:

```text
Hypothesis:

- Increased kernel CPU utilization may be caused by sk_buff copy overhead.
```

Never present hypotheses as facts.

---

## Required Report Structure

# Report Title

> Short summary

---

## 1. Executive Summary & Test Context

Required:

- benchmark goals
- workload characteristics
- test purpose
- major findings
- comparison with previous runs

---

## 2. Test Environment & System Specifications

### Hardware

Required:

- CPU
- core count
- thread count
- cache sizes
- RAM
- storage
- network interface
- thermal status
- power status

### Software

Required:

- operating system
- kernel version
- runtime version
- build configuration
- runtime settings
- GC settings
- optimization settings

---

## 3. Test Configuration

Required:

- benchmark tool
- payload size
- protocol
- duration
- timeout values
- concurrent connections
- topology
- local vs remote execution

---

## 4. Raw Benchmark Results

Required:

- throughput
- averages
- minimum values
- maximum values
- failure rates
- P50
- P95
- P99
- P99.9

Every metric must include:

- technical impact
- explanation

---

## 5. System Resource Analysis

### CPU Analysis

Required:

- user
- system
- idle
- iowait

Explain:

- scheduler behavior
- kernel overhead
- thread behavior

---

### Memory Analysis

Required:

- baseline memory
- peak memory
- average memory
- managed heap
- working set

Explain:

- growth patterns
- memory stability
- allocation behavior

---

## 6. Internal Framework Telemetry

### Object Pool Analysis

Required:

- hit rates
- misses
- active objects
- leaks
- throughput

Explain:

- pool behavior
- bottlenecks

---

### Buffer Pool Analysis

Required:

- slab math
- allocations
- hit rates
- expansion events
- shrink events

Explain:

- memory impact

---

### Task Scheduling & GC

Required:

- workers
- active threads
- completed work
- heap size
- GC generations

Explain:

- scheduling implications
- GC implications

---

### Dispatch Pipeline

Required:

- execution counts
- queue latency
- execution latency
- contention effects

If middleware is bypassed:

```text
WARNING:
Middleware Execution Bypass
```

---

## Root Cause Analysis

Separate:

### Observed behavior

Measured facts only.

### Hypothesis

Possible causes only.

Never mix them.

---

## Scope & Limitations

Required:

- localhost limitations
- LAN limitations
- missing instrumentation
- environmental bias
- assumptions

---

## Conclusions & Optimization Paths

### Current Findings

Measured conclusions only.

### Potential Optimizations

For every optimization:

- expected impact
- tradeoffs

---

## Data Rules

Never:

- invent metrics
- invent percentages
- invent latency values
- invent throughput
- estimate missing values

If data is unavailable:

```text
Data not collected.
```

---

## Chart Rules

Allowed:

- Mermaid pie
- Mermaid xychart
- Mermaid gantt

Never generate charts using fabricated values.

---

## Terminology Rules

Always distinguish:

- Application latency
- Queue latency
- Kernel latency
- Network latency
- End-to-end latency

Never mix them.

---

## Engineering Thinking Rules

Always answer:

1. What happened?
2. Why?
3. What became the bottleneck?
4. What component saturated?
5. What should be optimized next?
