// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class AdvancedSerializationAnalyzerTests
{
    [Fact]
    public async Task MissingSerializeOrder_CanUseSecondCodeFixToAddNextOrder()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeOrder(0)]
    public int A { get; set; }

    public int Value { get; set; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeOrder(0)]
    public int A { get; set; }
    [SerializeOrder(1)]
    public int Value { get; set; }
}
""";

        await Verifier<CodeFixes.SerializeOrderMissingCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX013",
            actionIndex: 1);
    }

    [Fact]
    public async Task NegativeSerializeOrder_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeOrder(-1)]
    public int Value { get; set; }
}
""";

        await Verifier<CodeFixes.SerializeOrderMissingCodeFixProvider>.VerifyAnalyzerAsync(
            source,
            "NALIX021");
    }

}
