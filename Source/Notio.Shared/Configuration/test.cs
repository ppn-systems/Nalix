using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Notio.Shared.Configuration;

internal sealed class ConfiguredIniFile1 : IDisposable
{
    private readonly string _path;
    private readonly Timer? _saveTimer;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _iniData;
    private FileSystemWatcher? _fileWatcher;
    private readonly bool _caseSensitive;
    private readonly ReaderWriterLockSlim _lock = new();

    public bool AutoReload { get; set; } = true;
    public TimeSpan SaveDelay { get; set; } = TimeSpan.FromSeconds(2);

    public ConfiguredIniFile1(string path, bool caseSensitive = false)
    {
        _path = path;
        _caseSensitive = caseSensitive;
        var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _iniData = new(comparer);

        Load();
        SetupFileWatcher();
        _saveTimer = new Timer(_ => SaveInternal(), null, Timeout.Infinite, Timeout.Infinite);
    }

    private void SetupFileWatcher()
    {
        try
        {
            _fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_path)!, Path.GetFileName(_path))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _fileWatcher.Changed += OnFileChanged;
        }
        catch { /* Xử lý lỗi file system */ }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (AutoReload && e.ChangeType == WatcherChangeTypes.Changed)
        {
            Thread.Sleep(100); // Chờ file unlock
            Load();
        }
    }

    private void Load()
    {
        _lock.EnterWriteLock();
        try
        {
            using var stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 8192);

            string currentSection = string.Empty;
            while (reader.ReadLine() is { } line)
            {
                ProcessLine(line, ref currentSection);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ProcessLine(ReadOnlySpan<char> line, ref string currentSection)
    {
        var trimmed = line.Trim();
        if (trimmed.IsEmpty || trimmed[0] == ';') return;

        if (trimmed[0] == '[' && trimmed[^1] == ']')
        {
            currentSection = trimmed[1..^1].Trim().ToString();
            _iniData.TryAdd(currentSection, new(_iniData.Comparer));
        }
        else if (!string.IsNullOrEmpty(currentSection))
        {
            var splitIdx = trimmed.IndexOf('=');
            if (splitIdx > 0)
            {
                var key = trimmed[..splitIdx].Trim().ToString();
                var value = trimmed[(splitIdx + 1)..].Trim().ToString();
                _iniData[currentSection].TryAdd(key, value);
            }
        }
    }

    public void WriteValue(string section, string key, object value, bool overwrite = false)
    {
        var sectionKey = NormalizeKey(section);
        var entryKey = NormalizeKey(key);

        var sectionDict = _iniData.GetOrAdd(sectionKey, _ => new(_iniData.Comparer));

        if (overwrite)
            sectionDict[entryKey] = value.ToString()!;
        else
            sectionDict.TryAdd(entryKey, value.ToString()!);

        ScheduleSave();
    }

    private string NormalizeKey(string key) => _caseSensitive ? key : key.ToUpperInvariant();

    private void ScheduleSave()
    {
        _saveTimer?.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void SaveInternal()
    {
        _lock.EnterReadLock();
        try
        {
            using var tempFile = new TempFile(_path);
            using var writer = new StreamWriter(tempFile.Path, false, Encoding.UTF8, 8192);

            foreach (var section in _iniData.OrderBy(s => s.Key))
            {
                writer.WriteLine($"[{section.Key}]");
                foreach (var kvp in section.Value.OrderBy(k => k.Key))
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }
                writer.WriteLine();
            }

            tempFile.Commit();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public T? GetValue<T>(string section, string key, T? defaultValue = default) where T : IParsable<T>
    {
        _lock.EnterReadLock();
        try
        {
            if (!_iniData.TryGetValue(section, out var sectionData) ||
                !sectionData.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            return T.TryParse(value, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        _fileWatcher?.Dispose();
        _saveTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TempFile(string originalPath) : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(originalPath)!,
                $"{Guid.NewGuid()}.tmp");

        private readonly string _originalPath = originalPath;

        public void Commit()
        {
            File.Replace(Path, _originalPath, null);
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { }
        }
    }
}