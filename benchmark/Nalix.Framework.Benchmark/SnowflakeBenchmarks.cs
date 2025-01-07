// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Quick benchmarks for Nalix.Framework.Identity.Snowflake
//
// XML docs and inline comments follow Microsoft standards and are written in English.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nalix.Common.Enums;
using Nalix.Common.Primitives;
using Nalix.Framework.Identity;

namespace Nalix.Framework.Benchmarks
{
    /// <summary>
    /// Quick benchmarks for common Snowflake operations.
    /// This class is configured to run with a short job to keep execution time low.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 1, iterationCount: 1)]
    public class SnowflakeBenchmarks
    {
        private Snowflake _snowflake;
        private System.Byte[] _buffer;
        private UInt56 _uInt56;

        /// <summary>
        /// Global setup executed once before the benchmarks.
        /// Prepare a Snowflake instance and buffers.
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            // Use default type value (0) to avoid depending on specific enum member names.
            _snowflake = Snowflake.NewId(SnowflakeType.Unknown);
            _uInt56 = _snowflake.ToUInt56();
            _buffer = new System.Byte[7];
            _ = _snowflake.TryWriteBytes(_buffer, out _);
        }

        [Benchmark(Description = "NewId(type)")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public Snowflake NewId_WithType()
        {
            // Instance method (required by BenchmarkDotNet).
            return Snowflake.NewId(SnowflakeType.Unknown);
        }

        [Benchmark(Description = "NewId(value,machine,type)")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public Snowflake NewId_WithValueMachineType()
        {
            return Snowflake.NewId(123456u, 42, 0);
        }

        [Benchmark(Description = "NewId(from UInt56)")]
        public Snowflake NewId_FromUInt56()
        {
            return Snowflake.NewId(_uInt56);
        }

        [Benchmark(Description = "ToString() hex")]
        public System.String ToString_Hex()
        {
            return _snowflake.ToString();
        }

        [Benchmark(Description = "ToUInt56()")]
        public UInt56 ToUInt56()
        {
            return _snowflake.ToUInt56();
        }

        [Benchmark(Description = "FromBytes(ReadOnlySpan<byte>)")]
        public Snowflake FromBytes()
        {
            return Snowflake.FromBytes(_buffer);
        }

        [Benchmark(Description = "TryWriteBytes(Span<byte>, out int) - stackalloc")]
        public System.Boolean TryWriteBytes_WithOut()
        {
            System.Span<System.Byte> dest = stackalloc System.Byte[7];
            return _snowflake.TryWriteBytes(dest, out _);
        }

        [Benchmark(Description = "TryWriteBytes(alloc) - heap alloc")]
        public System.Boolean TryWriteBytes_Alloc()
        {
            // small allocation per invocation; useful to measure alloc cost
            System.Byte[] dest = new System.Byte[7];
            return _snowflake.TryWriteBytes(dest, out _);
        }

        [Benchmark(Description = "Equals(self)")]
        public System.Boolean Equals_Self()
        {
            return _snowflake.Equals(_snowflake);
        }

        [Benchmark(Description = "CompareTo(self)")]
        public System.Int32 CompareTo_Self()
        {
            return _snowflake.CompareTo(_snowflake);
        }
    }
}