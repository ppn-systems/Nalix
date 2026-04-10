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
    public int Value { get; } = value; public string Name { get; } = name;
}

// ---------------------------------------------------------------------------
// Benchmark class
// ---------------------------------------------------------------------------

/// <summary>
/// Đo hiệu năng <see cref="InstanceManager"/> trên 3 operations:
/// Register, GetOrCreateInstance, GetExistingInstance.
/// </summary>
/// <remarks>
/// QUAN TRỌNG: <see cref="InstanceManager"/> kế thừa <c>SingletonBase&lt;T&gt;</c>
/// nên chỉ tồn tại MỘT instance trong toàn bộ process.
/// Không được gọi <c>Dispose()</c> trong <c>GlobalCleanup</c> —
/// chỉ dùng <c>Clear(dispose: false)</c> để reset state giữa các param runs.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
public class InstanceManagerBenchmarks
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    // Dùng singleton instance — KHÔNG tạo mới bằng new InstanceManager()
    private InstanceManager _manager = null!;

    // Pre-allocated args để tránh allocation trong vòng benchmark
    private static readonly object?[] s_ctorArgs = [42, "bench"];

    // -----------------------------------------------------------------------
    // Params
    // -----------------------------------------------------------------------

    [Params(10, 50)]
    public int PreloadCount { get; set; }

    // -----------------------------------------------------------------------
    // Setup / Cleanup
    // -----------------------------------------------------------------------

    [GlobalSetup]
    public void Setup()
    {
        // Lấy singleton — không tạo instance mới
        _manager = InstanceManager.Instance;

        // Reset cache sạch từ run trước (nếu có), KHÔNG dispose
        _manager.Clear(dispose: false);

        // Register các type cốt lõi
        _manager.Register(new BenchServiceA());
        _manager.Register(new BenchServiceB());
        _manager.Register(new BenchServiceC());
        _manager.Register(new BenchServiceWithIface());

        // Register BenchServiceWithArgs để GetOrCreate_SignatureCacheHit có cache hit
        _manager.Register(new BenchServiceWithArgs(42, "bench"));

        // Nạp thêm instances "nền" để đạt PreloadCount
        for (int i = 4; i < this.PreloadCount; i++)
        {
            // RegisterForClassOnly để không spam interface slots
            _manager.Register(new BenchServiceWithArgs(i, $"preload_{i}"),
                              registerInterfaces: false);
        }
    }

    [GlobalCleanup]
    public void Cleanup() =>
        // Chỉ Clear, KHÔNG Dispose — tránh lỗi "Cannot access a disposed object"
        // khi BenchmarkDotNet gọi lại GlobalCleanup cho param tiếp theo
        _manager?.Clear(dispose: false);

    // IterationSetup cho nhóm Register: đảm bảo instances tồn tại trước mỗi lần đo
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
    /// Path: TryUpdate ConcurrentDictionary + Volatile.Write generic slot.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — replace existing")]
    public void Register_Replace()
        => _manager.Register(new BenchServiceA());

    /// <summary>
    /// Overhead khi register kèm interface slots.
    /// Path: replace concrete + loop GetInterfaces() + reflection PUBLISH_TO_INTERFACE_SLOT.
    /// Kỳ vọng: chậm hơn Register_Replace vì có reflection per interface.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — with interface slots")]
    public void Register_WithInterfaces()
        => _manager.Register(new BenchServiceWithIface(), registerInterfaces: true);

    /// <summary>
    /// Register bỏ qua interfaces — baseline rẻ nhất.
    /// Chỉ 1 dict entry update + 1 Volatile.Write slot.
    /// </summary>
    [BenchmarkCategory("Register")]
    [Benchmark(Description = "Register<T> — class only (no interfaces)")]
    public void Register_ClassOnly()
        => _manager.Register(new BenchServiceB(), registerInterfaces: false);

    // -----------------------------------------------------------------------
    // GROUP 2 — GetOrCreateInstance<T>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fast-path: Volatile.Read một static field — không dict lookup, không lock.
    /// Kỳ vọng: dưới 5 ns, zero allocation.
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate<T> — generic slot hit (fastest path)")]
    public BenchServiceA? GetOrCreate_GenericSlotHit()
        => _manager.GetOrCreateInstance<BenchServiceA>();

    /// <summary>
    /// Dict lookup bằng RuntimeTypeHandle, không args.
    /// Path: non-generic overload → TryGetValue → TRY_PUBLISH_SLOT_BY_TYPE (reflection).
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate(Type) — dict hit, no args")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "<Pending>")]
    public object GetOrCreate_DictHit_NoArgs()
        => _manager.GetOrCreateInstance(typeof(BenchServiceC));

    /// <summary>
    /// Signature cache hit: key = ActivatorKey(BenchServiceWithArgs, int, string).
    /// Path: tính ActivatorKey.GetHashCode → TryGetValue _signatureInstanceCache.
    /// Kỳ vọng: chậm hơn dict hit vì HashCode phức tạp hơn RuntimeTypeHandle.
    /// </summary>
    [BenchmarkCategory("GetOrCreate")]
    [Benchmark(Description = "GetOrCreate(Type, args) — signature cache hit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "<Pending>")]
    public object GetOrCreate_SignatureCacheHit()
        => _manager.GetOrCreateInstance(typeof(BenchServiceWithArgs), s_ctorArgs);

    // -----------------------------------------------------------------------
    // GROUP 3 — GetExistingInstance<T>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Layer 1 — generic slot (Volatile.Read): nhanh nhất, không lock.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — generic slot hit")]
    public BenchServiceA? GetExisting_SlotHit()
        => _manager.GetExistingInstance<BenchServiceA>();

    /// <summary>
    /// Layer 2 — thread-local L1 (s_tsLastKey / s_tsLastValue).
    /// Gọi BenchServiceB sau BenchServiceA → L1 hit sau iteration đầu tiên.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — thread-local L1 hit")]
    public BenchServiceB? GetExisting_ThreadLocalHit()
        => _manager.GetExistingInstance<BenchServiceB>();

    /// <summary>
    /// Layer 3 — ConcurrentDictionary fallback.
    /// BenchServiceC xen kẽ ép s_tsLastKey miss → đi thẳng vào dict.
    /// Path: TryGetValue + Volatile.Write slot + Interlocked.Increment.
    /// </summary>
    [BenchmarkCategory("GetExisting")]
    [Benchmark(Description = "GetExisting<T> — dict fallback")]
    public BenchServiceC? GetExisting_DictFallback()
        => _manager.GetExistingInstance<BenchServiceC>();
}
