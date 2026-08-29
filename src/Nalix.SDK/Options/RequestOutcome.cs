// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

#pragma warning disable CA1000

namespace Nalix.SDK.Options;

/// <summary>
/// Classifies why a <see cref="Transport.Extensions.RequestExtensions.TryRequestAsync{TResponse}"/>
/// call did not (or did) produce a response.
/// </summary>
public enum RequestOutcomeKind
{
    /// <summary>A matching response was received.</summary>
    Ok,

    /// <summary>The client was not connected and no reconnect completed in time.</summary>
    NotConnected,

    /// <summary>No response arrived within the allotted timeout on all attempts.</summary>
    TimedOut,

    /// <summary>The request failed for another reason (send failure, disconnect mid-wait, etc.).</summary>
    Failed,
}

/// <summary>
/// Non-throwing result of a request/response exchange. Exactly one of <see cref="Value"/>
/// (when <see cref="Kind"/> is <see cref="RequestOutcomeKind.Ok"/>) or <see cref="Error"/>
/// (otherwise) is populated.
/// </summary>
/// <typeparam name="T">The expected response packet type.</typeparam>
public readonly struct RequestOutcome<T>
{
    /// <summary>Gets the classification of this outcome.</summary>
    public RequestOutcomeKind Kind { get; }

    /// <summary>Gets the response value when <see cref="Kind"/> is <see cref="RequestOutcomeKind.Ok"/>; otherwise <see langword="default"/>.</summary>
    public T? Value { get; }

    /// <summary>Gets the exception that caused a non-<see cref="RequestOutcomeKind.Ok"/> outcome; otherwise <see langword="null"/>.</summary>
    public Exception? Error { get; }

    private RequestOutcome(RequestOutcomeKind kind, T? value, Exception? error)
    {
        this.Kind = kind;
        this.Value = value;
        this.Error = error;
    }

    /// <summary>Creates a successful outcome wrapping <paramref name="value"/>.</summary>
    public static RequestOutcome<T> Ok(T value) => new(RequestOutcomeKind.Ok, value, null);

    /// <summary>Creates a failed outcome with the given <paramref name="kind"/> and <paramref name="error"/>.</summary>
    public static RequestOutcome<T> Fail(RequestOutcomeKind kind, Exception error) => new(kind, default, error);
}
