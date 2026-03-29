// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using Nalix.Framework.Injection;

namespace Nalix.Benchmark.Framework.Injection;

// ---------------------------------------------------------------------------
// Dummy types — chỉ dùng cho benchmark, tránh ảnh hưởng cache production
// ---------------------------------------------------------------------------

public sealed class BenchServiceA { }
public sealed class BenchServiceB { }
public sealed class BenchServiceC { }

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "<Pending>")]
public interface IBenchService { }
public sealed class BenchServiceWithIface : IBenchService { }

public sealed class BenchServiceWithArgs(int value, string name)
{
    public int Value { get; } = value;
    public string Name { get; } = name;
}

// ---------------------------------------------------------------------------
// Benchmark class
// ---------------------------------------------------------------------------

/// <summary>
/// Đo hiệu năng 3 operations chính của <see cref="InstanceManager"/>:
/// <list type="bullet">
///   <item>Register — replace existing, with/without interfaces</item>
///   <item>GetOrCreateInstance — generic slot hit, dict hit, signature cache hit</item>
///   <item>GetExistingInstance — generic slot hit, thread-local L1, dict fallback</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "<Pending>")]
public class InstanceManagerBenchmarks
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private InstanceManager _manager = null!;

    // Pre-allocated args — tránh tạo array mới trong vòng lặp benchmark
    private static readonly object[] s_ctorArgs = [42, "bench"];

    // -----------------------------------------------------------------------
    // Params
    // -----------------------------------------------------------------------

    /// <summary>
    /// Số lượng instance "nền" nạp sẵn vào cache.
    /// Giúp quan sát ảnh hưởng của dictionary size lên lookup performance.
    /// </summary>
    [Params(10, 50)]
    public int PreloadCount { get; set; }

    // -----------------------------------------------------------------------
    // Setup / Cleanup
    // -----------------------------------------------------------------------

    [GlobalSetup]
    public void Setup()
    {
        _manager = new InstanceManager();

        // Register các type cốt lõi mà benchmark sẽ dùng
        _manager.Register(new BenchServiceA());
        _manager.Register(new BenchServiceB());
        _manager.Register(new BenchServiceC());
        _manager.Register(new BenchServiceWithIface());

        // Warm up signature cache cho BenchServiceWithArgs
        _ = _manager.GetOrCreateInstance<BenchServiceWithArgs>(s_ctorArgs);

        // Nạp thêm instances "nền" để đạt PreloadCount mong muốn.
        // Mỗi i tạo ra ActivatorKey khác nhau → mô phỏng dictionary đông entries.
        for (int i = 4; i < this.PreloadCount; i++)
        {
            _ = _manager.GetOrCreateInstance<BenchServiceWithArgs>();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _manager.Dispose();

    // Đảm bảo instances tồn tại trước mỗi iteration của nhóm Register
    [IterationSetup(Targets =
    [
        nameof(Register_Replace),
        nameof(Register_WithInterfaces),
        nameof(Register_ClassOnly)
    ])]
    public void EnsureRegistered()
    {
        _manager.Register(new BenchServiceA());
        _manager.Register(new BenchServiceWithIface(), registerInterfaces: true);
        _manager.Register(new BenchServiceB());
    }

    // -----------------------------------------------------------------------
    // GROUP 1 — Register<T>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Chi phí replace instance đã có trong cache.
    /// Path: TryUpdate ConcurrentDictionary + Volatile.Write generic slot
    ///       + dispose instance cũ nếu IDisposable.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — replace existing")]
    public void Register_Replace()
        => _manager.Register(new BenchServiceA());

    /// <summary>
    /// Overhead khi register thêm interface slots.
    /// Path: replace concrete type + loop GetInterfaces()
    ///       + reflection PUBLISH_TO_INTERFACE_SLOT per interface.
    /// Kỳ vọng: chậm hơn Register_Replace vì có thêm reflection.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — with interface slots")]
    public void Register_WithInterfaces()
        => _manager.Register(new BenchServiceWithIface(), registerInterfaces: true);

    /// <summary>
    /// Register bỏ qua tất cả interfaces — baseline rẻ nhất.
    /// Chỉ update 1 dict entry + 1 Volatile.Write slot.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — class only (no interfaces)")]
    public void Register_ClassOnly()
        => _manager.Register(new BenchServiceB(), registerInterfaces: false);

    // -----------------------------------------------------------------------
    // GROUP 2 — GetOrCreateInstance<T>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fast-path tốt nhất: generic slot (Volatile.Read một static field).
    /// Không có dictionary lookup, không lock.
    /// Kỳ vọng: dưới 5 ns, zero allocation.
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate<T> — generic slot hit (fastest path)")]
    public BenchServiceA? GetOrCreate_GenericSlotHit()
        => _manager.GetOrCreateInstance<BenchServiceA>();

    /// <summary>
    /// Dict lookup bằng RuntimeTypeHandle, không có args.
    /// Path: TryGetValue ConcurrentDictionary → TRY_PUBLISH_SLOT_BY_TYPE (reflection).
    /// Chậm hơn generic slot vì qua non-generic overload + reflection publish.
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate(Type) — dict hit, no args")]
    public object GetOrCreate_DictHit_NoArgs()
        => _manager.GetOrCreateInstance<BenchServiceC>();

    /// <summary>
    /// Signature cache hit: key = ActivatorKey struct (Target + P0 + P1 + Arity).
    /// Path: tính HashCode ActivatorKey → TryGetValue _signatureInstanceCache.
    /// Kỳ vọng: chậm hơn dict hit vì ActivatorKey.GetHashCode phức tạp hơn.
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate(Type, args) — signature cache hit")]
    public object GetOrCreate_SignatureCacheHit()
        => _manager.GetOrCreateInstance<BenchServiceWithArgs>(s_ctorArgs);

    // -----------------------------------------------------------------------
    // GROUP 3 — GetExistingInstance<T>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Layer 1 — generic slot (Volatile.Read): nhanh nhất, không lock.
    /// BenchServiceA luôn được register nên slot luôn có giá trị.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — generic slot hit")]
    public BenchServiceA? GetExisting_SlotHit()
        => _manager.GetExistingInstance<BenchServiceA>();

    /// <summary>
    /// Layer 2 — thread-local L1 (s_tsLastKey / s_tsLastValue).
    /// BenchServiceB được gọi liên tiếp → L1 hit sau lần đầu tiên.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — thread-local L1 hit")]
    public BenchServiceB? GetExisting_ThreadLocalHit()
        => _manager.GetExistingInstance<BenchServiceB>();

    /// <summary>
    /// Layer 3 — ConcurrentDictionary fallback.
    /// BenchServiceC xen kẽ giữa A và B → ép s_tsLastKey miss → đi vào dict.
    /// Path: TryGetValue + Volatile.Write slot + Interlocked.Increment hit counter.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — dict fallback")]
    public BenchServiceC? GetExisting_DictFallback()
        => _manager.GetExistingInstance<BenchServiceC>();
}
