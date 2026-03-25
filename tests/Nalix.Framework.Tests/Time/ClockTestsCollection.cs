// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Xunit;

namespace Nalix.Framework.Tests.Time;

/// <summary>
/// Collection definition for Clock tests to ensure they don't run in parallel.
/// Clock is a static singleton, so parallel tests can interfere with each other.
/// </summary>
[CollectionDefinition("ClockTests", DisableParallelization = true)]
public class ClockTestsCollection
{
}
