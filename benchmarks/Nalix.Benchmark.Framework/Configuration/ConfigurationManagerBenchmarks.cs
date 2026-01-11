// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using Nalix.Framework.Configuration;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Benchmark.Framework.Configuration;

// ---------------------------------------------------------------------------
// Dummy ConfigurationLoader types — chỉ dùng cho benchmark
// ---------------------------------------------------------------------------

/// <summary>Loader A — type đầu tiên được cache trong _configContainerDict.</summary>
public sealed class BenchConfigA : ConfigurationLoader { }

/// <summary>Loader B — type thứ hai, dùng để benchmark multi-type dict lookup.</summary>
public sealed class BenchConfigB : ConfigurationLoader { }

/// <summary>
/// Loader C — KHÔNG được Get() trong Setup, dùng để đo cache MISS path
/// (Lazy chưa được khởi tạo → tạo mới + Initialize).
/// </summary>
public sealed class BenchConfigC : ConfigurationLoader { }

// ---------------------------------------------------------------------------
// Benchmark class
// ---------------------------------------------------------------------------

/// <summary>
/// Đo hiệu năng các public API của <see cref="ConfigurationManager"/>:
/// <list type="bullet">
///   <item><see cref="ConfigurationManager.Get{TClass}()"/> — cache hit vs cache miss</item>
///   <item><see cref="ConfigurationManager.IsLoaded{TClass}()"/> — ContainsKey lookup</item>
///   <item><see cref="ConfigurationManager.ReloadAll()"/> — write lock + file re-read</item>
///   <item><see cref="ConfigurationManager.SetConfigFilePath"/> — path change + optional reload</item>
/// </list>
/// </summary>
/// <remarks>
/// <b>Singleton pattern:</b> <see cref="ConfigurationManager"/> kế thừa <c>SingletonBase</c>
/// nên dùng <c>.Instance</c>, không <c>new()</c>.
/// Không gọi <c>Dispose()</c> trong <c>GlobalCleanup</c> — chỉ <c>ClearAll()</c>
/// để tránh <c>ObjectDisposedException</c> ở param run tiếp theo.
/// <para/>
/// <b>File I/O:</b> Benchmark tạo một file INI tạm thực sự trên disk để
/// <see cref="ConfigurationManager.ReloadAll"/> và <see cref="ConfigurationManager.SetConfigFilePath"/>
/// có file hợp lệ để đọc — tránh false result do exception bị nuốt trong reload path.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class ConfigurationManagerBenchmarks
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private ConfigurationManager _manager = null!;

    // Đường dẫn 2 file INI tạm — tạo trong GlobalSetup, xoá trong GlobalCleanup
    private string _iniPathA = null!;
    private string _iniPathB = null!;

    // -----------------------------------------------------------------------
    // Setup / Cleanup
    // -----------------------------------------------------------------------

    [GlobalSetup]
    public void Setup()
    {
        _manager = ConfigurationManager.Instance;

        // Lấy thư mục config hợp lệ từ singleton để tránh vi phạm VALIDATE_CONFIG_PATH
        string configDir = Path.GetDirectoryName(_manager.ConfigFilePath)!;
        _ = Directory.CreateDirectory(configDir);

        // Tạo 2 file INI tạm với nội dung tối thiểu hợp lệ
        _iniPathA = Path.Combine(configDir, "bench_a.ini");
        _iniPathB = Path.Combine(configDir, "bench_b.ini");

        File.WriteAllText(_iniPathA, "[Benchmark]\nKey=ValueA\n");
        File.WriteAllText(_iniPathB, "[Benchmark]\nKey=ValueB\n");

        // Trỏ manager về file A trước
        _ = _manager.SetConfigFilePath(_iniPathA, autoReload: false);
        _manager.ClearAll();

        // Warm up cache cho BenchConfigA và BenchConfigB (cache HIT path)
        _ = _manager.Get<BenchConfigA>();
        _ = _manager.Get<BenchConfigB>();
        // BenchConfigC KHÔNG được warm up → cache MISS path
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Chỉ ClearAll, KHÔNG Dispose — tránh ObjectDisposedException ở param run tiếp theo
        _manager.ClearAll();

        // Dọn file tạm
        if (File.Exists(_iniPathA))
        {
            File.Delete(_iniPathA);
        }

        if (File.Exists(_iniPathB))
        {
            File.Delete(_iniPathB);
        }
    }

    // IterationSetup cho Get_CacheMiss: xoá BenchConfigC trước mỗi lần đo
    // để đảm bảo mỗi iteration luôn đi qua path tạo mới (không bị warm từ lần trước)
    [IterationSetup(Target = nameof(Get_CacheMiss))]
    public void RemoveConfigC() => _manager.Remove<BenchConfigC>();

    // IterationSetup cho SetConfigFilePath benchmarks: reset về pathA trước mỗi lần đo pathB
    [IterationSetup(Target = nameof(SetConfigFilePath_NoReload))]
    public void ResetToPathA_NoReload() => _manager.SetConfigFilePath(_iniPathA, autoReload: false);

    [IterationSetup(Target = nameof(SetConfigFilePath_WithReload))]
    public void ResetToPathA_WithReload() => _manager.SetConfigFilePath(_iniPathA, autoReload: false);

    // -----------------------------------------------------------------------
    // GROUP 1 — Get<TClass>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Cache HIT: Lazy đã được khởi tạo → chỉ là GetOrAdd trên ConcurrentDictionary
    /// + read lock snapshot + <c>lazy.Value</c> (đã computed).
    /// Kỳ vọng: rất nhanh, gần như lock-free sau lần đầu.
    /// </summary>
    [BenchmarkCategory("Get")]
    [Benchmark(Description = "Get<T> — cache hit (Lazy already computed)")]
    public BenchConfigA Get_CacheHit()
        => _manager.Get<BenchConfigA>();

    /// <summary>
    /// Cache MISS: Lazy chưa tồn tại → GetOrAdd tạo Lazy mới → <c>lazy.Value</c>
    /// trigger <c>new TClass()</c> + <c>Initialize(iniSnapshot.Value)</c> + file I/O.
    /// IterationSetup xoá BenchConfigC trước mỗi lần đo để luôn miss.
    /// Kỳ vọng: chậm hơn cache hit nhiều lần vì có file I/O + write lock.
    /// </summary>
    [BenchmarkCategory("Get")]
    [Benchmark(Description = "Get<T> — cache miss (first load, file I/O)")]
    public BenchConfigC Get_CacheMiss()
        => _manager.Get<BenchConfigC>();

    // -----------------------------------------------------------------------
    // GROUP 2 — IsLoaded<TClass>
    // -----------------------------------------------------------------------

    /// <summary>
    /// IsLoaded khi type đã có trong dict — ContainsKey trên ConcurrentDictionary, lock-free.
    /// Kỳ vọng: cực nhanh, tương đương dict lookup.
    /// </summary>
    [BenchmarkCategory("IsLoaded")]
    [Benchmark(Description = "IsLoaded<T> — type present (ContainsKey hit)")]
    public bool IsLoaded_Hit()
        => _manager.IsLoaded<BenchConfigA>();

    /// <summary>
    /// IsLoaded khi type KHÔNG có trong dict — ContainsKey miss.
    /// BenchConfigC bị xoá bởi IterationSetup của Get_CacheMiss, nhưng ở đây
    /// ta dùng một type riêng biệt (BenchConfigC) mà KHÔNG setup Get trước.
    /// Kỳ vọng: tương đương IsLoaded_Hit — ConcurrentDictionary miss cũng O(1).
    /// </summary>
    [BenchmarkCategory("IsLoaded")]
    [Benchmark(Description = "IsLoaded<T> — type absent (ContainsKey miss)")]
    public bool IsLoaded_Miss()
        => _manager.IsLoaded<BenchConfigC>();

    // -----------------------------------------------------------------------
    // GROUP 3 — ReloadAll
    // -----------------------------------------------------------------------

    /// <summary>
    /// ReloadAll với 2 containers đã loaded (BenchConfigA + BenchConfigB).
    /// Path: _reloadGate.Wait → write lock → IniConfig.Reload (file I/O)
    ///       → Initialize() × N containers → release.
    /// Đây là operation đắt nhất — dominated bởi file I/O và write lock contention.
    /// </summary>
    [BenchmarkCategory("Reload")]
    [Benchmark(Description = "ReloadAll — 2 loaded containers (file I/O + write lock)")]
    public bool ReloadAll()
        => _manager.ReloadAll();

    // -----------------------------------------------------------------------
    // GROUP 4 — SetConfigFilePath
    // -----------------------------------------------------------------------

    /// <summary>
    /// Thay đổi path, KHÔNG auto-reload.
    /// Path: VALIDATE_CONFIG_PATH → _reloadGate.Wait → write lock
    ///       → swap _iniFile Lazy → SETUP_FILE_WATCHER → release.
    /// Không có file I/O nặng (Lazy chưa evaluated).
    /// </summary>
    [BenchmarkCategory("SetPath")]
    [Benchmark(Description = "SetConfigFilePath — no reload (path swap only)")]
    public bool SetConfigFilePath_NoReload()
        => _manager.SetConfigFilePath(_iniPathB, autoReload: false);

    /// <summary>
    /// Thay đổi path VÀ auto-reload tất cả containers.
    /// Path: như trên + force-load IniConfig mới (file I/O) + Initialize() × N.
    /// Kỳ vọng: chậm hơn NoReload đáng kể vì thêm file I/O.
    /// </summary>
    [BenchmarkCategory("SetPath")]
    [Benchmark(Description = "SetConfigFilePath — with auto-reload (file I/O)")]
    public bool SetConfigFilePath_WithReload()
        => _manager.SetConfigFilePath(_iniPathB, autoReload: true);
}
