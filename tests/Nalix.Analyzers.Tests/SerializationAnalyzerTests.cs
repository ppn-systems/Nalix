// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class SerializationAnalyzerTests
{
    [Fact]
    public async Task DuplicateSerializeOrder_ProducesDiagnosticAndFix()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeOrder(0)]
    public int A { get; set; }

    [SerializeOrder(0)]
    public int B { get; set; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeOrder(1)]
    public int A { get; set; }

    [SerializeOrder(0)]
    public int B { get; set; }
}
""";

        await Verifier<DuplicateSerializeOrderCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX014");
    }

    [Fact]
    public async Task MissingSerializeOrder_ProducesIgnoreFix()
    {
        const string source = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    public int Value { get; set; }
}
""";

        const string fixedSource = """
namespace Demo;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class Example
{
    [SerializeIgnore]
    public int Value { get; set; }
}
""";

        await Verifier<SerializeOrderMissingCodeFixProvider>.VerifyCodeFixAsync(
            source,
            fixedSource,
            "NALIX013");
    }
}
