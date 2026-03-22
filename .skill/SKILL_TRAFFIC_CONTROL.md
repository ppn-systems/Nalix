# 🚦 Nalix AI Skill — Traffic Control & Rate Limiting

This skill covers the mechanisms Nalix uses to protect the server from flooding, DDoS attacks, and resource exhaustion via intelligent traffic shaping.

---

## 🛡️ The "Guard" System

Nalix uses "Guard" components to monitor and control traffic at different layers.

### `ConnectionGuard`
Monitors individual TCP connections.
- **Packet Thresholds:** Limits the number of packets per second (PPS) from a single connection.
- **Bandwidth Throttling:** Limits the total bytes per second (BPS) to prevent a single user from saturating the uplink.
- **Penalty System:** Connections that exceed limits are first throttled (delayed) and then disconnected if the behavior persists.

### `DatagramGuard`
Monitors UDP/Datagram traffic.
- **Source IP Tracking:** Tracks traffic per source IP to prevent amplification attacks.
- **Fragile Contexts:** Automatically drops packets from IPs that send malformed or replayed data.

---

## 📜 Algorithms

Nalix implements several industry-standard algorithms for traffic control:

1.  **Token Bucket:** Used for bandwidth throttling to allow for short bursts of traffic while maintaining a steady long-term rate.
2.  **Leaky Bucket:** Used for smoothing out packet delivery and ensuring constant PPS.
3.  **Sliding Window Counter:** Used for high-precision rate limiting over short intervals (e.g., 1-second windows).

---

## 🛤️ Middleware Integration

Traffic control is typically applied in the pipeline via `IPacketMiddleware<T>`.

- **Inbound Guard:** Checks if the connection/IP is within limits after deserialization but before handling.
- **Outbound Guard:** (Optional) Can be used to limit the rate of outbound responses to prevent egress cost spikes.

---

## ⚡ Performance Mandates

- **Lock-Free Counters:** Uses `Interlocked.Add` and atomic operations to track packet counts across multiple threads without locks.
- **Memory Efficiency:** Guards are pooled and reused to avoid allocation per connection.
- **Fast Rejection:** Malicious traffic is dropped as early as possible to minimize CPU waste.

---

## 🛡️ Common Pitfalls

- **Over-Throttling:** Setting limits too low can cause legitimate users to experience lag or disconnects. Use "Dry-Run" mode (logging only) to tune values.
- **Burst Handling:** Ensure your token bucket `BurstSize` is large enough to handle legitimate bursts (like initial state sync).
- **IP Spoofing:** Remember that for UDP, the source IP can be spoofed. Use cryptographic session tokens (Handshake) to verify identity before applying complex rate limits.
