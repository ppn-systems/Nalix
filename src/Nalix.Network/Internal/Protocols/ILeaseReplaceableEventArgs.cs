// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Common.Networking;

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Represents event args that allow replacing the underlying <see cref="IBufferLease"/>.
/// This is required for protocols that transform payloads (decrypt/decompress).
/// </summary>
internal interface ILeaseReplaceableEventArgs : IConnectEventArgs
{
    /// <summary>
    /// Replaces the current lease with a new one and returns the previous lease (if any).
    /// The caller becomes the owner of the returned lease and must dispose it.
    /// </summary>
    IBufferLease? ReplaceLease(IBufferLease? newLease);
}
