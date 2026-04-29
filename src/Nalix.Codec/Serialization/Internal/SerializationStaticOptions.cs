// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.Options;
using Nalix.Environment.Configuration;

namespace Nalix.Codec.Serialization.Internal;

/// <summary>
/// Provides high-performance, cached access to serialization configuration for formatters.
/// </summary>
internal static class SerializationStaticOptions
{
    /// <summary>
    /// Gets the shared instance of serialization options.
    /// Since the manager re-initializes the same instance on reload, this reference remains valid.
    /// </summary>
    public static readonly SerializationOptions Instance = ConfigurationManager.Instance.Get<SerializationOptions>();
}
