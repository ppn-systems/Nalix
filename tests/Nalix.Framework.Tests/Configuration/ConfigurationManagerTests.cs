// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Nalix.Common.Environment;
using Nalix.Common.Exceptions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Configuration.Binding;
using Xunit;

namespace Nalix.Framework.Tests.Configuration;

/// <summary>
/// Provides unit tests for the public API exposed by <see cref="ConfigurationManager"/>.
/// </summary>
public sealed class ConfigurationManagerTests : IDisposable
{
    private readonly string _testDirectory;
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

        _ = Directory.CreateDirectory(_testDirectory);
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
    public void GetWhenConfigurationFileContainsValuesReturnsInitializedConfiguration()
    {
        string filePath = WriteConfigFile(
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
    public void GetWhenCalledMultipleTimesReturnsSameCachedInstance()
    {
        string filePath = WriteConfigFile(
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
    public void GetWhenConfigurationFileDoesNotExistReturnsDefaultValues()
    {
        string filePath = Path.Combine(_testDirectory, "missing.ini");

        using ConfigurationManager manager = CreateManager(filePath);

        Assert.False(manager.ConfigFileExists);

        SampleConfig configuration = manager.Get<SampleConfig>();

        Assert.NotNull(configuration);
        Assert.True(configuration.IsInitialized);
        Assert.Equal(0, configuration.Number);
        Assert.Equal(string.Empty, configuration.Message);
        Assert.True(manager.IsLoaded<SampleConfig>());
    }

    [Fact]
    public void GetWithPathOverloadUsesProvidedConfigurationFile()
    {
        string filePath = WriteConfigFile(
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
    public void SetConfigFilePathWhenPathIsNullOrWhitespaceThrowsArgumentException(string? path)
    {
        using ConfigurationManager manager = CreateManager();

        _ = Assert.Throws<ArgumentException>(() => manager.SetConfigFilePath(path!));
    }

    [Fact]
    public void SetConfigFilePathWhenPathIsOutsideConfigurationDirectoryThrowsInternalErrorException()
    {
        using ConfigurationManager manager = CreateManager();
        string outsidePath = Path.Combine(Path.GetTempPath(), $"outside_{Guid.NewGuid():N}.ini");

        _ = Assert.Throws<InternalErrorException>(() => manager.SetConfigFilePath(outsidePath));
    }

    [Fact]
    public void SetConfigFilePathWhenPathIsUnchangedReturnsFalse()
    {
        string filePath = WriteConfigFile(
            "same-path.ini",
            """
            [Sample]
            Number = 1
            Message = same
            """);

        using ConfigurationManager manager = CreateManager(filePath);

        bool changed = manager.SetConfigFilePath(filePath);

        Assert.False(changed);
        Assert.Equal(filePath, manager.ConfigFilePath);
    }

    [Fact]
    public void SetConfigFilePathWhenAutoReloadIsDisabledKeepsExistingValuesUntilReloadAll()
    {
        string firstPath = WriteConfigFile(
            "first.ini",
            """
            [Sample]
            Number = 1
            Message = first
            """);

        string secondPath = WriteConfigFile(
            "second.ini",
            """
            [Sample]
            Number = 2
            Message = second
            """);

        using ConfigurationManager manager = CreateManager(firstPath);
        SampleConfig configuration = manager.Get<SampleConfig>();

        bool changed = manager.SetConfigFilePath(secondPath, autoReload: false);

        Assert.True(changed);
        Assert.Equal(secondPath, manager.ConfigFilePath);
        Assert.Equal(1, configuration.Number);
        Assert.Equal("first", configuration.Message);

        bool reloaded = manager.ReloadAll();

        Assert.True(reloaded);
        Assert.Equal(2, configuration.Number);
        Assert.Equal("second", configuration.Message);
    }

    [Fact]
    public void SetConfigFilePathWhenAutoReloadIsEnabledUpdatesExistingLoadedInstance()
    {
        string firstPath = WriteConfigFile(
            "auto-first.ini",
            """
            [Sample]
            Number = 10
            Message = before
            """);

        string secondPath = WriteConfigFile(
            "auto-second.ini",
            """
            [Sample]
            Number = 20
            Message = after
            """);

        using ConfigurationManager manager = CreateManager(firstPath);
        SampleConfig configuration = manager.Get<SampleConfig>();

        DateTime beforeReload = manager.LastReloadTime;
        bool changed = manager.SetConfigFilePath(secondPath, autoReload: true);

        Assert.True(changed);
        Assert.Equal(secondPath, manager.ConfigFilePath);
        Assert.Equal(20, configuration.Number);
        Assert.Equal("after", configuration.Message);
        Assert.True(manager.LastReloadTime >= beforeReload);
    }

    [Fact]
    public void ReloadAllWhenConfigurationFileChangesReloadsLoadedConfigurationsAndUpdatesTimestamp()
    {
        string filePath = WriteConfigFile(
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

        bool reloaded = manager.ReloadAll();

        Assert.True(reloaded);
        Assert.Equal(99, configuration.Number);
        Assert.Equal("updated", configuration.Message);
        Assert.True(manager.LastReloadTime > beforeReload);
    }

    [Fact]
    public void RemoveWhenConfigurationWasLoadedRemovesItFromCache()
    {
        string filePath = WriteConfigFile(
            "remove.ini",
            """
            [Sample]
            Number = 8
            Message = remove
            """);

        using ConfigurationManager manager = CreateManager(filePath);
        SampleConfig first = manager.Get<SampleConfig>();

        bool removed = manager.Remove<SampleConfig>();
        SampleConfig second = manager.Get<SampleConfig>();

        Assert.True(removed);
        Assert.NotSame(first, second);
        Assert.True(manager.IsLoaded<SampleConfig>());
    }

    [Fact]
    public void RemoveWhenConfigurationWasNotLoadedReturnsFalse()
    {
        using ConfigurationManager manager = CreateManager();

        bool removed = manager.Remove<SampleConfig>();

        Assert.False(removed);
    }

    [Fact]
    public void ClearAllWhenConfigurationsWereLoadedRemovesAllCachedInstances()
    {
        string filePath = WriteConfigFile(
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
    public void FlushWhenConfigurationHasNotBeenCreatedDoesNotThrow()
    {
        string filePath = Path.Combine(_testDirectory, "flush.ini");

        using ConfigurationManager manager = CreateManager(filePath);

        Exception? exception = Record.Exception(manager.Flush);

        Assert.Null(exception);
    }

    private ConfigurationManager CreateManager(string? filePath = null)
    {
        ConfigurationManager manager = new();
        _managers.Add(manager);

        if (filePath is not null)
        {
            bool changed = manager.SetConfigFilePath(filePath, autoReload: true);
            Assert.True(changed);
        }

        return manager;
    }

    private string WriteConfigFile(string fileName, string content)
    {
        string filePath = Path.Combine(_testDirectory, fileName);
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
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the text value used by the test configuration.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an additional configuration type used to verify cache clearing.
    /// </summary>
    public sealed class AnotherConfig : ConfigurationLoader
    {
        /// <summary>
        /// Gets or sets a value indicating whether the configuration is enabled.
        /// </summary>
        public bool Enabled { get; set; }
    }
}
