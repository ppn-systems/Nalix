// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class ConfigurationAnalyzerTests
{
    [Fact]
    public async Task UnsupportedConfigurationProperty_ProducesDiagnosticAndFix()
    {
        const string source = """
namespace Demo;
using System.Collections.Generic;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public List<int> Values { get; set; } = [];
}
""";

        const string fixedSource = """
namespace Demo;
using System.Collections.Generic;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    [Nalix.Abstractions.ConfiguredIgnore]
    public List<int> Values { get; set; } = [];
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX023",
            actionIndex: 0,
            expectedTitle: "Add [ConfiguredIgnore]",
            expectedEquivalenceKey: "Nalix.Configuration.ConfiguredIgnore.Add");
    }

    [Fact]
    public async Task SupportedConfigurationProperties_DoNotProduceDiagnostic()
    {
        const string source = """
namespace Demo;
using System;
using Nalix.Environment.Configuration.Binding;

public enum Mode
{
    Off,
    On
}

public sealed class DemoOptions : ConfigurationLoader
{
    public int Port { get; set; }
    public string Host { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public TimeSpan Timeout { get; set; }
    public DateTime LastUpdated { get; set; }
    public Mode CurrentMode { get; set; }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnsupportedConfigurationProperty_WithConfigurationIgnore_IsSilent()
    {
        const string source = """
namespace Demo;
using System.Collections.Generic;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    [ConfiguredIgnore]
    public List<int> Values { get; set; } = [];
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task GetterOnlyOrNonPublicSetter_ProduceBindableInfoDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int ReadOnlyPort { get; }
    public int SecretPort { get; private set; }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX024",
            "NALIX024");
    }

    [Fact]
    public async Task GetterOnlyProperty_CanAddConfigurationIgnoreFix()
    {
        const string source = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int ReadOnlyPort { get; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    [Nalix.Abstractions.ConfiguredIgnore]
    public int ReadOnlyPort { get; }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX024",
            actionIndex: 0,
            expectedTitle: "Add [ConfiguredIgnore]",
            expectedEquivalenceKey: "Nalix.Configuration.ConfiguredIgnore.Add");
    }

    [Fact]
    public async Task GetterOnlyProperty_CanMakeSetterPublicFix()
    {
        const string source = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int ReadOnlyPort { get; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int ReadOnlyPort { get; set; }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX024",
            actionIndex: 1,
            expectedTitle: "Make setter public",
            expectedEquivalenceKey: "Nalix.Configuration.Setter.MakePublic");
    }

    [Fact]
    public async Task PrivateSetterProperty_CanMakeSetterPublicFix()
    {
        const string source = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int SecretPort { get; private set; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Environment.Configuration.Binding;

public sealed class DemoOptions : ConfigurationLoader
{
    public int SecretPort { get; set; }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX024",
            actionIndex: 1,
            expectedTitle: "Make setter public",
            expectedEquivalenceKey: "Nalix.Configuration.Setter.MakePublic");
    }
}













