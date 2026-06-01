// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Primitives;

namespace Nalix.Abstractions.Security;

/// <summary>
/// Defines a store for loading and saving the asymmetric cryptography identity key.
/// </summary>
public interface ICertificateStore
{
    /// <summary>
    /// Loads the private key of the certificate from the specified path.
    /// If the certificate does not exist, the store may generate and save a new one.
    /// </summary>
    /// <param name="path">The location of the certificate file.</param>
    /// <returns>The private key as a 32-byte primitive.</returns>
    Bytes32 Load(string path);

    /// <summary>
    /// Saves a newly generated certificate key pair.
    /// </summary>
    /// <param name="path">The target path to write the private key certificate to.</param>
    /// <param name="privateKey">The 32-byte private key.</param>
    /// <param name="publicKey">The 32-byte public key.</param>
    void Save(string path, Bytes32 privateKey, Bytes32 publicKey);
}
