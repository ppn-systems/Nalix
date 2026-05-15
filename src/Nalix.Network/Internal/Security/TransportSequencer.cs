// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Security;
using Nalix.Environment.Sequencing;

namespace Nalix.Network.Internal.Security;

/// <summary>
/// Default implementation of <see cref="ITransportSequencer"/>.
/// Provides independent sequence counters for send and receive directions
/// with built-in anti-replay protection.
/// </summary>
/// <remarks>
/// This class is thread-safe and suitable for use in high-performance networking scenarios.
/// </remarks>
internal struct TransportSequencer : ITransportSequencer
{
    private SequenceCounter _send;
    private SequenceCounter _receive;

    /// <inheritdoc />
    public readonly ISequenceCounter SendSequence => _send;

    /// <inheritdoc />
    public readonly ISequenceCounter ReceiveSequence => _receive;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportSequencer"/> class.
    /// Both counters start at sequence 0.
    /// </summary>
    public TransportSequencer()
    {
        _send = new SequenceCounter();
        _receive = new SequenceCounter();
    }
}
