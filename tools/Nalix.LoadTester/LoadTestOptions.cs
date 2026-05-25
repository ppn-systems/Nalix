// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace Nalix.LoadTester;

internal sealed class LoadTestOptions
{
    public LoadTestScenarioKind Scenario { get; private init; } = LoadTestScenarioKind.Payload;

    public String Host { get; private init; } = "127.0.0.1";

    public UInt16 Port { get; private init; } = 57206;

    public Int32 Connections { get; private init; } = 500;

    public Int32 DurationSeconds { get; private init; } = 15;

    public Int32 TimeoutMs { get; private init; } = 5000;

    public Int32 PayloadSize { get; private init; } = 1500;

    public Int32 ReportIntervalSeconds { get; private init; } = 1;

    public Int32 SampleCapacity { get; private init; } = 10_000_000;

    public static Boolean TryParse(
        String[] args,
        out LoadTestOptions options,
        out String? error,
        out Boolean showHelp)
    {
        ArgumentNullException.ThrowIfNull(args);

        Builder builder = new();
        error = null;
        showHelp = false;

        for (Int32 i = 0; i < args.Length; i++)
        {
            String arg = args[i];
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

            String name;
            String value;
            Int32 equalsIndex = arg.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex >= 0)
            {
                name = arg[..equalsIndex];
                value = arg[(equalsIndex + 1)..];
            }
            else
            {
                name = arg;
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Option '{name}' requires a value.";
                    options = builder.Build();
                    return false;
                }

                value = args[++i];
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
        writer.WriteLine("  --scenario ping|payload");
        writer.WriteLine("  --host <address>");
        writer.WriteLine("  --port <1-65535>");
        writer.WriteLine("  --connections <count>");
        writer.WriteLine("  --duration <seconds>");
        writer.WriteLine("  --timeout <milliseconds>");
        writer.WriteLine("  --payload-size <bytes>");
        writer.WriteLine("  --report-interval <seconds>");
        writer.WriteLine("  --sample-capacity <count>");
    }

    private Boolean Validate(out String? error)
    {
        error = null;

        if (String.IsNullOrWhiteSpace(this.Host))
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

        return true;
    }

    private sealed class Builder
    {
        private LoadTestScenarioKind _scenario = LoadTestScenarioKind.Payload;
        private String _host = "127.0.0.1";
        private UInt16 _port = 57206;
        private Int32 _connections = 500;
        private Int32 _durationSeconds = 15;
        private Int32 _timeoutMs = 5000;
        private Int32 _payloadSize = 1500;
        private Int32 _reportIntervalSeconds = 1;
        private Int32 _sampleCapacity = 10_000_000;

        public LoadTestOptions Build() => new()
        {
            Scenario = _scenario,
            Host = _host,
            Port = _port,
            Connections = _connections,
            DurationSeconds = _durationSeconds,
            TimeoutMs = _timeoutMs,
            PayloadSize = _payloadSize,
            ReportIntervalSeconds = _reportIntervalSeconds,
            SampleCapacity = _sampleCapacity
        };

        public Boolean TrySet(String name, String value, out String? error)
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

                    error = "--scenario must be either 'ping' or 'payload'.";
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

                default:
                    error = $"Unknown option '{name}'.";
                    return false;
            }
        }

        private static Boolean TryParseInt32(String value, String name, out Int32 result, out String? error)
        {
            if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                error = null;
                return true;
            }

            error = $"Option '{name}' expects an integer value.";
            return false;
        }

        private static Boolean TryParseUInt16(String value, String name, out UInt16 result, out String? error)
        {
            if (UInt16.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result > 0)
            {
                error = null;
                return true;
            }

            error = $"Option '{name}' expects a port between 1 and 65535.";
            return false;
        }
    }
}
