using FluentAssertions;
using Nalix.Shared.Configuration.Internal;
using System;
using System.IO;
using Xunit;

namespace Nalix.Shared.Tests.Configuration;

public sealed class IniConfigTests
{
    [Fact]
    public void Parse_Primitives_And_Enums_Success()
    {
        using var tmp = new TempIniFile("""
        [GameSettings]
        Title = My Game
        Port = 9000
        Enabled = true
        Ratio = 2.75
        LaunchAt = 2025-11-08T00:00:00.0000000Z
        Level = Pro
        Separator = ,
        """);

        var ini = new IniConfig(tmp.Path); // InternalsVisibleTo allows access. (:contentReference[oaicite:12]{index=12})

        ini.GetString("GameSettings", "Title").Should().Be("My Game");
        ini.GetInt32("GameSettings", "Port").Should().Be(9000);
        ini.GetBool("GameSettings", "Enabled").Should().BeTrue();
        ini.GetDouble("GameSettings", "Ratio").Should().Be(2.75);
        ini.GetDateTime("GameSettings", "LaunchAt")!.Value.ToUniversalTime().Should().Be(DateTime.Parse("2025-11-08T00:00:00Z").ToUniversalTime());
        // Enum by name via generic GetEnum<T>()
        ini.GetEnum<DemoLevel>("GameSettings", "Level").Should().Be(DemoLevel.Pro);
        ini.GetChar("GameSettings", "Separator").Should().Be(',');
    }

    [Fact]
    public void WriteValue_WritesOnlyWhenMissing_AndFlushes()
    {
        using var tmp = new TempIniFile("[S]\nA=1\n");
        var ini = new IniConfig(tmp.Path);

        // Existing key -> no overwrite
        ini.WriteValue("S", "A", 2);
        ini.GetInt32("S", "A").Should().Be(1);

        // Missing key -> write
        ini.WriteValue("S", "B", 999);
        ini.GetInt32("S", "B").Should().Be(999);
    }

    [Fact]
    public void Cache_IsCleared_And_Reload_PicksUpExternalChanges()
    {
        using var tmp = new TempIniFile("[S]\nFlag=true\n");
        var ini = new IniConfig(tmp.Path);

        ini.GetBool("S", "Flag").Should().BeTrue();

        // Simulate external file edit
        File.WriteAllText(tmp.Path, "[S]\nFlag=false\n");
        ini.Reload(); // (:contentReference[oaicite:13]{index=13})

        ini.GetBool("S", "Flag").Should().BeFalse();
        ini.ClearCache();
        ini.GetBool("S", "Flag").Should().BeFalse();
    }
}
