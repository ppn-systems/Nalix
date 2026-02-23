namespace Nalix.Shared.Tests.Environment;

[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
[Xunit.Collection("DirectoriesTests")]
public sealed class DirectoriesTests(DirectoriesFixture fx) : Xunit.IClassFixture<DirectoriesFixture>
{
    private readonly DirectoriesFixture _fx = fx;

    private static System.String Env(System.String name)
    {
        System.String value = System.Environment.GetEnvironmentVariable(name);
        return value ?? System.String.Empty;
    }

    [Xunit.Fact]
    public void BasePath_Respects_Environment_Or_Override()
    {
        System.String expected = Env("NALIX_BASE_PATH");
        Xunit.Assert.False(System.String.IsNullOrEmpty(expected));

        // Do đã gọi OverrideBasePathForTesting(BaseDir), BasePath phải bằng BaseDir
        Xunit.Assert.Equal(System.IO.Path.GetFullPath(expected),
                           System.IO.Path.GetFullPath(Nalix.Common.Infrastructure.Environment.Directories.BaseAssetsDirectory));
    }

    [Xunit.Fact]
    public void All_Known_Directories_Are_Created_And_Exist()
    {
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.BaseAssetsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.DataDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.LogsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.TemporaryDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.ConfigurationDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.StorageDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.DatabaseDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.CacheDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.UploadsDirectory));
        Xunit.Assert.True(System.IO.Directory.Exists(Nalix.Common.Infrastructure.Environment.Directories.BackupsDirectory));
    }

    [Xunit.Fact]
    public void GetFilePath_Creates_Parent_And_Combines_Safely()
    {
        System.String parent = System.IO.Path.Combine(
            Nalix.Common.Infrastructure.Environment.Directories.DataDirectory, "unit_parent_" + System.Guid.NewGuid().ToString("N"));

        System.String filePath = Nalix.Common.Infrastructure.Environment.Directories.GetFilePath(parent, "foo.bin");

        Xunit.Assert.True(System.IO.Directory.Exists(parent));
        Xunit.Assert.True(filePath.EndsWith(System.IO.Path.DirectorySeparatorChar + "foo.bin")
                       || filePath.EndsWith(System.IO.Path.AltDirectorySeparatorChar + "foo.bin"));
    }

    [Xunit.Fact]
    public void CreateTimestampedDirectory_Uses_UTC_Format_And_Exists()
    {
        System.String parent = Nalix.Common.Infrastructure.Environment.Directories.DataDirectory;
        System.String dir = Nalix.Common.Infrastructure.Environment.Directories.CreateTimestampedDirectory(parent, "px");

        Xunit.Assert.True(System.IO.Directory.Exists(dir));

        System.String name = new System.IO.DirectoryInfo(dir).Name;
        // "px_" + "yyyyMMddTHHmmssZ" -> tối thiểu 3 + 16 = 19
        Xunit.Assert.StartsWith("px_", name);
        Xunit.Assert.True(name.Length >= 19);
    }

    [Xunit.Fact]
    public void CleanupDirectory_Removes_Only_Old_Files()
    {
        System.String target = System.IO.Path.Combine(
            Nalix.Common.Infrastructure.Environment.Directories.TemporaryDirectory, "cleanup_" + System.Guid.NewGuid().ToString("N"));

        _ = System.IO.Directory.CreateDirectory(target);

        System.String oldFile = System.IO.Path.Combine(target, "old.tmp");
        System.String newFile = System.IO.Path.Combine(target, "new.tmp");

        using (System.IO.File.Create(oldFile)) { }
        using (System.IO.File.Create(newFile)) { }

        // Biến "old" thành cũ hơn 1 ngày
        System.DateTime past = System.DateTime.UtcNow - System.TimeSpan.FromDays(2);
        System.IO.File.SetLastWriteTimeUtc(oldFile, past);

        System.Int32 removed = Nalix.Common.Infrastructure.Environment.Directories.DeleteOldFiles(target, System.TimeSpan.FromDays(1), "*.tmp");

        Xunit.Assert.Equal(1, removed);
        Xunit.Assert.False(System.IO.File.Exists(oldFile));
        Xunit.Assert.True(System.IO.File.Exists(newFile));
    }

    [Xunit.Fact]
    public void CreateSubdirectory_Raises_DirectoryCreated_Event()
    {
        System.Collections.Generic.List<System.String> hits = [];

        void handler(System.String p)
        {
            if (!System.String.IsNullOrEmpty(p)) { hits.Add(p); }
        }

        Nalix.Common.Infrastructure.Environment.Directories.RegisterDirectoryCreationHandler(handler);

        try
        {
            System.String parent = System.IO.Path.Combine(
                Nalix.Common.Infrastructure.Environment.Directories.DataDirectory, "evt_" + System.Guid.NewGuid().ToString("N"));

            System.String sub = Nalix.Common.Infrastructure.Environment.Directories.CreateSubdirectory(parent, "child");

            Xunit.Assert.True(System.IO.Directory.Exists(sub));
            Xunit.Assert.True(hits.Count >= 1);
            Xunit.Assert.Contains(sub, hits);
        }
        finally
        {
            Nalix.Common.Infrastructure.Environment.Directories.UnregisterDirectoryCreationHandler(handler);
        }
    }

    [Xunit.Fact]
    public void GetFilePath_Blocks_Path_Traversal()
    {
        System.String baseDir = Nalix.Common.Infrastructure.Environment.Directories.DataDirectory;
        System.String traversal = ".." + System.IO.Path.DirectorySeparatorChar + "evil.txt";

        void act() => _ = Nalix.Common.Infrastructure.Environment.Directories.GetFilePath(baseDir, traversal);

        _ = Xunit.Assert.Throws<System.UnauthorizedAccessException>(act);
    }

    [Xunit.Fact]
    public void CreateHierarchicalDateDirectory_Builds_Y_M_D()
    {
        System.String parent = Nalix.Common.Infrastructure.Environment.Directories.DataDirectory;
        System.String day = Nalix.Common.Infrastructure.Environment.Directories.CreateHierarchicalDateDirectory(parent);

        Xunit.Assert.True(System.IO.Directory.Exists(day));

        System.String rel = day[parent.Length..].Trim(System.IO.Path.DirectorySeparatorChar);
        System.String[] parts = rel.Split(System.IO.Path.DirectorySeparatorChar);

        Xunit.Assert.Equal(3, parts.Length);
        Xunit.Assert.Equal(4, parts[0].Length);
        Xunit.Assert.Equal(2, parts[1].Length);
        Xunit.Assert.Equal(2, parts[2].Length);
    }

    [Xunit.Fact]
    public void ValidateDirectories_Returns_True()
    {
        System.Boolean ok = Nalix.Common.Infrastructure.Environment.Directories.CanAccessAllDirectories();
        Xunit.Assert.True(ok);
    }
}
