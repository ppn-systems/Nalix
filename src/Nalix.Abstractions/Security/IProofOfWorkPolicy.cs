// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Security;

/// <summary>
/// Provides dynamic difficulty settings for the Proof-of-Work anti-DDoS system.
/// </summary>
public interface IProofOfWorkPolicy
{
    /// <summary>
    /// Gets the current Proof-of-Work difficulty dynamically adjusted based on server load.
    /// The difficulty corresponds to the required number of leading zero bits in the hash.
    /// </summary>
    byte CurrentDifficulty { get; }
}
