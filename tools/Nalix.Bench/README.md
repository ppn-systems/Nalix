# ![Icon](https://raw.githubusercontent.com/ppn-systems/Nalix/refs/heads/master/docs/assets/nalix.ico) **Nalix Bench**

![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet?logo=dotnet&logoColor=white)
![Mode](https://img.shields.io/badge/Benchmarks-Latency%20|%20Throughput%20|%20Stress-blue)

**Nalix.Bench** is an industrial-grade benchmarking utility designed to stress-test the Nalix network stack. It provides high-precision measurements for latency stability (jitter), connection handshake throughput, and raw data transfer capacity.

## 🚀 Getting Started

To run the benchmark tool:

1.  **Navigate** to the benchmark directory:
    ```bash
    cd tools/Nalix.Bench
    ```
2.  **Run** a basic latency test:
    ```bash
    dotnet run -c Release -- --sessions 100 --count 1000
    ```

---

## 🏗️ Benchmarking Modes

The tool supports three primary modes of operation via the `-m` or `--mode` flag:

### 1. `ping` (Default)
Measures the **Round-Trip Time (RTT)** latency of small control packets.
- **Metrics**: Average latency, Min/Max, Standard Deviation (Jitter), P50/P99/P99.99 percentiles.
- **Use Case**: Testing network stability and tail-latency outliers.

### 2. `handshake`
Measures **Handshakes Per Second (HPS)** by rapidly churning connections.
- **Logic**: Connect -> Handshake -> Disconnect -> Repeat.
- **Metrics**: Total Successful/Failed handshakes, HPS, Average time per handshake.
- **Use Case**: Testing server-side authentication performance and `TcpListener` accept-loop efficiency.
- **Note**: Requires the `--key` argument if the server enforces authenticated handshakes.

### 3. `throughput`
Measures **Data Transfer Speed** by pushing raw payloads.
- **Logic**: Continuous asynchronous push of data packets.
- **Metrics**: Packets Per Second (PPS), Megabytes Per Second (MB/s).
- **Use Case**: Testing the maximum bandwidth and memory management (BufferPool) effectiveness.

---

## 🔧 CLI Options

| Option | Short | Default | Description |
| :--- | :--- | :--- | :--- |
| `--mode` | `-m` | `ping` | Benchmark mode: `ping`, `handshake`, or `throughput`. |
| `--host` | `-h` | `127.0.0.1` | The target server IP or hostname. |
| `--port` | `-p` | `57206` | The target server port. |
| `--sessions` | `-s` | `100` | Number of concurrent transport sessions. |
| `--count` | `-n` | `1000` | Iterations per session (pings, handshakes, or packets). |
| `--timeout` | `-t` | `5000` | Timeout per operation in milliseconds. |
| `--payload` | `-b` | `1024` | Payload size in bytes (only for `throughput` mode). |
| `--warmup` | | `200` | Number of initial iterations to stabilize the environment. |
| `--key` | `-k` | | Server Public Key (Hex) for authenticated handshakes. |

---

## 💡 Usage Examples

#### Run a high-concurrency latency test:
```bash
dotnet run -c Release -- -m ping -s 500 -n 10000 --warmup 1000
```

#### Stress-test the server handshake limit:
```bash
dotnet run -c Release -- -m handshake -s 50 -n 100 -k 4A8B...2E1F
```

#### Measure maximum throughput with 8KB payloads:
```bash
dotnet run -c Release -- -m throughput -s 20 -n 50000 -b 8192
```

---

## 📊 Interpreting Results

- **Standard Deviation (Jitter)**: A low "Std Dev" indicates a stable connection. High values suggest network interference or GC pauses on the server.
- **P99.99 Latency**: This is the "tail latency." It represents the absolute worst-case scenario. Industrial-grade systems aim to keep this value closer to the average.
- **Success Rate**: If this is below 100%, check for server-side errors, socket exhaustion, or insufficient timeouts.

---
<p align="center">
  Built for high-performance distributed systems. 🚀
</p>
