// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using Nalix.Analyzers.Generators;
using Xunit;

namespace Nalix.Analyzers.Tests;

/// <summary>
/// Layer 1 (generator-output) tests for <see cref="ConfigurationGenerator"/>.
/// </summary>
public sealed class ConfigurationGeneratorOutputTests
{
    private const string ConfigSource = """
        using Nalix.Abstractions;
        using Nalix.Abstractions.Validation;
        using Nalix.Environment.Configuration.Binding;

        namespace GenHarness.Config;

        [IniComment("Sample section")]
        public sealed partial class SampleConfig : ConfigurationLoader
        {
            [IniComment("Timeout in seconds")]
            [ValueRange(1, 100)]
            public int Timeout { get; set; } = 10;

            public string Name { get; set; } = "default";
        }
        """;

    [Fact]
    public void Generator_EmitsBindingMembers_WithNoDiagnostics()
    {
        GeneratorDriverHarness.Result result = GeneratorDriverHarness.Run(new ConfigurationGenerator(), ConfigSource);

        Assert.Empty(result.GeneratorDiagnostics);

        (string HintName, string Text) source = result.GeneratedSources.Single();
        Assert.Contains("BindProperties", source.Text);
        Assert.Contains("CopyPropertiesTo", source.Text);
        Assert.Contains("ValidateDataAnnotations", source.Text);
        Assert.Contains("Timeout", source.Text);

        GeneratorDriverHarness.AssertNoCompileErrors(result);
    }

    [Fact]
    public void Generator_ProducesNothing_ForClassNotDerivedFromConfigurationLoader()
    {
        const string source = """
            namespace GenHarness.Config.Empty;

            public sealed class NotAConfig
            {
                public int Value { get; set; }
            }
            """;

        GeneratorDriverHarness.Result result = GeneratorDriverHarness.Run(new ConfigurationGenerator(), source);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void Generator_IsDeterministic_AcrossTwoRuns() =>
        GeneratorDriverHarness.AssertDeterministic(new ConfigurationGenerator(), ConfigSource);
}
