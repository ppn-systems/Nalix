using FluentAssertions;
using Nalix.Shared.Configuration.Internal;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Configuration;

public sealed class PropertyMetadataTests
{
    private sealed class Target
    {
        public Int32 Number { get; set; }
    }

    [Fact]
    public void SetValue_Throws_When_Type_Mismatch()
    {
        var pi = typeof(Target).GetProperty(nameof(Target.Number))!;
        var meta = new PropertyMetadata
        {
            Name = nameof(Target.Number),
            PropertyInfo = pi,
            PropertyType = pi.PropertyType,
            TypeCode = Type.GetTypeCode(pi.PropertyType)
        };

        var t = new Target();

        var act = () => meta.SetValue(t, "oops"); // string passed to int prop -> should throw (:contentReference[oaicite:30]{index=30})
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Type mismatch for property Number*");
    }

    [Fact]
    public void SetValue_Succeeds_When_Type_Compatible()
    {
        var pi = typeof(Target).GetProperty(nameof(Target.Number))!;
        var meta = new PropertyMetadata
        {
            Name = nameof(Target.Number),
            PropertyInfo = pi,
            PropertyType = pi.PropertyType,
            TypeCode = Type.GetTypeCode(pi.PropertyType)
        };

        var t = new Target();
        meta.SetValue(t, 123);
        t.Number.Should().Be(123);
    }
}
