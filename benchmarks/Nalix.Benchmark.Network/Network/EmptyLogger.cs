// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Diagnostics.Models;
using System;

namespace Nalix.Benchmark.Network.Network;

public class EmptyLogger : ILogger
{
    public void Meta(String message) { }
    public void Meta(String format, params Object[] args) { }
    public void Meta(String message, EventId? eventId = null) { }

    public void Trace(String message) { }
    public void Trace(String format, params Object[] args) { }
    public void Trace(String message, EventId? eventId = null) { }

    public void Debug(String message) { }
    public void Debug(String format, params Object[] args) { }
    public void Debug(String message, EventId? eventId = null) { }

    public void Info(String message) { }
    public void Info(String format, params Object[] args) { }
    public void Info(String message, EventId? eventId = null) { }

    public void Warn(String message) { }
    public void Warn(String format, params Object[] args) { }
    public void Warn(String message, EventId? eventId = null) { }

    public void Error(String message, EventId? eventId = null) { }
    public void Error(String format, params Object[] args) { }
    public void Error(String message, Exception exception, EventId? eventId = null) { }

    public void Fatal(String format, params Object[] args) { }
    public void Fatal(String message, EventId? eventId = null) { }
    public void Fatal(String message, Exception exception, EventId? eventId = null) { }
}
