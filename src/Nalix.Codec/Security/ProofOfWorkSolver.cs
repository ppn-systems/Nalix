// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Nalix.Codec.Security.Hashing;

namespace Nalix.Codec.Security;

/// <summary>
/// Provides proof-of-work puzzle solving utilities for Nalix protocol handshakes.
/// </summary>
/// <remarks>
/// This type is intended for client-side use during proof-of-work negotiation.
/// It searches for a counter value that makes the Keccak-256 hash of the puzzle payload
/// satisfy the required number of leading zero bits.
/// </remarks>
public class ProofOfWorkSolver
{
    /// <summary>
    /// Solves the cryptographic puzzle by finding a solution that satisfies the difficulty condition.
    /// This method blocks until a valid solution is found.
    /// </summary>
    public static long SolveChallenge(ReadOnlySpan<byte> nonce, byte difficulty, long timestampTicks)
    {
        if (difficulty == 0)
        {
            return 0; // Trivial
        }

        Span<byte> puzzle = stackalloc byte[49];
        nonce.CopyTo(puzzle);
        puzzle[32] = difficulty;
        BinaryPrimitives.WriteInt64LittleEndian(puzzle.Slice(33, 8), timestampTicks);

        Span<byte> hash = stackalloc byte[32];
        Span<byte> solutionSpan = puzzle.Slice(41, 8);

        long solution = 0;
        while (true)
        {
            BinaryPrimitives.WriteInt64LittleEndian(solutionSpan, solution);
            Keccak256.HashData(puzzle, hash);

            if (HasLeadingZeroBits(hash, difficulty))
            {
                return solution;
            }

            solution++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasLeadingZeroBits(ReadOnlySpan<byte> hash, byte requiredZeroBits)
    {
        int bytesToCheck = requiredZeroBits / 8;
        int remainingBits = requiredZeroBits % 8;

        for (int i = 0; i < bytesToCheck; i++)
        {
            if (hash[i] != 0)
            {
                return false;
            }
        }

        if (remainingBits > 0)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((hash[bytesToCheck] & mask) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
