// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using Nalix.Framework.Environment;
using Xunit;

namespace Nalix.Framework.Tests.Environment;

public sealed class DirectoriesPublicMethodsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Nalix.Directories.Tests", Guid.NewGuid().ToString("N"));

    public DirectoriesPublicMethodsTests()
    {
        _ = Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CreateSubdirectoryWhenArgumentsAreInvalidThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Directories.CreateSubdirectory(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => Directories.CreateSubdirectory(_root, null!));
    }

    [Fact]
    public void CreateSubdirectoryWhenNameEscapesBaseThrowsUnauthorizedAccessException()
    {
        Assert.Throws<UnauthorizedAccessException>(() => Directories.CreateSubdirectory(_root, "..\\escape"));
    }

    [Fact]
    public void CreateSubdirectoryWhenCalledCreatesDirectoryAndRaisesEvent()
    {
        string? createdPath = null;
        void Handler(string path) => createdPath = path;

        Directories.RegisterDirectoryCreationHandler(Handler);
        try
        {
            string path = Directories.CreateSubdirectory(_root, "created");

            Assert.True(Directory.Exists(path));
            Assert.Equal(path, createdPath);
        }
        finally
        {
            Directories.UnregisterDirectoryCreationHandler(Handler);
        }
    }

    [Fact]
    public void GetFilePathWhenDirectoryDoesNotExistCreatesDirectory()
    {
        string nestedDir = Path.Combine(_root, "nested", "files");

        string filePath = Directories.GetFilePath(nestedDir, "a.txt");

        Assert.Equal(Path.Combine(nestedDir!, "a.txt"), filePath);
        Assert.True(Directory.Exists(nestedDir));
    }

    [Fact]
    public void TimestampDirectoryAndTimestampedFilePathUseExpectedNaming()
    {
        string dir = Directories.CreateTimestampedDirectory(_root, "log");
        string filePath = Directories.GetTimestampedFilePath(_root!, "report", ".txt");
        string fileName = Path.GetFileName(filePath);

        Assert.StartsWith("log_", Path.GetFileName(dir), StringComparison.Ordinal);
        Assert.StartsWith("report_", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".txt", fileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeleteOldFilesDeletesOnlyFilesOlderThanCutoff()
    {
        string dir = Directories.CreateSubdirectory(_root, "cleanup");
        string oldFile = Path.Combine(dir, "old.txt");
        string newFile = Path.Combine(dir, "new.txt");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow - TimeSpan.FromDays(10));
        File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow);

        int deleted = Directories.DeleteOldFiles(dir, TimeSpan.FromDays(3), "*.txt");

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public void EnumerateFilesAndCalculateDirectorySizeReturnExpectedValues()
    {
        string dir = Directories.CreateSubdirectory(_root, "enum");
        string sub = Directories.CreateSubdirectory(dir, "sub");
        string f1 = Path.Combine(dir!, "a.bin");
        string f2 = Path.Combine(sub!, "b.bin");
        File.WriteAllBytes(f1, [1, 2, 3]);
        File.WriteAllBytes(f2, [4, 5]);

        List<string> topOnly = [.. Directories.EnumerateFiles(dir!, "*.bin", recursive: false)];
        List<string> recursive = [.. Directories.EnumerateFiles(dir!, "*.bin", recursive: true)];
        long nonRecursiveSize = Directories.CalculateDirectorySize(dir!, includeSubdirectories: false);
        long recursiveSize = Directories.CalculateDirectorySize(dir!, includeSubdirectories: true);

        Assert.Single(topOnly);
        Assert.Equal(2, recursive.Count);
        Assert.Equal(3, nonRecursiveSize);
        Assert.Equal(5, recursiveSize);
    }

    [Fact]
    public void CreateDateDirectoryAndHierarchicalDirectoryCreateExpectedStructure()
    {
        string dateDir = Directories.CreateDateDirectory(_root);
        string hierarchicalDir = Directories.CreateHierarchicalDateDirectory(_root!);

        Assert.True(Directory.Exists(dateDir));
        Assert.True(Directory.Exists(hierarchicalDir));
        Assert.Equal(10, Path.GetFileName(dateDir).Length); // yyyy-MM-dd
    }

    [Fact]
    public void EnsureShardedPathWhenArgumentsInvalidThrows()
    {
        Assert.Throws<ArgumentNullException>(() => Directories.EnsureShardedPath(null!, "abc"));
        Assert.Throws<ArgumentNullException>(() => Directories.EnsureShardedPath(_root, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Directories.EnsureShardedPath(_root, "abc", depth: 0, width: 2));
    }

    [Fact]
    public void EnsureShardedPathWhenArgumentsValidReturnsPathWithKeyAndCreatesDirectories()
    {
        string fullPath = Directories.EnsureShardedPath(_root, "payload-key", depth: 2, width: 2);

        Assert.EndsWith(Path.Combine("payload-key"), fullPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(Path.GetDirectoryName(fullPath)!));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
