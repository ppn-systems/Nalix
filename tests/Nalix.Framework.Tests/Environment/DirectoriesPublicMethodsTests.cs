// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using Nalix.Framework.Environment;
using Xunit;

namespace Nalix.Framework.Tests.Environment;

public sealed class DirectoriesPublicMethodsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Nalix.Directories.Tests", Guid.NewGuid().ToString("N"));

    public DirectoriesPublicMethodsTests() => _ = Directory.CreateDirectory(_root);

    [Fact]
    public void GetFilePathWhenDirectoryDoesNotExistCreatesDirectory()
    {
        string nestedDir = Path.Combine(_root, "nested", "files");

        string filePath = Directories.GetFilePath(nestedDir, "a.txt");

        Assert.Equal(Path.Combine(nestedDir, "a.txt"), filePath);
        Assert.True(Directory.Exists(nestedDir));
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
