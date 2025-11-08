using FluentAssertions;
using Nalix.Shared.Configuration.Binding;
using Nalix.Shared.Configuration.Internal;
using Xunit;

namespace Nalix.Shared.Tests.Configuration;

public sealed class ConfigurationLoaderBindingTests
{
    [Fact]
    public void Initialize_Binds_From_Trimmed_SectionName()
    {
        using var tmp = new TempIniFile("""
        [GameSettings]
        Title = BoundTitle
        Port = 8100
        Enabled = yes
        Ratio = 3.14
        Level = 2
        LaunchAt = 2025-01-02T03:04:05.0000000Z
        Separator = :
        """);

        var ini = new IniConfig(tmp.Path);
        var cfg = new GameSettingsConfig();

        // Internal Initialize is accessible via InternalsVisibleTo (:contentReference[oaicite:19]{index=19})
        typeof(ConfigurationLoader)
            .GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cfg, new System.Object[] { ini });

        cfg.IsInitialized.Should().BeTrue();                              // (:contentReference[oaicite:20]{index=20})
        cfg.Title.Should().Be("BoundTitle");
        cfg.Port.Should().Be(8100);
        cfg.Enabled.Should().BeTrue();
        cfg.Ratio.Should().BeApproximately(3.14, 1e-9);
        cfg.Level.Should().Be(DemoLevel.Pro);                       // enum numeric underlying parse (:contentReference[oaicite:21]{index=21})
        cfg.Separator.Should().Be(':');
        cfg.Ignored.Should().Be("IGNORED");                               // not overwritten due to [ConfiguredIgnore] (:contentReference[oaicite:22]{index=22} :contentReference[oaicite:23]{index=23})
    }

    [Fact]
    public void Clone_Creates_Shallow_Copy_With_Flags()
    {
        var cfg = new GameSettingsConfig
        {
            Title = "T1",
            Port = 12,
            Enabled = true,
            Ratio = 9.9,
            Level = DemoLevel.Pro,
            Separator = '#'
        };

        // Mark initialized by invoking Initialize with empty INI
        using var tmp = new TempIniFile("[GameSettings]\n");
        var ini = new IniConfig(tmp.Path);
        typeof(ConfigurationLoader)
            .GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cfg, new System.Object[] { ini });

        var clone = cfg.Clone<GameSettingsConfig>(); // public API (:contentReference[oaicite:28]{index=28})
        clone.IsInitialized.Should().BeTrue();
        clone.Title.Should().Be("T1");
        clone.Port.Should().Be(12);
        clone.Enabled.Should().BeTrue();
        clone.Ratio.Should().Be(9.9);
        clone.Level.Should().Be(DemoLevel.Pro);
        clone.Separator.Should().Be('#');
    }
}
