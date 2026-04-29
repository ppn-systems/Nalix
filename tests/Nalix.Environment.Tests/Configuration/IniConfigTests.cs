#if DEBUG
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Nalix.Environment.Configuration.Internal;
using Xunit;

namespace Nalix.Environment.Tests.Configuration;

public class IniConfigTests : IDisposable
{
    private readonly string _path;

    public IniConfigTests()
    {
        _path = Path.GetTempFileName();
        File.WriteAllText(_path, "[Section]\n; Comment\nKey=Value\n");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Reload_ClearsComments_PreventingMemoryLeak()
    {
        using IniConfig config = new IniConfig(_path);
        
        // Use reflection to check private _comments dictionary size
        var commentsField = typeof(IniConfig).GetField("_comments", BindingFlags.NonPublic | BindingFlags.Instance);
        var comments = (Dictionary<string, string>)commentsField.GetValue(config);

        Assert.Single(comments); // Initially has 1 comment

        // Reload multiple times
        for (int i = 0; i < 10; i++)
        {
            config.Reload();
        }

        // Should still be 1, not 11 or more
        Assert.Single(comments);
    }
}
#endif
