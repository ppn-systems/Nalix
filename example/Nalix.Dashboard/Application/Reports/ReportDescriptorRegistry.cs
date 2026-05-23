using Nalix.Observability.Contracts;

namespace Nalix.Dashboard.Application.Reports;

internal sealed class ReportDescriptorRegistry
{
    private static readonly ReportDescriptor[] s_descriptors =
    [
        new(RuntimeObservationTarget.DISPATCH,         "/metrics/dispatch",        "Dispatch",         "Packet dispatch channel throughput and connection queues.",          3_000, 500, 60_000, true, true),
        new(RuntimeObservationTarget.TASKS,            "/metrics/tasks",           "Tasks",            "Task manager workers, recurring jobs, and process health.",           5_000, 1_000, 60_000, true, true),
        new(RuntimeObservationTarget.BUFFERS,          "/metrics/buffers",         "Buffers",          "Buffer pool hit rates, pool sizes, and memory usage.",               5_000, 1_000, 60_000, true, true),
        new(RuntimeObservationTarget.CONNECTIONS,      "/metrics/connections",     "Connections",      "Active connections, shards, and bytes transferred.",                  3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.INSTANCES,        "/metrics/instances",       "Instances",        "DI instance cache hits, creation counts, and registered types.",      10_000, 2_000, 60_000, true, false),
        new(RuntimeObservationTarget.OBJECT_POOLS,     "/metrics/object-pools",    "Object Pools",     "Object pool health, hit rates, and unhealthy pool alerts.",           5_000, 1_000, 60_000, true, true),
        new(RuntimeObservationTarget.CONNECTION_GUARD, "/metrics/connection-guard","Connection Guard",  "Rate-limiting endpoint tracking, rejections, and top talkers.",       5_000, 1_000, 60_000, true, false),
        new(RuntimeObservationTarget.LISTENER,         "/metrics/listeners",       "Listeners",        "Active listeners and transport binding configurations.",             3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.PROTOCOL,         "/metrics/protocols",       "Protocols",        "Protocol state machine and network traffic message metrics.",         3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.SESSIONS,         "/metrics/sessions",        "Sessions",         "Session store metrics, hit rates, and client attribute persistence.", 3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.CONCURRENCY_GATE, "/metrics/concurrency-gate", "Concurrency Gate", "Active connection opcodes, capacity limits, and queuing pressure.", 3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.POLICY_RATE_LIMITER, "/metrics/policy-rate-limiter", "Policy Rate Limiter", "Stateless policy-based token bucket evaluation engine.",       3_000, 500, 60_000, true, false),
        new(RuntimeObservationTarget.TOKEN_BUCKET_LIMITER, "/metrics/token-bucket-limiter", "Token Bucket Limiter", "Fine-grained connection IP endpoint rate-limiting and lockout.", 3_000, 500, 60_000, true, false),
    ];

    public IReadOnlyList<ReportDescriptor> All => s_descriptors;

    public ReportDescriptor? Get(RuntimeObservationTarget target)
    {
        foreach (ReportDescriptor d in s_descriptors)
        {
            if (d.Target == target)
            {
                return d;
            }
        }

        return null;
    }

    public ReportDescriptor? GetByRoute(string route)
    {
        foreach (ReportDescriptor d in s_descriptors)
        {
            if (string.Equals(d.Route, route, StringComparison.OrdinalIgnoreCase))
            {
                return d;
            }
        }

        return null;
    }

    public RuntimeObservationTarget[] AllTargets()
    {
        RuntimeObservationTarget[] targets = new RuntimeObservationTarget[s_descriptors.Length];
        for (int i = 0; i < s_descriptors.Length; i++)
        {
            targets[i] = s_descriptors[i].Target;
        }

        return targets;
    }
}
