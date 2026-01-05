// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Core;
using Nalix.Logging.Options;

namespace Nalix.Logging.Extensions;

/// <summary>
/// Provides extension methods for health check integration.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Creates a health check for a distributor.
    /// </summary>
    /// <param name="distributor">The distributor to monitor.</param>
    /// <param name="options">Health check configuration options.</param>
    /// <returns>An instance of <see cref="ILoggingHealthCheck"/> for monitoring.</returns>
    public static ILoggingHealthCheck CreateHealthCheck(
        this NLogixDistributor distributor,
        HealthCheckOptions? options = null)
    {
        System.ArgumentNullException.ThrowIfNull(distributor);

        return new LoggingHealthCheck(distributor, options);
    }

    /// <summary>
    /// Creates a health check for a distributor with configuration action.
    /// </summary>
    /// <param name="distributor">The distributor to monitor.</param>
    /// <param name="configure">Action to configure health check options.</param>
    /// <returns>An instance of <see cref="ILoggingHealthCheck"/> for monitoring.</returns>
    public static ILoggingHealthCheck CreateHealthCheck(
        this NLogixDistributor distributor,
        System.Action<HealthCheckOptions> configure)
    {
        System.ArgumentNullException.ThrowIfNull(distributor);
        System.ArgumentNullException.ThrowIfNull(configure);

        var options = new HealthCheckOptions();
        configure(options);

        return new LoggingHealthCheck(distributor, options);
    }
}
