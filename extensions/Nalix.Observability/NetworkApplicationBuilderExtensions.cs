// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Hosting;

namespace Nalix.Observability;

/// <summary>
/// Provides extension methods to easily register observability features.
/// </summary>
public static class ObservabilityNetworkApplicationBuilderExtensions
{
    /// <summary>
    /// Registers observability handlers in the network application.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static INetworkApplicationBuilder UseObservability(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.AddHandler<ObservabilityAccessHandlers>();
        _ = builder.AddHandler<RuntimeObservationHandlers>();

        return builder;
    }
}
