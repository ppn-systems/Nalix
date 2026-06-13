// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Codec.Security;

internal enum CipherError : byte
{
    Success = 0,
    EnvelopeTooShort,
    InvalidHeader,
    InvalidNonceLength,
    CiphertextTooShort,
    InvalidTagLength,
    AlgorithmMismatch,
    AuthenticationFailed,
    UnsupportedAlgorithm,
    DestinationTooSmall
}
