# ⚖️ Nalix AI Skill — Benchmarking & Performance Validation

This skill defines the methodology for validating performance changes in the Nalix ecosystem using **BenchmarkDotNet**.

---

## 🏗️ Benchmark Structure

Nalix benchmarks are located in the `benchmarks/` directory and categorized by component:
- **`Nalix.Benchmark.Framework`**: Serialization and Dispatching.
- **`Nalix.Benchmark.Network`**: Transport and Hub operations.

---

## 📜 Key Metrics to Watch

1.  **Mean/Median Latency:** The primary goal is sub-microsecond latency for hot path operations.
2.  **Allocated (Gen 0/1/2):** For hot paths, this **MUST** be `0 B`. Any allocation triggers a performance regression.
3.  **Throughput (Ops/sec):** High-frequency operations like Opcode lookup or Packet decryption must scale linearly with CPU frequency.

---

## ⚡ Running Benchmarks

### Command Line:
```bash
dotnet run -c Release --project benchmarks/Nalix.Benchmark.Framework --filter "*"
```

### Configuration:
- **Job:** `ShortRun` for quick iteration, `LongRun` for final validation.
- **GC:** Use `Server GC` to simulate production environment.
- **Hardware:** Ensure a stable environment (no background apps, plugged into power).

---

## 🛠️ Common Benchmark Patterns

### 1. Memory Profiling
Always include the `[MemoryDiagnoser]` attribute on benchmark classes.

### 2. Micro-Optimization
Use `[Arguments]` to test different payload sizes or data distributions.

### 3. Baseline Comparison
Mark the current stable implementation with `Baseline = true` to see the relative speed of new optimizations.

---

## 🛡️ Common Pitfalls

- **Dead Code Elimination:** Ensure the benchmark result is used (e.g., return it from the method) to prevent the JIT from optimizing away the code you are trying to measure.
- **Warmup:** Networking components require sufficient warmup to fill pools and stabilize JIT compilation.
- **OS Interference:** Windows Task Scheduler or Antivirus can cause spikes. Run benchmarks multiple times and look for consistency.
