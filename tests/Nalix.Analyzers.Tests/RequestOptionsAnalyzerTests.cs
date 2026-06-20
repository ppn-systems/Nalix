using Nalix.Environment.Memory;
// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using Nalix.Analyzers.CodeFixes;
using Xunit;

namespace Nalix.Analyzers.Tests;

public sealed class RequestOptionsAnalyzerTests
{
    [Fact]
    public async Task WithRetry_NegativeValue_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Options;

public sealed class Example
{
    public void Run()
    {
        _ = RequestOptions.Default.WithRetry(-1);
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX027");
    }

    [Fact]
    public async Task WithTimeout_NegativeValue_ProducesDiagnostic()
    {
        const string source = """
namespace Demo;
using Nalix.SDK.Options;

public sealed class Example
{
    public void Run()
    {
        _ = RequestOptions.Default.WithTimeout(-500);
    }
}
""";

        await Verifier<ConfigurationIgnoreCodeFixProvider>.VerifyAnalyzerAsync(source, "NALIX028");
    }
}



















