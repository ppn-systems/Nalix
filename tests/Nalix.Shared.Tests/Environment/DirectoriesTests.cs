// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Environment;

namespace Nalix.Shared.Tests.Environment;

[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
[Xunit.Collection("DirectoriesTests")]
public sealed class DirectoriesTests(DirectoriesFixture fx) : Xunit.IClassFixture<DirectoriesFixture>
{
    private readonly DirectoriesFixture _fx = fx;

    private static string Env(string name)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return value ?? string.Empty;
    }

    [Xunit.Fact]
    public void BasePathRespectsEnvironmentOrOverride()
    {
        string expected = Env("NALIX_BASE_PATH");
        Xunit.Assert.False(string.IsNullOrEmpty(expected));

        // Do đã gọi OverrideBasePathForTesting(BaseDir), BasePath phải bằng BaseDir
        Xunit.Assert.Equal(System.IO.Path.GetFullPath(expected),
                           System.IO.Path.GetFullPath(Directories.BaseAssetsDirectory));
    }

    [Xunit.Fact]
    public void AllKnownDirectoriesAreCreatedAndExist()
    {
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.BaseAssetsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.DataDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.LogsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.TemporaryDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.ConfigurationDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.StorageDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.DatabaseDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.CacheDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.UploadsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Directories.BackupsDirectory));
    }

    [Xunit.Fact]
    public void GetFilePathCreatesParentAndCombinesSafely()
    {
        string parent = System.IO.Path.Combine(
            Directories.DataDirectory, "unit_parent_" + System.Guid.NewGuid().ToString("N"));

        string filePath = Directories.GetFilePath(parent, "foo.bin");

        Xunit.Assert.True(System.IO.Directory.Exists(parent));
        Xunit.Assert.True(filePath.EndsWith(System.IO.Path.DirectorySeparatorChar + "foo.bin")
                       || filePath.EndsWith(System.IO.Path.AltDirectorySeparatorChar + "foo.bin"));
    }

    [Xunit.Fact]
    public void CreateTimestampedDirectoryUsesUTCFormatAndExists()
    {
        string parent = Directories.DataDirectory;
        string dir = Directories.CreateTimestampedDirectory(parent, "px");

        Xunit.Assert.True(System.IO.Directory.Exists(dir));

        string name = new System.IO.DirectoryInfo(dir).Name;
        // "px_" + "yyyyMMddTHHmmssZ" -> tối thiểu 3 + 16 = 19
        Xunit.Assert.StartsWith("px_", name);
        Xunit.Assert.True(name.Length >= 19);
    }

    [Xunit.Fact]
    public void CleanupDirectoryRemovesOnlyOldFiles()
    {
        string target = System.IO.Path.Combine(
            Directories.TemporaryDirectory, "cleanup_" + System.Guid.NewGuid().ToString("N"));

        _ = System.IO.Directory.CreateDirectory(target);

        string oldFile = System.IO.Path.Combine(target, "old.tmp");
        string newFile = System.IO.Path.Combine(target, "new.tmp");

        using (System.IO.File.Create(oldFile)) { }
        using (System.IO.File.Create(newFile)) { }

        // Biến "old" thành cũ hơn 1 ngày
        System.DateTime past = System.DateTime.UtcNow - System.TimeSpan.FromDays(2);
        System.IO.File.SetLastWriteTimeUtc(oldFile, past);

        int removed = Directories.DeleteOldFiles(target, System.TimeSpan.FromDays(1), "*.tmp");

        Xunit.Assert.Equal(1, removed);
        Xunit.Assert.False(System.IO.File.Exists(oldFile));
        Xunit.Assert.True(System.IO.File.Exists(newFile));
    }

    [Xunit.Fact]
    public void CreateSubdirectoryRaisesDirectoryCreatedEvent()
    {
        System.Collections.Generic.List<string> hits = [];

        void handler(string p)
        {
            if (!string.IsNullOrEmpty(p)) { hits.Add(p); }
        }

        Directories.RegisterDirectoryCreationHandler(handler);

        try
        {
            string parent = System.IO.Path.Combine(
                Directories.DataDirectory, "evt_" + System.Guid.NewGuid().ToString("N"));

            string sub = Directories.CreateSubdirectory(parent, "child");

            Xunit.Assert.True(System.IO.Directory.Exists(sub));
            Xunit.Assert.True(hits.Count >= 1);
            Xunit.Assert.Contains(sub, hits);
        }
        finally
        {
            Directories.UnregisterDirectoryCreationHandler(handler);
        }
    }

    [Xunit.Fact]
    public void GetFilePathBlocksPathTraversal()
    {
        string baseDir = Directories.DataDirectory;
        string traversal = ".." + System.IO.Path.DirectorySeparatorChar + "evil.txt";

        void act() => _ = Directories.GetFilePath(baseDir, traversal);

        _ = Xunit.Assert.Throws<System.UnauthorizedAccessException>(act);
    }

    [Xunit.Fact]
    public void CreateHierarchicalDateDirectoryBuildsYMD()
    {
        string parent = Directories.DataDirectory;
        string day = Directories.CreateHierarchicalDateDirectory(parent);

        Xunit.Assert.True(System.IO.Directory.Exists(day));

        string rel = day[parent.Length..].Trim(System.IO.Path.DirectorySeparatorChar);
        string[] parts = rel.Split(System.IO.Path.DirectorySeparatorChar);

        Xunit.Assert.Equal(3, parts.Length);
        Xunit.Assert.Equal(4, parts[0].Length);
        Xunit.Assert.Equal(2, parts[1].Length);
        Xunit.Assert.Equal(2, parts[2].Length);
    }

    [Xunit.Fact]
    public void ValidateDirectoriesReturnsTrue()
    {
        bool ok = Directories.CanAccessAllDirectories();
        Xunit.Assert.True(ok);
    }
}
