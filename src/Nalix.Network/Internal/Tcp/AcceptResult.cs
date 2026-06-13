// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking;

namespace Nalix.Network.Internal.Tcp;

/// <summary>
/// Represents the outcome of an inbound TCP accept operation.
/// </summary>
internal readonly struct AcceptResult
{
    /// <summary>
    /// Gets the accept operation result.
    /// </summary>
    public AcceptConnectionResult Result { get; }

    /// <summary>
    /// Gets the accepted connection when the operation succeeds; otherwise, <see langword="null"/>.
    /// </summary>
    public IConnection? Connection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AcceptResult"/> struct.
    /// </summary>
    /// <param name="result">The accept operation result.</param>
    /// <param name="connection">
    /// The accepted connection when <paramref name="result"/> is <see cref="AcceptConnectionResult.Accepted"/>;
    /// otherwise, <see langword="null"/>.
    /// </param>
    public AcceptResult(AcceptConnectionResult result, IConnection? connection)
    {
        this.Result = result;
        this.Connection = connection;
    }
}
