// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace Nalix.LoadTester;

internal sealed class LoadTestOptions
{
    public bool UseProxyProtocol { get; private init; }

    public LoadTestScenarioKind Scenario { get; private init; } = LoadTestScenarioKind.Payload;

    public string Host { get; private init; } = "127.0.0.1";

    public ushort Port { get; private init; } = 57206;

    public int Connections { get; private init; } = 500;

    public int DurationSeconds { get; private init; } = 15;

    public int TimeoutMs { get; private init; } = 5000;

    public int PayloadSize { get; private init; } = 1500;

    public int ReportIntervalSeconds { get; private init; } = 1;

    public int SampleCapacity { get; private init; } = 10_000_000;

    public int StartConnections { get; private init; } = 1;

    public int RampUpSeconds { get; private init; }

    public int WarmupSeconds { get; private init; }

    public int CooldownSeconds { get; private init; }

    public string? OutputPath { get; private init; }

    public static bool TryParse(
        string[] args,
        out LoadTestOptions options,
        out string? error,
        out bool showHelp)
    {
        ArgumentNullException.ThrowIfNull(args);

        Builder builder = new();
        error = null;
        showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (StringComparer.Ordinal.Equals(arg, "--help") || StringComparer.Ordinal.Equals(arg, "-h"))
            {
                showHelp = true;
                options = builder.Build();
                return false;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Positional argument '{arg}' is not supported. Use named options such as --host 127.0.0.1.";
                options = builder.Build();
                return false;
            }

            string name;
            string value;
            int equalsIndex = arg.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex >= 0)
            {
                name = arg[..equalsIndex];
                value = arg[(equalsIndex + 1)..];
            }
            else
            {
                name = arg;

                if (StringComparer.OrdinalIgnoreCase.Equals(name, "--proxy-protocol"))
                {
                    value = "true";
                }
                else
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Option '{name}' requires a value.";
                        options = builder.Build();
                        return false;
                    }

                    value = args[++i];
                }
            }

            if (!builder.TrySet(name, value, out error))
            {
                options = builder.Build();
                return false;
            }
        }

        options = builder.Build();
        if (!options.Validate(out error))
        {
            return false;
        }

        return true;
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project tools/Nalix.LoadTester/Nalix.LoadTester.csproj -- --scenario payload --host 127.0.0.1 --port 57206");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --scenario ping|payload|ddos");
        writer.WriteLine("  --host <address>");
        writer.WriteLine("  --port <1-65535>");
        writer.WriteLine("  --connections <count>");
        writer.WriteLine("  --duration <seconds>");
        writer.WriteLine("  --timeout <milliseconds>");
        writer.WriteLine("  --payload-size <bytes>");
        writer.WriteLine("  --report-interval <seconds>");
        writer.WriteLine("  --sample-capacity <count>");
        writer.WriteLine("  --start-connections <count>");
        writer.WriteLine("  --ramp-up <seconds>");
        writer.WriteLine("  --warmup <seconds>");
        writer.WriteLine("  --cooldown <seconds>");
        writer.WriteLine("  --output <report.json|report.csv|report.md>");
        writer.WriteLine("  --proxy-protocol (Enable Proxy Protocol V2 injection)");
    }

    private bool Validate(out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(this.Host))
        {
            error = "--host cannot be empty.";
            return false;
        }

        if (this.Connections <= 0)
        {
            error = "--connections must be greater than 0.";
            return false;
        }

        if (this.DurationSeconds <= 0)
        {
            error = "--duration must be greater than 0.";
            return false;
        }

        if (this.TimeoutMs <= 0)
        {
            error = "--timeout must be greater than 0.";
            return false;
        }

        if (this.PayloadSize < 0)
        {
            error = "--payload-size cannot be negative.";
            return false;
        }

        if (this.ReportIntervalSeconds <= 0)
        {
            error = "--report-interval must be greater than 0.";
            return false;
        }

        if (this.SampleCapacity <= 0)
        {
            error = "--sample-capacity must be greater than 0.";
            return false;
        }

        if (this.StartConnections <= 0)
        {
            error = "--start-connections must be greater than 0.";
            return false;
        }

        if (this.StartConnections > this.Connections)
        {
            error = "--start-connections cannot be greater than --connections.";
            return false;
        }

        if (this.RampUpSeconds < 0)
        {
            error = "--ramp-up cannot be negative.";
            return false;
        }

        if (this.WarmupSeconds < 0)
        {
            error = "--warmup cannot be negative.";
            return false;
        }

        if (this.CooldownSeconds < 0)
        {
            error = "--cooldown cannot be negative.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(this.OutputPath))
        {
            string extension = Path.GetExtension(this.OutputPath);
            if (!StringComparer.OrdinalIgnoreCase.Equals(extension, ".json") &&
                !StringComparer.OrdinalIgnoreCase.Equals(extension, ".csv") &&
                !StringComparer.OrdinalIgnoreCase.Equals(extension, ".md"))
            {
                error = "--output must end with .json, .csv, or .md.";
                return false;
            }
        }

        return true;
    }

    private sealed class Builder
    {
        private LoadTestScenarioKind _scenario = LoadTestScenarioKind.Payload;
        private string _host = "127.0.0.1";
        private ushort _port = 57206;
        private int _connections = 500;
        private int _durationSeconds = 15;
        private int _timeoutMs = 5000;
        private int _payloadSize = 1500;
        private int _reportIntervalSeconds = 1;
        private int _sampleCapacity = 10_000_000;
        private int _startConnections = 1;
        private int _rampUpSeconds;
        private int _warmupSeconds;
        private int _cooldownSeconds;
        private string? _outputPath;
        private bool _useProxyProtocol;

        public LoadTestOptions Build() => new()
        {
            UseProxyProtocol = _useProxyProtocol,
            Scenario = _scenario,
            Host = _host,
            Port = _port,
            Connections = _connections,
            DurationSeconds = _durationSeconds,
            TimeoutMs = _timeoutMs,
            PayloadSize = _payloadSize,
            ReportIntervalSeconds = _reportIntervalSeconds,
            SampleCapacity = _sampleCapacity,
            StartConnections = _startConnections,
            RampUpSeconds = _rampUpSeconds,
            WarmupSeconds = _warmupSeconds,
            CooldownSeconds = _cooldownSeconds,
            OutputPath = _outputPath
        };

        public bool TrySet(string name, string value, out string? error)
        {
            error = null;

            switch (name)
            {
                case "--scenario":
                    if (StringComparer.OrdinalIgnoreCase.Equals(value, "ping"))
                    {
                        _scenario = LoadTestScenarioKind.Ping;
                        return true;
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(value, "payload"))
                    {
                        _scenario = LoadTestScenarioKind.Payload;
                        return true;
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(value, "ddos"))
                    {
                        _scenario = LoadTestScenarioKind.DdosControl;
                        return true;
                    }

                    error = "--scenario must be 'ping', 'payload', or 'ddos'.";
                    return false;

                case "--host":
                    _host = value;
                    return true;

                case "--port":
                    return TryParseUInt16(value, name, out _port, out error);

                case "--connections":
                    return TryParseInt32(value, name, out _connections, out error);

                case "--duration":
                    return TryParseInt32(value, name, out _durationSeconds, out error);

                case "--timeout":
                    return TryParseInt32(value, name, out _timeoutMs, out error);

                case "--payload-size":
                    return TryParseInt32(value, name, out _payloadSize, out error);

                case "--report-interval":
                    return TryParseInt32(value, name, out _reportIntervalSeconds, out error);

                case "--sample-capacity":
                    return TryParseInt32(value, name, out _sampleCapacity, out error);

                case "--start-connections":
                    return TryParseInt32(value, name, out _startConnections, out error);

                case "--ramp-up":
                    return TryParseInt32(value, name, out _rampUpSeconds, out error);

                case "--warmup":
                    return TryParseInt32(value, name, out _warmupSeconds, out error);

                case "--cooldown":
                    return TryParseInt32(value, name, out _cooldownSeconds, out error);

                case "--output":
                    _outputPath = value;
                    return true;

                case "--proxy-protocol":
                    _useProxyProtocol = true;
                    return true;

                default:
                    error = $"Unknown option '{name}'.";
                    return false;
            }
        }

        private static bool TryParseInt32(string value, string name, out int result, out string? error)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                error = null;
                return true;
            }

            error = $"Option '{name}' expects an integer value.";
            return false;
        }

        private static bool TryParseUInt16(string value, string name, out ushort result, out string? error)
        {
            if (ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0)
            {
                error = null;
                return true;
            }

            error = $"Option '{name}' expects a port between 1 and 65535.";
            return false;
        }
    }
}
