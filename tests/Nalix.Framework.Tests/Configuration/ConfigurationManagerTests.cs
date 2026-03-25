// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
#nullable enable

using Nalix.Common.Environment;
using Nalix.Common.Exceptions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Configuration.Binding;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Nalix.Framework.Tests.Configuration;

/// <summary>
/// Provides unit tests for the public API exposed by <see cref="ConfigurationManager"/>.
/// </summary>
public sealed class ConfigurationManagerTests : IDisposable
{
    private readonly String _testDirectory;
    private readonly List<ConfigurationManager> _managers = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationManagerTests"/> class.
    /// </summary>
    public ConfigurationManagerTests()
    {
        _testDirectory = Path.Combine(
            Directories.ConfigurationDirectory,
            "ConfigurationManagerTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_testDirectory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ConfigurationManager manager in _managers)
        {
            try
            {
                manager.Dispose();
            }
            catch
            {
                // Test cleanup should be best-effort.
            }
        }

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // FileSystemWatcher cleanup can be asynchronous on some platforms.
        }
    }

    [Fact]
    public void Get_WhenConfigurationFileContainsValues_ReturnsInitializedConfiguration()
    {
        String filePath = WriteConfigFile(
            "appsettings.ini",
            """
            [Sample]
            Number = 42
            Message = hello
            """);

        using ConfigurationManager manager = CreateManager(filePath);

        SampleConfig configuration = manager.Get<SampleConfig>();

        Assert.True(configuration.IsInitialized);
        Assert.Equal(42, configuration.Number);
        Assert.Equal("hello", configuration.Message);
        Assert.True(manager.IsLoaded<SampleConfig>());
        Assert.True(manager.ConfigFileExists);
    }

    [Fact]
    public void Get_WhenCalledMultipleTimes_ReturnsSameCachedInstance()
    {
        String filePath = WriteConfigFile(
            "cached.ini",
            """
            [Sample]
            Number = 7
            Message = cache
            """);

        using ConfigurationManager manager = CreateManager(filePath);

        SampleConfig first = manager.Get<SampleConfig>();
        SampleConfig second = manager.Get<SampleConfig>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Get_WhenConfigurationFileDoesNotExist_ReturnsDefaultValues()
    {
        String filePath = Path.Combine(_testDirectory, "missing.ini");

        using ConfigurationManager manager = CreateManager(filePath);

        Assert.False(manager.ConfigFileExists);

        SampleConfig configuration = manager.Get<SampleConfig>();

        Assert.NotNull(configuration);
        Assert.True(configuration.IsInitialized);
        Assert.Equal(0, configuration.Number);
        Assert.Equal(String.Empty, configuration.Message);
        Assert.True(manager.IsLoaded<SampleConfig>());
    }

    [Fact]
    public void Get_WithPathOverload_UsesProvidedConfigurationFile()
    {
        String filePath = WriteConfigFile(
            "overload.ini",
            """
            [Sample]
            Number = 15
            Message = overload
            """);

        using ConfigurationManager manager = CreateManager();

        SampleConfig configuration = manager.Get<SampleConfig>(filePath);

        Assert.Equal(filePath, manager.ConfigFilePath);
        Assert.Equal(15, configuration.Number);
        Assert.Equal("overload", configuration.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetConfigFilePath_WhenPathIsNullOrWhitespace_ThrowsArgumentException(String? path)
    {
        using ConfigurationManager manager = CreateManager();

        Assert.Throws<ArgumentException>(() => manager.SetConfigFilePath(path!));
    }

    [Fact]
    public void SetConfigFilePath_WhenPathIsOutsideConfigurationDirectory_ThrowsInternalErrorException()
    {
        using ConfigurationManager manager = CreateManager();
        String outsidePath = Path.Combine(Path.GetTempPath(), $"outside_{Guid.NewGuid():N}.ini");

        Assert.Throws<InternalErrorException>(() => manager.SetConfigFilePath(outsidePath));
    }

    [Fact]
    public void SetConfigFilePath_WhenPathIsUnchanged_ReturnsFalse()
    {
        String filePath = WriteConfigFile(
            "same-path.ini",
            """
            [Sample]
            Number = 1
            Message = same
            """);

        using ConfigurationManager manager = CreateManager(filePath);

        Boolean changed = manager.SetConfigFilePath(filePath);

        Assert.False(changed);
        Assert.Equal(filePath, manager.ConfigFilePath);
    }

    [Fact]
    public void SetConfigFilePath_WhenAutoReloadIsDisabled_KeepsExistingValuesUntilReloadAll()
    {
        String firstPath = WriteConfigFile(
            "first.ini",
            """
            [Sample]
            Number = 1
            Message = first
            """);

        String secondPath = WriteConfigFile(
            "second.ini",
            """
            [Sample]
            Number = 2
            Message = second
            """);

        using ConfigurationManager manager = CreateManager(firstPath);
        SampleConfig configuration = manager.Get<SampleConfig>();

        Boolean changed = manager.SetConfigFilePath(secondPath, autoReload: false);

        Assert.True(changed);
        Assert.Equal(secondPath, manager.ConfigFilePath);
        Assert.Equal(1, configuration.Number);
        Assert.Equal("first", configuration.Message);

        Boolean reloaded = manager.ReloadAll();

        Assert.True(reloaded);
        Assert.Equal(2, configuration.Number);
        Assert.Equal("second", configuration.Message);
    }

    [Fact]
    public void SetConfigFilePath_WhenAutoReloadIsEnabled_UpdatesExistingLoadedInstance()
    {
        String firstPath = WriteConfigFile(
            "auto-first.ini",
            """
            [Sample]
            Number = 10
            Message = before
            """);

        String secondPath = WriteConfigFile(
            "auto-second.ini",
            """
            [Sample]
            Number = 20
            Message = after
            """);

        using ConfigurationManager manager = CreateManager(firstPath);
        SampleConfig configuration = manager.Get<SampleConfig>();

        DateTime beforeReload = manager.LastReloadTime;
        Boolean changed = manager.SetConfigFilePath(secondPath, autoReload: true);

        Assert.True(changed);
        Assert.Equal(secondPath, manager.ConfigFilePath);
        Assert.Equal(20, configuration.Number);
        Assert.Equal("after", configuration.Message);
        Assert.True(manager.LastReloadTime >= beforeReload);
    }

    [Fact]
    public void ReloadAll_WhenConfigurationFileChanges_ReloadsLoadedConfigurationsAndUpdatesTimestamp()
    {
        String filePath = WriteConfigFile(
            "reload.ini",
            """
            [Sample]
            Number = 5
            Message = initial
            """);

        using ConfigurationManager manager = CreateManager(filePath);
        SampleConfig configuration = manager.Get<SampleConfig>();
        DateTime beforeReload = manager.LastReloadTime;

        System.Threading.Thread.Sleep(20);

        File.WriteAllText(
            filePath,
            """
            [Sample]
            Number = 99
            Message = updated
            """);

        Boolean reloaded = manager.ReloadAll();

        Assert.True(reloaded);
        Assert.Equal(99, configuration.Number);
        Assert.Equal("updated", configuration.Message);
        Assert.True(manager.LastReloadTime > beforeReload);
    }

    [Fact]
    public void Remove_WhenConfigurationWasLoaded_RemovesItFromCache()
    {
        String filePath = WriteConfigFile(
            "remove.ini",
            """
            [Sample]
            Number = 8
            Message = remove
            """);

        using ConfigurationManager manager = CreateManager(filePath);
        SampleConfig first = manager.Get<SampleConfig>();

        Boolean removed = manager.Remove<SampleConfig>();
        SampleConfig second = manager.Get<SampleConfig>();

        Assert.True(removed);
        Assert.NotSame(first, second);
        Assert.True(manager.IsLoaded<SampleConfig>());
    }

    [Fact]
    public void Remove_WhenConfigurationWasNotLoaded_ReturnsFalse()
    {
        using ConfigurationManager manager = CreateManager();

        Boolean removed = manager.Remove<SampleConfig>();

        Assert.False(removed);
    }

    [Fact]
    public void ClearAll_WhenConfigurationsWereLoaded_RemovesAllCachedInstances()
    {
        String filePath = WriteConfigFile(
            "clear.ini",
            """
            [Sample]
            Number = 3
            Message = clear
            [Another]
            Enabled = true
            """);

        using ConfigurationManager manager = CreateManager(filePath);
        SampleConfig sample = manager.Get<SampleConfig>();
        AnotherConfig another = manager.Get<AnotherConfig>();

        manager.ClearAll();

        Assert.False(manager.IsLoaded<SampleConfig>());
        Assert.False(manager.IsLoaded<AnotherConfig>());

        SampleConfig reloadedSample = manager.Get<SampleConfig>();
        AnotherConfig reloadedAnother = manager.Get<AnotherConfig>();

        Assert.NotSame(sample, reloadedSample);
        Assert.NotSame(another, reloadedAnother);
    }

    [Fact]
    public void Flush_WhenConfigurationHasNotBeenCreated_DoesNotThrow()
    {
        String filePath = Path.Combine(_testDirectory, "flush.ini");

        using ConfigurationManager manager = CreateManager(filePath);

        Exception? exception = Record.Exception(manager.Flush);

        Assert.Null(exception);
    }

    private ConfigurationManager CreateManager(String? filePath = null)
    {
        ConfigurationManager manager = new();
        _managers.Add(manager);

        if (filePath is not null)
        {
            Boolean changed = manager.SetConfigFilePath(filePath, autoReload: true);
            Assert.True(changed);
        }

        return manager;
    }

    private String WriteConfigFile(String fileName, String content)
    {
        String filePath = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(filePath, content.ReplaceLineEndings(Environment.NewLine));
        return filePath;
    }

    /// <summary>
    /// Represents a configuration type used to validate scalar binding.
    /// </summary>
    public sealed class SampleConfig : ConfigurationLoader
    {
        /// <summary>
        /// Gets or sets the numeric value used by the test configuration.
        /// </summary>
        public Int32 Number { get; set; }

        /// <summary>
        /// Gets or sets the text value used by the test configuration.
        /// </summary>
        public String Message { get; set; } = String.Empty;
    }

    /// <summary>
    /// Represents an additional configuration type used to verify cache clearing.
    /// </summary>
    public sealed class AnotherConfig : ConfigurationLoader
    {
        /// <summary>
        /// Gets or sets a value indicating whether the configuration is enabled.
        /// </summary>
        public Boolean Enabled { get; set; }
    }
}
