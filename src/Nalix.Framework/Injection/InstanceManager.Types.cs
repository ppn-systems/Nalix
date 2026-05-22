// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace Nalix.Framework.Injection;

public sealed partial class InstanceManager
{
    #region Struct Keys

    /// <summary>
    /// Lightweight hashable key for constructor signature.
    /// </summary>
    private readonly struct ActivatorKey : IEquatable<ActivatorKey>
    {
        public readonly int Arity;
        public readonly RuntimeTypeHandle P0;
        public readonly RuntimeTypeHandle P1;
        public readonly RuntimeTypeHandle P2;
        public readonly RuntimeTypeHandle P3;
        public readonly RuntimeTypeHandle P4;
        public readonly RuntimeTypeHandle Target;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ActivatorKey(Type t, object?[]? args)
        {
            Target = t.TypeHandle;
            Arity = args?.Length ?? 0;

            P0 = default; P1 = default; P2 = default; P3 = default; P4 = default;
            if (Arity > 0)
            {
                P0 = (args![0]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 1)
            {
                P1 = (args![1]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 2)
            {
                P2 = (args![2]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 3)
            {
                P3 = (args![3]?.GetType() ?? typeof(object)).TypeHandle;
            }

            if (Arity > 4)
            {
                P4 = (args![4]?.GetType() ?? typeof(object)).TypeHandle;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ActivatorKey other)
            => Target.Equals(other.Target)
               && P0.Equals(other.P0)
               && P1.Equals(other.P1)
               && P2.Equals(other.P2)
               && P3.Equals(other.P3)
               && P4.Equals(other.P4)
               && Arity == other.Arity;

        public override bool Equals(object? obj)
            => obj is ActivatorKey k && this.Equals(k);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            HashCode hc = new();
            hc.Add(Target);
            hc.Add(Arity);
            hc.Add(P0);
            hc.Add(P1);
            hc.Add(P2);
            hc.Add(P3);
            hc.Add(P4);
            return hc.ToHashCode();
        }
    }

    #endregion Struct Keys
}

