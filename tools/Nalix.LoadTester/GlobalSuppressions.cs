// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Globalization",
    "CA1303:Do not pass literals as localized parameters",
    Justification = "Nalix.LoadTester is a developer CLI tool with fixed diagnostic output.")]

[assembly: SuppressMessage(
    "Performance",
    "CA1849:Call async methods when in an async method",
    Justification = "Console error output during argument parsing is intentionally synchronous and minimal.")]

[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "The load tester must keep workers alive and classify unexpected transport failures as metrics.")]
