# Nalix.LoadTester

Nalix.LoadTester is a high-performance load testing tool built to benchmark and stress test Nalix servers under extreme concurrency. It supports multiple workload scenarios (e.g. Ping, Payload Echo, DDoS Control) and outputs granular latency percentiles (P50, P95, P99, P99.9) and RPS (Throughput).

## Build & Native Execution (Recommended)

For peak benchmarking accuracy, it is highly recommended to compile and publish the tool to run it directly as a native executable (`.exe`). Running via `dotnet run` introduces JIT, runtime, and shell overhead which can skew high-concurrency results.

### 1. Build and Publish

Publish the project in `Release` configuration for your target operating system:

```powershell
# Build and publish as a self-contained folder
dotnet publish tools\Nalix.LoadTester\Nalix.LoadTester.csproj -c Release -o .\LoadTesterBin
```

This will compile and optimize the tool and save the output in the `.\LoadTesterBin` directory.

### 2. Run Directly using the EXE

Once compiled, run the executable directly from the command line:

```powershell
# For Windows
.\LoadTesterBin\Nalix.LoadTester.exe --scenario payload --host 127.0.0.1 --port 57206 --connections 1000 --duration 60

# For Linux / macOS (adjust publish command RID if cross-compiling)
./LoadTesterBin/Nalix.LoadTester --scenario payload --host 127.0.0.1 --port 57206 --connections 1000 --duration 60
```

---

## Configuration Options

| Option | Description | Default |
| :--- | :--- | :--- |
| `--scenario` | Workload type: `ping` \| `payload` \| `ddos` | `payload` |
| `--host` | Target host IP or domain name | `127.0.0.1` |
| `--port` | Target host port | `57206` |
| `--connections`| Peak number of concurrent clients | `500` |
| `--duration` | Steady-state measurement duration in seconds | `15` |
| `--timeout` | Request timeout in milliseconds | `5000` |
| `--payload-size`| Packet payload size in bytes (for `payload` scenario) | `1500` |
| `--ramp-up` | Time in seconds to scale from start to peak clients | `0` (immediate) |
| `--warmup` | Time in seconds to run warm-up loop before measurement | `0` |
| `--cooldown` | Time in seconds to scale down and close connections | `0` |
| `--proxy-protocol` | Set to true to inject a Proxy Protocol V2 header with spoofed IP per connection | `false` |
| `--output` | Save results as a report (`.json`, `.csv`, or `.md`) | None |

---

## Examples

### 1. Ping RTT Test (Measures RTT using SYSTEM_CONTROL PING/PONG)
```powershell
.\LoadTesterBin\Nalix.LoadTester.exe --scenario ping --host 127.0.0.1 --port 57206 --connections 100 --duration 30 --warmup 2
```

### 2. Payload Echo Stress Test (Sends and awaits BenchmarkPacket payload echo)
```powershell
.\LoadTesterBin\Nalix.LoadTester.exe --scenario payload --host 127.0.0.1 --port 57206 --connections 1000 --duration 60 --warmup 5 --ramp-up 2 --payload-size 1500
```

### 3. DDoS Control Flood Test (Fires non-awaited raw control packets)
```powershell
.\LoadTesterBin\Nalix.LoadTester.exe --scenario ddos --host 127.0.0.1 --port 57206 --connections 5000 --duration 30 --output reports/ddos_report.md
```

---

## Full End-to-End Benchmark Example

Follow these steps to run a native stress test from scratch:

### Step 1: Start the Backend Server
First, run the backend server in `Release` configuration so it is highly optimized:
```powershell
dotnet run --project example/Backend/Backend.csproj -c Release
```
*(Wait until you see `Started Nalix TCP server for protocol...` on the console)*

### Step 2: Build & Publish the LoadTester
In a separate terminal window, compile the LoadTester as a self-contained optimized folder:
```powershell
dotnet publish tools/Nalix.LoadTester/Nalix.LoadTester.csproj -c Release -o ./LoadTesterBin
```

### Step 3: Run the Load Tester natively
Run your chosen scenario using the native `.exe` file:
```powershell
.\LoadTesterBin\Nalix.LoadTester.exe --scenario payload --host 127.0.0.1 --port 57206 --connections 1000 --duration 30 --warmup 5 --ramp-up 3 --output reports/payload_benchmark.md
```

### Step 4: Analyze the Output
Once completed, the console will display the results, and the markdown report will be saved at `reports/payload_benchmark.md`. Open the report to analyze latency percentiles (P50, P95, P99, P99.9) and the final RPS throughput.
