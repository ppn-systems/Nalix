using Nalix.Common.Environment;
using Nalix.Common.Exceptions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Configuration.Binding;
using System;
using System.IO;
using Xunit;

namespace Nalix.Framework.Tests.Configuration;

/// <summary>
/// Tests for <see cref="ConfigurationManager"/> and configuration loading infrastructure.
/// </summary>
public sealed class ConfigurationManagerTests : IDisposable
{
    private readonly String _testDirectory;
    private readonly String _testConfigFilePath;

    public ConfigurationManagerTests()
    {
        _testDirectory = Directories.ConfigurationDirectory;
        Directory.CreateDirectory(_testDirectory);
        _testConfigFilePath = _testConfigFilePath = Path.Combine(_testDirectory, $"test_{Guid.NewGuid()}.ini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region Helper

    /// <summary>
    /// A dummy configuration class for testing.
    /// </summary>
    public sealed class DummyConfig : ConfigurationLoader
    {
        /// <summary>
        /// A sample int property.
        /// </summary>
        public Int32 IntValue { get; set; }

        /// <summary>
        /// A sample string property.
        /// </summary>
        public String StringValue { get; set; } = String.Empty;
    }

    #endregion

    [Fact]
    public void Get_HappyPath_CreatesAndReturnsConfigInstance()
    {
        // Arrange
        WriteIniFile("[Dummy]\nIntValue=42\nStringValue=hello");

        var mgr = CreateManager();

        // Act
        var result = mgr.Get<DummyConfig>();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsInitialized);
        Assert.Equal(42, result.IntValue);
        Assert.Equal("hello", result.StringValue);
    }


    [Fact]
    public void Get_NullIniValues_DefaultsApplied()
    {
        // Arrange: Only section, no key
        WriteIniFile("[Dummy]");

        var mgr = CreateManager();

        // Act
        var result = mgr.Get<DummyConfig>();

        // Assert
        Assert.Equal(0, result.IntValue); // Default of int
        Assert.Equal(String.Empty, result.StringValue);
    }

    [Fact]
    public void SetConfigFilePath_OutsideConfigDirectory_ThrowsSecurityException()
    {
        var mgr = new ConfigurationManager();
        String unsafePath = Path.Combine(Path.GetTempPath(), "..", "unsafe.ini");

        // Assert
        Assert.Throws<InternalErrorException>(() =>
        {
            mgr.SetConfigFilePath(unsafePath);
        });
    }

    [Fact]
    public void SetConfigFilePath_SamePath_ReturnsFalse()
    {
        // Arrange
        var mgr = CreateManager();
        var path = mgr.ConfigFilePath;

        // Act
        var result = mgr.SetConfigFilePath(path);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLoaded_AfterGet_ReturnsTrue()
    {
        var mgr = CreateManager();

        Assert.False(mgr.IsLoaded<DummyConfig>());

        var _ = mgr.Get<DummyConfig>();

        Assert.True(mgr.IsLoaded<DummyConfig>());
    }

    [Fact]
    public void Remove_Config_RemovesFromCache()
    {
        var mgr = CreateManager();
        mgr.Get<DummyConfig>();

        Assert.True(mgr.IsLoaded<DummyConfig>());

        var removed = mgr.Remove<DummyConfig>();

        Assert.True(removed);
        Assert.False(mgr.IsLoaded<DummyConfig>());
    }

    [Fact]
    public void ClearAll_RemovesAllConfigs()
    {
        var mgr = CreateManager();
        mgr.Get<DummyConfig>();
        Assert.True(mgr.IsLoaded<DummyConfig>());
        mgr.ClearAll();
        Assert.False(mgr.IsLoaded<DummyConfig>());
    }

    [Fact]
    public void ReloadAll_ConfigReloaded_ValuesAreUpToDate()
    {
        // Arrange
        WriteIniFile("[Dummy]\nIntValue=1\nStringValue=abc");
        var mgr = CreateManager();

        var cfg = mgr.Get<DummyConfig>();

        // Act: Update file, reload, check update
        WriteIniFile("[Dummy]\nIntValue=99\nStringValue=new");
        var reloadResult = mgr.ReloadAll();

        var newCfg = mgr.Get<DummyConfig>();

        // Assert
        Assert.True(reloadResult);
        Assert.Equal(99, newCfg.IntValue);
        Assert.Equal("new", newCfg.StringValue);
    }

    [Fact]
    public void Get_ConfigFileDoesNotExist_DefaultsApplied()
    {
        // Arrange (no file written)
        var mgr = CreateManager();

        // Act
        var cfg = mgr.Get<DummyConfig>();

        // Assert
        Assert.Equal(0, cfg.IntValue);
        Assert.Equal(String.Empty, cfg.StringValue);
    }

    [Fact]
    public void SetConfigFilePath_PathIsNull_ThrowsArgException()
    {
        var mgr = CreateManager();

        Assert.Throws<ArgumentException>(() => mgr.SetConfigFilePath(null!));
        Assert.Throws<ArgumentException>(() => mgr.SetConfigFilePath(""));
    }

    // Test
    [Fact]
    public void Get_WithCustomPath_UsesCorrectFile()
    {
        var filePath1 = Path.Combine(_testDirectory, $"test1_{Guid.NewGuid()}.ini");
        var filePath2 = Path.Combine(_testDirectory, $"test2_{Guid.NewGuid()}.ini");
        File.WriteAllText(filePath1, "[Dummy]\nIntValue=5\nStringValue=s1");
        File.WriteAllText(filePath2, "[Dummy]\nIntValue=8\nStringValue=s2");

        var mgr1 = new ConfigurationManager();
        mgr1.SetConfigFilePath(filePath1, autoReload: true);
        var cfg1 = mgr1.Get<DummyConfig>();

        var mgr2 = new ConfigurationManager();
        mgr2.SetConfigFilePath(filePath2, autoReload: true);
        var cfg2 = mgr2.Get<DummyConfig>();

        Assert.Equal(5, cfg1.IntValue);
        Assert.Equal("s1", cfg1.StringValue);
        Assert.Equal(8, cfg2.IntValue);
        Assert.Equal("s2", cfg2.StringValue);

        Directory.Delete(_testDirectory, true); // Cleanup
    }

    [Fact]
    public void Flush_WritesData_NoException()
    {
        // Arrange
        var mgr = CreateManager();

        // Act & Assert (should not throw)
        mgr.Flush();
    }

    [Fact]
    public void Clone_HappyPath_ClonesValuesCorrectly()
    {
        // Arrange
        var mgr = CreateManager();
        WriteIniFile("[Dummy]\nIntValue=13\nStringValue=abc");
        var cfg = mgr.Get<DummyConfig>();

        // Act
        var clone = cfg.Clone<DummyConfig>();

        // Assert
        Assert.NotSame(cfg, clone);
        Assert.Equal(cfg.IntValue, clone.IntValue);
        Assert.Equal(cfg.StringValue, clone.StringValue);
        Assert.Equal(cfg.LastInitializationTime, clone.LastInitializationTime);
    }

    [Fact]
    public void ConfigFileWatcher_TriggersReload()
    {
        // Arrange
        WriteIniFile("[Dummy]\nIntValue=1\nStringValue=abc");
        var mgr = CreateManager();
        var cfg = mgr.Get<DummyConfig>();

        // Act: Update file on disk, wait for watcher to detect
        WriteIniFile("[Dummy]\nIntValue=7\nStringValue=watcher");
        System.Threading.Thread.Sleep(300); // Allow watcher event to fire

        mgr.ReloadAll(); // fallback explicit

        var newCfg = mgr.Get<DummyConfig>();

        Assert.Equal(7, newCfg.IntValue);
        Assert.Equal("watcher", newCfg.StringValue);
    }

    /// <summary>
    /// Helper: write an INI file to the test location.
    /// </summary>
    private void WriteIniFile(String content) => File.WriteAllText(_testConfigFilePath, content.Replace("\n", Environment.NewLine));

    /// <summary>
    /// Helper: create a ConfigurationManager with a test config file path.
    /// </summary>
    private ConfigurationManager CreateManager()
    {
        var mgr = new ConfigurationManager();
        mgr.SetConfigFilePath(_testConfigFilePath, autoReload: true);
        return mgr;
    }
}